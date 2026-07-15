namespace PilotsRUs.Shared.SDK.Aircrafts;

public sealed record CreateAircraftRequest(
    string RegistrationNumber,
    int PassengerCapacityEconomy,
    int PassengerCapacityBusiness,
    int PassengerCapacityFirst,
    int CargoCapacityKg,
    Guid AircraftModelId,
    Guid SoftwareDeveloperId);
