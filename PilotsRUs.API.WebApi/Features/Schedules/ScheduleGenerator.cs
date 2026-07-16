using Microsoft.EntityFrameworkCore;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.Shared.SDK.ScheduleTemplates;

namespace PilotsRUs.API.WebApi.Features.Schedules;

// Generates Schedule rows from ScheduleTemplate recurrence patterns. Idempotent by construction: each
// template's watermark (MAX(Schedule.FlightDate) for that template) only ever advances in whole-week
// jumps, so a date is never processed twice across calls - see CLAUDE.md's "Schedules" section for the
// full algorithm rationale.
public static class ScheduleGenerator
{
    public static async Task<int> GenerateDueSchedulesAsync(
        IDbContextFactory<ApplicationDbContext> dbContextFactory, TimeProvider timeProvider, CancellationToken ct = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var templates = await dbContext.ScheduleTemplates.ToListAsync(ct);

        // Per-template, not global - a watermark shared across all templates would starve a newly added
        // template of any generation at all once an older template's watermark has run far ahead of today.
        var lastGeneratedDateByTemplate = await dbContext.Schedules
            .GroupBy(s => s.ScheduleTemplateId)
            .Select(g => new { ScheduleTemplateId = g.Key, LastFlightDate = g.Max(s => s.FlightDate) })
            .ToDictionaryAsync(x => x.ScheduleTemplateId, x => x.LastFlightDate, ct);

        var created = new List<Schedule>();

        foreach (var template in templates)
        {
            var lastGeneratedDate = lastGeneratedDateByTemplate.GetValueOrDefault(template.Id, today.AddDays(-1));

            // Keeps generating consecutive one-week chunks until there's at least a week of buffer ahead
            // of today - this is what makes a single call self-catch-up after downtime, while each
            // individual chunk still only ever covers one week.
            while (lastGeneratedDate < today.AddDays(6))
            {
                var windowStart = lastGeneratedDate.AddDays(1);
                var windowEnd = lastGeneratedDate.AddDays(7);

                foreach (var flightDate in ComputeFlightDates(template.StartDate, template.Frequency.ToIntervalDays(), windowStart, windowEnd))
                {
                    created.Add(new Schedule { Id = Guid.NewGuid(), ScheduleTemplateId = template.Id, FlightDate = flightDate });
                }

                lastGeneratedDate = windowEnd;
            }
        }

        if (created.Count == 0)
        {
            return 0;
        }

        dbContext.Schedules.AddRange(created);
        await dbContext.SaveChangesAsync(ct);
        return created.Count;
    }

    internal static IEnumerable<DateOnly> ComputeFlightDates(DateOnly templateStartDate, int intervalDays, DateOnly windowStart, DateOnly windowEnd)
    {
        for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
        {
            if (date < templateStartDate)
            {
                continue;
            }

            if ((date.DayNumber - templateStartDate.DayNumber) % intervalDays == 0)
            {
                yield return date;
            }
        }
    }
}
