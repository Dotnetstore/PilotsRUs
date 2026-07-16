namespace PilotsRUs.User.App.Services;

// In-memory only - nothing here is persisted to the local SQLite database, per the "always require login
// on launch" decision (see CLAUDE.md's "Accounts"/"User.App architecture" sections). Singleton: exactly
// one session ever exists in a desktop process.
public interface IAuthSessionService
{
    bool IsAuthenticated { get; }
    string? AccessToken { get; }
    string? RefreshToken { get; }
    string? DisplayName { get; }

    void SetSession(string accessToken, DateTimeOffset accessTokenExpiresAtUtc, string refreshToken, DateTimeOffset refreshTokenExpiresAtUtc, string displayName);
    void UpdateTokens(string accessToken, DateTimeOffset accessTokenExpiresAtUtc, string refreshToken, DateTimeOffset refreshTokenExpiresAtUtc);
    void Clear();
}

public sealed class AuthSessionService : IAuthSessionService
{
    public bool IsAuthenticated => AccessToken is not null;
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? DisplayName { get; private set; }
    private DateTimeOffset? AccessTokenExpiresAtUtc { get; set; }
    private DateTimeOffset? RefreshTokenExpiresAtUtc { get; set; }

    public void SetSession(string accessToken, DateTimeOffset accessTokenExpiresAtUtc, string refreshToken, DateTimeOffset refreshTokenExpiresAtUtc, string displayName)
    {
        AccessToken = accessToken;
        AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
        DisplayName = displayName;
    }

    public void UpdateTokens(string accessToken, DateTimeOffset accessTokenExpiresAtUtc, string refreshToken, DateTimeOffset refreshTokenExpiresAtUtc)
    {
        AccessToken = accessToken;
        AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
    }

    public void Clear()
    {
        AccessToken = null;
        AccessTokenExpiresAtUtc = null;
        RefreshToken = null;
        RefreshTokenExpiresAtUtc = null;
        DisplayName = null;
    }
}
