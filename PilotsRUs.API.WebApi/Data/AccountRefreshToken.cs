namespace PilotsRUs.API.WebApi.Data;

public sealed class AccountRefreshToken
{
    public Guid Id { get; init; }
    public required Guid AccountId { get; init; }
    public required string TokenHash { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public Guid FamilyId { get; init; }

    public Account Account { get; init; } = null!;
}
