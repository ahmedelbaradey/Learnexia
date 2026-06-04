using AutoMapper;
using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.AddChild;

// Relocated from Identity. The Identity-side account creation (duplicate-email check, UserManager.CreateAsync,
// AddToRoleAsync Student, compensating delete, UserRegisteredIntegrationEvent publish) is encapsulated behind
// the IChildAccountService seam — this handler owns only the family-link concern (the parent schema link row).
// The acting parent is ALWAYS the JWT-resolved caller; never read from the request body.
public class AddChildCommandHandler : BaseResponseHandler, ICommandHandler<AddChildCommand, BaseResponse<AddedChildResponse>>
{
    private readonly IChildAccountService _childAccountService;
    private readonly ILinkParentStudentService _linkService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public AddChildCommandHandler(
        IChildAccountService childAccountService,
        ILinkParentStudentService linkService,
        ICurrentUserService currentUserService,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _childAccountService = childAccountService;
        _linkService = linkService;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<AddedChildResponse>> Handle(AddChildCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<AddedChildResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var createResult = await _childAccountService.CreateChildAsync(
                new CreateChildRequest(
                    Email: request.Email,
                    Password: request.Password,
                    FullName: request.FullName,
                    Language: request.Language,
                    Country: request.Country,
                    Grade: request.Grade,
                    ActingParentId: parentId.Value,
                    LearningLanguage: request.LearningLanguage),
                cancellationToken);

            if (!createResult.Succeeded || createResult.Profile is null)
            {
                _logger.LogError(null, $"Add-Child failed for parent {parentId}: {createResult.ErrorCode}.");
                return createResult.ErrorCode switch
                {
                    ChildAccountError.DuplicateEmail => BadRequest<AddedChildResponse>(_localizer[SharedResourcesKey.ProfileDuplicateEmail]),
                    _ => ServerError<AddedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]),
                };
            }

            // Auto-link (commits immediately in the parent schema). The child account already exists.
            await _linkService.LinkAsync(parentId.Value, createResult.ChildUserId, cancellationToken);

            _logger.LogInfo($"Added child {createResult.Profile.Id} for parent {parentId}.");
            return Success(_mapper.Map<AddedChildResponse>(createResult.Profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddChildCommand");
            return ServerError<AddedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}
