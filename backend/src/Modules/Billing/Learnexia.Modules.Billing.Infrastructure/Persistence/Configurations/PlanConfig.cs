using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Plan"/> (schema <c>billing</c>).
///
/// Key decisions:
/// - <see cref="Plan.Code"/> is a unique enum column stored as <c>int</c>.
/// - Plans are seeded once (Free + Premium) idempotently via <c>HasData</c>.
/// - Prices are NOT on this entity — they live in <c>GlobalSettings</c>.
/// </summary>
public class PlanConfig : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // P10-14: IncludedSeats — seeded per tier; Free=1, Premium=3 (OQ-C locked 2026-06-16).
        // Config-seeded here so the value is queryable without hitting GlobalSettings every request.
        builder.Property(x => x.IncludedSeats)
            .IsRequired()
            .HasDefaultValue(0);

        // Audit columns.
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // One row per PlanCode.
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_Plans_Code");

        // Seed Free and Premium plan rows idempotently.
        // CreatedAt/UpdatedAt are fixed to a deterministic date so migrations are stable.
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new Plan
            {
                Id = 1,
                Code = PlanCode.Free,
                Name = "Free",
                IsActive = true,
                IncludedSeats = 1,   // OQ-C: Free tier = 1 included seat
                CreatedAt = seedDate,
                CreatedBy = 0,
            },
            new Plan
            {
                Id = 2,
                Code = PlanCode.Premium,
                Name = "Premium",
                IsActive = true,
                IncludedSeats = 3,   // OQ-C: Premium tier = 3 included seats
                CreatedAt = seedDate,
                CreatedBy = 0,
            });
    }
}
