namespace PilotsRUs.Shared.SDK.Auth;

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    IReadOnlyList<string> Roles);
