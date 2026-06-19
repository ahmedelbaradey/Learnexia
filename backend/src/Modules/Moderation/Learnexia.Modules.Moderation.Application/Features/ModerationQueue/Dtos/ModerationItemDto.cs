namespace Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;

/// <summary>
/// Read-side DTO for a moderation queue list item (P7-09 AC1).
/// PII-safe: contains only ids + enum/state values + opaque references.
/// Matches the FE contract defined in <c>P7-09-BE.md §Contract for Frontend</c>.
/// </summary>
public record ModerationItemDto(
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
    DateTime CreatedAt);
