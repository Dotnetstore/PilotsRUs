namespace PilotsRUs.API.WebApi.Data;

public sealed class Manufacturer
{
    public Guid Id { get; init; }
    public required string Name { get; set; }

    // No official standardized "manufacturer code" registry exists - left null on seeded rows; admins
    // fill it in later via Edit if they want one.
    public string? Code { get; set; }
}
