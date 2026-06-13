using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Mastery.Dtos;
using Learnexia.Modules.Learning.Application.Services;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// In-process implementation of <see cref="IMasteryService"/>.
/// Thin wrapper over <see cref="ILearningRepository.GetAllMasteryForStudentAsync"/> for use by
/// P3-08 / P3-10 / P3-11 within the Learning module boundary.
///
/// P5-02 CROSS-MODULE READ: the Parent module cannot inject this service directly (module isolation).
/// See <see cref="IMasteryService"/> for the P5-02 deferred resolution strategy.
/// </summary>
public class MasteryService : IMasteryService
{
    private readonly ILearningRepository _repository;

    public MasteryService(ILearningRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<List<MasteryDto>> GetMasteryForStudent(int studentId, CancellationToken ct = default)
    {
        var rows = await _repository.GetAllMasteryForStudentAsync(studentId, ct);

        return rows.Select(m => new MasteryDto
        {
            SkillId           = m.SkillId,
            MasteryPercentage = m.MasteryPercentage,
            Status            = m.Status,
            AttemptsCount     = m.AttemptsCount,
            LastPracticedAt   = m.LastPracticedAt == default ? null : m.LastPracticedAt,
        }).ToList();
    }
}
