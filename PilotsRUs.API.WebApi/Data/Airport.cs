namespace PilotsRUs.API.WebApi.Data;

public sealed class Airport
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string IcaoCode { get; set; }
    public string? IataCode { get; set; }
    public required string City { get; set; }
    public required Guid CountryId { get; set; }

    public Country Country { get; init; } = null!;
}
