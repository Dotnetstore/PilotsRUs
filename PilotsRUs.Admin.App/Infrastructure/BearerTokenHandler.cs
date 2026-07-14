using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace PilotsRUs.Admin.App.Infrastructure;

public sealed class BearerTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await (httpContextAccessor.HttpContext?.GetTokenAsync("access_token") ?? Task.FromResult<string?>(null));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
