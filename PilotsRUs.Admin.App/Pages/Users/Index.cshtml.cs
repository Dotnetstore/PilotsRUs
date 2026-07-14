using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Users;

namespace PilotsRUs.Admin.App.Pages.Users;

public sealed class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public List<UserResponse> Users { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/users");
        if (response.IsSuccessStatusCode)
        {
            Users = await response.Content.ReadFromJsonAsync<List<UserResponse>>() ?? [];
        }
    }

    public async Task<IActionResult> OnPostReactivateAsync(Guid id)
    {
        var client = httpClientFactory.CreateClient("Api");
        await client.PostAsync($"/users/{id}/reactivate", null);
        return RedirectToPage();
    }
}
