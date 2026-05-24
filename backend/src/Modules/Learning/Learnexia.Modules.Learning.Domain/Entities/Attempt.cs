using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// Represents a student's attempt at a quiz (the question set for a lesson).
/// StudentId and LessonId are plain int loose references (module isolation).
///
/// Fields are forward-compatible for P2-07 (IsCorrect/HintUsed) and P2-08 (accuracy, duration,
/// hints, completedAt) — they exist now so no schema rewrite is needed next wave.
/// </summary>
public class Attempt : AggregateRoot
{
    /// <summary>Loose reference to Identity student user (plain int, no FK — module isolation).</summary>
    public int StudentId { get; set; }

    /// <summary>Loose reference to Learning.Lesson (plain int, index only).</summary>
    public int LessonId { get; set; }

    public AttemptStatus Status { get; set; }

    /// <summary>Populated by P2-08 scoring; defaults to 0.</summary>
    public double AccuracyPercentage { get; set; }

    /// <summary>Total elapsed seconds for the attempt; populated by P2-08.</summary>
    public int DurationSeconds { get; set; }

    /// <summary>Number of hints used across all questions in this attempt; populated by P2-08.</summary>
    public int HintsUsedCount { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Null until the attempt is completed or abandoned (P2-08).</summary>
    public DateTime? CompletedAt { get; set; }

    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
}
