using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Aircrafts;

namespace PilotsRUs.Admin.App.Pages.Aircrafts;

public sealed class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public List<AircraftResponse> Aircraft { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/aircrafts");
        if (response.IsSuccessStatusCode)
        {
            Aircraft = await response.Content.ReadFromJsonAsync<List<AircraftResponse>>() ?? [];
        }
    }
}
