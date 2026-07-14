using Microsoft.EntityFrameworkCore;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.Airports;

namespace PilotsRUs.API.WebApi.Features.Airports;

public static class AirportEndpoints
{
    public static IEndpointRouteBuilder MapAirportEndpoints(this IEndpointRouteBuilder app)
    {
        // No policy name - same as Manufacturers/AircraftModels/Countries, any authenticated user can
        // manage airports.
        var group = app.MapGroup("/airports").RequireAuthorization();

        group.MapGet("/", async (IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var airports = await dbContext.Airports
                .Include(a => a.Country)
                .OrderBy(a => a.Name)
                .Select(a => new AirportResponse(a.Id, a.Name, a.IcaoCode, a.IataCode, a.City, a.CountryId, a.Country.Name))
                .ToListAsync();
            return Results.Ok(airports);
        }).WithName("GetAirports");

        group.MapGet("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var airport = await dbContext.Airports.Include(a => a.Country).FirstOrDefaultAsync(a => a.Id == id);
            return airport is null
                ? Results.NotFound()
                : Results.Ok(new AirportResponse(airport.Id, airport.Name, airport.IcaoCode, airport.IataCode, airport.City, airport.CountryId, airport.Country.Name));
        }).WithName("GetAirportById");

        group.MapPost("/", async (CreateAirportRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var country = await dbContext.Countries.FindAsync(request.CountryId);
            if (country is null)
            {
                return Results.BadRequest("The selected country does not exist.");
            }

            var icao = request.IcaoCode.ToUpperInvariant();
            var iata = string.IsNullOrWhiteSpace(request.IataCode) ? null : request.IataCode.ToUpperInvariant();

            var conflict = await FindConflictAsync(dbContext, icao, iata, excludingId: null);
            if (conflict is not null)
            {
                return Results.Conflict(conflict);
            }

            var airport = new Airport { Id = Guid.NewGuid(), Name = request.Name, IcaoCode = icao, IataCode = iata, City = request.City, CountryId = request.CountryId };
            dbContext.Airports.Add(airport);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the checks above and this insert.
                return Results.Conflict("An airport with the same ICAO or IATA code already exists.");
            }

            var response = new AirportResponse(airport.Id, airport.Name, airport.IcaoCode, airport.IataCode, airport.City, airport.CountryId, country.Name);
            return Results.Created($"/airports/{airport.Id}", response);
        }).WithName("CreateAirport");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAirportRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var airport = await dbContext.Airports.FindAsync(id);
            if (airport is null)
            {
                return Results.NotFound();
            }

            var country = await dbContext.Countries.FindAsync(request.CountryId);
            if (country is null)
            {
                return Results.BadRequest("The selected country does not exist.");
            }

            var icao = request.IcaoCode.ToUpperInvariant();
            var iata = string.IsNullOrWhiteSpace(request.IataCode) ? null : request.IataCode.ToUpperInvariant();

            var conflict = await FindConflictAsync(dbContext, icao, iata, excludingId: id);
            if (conflict is not null)
            {
                return Results.Conflict(conflict);
            }

            airport.Name = request.Name;
            airport.IcaoCode = icao;
            airport.IataCode = iata;
            airport.City = request.City;
            airport.CountryId = request.CountryId;

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Results.Conflict("An airport with the same ICAO or IATA code already exists.");
            }

            return Results.Ok(new AirportResponse(airport.Id, airport.Name, airport.IcaoCode, airport.IataCode, airport.City, airport.CountryId, country.Name));
        }).WithName("UpdateAirport");

        group.MapDelete("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var airport = await dbContext.Airports.FindAsync(id);
            if (airport is null)
            {
                return Results.NotFound();
            }

            // Hard delete - no FK references Airport yet.
            dbContext.Airports.Remove(airport);
            await dbContext.SaveChangesAsync();

            return Results.NoContent();
        }).WithName("DeleteAirport");

        return app;
    }

    private static async Task<string?> FindConflictAsync(ApplicationDbContext dbContext, string icao, string? iata, Guid? excludingId)
    {
        if (await dbContext.Airports.AnyAsync(a => a.Id != excludingId && a.IcaoCode == icao))
        {
            return $"An airport with ICAO code '{icao}' already exists.";
        }

        if (iata is not null && await dbContext.Airports.AnyAsync(a => a.Id != excludingId && a.IataCode == iata))
        {
            return $"An airport with IATA code '{iata}' already exists.";
        }

        return null;
    }
}
