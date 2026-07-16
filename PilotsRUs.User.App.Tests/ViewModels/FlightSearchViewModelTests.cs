using PilotsRUs.Shared.SDK.Schedules;
using PilotsRUs.User.App.Data;
using PilotsRUs.User.App.Services;
using PilotsRUs.User.App.Tests.Services;
using PilotsRUs.User.App.ViewModels;

namespace PilotsRUs.User.App.Tests.ViewModels;

public sealed class FlightSearchViewModelTests
{
    private static ScheduleResponse CreateSchedule() => new(
        Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 3, 1), "PR100",
        "ESSA", "Stockholm Arlanda", "EGLL", "London Heathrow",
        Guid.NewGuid(), "SE-ABC", 800, TimeSpan.FromHours(2),
        150, 20, 8, 5000);

    [Fact]
    public async Task SearchAsync_WhenApiSucceeds_PopulatesResults()
    {
        var schedule = CreateSchedule();
        var apiClient = new FakeScheduleApiClient
        {
            SearchResult = ApiResult<IReadOnlyList<ScheduleResponse>>.Ok([schedule])
        };
        var vm = new FlightSearchViewModel(apiClient, new FakeFlightAssignmentService(), onFlightAssigned: _ => { }, onBackToShell: () => { });

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Single(vm.Results);
        Assert.Equal(schedule.Id, vm.Results[0].Id);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task SearchAsync_WhenApiFails_SetsErrorMessage()
    {
        var apiClient = new FakeScheduleApiClient
        {
            SearchResult = ApiResult<IReadOnlyList<ScheduleResponse>>.Fail("Failed to search flights.")
        };
        var vm = new FlightSearchViewModel(apiClient, new FakeFlightAssignmentService(), onFlightAssigned: _ => { }, onBackToShell: () => { });

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal("Failed to search flights.", vm.ErrorMessage);
        Assert.Empty(vm.Results);
    }

    [Fact]
    public async Task SelectFlightAsync_CallsAssignmentServiceAndInvokesCallback()
    {
        var schedule = CreateSchedule();
        var assignment = new FlightAssignment
        {
            Id = Guid.NewGuid(), ScheduleId = schedule.Id, FlightNumber = schedule.FlightNumber,
            DepartureAirportIcaoCode = schedule.DepartureAirportIcaoCode, ArrivalAirportIcaoCode = schedule.ArrivalAirportIcaoCode,
            FlightDate = schedule.FlightDate, AircraftRegistrationNumber = schedule.AircraftRegistrationNumber,
            AssignedPassengersEconomy = 100, AssignedPassengersBusiness = 15, AssignedPassengersFirst = 5, AssignedCargoKg = 3000,
            AssignedAtUtc = DateTimeOffset.UtcNow
        };
        var flightAssignmentService = new FakeFlightAssignmentService { AssignResult = assignment };
        FlightAssignment? assigned = null;
        var vm = new FlightSearchViewModel(new FakeScheduleApiClient(), flightAssignmentService, onFlightAssigned: a => assigned = a, onBackToShell: () => { });

        await vm.SelectFlightCommand.ExecuteAsync(schedule);

        Assert.Equal(schedule, flightAssignmentService.LastAssignedSchedule);
        Assert.Equal(assignment, assigned);
    }

    [Fact]
    public void GoBack_InvokesCallback()
    {
        var backToShell = false;
        var vm = new FlightSearchViewModel(new FakeScheduleApiClient(), new FakeFlightAssignmentService(), onFlightAssigned: _ => { }, onBackToShell: () => backToShell = true);

        vm.GoBackCommand.Execute(null);

        Assert.True(backToShell);
    }
}
