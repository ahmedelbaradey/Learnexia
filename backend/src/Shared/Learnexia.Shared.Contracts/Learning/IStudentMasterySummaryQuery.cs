namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Cross-module read seam: Learning implements, Parent (and any other consumer) injects.
/// Returns per-subject mastery percentages and an overall mastery rollup for a student.
///
/// <para>Source data is <c>StudentSkillMastery</c> rows — the P3-09 mastery engine output.
/// Subjects are the four MVP subjects (Math / Science / Arabic / English) identified by
/// <c>SubjectCode</c> in the Learning domain.</para>
///
/// <para>Registered in <c>AddLearningInfrastructure()</c> as Scoped.</para>
///
/// <para>Sentinel contract: a brand-new student with no mastery rows returns a
/// <see cref="MasterySummary"/> with all subjects at 0% — never null, never throws (AC3).</para>
///
/// <para>P5-08-BE-3 (Learning batch 1c).</para>
/// </summary>
public interface IStudentMasterySummaryQuery
{
    /// <summary>
    /// Returns per-subject mastery summary for <paramref name="studentId"/>.
    /// </summary>
    /// <param name="studentId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="MasterySummary"/> — never null. All four subjects are always present
    /// in the <see cref="MasterySummary.BySubject"/> list; zero-percent entries represent
    /// subjects the student has not yet practiced.
    /// </returns>
    Task<MasterySummary> GetByStudentAsync(int studentId, CancellationToken ct = default);
}

/// <summary>
/// Per-subject + overall mastery rollup for a student.
/// Produced by <see cref="IStudentMasterySummaryQuery"/>.
/// </summary>
/// <param name="OverallPercent">
/// Average mastery percentage across all skills the student has practiced (0–100).
/// Zero when the student has no mastery data yet.
/// </param>
/// <param name="BySubject">
/// One entry per curriculum subject. Always contains exactly the four MVP subjects
/// (Math / Science / Arabic / English) even when the student has no data for a subject
/// (that subject's <see cref="SubjectMasteryEntry.Percent"/> is 0).
/// </param>
public record MasterySummary(
    int OverallPercent,
    IReadOnlyList<SubjectMasteryEntry> BySubject);

/// <summary>
/// Mastery percentage for a single subject.
/// </summary>
/// <param name="SubjectCode">
/// The stable curriculum code for the subject (MATH / SCIENCE / ARABIC / ENGLISH).
/// Matches <c>Learning.Domain.Enums.SubjectCode</c> int values — stored here as int
/// to avoid a cross-module enum reference from Shared.Contracts back into Learning.Domain.
/// Consumers that need the enum cast from int.
/// </param>
/// <param name="Percent">Average mastery percentage for all skills in this subject (0–100).</param>
public record SubjectMasteryEntry(int SubjectCode, int Percent);
