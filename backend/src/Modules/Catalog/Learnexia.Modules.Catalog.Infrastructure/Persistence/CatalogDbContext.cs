using Learnexia.Modules.Catalog.Domain.Entities;
using Learnexia.Shared.Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pgvector.EntityFrameworkCore;

namespace Learnexia.Modules.Catalog.Infrastructure.Persistence;

public class CatalogDbContext : DbContext
{
    public const string Schema = "catalog";

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<DemoEntity> DemoEntities => Set<DemoEntity>();

    // DEMO ONLY — disposable; remove before curriculum/RAG story (P1-06 AC-2 proof).
    public DbSet<EmbeddingDemo> EmbeddingDemos => Set<EmbeddingDemo>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        // DEMO ONLY — enables the pgvector extension in the catalog schema (P1-06 AC-2 proof).
        // HasPostgresExtension is idempotent (emits CREATE EXTENSION IF NOT EXISTS vector).
        // Remove when EmbeddingDemo is dropped after the proof is verified.
        modelBuilder.HasPostgresExtension("vector");
        // Configure the demo vector column explicitly: vector(3) for the 3-dim proof.
        modelBuilder.Entity<EmbeddingDemo>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Embedding).HasColumnType("vector(3)");
        });
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    // Mirrors backend AuditableDbContext.SaveChangesAsync(userId): stamps audit fields on save.
    public virtual async Task<int> SaveChangesAsync(int userId)
    {
        foreach (var entry in ChangeTracker.Entries<CreationAuditedEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.Now;
                entry.Entity.CreatedBy = userId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<AduitedEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.Now;
                entry.Entity.UpdatedBy = userId;
            }
            else if (entry.State == EntityState.Added)
            {
                entry.Entity.UpdatedAt = DateTime.Now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<FullAuditedEntity>())
        {
            if (entry.State == EntityState.Modified && entry.Entity.IsDeleted == true)
            {
                entry.Entity.DeletedAt = DateTime.Now;
                entry.Entity.DeletedBy = userId;
            }
        }

        return await base.SaveChangesAsync();
    }
}
