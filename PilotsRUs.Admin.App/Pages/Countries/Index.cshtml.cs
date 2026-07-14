using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Countries;

namespace PilotsRUs.Admin.App.Pages.Countries;

public sealed class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public List<CountryResponse> Countries { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/countries");
        if (response.IsSuccessStatusCode)
        {
            Countries = await response.Content.ReadFromJsonAsync<List<CountryResponse>>() ?? [];
        }
    }
}
