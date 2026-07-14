using System.Net;
using System.Net.Http.Json;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.Countries;

namespace PilotsRUs.API.WebApi.Tests.Features.Countries;

// Codes use ISO 3166-1's reserved "user-assigned" ranges (QM-QZ, XA-XZ), which are guaranteed to never be
// assigned to a real country - safe against colliding with CountrySeeder's real ISO 3166-1 seed data,
// which now runs for every test via the shared ApiFactory/WebApplicationFactory host.
public sealed class CountryEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task List_ReturnsCreatedCountries()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("countries-list@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia List", "QM", "QMA"));

        var response = await client.GetAsync("/countries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var countries = await response.Content.ReadFromJsonAsync<List<CountryResponse>>();
        Assert.NotNull(countries);
        Assert.Contains(countries!, c => c.Name == "Testlandia List" && c.IsoAlpha2Code == "QM" && c.IsoAlpha3Code == "QMA");
    }

    [Fact]
    public async Task GetById_ForExistingCountry_ReturnsCountry()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("countries-get@pilotsrus.test", "P@ssw0rd123!");
        var createResponse = await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Get", "QN", "QNB"));
        var created = await createResponse.Content.ReadFromJsonAsync<CountryResponse>();

        var response = await client.GetAsync($"/countries/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var country = await response.Content.ReadFromJsonAsync<CountryResponse>();
        Assert.Equal("Testlandia Get", country!.Name);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("countries-create@pilotsrus.test", "P@ssw0rd123!");

        var response = await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Create", "QO", "QOC"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var country = await response.Content.ReadFromJsonAsync<CountryResponse>();
        Assert.Equal("QO", country!.IsoAlpha2Code);
    }

    [Fact]
    public async Task Create_WithDuplicateName_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("countries-dup-name@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia DupName", "QP", "QPD"));

        var response = await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia DupName", "QQ", "QQE"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateAlpha2Code_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("countries-dup-alpha2@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Alpha2 A", "QR", "QRF"));

        var response = await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Alpha2 B", "QR", "QRG"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateAlpha3Code_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("countries-dup-alpha3@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Alpha3 A", "QS", "QSH"));

        var response = await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Alpha3 B", "QT", "QSH"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithLowercaseCodes_NormalizesToUppercaseAndEnforcesUniqueness()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("countries-normalize@pilotsrus.test", "P@ssw0rd123!");

        var createResponse = await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Normalize", "qu", "qui"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CountryResponse>();
        Assert.Equal("QU", created!.IsoAlpha2Code);
        Assert.Equal("QUI", created.IsoAlpha3Code);

        var conflictResponse = await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Normalize Other", "QU", "OTH"));
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOk()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("countries-update@pilotsrus.test", "P@ssw0rd123!");
        var createResponse = await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Old Name", "QV", "QVJ"));
        var created = await createResponse.Content.ReadFromJsonAsync<CountryResponse>();

        var response = await client.PutAsJsonAsync($"/countries/{created!.Id}", new UpdateCountryRequest("Testlandia New Name", "QW", "QWK"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CountryResponse>();
        Assert.Equal("Testlandia New Name", updated!.Name);
        Assert.Equal("QW", updated.IsoAlpha2Code);
    }

    [Fact]
    public async Task Update_WithNameTakenByAnother_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("countries-update-dup@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Nine A", "QX", "QXL"));
        var createResponse = await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Nine B", "QY", "QYM"));
        var created = await createResponse.Content.ReadFromJsonAsync<CountryResponse>();

        var response = await client.PutAsJsonAsync($"/countries/{created!.Id}", new UpdateCountryRequest("Testlandia Nine A", "QY", "QYM"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ForExistingCountry_RemovesIt()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("countries-delete@pilotsrus.test", "P@ssw0rd123!");
        var createResponse = await client.PostAsJsonAsync("/countries", new CreateCountryRequest("Testlandia Delete", "QZ", "QZN"));
        var created = await createResponse.Content.ReadFromJsonAsync<CountryResponse>();

        var deleteResponse = await client.DeleteAsync($"/countries/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/countries/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/countries");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
