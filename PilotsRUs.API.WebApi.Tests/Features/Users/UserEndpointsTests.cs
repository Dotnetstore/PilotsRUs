using System.Net;
using System.Net.Http.Json;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.Auth;
using PilotsRUs.Shared.SDK.Users;

namespace PilotsRUs.API.WebApi.Tests.Features.Users;

public sealed class UserEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task List_ReturnsCreatedUsers()
    {
        var (client, _) = await factory.CreateAuthenticatedAdminClientAsync("users-list-admin@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/users", new CreateUserRequest("users-list-member@pilotsrus.test", "First", "Last", "P@ssw0rd123!", false));

        var response = await client.GetAsync("/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        Assert.NotNull(users);
        Assert.Contains(users!, u => u.Email == "users-list-member@pilotsrus.test");
    }

    [Fact]
    public async Task GetById_ForExistingUser_ReturnsUser()
    {
        var (client, _) = await factory.CreateAuthenticatedAdminClientAsync("users-get-admin@pilotsrus.test", "P@ssw0rd123!");
        var createResponse = await client.PostAsJsonAsync("/users", new CreateUserRequest("users-get-member@pilotsrus.test", "First", "Last", "P@ssw0rd123!", false));
        var created = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        var response = await client.GetAsync($"/users/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("users-get-member@pilotsrus.test", user!.Email);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        var (client, _) = await factory.CreateAuthenticatedAdminClientAsync("users-create-admin@pilotsrus.test", "P@ssw0rd123!");

        var response = await client.PostAsJsonAsync("/users", new CreateUserRequest("users-create-member@pilotsrus.test", "First", "Last", "P@ssw0rd123!", true));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.True(user!.IsAdmin);
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_ReturnsBadRequestWithEmailField()
    {
        var (client, _) = await factory.CreateAuthenticatedAdminClientAsync("users-dup-admin@pilotsrus.test", "P@ssw0rd123!");
        await client.PostAsJsonAsync("/users", new CreateUserRequest("users-dup-member@pilotsrus.test", "First", "Last", "P@ssw0rd123!", false));

        var response = await client.PostAsJsonAsync("/users", new CreateUserRequest("users-dup-member@pilotsrus.test", "First", "Last", "P@ssw0rd123!", false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<UserValidationProblem>();
        Assert.Contains(problem!.Errors, e => e.Field == "Email");
    }

    [Fact]
    public async Task Update_RemovingAdminRoleFromLastActiveAdmin_ReturnsConflict()
    {
        // The "last active admin" guard counts every active admin in the system, so this needs its own
        // isolated factory/database rather than the shared class-fixture one - other tests in this class
        // also create admins there, which would make this guard never trigger.
        using var isolatedFactory = new ApiFactory();
        var (client, admin) = await isolatedFactory.CreateAuthenticatedAdminClientAsync("users-lastadmin-update@pilotsrus.test", "P@ssw0rd123!");

        var response = await client.PutAsJsonAsync($"/users/{admin.Id}", new UpdateUserRequest(admin.Email!, admin.FirstName, admin.LastName, false));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_Self_ReturnsBadRequest()
    {
        var (client, admin) = await factory.CreateAuthenticatedAdminClientAsync("users-self-deactivate@pilotsrus.test", "P@ssw0rd123!");

        var response = await client.PostAsync($"/users/{admin.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_LastActiveAdmin_ReturnsConflict()
    {
        // Same isolation reasoning as Update_RemovingAdminRoleFromLastActiveAdmin_ReturnsConflict above -
        // this guard counts every active admin in the system.
        using var isolatedFactory = new ApiFactory();
        var (adminClient, admin) = await isolatedFactory.CreateAuthenticatedAdminClientAsync("users-lastadmin-deactivate@pilotsrus.test", "P@ssw0rd123!");
        var (otherAdminClient, otherAdmin) = await isolatedFactory.CreateAuthenticatedAdminClientAsync("users-lastadmin-deactivate-other@pilotsrus.test", "P@ssw0rd123!");

        // Deactivating the other admin first leaves `admin` as the sole remaining active admin.
        var firstDeactivateResponse = await adminClient.PostAsync($"/users/{otherAdmin.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, firstDeactivateResponse.StatusCode);

        // otherAdminClient's access token was issued before deactivation, so it's still a valid bearer
        // credential (JWTs aren't revoked, only refresh tokens are) - use it as a non-self caller to
        // reach the last-active-admin guard rather than the self-deactivation guard.
        var secondDeactivateResponse = await otherAdminClient.PostAsync($"/users/{admin.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, secondDeactivateResponse.StatusCode);
    }

    [Fact]
    public async Task Deactivate_NonLastAdmin_RevokesLoginAndReactivateRestoresIt()
    {
        var (adminClient, _) = await factory.CreateAuthenticatedAdminClientAsync("users-deactivate-admin@pilotsrus.test", "P@ssw0rd123!");
        const string memberEmail = "users-deactivate-member@pilotsrus.test";
        const string memberPassword = "P@ssw0rd123!";
        var createResponse = await adminClient.PostAsJsonAsync("/users", new CreateUserRequest(memberEmail, "First", "Last", memberPassword, false));
        var member = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        var deactivateResponse = await adminClient.PostAsync($"/users/{member!.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        using var anonymousClient = factory.CreateClient();
        var loginAfterDeactivate = await anonymousClient.PostAsJsonAsync("/auth/login", new LoginRequest(memberEmail, memberPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, loginAfterDeactivate.StatusCode);

        var reactivateResponse = await adminClient.PostAsync($"/users/{member.Id}/reactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, reactivateResponse.StatusCode);

        var loginAfterReactivate = await anonymousClient.PostAsJsonAsync("/auth/login", new LoginRequest(memberEmail, memberPassword));
        Assert.Equal(HttpStatusCode.OK, loginAfterReactivate.StatusCode);
    }
}
