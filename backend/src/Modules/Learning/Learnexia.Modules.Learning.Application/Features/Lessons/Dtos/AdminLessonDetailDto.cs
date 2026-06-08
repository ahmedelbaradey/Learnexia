using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;

/// <summary>
/// Admin-facing full lesson detail DTO. Includes all admin-visible fields:
/// - IsActive: admin active/inactive toggle (students see only active lessons).
/// - EstimatedMinutes: lesson duration hint.
/// - ContentBlocks: all non-deleted blocks (including inactive) ordered by SequenceOrder.
///
/// This DTO is NEVER returned to students. It is only served from admin-gated endpoints.
/// Language is resolved (read-only) from the owning Subject tree — not stored on the lesson.
/// </summary>
public record AdminLessonDetailDto
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public DifficultyLevel Difficulty { get; init; }
    public int SequenceOrder { get; init; }
    public bool IsLocked { get; init; }
    public int UnitId { get; init; }
    public int? SkillId { get; init; }
    public string? Explanation { get; init; }
    public string? Visual { get; init; }
    public bool IsBoss { get; init; }

    // P7-02 admin-only fields
    public bool IsActive { get; init; }
    public int EstimatedMinutes { get; init; }

    /// <summary>All non-deleted content blocks (incl. inactive) ordered by SequenceOrder.</summary>
    public List<AdminContentBlockDto> ContentBlocks { get; init; } = new();
}
