using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GesCo.Api.Models;

namespace GesCo.Api.Data;

public class ApplicationDbContext : IdentityDbContext<User>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets para las entidades personalizadas
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<LoginAttempt> LoginAttempts { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configuración de User
        builder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.OrganizationId);
            
            entity.HasOne(u => u.Organization)
                  .WithMany(o => o.Users)
                  .HasForeignKey(u => u.OrganizationId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(u => u.Sessions)
                  .WithOne(s => s.User)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(u => u.LoginAttempts)
                  .WithOne(la => la.User)
                  .HasForeignKey(la => la.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de Organization
        builder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Name);

            entity.HasOne(o => o.Subscription)
                  .WithOne(s => s.Organization)
                  .HasForeignKey<Subscription>(s => s.OrganizationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de UserSession
        builder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionToken).IsUnique();
            entity.HasIndex(e => e.RefreshToken).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);

            entity.HasOne(s => s.User)
                  .WithMany(u => u.Sessions)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de Subscription
        builder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrganizationId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.EndDate);

            entity.Property(e => e.MonthlyPrice)
                  .HasPrecision(10, 2);

            entity.HasOne(s => s.Organization)
                  .WithOne(o => o.Subscription)
                  .HasForeignKey<Subscription>(s => s.OrganizationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de LoginAttempt
        builder.Entity<LoginAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AttemptedEmail);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.AttemptedAt);
            entity.HasIndex(e => e.IpAddress);

            entity.HasOne(la => la.User)
                  .WithMany(u => u.LoginAttempts)
                  .HasForeignKey(la => la.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Configurar nombres de tablas personalizados si es necesario
        builder.Entity<User>().ToTable("AspNetUsers");
        builder.Entity<Organization>().ToTable("Organizations");
        builder.Entity<UserSession>().ToTable("UserSessions");
        builder.Entity<Subscription>().ToTable("Subscriptions");
        builder.Entity<LoginAttempt>().ToTable("LoginAttempts");
    }
}