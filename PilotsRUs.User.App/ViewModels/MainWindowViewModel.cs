using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty]
    private ViewModelBase _currentViewModel;

    public MainWindowViewModel(IAccountApiClient accountApiClient, IAuthSessionService authSessionService)
    {
        _accountApiClient = accountApiClient;
        _authSessionService = authSessionService;
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
        onLoggedOut: () => CurrentViewModel = CreateLoginViewModel());
}
