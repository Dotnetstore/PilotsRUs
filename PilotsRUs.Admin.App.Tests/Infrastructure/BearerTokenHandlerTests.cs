using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PilotsRUs.Admin.App.Infrastructure;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.Admin.App.Tests.Infrastructure;

public sealed class BearerTokenHandlerTests
{
    [Fact]
    public async Task SendAsync_WithNoStoredAccessToken_SendsNoAuthorizationHeader()
    {
        var fakeAuth = new FakeAuthenticationService(accessToken: null, refreshToken: null);
        var innerHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = CreateHandler(fakeAuth, innerHandler, refreshHandler: null);

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("https://admin.example/some-page");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(innerHandler.Requests);
        Assert.Null(innerHandler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_On401WithRefreshToken_RefreshesOnceAndRetriesOriginalRequest()
    {
        var fakeAuth = new FakeAuthenticationService(accessToken: "expired-access-token", refreshToken: "valid-refresh-token");

        var callCount = 0;
        var innerHandler = new RecordingHandler(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        var newTokens = new LoginResponse("new-access-token", DateTimeOffset.UtcNow.AddHours(1), "new-refresh-token", DateTimeOffset.UtcNow.AddDays(14));
        var refreshHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(newTokens)
        });

        var handler = CreateHandler(fakeAuth, innerHandler, refreshHandler);

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("https://admin.example/some-page");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Original attempt (401) + retry (200) went through the inner ("Api") pipeline.
        Assert.Equal(2, innerHandler.Requests.Count);
        Assert.Equal("Bearer expired-access-token", innerHandler.Requests[0].Headers.Authorization?.ToString());
        Assert.Equal("Bearer new-access-token", innerHandler.Requests[1].Headers.Authorization?.ToString());

        // Exactly one refresh call, and it did not go through the inner handler (no recursion).
        Assert.Single(refreshHandler.Requests);
        Assert.EndsWith("/auth/refresh", refreshHandler.Requests[0].RequestUri!.AbsolutePath);

        // The cookie was re-issued with the new tokens.
        Assert.Single(fakeAuth.SignInCalls);
        var newProperties = fakeAuth.SignInCalls[0];
        Assert.Equal("new-access-token", newProperties.GetTokenValue("access_token"));
        Assert.Equal("new-refresh-token", newProperties.GetTokenValue("refresh_token"));
    }

    [Fact]
    public async Task SendAsync_On401WithNoRefreshToken_ReturnsOriginal401WithoutCallingRefresh()
    {
        var fakeAuth = new FakeAuthenticationService(accessToken: "expired-access-token", refreshToken: null);
        var innerHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var refreshHandler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var handler = CreateHandler(fakeAuth, innerHandler, refreshHandler);

        using var client = new HttpClient(handler);
        var response = await client.GetAsync("https://admin.example/some-page");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(innerHandler.Requests);
        Assert.Empty(refreshHandler.Requests);
        Assert.Empty(fakeAuth.SignInCalls);
    }

    private static BearerTokenHandler CreateHandler(FakeAuthenticationService fakeAuth, HttpMessageHandler innerHandler, HttpMessageHandler? refreshHandler)
    {
        var services = new ServiceCollection();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        services.AddSingleton<IAuthenticationService>(fakeAuth);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "test@pilotsrus.test")], CookieAuthenticationDefaults.AuthenticationScheme));

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var httpClientFactory = new FakeHttpClientFactory(refreshHandler ?? new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        return new BearerTokenHandler(httpContextAccessor, httpClientFactory) { InnerHandler = innerHandler };
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FakeAuthenticationService(string? accessToken, string? refreshToken) : IAuthenticationService
    {
        private AuthenticationProperties _properties = CreateProperties(accessToken, refreshToken);
        private ClaimsPrincipal _principal = new(new ClaimsIdentity());

        public List<AuthenticationProperties> SignInCalls { get; } = [];

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var ticket = new AuthenticationTicket(_principal, _properties, scheme ?? CookieAuthenticationDefaults.AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            _principal = principal;
            _properties = properties ?? new AuthenticationProperties();
            SignInCalls.Add(_properties);
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException("Not exercised by BearerTokenHandler.");

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            throw new NotSupportedException("Not exercised by BearerTokenHandler.");

        private static AuthenticationProperties CreateProperties(string? accessToken, string? refreshToken)
        {
            var properties = new AuthenticationProperties();
            List<AuthenticationToken> tokens = [];
            if (accessToken is not null)
            {
                tokens.Add(new AuthenticationToken { Name = "access_token", Value = accessToken });
            }

            if (refreshToken is not null)
            {
                tokens.Add(new AuthenticationToken { Name = "refresh_token", Value = refreshToken });
            }

            properties.StoreTokens(tokens);
            return properties;
        }
    }
}
