using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.SimilarExample.Commands;

/// <summary>
/// Validates <see cref="SimilarExampleCommand"/> via <c>ValidationBehavior</c> (runs for
/// <c>ICommand&lt;T&gt;</c> only — not for queries).
///
/// Rules (per P3-06-BE-10):
/// <list type="bullet">
///   <item><see cref="SimilarExampleCommand.SkillId"/> must be greater than zero — scopes the
///         example to the active skill context (AC-7 scope guard).</item>
/// </list>
/// </summary>
public sealed class SimilarExampleCommandValidator : AbstractValidator<SimilarExampleCommand>
{
    public SimilarExampleCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        // SkillId is required and must be a valid positive id (scope guard — AC-7).
        RuleFor(c => c.SkillId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.SimilarExampleSkillIdRequired]);
    }
}
