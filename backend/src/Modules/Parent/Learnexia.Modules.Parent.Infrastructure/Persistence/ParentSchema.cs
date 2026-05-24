namespace Learnexia.Modules.Parent.Infrastructure.Persistence;

/// <summary>
/// Schema name for the Parent module (schema-per-module). Defined as a standalone constant so the EF
/// <c>IEntityTypeConfiguration</c>s can reference it. The DbContext exposes <c>Schema</c> via this value.
/// </summary>
public static class ParentSchema
{
    public const string Name = "parent";
}
