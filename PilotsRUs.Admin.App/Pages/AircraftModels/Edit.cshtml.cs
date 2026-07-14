using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using PilotsRUs.Shared.SDK.AircraftModels;
using PilotsRUs.Shared.SDK.Manufacturers;

namespace PilotsRUs.Admin.App.Pages.AircraftModels;

public sealed class EditModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public List<SelectListItem> ManufacturerOptions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/aircraft-models/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var model = await response.Content.ReadFromJsonAsync<AircraftModelResponse>();
        if (model is null)
        {
            return NotFound();
        }

        Input = new InputModel { Name = model.Name, IcaoTypeDesignator = model.IcaoTypeDesignator, ManufacturerId = model.ManufacturerId };
        await LoadManufacturerOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadManufacturerOptionsAsync();
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PutAsJsonAsync(
            $"/aircraft-models/{Id}",
            new UpdateAircraftModelRequest(Input.Name, string.IsNullOrWhiteSpace(Input.IcaoTypeDesignator) ? null : Input.IcaoTypeDesignator, Input.ManufacturerId));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/AircraftModels/Index");
        }

        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadFromJsonAsync<string>();
            ModelState.AddModelError(string.Empty, message ?? "Update failed.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Update failed.");
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
