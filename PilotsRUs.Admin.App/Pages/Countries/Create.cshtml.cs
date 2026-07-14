using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Countries;

namespace PilotsRUs.Admin.App.Pages.Countries;

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
            "/countries",
            new CreateCountryRequest(Input.Name, Input.IsoAlpha2Code, Input.IsoAlpha3Code));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Countries/Index");
        }

        // Country has no structured error shape - the three uniqueness rules (Name/Alpha2/Alpha3) return
        // Results.Conflict(string), same JSON-string-literal pattern used throughout Manufacturers.
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

        [Required, StringLength(2, MinimumLength = 2)]
        public string IsoAlpha2Code { get; set; } = string.Empty;

        [Required, StringLength(3, MinimumLength = 3)]
        public string IsoAlpha3Code { get; set; } = string.Empty;
    }
}
