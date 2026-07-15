using Microsoft.EntityFrameworkCore;

namespace PilotsRUs.API.WebApi.Data;

/// <summary>
/// Seeds common real-world aircraft models for each seeded manufacturer. Runs unconditionally (every
/// environment), idempotent - same pattern as <see cref="ManufacturerSeeder"/>. Must run after
/// ManufacturerSeeder, since it resolves ManufacturerIds by name.
/// </summary>
public static class AircraftModelSeeder
{
    // Best-effort real ICAO Doc 8643 type designators for common variants - not guaranteed authoritative
    // for every seeded model, but populated where confidently known (unlike Manufacturer.Code, which has
    // no equivalent registry to draw from).
    private static readonly Dictionary<string, (string Name, string? Icao)[]> SeedModelsByManufacturer = new()
    {
        ["Boeing"] =
        [
            ("737 MAX 8", "B38M"), ("737-800", "B738"), ("747-8", "B748"),
            ("777-300ER", "B77W"), ("787-9 Dreamliner", "B789")
        ],
        ["Airbus"] =
        [
            ("A320neo", "A20N"), ("A321neo", "A21N"), ("A330-300", "A333"),
            ("A350-900", "A359"), ("A380-800", "A388")
        ],
        ["Cessna"] =
        [
            ("152", "C152"), ("172 Skyhawk", "C172"), ("182 Skylane", "C182"),
            ("208 Caravan", "C208"), ("Citation CJ4", "C25C")
        ],
        ["Cirrus"] = [("SR20", "SR20"), ("SR22", "SR22"), ("Vision SF50", "SF50")],
        ["Embraer"] = [("E175", "E175"), ("E190", "E190"), ("Phenom 300", "E55P")],
        ["Bombardier"] = [("CRJ900", "CRJ9"), ("Challenger 350", "CL35"), ("Global 7500", "GL7T")],
        ["Piper"] = [("PA-28 Cherokee", "P28A"), ("PA-34 Seneca", "PA34"), ("PA-46 Malibu Meridian", "P46T")],
        ["Beechcraft"] = [("Bonanza G36", "BE36"), ("King Air 350", "B350"), ("Baron G58", "BE58")],
        ["Mooney"] = [("M20V Acclaim", "M20P")],
        ["Diamond Aircraft"] = [("DA40 NG", "DA40"), ("DA42 Twin Star", "DA42"), ("DA62", "DA62")]
    };

    public static async Task SeedAsync(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var manufacturerIdsByName = await dbContext.Manufacturers
            .ToDictionaryAsync(m => m.Name, m => m.Id, StringComparer.OrdinalIgnoreCase);

        var existingPairs = await dbContext.AircraftModels
            .Select(a => new { a.ManufacturerId, a.Name })
            .ToListAsync();
        var existingPairSet = existingPairs
            .Select(p => (p.ManufacturerId, p.Name))
            .ToHashSet();

        var existingIcaoCodes = (await dbContext.AircraftModels
                .Where(a => a.IcaoTypeDesignator != null)
                .Select(a => a.IcaoTypeDesignator!)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = new List<AircraftModel>();
        foreach (var (manufacturerName, models) in SeedModelsByManufacturer)
        {
            if (!manufacturerIdsByName.TryGetValue(manufacturerName, out var manufacturerId))
            {
                continue; // Manufacturer wasn't seeded (renamed/removed) - skip rather than fail startup.
            }

            foreach (var (name, icao) in models)
            {
                // Checks against both already-committed rows AND entries already queued earlier in this
                // same pass - guards against an accidental duplicate (ManufacturerId, Name) pair or ICAO
                // type designator within SeedModelsByManufacturer causing an unhandled unique-index
                // violation when AddRange + SaveChangesAsync runs, which would otherwise crash startup.
                // Same pattern as AirportSeeder.
                if (!existingPairSet.Add((manufacturerId, name)))
                {
                    continue;
                }

                if (icao is not null && !existingIcaoCodes.Add(icao))
                {
                    continue;
                }

                missing.Add(new AircraftModel { Id = Guid.NewGuid(), ManufacturerId = manufacturerId, Name = name, IcaoTypeDesignator = icao });
            }
        }

        if (missing.Count == 0)
        {
            return;
        }

        dbContext.AircraftModels.AddRange(missing);
        await dbContext.SaveChangesAsync();
    }
}
