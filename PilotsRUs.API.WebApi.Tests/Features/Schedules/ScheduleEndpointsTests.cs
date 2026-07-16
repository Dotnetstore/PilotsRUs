using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.Schedules;

namespace PilotsRUs.API.WebApi.Tests.Features.Schedules;

public sealed class ScheduleEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task List_ReturnsCreatedSchedules()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedules-list@pilotsrus.test", "P@ssw0rd123!");
        var template = await ScheduleTestData.CreateDailyTemplateAsync(client, "SchedList", new DateOnly(2026, 1, 1), "ZZSG", "XH", "ZZSH", "XI");
        var scheduleId = await AddScheduleAsync(template.Id, new DateOnly(2026, 1, 1));

        var response = await client.GetAsync("/schedules");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedules = await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>();
        Assert.NotNull(schedules);
        Assert.Contains(schedules!, s => s.Id == scheduleId && s.DepartureAirportIcaoCode == "ZZSG" && s.ArrivalAirportIcaoCode == "ZZSH");
    }

    [Fact]
    public async Task GetById_ForExistingSchedule_ReturnsSchedule()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedules-get@pilotsrus.test", "P@ssw0rd123!");
        var template = await ScheduleTestData.CreateDailyTemplateAsync(client, "SchedGet", new DateOnly(2026, 1, 1), "ZZSI", "XJ", "ZZSJ", "XK");
        var scheduleId = await AddScheduleAsync(template.Id, new DateOnly(2026, 1, 2));

        var response = await client.GetAsync($"/schedules/{scheduleId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.Equal(new DateOnly(2026, 1, 2), schedule!.FlightDate);
        Assert.Equal(template.FlightNumber, schedule.FlightNumber);
        Assert.Equal(template.Id, schedule.ScheduleTemplateId);
    }

    [Fact]
    public async Task GetById_ForUnknownSchedule_ReturnsNotFound()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedules-get-404@pilotsrus.test", "P@ssw0rd123!");

        var response = await client.GetAsync($"/schedules/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/schedules");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Schedule rows are system-generated (via ScheduleGenerator), not created through an endpoint - insert
    // directly via the DbContext, same as ScheduleTemplateEndpointsTests.Delete_WithExistingSchedule_ReturnsConflict.
    private async Task<Guid> AddScheduleAsync(Guid scheduleTemplateId, DateOnly flightDate)
    {
        var scheduleId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Schedules.Add(new Schedule { Id = scheduleId, ScheduleTemplateId = scheduleTemplateId, FlightDate = flightDate });
        await dbContext.SaveChangesAsync();
        return scheduleId;
    }
}
