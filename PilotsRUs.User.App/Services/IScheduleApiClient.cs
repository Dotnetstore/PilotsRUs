using System.Net.Http.Json;
using System.Text;
using PilotsRUs.Shared.SDK.Schedules;

namespace PilotsRUs.User.App.Services;

// All fields nullable - every filter is optional, searching with none returns every generated Schedule.
public sealed record FlightSearchCriteria(
    string? DepartureIcao = null,
    string? ArrivalIcao = null,
    int? MinDistanceNauticalMiles = null,
    int? MaxDistanceNauticalMiles = null,
    int? MinFlightTimeMinutes = null,
    int? MaxFlightTimeMinutes = null);

// Wraps GET /schedules, following the same result-wrapper convention as IAccountApiClient. Builds the query
// string by hand rather than via Microsoft.AspNetCore.WebUtilities.QueryHelpers - that package belongs to
// the ASP.NET Core-web SDK, which this plain desktop-app project doesn't reference, and pulling it in for
// one helper isn't worth it.
public interface IScheduleApiClient
{
    Task<ApiResult<IReadOnlyList<ScheduleResponse>>> SearchAsync(FlightSearchCriteria criteria, CancellationToken ct = default);
}

public sealed class ScheduleApiClient(IHttpClientFactory httpClientFactory) : IScheduleApiClient
{
    public async Task<ApiResult<IReadOnlyList<ScheduleResponse>>> SearchAsync(FlightSearchCriteria criteria, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync(BuildRequestUri(criteria), ct);

        if (response.IsSuccessStatusCode)
        {
            var schedules = await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>(cancellationToken: ct);
            return ApiResult<IReadOnlyList<ScheduleResponse>>.Ok(schedules ?? []);
        }

        return ApiResult<IReadOnlyList<ScheduleResponse>>.Fail("Failed to search flights.");
    }

    private static string BuildRequestUri(FlightSearchCriteria criteria)
    {
        var queryParameters = new List<string>();

        AppendIfPresent(queryParameters, "departureIcao", criteria.DepartureIcao);
        AppendIfPresent(queryParameters, "arrivalIcao", criteria.ArrivalIcao);
        AppendIfPresent(queryParameters, "minDistanceNauticalMiles", criteria.MinDistanceNauticalMiles);
        AppendIfPresent(queryParameters, "maxDistanceNauticalMiles", criteria.MaxDistanceNauticalMiles);
        AppendIfPresent(queryParameters, "minFlightTimeMinutes", criteria.MinFlightTimeMinutes);
        AppendIfPresent(queryParameters, "maxFlightTimeMinutes", criteria.MaxFlightTimeMinutes);

        if (queryParameters.Count == 0)
        {
            return "/schedules";
        }

        var builder = new StringBuilder("/schedules?");
        builder.Append(string.Join('&', queryParameters));
        return builder.ToString();
    }

    private static void AppendIfPresent(List<string> queryParameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            queryParameters.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }

    private static void AppendIfPresent(List<string> queryParameters, string name, int? value)
    {
        if (value.HasValue)
        {
            queryParameters.Add($"{name}={value.Value}");
        }
    }
}
