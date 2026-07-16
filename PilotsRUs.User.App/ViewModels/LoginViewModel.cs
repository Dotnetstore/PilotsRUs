using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.ViewModels;

public partial class LoginViewModel(
    IAccountApiClient accountApiClient, IAuthSessionService authSessionService, Action onLoginSucceeded, Action onGoToRegister) : ViewModelBase
{
    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await accountApiClient.LoginAsync(Email, Password);
            if (result.Success)
            {
                var login = result.Value!;
                authSessionService.SetSession(login.AccessToken, login.ExpiresAtUtc, login.RefreshToken, login.RefreshExpiresAtUtc, login.DisplayName);
                onLoginSucceeded();
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
    private void GoToRegister() => onGoToRegister();
}
