using System.Net;
using System.Net.Http.Json;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.AircraftModels;
using PilotsRUs.Shared.SDK.Aircrafts;
using PilotsRUs.Shared.SDK.Manufacturers;
using PilotsRUs.Shared.SDK.SoftwareDevelopers;

namespace PilotsRUs.API.WebApi.Tests.Features.SoftwareDevelopers;

public sealed class SoftwareDeveloperEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task List_ReturnsCreatedSoftwareDevelopers()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("softwaredevs-list@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest("PMDG"));

        var response = await client.GetAsync("/software-developers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var softwareDevelopers = await response.Content.ReadFromJsonAsync<List<SoftwareDeveloperResponse>>();
        Assert.NotNull(softwareDevelopers);
        Assert.Contains(softwareDevelopers!, s => s.Name == "PMDG");
    }

    [Fact]
    public async Task GetById_ForExistingSoftwareDeveloper_ReturnsSoftwareDeveloper()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("softwaredevs-get@pilotsrus.test", "P@ssw0rd123!");
        var createResponse = await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest("Fenix Simulations"));
        var created = await createResponse.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();

        var response = await client.GetAsync($"/software-developers/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var softwareDeveloper = await response.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();
        Assert.Equal("Fenix Simulations", softwareDeveloper!.Name);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("softwaredevs-create@pilotsrus.test", "P@ssw0rd123!");

        var response = await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest("FlyByWire Simulations"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var softwareDeveloper = await response.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();
        Assert.Equal("FlyByWire Simulations", softwareDeveloper!.Name);
    }

    [Fact]
    public async Task Create_WithDuplicateName_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("softwaredevs-dup@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest("Aerosoft"));

        var response = await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest("Aerosoft"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOk()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("softwaredevs-update@pilotsrus.test", "P@ssw0rd123!");
        var createResponse = await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest("iniBuilds"));
        var created = await createResponse.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();

        var response = await client.PutAsJsonAsync($"/software-developers/{created!.Id}", new UpdateSoftwareDeveloperRequest("iniBuilds Simulations"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();
        Assert.Equal("iniBuilds Simulations", updated!.Name);
    }

    [Fact]
    public async Task Update_WithNameTakenByAnother_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("softwaredevs-update-dup@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest("Just Flight"));
        var createResponse = await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest("Just Flight 2"));
        var created = await createResponse.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();

        var response = await client.PutAsJsonAsync($"/software-developers/{created!.Id}", new UpdateSoftwareDeveloperRequest("Just Flight"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ForExistingSoftwareDeveloper_RemovesIt()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("softwaredevs-delete@pilotsrus.test", "P@ssw0rd123!");
        var createResponse = await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest("Carenado"));
        var created = await createResponse.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();

        var deleteResponse = await client.DeleteAsync($"/software-developers/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/software-developers/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_WithExistingAircraft_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("softwaredevs-delete-guard@pilotsrus.test", "P@ssw0rd123!");
        var manufacturerResponse = await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest("Boeing SWDG Test", null));
        var manufacturer = await manufacturerResponse.Content.ReadFromJsonAsync<ManufacturerResponse>();
        var modelResponse = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("737-800 SWDG Test", null, manufacturer!.Id));
        var model = await modelResponse.Content.ReadFromJsonAsync<AircraftModelResponse>();
        var softwareDeveloperResponse = await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest("PMDG SWDG Test"));
        var softwareDeveloper = await softwareDeveloperResponse.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();
        var aircraftResponse = await client.PostAsJsonAsync("/aircrafts", new CreateAircraftRequest("N100SD", 150, 20, 0, 2000, model!.Id, softwareDeveloper!.Id));
        var aircraft = await aircraftResponse.Content.ReadFromJsonAsync<AircraftResponse>();

        var blockedResponse = await client.DeleteAsync($"/software-developers/{softwareDeveloper.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blockedResponse.StatusCode);

        var deleteAircraftResponse = await client.DeleteAsync($"/aircrafts/{aircraft!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteAircraftResponse.StatusCode);

        var allowedResponse = await client.DeleteAsync($"/software-developers/{softwareDeveloper.Id}");
        Assert.Equal(HttpStatusCode.NoContent, allowedResponse.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/software-developers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
