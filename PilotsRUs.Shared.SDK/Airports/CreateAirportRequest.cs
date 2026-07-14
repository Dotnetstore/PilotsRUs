namespace PilotsRUs.Shared.SDK.Airports;

public sealed record CreateAirportRequest(string Name, string IcaoCode, string? IataCode, string City, Guid CountryId);
