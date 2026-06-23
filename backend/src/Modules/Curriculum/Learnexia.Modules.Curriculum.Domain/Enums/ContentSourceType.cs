namespace Learnexia.Modules.Curriculum.Domain.Enums;

/// <summary>
/// Physical type of a curriculum source document. Stored as int in the database.
/// See curriculum-system-of-record.md §1b (provenance tree — BE-8).
/// </summary>
public enum ContentSourceType
{
    Book = 0,
    Image = 1,
    Worksheet = 2,
    VideoTranscript = 3,
}
