using AutoMapper;
using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Admin.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Queries.GetAdminBadgeDefinitions;

/// <summary>Returns all badge definitions (active + inactive) for admin display.</summary>
public sealed class GetAdminBadgeDefinitionsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetAdminBadgeDefinitionsQuery, BaseResponse<List<BadgeDefinitionDto>>>
{
    private readonly IGamificationRepository _repository;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;

    public GetAdminBadgeDefinitionsQueryHandler(
        IGamificationRepository repository,
        IMapper mapper,
        ILoggerManager logger)
    {
        _repository = repository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<List<BadgeDefinitionDto>>> Handle(
        GetAdminBadgeDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var definitions = await _repository.GetAllBadgeDefinitionsAdminAsync(cancellationToken);
            var dtos = _mapper.Map<List<BadgeDefinitionDto>>(definitions);
            return Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetAdminBadgeDefinitionsQuery");
            return ServerError<List<BadgeDefinitionDto>>();
        }
    }
}
