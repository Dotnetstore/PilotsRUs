namespace PilotsRUs.API.WebApi.Data;

public sealed class Aircraft
{
    public Guid Id { get; init; }
    public required string RegistrationNumber { get; set; }
    public required int PassengerCapacityEconomy { get; set; }
    public required int PassengerCapacityBusiness { get; set; }
    public required int PassengerCapacityFirst { get; set; }
    public required int CargoCapacityKg { get; set; }
    public required Guid AircraftModelId { get; set; }
    public required Guid SoftwareDeveloperId { get; set; }

    public AircraftModel AircraftModel { get; init; } = null!;
    public SoftwareDeveloper SoftwareDeveloper { get; init; } = null!;
}
