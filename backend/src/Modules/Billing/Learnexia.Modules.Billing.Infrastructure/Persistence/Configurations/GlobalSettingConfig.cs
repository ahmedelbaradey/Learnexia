using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="GlobalSetting"/>.
/// Table: <c>platform.GlobalSettings</c> — note the <c>platform</c> schema override
/// (the BillingDbContext default is <c>billing</c>; this table deliberately goes to <c>platform</c>).
/// </summary>
public sealed class GlobalSettingConfig : IEntityTypeConfiguration<GlobalSetting>
{
    public void Configure(EntityTypeBuilder<GlobalSetting> builder)
    {
        // Schema override — platform, not billing.
        builder.ToTable("GlobalSettings", "platform");

        // Natural PK — the key string itself (max 200, invariant).
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key)
               .HasMaxLength(200)
               .IsRequired();

        builder.Property(s => s.Value)
               .HasMaxLength(2000)
               .IsRequired();

        builder.Property(s => s.Type)
               .HasConversion<int>()
               .IsRequired();

        builder.Property(s => s.UpdatedBy)
               .HasMaxLength(200)
               .IsRequired();

        builder.Property(s => s.UpdatedAt)
               .HasColumnType("timestamptz")
               .IsRequired();

        // Index to support ordering / audit queries by update time.
        builder.HasIndex(s => s.UpdatedAt)
               .HasDatabaseName("IX_GlobalSettings_UpdatedAt");
    }
}
