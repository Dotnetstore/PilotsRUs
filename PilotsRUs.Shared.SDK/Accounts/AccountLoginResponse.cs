namespace PilotsRUs.Shared.SDK.Accounts;

public sealed record AccountLoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAtUtc,
    string DisplayName);
