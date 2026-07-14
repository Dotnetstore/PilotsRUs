using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PilotsRUs.API.WebApi.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<AircraftModel> AircraftModels => Set<AircraftModel>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.FamilyId);
            entity.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Manufacturer>(entity =>
        {
            entity.Property(m => m.Name).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Code).HasMaxLength(20);
            entity.HasIndex(m => m.Name).IsUnique();
        });

        builder.Entity<AircraftModel>(entity =>
        {
            entity.Property(a => a.Name).IsRequired().HasMaxLength(200);
            entity.Property(a => a.IcaoTypeDesignator).HasMaxLength(10);
            entity.HasIndex(a => new { a.ManufacturerId, a.Name }).IsUnique();
            entity.HasIndex(a => a.IcaoTypeDesignator).IsUnique().HasFilter("\"IcaoTypeDesignator\" IS NOT NULL");
            entity.HasOne(a => a.Manufacturer).WithMany().HasForeignKey(a => a.ManufacturerId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
