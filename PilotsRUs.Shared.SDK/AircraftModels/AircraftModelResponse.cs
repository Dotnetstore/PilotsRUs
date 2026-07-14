namespace PilotsRUs.Shared.SDK.AircraftModels;

public sealed record AircraftModelResponse(Guid Id, string Name, string? IcaoTypeDesignator, Guid ManufacturerId, string ManufacturerName);
