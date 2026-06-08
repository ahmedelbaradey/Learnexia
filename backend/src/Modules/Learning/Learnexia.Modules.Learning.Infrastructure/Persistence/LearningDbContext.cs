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

    // P7-02: Lesson content blocks
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearningDbContext).Assembly);

        // P7-01/P7-02: Global soft-delete filters — rows with IsDeleted == true are invisible to all
        // standard EF queries. Uses != true (not == false) because the column is nullable bool?.
        // Admin reads that need to see soft-deleted rows must use .IgnoreQueryFilters().
        // IsActive is intentionally NOT in the global filter — admins must see inactive items
        // to reactivate them; student-facing reads apply IsActive == true per-query.
        modelBuilder.Entity<Subject>().HasQueryFilter(s => s.IsDeleted != true);
        modelBuilder.Entity<Unit>().HasQueryFilter(u => u.IsDeleted != true);
        modelBuilder.Entity<Lesson>().HasQueryFilter(l => l.IsDeleted != true);
        modelBuilder.Entity<ContentBlock>().HasQueryFilter(cb => cb.IsDeleted != true);

        // P7-03: Soft-delete filters for Skill and the graph entities.
        //
        // Skill: required so that KnowledgeNode.SkillId (SetNull FK) + Lesson.SkillId (nullable FK)
        // don't reference a ghost row that happens to be soft-deleted.
        modelBuilder.Entity<Skill>().HasQueryFilter(sk => sk.IsDeleted != true);

        // KnowledgeNode / KnowledgeEdge: both derive from AggregateRoot → FullAuditedEntity and carry
        // IsDeleted. Adding the filter here means soft-deleted nodes/edges are invisible to standard
        // graph queries without extra .IgnoreQueryFilters().
        //
        // Required-relationship FK warning mitigation: KnowledgeNode has required FKs to Subject
        // (which now has a soft-delete filter) and Grade (no filter). KnowledgeEdge has required FKs
        // to KnowledgeNode (which now has its own filter). Giving every entity in the chain its own
        // matching IsDeleted filter satisfies EF Core's required-relationship-filter consistency
        // requirement and avoids the model-validation warning:
        //   "Entity type 'X' has a global query filter and has a required relationship with 'Y'
        //    that also has a global query filter."
        // With all three entities filtered, EF's own consistency check is satisfied.
        modelBuilder.Entity<KnowledgeNode>().HasQueryFilter(kn => kn.IsDeleted != true);
        modelBuilder.Entity<KnowledgeEdge>().HasQueryFilter(ke => ke.IsDeleted != true);

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
