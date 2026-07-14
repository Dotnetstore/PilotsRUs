namespace PilotsRUs.Shared.SDK.AircraftModels;

public sealed record CreateAircraftModelRequest(string Name, string? IcaoTypeDesignator, Guid ManufacturerId);
