using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Aircrafts;

namespace PilotsRUs.Admin.App.Pages.Aircrafts;

public sealed class DeleteModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public AircraftResponse? TargetAircraft { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/aircrafts/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        TargetAircraft = await response.Content.ReadFromJsonAsync<AircraftResponse>();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.DeleteAsync($"/aircrafts/{Id}");

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Aircrafts/Index");
        }

        ErrorMessage = "Delete failed.";

        var getResponse = await client.GetAsync($"/aircrafts/{Id}");
        if (getResponse.IsSuccessStatusCode)
        {
            TargetAircraft = await getResponse.Content.ReadFromJsonAsync<AircraftResponse>();
        }

        return Page();
    }
}
