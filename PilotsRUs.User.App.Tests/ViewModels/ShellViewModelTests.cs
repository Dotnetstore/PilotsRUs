using PilotsRUs.User.App.Services;
using PilotsRUs.User.App.Tests.Services;
using PilotsRUs.User.App.ViewModels;

namespace PilotsRUs.User.App.Tests.ViewModels;

public sealed class ShellViewModelTests
{
    [Fact]
    public void DisplayName_ReflectsAuthSessionService()
    {
        var authSession = new AuthSessionService();
        authSession.SetSession("access-token", DateTimeOffset.UtcNow.AddMinutes(60), "refresh-token", DateTimeOffset.UtcNow.AddDays(14), "Goose");
        var vm = new ShellViewModel(new FakeAccountApiClient(), authSession, onLoggedOut: () => { });

        Assert.Equal("Goose", vm.DisplayName);
    }

    [Fact]
    public async Task LogoutAsync_CallsApiClearsSessionAndInvokesCallback()
    {
        var authSession = new AuthSessionService();
        authSession.SetSession("access-token", DateTimeOffset.UtcNow.AddMinutes(60), "refresh-token", DateTimeOffset.UtcNow.AddDays(14), "Goose");
        var apiClient = new FakeAccountApiClient();
        var loggedOut = false;
        var vm = new ShellViewModel(apiClient, authSession, onLoggedOut: () => loggedOut = true);

        await vm.LogoutCommand.ExecuteAsync(null);

        Assert.True(apiClient.LogoutCalled);
        Assert.Equal("refresh-token", apiClient.LogoutRefreshToken);
        Assert.False(authSession.IsAuthenticated);
        Assert.True(loggedOut);
    }
}
