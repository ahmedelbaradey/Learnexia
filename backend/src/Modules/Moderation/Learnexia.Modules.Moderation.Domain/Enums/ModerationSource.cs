namespace Learnexia.Modules.Moderation.Domain.Enums;

/// <summary>
/// Identifies the origin of a flagged content item that enters the moderation queue.
/// Persisted as a string for stability (no rename-breaks if enum names are reused).
/// </summary>
public enum ModerationSource
{
    /// <summary>
    /// Flagged output from the AI safety pipeline (P3-02 <c>SafetyLayer</c>).
    /// This is the only live producer at P7-09 ship time.
    /// </summary>
    AiOutput,

    /// <summary>
    /// Curriculum content uploaded by an admin and queued for human review.
    /// Producer (BL-01) is not yet built; items with this source degrade to an empty
    /// sub-queue until BL-01 publishes its integration event.
    /// </summary>
    CurriculumUpload
}
