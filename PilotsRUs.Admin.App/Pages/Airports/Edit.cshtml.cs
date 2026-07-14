using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using PilotsRUs.Shared.SDK.Airports;
using PilotsRUs.Shared.SDK.Countries;

namespace PilotsRUs.Admin.App.Pages.Airports;

public sealed class EditModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public List<SelectListItem> CountryOptions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/airports/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var airport = await response.Content.ReadFromJsonAsync<AirportResponse>();
        if (airport is null)
        {
            return NotFound();
        }

        Input = new InputModel { Name = airport.Name, IcaoCode = airport.IcaoCode, IataCode = airport.IataCode, City = airport.City, CountryId = airport.CountryId };
        await LoadCountryOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadCountryOptionsAsync();
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PutAsJsonAsync(
            $"/airports/{Id}",
            new UpdateAirportRequest(Input.Name, Input.IcaoCode, string.IsNullOrWhiteSpace(Input.IataCode) ? null : Input.IataCode, Input.City, Input.CountryId));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Airports/Index");
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
