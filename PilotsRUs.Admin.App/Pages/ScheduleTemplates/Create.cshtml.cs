using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using PilotsRUs.Shared.SDK.Aircrafts;
using PilotsRUs.Shared.SDK.Airports;
using PilotsRUs.Shared.SDK.ScheduleTemplates;

namespace PilotsRUs.Admin.App.Pages.ScheduleTemplates;

public sealed class CreateModel(IHttpClientFactory httpClientFactory) : PageModel
{
    // See IndexModel.JsonOptions - the "Api" HttpClient doesn't inherit the API's server-side
    // JsonStringEnumConverter registration, so ScheduleFrequency needs it passed explicitly.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Dictionary<ScheduleFrequency, string> FrequencyLabels = new()
    {
        [ScheduleFrequency.Daily] = "Daily",
        [ScheduleFrequency.EverySecondDay] = "Every 2 Days",
        [ScheduleFrequency.EveryThirdDay] = "Every 3 Days",
        [ScheduleFrequency.EveryFourthDay] = "Every 4 Days",
        [ScheduleFrequency.EveryFifthDay] = "Every 5 Days",
        [ScheduleFrequency.EverySixthDay] = "Every 6 Days",
        [ScheduleFrequency.Weekly] = "Weekly"
    };

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> DepartureAirportOptions { get; set; } = [];

    public List<SelectListItem> ArrivalAirportOptions { get; set; } = [];

    public List<SelectListItem> AircraftOptions { get; set; } = [];

    public List<SelectListItem> FrequencyOptions { get; set; } = [];

    public async Task OnGetAsync()
    {
        await LoadOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest(
                Input.FlightNumber, Input.DepartureAirportId, Input.ArrivalAirportId, Input.AircraftId,
                Input.DistanceNauticalMiles, Input.FlightTime.ToTimeSpan(), Input.Frequency),
            JsonOptions);

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/ScheduleTemplates/Index");
        }

        // ScheduleTemplate has zero uniqueness rules - only the FK-existence/same-airport validation
        // returns Results.BadRequest(string), same JSON-string-literal pattern used throughout.
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadFromJsonAsync<string>();
            ModelState.AddModelError(string.Empty, message ?? "Create failed.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Create failed.");
        }

        await LoadOptionsAsync();
        return Page();
    }

    private async Task LoadOptionsAsync()
    {
        var client = httpClientFactory.CreateClient("Api");

        var airportsResponse = await client.GetAsync("/airports");
        if (airportsResponse.IsSuccessStatusCode)
        {
            var airports = await airportsResponse.Content.ReadFromJsonAsync<List<AirportResponse>>() ?? [];
            // Two independent SelectListItem lists (not one shared reference) - each <select> tag helper
            // may mark items as selected against the bound value, so sharing instances across the two
            // dropdowns could let one dropdown's selection state bleed into the other.
            DepartureAirportOptions = airports.Select(a => new SelectListItem($"{a.IcaoCode} - {a.Name}", a.Id.ToString())).ToList();
            ArrivalAirportOptions = airports.Select(a => new SelectListItem($"{a.IcaoCode} - {a.Name}", a.Id.ToString())).ToList();
        }

        var aircraftResponse = await client.GetAsync("/aircrafts");
        if (aircraftResponse.IsSuccessStatusCode)
        {
            var aircraft = await aircraftResponse.Content.ReadFromJsonAsync<List<AircraftResponse>>() ?? [];
            AircraftOptions = aircraft.Select(a => new SelectListItem($"{a.RegistrationNumber} ({a.AircraftModelName})", a.Id.ToString())).ToList();
        }

        FrequencyOptions = FrequencyLabels.Select(kvp => new SelectListItem(kvp.Value, kvp.Key.ToString())).ToList();
    }

    public sealed class InputModel
    {
        [Required, StringLength(10)]
        public string FlightNumber { get; set; } = string.Empty;

        [Required]
        public Guid DepartureAirportId { get; set; }

        [Required]
        public Guid ArrivalAirportId { get; set; }

        [Required]
        public Guid AircraftId { get; set; }

        [Required, Range(0, int.MaxValue)]
        public int DistanceNauticalMiles { get; set; }

        [Required, DataType(DataType.Time)]
        public TimeOnly FlightTime { get; set; }

        [Required]
        public ScheduleFrequency Frequency { get; set; }
    }
}
