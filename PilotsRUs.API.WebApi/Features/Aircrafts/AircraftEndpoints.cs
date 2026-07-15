using Microsoft.EntityFrameworkCore;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.Aircrafts;

namespace PilotsRUs.API.WebApi.Features.Aircrafts;

public static class AircraftEndpoints
{
    public static IEndpointRouteBuilder MapAircraftEndpoints(this IEndpointRouteBuilder app)
    {
        // No policy name - same as Manufacturers/AircraftModels, any authenticated user can manage aircraft.
        var group = app.MapGroup("/aircrafts").RequireAuthorization();

        group.MapGet("/", async (IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var aircraft = await dbContext.Aircraft
                .Include(a => a.AircraftModel).ThenInclude(m => m.Manufacturer)
                .Include(a => a.SoftwareDeveloper)
                .OrderBy(a => a.RegistrationNumber)
                .ToListAsync();
            return Results.Ok(aircraft.Select(ToResponse));
        }).WithName("GetAircraft");

        group.MapGet("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var aircraft = await dbContext.Aircraft
                .Include(a => a.AircraftModel).ThenInclude(m => m.Manufacturer)
                .Include(a => a.SoftwareDeveloper)
                .FirstOrDefaultAsync(a => a.Id == id);
            return aircraft is null ? Results.NotFound() : Results.Ok(ToResponse(aircraft));
        }).WithName("GetAircraftById");

        group.MapPost("/", async (CreateAircraftRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var aircraftModel = await dbContext.AircraftModels.Include(m => m.Manufacturer).FirstOrDefaultAsync(m => m.Id == request.AircraftModelId);
            if (aircraftModel is null)
            {
                return Results.BadRequest("The selected aircraft model does not exist.");
            }

            var softwareDeveloper = await dbContext.SoftwareDevelopers.FindAsync(request.SoftwareDeveloperId);
            if (softwareDeveloper is null)
            {
                return Results.BadRequest("The selected software developer does not exist.");
            }

            var registration = request.RegistrationNumber.ToUpperInvariant();

            if (await dbContext.Aircraft.AnyAsync(a => a.RegistrationNumber == registration))
            {
                return Results.Conflict($"An aircraft with registration number '{registration}' already exists.");
            }

            var aircraft = new Aircraft
            {
                Id = Guid.NewGuid(),
                RegistrationNumber = registration,
                PassengerCapacityEconomy = request.PassengerCapacityEconomy,
                PassengerCapacityBusiness = request.PassengerCapacityBusiness,
                PassengerCapacityFirst = request.PassengerCapacityFirst,
                CargoCapacityKg = request.CargoCapacityKg,
                AircraftModelId = request.AircraftModelId,
                SoftwareDeveloperId = request.SoftwareDeveloperId
            };
            dbContext.Aircraft.Add(aircraft);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Closes the TOCTOU gap between the AnyAsync check above and this insert.
                return Results.Conflict($"An aircraft with registration number '{registration}' already exists.");
            }

            var response = new AircraftResponse(
                aircraft.Id, aircraft.RegistrationNumber,
                aircraft.PassengerCapacityEconomy, aircraft.PassengerCapacityBusiness, aircraft.PassengerCapacityFirst, aircraft.CargoCapacityKg,
                aircraft.AircraftModelId, aircraftModel.Name, aircraftModel.Manufacturer.Name,
                aircraft.SoftwareDeveloperId, softwareDeveloper.Name);
            return Results.Created($"/aircrafts/{aircraft.Id}", response);
        }).WithName("CreateAircraft");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAircraftRequest request, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var aircraft = await dbContext.Aircraft.FindAsync(id);
            if (aircraft is null)
            {
                return Results.NotFound();
            }

            var aircraftModel = await dbContext.AircraftModels.Include(m => m.Manufacturer).FirstOrDefaultAsync(m => m.Id == request.AircraftModelId);
            if (aircraftModel is null)
            {
                return Results.BadRequest("The selected aircraft model does not exist.");
            }

            var softwareDeveloper = await dbContext.SoftwareDevelopers.FindAsync(request.SoftwareDeveloperId);
            if (softwareDeveloper is null)
            {
                return Results.BadRequest("The selected software developer does not exist.");
            }

            var registration = request.RegistrationNumber.ToUpperInvariant();

            if (await dbContext.Aircraft.AnyAsync(a => a.Id != id && a.RegistrationNumber == registration))
            {
                return Results.Conflict($"An aircraft with registration number '{registration}' already exists.");
            }

            aircraft.RegistrationNumber = registration;
            aircraft.PassengerCapacityEconomy = request.PassengerCapacityEconomy;
            aircraft.PassengerCapacityBusiness = request.PassengerCapacityBusiness;
            aircraft.PassengerCapacityFirst = request.PassengerCapacityFirst;
            aircraft.CargoCapacityKg = request.CargoCapacityKg;
            aircraft.AircraftModelId = request.AircraftModelId;
            aircraft.SoftwareDeveloperId = request.SoftwareDeveloperId;

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Results.Conflict($"An aircraft with registration number '{registration}' already exists.");
            }

            var response = new AircraftResponse(
                aircraft.Id, aircraft.RegistrationNumber,
                aircraft.PassengerCapacityEconomy, aircraft.PassengerCapacityBusiness, aircraft.PassengerCapacityFirst, aircraft.CargoCapacityKg,
                aircraft.AircraftModelId, aircraftModel.Name, aircraftModel.Manufacturer.Name,
                aircraft.SoftwareDeveloperId, softwareDeveloper.Name);
            return Results.Ok(response);
        }).WithName("UpdateAircraft");

        group.MapDelete("/{id:guid}", async (Guid id, IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var aircraft = await dbContext.Aircraft.FindAsync(id);
            if (aircraft is null)
            {
                return Results.NotFound();
            }

            // Hard delete - nothing references Aircraft yet. Once something does, this needs the same
            // Restrict-guard treatment DeleteManufacturer/DeleteAircraftModel/DeleteSoftwareDeveloper have.
            dbContext.Aircraft.Remove(aircraft);
            await dbContext.SaveChangesAsync();

            return Results.NoContent();
        }).WithName("DeleteAircraft");

        return app;
    }

    private static AircraftResponse ToResponse(Aircraft aircraft) => new(
        aircraft.Id, aircraft.RegistrationNumber,
        aircraft.PassengerCapacityEconomy, aircraft.PassengerCapacityBusiness, aircraft.PassengerCapacityFirst, aircraft.CargoCapacityKg,
        aircraft.AircraftModelId, aircraft.AircraftModel.Name, aircraft.AircraftModel.Manufacturer.Name,
        aircraft.SoftwareDeveloperId, aircraft.SoftwareDeveloper.Name);
}
