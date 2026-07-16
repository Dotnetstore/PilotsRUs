using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PilotsRUs.API.WebApi.Data;
using PilotsRUs.API.WebApi.Features.Auth;

namespace PilotsRUs.API.WebApi.Features.Accounts;

// Deliberately parallel to RefreshTokenService, not a generalization of it - see CLAUDE.md's "Accounts"
// section for the reasoning (keeping Admin's already-relied-upon refresh token logic untouched).
public interface IAccountRefreshTokenService
{
    Task<(string RawToken, DateTimeOffset ExpiresAtUtc)> IssueAsync(Guid accountId, Guid familyId, CancellationToken ct = default);
    Task<AccountRefreshTokenResult> RotateAsync(string rawToken, CancellationToken ct = default);
    Task RevokeByRawTokenAsync(string rawToken, string reason, CancellationToken ct = default);
}

public enum AccountRefreshTokenOutcome { Success, NotFound, Expired, ReuseDetected }

public sealed record AccountRefreshTokenResult(
    AccountRefreshTokenOutcome Outcome,
    Account? Account = null,
    string? NewRawToken = null,
    DateTimeOffset? NewExpiresAtUtc = null);

public sealed class AccountRefreshTokenService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IOptions<RefreshTokenOptions> options) : IAccountRefreshTokenService
{
    private readonly RefreshTokenOptions _options = options.Value;

    public async Task<(string RawToken, DateTimeOffset ExpiresAtUtc)> IssueAsync(Guid accountId, Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var rawToken = GenerateRawToken();
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(_options.Expiry);

        db.AccountRefreshTokens.Add(new AccountRefreshToken
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            FamilyId = familyId,
            TokenHash = HashToken(rawToken),
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);

        return (rawToken, expiresAtUtc);
    }

    public async Task<AccountRefreshTokenResult> RotateAsync(string rawToken, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var tokenHash = HashToken(rawToken);
        var existing = await db.AccountRefreshTokens
            .Include(t => t.Account)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (existing is null)
        {
            return new AccountRefreshTokenResult(AccountRefreshTokenOutcome.NotFound);
        }

        if (existing.RevokedAtUtc is not null)
        {
            await RevokeFamilyAsync(db, existing.FamilyId, "reuse-detected", ct);
            return new AccountRefreshTokenResult(AccountRefreshTokenOutcome.ReuseDetected);
        }

        if (existing.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return new AccountRefreshTokenResult(AccountRefreshTokenOutcome.Expired);
        }

        var newRawToken = GenerateRawToken();
        var newExpiresAtUtc = DateTimeOffset.UtcNow.Add(_options.Expiry);
        var newToken = new AccountRefreshToken
        {
            Id = Guid.NewGuid(),
            AccountId = existing.AccountId,
            FamilyId = existing.FamilyId,
            TokenHash = HashToken(newRawToken),
            ExpiresAtUtc = newExpiresAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        existing.RevokedAtUtc = DateTimeOffset.UtcNow;
        existing.RevokedReason = "rotated";
        existing.ReplacedByTokenId = newToken.Id;

        db.AccountRefreshTokens.Add(newToken);
        await db.SaveChangesAsync(ct);

        return new AccountRefreshTokenResult(AccountRefreshTokenOutcome.Success, existing.Account, newRawToken, newExpiresAtUtc);
    }

    public async Task RevokeByRawTokenAsync(string rawToken, string reason, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var tokenHash = HashToken(rawToken);
        var existing = await db.AccountRefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
        if (existing is null)
        {
            return;
        }

        await RevokeFamilyAsync(db, existing.FamilyId, reason, ct);
    }

    private static Task RevokeFamilyAsync(ApplicationDbContext db, Guid familyId, string reason, CancellationToken ct) =>
        RevokeMatchingAsync(db, t => t.FamilyId == familyId && t.RevokedAtUtc == null, reason, ct);

    private static async Task RevokeMatchingAsync(ApplicationDbContext db, Expression<Func<AccountRefreshToken, bool>> predicate, string reason, CancellationToken ct)
    {
        var activeTokens = await db.AccountRefreshTokens.Where(predicate).ToListAsync(ct);

        var revokedAtUtc = DateTimeOffset.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = revokedAtUtc;
            token.RevokedReason = reason;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string GenerateRawToken() => Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string rawToken) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
