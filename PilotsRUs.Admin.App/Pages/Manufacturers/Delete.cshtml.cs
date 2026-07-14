using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Manufacturers;

namespace PilotsRUs.Admin.App.Pages.Manufacturers;

public sealed class DeleteModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public ManufacturerResponse? TargetManufacturer { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/manufacturers/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        TargetManufacturer = await response.Content.ReadFromJsonAsync<ManufacturerResponse>();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.DeleteAsync($"/manufacturers/{Id}");

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Manufacturers/Index");
        }

        ErrorMessage = "Delete failed.";

        var getResponse = await client.GetAsync($"/manufacturers/{Id}");
        if (getResponse.IsSuccessStatusCode)
        {
            TargetManufacturer = await getResponse.Content.ReadFromJsonAsync<ManufacturerResponse>();
        }

        return Page();
    }
}
