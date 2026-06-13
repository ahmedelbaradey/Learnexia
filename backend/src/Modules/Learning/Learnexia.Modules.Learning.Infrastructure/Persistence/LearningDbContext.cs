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

    // P7-05: Publish-time version snapshots
    public DbSet<ContentVersion> ContentVersions => Set<ContentVersion>();

    // P3-09: Per-(student, skill) persisted mastery (Student Modeling Engine)
    public DbSet<StudentSkillMastery> StudentSkillMasteries => Set<StudentSkillMastery>();

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

        // P7-04: Soft-delete filters for QuizQuestion and StudentAnswer.
        //
        // QuizQuestion: IsActive is intentionally NOT in the global filter — admins must see inactive
        // questions to re-activate them; student-facing quiz reads apply IsActive == true per-query.
        // Adding this filter hides soft-deleted questions from the student quiz/attempt path
        // (QuizzesController Start/Submit/Complete/Abandon) — that is the desired behaviour.
        //
        // Student-read regression point for api-tester: the existing Start/Submit attempt path reads
        // QuizQuestion rows; after this filter is applied, soft-deleted questions will be invisible to
        // those reads. Confirm this is desired and that no active question is accidentally soft-deleted.
        modelBuilder.Entity<QuizQuestion>().HasQueryFilter(qq => qq.IsDeleted != true);

        // StudentAnswer carries a required FK (QuestionId, DeleteBehavior.Restrict) to QuizQuestion.
        // EF Core emits a model-validation warning when a dependent entity has a required FK to a
        // principal entity that has a global query filter, unless the dependent also has its own filter.
        // Adding the matching IsDeleted filter here satisfies EF's consistency check — the same pattern
        // applied to KnowledgeNode/KnowledgeEdge above.
        // StudentAnswer.AttemptId FK to Attempt (no filter) does NOT trigger the warning because
        // Attempt has no global query filter.
        modelBuilder.Entity<StudentAnswer>().HasQueryFilter(sa => sa.IsDeleted != true);

        // P7-05: Global soft-delete filter for ContentVersion.
        //
        // ContentVersion derives from FullAuditedEntity (not AggregateRoot) and carries IsDeleted.
        // Soft-deleted version rows are invisible to all standard EF queries; admin reads that need
        // to see soft-deleted rows must use .IgnoreQueryFilters().
        //
        // Required-relationship-filter warning analysis: ContentVersion has NO required FK navigations
        // to other entities with global query filters (EntityId is a loose plain int, no navigation
        // properties). Therefore no required-relationship-filter warning is expected here.
        modelBuilder.Entity<ContentVersion>().HasQueryFilter(cv => cv.IsDeleted != true);

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
