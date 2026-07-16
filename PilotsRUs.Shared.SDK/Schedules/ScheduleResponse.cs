namespace PilotsRUs.Shared.SDK.Schedules;

public sealed record ScheduleResponse(
    Guid Id,
    Guid ScheduleTemplateId,
    DateOnly FlightDate,
    string FlightNumber,
    string DepartureAirportIcaoCode,
    string DepartureAirportName,
    string ArrivalAirportIcaoCode,
    string ArrivalAirportName,
    Guid AircraftId,
    string AircraftRegistrationNumber,
    int DistanceNauticalMiles,
    TimeSpan FlightTime,
    int PassengerCapacityEconomy,
    int PassengerCapacityBusiness,
    int PassengerCapacityFirst,
    int CargoCapacityKg);
