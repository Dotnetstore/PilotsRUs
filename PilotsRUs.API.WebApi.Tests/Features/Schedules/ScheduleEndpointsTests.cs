using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.API.WebApi.Tests.Infrastructure;
using PilotsRUs.Shared.SDK.Accounts;
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

    [Fact]
    public async Task List_FilterByDepartureIcao_ReturnsOnlyMatchingSchedules()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedules-dep-filter@pilotsrus.test", "P@ssw0rd123!");
        var matching = await ScheduleTestData.CreateDailyTemplateAsync(client, "FilterDepA", new DateOnly(2026, 1, 1), "ZZFA", "XL", "ZZFB", "XM");
        var other = await ScheduleTestData.CreateDailyTemplateAsync(client, "FilterDepB", new DateOnly(2026, 1, 1), "ZZFC", "XN", "ZZFD", "XO");
        var matchingId = await AddScheduleAsync(matching.Id, new DateOnly(2026, 1, 1));
        var otherId = await AddScheduleAsync(other.Id, new DateOnly(2026, 1, 1));

        var response = await client.GetAsync("/schedules?departureIcao=zzfa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedules = await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>();
        Assert.Contains(schedules!, s => s.Id == matchingId);
        Assert.DoesNotContain(schedules!, s => s.Id == otherId);
    }

    [Fact]
    public async Task List_FilterByArrivalIcao_ReturnsOnlyMatchingSchedules()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedules-arr-filter@pilotsrus.test", "P@ssw0rd123!");
        var matching = await ScheduleTestData.CreateDailyTemplateAsync(client, "FilterArrA", new DateOnly(2026, 1, 1), "ZZFE", "XP", "ZZFF", "XQ");
        var other = await ScheduleTestData.CreateDailyTemplateAsync(client, "FilterArrB", new DateOnly(2026, 1, 1), "ZZFG", "XR", "ZZFH", "XS");
        var matchingId = await AddScheduleAsync(matching.Id, new DateOnly(2026, 1, 1));
        var otherId = await AddScheduleAsync(other.Id, new DateOnly(2026, 1, 1));

        var response = await client.GetAsync("/schedules?arrivalIcao=zzff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedules = await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>();
        Assert.Contains(schedules!, s => s.Id == matchingId);
        Assert.DoesNotContain(schedules!, s => s.Id == otherId);
    }

    [Fact]
    public async Task List_FilterByDistanceRange_ReturnsOnlyMatchingSchedules()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedules-dist-filter@pilotsrus.test", "P@ssw0rd123!");
        var shortHaul = await ScheduleTestData.CreateDailyTemplateAsync(client, "FilterDistA", new DateOnly(2026, 1, 1), "ZZFI", "XT", "ZZFJ", "XU", distanceNauticalMiles: 200);
        var longHaul = await ScheduleTestData.CreateDailyTemplateAsync(client, "FilterDistB", new DateOnly(2026, 1, 1), "ZZFK", "XV", "ZZFL", "XW", distanceNauticalMiles: 3000);
        var shortHaulId = await AddScheduleAsync(shortHaul.Id, new DateOnly(2026, 1, 1));
        var longHaulId = await AddScheduleAsync(longHaul.Id, new DateOnly(2026, 1, 1));

        var response = await client.GetAsync("/schedules?minDistanceNauticalMiles=1000&maxDistanceNauticalMiles=5000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedules = await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>();
        Assert.Contains(schedules!, s => s.Id == longHaulId);
        Assert.DoesNotContain(schedules!, s => s.Id == shortHaulId);
    }

    [Fact]
    public async Task List_FilterByFlightTimeRange_ReturnsOnlyMatchingSchedules()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedules-time-filter@pilotsrus.test", "P@ssw0rd123!");
        var quick = await ScheduleTestData.CreateDailyTemplateAsync(client, "FilterTimeA", new DateOnly(2026, 1, 1), "ZZFM", "XY", "ZZFN", "XZ", flightTime: TimeSpan.FromMinutes(45));
        var long_ = await ScheduleTestData.CreateDailyTemplateAsync(client, "FilterTimeB", new DateOnly(2026, 1, 1), "ZZFO", "QM", "ZZFP", "QN", flightTime: TimeSpan.FromHours(8));
        var quickId = await AddScheduleAsync(quick.Id, new DateOnly(2026, 1, 1));
        var longId = await AddScheduleAsync(long_.Id, new DateOnly(2026, 1, 1));

        var response = await client.GetAsync("/schedules?minFlightTimeMinutes=300&maxFlightTimeMinutes=600");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedules = await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>();
        Assert.Contains(schedules!, s => s.Id == longId);
        Assert.DoesNotContain(schedules!, s => s.Id == quickId);
    }

    [Fact]
    public async Task List_WithCombinedFilters_ReturnsOnlyFullyMatchingSchedules()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync("schedules-combined-filter@pilotsrus.test", "P@ssw0rd123!");
        var matching = await ScheduleTestData.CreateDailyTemplateAsync(client, "FilterCombA", new DateOnly(2026, 1, 1), "ZZFQ", "QO", "ZZFR", "QP", distanceNauticalMiles: 1500);
        var wrongDeparture = await ScheduleTestData.CreateDailyTemplateAsync(client, "FilterCombB", new DateOnly(2026, 1, 1), "ZZFS", "QQ", "ZZFT", "QR", distanceNauticalMiles: 1500);
        var matchingId = await AddScheduleAsync(matching.Id, new DateOnly(2026, 1, 1));
        var wrongDepartureId = await AddScheduleAsync(wrongDeparture.Id, new DateOnly(2026, 1, 1));

        var response = await client.GetAsync("/schedules?departureIcao=ZZFQ&arrivalIcao=ZZFR&minDistanceNauticalMiles=1000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedules = await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>();
        Assert.Contains(schedules!, s => s.Id == matchingId);
        Assert.DoesNotContain(schedules!, s => s.Id == wrongDepartureId);
    }

    // Positive-direction complement to AccountEndpointsTests.AccountToken_CannotAccessAdminScopedEndpoint -
    // proves the "AccountOrAdmin" policy actually accepts Account-audience tokens, since GET /schedules is
    // the first endpoint User.App's flight search needs to call with one.
    [Fact]
    public async Task AccountToken_CanAccessSchedules()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/account/register", new RegisterAccountRequest("pilot-schedules@pilotsrus.test", "P@ssw0rd123!", "Iceman"));
        var loginResponse = await client.PostAsJsonAsync("/account/login", new AccountLoginRequest("pilot-schedules@pilotsrus.test", "P@ssw0rd123!"));
        var login = await loginResponse.Content.ReadFromJsonAsync<AccountLoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var response = await client.GetAsync("/schedules");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
