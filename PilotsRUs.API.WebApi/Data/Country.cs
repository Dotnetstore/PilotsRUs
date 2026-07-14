namespace PilotsRUs.API.WebApi.Data;

public sealed class Country
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string IsoAlpha2Code { get; set; }
    public required string IsoAlpha3Code { get; set; }
}
