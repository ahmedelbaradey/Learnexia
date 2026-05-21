using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnexia.Modules.Identity.Infrastructure.Persistence;

public class IdentityModuleDbContext : IdentityDbContext<User, Role, int,
    IdentityUserClaim<int>, IdentityUserRole<int>, IdentityUserLogin<int>,
    IdentityRoleClaim<int>, IdentityUserToken<int>>
{
    public const string Schema = "identity";

    public IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options) : base(options)
    {
    }

    public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();
    public DbSet<UserAuditHistory> UserAuditHistories => Set<UserAuditHistory>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            relationship.DeleteBehavior = DeleteBehavior.Restrict;

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityModuleDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public virtual Task<int> SaveChangesAsync(int userId)
        => base.SaveChangesAsync();
}
