using CommunityToolkit.Mvvm.Input;
using PilotsRUs.User.App.Data;

namespace PilotsRUs.User.App.ViewModels;

// Displays the just-created FlightAssignment directly - no re-fetch needed, the caller already has
// everything. Confirmation is a separate screen (not shown inline on the search results), per the user's
// explicit choice over the "inline panel" alternative.
public partial class FlightAssignmentResultViewModel(FlightAssignment assignment, Action onBackToSearch, Action onBackToShell) : ViewModelBase
{
    public string FlightNumber => assignment.FlightNumber;
    public string DepartureAirportIcaoCode => assignment.DepartureAirportIcaoCode;
    public string ArrivalAirportIcaoCode => assignment.ArrivalAirportIcaoCode;
    public DateOnly FlightDate => assignment.FlightDate;
    public string AircraftRegistrationNumber => assignment.AircraftRegistrationNumber;
    public int AssignedPassengersEconomy => assignment.AssignedPassengersEconomy;
    public int AssignedPassengersBusiness => assignment.AssignedPassengersBusiness;
    public int AssignedPassengersFirst => assignment.AssignedPassengersFirst;
    public int AssignedCargoKg => assignment.AssignedCargoKg;

    [RelayCommand]
    private void SearchAgain() => onBackToSearch();

    [RelayCommand]
    private void BackToShell() => onBackToShell();
}
