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

            if (await dbContext.AircraftModels.AnyAsync(a => a.ManufacturerId == request.ManufacturerId && a.Name == request.Name))
            {
                return Results.Conflict($"'{manufacturer.Name}' already has a model named '{request.Name}'.");
            }

            var model = new AircraftModel { Id = Guid.NewGuid(), ManufacturerId = request.ManufacturerId, Name = request.Name, IcaoTypeDesignator = request.IcaoTypeDesignator };
            dbContext.AircraftModels.Add(model);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the AnyAsync check above and this insert.
                return Results.Conflict($"'{manufacturer.Name}' already has a model named '{request.Name}'.");
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

            if (await dbContext.AircraftModels.AnyAsync(a => a.Id != id && a.ManufacturerId == request.ManufacturerId && a.Name == request.Name))
            {
                return Results.Conflict($"'{manufacturer.Name}' already has a model named '{request.Name}'.");
            }

            model.Name = request.Name;
            model.IcaoTypeDesignator = request.IcaoTypeDesignator;
            model.ManufacturerId = request.ManufacturerId;

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Results.Conflict($"'{manufacturer.Name}' already has a model named '{request.Name}'.");
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

            // Hard delete, same reasoning as Manufacturer - no session/lockout state tied to a lookup
            // entity. Once a future Aircraft entity references AircraftModel via FK, this needs the same
            // Restrict-guard treatment DeleteManufacturer now has.
            dbContext.AircraftModels.Remove(model);
            await dbContext.SaveChangesAsync();

            return Results.NoContent();
        }).WithName("DeleteAircraftModel");

        return app;
    }
}
