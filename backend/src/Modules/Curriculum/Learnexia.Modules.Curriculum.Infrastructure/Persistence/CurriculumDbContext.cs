using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Shared.Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pgvector.EntityFrameworkCore;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Curriculum module. Schema = "curriculum" (schema-per-module).
///
/// Mirrors <c>AiDbContext</c> / <c>ModerationDbContext</c>:
/// - <see cref="HasDefaultSchema"/> set to <see cref="Schema"/>.
/// - <see cref="ApplyConfigurationsFromAssembly"/> auto-discovers all <c>IEntityTypeConfiguration&lt;T&gt;</c>
///   files in Persistence/Configurations/.
/// - Audit-stamping <see cref="SaveChangesAsync(int userId)"/> override stamps timestamps + user IDs.
///
/// pgvector extension is declared in <c>ChunkEmbeddingBgeM3Config</c> via
/// <c>modelBuilder.HasPostgresExtension("vector")</c> — this emits
/// <c>CREATE EXTENSION IF NOT EXISTS vector</c> in the migration.
///
/// BL-04 / BL-05 extend this context (new DbSets); they do NOT re-create the tables created here.
/// </summary>
public class CurriculumDbContext : DbContext
{
    public const string Schema = "curriculum";

    public CurriculumDbContext(DbContextOptions<CurriculumDbContext> options) : base(options)
    {
    }

    public DbSet<CurriculumVersion> CurriculumVersions => Set<CurriculumVersion>();
    public DbSet<CurriculumChunk> CurriculumChunks => Set<CurriculumChunk>();
    public DbSet<ChunkEmbeddingBgeM3> ChunkEmbeddingsBgeM3 => Set<ChunkEmbeddingBgeM3>();

    // BL-04 BE-8: provenance tree (AddProvenanceTree migration)
    public DbSet<ContentSource> ContentSources => Set<ContentSource>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<ProvenanceMapping> ProvenanceMappings => Set<ProvenanceMapping>();

    // BL-04 BE-10: KG suggestion queue (AddKGSuggestionTable migration)
    public DbSet<KGSuggestion> KGSuggestions => Set<KGSuggestion>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        // Declare the pgvector extension — emits CREATE EXTENSION IF NOT EXISTS vector in the migration.
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CurriculumDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Stamps audit fields on tracked entities before saving.
    /// Mirrors AiDbContext.SaveChangesAsync(userId) and ModerationDbContext.SaveChangesAsync(userId).
    /// </summary>
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
