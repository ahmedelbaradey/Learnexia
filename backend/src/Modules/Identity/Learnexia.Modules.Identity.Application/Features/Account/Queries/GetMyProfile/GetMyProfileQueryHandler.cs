using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Abstractions.Storage;
using Learnexia.Shared.Kernel.Storage;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Account.Queries.GetMyProfile;

// Mirrors GetMeQueryHandler: self-scoped (fail-closed) read keyed on the JWT-resolved caller id,
// never a body/query param. Returns only the account-profile contract — no password/token/audit fields.
public class GetMyProfileQueryHandler : BaseResponseHandler, IQueryHandler<GetMyProfileQuery, BaseResponse<AccountProfileResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStorageService _storageService;
    private readonly MinIOConfiguration _config;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public GetMyProfileQueryHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IStorageService storageService,
        IOptions<MinIOConfiguration> config,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _storageService = storageService;
        _config = config.Value;
        _mapper = mapper;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<AccountProfileResponse>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Self-scope (fail-closed): keyed on the authenticated caller's id from the JWT — never a
            // body/query param. No id means no token → Unauthorized.
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<AccountProfileResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var user = await _service.UserManagmentService.FindByIdAsync(userId.Value.ToString());
            if (user is null)
                return Unauthorized<AccountProfileResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var profile = _mapper.Map<AccountProfileResponse>(user);

            // AvatarUrl stores the storage OBJECT KEY; turn it into a freshly presigned GET URL (pure
            // compute, no network). Null/empty key → null URL.
            profile.AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarUrl)
                ? null
                : await _storageService.GetPreviewUrlAsync(user.AvatarUrl, _config.DefaultBucket, _config.DefaultUrlExpiryMinutes, cancellationToken);

            return Success(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetMyProfileQuery");
            return ServerError<AccountProfileResponse>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
