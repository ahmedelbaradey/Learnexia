using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Commands.Add;

public class AddGradeCommandHandler : BaseResponseHandler, ICommandHandler<AddGradeCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddGradeCommandHandler(ILearningServiceManager service, ILoggerManager logger, IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _service = service;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(AddGradeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Deferred-commit: the service stages the entity; the UnitOfWorkBehavior commits after the handler.
            return await _service.GradeService.AddAsync<AddGradeCommand>(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddGradeCommand");
            return ServerError<string>(ex.Message);
        }
    }
}
