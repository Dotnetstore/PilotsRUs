using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.AircraftModels;

namespace PilotsRUs.Admin.App.Pages.AircraftModels;

public sealed class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public List<AircraftModelResponse> AircraftModels { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/aircraft-models");
        if (response.IsSuccessStatusCode)
        {
            AircraftModels = await response.Content.ReadFromJsonAsync<List<AircraftModelResponse>>() ?? [];
        }
    }
}
