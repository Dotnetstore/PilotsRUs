namespace PilotsRUs.API.WebApi.Data;

public sealed class Schedule
{
    public Guid Id { get; init; }
    public required Guid ScheduleTemplateId { get; set; }
    public required DateOnly FlightDate { get; set; }

    public ScheduleTemplate ScheduleTemplate { get; init; } = null!;
}
