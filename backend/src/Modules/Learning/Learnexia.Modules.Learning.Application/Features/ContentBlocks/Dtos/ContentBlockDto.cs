using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;

/// <summary>
/// Student-facing content block DTO — excludes admin-only fields (IsActive).
/// Ordered by SequenceOrder; only active, non-deleted blocks are returned to students.
/// </summary>
public record ContentBlockDto
{
    public int Id { get; init; }
    public ContentBlockType BlockType { get; init; }

    /// <summary>
    /// JSON payload — shape depends on BlockType:
    /// Text:    { "markdown": "..." }
    /// Image:   { "url": "...", "altText": "..." }
    /// Video:   { "url": "...", "caption": "..." }
    /// Callout: { "variant": "info|warning|tip", "markdown": "..." }
    /// </summary>
    public string Payload { get; init; } = null!;
    public int SequenceOrder { get; init; }
}
