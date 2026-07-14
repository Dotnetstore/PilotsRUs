using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Countries;

namespace PilotsRUs.Admin.App.Pages.Countries;

public sealed class DeleteModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public CountryResponse? TargetCountry { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/countries/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        TargetCountry = await response.Content.ReadFromJsonAsync<CountryResponse>();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.DeleteAsync($"/countries/{Id}");

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Countries/Index");
        }

        // DeleteCountry can now return Results.Conflict(string) when airports still reference it -
        // surface that specific message instead of a generic one, same pattern Manufacturers/Delete
        // already uses for its AircraftModel guard.
        ErrorMessage = response.StatusCode == HttpStatusCode.Conflict
            ? await response.Content.ReadFromJsonAsync<string>() ?? "Delete failed."
            : "Delete failed.";

        var getResponse = await client.GetAsync($"/countries/{Id}");
        if (getResponse.IsSuccessStatusCode)
        {
            TargetCountry = await getResponse.Content.ReadFromJsonAsync<CountryResponse>();
        }

        return Page();
    }
}
