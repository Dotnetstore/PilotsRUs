using Microsoft.EntityFrameworkCore;
using PilotsRUs.Shared.SDK.Schedules;
using PilotsRUs.User.App.Data;

namespace PilotsRUs.User.App.Services;

public interface IFlightAssignmentService
{
    Task<FlightAssignment> AssignAsync(ScheduleResponse schedule, CancellationToken ct = default);
    Task<IReadOnlyList<FlightAssignment>> GetAllAsync(CancellationToken ct = default);
}

// Wraps FlightAssignmentGenerator's pure random logic with the actual local SQLite write, and the
// read-back "My Flights" needs. IDbContextFactory<UserAppDbContext>, not a scoped DbContext - matches
// CLAUDE.md's factory convention (see API.WebApi's non-Identity repository code).
public sealed class FlightAssignmentService(IDbContextFactory<UserAppDbContext> dbContextFactory) : IFlightAssignmentService
{
    public async Task<FlightAssignment> AssignAsync(ScheduleResponse schedule, CancellationToken ct = default)
    {
        var numbers = FlightAssignmentGenerator.Generate(
            schedule.PassengerCapacityEconomy, schedule.PassengerCapacityBusiness,
            schedule.PassengerCapacityFirst, schedule.CargoCapacityKg);

        var assignment = new FlightAssignment
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            FlightNumber = schedule.FlightNumber,
            DepartureAirportIcaoCode = schedule.DepartureAirportIcaoCode,
            ArrivalAirportIcaoCode = schedule.ArrivalAirportIcaoCode,
            FlightDate = schedule.FlightDate,
            AircraftRegistrationNumber = schedule.AircraftRegistrationNumber,
            AssignedPassengersEconomy = numbers.Economy,
            AssignedPassengersBusiness = numbers.Business,
            AssignedPassengersFirst = numbers.First,
            AssignedCargoKg = numbers.CargoKg,
            AssignedAtUtc = DateTimeOffset.UtcNow
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        dbContext.FlightAssignments.Add(assignment);
        await dbContext.SaveChangesAsync(ct);

        return assignment;
    }

    public async Task<IReadOnlyList<FlightAssignment>> GetAllAsync(CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);
        return await dbContext.FlightAssignments
            .OrderByDescending(a => a.AssignedAtUtc)
            .ToListAsync(ct);
    }
}
