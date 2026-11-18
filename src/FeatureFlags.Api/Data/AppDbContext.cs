using FeatureFlags.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Flag> Flags => Set<Flag>();
    public DbSet<AppEnvironment> Environments => Set<AppEnvironment>();
    public DbSet<FlagEnvironmentValue> FlagEnvironmentValues => Set<FlagEnvironmentValue>();
    public DbSet<TargetingRule> TargetingRules => Set<TargetingRule>();
    public DbSet<FlagVersion> FlagVersions => Set<FlagVersion>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Modified)
            {
                if (entry.Entity is Models.Flag f) f.RowVersion = Guid.NewGuid().ToString("N");
                if (entry.Entity is Models.FlagEnvironmentValue fev) fev.RowVersion = Guid.NewGuid().ToString("N");
            }
        }
        return base.SaveChangesAsync(ct);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Flag>(e =>
        {
            e.HasIndex(f => f.Key).IsUnique();
            e.Property(f => f.RowVersion).IsConcurrencyToken();
            e.HasMany(f => f.EnvironmentValues).WithOne(fev => fev.Flag).HasForeignKey(fev => fev.FlagId);
            e.HasMany(f => f.Versions).WithOne(v => v.Flag).HasForeignKey(v => v.FlagId);
            e.HasMany(f => f.ChangeRequests).WithOne(cr => cr.Flag).HasForeignKey(cr => cr.FlagId);
        });

        builder.Entity<FlagEnvironmentValue>(e =>
        {
            e.HasIndex(fev => new { fev.FlagId, fev.EnvironmentId }).IsUnique();
            e.Property(fev => fev.RowVersion).IsConcurrencyToken();
            e.HasMany(fev => fev.Rules).WithOne(r => r.FlagEnvironmentValue)
                .HasForeignKey(r => r.FlagEnvironmentValueId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AppEnvironment>(e =>
        {
            e.HasIndex(env => env.Name).IsUnique();
            e.HasMany(env => env.ApiKeys).WithOne(k => k.Environment).HasForeignKey(k => k.EnvironmentId);
        });

        builder.Entity<FlagVersion>(e =>
        {
            e.HasIndex(v => new { v.FlagId, v.EnvironmentId, v.Version });
        });

        builder.Entity<ChangeRequest>(e =>
        {
            e.HasIndex(cr => cr.Status);
        });

        builder.Entity<ApiKey>(e =>
        {
            e.HasIndex(k => k.KeyHash);
        });
    }
}
