using CommunityToolkit.Mvvm.ComponentModel;
using PilotsRUs.User.App.Data;
using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.ViewModels;

// No navigation service/router - this is the only place navigation happens so far. MainWindowViewModel is
// DI-resolved (gets the actual service dependencies from the container) and manually constructs each leaf
// screen ViewModel with simple Action callbacks for "navigate to X", swapping CurrentViewModel. The
// ContentControl in MainWindow.axaml + the existing ViewLocator resolve the matching View by name.
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAccountApiClient _accountApiClient;
    private readonly IAuthSessionService _authSessionService;
    private readonly IScheduleApiClient _scheduleApiClient;
    private readonly IFlightAssignmentService _flightAssignmentService;

    [ObservableProperty]
    private ViewModelBase _currentViewModel;

    public MainWindowViewModel(
        IAccountApiClient accountApiClient, IAuthSessionService authSessionService,
        IScheduleApiClient scheduleApiClient, IFlightAssignmentService flightAssignmentService)
    {
        _accountApiClient = accountApiClient;
        _authSessionService = authSessionService;
        _scheduleApiClient = scheduleApiClient;
        _flightAssignmentService = flightAssignmentService;
        _currentViewModel = CreateLoginViewModel();
    }

    private LoginViewModel CreateLoginViewModel() => new(
        _accountApiClient, _authSessionService,
        onLoginSucceeded: () => CurrentViewModel = CreateShellViewModel(),
        onGoToRegister: () => CurrentViewModel = CreateRegisterViewModel());

    private RegisterViewModel CreateRegisterViewModel() => new(
        _accountApiClient,
        onRegisterSucceeded: () => CurrentViewModel = CreateLoginViewModel(),
        onGoToLogin: () => CurrentViewModel = CreateLoginViewModel());

    private ShellViewModel CreateShellViewModel() => new(
        _accountApiClient, _authSessionService,
        onLoggedOut: () => CurrentViewModel = CreateLoginViewModel(),
        onSearchFlights: () => CurrentViewModel = CreateFlightSearchViewModel(),
        onMyFlights: () => CurrentViewModel = CreateMyFlightsViewModel());

    private FlightSearchViewModel CreateFlightSearchViewModel() => new(
        _scheduleApiClient, _flightAssignmentService,
        onFlightAssigned: assignment => CurrentViewModel = CreateFlightAssignmentResultViewModel(assignment),
        onBackToShell: () => CurrentViewModel = CreateShellViewModel());

    private FlightAssignmentResultViewModel CreateFlightAssignmentResultViewModel(FlightAssignment assignment) => new(
        assignment,
        onBackToSearch: () => CurrentViewModel = CreateFlightSearchViewModel(),
        onBackToShell: () => CurrentViewModel = CreateShellViewModel());

    private MyFlightsViewModel CreateMyFlightsViewModel()
    {
        var viewModel = new MyFlightsViewModel(_flightAssignmentService, onBackToShell: () => CurrentViewModel = CreateShellViewModel());
        // No page-navigation lifecycle event to hook into here - firing the generated LoadCommand right
        // after construction is how this app already triggers cross-VM work (see ShellViewModel.LogoutAsync
        // calling back into MainWindowViewModel via its own Action callback).
        _ = viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }
}
