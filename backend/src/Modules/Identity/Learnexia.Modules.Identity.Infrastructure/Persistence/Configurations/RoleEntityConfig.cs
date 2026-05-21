using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Identity.Infrastructure.Persistence.Configurations;

public class RoleEntityConfig : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // User⇄Role skip navigation over the existing Identity join (IdentityUserRole<int>).
        // Use .WithMany() with no inverse collection so EF reuses the base join's (UserId, RoleId)
        // and does not create shadow FKs (RoleId1/UserId1).
        builder
            .HasMany(r => r.Users)
            .WithMany(u => u.Roles)
            .UsingEntity<IdentityUserRole<int>>(
                userRole => userRole
                    .HasOne<User>()
                    .WithMany()
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade),
                userRole => userRole
                    .HasOne<Role>()
                    .WithMany()
                    .HasForeignKey(ur => ur.RoleId)
                    .OnDelete(DeleteBehavior.Cascade),
                userRole =>
                {
                    userRole.HasKey(ur => new { ur.UserId, ur.RoleId });
                    userRole.ToTable("AspNetUserRoles");
                });

        builder.Property(r => r.Name)
            .IsRequired().HasMaxLength(256)
            .HasComment("Role name");

        builder.Property(r => r.NormalizedName)
            .IsRequired().HasMaxLength(256)
            .HasComment("Normalized role name for case-insensitive lookups");

        builder.Property(r => r.ConcurrencyStamp)
            .IsConcurrencyToken()
            .HasComment("Concurrency token for optimistic concurrency control");

        builder.HasIndex(r => r.NormalizedName)
            .IsUnique()
            .HasDatabaseName("RoleNameIndex")
            .HasFilter("\"NormalizedName\" IS NOT NULL");

        builder.ToTable("AspNetRoles");
    }
}
