using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Add;

public class AddSubjectCommandHandler : BaseResponseHandler, ICommandHandler<AddSubjectCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddSubjectCommandHandler(ILearningServiceManager service, ILoggerManager logger, IStringLocalizer<SharedResources> localizer)
    {
        _logger = logger;
        _service = service;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(AddSubjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            return await _service.SubjectService.AddAsync<AddSubjectCommand>(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddSubjectCommand");
            return ServerError<string>(ex.Message);
        }
    }
}
