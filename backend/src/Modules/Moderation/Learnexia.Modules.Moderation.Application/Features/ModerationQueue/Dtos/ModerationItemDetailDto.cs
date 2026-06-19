namespace Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;

/// <summary>
/// Read-side DTO for the moderation item detail endpoint (P7-09 AC2 / AC3).
/// Extends <see cref="ModerationItemDto"/> with review fields.
///
/// <para>v1 content preview: the <see cref="SafetyVerdict"/> field contains the full JSON payload
/// (failed checks + reason codes + action taken) copied from the source event. No raw-content
/// preview is available in v1 (P3-02 stores none; quarantine store deferred — OQ-8).</para>
/// </summary>
public record ModerationItemDetailDto(
    int Id,
    Guid SourceEventId,
    string Source,
    string ContentReference,
    int? StudentId,
    string? TaskKind,
    string? SubjectCode,
    int? Grade,
    string Status,
    string SafetyVerdict,
    DateTime DetectedAt,
    DateTime CreatedAt,
    // ── Review fields ───────────────────────────────────
    int? ReviewedByUserId,
    DateTime? ReviewedAtUtc,
    string? ReviewDecisionReason);
