using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.Learning;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Re-wires the Ai module's <see cref="IStudentWeakAreasQuery"/> (subject-scoped) to use the
/// real P5-02 weak-area detector instead of the <c>EmptyWeakAreasQuery</c> placeholder (P5-02-BE-3).
///
/// <para>The Ai seam signature is per-subject: <c>GetWeakAreasAsync(studentId, Subject, ct)</c>.
/// This bridge delegates to <see cref="IWeakAreaDetectorService"/> (all-subjects) and then filters
/// the result to the requested <c>Subject</c> — so Ai prompt-builder consumers continue to receive
/// only the subject-relevant weak areas as before, but now backed by real mastery data.</para>
///
/// <para>Registered in <c>AddLearningInfrastructure</c> as <c>IStudentWeakAreasQuery</c>, which the
/// Host runs BEFORE the Ai module. The Ai module registers <c>EmptyWeakAreasQuery</c> via
/// <c>TryAddTransient</c>, so when Learning is loaded this bridge (registered first) is the one that
/// resolves; the Ai stub remains only as the fallback for Ai-isolated scenarios.</para>
///
/// <para>Module isolation: this class lives in <c>Learning.Infrastructure</c> and references both
/// <c>Shared.Contracts.Ai</c> (for <see cref="IStudentWeakAreasQuery"/> + <see cref="WeakArea"/>)
/// and <c>Shared.Contracts.Learning</c> (for <see cref="WeakAreaEntry"/>). It does NOT reference
/// any Ai module project — that would violate module isolation.</para>
///
/// <para>P5-02-BE-3 (DI re-wire).</para>
/// </summary>
public sealed class AiWeakAreasQueryBridge : IStudentWeakAreasQuery
{
    // Mapping from Ai.Subject enum to Learning.Domain.Enums.SubjectCode int values.
    // Ai.Subject: Math=1, Science=2, Arabic=3, English=4.
    // SubjectCode:  MATH=0, SCIENCE=1, ARABIC=2, ENGLISH=3.
    private static readonly Dictionary<Subject, int> AiSubjectToSubjectCode = new()
    {
        { Subject.Math,    (int)SubjectCode.MATH    },
        { Subject.Science, (int)SubjectCode.SCIENCE },
        { Subject.Arabic,  (int)SubjectCode.ARABIC  },
        { Subject.English, (int)SubjectCode.ENGLISH },
    };

    private readonly IWeakAreaDetectorService _detector;

    public AiWeakAreasQueryBridge(IWeakAreaDetectorService detector)
    {
        _detector = detector;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WeakArea>> GetWeakAreasAsync(
        int studentId,
        Subject subject,
        CancellationToken ct = default)
    {
        // Detect ALL weak areas (no limit — return up to 20 so the subject filter still gets results).
        var all = await _detector.DetectAsync(studentId, maxResults: 20, ct);

        if (all.Count == 0)
            return Array.Empty<WeakArea>();

        // Filter to the requested subject.
        if (!AiSubjectToSubjectCode.TryGetValue(subject, out int subjectCodeInt))
            return Array.Empty<WeakArea>();

        // Map WeakAreaEntry → Ai.WeakArea (normalize MasteryScore to [0,1]).
        return all
            .Where(e => e.SubjectCode == subjectCodeInt)
            .Select(e => new WeakArea(
                SkillId:      e.SkillId,
                SkillName:    e.SkillName,
                MasteryScore: e.MasteryPercent / 100.0))
            .ToList()
            .AsReadOnly();
    }
}
