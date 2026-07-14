namespace PilotsRUs.Shared.SDK.Airports;

public sealed record UpdateAirportRequest(string Name, string IcaoCode, string? IataCode, string City, Guid CountryId);
