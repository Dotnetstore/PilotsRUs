using Microsoft.EntityFrameworkCore;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.Countries;

namespace PilotsRUs.API.WebApi.Features.Countries;

public static class CountryEndpoints
{
    public static IEndpointRouteBuilder MapCountryEndpoints(this IEndpointRouteBuilder app)
    {
        // No policy name - same as Manufacturers/AircraftModels, any authenticated user can manage countries.
        var group = app.MapGroup("/countries").RequireAuthorization();

        group.MapGet("/", async (IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var countries = await dbContext.Countries
                .OrderBy(c => c.Name)
                .Select(c => new CountryResponse(c.Id, c.Name, c.IsoAlpha2Code, c.IsoAlpha3Code))
                .ToListAsync();
            return Results.Ok(countries);
        }).WithName("GetCountries");

        group.MapGet("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var country = await dbContext.Countries.FindAsync(id);
            return country is null
                ? Results.NotFound()
                : Results.Ok(new CountryResponse(country.Id, country.Name, country.IsoAlpha2Code, country.IsoAlpha3Code));
        }).WithName("GetCountryById");

        group.MapPost("/", async (CreateCountryRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var alpha2 = request.IsoAlpha2Code.ToUpperInvariant();
            var alpha3 = request.IsoAlpha3Code.ToUpperInvariant();

            var conflict = await FindConflictAsync(dbContext, request.Name, alpha2, alpha3, excludingId: null);
            if (conflict is not null)
            {
                return Results.Conflict(conflict);
            }

            var country = new Country { Id = Guid.NewGuid(), Name = request.Name, IsoAlpha2Code = alpha2, IsoAlpha3Code = alpha3 };
            dbContext.Countries.Add(country);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the checks above and this insert.
                return Results.Conflict("A country with the same name or ISO code already exists.");
            }

            var response = new CountryResponse(country.Id, country.Name, country.IsoAlpha2Code, country.IsoAlpha3Code);
            return Results.Created($"/countries/{country.Id}", response);
        }).WithName("CreateCountry");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCountryRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var country = await dbContext.Countries.FindAsync(id);
            if (country is null)
            {
                return Results.NotFound();
            }

            var alpha2 = request.IsoAlpha2Code.ToUpperInvariant();
            var alpha3 = request.IsoAlpha3Code.ToUpperInvariant();

            var conflict = await FindConflictAsync(dbContext, request.Name, alpha2, alpha3, excludingId: id);
            if (conflict is not null)
            {
                return Results.Conflict(conflict);
            }

            country.Name = request.Name;
            country.IsoAlpha2Code = alpha2;
            country.IsoAlpha3Code = alpha3;

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Results.Conflict("A country with the same name or ISO code already exists.");
            }

            return Results.Ok(new CountryResponse(country.Id, country.Name, country.IsoAlpha2Code, country.IsoAlpha3Code));
        }).WithName("UpdateCountry");

        group.MapDelete("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var country = await dbContext.Countries.FindAsync(id);
            if (country is null)
            {
                return Results.NotFound();
            }

            // Blocks deletion rather than cascading - same reasoning as Manufacturer -> AircraftModel. The
            // FK itself uses DeleteBehavior.Restrict; this pre-check turns what would otherwise be an
            // unhandled 500 (FK violation) into a clean 409.
            if (await dbContext.Airports.AnyAsync(a => a.CountryId == id))
            {
                return Results.Conflict($"Cannot delete '{country.Name}' - it still has airports. Delete or reassign them first.");
            }

            dbContext.Countries.Remove(country);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the AnyAsync check above and this delete.
                return Results.Conflict($"Cannot delete '{country.Name}' - it still has airports. Delete or reassign them first.");
            }

            return Results.NoContent();
        }).WithName("DeleteCountry");

        return app;
    }

    private static async Task<string?> FindConflictAsync(ApplicationDbContext dbContext, string name, string alpha2, string alpha3, Guid? excludingId)
    {
        // Single round-trip for all three uniqueness rules - each column has its own unique index, so at
        // most a couple of rows can ever come back here, and the in-memory checks below preserve the
        // documented Name -> Alpha2 -> Alpha3 "first match wins" precedence.
        var conflicts = await dbContext.Countries
            .Where(c => c.Id != excludingId && (c.Name == name || c.IsoAlpha2Code == alpha2 || c.IsoAlpha3Code == alpha3))
            .Select(c => new { c.Name, c.IsoAlpha2Code, c.IsoAlpha3Code })
            .ToListAsync();

        if (conflicts.Any(c => c.Name == name))
        {
            return $"A country named '{name}' already exists.";
        }

        if (conflicts.Any(c => c.IsoAlpha2Code == alpha2))
        {
            return $"ISO alpha-2 code '{alpha2}' is already in use.";
        }

        if (conflicts.Any(c => c.IsoAlpha3Code == alpha3))
        {
            return $"ISO alpha-3 code '{alpha3}' is already in use.";
        }

        return null;
    }
}
