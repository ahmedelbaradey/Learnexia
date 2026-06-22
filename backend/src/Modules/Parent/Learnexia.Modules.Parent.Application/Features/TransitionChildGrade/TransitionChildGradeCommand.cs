using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.TransitionChildGrade;

/// <summary>
/// Parent requests a self-service grade transition for one of their linked children (P5-06).
/// Non-destructive: XP, badges, streaks, mastery, and attempt rows are NOT touched.
/// Learning re-scope is automatic — the Learning module reads grade at query time.
///
/// Security:
///   - <c>ChildId</c> is supplied from the route (controller); the acting parent id is ALWAYS
///     resolved from the JWT (<see cref="Learnexia.Shared.Kernel.Abstractions.ICurrentUserService"/>)
///     — never from the request body (IDOR rule mirrors <c>UpdateChildCommand</c>).
///   - Family-scope link check is performed in the handler before any mutation.
///   - No server-side confirm flag (grade transition is non-destructive — FE provides the dialog).
/// </summary>
public record TransitionChildGradeCommand : ICommand<BaseResponse<TransitionedChildGradeResponse>>
{
    /// <summary>The child's user id (supplied from the route, not the body).</summary>
    public int ChildId { get; set; }

    /// <summary>Target grade (1–6). Validated by <see cref="TransitionChildGradeCommandValidator"/>.</summary>
    public int TargetGrade { get; set; }
}
