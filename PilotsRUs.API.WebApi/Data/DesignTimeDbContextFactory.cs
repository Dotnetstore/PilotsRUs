using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PilotsRUs.API.WebApi.Data;

/// <summary>
/// Lets `dotnet ef migrations` build <see cref="ApplicationDbContext"/> without running the Aspire
/// AppHost, since the context is registered via AddNpgsqlDbContext/AddDbContextFactory at runtime rather
/// than the classic AddDbContext(options => ...) pattern the EF tools auto-discover.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("pilotsrus")
            ?? "Host=localhost;Database=pilotsrus;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
