namespace Learnexia.Modules.Moderation.Application;

/// <summary>
/// Marker type so the Host can reference this Application assembly for the unified MediatR scan
/// (ADR 0002 §4 / P4-01-BE-4). Mirrors the AssemblyReference markers in the other module
/// Application projects. This is the #1 most critical registration — without it, the
/// AuditLogEventHandler is never discovered and AdminActionPerformedEvent publishes into a void.
/// </summary>
public class AssemblyReference
{
}
