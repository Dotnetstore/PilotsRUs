using PilotsRUs.Shared.SDK.Schedules;
using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.Tests.Services;

internal sealed class FakeScheduleApiClient : IScheduleApiClient
{
    public ApiResult<IReadOnlyList<ScheduleResponse>>? SearchResult { get; set; }
    public FlightSearchCriteria? LastCriteria { get; private set; }

    public Task<ApiResult<IReadOnlyList<ScheduleResponse>>> SearchAsync(FlightSearchCriteria criteria, CancellationToken ct = default)
    {
        LastCriteria = criteria;
        return Task.FromResult(SearchResult!);
    }
}
