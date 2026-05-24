using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Family.Commands.UpdateChild;

// Mirrors AddChildCommandHandler (parent resolved from ICurrentUserService, never the body; Identity
// persists immediately with no Unit of Work) and the ListMyChildren/AddChild family-scope pattern:
// the (parentId, childId) ParentStudent link is verified via ILinkParentStudentService.IsLinkedAsync
// BEFORE any write, so a parent can only edit a child in their own family (AC family-scope).
public class UpdateChildCommandHandler : BaseResponseHandler, ICommandHandler<UpdateChildCommand, BaseResponse<UpdatedChildResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public UpdateChildCommandHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<UpdatedChildResponse>> Handle(UpdateChildCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The acting parent is ALWAYS the authenticated caller. Never read a parent id from the
            // request body. If the token has no Id claim, fail closed as unauthorized.
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<UpdatedChildResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Family-scope authorization: the parent may edit a child ONLY if a ParentStudent link
            // exists for (parentId, childId). Same link check AddChild/ListMyChildren rely on. A
            // non-linked child → forbidden (localized), never a silent edit of someone else's child.
            var isLinked = await _service.LinkParentStudentService.IsLinkedAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isLinked)
            {
                _logger.LogWarn($"Update-Child forbidden: parent {parentId} is not linked to child {request.ChildId}.");
                return Forbidden<UpdatedChildResponse>(_localizer[SharedResourcesKey.CannotEditChildNotInFamily]);
            }

            var child = await _service.UserManagmentService.FindByIdAsync(request.ChildId.ToString());
            if (child is null)
            {
                _logger.LogWarn($"Update-Child forbidden: child {request.ChildId} not found (parent {parentId}).");
                return Forbidden<UpdatedChildResponse>(_localizer[SharedResourcesKey.CannotEditChildNotInFamily]);
            }

            // Mutate ONLY the editable fields — no email/login/role/id change (no mass-assignment).
            child.FullName = request.FullName;
            child.Grade = request.Grade;
            child.PreferredLanguage = NormalizeLanguage(request.Language);
            child.Nationality = request.Country;
            child.UpdatedAt = DateTime.UtcNow;
            child.UpdatedBy = parentId;

            // Identity persists immediately (no Unit of Work). On failure return a generic localized
            // message; never echo raw Identity error descriptions to the client.
            var result = await _service.UserManagmentService.UpdateAsync(child);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError(null, $"Update-Child UpdateAsync failed for parent {parentId}, child {child.Id}: {errors}");
                return BadRequest<UpdatedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
            }

            _logger.LogInfo($"Updated child {child.Id} for parent {parentId}.");
            return Success(_mapper.Map<UpdatedChildResponse>(child));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateChildCommand");
            return ServerError<UpdatedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }

    // Maps the request's short language code to the stored culture code. The validator guards the
    // input to {ar,en}; this defensively falls back to the platform default (ar-EG) otherwise.
    private static string NormalizeLanguage(string language) => language?.Trim().ToLowerInvariant() switch
    {
        "en" => "en-US",
        "ar" => "ar-EG",
        _ => "ar-EG",
    };
}
