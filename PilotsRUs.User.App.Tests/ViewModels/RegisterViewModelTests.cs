using PilotsRUs.Shared.SDK.Accounts;
using PilotsRUs.User.App.Services;
using PilotsRUs.User.App.Tests.Services;
using PilotsRUs.User.App.ViewModels;

namespace PilotsRUs.User.App.Tests.ViewModels;

public sealed class RegisterViewModelTests
{
    [Fact]
    public async Task RegisterAsync_WithMismatchedPasswords_SetsErrorAndDoesNotNavigate()
    {
        var navigatedToLogin = false;
        var vm = new RegisterViewModel(new FakeAccountApiClient(), onRegisterSucceeded: () => navigatedToLogin = true, onGoToLogin: () => { })
        {
            Email = "test@pilotsrus.test",
            Password = "password123",
            ConfirmPassword = "different123",
            DisplayName = "Test"
        };

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("Passwords do not match.", vm.ErrorMessage);
        Assert.False(navigatedToLogin);
    }

    [Fact]
    public async Task RegisterAsync_WhenApiSucceeds_InvokesOnRegisterSucceeded()
    {
        var apiClient = new FakeAccountApiClient
        {
            RegisterResult = AccountApiResult<AccountResponse>.Ok(new AccountResponse(Guid.NewGuid(), "test@pilotsrus.test", "Test"))
        };
        var navigatedToLogin = false;
        var vm = new RegisterViewModel(apiClient, onRegisterSucceeded: () => navigatedToLogin = true, onGoToLogin: () => { })
        {
            Email = "test@pilotsrus.test",
            Password = "password123",
            ConfirmPassword = "password123",
            DisplayName = "Test"
        };

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.True(navigatedToLogin);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task RegisterAsync_WhenApiFails_SetsErrorMessage()
    {
        var apiClient = new FakeAccountApiClient { RegisterResult = AccountApiResult<AccountResponse>.Fail("An account with this email already exists.") };
        var vm = new RegisterViewModel(apiClient, onRegisterSucceeded: () => { }, onGoToLogin: () => { })
        {
            Email = "test@pilotsrus.test",
            Password = "password123",
            ConfirmPassword = "password123",
            DisplayName = "Test"
        };

        await vm.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("An account with this email already exists.", vm.ErrorMessage);
    }

    [Fact]
    public void GoToLogin_InvokesCallback()
    {
        var navigated = false;
        var vm = new RegisterViewModel(new FakeAccountApiClient(), onRegisterSucceeded: () => { }, onGoToLogin: () => navigated = true);

        vm.GoToLoginCommand.Execute(null);

        Assert.True(navigated);
    }
}
