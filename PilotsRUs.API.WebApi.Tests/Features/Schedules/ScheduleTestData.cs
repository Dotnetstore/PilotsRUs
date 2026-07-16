using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PilotsRUs.Shared.SDK.AircraftModels;
using PilotsRUs.Shared.SDK.Aircrafts;
using PilotsRUs.Shared.SDK.Airports;
using PilotsRUs.Shared.SDK.Countries;
using PilotsRUs.Shared.SDK.Manufacturers;
using PilotsRUs.Shared.SDK.ScheduleTemplates;
using PilotsRUs.Shared.SDK.SoftwareDevelopers;

namespace PilotsRUs.API.WebApi.Tests.Features.Schedules;

// Shared setup helpers for ScheduleGeneratorTests/ScheduleEndpointsTests - both need a full
// Airport -> Aircraft -> ScheduleTemplate chain, same helper-method convention already used in
// ScheduleTemplateEndpointsTests.cs. Callers pass explicit ICAO/alpha-2 codes (same convention as
// ScheduleTemplateEndpointsTests) rather than deriving them from the qualifier, since deriving short codes
// from arbitrary-length qualifier strings risks collisions across test methods sharing one ApiFactory.
internal static class ScheduleTestData
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<ScheduleTemplateResponse> CreateDailyTemplateAsync(
        HttpClient client, string qualifier, DateOnly startDate,
        string departureIcao, string departureAlpha2, string arrivalIcao, string arrivalAlpha2,
        int distanceNauticalMiles = 500, TimeSpan? flightTime = null)
    {
        var departure = await CreateAirportAsync(client, $"{qualifier} Dep", departureIcao, departureAlpha2);
        var arrival = await CreateAirportAsync(client, $"{qualifier} Arr", arrivalIcao, arrivalAlpha2);
        var aircraft = await CreateAircraftAsync(client, qualifier);

        var response = await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest(
                $"SG{departureIcao[..2]}", departure.Id, arrival.Id, aircraft.Id,
                distanceNauticalMiles, flightTime ?? TimeSpan.FromHours(1), ScheduleFrequency.Daily, startDate),
            JsonOptions);
        return (await response.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions))!;
    }

    private static async Task<AirportResponse> CreateAirportAsync(HttpClient client, string qualifier, string icaoCode, string alpha2)
    {
        var countryResponse = await client.PostAsJsonAsync("/countries", new CreateCountryRequest($"Testlandia {qualifier}", alpha2, alpha2 + "X"));
        var country = await countryResponse.Content.ReadFromJsonAsync<CountryResponse>();
        var airportResponse = await client.PostAsJsonAsync("/airports", new CreateAirportRequest($"Test Field {qualifier}", icaoCode, null, "Testville", country!.Id));
        return (await airportResponse.Content.ReadFromJsonAsync<AirportResponse>())!;
    }

    private static async Task<AircraftResponse> CreateAircraftAsync(HttpClient client, string qualifier)
    {
        var manufacturerResponse = await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest($"Manufacturer {qualifier}", null));
        var manufacturer = await manufacturerResponse.Content.ReadFromJsonAsync<ManufacturerResponse>();

        var modelResponse = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest($"Model {qualifier}", null, manufacturer!.Id));
        var model = await modelResponse.Content.ReadFromJsonAsync<AircraftModelResponse>();

        var softwareDeveloperResponse = await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest($"SoftwareDeveloper {qualifier}"));
        var softwareDeveloper = await softwareDeveloperResponse.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();

        var aircraftResponse = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest($"N1{Guid.NewGuid():N}"[..10], 100, 10, 0, 1000, model!.Id, softwareDeveloper!.Id));
        return (await aircraftResponse.Content.ReadFromJsonAsync<AircraftResponse>())!;
    }
}
