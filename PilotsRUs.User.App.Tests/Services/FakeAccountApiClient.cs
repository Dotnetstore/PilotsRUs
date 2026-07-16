using PilotsRUs.Shared.SDK.Accounts;
using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.Tests.Services;

internal sealed class FakeAccountApiClient : IAccountApiClient
{
    public ApiResult<AccountResponse>? RegisterResult { get; set; }
    public ApiResult<AccountLoginResponse>? LoginResult { get; set; }
    public bool LogoutCalled { get; private set; }
    public string? LogoutRefreshToken { get; private set; }

    public Task<ApiResult<AccountResponse>> RegisterAsync(string email, string password, string displayName, CancellationToken ct = default) =>
        Task.FromResult(RegisterResult!);

    public Task<ApiResult<AccountLoginResponse>> LoginAsync(string email, string password, CancellationToken ct = default) =>
        Task.FromResult(LoginResult!);

    public Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        LogoutCalled = true;
        LogoutRefreshToken = refreshToken;
        return Task.CompletedTask;
    }
}
