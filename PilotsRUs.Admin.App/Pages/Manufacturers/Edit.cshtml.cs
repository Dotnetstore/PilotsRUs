using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Manufacturers;

namespace PilotsRUs.Admin.App.Pages.Manufacturers;

public sealed class EditModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/manufacturers/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var manufacturer = await response.Content.ReadFromJsonAsync<ManufacturerResponse>();
        if (manufacturer is null)
        {
            return NotFound();
        }

        Input = new InputModel { Name = manufacturer.Name, Code = manufacturer.Code };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PutAsJsonAsync(
            $"/manufacturers/{Id}",
            new UpdateManufacturerRequest(Input.Name, string.IsNullOrWhiteSpace(Input.Code) ? null : Input.Code));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Manufacturers/Index");
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var message = await response.Content.ReadFromJsonAsync<string>();
            ModelState.AddModelError(string.Empty, message ?? "Update failed.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Update failed.");
        }

        return Page();
    }

    public sealed class InputModel
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Code { get; set; }
    }
}
