namespace PilotsRUs.User.App.Services;

// Generic success/value-or-failure/message wrapper for API client calls, so ViewModels can consume results
// without try/catch around every call. Originally IAccountApiClient-only (as AccountApiResult<T>);
// generalized here once IScheduleApiClient needed the exact same shape - a pure data wrapper with no
// business logic in it, so sharing it carries none of the risk that keeping RefreshTokenService and
// AccountRefreshTokenService separate avoided.
public sealed record ApiResult<T>(bool Success, T? Value, string? ErrorMessage)
{
    public static ApiResult<T> Ok(T value) => new(true, value, null);
    public static ApiResult<T> Fail(string errorMessage) => new(false, default, errorMessage);
}
