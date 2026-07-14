namespace PilotsRUs.Shared.SDK.AircraftModels;

public sealed record UpdateAircraftModelRequest(string Name, string? IcaoTypeDesignator, Guid ManufacturerId);
