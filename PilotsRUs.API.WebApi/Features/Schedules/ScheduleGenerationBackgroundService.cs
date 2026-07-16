using Microsoft.EntityFrameworkCore;
using PilotsRUs.API.WebApi.Data;

namespace PilotsRUs.API.WebApi.Features.Schedules;

// Thin timer wrapper around ScheduleGenerator - the do/while + PeriodicTimer.WaitForNextTickAsync shape
// runs generation immediately on startup, then every 7 days thereafter. All the actual logic (and its
// catch-up/idempotency behavior) lives in ScheduleGenerator so it can be unit/integration tested without
// waiting on a real timer.
public sealed class ScheduleGenerationBackgroundService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ILogger<ScheduleGenerationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(7));
        do
        {
            var created = await ScheduleGenerator.GenerateDueSchedulesAsync(dbContextFactory, timeProvider, stoppingToken);
            logger.LogInformation("Schedule generation created {Count} schedule(s)", created);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
