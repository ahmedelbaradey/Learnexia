using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Queries.GetStudentAttempts;

/// <summary>
/// Handles GetStudentAttemptsQuery.
///
/// Returns all attempts for the requested student, ordered by StartedAt descending.
/// Authorization scope (deny-by-default), either:
///   (a) the owning student — the route-supplied StudentId matches the JWT-resolved UserId (IDOR guard); or
///   (b) a parent linked to that student — verified via the <see cref="IParentChildQuery"/>
///       Shared.Contracts seam (no Parent-module project reference; mirrors the Notifications
///       re-engagement preference handlers).
/// Everyone else gets a generic 403 Forbidden (not 401 — the FE transport treats 401 as a
/// token-refresh trigger). Anti-enumeration: the same generic 403 is returned whether the
/// student id is unknown or simply not linked to the caller. Admin scoping remains out of scope.
///
/// Empty list is a valid response (200 + EmptyCollection) — do NOT return 404.
/// CorrectAnswer is NEVER in AttemptListItemDto.
/// </summary>
public class GetStudentAttemptsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetStudentAttemptsQuery, BaseResponse<List<AttemptListItemDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetStudentAttemptsQueryHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
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
            // Unauthenticated (no resolvable user id) → 401; authenticated-but-not-authorized → 403
            // (401 would make the FE transport attempt a token refresh).
            var currentUserId = _currentUser.UserId;
            if (currentUserId is null)
                return Unauthorized<List<AttemptListItemDto>>(_localizer[SharedResourcesKey.Unauthorized]);

            if (request.StudentId != currentUserId.Value)
            {
                // Cross-module seam (Shared.Contracts) — is the caller a parent linked to this student?
                // Anti-enumeration: same generic 403 whether the student id is unknown or not linked.
                var isLinkedParent = await _parentChildQuery.IsParentOfChildAsync(
                    currentUserId.Value, request.StudentId, cancellationToken);
                if (!isLinkedParent)
                    return Forbidden<List<AttemptListItemDto>>(_localizer[SharedResourcesKey.AttemptsAccessForbidden]);
            }

            // Step 3 — Query: fetch all attempts for this student, most recent first.
            var attempts = await _repository.Learning
                .GetByCondition<Attempt>(a => a.StudentId == request.StudentId, trackChanges: false)
                .OrderByDescending(a => a.StartedAt)
                .ToListAsync(cancellationToken);

            // Step 4 — Empty list is valid; return EmptyCollection (200) rather than 404.
            if (!attempts.Any())
                return EmptyCollection(new List<AttemptListItemDto>());

            // Step 5 — Map via AutoMapper (Status enum → string conversion is handled in QuizProfile).
            var dtos = _mapper.Map<List<AttemptListItemDto>>(attempts);

            // Step 6 — Return.
            var result = Success(dtos);
            result.Message = _localizer[SharedResourcesKey.AttemptsRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // Log server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in GetStudentAttemptsQuery");
            return ServerError<List<AttemptListItemDto>>();
        }
    }
}
