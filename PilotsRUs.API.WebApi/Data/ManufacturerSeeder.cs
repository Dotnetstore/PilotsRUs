using Microsoft.EntityFrameworkCore;

namespace PilotsRUs.API.WebApi.Data;

/// <summary>
/// Seeds common real-world manufacturers relevant to MSFS 2024 aircraft. Runs unconditionally (every
/// environment, not just Development), idempotent - same pattern as <see cref="RoleSeeder"/>. Unlike
/// RoleSeeder, Manufacturer isn't an Identity entity, so this resolves
/// <see cref="IDbContextFactory{TContext}"/> directly instead of going through Identity's scoped DbContext.
/// </summary>
public static class ManufacturerSeeder
{
    private static readonly string[] SeedNames =
    [
        "Boeing", "Airbus", "Cessna", "Cirrus", "Embraer",
        "Bombardier", "Piper", "Beechcraft", "Mooney", "Diamond Aircraft"
    ];

    public static async Task SeedAsync(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var existingNames = await dbContext.Manufacturers.Select(m => m.Name).ToListAsync();
        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        var missing = SeedNames
            .Where(name => !existingSet.Contains(name))
            .Select(name => new Manufacturer { Id = Guid.NewGuid(), Name = name })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        dbContext.Manufacturers.AddRange(missing);
        await dbContext.SaveChangesAsync();
    }
}
