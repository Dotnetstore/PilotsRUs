using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PilotsRUs.API.WebApi.Data;

namespace PilotsRUs.API.WebApi.Tests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Unique per instance (not a shared constant) so separate test classes - each getting their own
    // ApiFactory via IClassFixture - don't share EF Core's process-wide InMemory database store.
    private readonly string _testDatabaseName = $"PilotsRUsApiTests-{Guid.NewGuid()}";

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

            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_testDatabaseName));
            services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(_testDatabaseName));
        });
    }

    public async Task<ApplicationUser> CreateUserAsync(string email, string password)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        return user;
    }
}
