using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Airports;

namespace PilotsRUs.Admin.App.Pages.Airports;

public sealed class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public List<AirportResponse> Airports { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/airports");
        if (response.IsSuccessStatusCode)
        {
            Airports = await response.Content.ReadFromJsonAsync<List<AirportResponse>>() ?? [];
        }
    }
}
