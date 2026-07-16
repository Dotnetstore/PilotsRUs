using System.Net;
using System.Net.Http.Json;
using PilotsRUs.Shared.SDK.Accounts;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.User.App.Services;

public sealed record AccountApiResult<T>(bool Success, T? Value, string? ErrorMessage)
{
    public static AccountApiResult<T> Ok(T value) => new(true, value, null);
    public static AccountApiResult<T> Fail(string errorMessage) => new(false, default, errorMessage);
}

// Wraps HTTP calls to /account/* and returns simple success/failure results rather than throwing, so
// ViewModels can consume this without try/catch around every call.
public interface IAccountApiClient
{
    Task<AccountApiResult<AccountResponse>> RegisterAsync(string email, string password, string displayName, CancellationToken ct = default);
    Task<AccountApiResult<AccountLoginResponse>> LoginAsync(string email, string password, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}

public sealed class AccountApiClient(IHttpClientFactory httpClientFactory) : IAccountApiClient
{
    public async Task<AccountApiResult<AccountResponse>> RegisterAsync(string email, string password, string displayName, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest(email, password, displayName), ct);

        if (response.IsSuccessStatusCode)
        {
            var account = await response.Content.ReadFromJsonAsync<AccountResponse>(cancellationToken: ct);
            return AccountApiResult<AccountResponse>.Ok(account!);
        }

        return AccountApiResult<AccountResponse>.Fail(await ReadErrorMessageAsync(response, ct));
    }

    public async Task<AccountApiResult<AccountLoginResponse>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PostAsJsonAsync("/account/login", new AccountLoginRequest(email, password), ct);

        if (response.IsSuccessStatusCode)
        {
            var login = await response.Content.ReadFromJsonAsync<AccountLoginResponse>(cancellationToken: ct);
            return AccountApiResult<AccountLoginResponse>.Ok(login!);
        }

        return AccountApiResult<AccountLoginResponse>.Fail(await ReadErrorMessageAsync(response, ct));
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("Api");
        await client.PostAsJsonAsync("/account/logout", new RefreshTokenRequest(refreshToken), ct);
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadFromJsonAsync<string>(cancellationToken: ct);
            return message ?? "Request failed.";
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "Invalid email or password.";
        }

        return "Request failed.";
    }
}
