namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Resolution state of a <c>QuestionRecalibrationProposal</c> (P5-07).
/// Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> — never a magic string/int.
///
/// Non-zero start (members begin at 1) — avoids the enum-0 default trap where a
/// forgotten initializer or a zero-value DB row silently resolves to an unintended state.
///
/// Members:
/// - <see cref="Pending"/>  = 1 — awaiting admin review; at most one per question (DB partial unique index).
/// - <see cref="Promoted"/> = 2 — admin accepted; <c>QuizQuestion.Difficulty</c> updated to proposed band.
/// - <see cref="Rejected"/> = 3 — admin declined; proposal closed, authored band unchanged.
/// </summary>
public enum ProposalStatus
{
    /// <summary>Awaiting admin review. Only one Pending proposal per question is allowed (partial unique index).</summary>
    Pending = 1,

    /// <summary>Admin accepted the proposal; authored difficulty updated to the proposed band.</summary>
    Promoted = 2,

    /// <summary>Admin declined the proposal; authored difficulty unchanged.</summary>
    Rejected = 3,
}
