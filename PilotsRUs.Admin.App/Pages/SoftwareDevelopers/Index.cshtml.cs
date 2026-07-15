using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.SoftwareDevelopers;

namespace PilotsRUs.Admin.App.Pages.SoftwareDevelopers;

public sealed class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public List<SoftwareDeveloperResponse> SoftwareDevelopers { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/software-developers");
        if (response.IsSuccessStatusCode)
        {
            SoftwareDevelopers = await response.Content.ReadFromJsonAsync<List<SoftwareDeveloperResponse>>() ?? [];
        }
    }
}
