using AutoMapper;
using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Admin.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Queries.GetAdminMissionDefinitions;

/// <summary>Returns all mission definitions (active + inactive) for admin display.</summary>
public sealed class GetAdminMissionDefinitionsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetAdminMissionDefinitionsQuery, BaseResponse<List<MissionDefinitionDto>>>
{
    private readonly IGamificationRepository _repository;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;

    public GetAdminMissionDefinitionsQueryHandler(
        IGamificationRepository repository,
        IMapper mapper,
        ILoggerManager logger)
    {
        _repository = repository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<List<MissionDefinitionDto>>> Handle(
        GetAdminMissionDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var definitions = await _repository.GetAllMissionDefinitionsAdminAsync(cancellationToken);
            var dtos = _mapper.Map<List<MissionDefinitionDto>>(definitions);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetAdminMissionDefinitionsQuery");
            return ServerError<List<MissionDefinitionDto>>();
        }
    }
}
