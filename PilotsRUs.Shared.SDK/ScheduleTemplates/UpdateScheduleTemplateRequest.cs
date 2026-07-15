namespace PilotsRUs.Shared.SDK.ScheduleTemplates;

public sealed record UpdateScheduleTemplateRequest(
    string FlightNumber,
    Guid DepartureAirportId,
    Guid ArrivalAirportId,
    Guid AircraftId,
    int DistanceNauticalMiles,
    TimeSpan FlightTime,
    ScheduleFrequency Frequency);
