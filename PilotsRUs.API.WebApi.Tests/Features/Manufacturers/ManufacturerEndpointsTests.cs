using System.Net;
using System.Net.Http.Json;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.Manufacturers;

namespace PilotsRUs.API.WebApi.Tests.Features.Manufacturers;

public sealed class ManufacturerEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task List_ReturnsCreatedManufacturers()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("manufacturers-list@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest("Textron", "TXN"));

        var response = await client.GetAsync("/manufacturers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var manufacturers = await response.Content.ReadFromJsonAsync<List<ManufacturerResponse>>();
        Assert.NotNull(manufacturers);
        Assert.Contains(manufacturers!, m => m.Name == "Textron");
    }

    [Fact]
    public async Task GetById_ForExistingManufacturer_ReturnsManufacturer()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("manufacturers-get@pilotsrus.test", "P@ssw0rd123!");
        var createResponse = await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest("Gulfstream", null));
        var created = await createResponse.Content.ReadFromJsonAsync<ManufacturerResponse>();

        var response = await client.GetAsync($"/manufacturers/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var manufacturer = await response.Content.ReadFromJsonAsync<ManufacturerResponse>();
        Assert.Equal("Gulfstream", manufacturer!.Name);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("manufacturers-create@pilotsrus.test", "P@ssw0rd123!");

        var response = await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest("Pilatus", "PC"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var manufacturer = await response.Content.ReadFromJsonAsync<ManufacturerResponse>();
        Assert.Equal("PC", manufacturer!.Code);
    }

    [Fact]
    public async Task Create_WithDuplicateName_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("manufacturers-dup@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest("Textron Aviation", null));

        var response = await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest("Textron Aviation", null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithValidRequest_ReturnsOk()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("manufacturers-update@pilotsrus.test", "P@ssw0rd123!");
        var createResponse = await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest("Daher", null));
        var created = await createResponse.Content.ReadFromJsonAsync<ManufacturerResponse>();

        var response = await client.PutAsJsonAsync($"/manufacturers/{created!.Id}", new UpdateManufacturerRequest("Daher Aerospace", "DHR"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ManufacturerResponse>();
        Assert.Equal("Daher Aerospace", updated!.Name);
        Assert.Equal("DHR", updated.Code);
    }

    [Fact]
    public async Task Update_WithNameTakenByAnother_ReturnsConflict()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("manufacturers-update-dup@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest("Robin Aircraft", null));
        var createResponse = await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest("Robin Aircraft 2", null));
        var created = await createResponse.Content.ReadFromJsonAsync<ManufacturerResponse>();

        var response = await client.PutAsJsonAsync($"/manufacturers/{created!.Id}", new UpdateManufacturerRequest("Robin Aircraft", null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ForExistingManufacturer_RemovesIt()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("manufacturers-delete@pilotsrus.test", "P@ssw0rd123!");
        var createResponse = await client.PostAsJsonAsync("/manufacturers", new CreateManufacturerRequest("Icon Aircraft", null));
        var created = await createResponse.Content.ReadFromJsonAsync<ManufacturerResponse>();

        var deleteResponse = await client.DeleteAsync($"/manufacturers/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/manufacturers/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/manufacturers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
