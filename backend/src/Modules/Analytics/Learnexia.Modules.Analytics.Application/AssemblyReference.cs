namespace Learnexia.Modules.Analytics.Application;

/// <summary>
/// Marker type so the Host can reference this Application assembly for the unified MediatR scan
/// (ADR 0002 §4). Mirrors the AssemblyReference markers in the other module Application projects.
/// CRITICAL: without this scan, INotificationHandler consumers in this assembly are never discovered
/// and integration-event notifications publish into a void with no error.
/// </summary>
public class AssemblyReference
{
}
