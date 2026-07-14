using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Manufacturers;

namespace PilotsRUs.Admin.App.Pages.Manufacturers;

public sealed class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public List<ManufacturerResponse> Manufacturers { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/manufacturers");
        if (response.IsSuccessStatusCode)
        {
            Manufacturers = await response.Content.ReadFromJsonAsync<List<ManufacturerResponse>>() ?? [];
        }
    }
}
