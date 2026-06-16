using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Queries.GetStudentAttempts;

/// <summary>
/// Handles GetStudentAttemptsQuery.
///
/// Returns all attempts for the requested student, ordered by StartedAt descending.
/// Authorization scope (deny-by-default), either:
///   (a) the owning student — the route-supplied StudentId matches the JWT-resolved UserId; or
///   (b) a parent linked to that student — verified via <see cref="IParentChildQuery"/>
///       Shared.Contracts seam.
/// Everyone else gets a generic 403 Forbidden.
///
/// Empty list is a valid response (200 + EmptyCollection) — do NOT return 404.
/// CorrectAnswer is NEVER in AttemptListItemDto.
///
/// Option C (no EF in Application): all DB access delegated to IAttemptQueryService.
/// </summary>
public class GetStudentAttemptsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetStudentAttemptsQuery, BaseResponse<List<AttemptListItemDto>>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetStudentAttemptsQueryHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service          = service;
        _currentUser      = currentUser;
        _parentChildQuery = parentChildQuery;
        _mapper           = mapper;
        _logger           = logger;
        _localizer        = localizer;
    }

    public async Task<BaseResponse<List<AttemptListItemDto>>> Handle(
        GetStudentAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — Inline validation: queries are NOT auto-validated by ValidationBehavior.
            if (request.StudentId <= 0)
                return BadRequest<List<AttemptListItemDto>>(_localizer[SharedResourcesKey.StudentIdMustBePositive]);

            // Step 2 — Authorization scope (deny-by-default): owning student OR linked parent.
            var currentUserId = _currentUser.UserId;
            if (currentUserId is null)
                return Unauthorized<List<AttemptListItemDto>>(_localizer[SharedResourcesKey.Unauthorized]);

            if (request.StudentId != currentUserId.Value)
            {
                // Cross-module seam (Shared.Contracts) — is the caller a parent linked to this student?
                var isLinkedParent = await _parentChildQuery.IsParentOfChildAsync(
                    currentUserId.Value, request.StudentId, cancellationToken);
                if (!isLinkedParent)
                    return Forbidden<List<AttemptListItemDto>>(_localizer[SharedResourcesKey.AttemptsAccessForbidden]);
            }

            // Step 3 — Delegate to service (all EF inside Infrastructure).
            var attempts = await _service.AttemptQueryService
                .GetAttemptsForStudentAsync(request.StudentId, cancellationToken);

            // Step 4 — Empty list is valid; return EmptyCollection (200) rather than 404.
            if (!attempts.Any())
                return EmptyCollection(new List<AttemptListItemDto>());

            // Step 5 — Map via AutoMapper.
            var dtos = _mapper.Map<List<AttemptListItemDto>>(attempts);

            // Step 6 — Return.
            var result = Success(dtos);
            result.Message = _localizer[SharedResourcesKey.AttemptsRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetStudentAttemptsQuery");
            return ServerError<List<AttemptListItemDto>>();
        }
    }
}
