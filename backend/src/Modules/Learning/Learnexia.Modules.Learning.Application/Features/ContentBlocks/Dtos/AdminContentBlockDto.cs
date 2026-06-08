using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;

/// <summary>
/// Admin-facing content block DTO — includes IsActive so admins can see and re-activate inactive blocks.
/// Returned in <see cref="AdminLessonDetailDto"/>; never returned to students.
/// </summary>
public record AdminContentBlockDto
{
    public int Id { get; init; }
    public ContentBlockType BlockType { get; init; }
    public string Payload { get; init; } = null!;
    public int SequenceOrder { get; init; }

    /// <summary>Admin-controlled active/inactive toggle. Inactive blocks are hidden from students.</summary>
    public bool IsActive { get; init; }
}
