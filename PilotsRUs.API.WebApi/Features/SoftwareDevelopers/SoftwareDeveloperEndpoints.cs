using Microsoft.EntityFrameworkCore;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.SoftwareDevelopers;

namespace PilotsRUs.API.WebApi.Features.SoftwareDevelopers;

public static class SoftwareDeveloperEndpoints
{
    public static IEndpointRouteBuilder MapSoftwareDeveloperEndpoints(this IEndpointRouteBuilder app)
    {
        // No policy name - same as Manufacturers, any authenticated user can manage software developers.
        var group = app.MapGroup("/software-developers").RequireAuthorization();

        group.MapGet("/", async (IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var softwareDevelopers = await dbContext.SoftwareDevelopers
                .OrderBy(s => s.Name)
                .Select(s => new SoftwareDeveloperResponse(s.Id, s.Name))
                .ToListAsync();
            return Results.Ok(softwareDevelopers);
        }).WithName("GetSoftwareDevelopers");

        group.MapGet("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var softwareDeveloper = await dbContext.SoftwareDevelopers.FindAsync(id);
            return softwareDeveloper is null
                ? Results.NotFound()
                : Results.Ok(new SoftwareDeveloperResponse(softwareDeveloper.Id, softwareDeveloper.Name));
        }).WithName("GetSoftwareDeveloperById");

        group.MapPost("/", async (CreateSoftwareDeveloperRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            if (await dbContext.SoftwareDevelopers.AnyAsync(s => s.Name == request.Name))
            {
                return Results.Conflict($"A software developer named '{request.Name}' already exists.");
            }

            var softwareDeveloper = new SoftwareDeveloper { Id = Guid.NewGuid(), Name = request.Name };
            dbContext.SoftwareDevelopers.Add(softwareDeveloper);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the AnyAsync check above and this insert.
                return Results.Conflict($"A software developer named '{request.Name}' already exists.");
            }

            var response = new SoftwareDeveloperResponse(softwareDeveloper.Id, softwareDeveloper.Name);
            return Results.Created($"/software-developers/{softwareDeveloper.Id}", response);
        }).WithName("CreateSoftwareDeveloper");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSoftwareDeveloperRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var softwareDeveloper = await dbContext.SoftwareDevelopers.FindAsync(id);
            if (softwareDeveloper is null)
            {
                return Results.NotFound();
            }

            if (await dbContext.SoftwareDevelopers.AnyAsync(s => s.Id != id && s.Name == request.Name))
            {
                return Results.Conflict($"A software developer named '{request.Name}' already exists.");
            }

            softwareDeveloper.Name = request.Name;

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Results.Conflict($"A software developer named '{request.Name}' already exists.");
            }

            return Results.Ok(new SoftwareDeveloperResponse(softwareDeveloper.Id, softwareDeveloper.Name));
        }).WithName("UpdateSoftwareDeveloper");

        group.MapDelete("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var softwareDeveloper = await dbContext.SoftwareDevelopers.FindAsync(id);
            if (softwareDeveloper is null)
            {
                return Results.NotFound();
            }

            // Blocks deletion rather than cascading - same reasoning as Manufacturer -> AircraftModel. The
            // FK itself uses DeleteBehavior.Restrict; this pre-check turns what would otherwise be an
            // unhandled 500 (FK violation) into a clean 409.
            if (await dbContext.Aircraft.AnyAsync(a => a.SoftwareDeveloperId == id))
            {
                return Results.Conflict($"Cannot delete '{softwareDeveloper.Name}' - it still has aircraft. Delete or reassign them first.");
            }

            dbContext.SoftwareDevelopers.Remove(softwareDeveloper);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the AnyAsync check above and this delete.
                return Results.Conflict($"Cannot delete '{softwareDeveloper.Name}' - it still has aircraft. Delete or reassign them first.");
            }

            return Results.NoContent();
        }).WithName("DeleteSoftwareDeveloper");

        return app;
    }
}
