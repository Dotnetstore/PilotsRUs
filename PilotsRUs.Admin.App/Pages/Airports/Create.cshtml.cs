using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using PilotsRUs.Shared.SDK.Airports;
using PilotsRUs.Shared.SDK.Countries;

namespace PilotsRUs.Admin.App.Pages.Airports;

public sealed class CreateModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> CountryOptions { get; set; } = [];

    public async Task OnGetAsync()
    {
        await LoadCountryOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadCountryOptionsAsync();
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PostAsJsonAsync(
            "/airports",
            new CreateAirportRequest(Input.Name, Input.IcaoCode, string.IsNullOrWhiteSpace(Input.IataCode) ? null : Input.IataCode, Input.City, Input.CountryId));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Airports/Index");
        }

        // Airport has no structured error shape - the validation rules (unknown country, duplicate
        // ICAO/IATA) return Results.BadRequest(string)/Results.Conflict(string), same JSON-string-literal
        // pattern used throughout Manufacturers/AircraftModels/Countries.
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadFromJsonAsync<string>();
            ModelState.AddModelError(string.Empty, message ?? "Create failed.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Create failed.");
        }

        await LoadCountryOptionsAsync();
        return Page();
    }

    private async Task LoadCountryOptionsAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/countries");
        if (response.IsSuccessStatusCode)
        {
            var countries = await response.Content.ReadFromJsonAsync<List<CountryResponse>>() ?? [];
            CountryOptions = countries.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
        }
    }

    public sealed class InputModel
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(4, MinimumLength = 4)]
        public string IcaoCode { get; set; } = string.Empty;

        [StringLength(3)]
        public string? IataCode { get; set; }

        [Required, StringLength(200)]
        public string City { get; set; } = string.Empty;

        [Required]
        public Guid CountryId { get; set; }
    }
}
