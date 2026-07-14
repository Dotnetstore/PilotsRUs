namespace PilotsRUs.Shared.SDK.Airports;

public sealed record AirportResponse(Guid Id, string Name, string IcaoCode, string? IataCode, string City, Guid CountryId, string CountryName);
