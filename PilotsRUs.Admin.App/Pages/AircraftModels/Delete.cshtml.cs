using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.AircraftModels;

namespace PilotsRUs.Admin.App.Pages.AircraftModels;

public sealed class DeleteModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public AircraftModelResponse? TargetModel { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/aircraft-models/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        TargetModel = await response.Content.ReadFromJsonAsync<AircraftModelResponse>();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.DeleteAsync($"/aircraft-models/{Id}");

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/AircraftModels/Index");
        }

        ErrorMessage = "Delete failed.";

        var getResponse = await client.GetAsync($"/aircraft-models/{Id}");
        if (getResponse.IsSuccessStatusCode)
        {
            TargetModel = await getResponse.Content.ReadFromJsonAsync<AircraftModelResponse>();
        }

        return Page();
    }
}
