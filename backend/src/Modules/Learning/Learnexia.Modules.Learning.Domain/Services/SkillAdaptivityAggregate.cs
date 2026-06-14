namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Raw aggregate returned by <c>ILearningRepository.GetAdaptivitySignalsAsync</c>.
///
/// This is a read-only projection from <c>StudentAnswer</c> rows for a given (studentId, skillId) pair.
/// <c>AdaptivityService</c> maps this aggregate into <see cref="AdaptivitySignals"/> before calling
/// <see cref="AdaptivityEngine.DecideDifficulty"/>.
///
/// <c>RetryCount</c> is defined as extra answers beyond the first (TotalAnswers - 1, floored at 0).
/// This proxy captures repeated attempts at the same skill without requiring explicit retry tracking.
/// </summary>
public record SkillAdaptivityAggregate(
    int    TotalAnswers,
    int    CorrectAnswers,
    double AvgTimeSeconds,
    int    HintCount,
    int    RetryCount);
