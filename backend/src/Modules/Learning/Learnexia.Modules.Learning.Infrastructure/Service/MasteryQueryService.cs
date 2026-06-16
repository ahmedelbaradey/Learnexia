using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Infrastructure implementation of <see cref="IMasteryQueryService"/>.
/// Thin wrapper over <see cref="ILearningRepository"/> mastery-read methods for use by
/// mastery query handlers within the Learning module (Option C — no EF in Application).
///
/// Distinct from <see cref="MasteryService"/> (<c>Application/Services/IMasteryService</c>)
/// which is the cross-module seam that returns projected DTOs. This service returns materialized
/// domain rows for internal handler use.
/// </summary>
public class MasteryQueryService : LearningBaseService<StudentSkillMastery>, IMasteryQueryService
{
    public MasteryQueryService(
        ILearningRepository repository,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    /// <inheritdoc />
    public async Task<StudentSkillMastery?> GetSkillMasteryForStudentAsync(
        int studentId, int skillId, CancellationToken ct = default)
        => await _repository.GetSkillMasteryForStudentAsync(studentId, skillId, ct);

    /// <inheritdoc />
    public async Task<List<StudentSkillMastery>> GetAllMasteryForStudentAsync(
        int studentId, CancellationToken ct = default)
        => await _repository.GetAllMasteryForStudentAsync(studentId, ct);
}
