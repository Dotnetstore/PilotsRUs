namespace PilotsRUs.User.App.Data;

// Denormalized, not FK'd to anything - this is a different database (local SQLite) than the API's
// (Postgres), so there's no cross-database foreign key to a Schedule row. ScheduleId is kept purely for
// reference/traceability; every field the "My Flights" list needs to render is copied in directly so
// reading it back never requires an API round-trip.
public sealed class FlightAssignment
{
    public Guid Id { get; init; }
    public required Guid ScheduleId { get; init; }
    public required string FlightNumber { get; init; }
    public required string DepartureAirportIcaoCode { get; init; }
    public required string ArrivalAirportIcaoCode { get; init; }
    public required DateOnly FlightDate { get; init; }
    public required string AircraftRegistrationNumber { get; init; }
    public required int AssignedPassengersEconomy { get; init; }
    public required int AssignedPassengersBusiness { get; init; }
    public required int AssignedPassengersFirst { get; init; }
    public required int AssignedCargoKg { get; init; }
    public required DateTimeOffset AssignedAtUtc { get; init; }
}
