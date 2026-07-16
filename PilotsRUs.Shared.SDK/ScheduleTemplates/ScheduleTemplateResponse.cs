namespace PilotsRUs.Shared.SDK.ScheduleTemplates;

public sealed record ScheduleTemplateResponse(
    Guid Id,
    string FlightNumber,
    Guid DepartureAirportId,
    string DepartureAirportIcaoCode,
    string DepartureAirportName,
    Guid ArrivalAirportId,
    string ArrivalAirportIcaoCode,
    string ArrivalAirportName,
    Guid AircraftId,
    string AircraftRegistrationNumber,
    int DistanceNauticalMiles,
    TimeSpan FlightTime,
    ScheduleFrequency Frequency,
    DateOnly StartDate);
