using Microsoft.EntityFrameworkCore;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.Manufacturers;

namespace PilotsRUs.API.WebApi.Features.Manufacturers;

public static class ManufacturerEndpoints
{
    public static IEndpointRouteBuilder MapManufacturerEndpoints(this IEndpointRouteBuilder app)
    {
        // No policy name - unlike Users' "AdminOnly", any authenticated user can manage manufacturers.
        var group = app.MapGroup("/manufacturers").RequireAuthorization();

        group.MapGet("/", async (IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var manufacturers = await dbContext.Manufacturers
                .OrderBy(m => m.Name)
                .Select(m => new ManufacturerResponse(m.Id, m.Name, m.Code))
                .ToListAsync();
            return Results.Ok(manufacturers);
        }).WithName("GetManufacturers");

        group.MapGet("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var manufacturer = await dbContext.Manufacturers.FindAsync(id);
            return manufacturer is null
                ? Results.NotFound()
                : Results.Ok(new ManufacturerResponse(manufacturer.Id, manufacturer.Name, manufacturer.Code));
        }).WithName("GetManufacturerById");

        group.MapPost("/", async (CreateManufacturerRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            if (await dbContext.Manufacturers.AnyAsync(m => m.Name == request.Name))
            {
                return Results.Conflict($"A manufacturer named '{request.Name}' already exists.");
            }

            var manufacturer = new Manufacturer { Id = Guid.NewGuid(), Name = request.Name, Code = request.Code };
            dbContext.Manufacturers.Add(manufacturer);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the AnyAsync check above and this insert - two concurrent
                // creates for the same Name would otherwise surface as an unhandled 500 from the unique
                // index violation instead of a clean 409.
                return Results.Conflict($"A manufacturer named '{request.Name}' already exists.");
            }

            var response = new ManufacturerResponse(manufacturer.Id, manufacturer.Name, manufacturer.Code);
            return Results.Created($"/manufacturers/{manufacturer.Id}", response);
        }).WithName("CreateManufacturer");

        group.MapPut("/{id:guid}", async (Guid id, UpdateManufacturerRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var manufacturer = await dbContext.Manufacturers.FindAsync(id);
            if (manufacturer is null)
            {
                return Results.NotFound();
            }

            if (await dbContext.Manufacturers.AnyAsync(m => m.Id != id && m.Name == request.Name))
            {
                return Results.Conflict($"A manufacturer named '{request.Name}' already exists.");
            }

            manufacturer.Name = request.Name;
            manufacturer.Code = request.Code;

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Results.Conflict($"A manufacturer named '{request.Name}' already exists.");
            }

            return Results.Ok(new ManufacturerResponse(manufacturer.Id, manufacturer.Name, manufacturer.Code));
        }).WithName("UpdateManufacturer");

        group.MapDelete("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var manufacturer = await dbContext.Manufacturers.FindAsync(id);
            if (manufacturer is null)
            {
                return Results.NotFound();
            }

            // Blocks deletion rather than cascading - an AircraftModel (e.g. "Boeing 737 MAX 8") is itself
            // meaningful reference data, not disposable state the way RefreshTokens are for ApplicationUser.
            // The FK itself uses DeleteBehavior.Restrict; this pre-check turns what would otherwise be an
            // unhandled 500 (FK violation) into a clean 409.
            if (await dbContext.AircraftModels.AnyAsync(a => a.ManufacturerId == id))
            {
                return Results.Conflict($"Cannot delete '{manufacturer.Name}' - it still has aircraft models. Delete or reassign them first.");
            }

            dbContext.Manufacturers.Remove(manufacturer);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the AnyAsync check above and this delete (a model could be
                // created concurrently between the check and the delete).
                return Results.Conflict($"Cannot delete '{manufacturer.Name}' - it still has aircraft models. Delete or reassign them first.");
            }

            return Results.NoContent();
        }).WithName("DeleteManufacturer");

        return app;
    }
}
