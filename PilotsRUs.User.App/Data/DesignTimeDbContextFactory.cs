using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PilotsRUs.User.App.Data;

/// <summary>
/// Lets `dotnet ef migrations` build <see cref="UserAppDbContext"/> without running the full Avalonia app,
/// mirroring API.WebApi's DesignTimeDbContextFactory.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<UserAppDbContext>
{
    public UserAppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UserAppDbContext>()
            .UseSqlite($"Data Source={UserAppDbContext.GetDefaultDbPath()}");

        return new UserAppDbContext(optionsBuilder.Options);
    }
}
