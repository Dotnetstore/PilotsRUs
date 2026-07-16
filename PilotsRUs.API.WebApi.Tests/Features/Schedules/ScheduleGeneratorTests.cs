using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.API.WebApi.Features.Schedules;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.ScheduleTemplates;

namespace PilotsRUs.API.WebApi.Tests.Features.Schedules;

public sealed class ScheduleGeneratorTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(ScheduleFrequency.Daily, "2026-01-01", "2026-01-01", "2026-01-07", 7)]
    [InlineData(ScheduleFrequency.EveryThirdDay, "2026-01-01", "2026-01-01", "2026-01-07", 3)]
    [InlineData(ScheduleFrequency.Weekly, "2026-01-01", "2026-01-01", "2026-01-07", 1)]
    public void ComputeFlightDates_ReturnsExpectedCount(ScheduleFrequency frequency, string startDate, string windowStart, string windowEnd, int expectedCount)
    {
        var dates = ScheduleGenerator.ComputeFlightDates(
            DateOnly.Parse(startDate), frequency.ToIntervalDays(), DateOnly.Parse(windowStart), DateOnly.Parse(windowEnd)).ToList();

        Assert.Equal(expectedCount, dates.Count);
    }

    [Fact]
    public void ComputeFlightDates_ExcludesDatesBeforeTemplateStartDate()
    {
        var dates = ScheduleGenerator.ComputeFlightDates(
            new DateOnly(2026, 1, 4), 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7)).ToList();

        Assert.All(dates, d => Assert.True(d >= new DateOnly(2026, 1, 4)));
        Assert.Equal(4, dates.Count); // Jan 4, 5, 6, 7
    }

    [Fact]
    public void ComputeFlightDates_WhenStartDateAfterWindow_ReturnsNothing()
    {
        var dates = ScheduleGenerator.ComputeFlightDates(
            new DateOnly(2026, 2, 1), 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7)).ToList();

        Assert.Empty(dates);
    }

    [Fact]
    public void ComputeFlightDates_EveryThirdDay_ReturnsCorrectSpecificDates()
    {
        var dates = ScheduleGenerator.ComputeFlightDates(
            new DateOnly(2026, 1, 1), 3, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7)).ToList();

        Assert.Equal([new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 7)], dates);
    }

    [Fact]
    public async Task GenerateDueSchedulesAsync_WithEmptySchedules_GeneratesOneWeekStartingToday()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedulegen-empty@pilotsrus.test", "P@ssw0rd123!");
        var template = await ScheduleTestData.CreateDailyTemplateAsync(client, "GenEmpty", new DateOnly(2026, 1, 1), "ZZSA", "XB", "ZZSB", "XC");
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        // The returned count reflects every ScheduleTemplate in the (shared, per-test-class) database, not
        // just this test's own template, so assertions below are scoped to this template's own rows rather
        // than the raw return value.
        await ScheduleGenerator.GenerateDueSchedulesAsync(GetDbContextFactory(), timeProvider);

        var schedules = await GetSchedulesForTemplateAsync(template.Id);
        Assert.Equal(7, schedules.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), schedules.Min());
        Assert.Equal(new DateOnly(2026, 1, 7), schedules.Max());
    }

    [Fact]
    public async Task GenerateDueSchedulesAsync_CalledTwiceWithSameNow_IsIdempotent()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedulegen-idempotent@pilotsrus.test", "P@ssw0rd123!");
        var template = await ScheduleTestData.CreateDailyTemplateAsync(client, "GenIdempotent", new DateOnly(2026, 1, 1), "ZZSC", "XD", "ZZSD", "XE");
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        // See the comment in GenerateDueSchedulesAsync_WithEmptySchedules... - the raw return value covers
        // every template in the shared test database, so idempotency is verified via this template's own
        // row count staying at 7 (not doubling to 14) rather than asserting the second call's return value.
        await ScheduleGenerator.GenerateDueSchedulesAsync(GetDbContextFactory(), timeProvider);
        await ScheduleGenerator.GenerateDueSchedulesAsync(GetDbContextFactory(), timeProvider);

        var schedules = await GetSchedulesForTemplateAsync(template.Id);
        Assert.Equal(7, schedules.Count);
    }

    [Fact]
    public async Task GenerateDueSchedulesAsync_WhenBehindByThreeWeeks_CatchesUpInOneCall()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedulegen-catchup@pilotsrus.test", "P@ssw0rd123!");
        var template = await ScheduleTestData.CreateDailyTemplateAsync(client, "GenCatchup", new DateOnly(2026, 1, 1), "ZZSE", "XF", "ZZSF", "XG");
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        await ScheduleGenerator.GenerateDueSchedulesAsync(GetDbContextFactory(), timeProvider);

        // Simulate 3 weeks passing without the service running.
        timeProvider.Advance(TimeSpan.FromDays(21));
        await ScheduleGenerator.GenerateDueSchedulesAsync(GetDbContextFactory(), timeProvider);

        var schedules = await GetSchedulesForTemplateAsync(template.Id);
        Assert.Equal(28, schedules.Count); // 1 initial week + 3 catch-up weeks
        Assert.Equal(new DateOnly(2026, 1, 28), schedules.Max());
    }

    private IDbContextFactory<ApplicationDbContext> GetDbContextFactory() =>
        factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

    private async Task<List<DateOnly>> GetSchedulesForTemplateAsync(Guid scheduleTemplateId)
    {
        await using var dbContext = await GetDbContextFactory().CreateDbContextAsync();
        return await dbContext.Schedules.Where(s => s.ScheduleTemplateId == scheduleTemplateId).Select(s => s.FlightDate).ToListAsync();
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
