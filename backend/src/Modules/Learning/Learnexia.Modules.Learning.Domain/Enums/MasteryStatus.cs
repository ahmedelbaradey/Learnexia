namespace Learnexia.Modules.Learning.Domain.Enums;

/// <summary>
/// Persistence state of a student's mastery over a skill.
/// Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> — never a magic string or int literal.
///
/// State machine (P3-09 Q2 — per-skill threshold + 50% floor):
///   <c>NotStarted</c>   — the student has made zero answers against this skill.
///   <c>NeedsReview</c>  — answered, but cumulative accuracy &lt; 50 % (FR-AD-3 remediation bar).
///   <c>InProgress</c>   — 50 % ≤ accuracy &lt; per-skill MasteryThreshold.
///   <c>Mastered</c>     — accuracy ≥ per-skill MasteryThreshold (seeded 70–85 %; default 80 % per FR-AD-3).
///
/// Values are intentionally zero-based so the EF column default (0) maps to <c>NotStarted</c>
/// without needing an explicit seed value in the DB migration.
/// </summary>
public enum MasteryStatus
{
    NotStarted  = 0,
    InProgress  = 1,
    Mastered    = 2,
    NeedsReview = 3,
}
