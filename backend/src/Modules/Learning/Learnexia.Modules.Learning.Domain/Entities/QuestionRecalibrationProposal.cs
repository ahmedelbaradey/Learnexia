using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// A human-promotable proposal to recalibrate a question's authored difficulty band (P5-07 BE-2).
///
/// The calibration job writes (or upserts) a <see cref="ProposalStatus.Pending"/> proposal when the
/// empirical p-value band diverges from the authored <see cref="DifficultyLevel"/> by enough evidence
/// (<c>CalibrationOptions.MinSamples</c> gate). Admins then promote or reject via BE-5.
///
/// <b>Safety model (LOCKED):</b> the job NEVER mutates <see cref="QuizQuestion.Difficulty"/> directly.
/// The authored band changes ONLY via the human <c>Promote</c> path (BE-5).
///
/// <b>One-open-proposal rule:</b> a partial unique index (<c>WHERE "Status" = 1</c>) guarantees
/// at most one <see cref="ProposalStatus.Pending"/> proposal per question. The job upserts the
/// open proposal on re-runs rather than inserting duplicates.
///
/// <b>Reason field:</b> stores a stable machine-generated reason CODE (not English prose) so the
/// application layer can localize it at read time — avoiding the persisted-string localization debt
/// (P5-07 OQ-6 decision: store code, localize at read).
///
/// Derives from <see cref="AggregateRoot"/> so it can raise domain events
/// (<c>AdminActionPerformedDomainEvent</c>) on promote/reject for AC4 auditability.
/// </summary>
public class QuestionRecalibrationProposal : AggregateRoot
{
    /// <summary>
    /// Real intra-module FK to <see cref="QuizQuestion.Id"/> (Restrict).
    /// Composite index on <c>(QuestionId, Status)</c> supports "open proposal for this question" lookups.
    /// </summary>
    public int QuestionId { get; set; }

    /// <summary>Navigation to the owning question (intra-module FK, Restrict).</summary>
    public QuizQuestion Question { get; set; } = null!;

    /// <summary>
    /// Snapshot of the authored difficulty at the time the proposal was generated.
    /// Never modified after insert — preserves the before-picture for auditability (AC7).
    /// Stored as <c>integer</c> via <c>HasConversion&lt;int&gt;()</c>.
    /// </summary>
    public DifficultyLevel AuthoredDifficulty { get; set; }

    /// <summary>
    /// The empirically inferred difficulty band that the calibration engine proposes.
    /// Applied to <see cref="QuizQuestion.Difficulty"/> ONLY when an admin promotes this proposal.
    /// Stored as <c>integer</c> via <c>HasConversion&lt;int&gt;()</c>.
    /// </summary>
    public DifficultyLevel ProposedDifficulty { get; set; }

    /// <summary>
    /// The p-value (fraction correct, 0.0–1.0) that triggered this proposal.
    /// Preserved as evidence for admin review and future auditability.
    /// </summary>
    public double EvidencePValue { get; set; }

    /// <summary>
    /// The sample size at the time the proposal was generated.
    /// </summary>
    public int EvidenceSampleSize { get; set; }

    /// <summary>
    /// A stable machine-generated reason code (e.g. <c>"DIFFICULTY_DIVERGED_EASY_VS_HARD"</c>).
    /// NOT localized prose — the application layer maps this code to a localized string at read time
    /// (P5-07 OQ-6 decision). Using a stable code keeps the persisted row locale-agnostic.
    /// </summary>
    public string ReasonCode { get; set; } = null!;

    /// <summary>
    /// Resolution state of this proposal.
    /// DB column starts as <see cref="ProposalStatus.Pending"/> (1) for all new inserts.
    /// Stored as <c>integer</c> via <c>HasConversion&lt;int&gt;()</c>.
    /// </summary>
    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

    /// <summary>
    /// UTC timestamp of when an admin resolved (promoted or rejected) this proposal.
    /// <c>null</c> while <see cref="Status"/> is <see cref="ProposalStatus.Pending"/>.
    /// Stored as <c>timestamptz</c> nullable.
    /// </summary>
    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>
    /// Plain <c>int</c> reference to the Identity user (admin) who resolved this proposal.
    /// <c>null</c> while pending. No cross-module FK (module isolation rule 1).
    /// </summary>
    public int? ResolvedByUserId { get; set; }
}
