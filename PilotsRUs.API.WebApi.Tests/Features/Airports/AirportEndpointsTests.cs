using System.Net;
using System.Net.Http.Json;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.Airports;
using PilotsRUs.Shared.SDK.Countries;

namespace PilotsRUs.API.WebApi.Tests.Features.Airports;

// ICAO/IATA codes use a "ZZT"-prefixed convention - there's no ISO-style formally reserved block for
// airport codes (unlike ISO 3166-1's QM-QZ/XA-XZ country-code range used in CountryEndpointsTests), but
// this is a low-collision-risk best-effort convention since AirportSeeder is expected to eventually carry
// real airport data too. Follow the same convention for any future test that creates an Airport.
public sealed class AirportEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task List_ReturnsCreatedAirports()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("airports-list@pilotsrus.test", "P@ssw0rd123!");
        var country = await CreateCountryAsync(client, "Testlandia Airports List", "QM");
        await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Test Field List", "ZZTA", "ZTA", "Testville", country.Id));

        var response = await client.GetAsync("/airports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var airports = await response.Content.ReadFromJsonAsync<List<AirportResponse>>();
        Assert.NotNull(airports);
        Assert.Contains(airports!, a => a.Name == "Test Field List" && a.IcaoCode == "ZZTA" && a.CountryName == "Testlandia Airports List");
    }

    [Fact]
    public async Task GetById_ForExistingAirport_ReturnsAirport()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("airports-get@pilotsrus.test", "P@ssw0rd123!");
        var country = await CreateCountryAsync(client, "Testlandia Airports Get", "QN");
        var createResponse = await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Test Field Get", "ZZTB", "ZTB", "Testville", country.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AirportResponse>();

        var response = await client.GetAsync($"/airports/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var airport = await response.Content.ReadFromJsonAsync<AirportResponse>();
        Assert.Equal("Test Field Get", airport!.Name);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("airports-create@pilotsrus.test", "P@ssw0rd123!");
        var country = await CreateCountryAsync(client, "Testlandia Airports Create", "QO");

        var response = await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Test Field Create", "ZZTC", "ZTC", "Testville", country.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var airport = await response.Content.ReadFromJsonAsync<AirportResponse>();
        Assert.Equal("ZZTC", airport!.IcaoCode);
    }

    [Fact]
    public async Task Create_WithoutIataCode_Succeeds()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("airports-no-iata@pilotsrus.test", "P@ssw0rd123!");
        var country = await CreateCountryAsync(client, "Testlandia Airports NoIata", "QP");

        var response = await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Test Field NoIata", "ZZTD", null, "Testville", country.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var airport = await response.Content.ReadFromJsonAsync<AirportResponse>();
        Assert.Null(airport!.IataCode);
    }

    [Fact]
    public async Task Create_WithUnknownCountry_ReturnsBadRequest()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("airports-badcountry@pilotsrus.test", "P@ssw0rd123!");

        var response = await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Test Field Bad", "ZZTE", null, "Testville", Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateIcaoCode_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("airports-dup-icao@pilotsrus.test", "P@ssw0rd123!");
        var country = await CreateCountryAsync(client, "Testlandia Airports DupIcao", "QQ");
        await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Test Field DupIcao A", "ZZTF", null, "Testville", country.Id));

        var response = await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Test Field DupIcao B", "ZZTF", null, "Testville", country.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateIataCode_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("airports-dup-iata@pilotsrus.test", "P@ssw0rd123!");
        var country = await CreateCountryAsync(client, "Testlandia Airports DupIata", "QR");
        await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Test Field DupIata A", "ZZTG", "ZTG", "Testville", country.Id));

        var response = await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Test Field DupIata B", "ZZTH", "ZTG", "Testville", country.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOk()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("airports-update@pilotsrus.test", "P@ssw0rd123!");
        var country = await CreateCountryAsync(client, "Testlandia Airports Update", "QS");
        var createResponse = await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Old Field Name", "ZZTI", "ZTI", "Old City", country.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AirportResponse>();

        var response = await client.PutAsJsonAsync($"/airports/{created!.Id}", new UpdateAirportRequest("New Field Name", "ZZTJ", "ZTJ", "New City", country.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AirportResponse>();
        Assert.Equal("New Field Name", updated!.Name);
        Assert.Equal("ZZTJ", updated.IcaoCode);
    }

    [Fact]
    public async Task Update_WithIcaoTakenByAnother_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("airports-update-dup@pilotsrus.test", "P@ssw0rd123!");
        var country = await CreateCountryAsync(client, "Testlandia Airports UpdateDup", "QT");
        await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Field A", "ZZTK", null, "Testville", country.Id));
        var createResponse = await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Field B", "ZZTL", null, "Testville", country.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AirportResponse>();

        var response = await client.PutAsJsonAsync($"/airports/{created!.Id}", new UpdateAirportRequest("Field B", "ZZTK", null, "Testville", country.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ForExistingAirport_RemovesIt()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("airports-delete@pilotsrus.test", "P@ssw0rd123!");
        var country = await CreateCountryAsync(client, "Testlandia Airports Delete", "QU");
        var createResponse = await client.PostAsJsonAsync("/airports", new CreateAirportRequest("Field Delete", "ZZTM", null, "Testville", country.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AirportResponse>();

        var deleteResponse = await client.DeleteAsync($"/airports/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/airports/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/airports");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<CountryResponse> CreateCountryAsync(HttpClient client, string name, string alpha2)
    {
        var response = await client.PostAsJsonAsync("/countries", new CreateCountryRequest(name, alpha2, alpha2 + "X"));
        return (await response.Content.ReadFromJsonAsync<CountryResponse>())!;
    }
}
