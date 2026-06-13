namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Cross-module seam: resolves the current learning profile (grade, age, language) for a child
/// at request time.
///
/// <para>The profile must be read per-request, never cached stale — per-child grade transitions
/// (CLAUDE.md product rule) can change grade between requests.</para>
///
/// <para><strong>P3-04 wiring:</strong> P3-04 registers a real implementation sourced from the
/// Identity/Parent module once a child-profile seam is available. Until then the Ai module
/// registers a stub that returns safe defaults — callers (handlers) are expected to populate
/// <see cref="PromptContext"/> from the JWT/claim context before calling <c>IPromptBuilder.Build</c>,
/// falling back to this seam only when the handler does not have profile data in-scope.</para>
///
/// <para>The seam intentionally returns only anonymous proxies (grade, age, language) — never
/// the child's name, email, or any direct identifier (PII minimisation, security rule).</para>
/// </summary>
public interface IChildLearningProfileQuery
{
    /// <summary>
    /// Returns the learning profile for <paramref name="studentId"/>.
    /// The implementation must not throw; return a safe default on failure.
    /// </summary>
    Task<ChildLearningProfile> GetAsync(int studentId, CancellationToken ct = default);
}

/// <summary>
/// The anonymous learning profile facts used by the prompt builder (P3-03).
/// Contains only grade and age (anonymous proxies) — no name, email, or identifier.
/// </summary>
/// <param name="Grade">The student's current school grade (1–12).</param>
/// <param name="Age">The student's approximate age in years.</param>
/// <param name="Language">The student's medium-of-instruction language preference.</param>
public sealed record ChildLearningProfile(int Grade, int Age, TutorLanguage Language);
