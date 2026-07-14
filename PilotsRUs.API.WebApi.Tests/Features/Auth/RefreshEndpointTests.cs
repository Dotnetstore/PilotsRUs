using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.API.WebApi.Features.Auth;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.API.WebApi.Tests.Features.Auth;

public sealed class RefreshEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewPair()
    {
        const string email = "refresh-valid@pilotsrus.test";
        const string password = "P@ssw0rd123!";
        await factory.CreateUserAsync(email, password);

        using var client = factory.CreateClient();
        var loginResponse = await (await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password)))
            .Content.ReadFromJsonAsync<LoginResponse>();

        var refreshResponse = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(loginResponse!.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var body = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(loginResponse.AccessToken, body!.AccessToken);
        Assert.NotEqual(loginResponse.RefreshToken, body.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithAlreadyRotatedToken_RevokesWholeChain()
    {
        const string email = "refresh-reuse@pilotsrus.test";
        const string password = "P@ssw0rd123!";
        await factory.CreateUserAsync(email, password);

        using var client = factory.CreateClient();
        var loginResponse = await (await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password)))
            .Content.ReadFromJsonAsync<LoginResponse>();

        var firstRefreshResponse = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(loginResponse!.RefreshToken));
        var firstRefreshBody = await firstRefreshResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Replay the original (already-rotated-away) token - should be rejected as reuse.
        var replayResponse = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(loginResponse.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        // The immediately-prior generation (still technically unexpired) should now also be rejected,
        // proving the whole lineage was revoked, not just the replayed token.
        var secondGenerationResponse = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(firstRefreshBody!.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, secondGenerationResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ReturnsUnauthorized()
    {
        const string email = "refresh-expired@pilotsrus.test";
        var user = await factory.CreateUserAsync(email, "P@ssw0rd123!");

        const string rawToken = "expired-raw-token-for-test";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FamilyId = Guid.NewGuid(),
                TokenHash = RefreshTokenService.HashToken(rawToken),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-15)
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(rawToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesToken_SubsequentRefreshFails()
    {
        const string email = "refresh-logout@pilotsrus.test";
        const string password = "P@ssw0rd123!";
        await factory.CreateUserAsync(email, password);

        using var client = factory.CreateClient();
        var loginResponse = await (await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password)))
            .Content.ReadFromJsonAsync<LoginResponse>();

        var logoutResponse = await client.PostAsJsonAsync("/auth/logout", new RefreshTokenRequest(loginResponse!.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshResponse = await client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest(loginResponse.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }
}
