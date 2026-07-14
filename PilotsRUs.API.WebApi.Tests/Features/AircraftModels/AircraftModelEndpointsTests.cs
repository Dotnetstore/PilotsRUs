using System.Net;
using System.Net.Http.Json;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.AircraftModels;
using PilotsRUs.Shared.SDK.Manufacturers;

namespace PilotsRUs.API.WebApi.Tests.Features.AircraftModels;

public sealed class AircraftModelEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task List_ReturnsCreatedModels()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("models-list@pilotsrus.test", "P@ssw0rd123!");
        var manufacturer = await CreateManufacturerAsync(client, "Textron Aviation");
        await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("208 Caravan", "C208", manufacturer.Id));

        var response = await client.GetAsync("/aircraft-models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var models = await response.Content.ReadFromJsonAsync<List<AircraftModelResponse>>();
        Assert.NotNull(models);
        Assert.Contains(models!, m => m.Name == "208 Caravan" && m.ManufacturerName == "Textron Aviation");
    }

    [Fact]
    public async Task GetById_ForExistingModel_ReturnsModel()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("models-get@pilotsrus.test", "P@ssw0rd123!");
        var manufacturer = await CreateManufacturerAsync(client, "Gulfstream");
        var createResponse = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("G650", "GLF6", manufacturer.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AircraftModelResponse>();

        var response = await client.GetAsync($"/aircraft-models/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var model = await response.Content.ReadFromJsonAsync<AircraftModelResponse>();
        Assert.Equal("G650", model!.Name);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("models-create@pilotsrus.test", "P@ssw0rd123!");
        var manufacturer = await CreateManufacturerAsync(client, "Pilatus");

        var response = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("PC-12", "PC12", manufacturer.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var model = await response.Content.ReadFromJsonAsync<AircraftModelResponse>();
        Assert.Equal("PC12", model!.IcaoTypeDesignator);
    }

    [Fact]
    public async Task Create_WithUnknownManufacturer_ReturnsBadRequest()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("models-badmanufacturer@pilotsrus.test", "P@ssw0rd123!");

        var response = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("Unknown Model", null, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateNameUnderSameManufacturer_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("models-dup@pilotsrus.test", "P@ssw0rd123!");
        var manufacturer = await CreateManufacturerAsync(client, "Daher");
        await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("TBM 940", "TBM9", manufacturer.Id));

        var response = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("TBM 940", null, manufacturer.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithSameNameUnderDifferentManufacturer_Succeeds()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("models-scoped@pilotsrus.test", "P@ssw0rd123!");
        var manufacturerA = await CreateManufacturerAsync(client, "Robin Aircraft");
        var manufacturerB = await CreateManufacturerAsync(client, "Robin Aircraft 2");
        await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("DR400", null, manufacturerA.Id));

        var response = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("DR400", null, manufacturerB.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOk()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("models-update@pilotsrus.test", "P@ssw0rd123!");
        var manufacturer = await CreateManufacturerAsync(client, "Icon Aircraft");
        var createResponse = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("A5", null, manufacturer.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AircraftModelResponse>();

        var response = await client.PutAsJsonAsync($"/aircraft-models/{created!.Id}", new UpdateAircraftModelRequest("A5 Special Edition", "ICA5", manufacturer.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AircraftModelResponse>();
        Assert.Equal("A5 Special Edition", updated!.Name);
    }

    [Fact]
    public async Task Update_WithNameCollisionWithinSameManufacturer_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("models-update-dup@pilotsrus.test", "P@ssw0rd123!");
        var manufacturer = await CreateManufacturerAsync(client, "Robin Aircraft 3");
        await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("DR400", null, manufacturer.Id));
        var createResponse = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("DR250", null, manufacturer.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AircraftModelResponse>();

        var response = await client.PutAsJsonAsync($"/aircraft-models/{created!.Id}", new UpdateAircraftModelRequest("DR400", null, manufacturer.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ForExistingModel_RemovesIt()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("models-delete@pilotsrus.test", "P@ssw0rd123!");
        var manufacturer = await CreateManufacturerAsync(client, "Zenith Aircraft");
        var createResponse = await client.PostAsJsonAsync("/aircraft-models", new CreateAircraftModelRequest("CH 750", null, manufacturer.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<AircraftModelResponse>();

        var deleteResponse = await client.DeleteAsync($"/aircraft-models/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/aircraft-models/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/aircraft-models");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<ManufacturerResponse> CreateManufacturerAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest(name, null));
        return (await response.Content.ReadFromJsonAsync<ManufacturerResponse>())!;
    }
}
