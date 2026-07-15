using Microsoft.EntityFrameworkCore;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.AircraftModels;

namespace PilotsRUs.API.WebApi.Features.AircraftModels;

public static class AircraftModelEndpoints
{
    public static IEndpointRouteBuilder MapAircraftModelEndpoints(this IEndpointRouteBuilder app)
    {
        // No policy name - same as Manufacturers, any authenticated user can manage aircraft models.
        var group = app.MapGroup("/aircraft-models").RequireAuthorization();

        group.MapGet("/", async (IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var models = await dbContext.AircraftModels
                .Include(a => a.Manufacturer)
                .OrderBy(a => a.Manufacturer.Name).ThenBy(a => a.Name)
                .Select(a => new AircraftModelResponse(a.Id, a.Name, a.IcaoTypeDesignator, a.ManufacturerId, a.Manufacturer.Name))
                .ToListAsync();
            return Results.Ok(models);
        }).WithName("GetAircraftModels");

        group.MapGet("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var model = await dbContext.AircraftModels.Include(a => a.Manufacturer).FirstOrDefaultAsync(a => a.Id == id);
            return model is null
                ? Results.NotFound()
                : Results.Ok(new AircraftModelResponse(model.Id, model.Name, model.IcaoTypeDesignator, model.ManufacturerId, model.Manufacturer.Name));
        }).WithName("GetAircraftModelById");

        group.MapPost("/", async (CreateAircraftModelRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var manufacturer = await dbContext.Manufacturers.FindAsync(request.ManufacturerId);
            if (manufacturer is null)
            {
                return Results.BadRequest("The selected manufacturer does not exist.");
            }

            var icao = string.IsNullOrWhiteSpace(request.IcaoTypeDesignator) ? null : request.IcaoTypeDesignator.ToUpperInvariant();

            var conflict = await FindConflictAsync(dbContext, manufacturer.Name, request.ManufacturerId, request.Name, icao, excludingId: null);
            if (conflict is not null)
            {
                return Results.Conflict(conflict);
            }

            var model = new AircraftModel { Id = Guid.NewGuid(), ManufacturerId = request.ManufacturerId, Name = request.Name, IcaoTypeDesignator = icao };
            dbContext.AircraftModels.Add(model);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the checks above and this insert.
                return Results.Conflict($"'{manufacturer.Name}' already has a model named '{request.Name}', or its ICAO type designator is already in use.");
            }

            var response = new AircraftModelResponse(model.Id, model.Name, model.IcaoTypeDesignator, model.ManufacturerId, manufacturer.Name);
            return Results.Created($"/aircraft-models/{model.Id}", response);
        }).WithName("CreateAircraftModel");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAircraftModelRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var model = await dbContext.AircraftModels.FindAsync(id);
            if (model is null)
            {
                return Results.NotFound();
            }

            var manufacturer = await dbContext.Manufacturers.FindAsync(request.ManufacturerId);
            if (manufacturer is null)
            {
                return Results.BadRequest("The selected manufacturer does not exist.");
            }

            var icao = string.IsNullOrWhiteSpace(request.IcaoTypeDesignator) ? null : request.IcaoTypeDesignator.ToUpperInvariant();

            var conflict = await FindConflictAsync(dbContext, manufacturer.Name, request.ManufacturerId, request.Name, icao, excludingId: id);
            if (conflict is not null)
            {
                return Results.Conflict(conflict);
            }

            model.Name = request.Name;
            model.IcaoTypeDesignator = icao;
            model.ManufacturerId = request.ManufacturerId;

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Results.Conflict($"'{manufacturer.Name}' already has a model named '{request.Name}', or its ICAO type designator is already in use.");
            }

            return Results.Ok(new AircraftModelResponse(model.Id, model.Name, model.IcaoTypeDesignator, model.ManufacturerId, manufacturer.Name));
        }).WithName("UpdateAircraftModel");

        group.MapDelete("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var model = await dbContext.AircraftModels.FindAsync(id);
            if (model is null)
            {
                return Results.NotFound();
            }

            // Blocks deletion rather than cascading - same reasoning as Manufacturer -> AircraftModel. The
            // FK itself uses DeleteBehavior.Restrict; this pre-check turns what would otherwise be an
            // unhandled 500 (FK violation) into a clean 409.
            if (await dbContext.Aircraft.AnyAsync(a => a.AircraftModelId == id))
            {
                return Results.Conflict($"Cannot delete '{model.Name}' - it still has aircraft. Delete or reassign them first.");
            }

            dbContext.AircraftModels.Remove(model);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the AnyAsync check above and this delete.
                return Results.Conflict($"Cannot delete '{model.Name}' - it still has aircraft. Delete or reassign them first.");
            }

            return Results.NoContent();
        }).WithName("DeleteAircraftModel");

        return app;
    }

    private static async Task<string?> FindConflictAsync(ApplicationDbContext dbContext, string manufacturerName, Guid manufacturerId, string name, string? icao, Guid? excludingId)
    {
        if (await dbContext.AircraftModels.AnyAsync(a => a.Id != excludingId && a.ManufacturerId == manufacturerId && a.Name == name))
        {
            return $"'{manufacturerName}' already has a model named '{name}'.";
        }

        if (icao is not null && await dbContext.AircraftModels.AnyAsync(a => a.Id != excludingId && a.IcaoTypeDesignator == icao))
        {
            return $"ICAO type designator '{icao}' is already in use by another aircraft model.";
        }

        return null;
    }
}
