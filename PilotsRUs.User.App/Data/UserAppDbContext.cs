using Microsoft.EntityFrameworkCore;

namespace PilotsRUs.User.App.Data;

// Starts with zero entities - the auth session is kept in memory only (IAuthSessionService), not
// persisted locally, per the "always require login on launch" decision. This DbContext establishes the
// local SQLite/EF Core architecture for future User.App features that do need cached/synced data (e.g.
// browsing cached Schedules) - see CLAUDE.md's "User.App architecture" section.
public sealed class UserAppDbContext(DbContextOptions<UserAppDbContext> options) : DbContext(options)
{
    public static string GetDefaultDbPath()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PilotsRUs");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "pilotsrus.db");
    }
}
