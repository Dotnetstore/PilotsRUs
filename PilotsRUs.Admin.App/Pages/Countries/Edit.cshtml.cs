using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Countries;

namespace PilotsRUs.Admin.App.Pages.Countries;

public sealed class EditModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/countries/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var country = await response.Content.ReadFromJsonAsync<CountryResponse>();
        if (country is null)
        {
            return NotFound();
        }

        Input = new InputModel { Name = country.Name, IsoAlpha2Code = country.IsoAlpha2Code, IsoAlpha3Code = country.IsoAlpha3Code };
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
            $"/countries/{Id}",
            new UpdateCountryRequest(Input.Name, Input.IsoAlpha2Code, Input.IsoAlpha3Code));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Countries/Index");
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

        [Required, StringLength(2, MinimumLength = 2)]
        public string IsoAlpha2Code { get; set; } = string.Empty;

        [Required, StringLength(3, MinimumLength = 3)]
        public string IsoAlpha3Code { get; set; } = string.Empty;
    }
}
