using System.Net;
using System.Net.Http.Json;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.API.WebApi.Tests.Features.Auth;

public sealed class LoginEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        const string email = "valid-user@pilotsrus.test";
        const string password = "P@ssw0rd123!";
        await factory.CreateUserAsync(email, password);

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        const string email = "wrong-password-user@pilotsrus.test";
        await factory.CreateUserAsync(email, "CorrectP@ssw0rd1");

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
