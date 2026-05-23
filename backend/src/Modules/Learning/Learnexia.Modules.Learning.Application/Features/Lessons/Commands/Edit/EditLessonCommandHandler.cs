using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Edit;

public class EditLessonCommandHandler : BaseResponseHandler, ICommandHandler<EditLessonCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditLessonCommandHandler(ILearningServiceManager service, ILoggerManager logger, IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _service = service;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(EditLessonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            return await _service.LessonService.UpdateAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditLessonCommand");
            return ServerError<string>(ex.Message);
        }
    }
}
