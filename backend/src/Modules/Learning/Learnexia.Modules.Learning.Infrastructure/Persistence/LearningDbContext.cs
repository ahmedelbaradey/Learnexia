using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Learning module. Schema = "learning" (schema-per-module).
///
/// Mirrors CatalogDbContext shape:
/// - <see cref="HasDefaultSchema"/> set to <see cref="LearningSchema.Name"/>.
/// - <see cref="ApplyConfigurationsFromAssembly"/> auto-discovers all <c>IEntityTypeConfiguration&lt;T&gt;</c>
///   files in Configurations/.
/// - Audit-stamping <see cref="SaveChangesAsync(int)"/> override iterates CreationAuditedEntity /
///   AduitedEntity / FullAuditedEntity entries and stamps timestamps + user IDs before saving.
///
/// No pgvector / EmbeddingDemo bits — those are a Catalog-only demo artifact.
/// </summary>
public class LearningDbContext : DbContext
{
    public const string Schema = LearningSchema.Name;

    public LearningDbContext(DbContextOptions<LearningDbContext> options) : base(options)
    {
    }

    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Concept> Concepts => Set<Concept>();
    public DbSet<Skill> Skills => Set<Skill>();

    // Skill dependency graph (P2-11)
    public DbSet<KnowledgeNode> KnowledgeNodes => Set<KnowledgeNode>();
    public DbSet<KnowledgeEdge> KnowledgeEdges => Set<KnowledgeEdge>();

    // Quiz entities (P2-06 folded into Learning from Assessment)
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearningDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Stamps audit fields on tracked entities before saving. Mirrors CatalogDbContext.SaveChangesAsync(userId).
    /// Called by the per-module <c>UnitOfWorkBehavior</c> after the command handler stages its changes.
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
