using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Commands.Add;

public class AddConceptCommandHandler : BaseResponseHandler, ICommandHandler<AddConceptCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddConceptCommandHandler(
        ILearningServiceManager service,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _service = service;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(AddConceptCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // DEFECT-2 fix: pre-check parent Subject existence before staging the insert.
            // Without this, a bad SubjectId reaches SaveChangesAsync → FK violation → DbUpdateException → 500.
            // The AnyAsync EF call now lives inside IConceptService.SubjectExistsAsync (Infrastructure) —
            // Application layer stays EF-free (Option C).
            var subjectExists = await _service.ConceptService.SubjectExistsAsync(request.SubjectId, cancellationToken);

            if (!subjectExists)
                return NotFound<string>(_localizer[SharedResourcesKey.SubjectNotFound]);

            return await _service.ConceptService.AddAsync<AddConceptCommand>(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddConceptCommand");
            return ServerError<string>();
        }
    }
}
