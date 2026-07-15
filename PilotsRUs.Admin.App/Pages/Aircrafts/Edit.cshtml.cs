using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using PilotsRUs.Shared.SDK.AircraftModels;
using PilotsRUs.Shared.SDK.Aircrafts;
using PilotsRUs.Shared.SDK.SoftwareDevelopers;

namespace PilotsRUs.Admin.App.Pages.Aircrafts;

public sealed class EditModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public List<SelectListItem> AircraftModelOptions { get; set; } = [];

    public List<SelectListItem> SoftwareDeveloperOptions { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/aircrafts/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var aircraft = await response.Content.ReadFromJsonAsync<AircraftResponse>();
        if (aircraft is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            RegistrationNumber = aircraft.RegistrationNumber,
            PassengerCapacityEconomy = aircraft.PassengerCapacityEconomy,
            PassengerCapacityBusiness = aircraft.PassengerCapacityBusiness,
            PassengerCapacityFirst = aircraft.PassengerCapacityFirst,
            CargoCapacityKg = aircraft.CargoCapacityKg,
            AircraftModelId = aircraft.AircraftModelId,
            SoftwareDeveloperId = aircraft.SoftwareDeveloperId
        };

        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PutAsJsonAsync(
            $"/aircrafts/{Id}",
            new UpdateAircraftRequest(
                Input.RegistrationNumber,
                Input.PassengerCapacityEconomy, Input.PassengerCapacityBusiness, Input.PassengerCapacityFirst, Input.CargoCapacityKg,
                Input.AircraftModelId, Input.SoftwareDeveloperId));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Aircrafts/Index");
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

        await LoadOptionsAsync();
        return Page();
    }

    private async Task LoadOptionsAsync()
    {
        var client = httpClientFactory.CreateClient("Api");

        var modelsResponse = await client.GetAsync("/aircraft-models");
        if (modelsResponse.IsSuccessStatusCode)
        {
            var models = await modelsResponse.Content.ReadFromJsonAsync<List<AircraftModelResponse>>() ?? [];
            AircraftModelOptions = models.Select(m => new SelectListItem($"{m.ManufacturerName} {m.Name}", m.Id.ToString())).ToList();
        }

        var softwareDevelopersResponse = await client.GetAsync("/software-developers");
        if (softwareDevelopersResponse.IsSuccessStatusCode)
        {
            var softwareDevelopers = await softwareDevelopersResponse.Content.ReadFromJsonAsync<List<SoftwareDeveloperResponse>>() ?? [];
            SoftwareDeveloperOptions = softwareDevelopers.Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList();
        }
    }

    public sealed class InputModel
    {
        [Required, StringLength(20)]
        public string RegistrationNumber { get; set; } = string.Empty;

        [Required, Range(0, int.MaxValue)]
        public int PassengerCapacityEconomy { get; set; }

        [Required, Range(0, int.MaxValue)]
        public int PassengerCapacityBusiness { get; set; }

        [Required, Range(0, int.MaxValue)]
        public int PassengerCapacityFirst { get; set; }

        [Required, Range(0, int.MaxValue)]
        public int CargoCapacityKg { get; set; }

        [Required]
        public Guid AircraftModelId { get; set; }

        [Required]
        public Guid SoftwareDeveloperId { get; set; }
    }
}
