using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Family.Commands.LinkChild;

public class LinkChildCommandHandler : BaseResponseHandler, ICommandHandler<LinkChildCommand, BaseResponse<LinkedChildResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public LinkChildCommandHandler(
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

    public async Task<BaseResponse<LinkedChildResponse>> Handle(LinkChildCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The acting parent is ALWAYS the authenticated caller (AC-7). Never read a parent id from
            // the request body. If the token has no Id claim, fail closed as unauthorized.
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<LinkedChildResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // FAIL-CLOSED from here on: every rejection returns the SAME generic message with the SAME
            // shape (BadRequest), so a caller cannot tell a non-existent email from an existing-but-
            // ineligible one (AC-5 email-enumeration defence). Specific reasons are logged server-side only.
            var genericFailure = BadRequest<LinkedChildResponse>(_localizer[SharedResourcesKey.CannotLinkChild]);

            var child = await _service.UserManagmentService.FindByEmailAsync(request.ChildEmail);
            if (child is null)
            {
                _logger.LogInfo($"Link-Child rejected: no user for the supplied email (parent {parentId}).");
                return genericFailure;
            }

            // Reject self-link (a parent linking their own account).
            if (child.Id == parentId)
            {
                _logger.LogInfo($"Link-Child rejected: self-link attempt by user {parentId}.");
                return genericFailure;
            }

            // Target must hold the Student role. GetUserRolesAsync returns the role Name verbatim
            // (PascalCase post-RoleSeeder), so compare against Roles.Student.ToString() ordinally-insensitive.
            var childRoles = await _service.UserManagmentService.GetUserRolesAsync(child);
            var isStudent = childRoles.Any(r => string.Equals(r, Roles.Student.ToString(), StringComparison.OrdinalIgnoreCase));
            if (!isStudent)
            {
                _logger.LogInfo($"Link-Child rejected: target user {child.Id} is not a Student (parent {parentId}).");
                return genericFailure;
            }

            // Already linked to THIS parent → idempotent success (AC-6): no duplicate, return the summary.
            if (await _service.LinkParentStudentService.IsLinkedAsync(parentId.Value, child.Id, cancellationToken))
            {
                _logger.LogInfo($"Link-Child no-op: child {child.Id} already linked to parent {parentId}.");
                return Success(_mapper.Map<LinkedChildResponse>(child));
            }

            // Cross-family guard (AC-7): a parent may only link a child they CREATED, or a child that is
            // not yet linked to ANY family. If the child already belongs to another family, fail closed —
            // and with the SAME generic message so existence/ownership is not disclosed.
            var createdByThisParent = child.CreatedBy == parentId;
            if (!createdByThisParent)
            {
                var alreadyHasParent = await _service.LinkParentStudentService.StudentHasAnyParentAsync(child.Id, cancellationToken);
                if (alreadyHasParent)
                {
                    _logger.LogWarn($"Link-Child rejected: child {child.Id} belongs to another family; parent {parentId} cannot claim it.");
                    return genericFailure;
                }
            }

            await _service.LinkParentStudentService.LinkAsync(parentId.Value, child.Id, cancellationToken);

            _logger.LogInfo($"Linked child {child.Id} to parent {parentId}.");
            return Success(_mapper.Map<LinkedChildResponse>(child));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in LinkChildCommand");
            return ServerError<LinkedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}
