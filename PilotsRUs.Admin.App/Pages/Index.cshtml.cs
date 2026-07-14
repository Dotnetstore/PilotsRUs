using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.Admin.App.Pages;

public class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public CurrentUserResponse? CurrentUser { get; set; }

    public async Task OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/auth/me");
        if (response.IsSuccessStatusCode)
        {
            CurrentUser = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        }
    }
}
