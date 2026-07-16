using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PilotsRUs.Shared.SDK.Schedules;
using PilotsRUs.User.App.Data;
using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.Tests.Services;

public sealed class FlightAssignmentServiceTests
{
    private static IFlightAssignmentService CreateService(string databaseName)
    {
        var options = new DbContextOptionsBuilder<UserAppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var dbContextFactory = new PooledDbContextFactory<UserAppDbContext>(options);
        return new FlightAssignmentService(dbContextFactory);
    }

    private static ScheduleResponse CreateSchedule() => new(
        Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 3, 1), "PR100",
        "ESSA", "Stockholm Arlanda", "EGLL", "London Heathrow",
        Guid.NewGuid(), "SE-ABC", 800, TimeSpan.FromHours(2),
        150, 20, 8, 5000);

    [Fact]
    public async Task AssignAsync_PersistsRowWithDenormalizedFieldsAndBoundedNumbers()
    {
        var service = CreateService(nameof(AssignAsync_PersistsRowWithDenormalizedFieldsAndBoundedNumbers));
        var schedule = CreateSchedule();

        var assignment = await service.AssignAsync(schedule);

        Assert.Equal(schedule.Id, assignment.ScheduleId);
        Assert.Equal(schedule.FlightNumber, assignment.FlightNumber);
        Assert.Equal(schedule.DepartureAirportIcaoCode, assignment.DepartureAirportIcaoCode);
        Assert.Equal(schedule.ArrivalAirportIcaoCode, assignment.ArrivalAirportIcaoCode);
        Assert.Equal(schedule.FlightDate, assignment.FlightDate);
        Assert.Equal(schedule.AircraftRegistrationNumber, assignment.AircraftRegistrationNumber);
        Assert.InRange(assignment.AssignedPassengersEconomy, 60, 150);
        Assert.InRange(assignment.AssignedPassengersBusiness, 8, 20);
        Assert.InRange(assignment.AssignedPassengersFirst, 4, 8);
        Assert.InRange(assignment.AssignedCargoKg, 2000, 5000);

        var all = await service.GetAllAsync();
        Assert.Contains(all, a => a.Id == assignment.Id);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAssignmentsNewestFirst()
    {
        var service = CreateService(nameof(GetAllAsync_ReturnsAssignmentsNewestFirst));

        var first = await service.AssignAsync(CreateSchedule());
        await Task.Delay(10);
        var second = await service.AssignAsync(CreateSchedule());

        var all = await service.GetAllAsync();

        Assert.Equal(second.Id, all[0].Id);
        Assert.Equal(first.Id, all[1].Id);
    }
}
