using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Family.Commands.AddChild;

// Mirrors RegisterParentCommandHandler (server-assigned role, Identity-hashed password, error
// shaping that never echoes raw Identity errors) and LinkChildCommandHandler (parent resolved
// from ICurrentUserService, never the request body; auto-link via ILinkParentStudentService).
public class AddChildCommandHandler : BaseResponseHandler, ICommandHandler<AddChildCommand, BaseResponse<AddedChildResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;
    private readonly IPublisher _publisher;

    public AddChildCommandHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger,
        IPublisher publisher)
    {
        _service = service;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _localizer = localizer;
        _logger = logger;
        _publisher = publisher;
    }

    public async Task<BaseResponse<AddedChildResponse>> Handle(AddChildCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // The acting parent is ALWAYS the authenticated caller (AC-4). Never read a parent id
            // from the request body. If the token has no Id claim, fail closed as unauthorized.
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<AddedChildResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Race-safe backstop. The validator does not do an existence lookup (handler owns
            // uniqueness), so this is the single source of the duplicate-email rejection (AC-7).
            // UserName == Email for the child, mirroring parent registration.
            var existingUser = await _service.UserManagmentService.FindByEmailAsync(request.Email);
            if (existingUser != null)
                return BadRequest<AddedChildResponse>(_localizer[SharedResourcesKey.ProfileDuplicateEmail]);

            var child = new User
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                PreferredLanguage = NormalizeLanguage(request.Language),
                Nationality = request.Country,
                Grade = request.Grade,
                RegistrationIsCompleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = parentId,
            };

            // Identity hashes the parent-set password and persists the child immediately (no Unit
            // of Work). On failure return a generic localized message; never echo raw Identity
            // error descriptions to the client (they can carry password/identity internals).
            var result = await _service.UserManagmentService.CreateAsync(child, request.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError(null, $"Add-Child CreateAsync failed for parent {parentId}: {errors}");
                return BadRequest<AddedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
            }

            // Role is server-assigned — always Student, never client-supplied (AC-4 / no privilege
            // escalation). Seeded as PascalCase by RoleSeeder. Fail closed if assignment fails:
            // there is no Unit of Work, so CreateAsync has already committed the child. A roleless
            // minor account is an integrity hole, so delete the just-created child and reject
            // (generic message; raw Identity detail logged server-side only).
            var roleResult = await _service.UserManagmentService.AddToRoleAsync(child, Roles.Student.ToString());
            if (!roleResult.Succeeded)
            {
                var roleErrors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError(null, $"Add-Child AddToRoleAsync failed for parent {parentId}, child {child.Id}: {roleErrors}");

                // Compensating delete to avoid an orphaned roleless child (no UoW to roll back on).
                var deleteResult = await _service.UserManagmentService.DeleteAsync(child);
                if (!deleteResult.Succeeded)
                {
                    var deleteErrors = string.Join(", ", deleteResult.Errors.Select(e => e.Description));
                    _logger.LogError(null, $"Add-Child compensating DeleteAsync failed for parent {parentId}, child {child.Id}: {deleteErrors}");
                }

                return ServerError<AddedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
            }

            // Auto-link (AC-5). Called AFTER CreateAsync commits so the FK target exists; LinkAsync
            // commits the ParentStudent row immediately and only links to the JWT-resolved parentId.
            await _service.LinkParentStudentService.LinkAsync(parentId.Value, child.Id, cancellationToken);

            // Best-effort cross-module fan-out (parity with RegisterParent/AddUser); a publish
            // failure must not affect the committed child or the success response.
            await PublishUserRegisteredEventAsync(child, cancellationToken);

            _logger.LogInfo($"Added child {child.Id} for parent {parentId}.");
            return Success(_mapper.Map<AddedChildResponse>(child));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddChildCommand");
            return ServerError<AddedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
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

    private async Task PublishUserRegisteredEventAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var integrationEvent = new UserRegisteredIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: DateTime.UtcNow,
                UserId: user.Id,
                UserName: user.UserName ?? string.Empty);

            await _publisher.Publish(integrationEvent, cancellationToken);

            _logger.LogInfo($"Published UserRegisteredIntegrationEvent for user {user.Id}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to publish UserRegisteredIntegrationEvent for user {user.Id}.");
        }
    }
}
