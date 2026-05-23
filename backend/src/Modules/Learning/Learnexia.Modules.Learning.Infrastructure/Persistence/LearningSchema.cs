namespace Learnexia.Modules.Learning.Infrastructure.Persistence;

/// <summary>
/// Schema name for the Learning module (schema-per-module). Defined as a standalone constant so the
/// EF <c>IEntityTypeConfiguration</c>s can reference it before the <c>LearningDbContext</c> is added
/// (Batch 2). The DbContext exposes <c>Schema</c> via this value too.
/// </summary>
public static class LearningSchema
{
    public const string Name = "learning";
}
