namespace PilotsRUs.API.WebApi.Features.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Key { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string AccountAudience { get; init; }
    public TimeSpan Expiry { get; init; } = TimeSpan.FromMinutes(60);
}
