using PilotsRUs.Shared.SDK.Accounts;
using PilotsRUs.User.App.Services;
using PilotsRUs.User.App.Tests.Services;
using PilotsRUs.User.App.ViewModels;

namespace PilotsRUs.User.App.Tests.ViewModels;

public sealed class LoginViewModelTests
{
    [Fact]
    public async Task LoginAsync_WhenApiSucceeds_SetsSessionAndInvokesCallback()
    {
        var apiClient = new FakeAccountApiClient
        {
            LoginResult = AccountApiResult<AccountLoginResponse>.Ok(
                new AccountLoginResponse("access-token", DateTimeOffset.UtcNow.AddMinutes(60), "refresh-token", DateTimeOffset.UtcNow.AddDays(14), "Maverick"))
        };
        var authSession = new AuthSessionService();
        var succeeded = false;
        var vm = new LoginViewModel(apiClient, authSession, onLoginSucceeded: () => succeeded = true, onGoToRegister: () => { })
        {
            Email = "test@pilotsrus.test",
            Password = "password123"
        };

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(succeeded);
        Assert.True(authSession.IsAuthenticated);
        Assert.Equal("Maverick", authSession.DisplayName);
        Assert.Equal("access-token", authSession.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_WhenApiFails_SetsErrorAndDoesNotSetSession()
    {
        var apiClient = new FakeAccountApiClient { LoginResult = AccountApiResult<AccountLoginResponse>.Fail("Invalid email or password.") };
        var authSession = new AuthSessionService();
        var vm = new LoginViewModel(apiClient, authSession, onLoginSucceeded: () => { }, onGoToRegister: () => { })
        {
            Email = "test@pilotsrus.test",
            Password = "wrong-password"
        };

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal("Invalid email or password.", vm.ErrorMessage);
        Assert.False(authSession.IsAuthenticated);
    }

    [Fact]
    public void GoToRegister_InvokesCallback()
    {
        var navigated = false;
        var vm = new LoginViewModel(new FakeAccountApiClient(), new AuthSessionService(), onLoginSucceeded: () => { }, onGoToRegister: () => navigated = true);

        vm.GoToRegisterCommand.Execute(null);

        Assert.True(navigated);
    }
}
