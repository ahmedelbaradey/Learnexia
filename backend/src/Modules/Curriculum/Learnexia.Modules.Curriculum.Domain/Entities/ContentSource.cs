using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Curriculum.Domain.Entities;

/// <summary>
/// Root of the provenance tree — represents a physical source document from which
/// curriculum content was extracted. (curriculum-system-of-record.md §1b, BL-04 BE-8)
///
/// Design rules:
/// - <see cref="GradeId"/> is a plain int (no cross-module FK/nav to learning.Grades).
/// - <see cref="Language"/> reuses the existing <see cref="ContentLanguage"/> enum.
/// - Has many <see cref="Chapter"/>s (intra-module FK, one-to-many).
/// </summary>
public class ContentSource : AggregateRoot
{
    /// <summary>
    /// Physical type of this source document (Book, Image, Worksheet, VideoTranscript).
    /// Stored as int.
    /// </summary>
    public ContentSourceType Type { get; set; }

    /// <summary>
    /// Human-readable title of the source (e.g. "Grade 4 Mathematics Textbook Vol.1").
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Optional publisher name (e.g. "Ministry of Education").
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    /// Optional edition identifier (e.g. "2023 Edition").
    /// </summary>
    public string? Edition { get; set; }

    /// <summary>
    /// Language of the source content. Stored as int.
    /// Reuses the existing <see cref="ContentLanguage"/> enum (module isolation — no new language enum).
    /// </summary>
    public ContentLanguage Language { get; set; }

    /// <summary>
    /// Loose reference to the target grade (no cross-module FK).
    /// Cross-module isolation: GradeId is a plain int referencing learning.Grades.Id.
    /// </summary>
    public int GradeId { get; set; }

    // Navigation
    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
}
