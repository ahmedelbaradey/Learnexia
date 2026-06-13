using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Output of <see cref="AdaptivityEngine.DecideDifficulty"/>.
///
/// <c>Difficulty</c> — the recommended <see cref="DifficultyLevel"/> for the next quiz/question set.
///   Easy / Medium / Hard — same enum used by <c>QuizQuestion.Difficulty</c> so P3-11 can filter directly.
/// <c>IsDefault</c> — true when the decision is the cold-start default (Medium), not computed from real signals.
///   Consumers (P3-11) may apply their own fallback policy when this is true.
/// <c>Score</c>    — the normalized weighted performance score ∈ [0, 1] that drove the banding decision.
///   0 when IsDefault=true. Useful for diagnostics / the optional inspection endpoint (BE-5).
/// </summary>
public record AdaptivityDecision(
    DifficultyLevel Difficulty,
    bool            IsDefault,
    double          Score);
