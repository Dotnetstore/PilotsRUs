using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.ViewModels;

public partial class ShellViewModel(
    IAccountApiClient accountApiClient, IAuthSessionService authSessionService,
    Action onLoggedOut, Action onSearchFlights, Action onMyFlights) : ViewModelBase
{
    public string DisplayName => authSessionService.DisplayName ?? string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task LogoutAsync()
    {
        IsBusy = true;
        try
        {
            var refreshToken = authSessionService.RefreshToken;
            if (refreshToken is not null)
            {
                await accountApiClient.LogoutAsync(refreshToken);
            }
        }
        finally
        {
            authSessionService.Clear();
            IsBusy = false;
            onLoggedOut();
        }
    }

    [RelayCommand]
    private void SearchFlights() => onSearchFlights();

    [RelayCommand]
    private void MyFlights() => onMyFlights();
}
