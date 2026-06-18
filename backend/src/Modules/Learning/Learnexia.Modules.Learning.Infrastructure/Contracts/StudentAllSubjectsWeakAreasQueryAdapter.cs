using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Contracts.Learning;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="IStudentAllSubjectsWeakAreasQuery"/> by delegating to
/// <see cref="IWeakAreaDetectorService"/> (P5-02-BE-2 / the real seam implementation).
///
/// <para>Cross-module isolation: Parent and Ai modules inject
/// <see cref="IStudentAllSubjectsWeakAreasQuery"/> from <c>Shared.Contracts</c>; they never
/// reference this adapter or any Learning project directly.</para>
///
/// <para>This adapter is a thin pass-through; all detection logic lives in
/// <see cref="WeakAreaDetectorService"/>.</para>
/// </summary>
public sealed class StudentAllSubjectsWeakAreasQueryAdapter : IStudentAllSubjectsWeakAreasQuery
{
    private readonly IWeakAreaDetectorService _detector;

    public StudentAllSubjectsWeakAreasQueryAdapter(IWeakAreaDetectorService detector)
    {
        _detector = detector;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WeakAreaEntry>> GetWeakAreasAsync(
        int studentId,
        int maxResults = 5,
        CancellationToken ct = default)
        => _detector.DetectAsync(studentId, maxResults, ct);
}
