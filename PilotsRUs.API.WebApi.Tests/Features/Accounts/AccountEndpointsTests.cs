using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.Accounts;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.API.WebApi.Tests.Features.Accounts;

public sealed class AccountEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreatedWithoutTokens()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-register@pilotsrus.test", "P@ssw0rd123!", "Maverick"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.Equal("pilot-register@pilotsrus.test", account!.Email);
        Assert.Equal("Maverick", account.DisplayName);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-dup@pilotsrus.test", "P@ssw0rd123!", "Goose"));

        var response = await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-dup@pilotsrus.test", "AnotherP@ss1", "Iceman"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-shortpw@pilotsrus.test", "short1", "Viper"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithTokens()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-login@pilotsrus.test", "P@ssw0rd123!", "Rooster"));

        var response = await client.PostAsJsonAsync("/account/login", new AccountLoginRequest("pilot-login@pilotsrus.test", "P@ssw0rd123!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccountLoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal("Rooster", body.DisplayName);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-wrongpw@pilotsrus.test", "P@ssw0rd123!", "Hangman"));

        var response = await client.PostAsJsonAsync("/account/login", new AccountLoginRequest("pilot-wrongpw@pilotsrus.test", "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/account/login", new AccountLoginRequest("pilot-unknown@pilotsrus.test", "P@ssw0rd123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-refresh@pilotsrus.test", "P@ssw0rd123!", "Phoenix"));
        var loginResponse = await client.PostAsJsonAsync("/account/login", new AccountLoginRequest("pilot-refresh@pilotsrus.test", "P@ssw0rd123!"));
        var login = await loginResponse.Content.ReadFromJsonAsync<AccountLoginResponse>();

        var response = await client.PostAsJsonAsync("/account/refresh", new RefreshTokenRequest(login!.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await response.Content.ReadFromJsonAsync<AccountLoginResponse>();
        Assert.NotEqual(login.RefreshToken, refreshed!.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithAlreadyRevokedToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-reuse@pilotsrus.test", "P@ssw0rd123!", "Bob"));
        var loginResponse = await client.PostAsJsonAsync("/account/login", new AccountLoginRequest("pilot-reuse@pilotsrus.test", "P@ssw0rd123!"));
        var login = await loginResponse.Content.ReadFromJsonAsync<AccountLoginResponse>();
        await client.PostAsJsonAsync("/account/refresh", new RefreshTokenRequest(login!.RefreshToken));

        // Presenting the now-rotated-away original token again.
        var response = await client.PostAsJsonAsync("/account/refresh", new RefreshTokenRequest(login.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-logout@pilotsrus.test", "P@ssw0rd123!", "Slider"));
        var loginResponse = await client.PostAsJsonAsync("/account/login", new AccountLoginRequest("pilot-logout@pilotsrus.test", "P@ssw0rd123!"));
        var login = await loginResponse.Content.ReadFromJsonAsync<AccountLoginResponse>();

        var logoutResponse = await client.PostAsJsonAsync("/account/logout", new RefreshTokenRequest(login!.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshAfterLogout = await client.PostAsJsonAsync("/account/refresh", new RefreshTokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithAccountToken_ReturnsCurrentAccount()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-me@pilotsrus.test", "P@ssw0rd123!", "Merlin"));
        var loginResponse = await client.PostAsJsonAsync("/account/login", new AccountLoginRequest("pilot-me@pilotsrus.test", "P@ssw0rd123!"));
        var login = await loginResponse.Content.ReadFromJsonAsync<AccountLoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var response = await client.GetAsync("/account/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<CurrentAccountResponse>();
        Assert.Equal("pilot-me@pilotsrus.test", me!.Email);
        Assert.Equal("Merlin", me.DisplayName);
    }

    // This is the regression guard for the whole dual-JWT-scheme/audience design (see CLAUDE.md's
    // "Accounts" section) - without the separate "AccountBearer" scheme/audience, this Account-issued
    // token would also satisfy .RequireAuthorization() on every "any authenticated user" domain endpoint.
    [Fact]
    public async Task AccountToken_CannotAccessAdminScopedEndpoint()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-scoping@pilotsrus.test", "P@ssw0rd123!", "Chipper"));
        var loginResponse = await client.PostAsJsonAsync("/account/login", new AccountLoginRequest("pilot-scoping@pilotsrus.test", "P@ssw0rd123!"));
        var login = await loginResponse.Content.ReadFromJsonAsync<AccountLoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var response = await client.GetAsync("/manufacturers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_GetMeReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/account/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
