using FluentValidation;

namespace Learnexia.Modules.Ai.Application.Features.Recommendation.Commands;

/// <summary>
/// Validates <see cref="RecommendationNarrationCommand"/> via <c>ValidationBehavior</c>
/// (runs for <c>ICommand&lt;T&gt;</c> only — not for queries).
///
/// <para>The command carries no body fields. The only validator contract to uphold is that the
/// command record itself is non-null (guaranteed by the model-binder) and that the child id is
/// resolved server-side from the JWT (enforced in the handler — not the validator).</para>
///
/// <para>An explicit rule is still required so the <c>ValidationBehavior</c> pipeline
/// discovers and registers this validator class.</para>
/// </summary>
public sealed class RecommendationNarrationCommandValidator : AbstractValidator<RecommendationNarrationCommand>
{
    public RecommendationNarrationCommandValidator()
    {
        // The command body is intentionally empty — child id always comes from JWT (security rule).
        // No additional validation rules needed at the command level.
    }
}
