using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PilotsRUs.Shared.SDK.Schedules;
using PilotsRUs.User.App.Data;
using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.ViewModels;

public partial class FlightSearchViewModel(
    IScheduleApiClient scheduleApiClient, IFlightAssignmentService flightAssignmentService,
    Action<FlightAssignment> onFlightAssigned, Action onBackToShell) : ViewModelBase
{
    // Plain strings, parsed to int? in SearchAsync (invalid/empty -> no filter for that field) - simpler
    // and more robust than binding a NumericUpDown's decimal? Value against an int? property.
    [ObservableProperty]
    private string _departureIcao = string.Empty;

    [ObservableProperty]
    private string _arrivalIcao = string.Empty;

    [ObservableProperty]
    private string _minDistanceNauticalMiles = string.Empty;

    [ObservableProperty]
    private string _maxDistanceNauticalMiles = string.Empty;

    [ObservableProperty]
    private string _minFlightTimeMinutes = string.Empty;

    [ObservableProperty]
    private string _maxFlightTimeMinutes = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ScheduleResponse> Results { get; } = [];

    [RelayCommand]
    private async Task SearchAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var criteria = new FlightSearchCriteria(
                DepartureIcao, ArrivalIcao,
                ParseInt(MinDistanceNauticalMiles), ParseInt(MaxDistanceNauticalMiles),
                ParseInt(MinFlightTimeMinutes), ParseInt(MaxFlightTimeMinutes));

            var result = await scheduleApiClient.SearchAsync(criteria);
            if (result.Success)
            {
                Results.Clear();
                foreach (var schedule in result.Value!)
                {
                    Results.Add(schedule);
                }
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectFlightAsync(ScheduleResponse schedule)
    {
        IsBusy = true;
        try
        {
            var assignment = await flightAssignmentService.AssignAsync(schedule);
            onFlightAssigned(assignment);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => onBackToShell();

    private static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
