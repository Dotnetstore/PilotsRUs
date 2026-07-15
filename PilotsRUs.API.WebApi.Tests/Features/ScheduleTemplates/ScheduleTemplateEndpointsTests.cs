using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.AircraftModels;
using PilotsRUs.Shared.SDK.Aircrafts;
using PilotsRUs.Shared.SDK.Airports;
using PilotsRUs.Shared.SDK.Countries;
using PilotsRUs.Shared.SDK.Manufacturers;
using PilotsRUs.Shared.SDK.ScheduleTemplates;
using PilotsRUs.Shared.SDK.SoftwareDevelopers;

namespace PilotsRUs.API.WebApi.Tests.Features.ScheduleTemplates;

public sealed class ScheduleTemplateEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // PostAsJsonAsync/ReadFromJsonAsync use System.Text.Json's default options, which don't inherit
    // Program.cs's ConfigureHttpJsonOptions server-side JsonStringEnumConverter registration - the test
    // HttpClient is a separate JSON pipeline. ScheduleFrequency (the first enum in any API DTO) needs this
    // passed explicitly on every call that reads or writes a ScheduleTemplateResponse/Create-or-Update
    // request, otherwise deserializing the string-form "Daily" the server returns throws.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task List_ReturnsCreatedScheduleTemplates()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("scheduletemplates-list@pilotsrus.test", "P@ssw0rd123!");
        var departure = await CreateAirportAsync(client, "ST List Dep", "ZZTA", "QM");
        var arrival = await CreateAirportAsync(client, "ST List Arr", "ZZTB", "QN");
        var aircraft = await CreateAircraftAsync(client, "STList");
        await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest("ST100", departure.Id, arrival.Id, aircraft.Id, 500, TimeSpan.FromHours(1.5), ScheduleFrequency.Daily),
            JsonOptions);

        var response = await client.GetAsync("/schedule-templates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scheduleTemplates = await response.Content.ReadFromJsonAsync<List<ScheduleTemplateResponse>>(JsonOptions);
        Assert.NotNull(scheduleTemplates);
        Assert.Contains(scheduleTemplates!, s => s.FlightNumber == "ST100" && s.DepartureAirportIcaoCode == "ZZTA" && s.ArrivalAirportIcaoCode == "ZZTB");
    }

    [Fact]
    public async Task GetById_ForExistingScheduleTemplate_ReturnsScheduleTemplate()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("scheduletemplates-get@pilotsrus.test", "P@ssw0rd123!");
        var departure = await CreateAirportAsync(client, "ST Get Dep", "ZZTC", "QO");
        var arrival = await CreateAirportAsync(client, "ST Get Arr", "ZZTD", "QP");
        var aircraft = await CreateAircraftAsync(client, "STGet");
        var createResponse = await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest("ST200", departure.Id, arrival.Id, aircraft.Id, 600, TimeSpan.FromHours(2), ScheduleFrequency.Weekly),
            JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions);

        var response = await client.GetAsync($"/schedule-templates/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scheduleTemplate = await response.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions);
        Assert.Equal("ST200", scheduleTemplate!.FlightNumber);
        Assert.Equal(ScheduleFrequency.Weekly, scheduleTemplate.Frequency);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("scheduletemplates-create@pilotsrus.test", "P@ssw0rd123!");
        var departure = await CreateAirportAsync(client, "ST Create Dep", "ZZTE", "QQ");
        var arrival = await CreateAirportAsync(client, "ST Create Arr", "ZZTF", "QR");
        var aircraft = await CreateAircraftAsync(client, "STCreate");

        var response = await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest("ST300", departure.Id, arrival.Id, aircraft.Id, 700, TimeSpan.FromHours(1), ScheduleFrequency.EveryThirdDay),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var scheduleTemplate = await response.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions);
        Assert.Equal("ST300", scheduleTemplate!.FlightNumber);
        Assert.Equal(700, scheduleTemplate.DistanceNauticalMiles);
        Assert.Equal(TimeSpan.FromHours(1), scheduleTemplate.FlightTime);
        Assert.Equal(ScheduleFrequency.EveryThirdDay, scheduleTemplate.Frequency);
        Assert.Equal(aircraft.RegistrationNumber, scheduleTemplate.AircraftRegistrationNumber);
    }

    [Fact]
    public async Task Create_WithUnknownDepartureAirport_ReturnsBadRequest()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("scheduletemplates-baddep@pilotsrus.test", "P@ssw0rd123!");
        var arrival = await CreateAirportAsync(client, "ST BadDep Arr", "ZZTG", "QS");
        var aircraft = await CreateAircraftAsync(client, "STBadDep");

        var response = await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest("ST400", Guid.NewGuid(), arrival.Id, aircraft.Id, 500, TimeSpan.FromHours(1), ScheduleFrequency.Daily),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownArrivalAirport_ReturnsBadRequest()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("scheduletemplates-badarr@pilotsrus.test", "P@ssw0rd123!");
        var departure = await CreateAirportAsync(client, "ST BadArr Dep", "ZZTH", "QT");
        var aircraft = await CreateAircraftAsync(client, "STBadArr");

        var response = await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest("ST500", departure.Id, Guid.NewGuid(), aircraft.Id, 500, TimeSpan.FromHours(1), ScheduleFrequency.Daily),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithSameDepartureAndArrivalAirport_ReturnsBadRequest()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("scheduletemplates-sameairport@pilotsrus.test", "P@ssw0rd123!");
        var airport = await CreateAirportAsync(client, "ST Same", "ZZTI", "QU");
        var aircraft = await CreateAircraftAsync(client, "STSame");

        var response = await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest("ST600", airport.Id, airport.Id, aircraft.Id, 500, TimeSpan.FromHours(1), ScheduleFrequency.Daily),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownAircraft_ReturnsBadRequest()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("scheduletemplates-badaircraft@pilotsrus.test", "P@ssw0rd123!");
        var departure = await CreateAirportAsync(client, "ST BadAircraft Dep", "ZZTJ", "QV");
        var arrival = await CreateAirportAsync(client, "ST BadAircraft Arr", "ZZTK", "QW");

        var response = await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest("ST700", departure.Id, arrival.Id, Guid.NewGuid(), 500, TimeSpan.FromHours(1), ScheduleFrequency.Daily),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOk()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("scheduletemplates-update@pilotsrus.test", "P@ssw0rd123!");
        var departure = await CreateAirportAsync(client, "ST Update Dep", "ZZTL", "QX");
        var arrival = await CreateAirportAsync(client, "ST Update Arr", "ZZTM", "QY");
        var aircraft = await CreateAircraftAsync(client, "STUpdate");
        var createResponse = await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest("ST800", departure.Id, arrival.Id, aircraft.Id, 500, TimeSpan.FromHours(1), ScheduleFrequency.Daily),
            JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions);

        var response = await client.PutAsJsonAsync(
            $"/schedule-templates/{created!.Id}",
            new UpdateScheduleTemplateRequest("ST801", departure.Id, arrival.Id, aircraft.Id, 550, TimeSpan.FromHours(1.25), ScheduleFrequency.Weekly),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions);
        Assert.Equal("ST801", updated!.FlightNumber);
        Assert.Equal(550, updated.DistanceNauticalMiles);
        Assert.Equal(ScheduleFrequency.Weekly, updated.Frequency);
    }

    [Fact]
    public async Task Delete_ForExistingScheduleTemplate_RemovesIt()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("scheduletemplates-delete@pilotsrus.test", "P@ssw0rd123!");
        var departure = await CreateAirportAsync(client, "ST Delete Dep", "ZZTN", "QZ");
        var arrival = await CreateAirportAsync(client, "ST Delete Arr", "ZZTO", "XA");
        var aircraft = await CreateAircraftAsync(client, "STDelete");
        var createResponse = await client.PostAsJsonAsync(
            "/schedule-templates",
            new CreateScheduleTemplateRequest("ST900", departure.Id, arrival.Id, aircraft.Id, 500, TimeSpan.FromHours(1), ScheduleFrequency.Daily),
            JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions);

        var deleteResponse = await client.DeleteAsync($"/schedule-templates/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/schedule-templates/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/schedule-templates");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

        var aircraftResponse = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest($"N1{qualifier}", 100, 10, 0, 1000, model!.Id, softwareDeveloper!.Id));
        return (await aircraftResponse.Content.ReadFromJsonAsync<AircraftResponse>())!;
    }
}
