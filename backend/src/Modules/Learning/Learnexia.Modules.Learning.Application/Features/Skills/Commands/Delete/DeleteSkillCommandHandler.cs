using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.Delete;

public class DeleteSkillCommandHandler : BaseResponseHandler, ICommandHandler<DeleteSkillCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeleteSkillCommandHandler(ILearningServiceManager service, ILoggerManager logger, IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _service = service;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(DeleteSkillCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            return await _service.SkillService.DeleteAsync(request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in DeleteSkillCommand");
            return ServerError<string>(ex.Message);
        }
    }
}
