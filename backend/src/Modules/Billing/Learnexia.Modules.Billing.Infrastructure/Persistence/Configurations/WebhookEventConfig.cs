using Learnexia.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="WebhookEvent"/> (schema <c>billing</c>).
///
/// Key decisions:
/// - Unique index on <see cref="WebhookEvent.ProviderEventId"/> is the idempotency guard:
///   the handler inserts this row FIRST (within the atomic transaction) before any state
///   mutation; a duplicate-key violation means the event was already processed → no-op.
/// - <see cref="WebhookEvent.Payload"/> stores the raw JSON for audit/replay — MUST NOT be
///   reflected in API responses (could leak internal details).
/// - <see cref="WebhookEvent.EventType"/> has an index for efficient audit queries by type.
/// </summary>
public class WebhookEventConfig : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("WebhookEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderEventId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Payload)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.ProcessedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(x => x.Succeeded)
            .IsRequired()
            .HasDefaultValue(false);

        // Audit columns.
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // THE idempotency guard — unique ProviderEventId.
        builder.HasIndex(x => x.ProviderEventId)
            .IsUnique()
            .HasDatabaseName("UX_WebhookEvents_ProviderEventId");

        // Audit queries by event type.
        builder.HasIndex(x => x.EventType)
            .HasDatabaseName("IX_WebhookEvents_EventType");

        // Failed / incomplete webhook investigation.
        builder.HasIndex(x => x.Succeeded)
            .HasDatabaseName("IX_WebhookEvents_Succeeded");
    }
}
