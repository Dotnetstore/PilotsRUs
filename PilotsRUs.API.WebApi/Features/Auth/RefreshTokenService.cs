using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PilotsRUs.API.WebApi.Data;

namespace PilotsRUs.API.WebApi.Features.Auth;

public interface IRefreshTokenService
{
    Task<(string RawToken, DateTimeOffset ExpiresAtUtc)> IssueAsync(Guid userId, Guid familyId, CancellationToken ct = default);
    Task<RefreshTokenResult> RotateAsync(string rawToken, CancellationToken ct = default);
    Task RevokeByRawTokenAsync(string rawToken, string reason, CancellationToken ct = default);
}

public enum RefreshTokenOutcome { Success, NotFound, Expired, ReuseDetected }

public sealed record RefreshTokenResult(
    RefreshTokenOutcome Outcome,
    ApplicationUser? User = null,
    string? NewRawToken = null,
    DateTimeOffset? NewExpiresAtUtc = null);

public sealed class RefreshTokenService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IOptions<RefreshTokenOptions> options) : IRefreshTokenService
{
    private readonly RefreshTokenOptions _options = options.Value;

    public async Task<(string RawToken, DateTimeOffset ExpiresAtUtc)> IssueAsync(Guid userId, Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var rawToken = GenerateRawToken();
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(_options.Expiry);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = HashToken(rawToken),
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);

        return (rawToken, expiresAtUtc);
    }

    public async Task<RefreshTokenResult> RotateAsync(string rawToken, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var tokenHash = HashToken(rawToken);
        var existing = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (existing is null)
        {
            return new RefreshTokenResult(RefreshTokenOutcome.NotFound);
        }

        if (existing.RevokedAtUtc is not null)
        {
            // A legitimate rotation always revokes the old token as part of issuing the new one, so a
            // second presentation of an already-revoked token can only mean a retry or a replayed stolen
            // token. Both are safely handled by revoking the whole lineage.
            await RevokeFamilyAsync(db, existing.FamilyId, "reuse-detected", ct);
            return new RefreshTokenResult(RefreshTokenOutcome.ReuseDetected);
        }

        if (existing.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return new RefreshTokenResult(RefreshTokenOutcome.Expired);
        }

        var newRawToken = GenerateRawToken();
        var newExpiresAtUtc = DateTimeOffset.UtcNow.Add(_options.Expiry);
        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            FamilyId = existing.FamilyId,
            TokenHash = HashToken(newRawToken),
            ExpiresAtUtc = newExpiresAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        existing.RevokedAtUtc = DateTimeOffset.UtcNow;
        existing.RevokedReason = "rotated";
        existing.ReplacedByTokenId = newToken.Id;

        db.RefreshTokens.Add(newToken);
        await db.SaveChangesAsync(ct);

        return new RefreshTokenResult(RefreshTokenOutcome.Success, existing.User, newRawToken, newExpiresAtUtc);
    }

    public async Task RevokeByRawTokenAsync(string rawToken, string reason, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var tokenHash = HashToken(rawToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        if (existing is null)
        {
            return;
        }

        await RevokeFamilyAsync(db, existing.FamilyId, reason, ct);
    }

    private static async Task RevokeFamilyAsync(ApplicationDbContext db, Guid familyId, string reason, CancellationToken ct)
    {
        // Load + mutate + SaveChanges rather than ExecuteUpdateAsync, since the latter isn't supported by
        // EF Core's InMemory provider (used in tests) - a lineage only ever has a handful of rows, so this
        // is not a meaningful cost against Postgres either.
        var activeTokens = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

        var revokedAtUtc = DateTimeOffset.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = revokedAtUtc;
            token.RevokedReason = reason;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string GenerateRawToken() => Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    internal static string HashToken(string rawToken) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
