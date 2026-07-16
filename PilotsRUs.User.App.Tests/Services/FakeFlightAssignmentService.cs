using PilotsRUs.Shared.SDK.Schedules;
using PilotsRUs.User.App.Data;
using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.Tests.Services;

internal sealed class FakeFlightAssignmentService : IFlightAssignmentService
{
    public FlightAssignment? AssignResult { get; set; }
    public IReadOnlyList<FlightAssignment> Assignments { get; set; } = [];
    public ScheduleResponse? LastAssignedSchedule { get; private set; }

    public Task<FlightAssignment> AssignAsync(ScheduleResponse schedule, CancellationToken ct = default)
    {
        LastAssignedSchedule = schedule;
        return Task.FromResult(AssignResult!);
    }

    public Task<IReadOnlyList<FlightAssignment>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult(Assignments);
}
