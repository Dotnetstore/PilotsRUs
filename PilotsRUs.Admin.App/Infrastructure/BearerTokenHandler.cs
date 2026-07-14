using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.Admin.App.Infrastructure;

public sealed class BearerTokenHandler(
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory) : DelegatingHandler
{
    // One semaphore per authenticated session, so concurrent 401s within the same browser session
    // serialize onto a single /auth/refresh call instead of each consuming (and invalidating, since
    // refresh tokens are single-use/rotating) the same token.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RefreshLocks = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var sentAccessToken = httpContext is null ? null : await httpContext.GetTokenAsync("access_token");
        if (sentAccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sentAccessToken);
        }

        var bufferedContent = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || httpContext is null)
        {
            return response;
        }

        var refreshToken = await httpContext.GetTokenAsync("refresh_token");
        if (refreshToken is null)
        {
            return response; // nothing to try - let the 401 bubble to cookie auth's LoginPath redirect
        }

        var sessionKey = httpContext.User.Identity?.Name ?? httpContext.Connection.Id;
        var refreshLock = RefreshLocks.GetOrAdd(sessionKey, _ => new SemaphoreSlim(1, 1));

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            var currentAccessToken = await httpContext.GetTokenAsync("access_token");
            string newAccessToken;

            if (currentAccessToken != sentAccessToken)
            {
                // Someone else already refreshed while we waited on the lock - just use their result
                // instead of calling /auth/refresh again (the previous token is now rotated away).
                newAccessToken = currentAccessToken!;
            }
            else
            {
                // Plain, unnamed HttpClient - deliberately NOT the "Api" named client, which has this same
                // handler in its pipeline and would recurse. ConfigureHttpClientDefaults in ServiceDefaults
                // applies service discovery to every client from the factory, named or not, so this still
                // resolves "https+http://api" correctly.
                using var refreshClient = httpClientFactory.CreateClient();
                refreshClient.BaseAddress = new Uri("https+http://api");
                var refreshResponse = await refreshClient.PostAsJsonAsync(
                    "/auth/refresh", new RefreshTokenRequest(refreshToken), cancellationToken);

                if (!refreshResponse.IsSuccessStatusCode)
                {
                    return response; // refresh failed too - bubble the original 401
                }

                var tokens = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
                if (tokens is null)
                {
                    return response;
                }

                var authProperties = new AuthenticationProperties();
                authProperties.StoreTokens(
                [
                    new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken },
                    new AuthenticationToken { Name = "refresh_token", Value = tokens.RefreshToken },
                    new AuthenticationToken { Name = "expires_at", Value = tokens.ExpiresAtUtc.ToString("o") }
                ]);
                await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, httpContext.User, authProperties);
                newAccessToken = tokens.AccessToken;
            }

            response.Dispose();
            using var retry = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
            {
                retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (bufferedContent is not null)
            {
                retry.Content = new ByteArrayContent(bufferedContent);
                if (request.Content is not null)
                {
                    foreach (var header in request.Content.Headers)
                    {
                        retry.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccessToken);
            return await base.SendAsync(retry, cancellationToken);
        }
        finally
        {
            refreshLock.Release();
        }
    }
}
