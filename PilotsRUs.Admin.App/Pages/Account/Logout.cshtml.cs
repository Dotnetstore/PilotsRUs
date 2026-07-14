using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.Admin.App.Pages.Account;

public sealed class LogoutModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        // Read the refresh token before signing out - once signed out, the cookie holding it is gone.
        var refreshToken = await HttpContext.GetTokenAsync("refresh_token");
        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                var client = httpClientFactory.CreateClient("Api");
                await client.PostAsJsonAsync("/auth/logout", new RefreshTokenRequest(refreshToken));
            }
            catch (HttpRequestException)
            {
                // Best-effort: local sign-out must always succeed even if the API call fails.
            }
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
