using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.Auth;

namespace PilotsRUs.API.WebApi.Tests.Features.Auth;

public sealed class LoginEndpointTests(LoginEndpointTests.ApiFactory factory) : IClassFixture<LoginEndpointTests.ApiFactory>
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        const string email = "valid-user@pilotsrus.test";
        const string password = "P@ssw0rd123!";
        await factory.CreateUserAsync(email, password);

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        const string email = "wrong-password-user@pilotsrus.test";
        await factory.CreateUserAsync(email, "CorrectP@ssw0rd1");

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private const string TestDatabaseName = "PilotsRUsLoginEndpointTests";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:pilotsrus"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                    ["Jwt:Key"] = "Test-Only-Signing-Key-Not-For-Production-Use-0123456789",
                    ["Jwt:Issuer"] = "PilotsRUs.API.Tests",
                    ["Jwt:Audience"] = "PilotsRUs.Admin.Tests"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Aspire's AddNpgsqlDbContext registers ApplicationDbContext via AddDbContextPool, which
                // spreads its wiring across several service types (pool, factory, options configuration).
                // Removing just DbContextOptions<T> leaves the rest pointing at Npgsql, so remove anything
                // closing over ApplicationDbContext and replace it with an EF InMemory-backed registration.
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(ApplicationDbContext)
                        || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(ApplicationDbContext))))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(TestDatabaseName));
                services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(TestDatabaseName));
            });
        }

        public async Task CreateUserAsync(string email, string password)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
