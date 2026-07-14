using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using PilotsRUs.Shared.SDK.AircraftModels;
using PilotsRUs.Shared.SDK.Manufacturers;

namespace PilotsRUs.Admin.App.Pages.AircraftModels;

public sealed class CreateModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> ManufacturerOptions { get; set; } = [];

    public async Task OnGetAsync()
    {
        await LoadManufacturerOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadManufacturerOptionsAsync();
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PostAsJsonAsync(
            "/aircraft-models",
            new CreateAircraftModelRequest(Input.Name, string.IsNullOrWhiteSpace(Input.IcaoTypeDesignator) ? null : Input.IcaoTypeDesignator, Input.ManufacturerId));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/AircraftModels/Index");
        }

        // AircraftModel has no structured error shape - the validation rules (unknown manufacturer,
        // duplicate name within a manufacturer) return Results.BadRequest(string)/Results.Conflict(string),
        // same JSON-string-literal pattern used throughout Manufacturers.
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadFromJsonAsync<string>();
            ModelState.AddModelError(string.Empty, message ?? "Create failed.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Create failed.");
        }

        await LoadManufacturerOptionsAsync();
        return Page();
    }

    private async Task LoadManufacturerOptionsAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/manufacturers");
        if (response.IsSuccessStatusCode)
        {
            var manufacturers = await response.Content.ReadFromJsonAsync<List<ManufacturerResponse>>() ?? [];
            ManufacturerOptions = manufacturers.Select(m => new SelectListItem(m.Name, m.Id.ToString())).ToList();
        }
    }

    public sealed class InputModel
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(10)]
        public string? IcaoTypeDesignator { get; set; }

        [Required]
        public Guid ManufacturerId { get; set; }
    }
}
