using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PilotsRUs.User.App.Data;
using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.ViewModels;

public partial class MyFlightsViewModel(IFlightAssignmentService flightAssignmentService, Action onBackToShell) : ViewModelBase
{
    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<FlightAssignment> Assignments { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var assignments = await flightAssignmentService.GetAllAsync();
            Assignments.Clear();
            foreach (var assignment in assignments)
            {
                Assignments.Add(assignment);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack() => onBackToShell();
}
