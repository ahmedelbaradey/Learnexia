using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Validation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Add;

/// <summary>
/// Validates <see cref="AddContentBlockCommand"/> before the handler runs.
/// Per-type payload shape is delegated to <see cref="ContentBlockPayloadValidator"/>.
/// </summary>
public class AddContentBlockCommandValidator : AbstractValidator<AddContentBlockCommand>
{
    public AddContentBlockCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.LessonId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);

        RuleFor(x => x.BlockType)
            .IsInEnum().WithMessage(localizer[SharedResourcesKey.ContentBlockTypeInvalid]);

        RuleFor(x => x.Payload)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.ContentBlockPayloadInvalid])
            // P7-SEC-2: hard upper bound — jsonb column is unbounded at DB level, so the validator is the guard.
            .MaximumLength(ContentBlockPayloadValidator.MaxPayloadLength)
                .WithMessage(localizer[SharedResourcesKey.ContentBlockPayloadTooLong])
            .Must((cmd, payload) =>
            {
                var error = ContentBlockPayloadValidator.Validate(cmd.BlockType, payload, localizer);
                return error is null;
            })
            .WithMessage((cmd, _) =>
                ContentBlockPayloadValidator.Validate(cmd.BlockType, cmd.Payload, localizer)
                ?? localizer[SharedResourcesKey.ContentBlockPayloadInvalid]);
    }
}
