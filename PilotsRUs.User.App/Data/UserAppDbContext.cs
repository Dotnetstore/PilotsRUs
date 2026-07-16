using Microsoft.EntityFrameworkCore;

namespace PilotsRUs.User.App.Data;

// FlightAssignment is the first real entity - the auth session itself is still kept in memory only
// (IAuthSessionService), not persisted locally, per the "always require login on launch" decision. See
// CLAUDE.md's "Flight Assignments" section.
public sealed class UserAppDbContext(DbContextOptions<UserAppDbContext> options) : DbContext(options)
{
    public DbSet<FlightAssignment> FlightAssignments => Set<FlightAssignment>();

    public static string GetDefaultDbPath()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PilotsRUs");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "pilotsrus.db");
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<FlightAssignment>(entity =>
        {
            entity.Property(a => a.FlightNumber).IsRequired().HasMaxLength(20);
            entity.Property(a => a.DepartureAirportIcaoCode).IsRequired().HasMaxLength(4);
            entity.Property(a => a.ArrivalAirportIcaoCode).IsRequired().HasMaxLength(4);
            entity.Property(a => a.AircraftRegistrationNumber).IsRequired().HasMaxLength(20);
        });
    }
}
