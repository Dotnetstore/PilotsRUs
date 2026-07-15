namespace PilotsRUs.Shared.SDK.Aircrafts;

public sealed record AircraftResponse(
    Guid Id,
    string RegistrationNumber,
    int PassengerCapacityEconomy,
    int PassengerCapacityBusiness,
    int PassengerCapacityFirst,
    int CargoCapacityKg,
    Guid AircraftModelId,
    string AircraftModelName,
    string ManufacturerName,
    Guid SoftwareDeveloperId,
    string SoftwareDeveloperName);
