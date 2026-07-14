namespace PilotsRUs.API.WebApi.Features.Auth;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    public TimeSpan Expiry { get; init; } = TimeSpan.FromDays(14);
}
