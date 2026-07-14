using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Airports;

namespace PilotsRUs.Admin.App.Pages.Airports;

public sealed class DeleteModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public AirportResponse? TargetAirport { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/airports/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        TargetAirport = await response.Content.ReadFromJsonAsync<AirportResponse>();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.DeleteAsync($"/airports/{Id}");

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Airports/Index");
        }

        ErrorMessage = "Delete failed.";

        var getResponse = await client.GetAsync($"/airports/{Id}");
        if (getResponse.IsSuccessStatusCode)
        {
            TargetAirport = await getResponse.Content.ReadFromJsonAsync<AirportResponse>();
        }

        return Page();
    }
}
