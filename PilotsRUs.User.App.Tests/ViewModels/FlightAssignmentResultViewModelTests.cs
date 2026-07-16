using PilotsRUs.User.App.Data;
using PilotsRUs.User.App.ViewModels;

namespace PilotsRUs.User.App.Tests.ViewModels;

public sealed class FlightAssignmentResultViewModelTests
{
    private static FlightAssignment CreateAssignment() => new()
    {
        Id = Guid.NewGuid(), ScheduleId = Guid.NewGuid(), FlightNumber = "PR100",
        DepartureAirportIcaoCode = "ESSA", ArrivalAirportIcaoCode = "EGLL",
        FlightDate = new DateOnly(2026, 3, 1), AircraftRegistrationNumber = "SE-ABC",
        AssignedPassengersEconomy = 100, AssignedPassengersBusiness = 15, AssignedPassengersFirst = 5, AssignedCargoKg = 3000,
        AssignedAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void ExposesAssignmentFields()
    {
        var assignment = CreateAssignment();
        var vm = new FlightAssignmentResultViewModel(assignment, onBackToSearch: () => { }, onBackToShell: () => { });

        Assert.Equal(assignment.FlightNumber, vm.FlightNumber);
        Assert.Equal(assignment.DepartureAirportIcaoCode, vm.DepartureAirportIcaoCode);
        Assert.Equal(assignment.ArrivalAirportIcaoCode, vm.ArrivalAirportIcaoCode);
        Assert.Equal(assignment.FlightDate, vm.FlightDate);
        Assert.Equal(assignment.AircraftRegistrationNumber, vm.AircraftRegistrationNumber);
        Assert.Equal(assignment.AssignedPassengersEconomy, vm.AssignedPassengersEconomy);
        Assert.Equal(assignment.AssignedPassengersBusiness, vm.AssignedPassengersBusiness);
        Assert.Equal(assignment.AssignedPassengersFirst, vm.AssignedPassengersFirst);
        Assert.Equal(assignment.AssignedCargoKg, vm.AssignedCargoKg);
    }

    [Fact]
    public void SearchAgain_InvokesCallback()
    {
        var backToSearch = false;
        var vm = new FlightAssignmentResultViewModel(CreateAssignment(), onBackToSearch: () => backToSearch = true, onBackToShell: () => { });

        vm.SearchAgainCommand.Execute(null);

        Assert.True(backToSearch);
    }

    [Fact]
    public void BackToShell_InvokesCallback()
    {
        var backToShell = false;
        var vm = new FlightAssignmentResultViewModel(CreateAssignment(), onBackToSearch: () => { }, onBackToShell: () => backToShell = true);

        vm.BackToShellCommand.Execute(null);

        Assert.True(backToShell);
    }
}
