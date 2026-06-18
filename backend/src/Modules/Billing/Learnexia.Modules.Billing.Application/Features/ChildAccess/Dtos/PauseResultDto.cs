using Learnexia.Modules.Billing.Domain.Enums;

namespace Learnexia.Modules.Billing.Application.Features.ChildAccess.Dtos;

/// <summary>
/// Result returned by <see cref="Abstractions.IPauseChildService.PauseChildAsync"/> and
/// <see cref="Abstractions.IPauseChildService.UnpauseChildAsync"/> (P10-18-BE-2).
///
/// <para>Carries the child's updated <see cref="ParentPauseState"/> and the UTC timestamp
/// of the change. On idempotent no-op calls the <see cref="ParentPauseState"/> reflects the
/// existing state and <see cref="WasNoOp"/> is <see langword="true"/>.</para>
/// </summary>
public sealed record PauseResultDto
{
    /// <summary>The child user id this result pertains to.</summary>
    public int ChildId { get; init; }

    /// <summary>The child's current <see cref="ParentPauseState"/> after the operation.</summary>
    public ParentPauseState ParentPauseState { get; init; }

    /// <summary>
    /// UTC timestamp when the state was last changed. Null if the child has never been paused
    /// (i.e. state is <see cref="ParentPauseState.Active"/> and no prior pause event exists).
    /// </summary>
    public DateTime? ChangedAt { get; init; }

    /// <summary>
    /// <see langword="true"/> when the operation was an idempotent no-op (e.g. pausing an
    /// already-paused child). The state is unchanged; no ledger entry was written.
    /// </summary>
    public bool WasNoOp { get; init; }
}
