using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Curriculum.Domain.Entities;

/// <summary>
/// A chapter within a <see cref="ContentSource"/>. (curriculum-system-of-record.md §1b, BL-04 BE-8)
///
/// Design rules:
/// - <see cref="ContentSourceId"/> is an intra-module FK to <see cref="ContentSource"/> (real EF nav OK).
/// - A Chapter is NOT a pedagogical Unit — these are provenance tree nodes, not the teaching hierarchy.
/// </summary>
public class Chapter : AggregateRoot
{
    /// <summary>
    /// Intra-module FK to <see cref="ContentSource"/>.
    /// </summary>
    public int ContentSourceId { get; set; }

    /// <summary>
    /// Chapter number within the source document (e.g. 5 for "Chapter 5").
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Human-readable chapter title.
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Optional page number where this chapter starts (physical source page, not curriculum page).
    /// </summary>
    public int? PageStart { get; set; }

    /// <summary>
    /// Optional page number where this chapter ends.
    /// </summary>
    public int? PageEnd { get; set; }

    // Navigation
    public ContentSource ContentSource { get; set; } = null!;
}
