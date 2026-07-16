using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PilotsRUs.Shared.SDK.Accounts;
using PilotsRUs.Shared.SDK.Auth;
using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.Infrastructure;

// Mirrors Admin.App's BearerTokenHandler, but simpler - no HttpContext/cookie involved, just reads/writes
// the in-memory IAuthSessionService. A single SemaphoreSlim is enough (not Admin.App's
// ConcurrentDictionary<string, SemaphoreSlim> keyed per browser session) since only one session ever
// exists in a desktop process.
public sealed class BearerTokenHandler(IAuthSessionService authSessionService, IHttpClientFactory httpClientFactory, IOptions<ApiOptions> apiOptions) : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sentAccessToken = authSessionService.AccessToken;
        if (sentAccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sentAccessToken);
        }

        var bufferedContent = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || authSessionService.RefreshToken is null)
        {
            return response;
        }

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            var currentAccessToken = authSessionService.AccessToken;
            string newAccessToken;

            if (currentAccessToken != sentAccessToken)
            {
                // Someone else already refreshed while we waited on the lock.
                newAccessToken = currentAccessToken!;
            }
            else
            {
                // Plain, unnamed HttpClient - deliberately NOT the "Api" named client, which has this same
                // handler in its pipeline and would recurse.
                using var refreshClient = httpClientFactory.CreateClient();
                refreshClient.BaseAddress = new Uri(apiOptions.Value.BaseAddress);
                var refreshResponse = await refreshClient.PostAsJsonAsync(
                    "/account/refresh", new RefreshTokenRequest(authSessionService.RefreshToken!), cancellationToken);

                if (!refreshResponse.IsSuccessStatusCode)
                {
                    return response; // refresh failed too - bubble the original 401
                }

                var tokens = await refreshResponse.Content.ReadFromJsonAsync<AccountLoginResponse>(cancellationToken: cancellationToken);
                if (tokens is null)
                {
                    return response;
                }

                authSessionService.UpdateTokens(tokens.AccessToken, tokens.ExpiresAtUtc, tokens.RefreshToken, tokens.RefreshExpiresAtUtc);
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
            RefreshLock.Release();
        }
    }
}
