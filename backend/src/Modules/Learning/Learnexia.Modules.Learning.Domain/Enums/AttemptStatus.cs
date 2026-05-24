namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Lifecycle state of a quiz attempt. Stored as int via HasConversion&lt;int&gt;() per the
/// project no-free-text rule — referenced by member, never a magic string/int.
/// </summary>
public enum AttemptStatus
{
    InProgress = 1,
    Completed = 2,
    Abandoned = 3,
}
