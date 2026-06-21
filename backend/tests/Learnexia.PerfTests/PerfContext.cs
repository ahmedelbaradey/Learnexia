namespace Learnexia.PerfTests;

/// <summary>
/// Holds all IDs and JWTs resolved during the warmup/init phase.
/// Populated once before the NBomber scenarios start; read-only during the load phase.
/// </summary>
internal sealed class PerfContext
{
    // ------------------------------------------------------------------
    // JWTs
    // ------------------------------------------------------------------
    public string StudentToken  { get; set; } = string.Empty;
    public string ParentToken   { get; set; } = string.Empty;
    public string AdminToken    { get; set; } = string.Empty;

    // ------------------------------------------------------------------
    // IDs resolved during warmup
    // ------------------------------------------------------------------
    public int StudentUserId { get; set; }
    public int ParentUserId  { get; set; }

    /// <summary>ID of the child (Student-role) user linked to the parent.</summary>
    public int ChildId { get; set; }

    /// <summary>A real subjectId seeded by the Dev seed (Grade 1 Mathematics).</summary>
    public int SubjectId { get; set; }

    /// <summary>A real lessonId under SubjectId that has at least one question.</summary>
    public int LessonId { get; set; }

    /// <summary>
    /// The first questionId in LessonId — used in SubmitAnswer and AI scenarios.
    /// May be 0 if the lesson has no questions (seeding fell back to empty lesson).
    /// </summary>
    public int QuestionId { get; set; }

    /// <summary>
    /// A live attemptId obtained by calling StartAttempt during warmup.
    /// The hot-loop scenarios reuse this (StartAttempt is resume-safe).
    /// </summary>
    public int AttemptId { get; set; }

    /// <summary>
    /// A nodeId discovered from the skill-tree (used in KnowledgeGraph scenario).
    /// May be 0 if no skill-tree node was found.
    /// </summary>
    public int NodeId { get; set; }
}
