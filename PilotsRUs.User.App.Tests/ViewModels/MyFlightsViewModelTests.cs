using PilotsRUs.User.App.Data;
using PilotsRUs.User.App.Tests.Services;
using PilotsRUs.User.App.ViewModels;

namespace PilotsRUs.User.App.Tests.ViewModels;

public sealed class MyFlightsViewModelTests
{
    [Fact]
    public async Task LoadAsync_PopulatesAssignmentsFromService()
    {
        var assignment = new FlightAssignment
        {
            Id = Guid.NewGuid(), ScheduleId = Guid.NewGuid(), FlightNumber = "PR100",
            DepartureAirportIcaoCode = "ESSA", ArrivalAirportIcaoCode = "EGLL",
            FlightDate = new DateOnly(2026, 3, 1), AircraftRegistrationNumber = "SE-ABC",
            AssignedPassengersEconomy = 100, AssignedPassengersBusiness = 15, AssignedPassengersFirst = 5, AssignedCargoKg = 3000,
            AssignedAtUtc = DateTimeOffset.UtcNow
        };
        var service = new FakeFlightAssignmentService { Assignments = [assignment] };
        var vm = new MyFlightsViewModel(service, onBackToShell: () => { });

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.Assignments);
        Assert.Equal(assignment.Id, vm.Assignments[0].Id);
    }

    [Fact]
    public void GoBack_InvokesCallback()
    {
        var backToShell = false;
        var vm = new MyFlightsViewModel(new FakeFlightAssignmentService(), onBackToShell: () => backToShell = true);

        vm.GoBackCommand.Execute(null);

        Assert.True(backToShell);
    }
}
