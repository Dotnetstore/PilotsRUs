namespace PilotsRUs.API.WebApi.Data;

public sealed class AircraftModel
{
    public Guid Id { get; init; }
    public required Guid ManufacturerId { get; set; } // set, not init - Edit can reassign the manufacturer
    public required string Name { get; set; }

    // ICAO Doc 8643 type designator - an official standardized registry (unlike Manufacturer.Code), so
    // seeded rows carry real best-effort codes for common variants.
    public string? IcaoTypeDesignator { get; set; }

    public Manufacturer Manufacturer { get; init; } = null!;
}
