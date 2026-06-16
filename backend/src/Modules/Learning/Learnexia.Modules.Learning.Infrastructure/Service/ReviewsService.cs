using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Infrastructure implementation of <see cref="IReviewsService"/>.
/// Thin wrapper over <see cref="ILearningRepository.GetDueMasteryRowsAsync"/> for use by
/// the GetDueReviews query handler (Option C — no EF in Application).
/// AsNoTracking — this is a read path; no writes are staged here.
/// </summary>
public class ReviewsService : LearningBaseService<StudentSkillMastery>, IReviewsService
{
    public ReviewsService(
        ILearningRepository repository,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentSkillMastery>> GetDueMasteryRowsAsync(
        DateTime utcNow, CancellationToken ct = default)
        => await _repository.GetDueMasteryRowsAsync(utcNow, ct);
}
