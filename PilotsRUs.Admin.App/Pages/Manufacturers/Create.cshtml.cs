using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Manufacturers;

namespace PilotsRUs.Admin.App.Pages.Manufacturers;

public sealed class CreateModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PostAsJsonAsync(
            "/manufacturers",
            new CreateManufacturerRequest(Input.Name, string.IsNullOrWhiteSpace(Input.Code) ? null : Input.Code));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Manufacturers/Index");
        }

        // Manufacturer has no UserValidationProblem-style structured error - the only validation rule
        // (duplicate Name) returns Results.Conflict(string), which serializes as a JSON string literal,
        // same pattern Users' Edit.cshtml.cs already uses for the last-admin guard.
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var message = await response.Content.ReadFromJsonAsync<string>();
            ModelState.AddModelError(string.Empty, message ?? "Create failed.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Create failed.");
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
