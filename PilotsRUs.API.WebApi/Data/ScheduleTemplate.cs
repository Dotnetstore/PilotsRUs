using PilotsRUs.Shared.SDK.ScheduleTemplates;

namespace PilotsRUs.API.WebApi.Data;

public sealed class ScheduleTemplate
{
    public Guid Id { get; init; }
    public required string FlightNumber { get; set; }
    public required Guid DepartureAirportId { get; set; }
    public required Guid ArrivalAirportId { get; set; }
    public required Guid AircraftId { get; set; }
    public required int DistanceNauticalMiles { get; set; }
    public required TimeSpan FlightTime { get; set; }
    public required ScheduleFrequency Frequency { get; set; }

    public Airport DepartureAirport { get; init; } = null!;
    public Airport ArrivalAirport { get; init; } = null!;
    public Aircraft Aircraft { get; init; } = null!;
}
