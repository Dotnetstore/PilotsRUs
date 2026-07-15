using System.Net;
using System.Net.Http.Json;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.AircraftModels;
using PilotsRUs.Shared.SDK.Aircrafts;
using PilotsRUs.Shared.SDK.Manufacturers;
using PilotsRUs.Shared.SDK.SoftwareDevelopers;

namespace PilotsRUs.API.WebApi.Tests.Features.Aircrafts;

public sealed class AircraftEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task List_ReturnsCreatedAircraft()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("aircraft-list@pilotsrus.test", "P@ssw0rd123!");
        var (model, softwareDeveloper) = await CreateModelAndSoftwareDeveloperAsync(client, "Aircraft List");
        await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("N1LIST", 150, 20, 0, 2000, model.Id, softwareDeveloper.Id));

        var response = await client.GetAsync("/aircrafts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var aircraft = await response.Content.ReadFromJsonAsync<List<AircraftResponse>>();
        Assert.NotNull(aircraft);
        Assert.Contains(aircraft!, a => a.RegistrationNumber == "N1LIST" && a.AircraftModelName == model.Name && a.SoftwareDeveloperName == softwareDeveloper.Name);
    }

    [Fact]
    public async Task GetById_ForExistingAircraft_ReturnsAircraft()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("aircraft-get@pilotsrus.test", "P@ssw0rd123!");
        var (model, softwareDeveloper) = await CreateModelAndSoftwareDeveloperAsync(client, "Aircraft Get");
        var createResponse = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("N1GET", 150, 20, 0, 2000, model.Id, softwareDeveloper.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AircraftResponse>();

        var response = await client.GetAsync($"/aircrafts/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var aircraft = await response.Content.ReadFromJsonAsync<AircraftResponse>();
        Assert.Equal("N1GET", aircraft!.RegistrationNumber);
        Assert.Equal(model.ManufacturerName, aircraft.ManufacturerName);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("aircraft-create@pilotsrus.test", "P@ssw0rd123!");
        var (model, softwareDeveloper) = await CreateModelAndSoftwareDeveloperAsync(client, "Aircraft Create");

        var response = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("n1create", 150, 20, 8, 2500, model.Id, softwareDeveloper.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var aircraft = await response.Content.ReadFromJsonAsync<AircraftResponse>();
        Assert.Equal("N1CREATE", aircraft!.RegistrationNumber);
        Assert.Equal(150, aircraft.PassengerCapacityEconomy);
        Assert.Equal(20, aircraft.PassengerCapacityBusiness);
        Assert.Equal(8, aircraft.PassengerCapacityFirst);
        Assert.Equal(2500, aircraft.CargoCapacityKg);
    }

    [Fact]
    public async Task Create_WithUnknownAircraftModel_ReturnsBadRequest()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("aircraft-badmodel@pilotsrus.test", "P@ssw0rd123!");
        var (_, softwareDeveloper) = await CreateModelAndSoftwareDeveloperAsync(client, "Aircraft Bad Model");

        var response = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("N1BADMODEL", 150, 20, 0, 2000, Guid.NewGuid(), softwareDeveloper.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithUnknownSoftwareDeveloper_ReturnsBadRequest()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("aircraft-badswdev@pilotsrus.test", "P@ssw0rd123!");
        var (model, _) = await CreateModelAndSoftwareDeveloperAsync(client, "Aircraft Bad SwDev");

        var response = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("N1BADSWDEV", 150, 20, 0, 2000, model.Id, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateRegistrationNumber_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("aircraft-dup@pilotsrus.test", "P@ssw0rd123!");
        var (model, softwareDeveloper) = await CreateModelAndSoftwareDeveloperAsync(client, "Aircraft Dup");
        await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("N1DUP", 150, 20, 0, 2000, model.Id, softwareDeveloper.Id));

        var response = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("n1dup", 100, 10, 0, 1500, model.Id, softwareDeveloper.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOk()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("aircraft-update@pilotsrus.test", "P@ssw0rd123!");
        var (model, softwareDeveloper) = await CreateModelAndSoftwareDeveloperAsync(client, "Aircraft Update");
        var createResponse = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("N1UPD", 150, 20, 0, 2000, model.Id, softwareDeveloper.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AircraftResponse>();

        var response = await client.PutAsJsonAsync(
            $"/aircrafts/{created!.Id}",
            new UpdateAircraftRequest("N1UPD2", 160, 24, 8, 2600, model.Id, softwareDeveloper.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AircraftResponse>();
        Assert.Equal("N1UPD2", updated!.RegistrationNumber);
        Assert.Equal(160, updated.PassengerCapacityEconomy);
    }

    [Fact]
    public async Task Update_WithRegistrationNumberTakenByAnother_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("aircraft-update-dup@pilotsrus.test", "P@ssw0rd123!");
        var (model, softwareDeveloper) = await CreateModelAndSoftwareDeveloperAsync(client, "Aircraft Update Dup");
        await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("N1UPDDUPA", 150, 20, 0, 2000, model.Id, softwareDeveloper.Id));
        var createResponse = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("N1UPDDUPB", 150, 20, 0, 2000, model.Id, softwareDeveloper.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AircraftResponse>();

        var response = await client.PutAsJsonAsync(
            $"/aircrafts/{created!.Id}",
            new UpdateAircraftRequest("N1UPDDUPA", 150, 20, 0, 2000, model.Id, softwareDeveloper.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ForExistingAircraft_RemovesIt()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("aircraft-delete@pilotsrus.test", "P@ssw0rd123!");
        var (model, softwareDeveloper) = await CreateModelAndSoftwareDeveloperAsync(client, "Aircraft Delete");
        var createResponse = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("N1DEL", 150, 20, 0, 2000, model.Id, softwareDeveloper.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AircraftResponse>();

        var deleteResponse = await client.DeleteAsync($"/aircrafts/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/aircrafts/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/aircrafts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<(AircraftModelResponse Model, SoftwareDeveloperResponse SoftwareDeveloper)> CreateModelAndSoftwareDeveloperAsync(HttpClient client, string qualifier)
    {
        var manufacturerResponse = await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest($"Manufacturer {qualifier}", null));
        var manufacturer = await manufacturerResponse.Content.ReadFromJsonAsync<ManufacturerResponse>();

        var modelResponse = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest($"Model {qualifier}", null, manufacturer!.Id));
        var model = await modelResponse.Content.ReadFromJsonAsync<AircraftModelResponse>();

        var softwareDeveloperResponse = await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest($"SoftwareDeveloper {qualifier}"));
        var softwareDeveloper = await softwareDeveloperResponse.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();

        return (model!, softwareDeveloper!);
    }
}
