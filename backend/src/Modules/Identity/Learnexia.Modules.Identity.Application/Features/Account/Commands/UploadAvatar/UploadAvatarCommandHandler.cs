using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Account.Avatars;
using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Abstractions.Storage;
using Learnexia.Shared.Kernel.Storage;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Account.Commands.UploadAvatar;

// Self-scoped (fail-closed): caller resolved from the JWT, never the request. Validates content-type,
// size, and the actual magic-byte signature before touching storage. On success uploads to the avatars
// bucket under a GUID OBJECT KEY and stores THAT KEY (not a URL) in User.AvatarUrl; the read path
// (profile/Me) turns the key into a freshly presigned GET URL on demand. The upload response returns
// the presigned PreviewUrl so the FE can render immediately. No Unit of Work — UpdateAsync commits one row.
public class UploadAvatarCommandHandler : BaseResponseHandler, ICommandHandler<UploadAvatarCommand, BaseResponse<AvatarUploadResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStorageService _storageService;
    private readonly MinIOConfiguration _config;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public UploadAvatarCommandHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IStorageService storageService,
        IOptions<MinIOConfiguration> config,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _storageService = storageService;
        _config = config.Value;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<AvatarUploadResponse>> Handle(UploadAvatarCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<AvatarUploadResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var file = request.File;

            // 1) Presence/size. Invalid-file rejections return 422 (UnprocessableEntity) to match the
            //    controller's advertised contract.
            if (file is null || file.Length == 0)
                return UnprocessableEntity<AvatarUploadResponse>(_localizer[SharedResourcesKey.AvatarFileRequired]);

            if (file.Length > _config.MaxFileSize)
                return UnprocessableEntity<AvatarUploadResponse>(_localizer[SharedResourcesKey.AvatarFileTooLarge]);

            // 2) Declared content-type must be an allowed image MIME type.
            if (!AvatarImageValidator.IsAllowedContentType(file.ContentType))
                return UnprocessableEntity<AvatarUploadResponse>(_localizer[SharedResourcesKey.AvatarFileInvalidType]);

            // 3) Magic-byte signature must ALSO resolve to a supported image (defends against a spoofed
            //    content-type / extension on non-image content).
            var detected = await AvatarImageValidator.DetectByMagicBytesAsync(file, cancellationToken);
            if (detected is null)
                return UnprocessableEntity<AvatarUploadResponse>(_localizer[SharedResourcesKey.AvatarFileInvalidType]);

            var user = await _service.UserManagmentService.FindByIdAsync(userId.Value.ToString());
            if (user is null)
                return Unauthorized<AvatarUploadResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Upload under a GUID key (extension derived from the verified format, not the user's name).
            // Store the object with the TRUE magic-byte-detected content-type, NOT the client-declared MIME
            // (defends against a spoofed Content-Type being served back later). Stream-based signature keeps
            // the storage seam (Shared.Kernel) free of an ASP.NET IFormFile dependency.
            var objectKey = $"{Guid.NewGuid():N}{AvatarImageValidator.GetExtension(detected.Value)}";
            var detectedContentType = AvatarImageValidator.GetContentType(detected.Value);
            await using var uploadStream = file.OpenReadStream();
            var result = await _storageService.UploadFileAsync(uploadStream, objectKey, detectedContentType, file.Length, _config.DefaultBucket, cancellationToken);
            if (!result.Success)
            {
                _logger.LogError(null, $"Avatar upload failed for user {userId}: {result.ErrorMessage}");
                return ServerError<AvatarUploadResponse>(_localizer[SharedResourcesKey.AvatarUploadFailed]);
            }

            // Store the OBJECT KEY (result.FilePath), not a URL. A previous object (if any) is left in
            // place — object DELETE is not part of the storage contract for the MVP (orphaned object is
            // an accepted trade-off; follow-up: add delete + a sweep). The read path presigns the key.
            user.AvatarUrl = result.FilePath;
            var update = await _service.UserManagmentService.UpdateAsync(user);
            if (!update.Succeeded)
            {
                var errors = string.Join(", ", update.Errors.Select(e => e.Description));
                _logger.LogError(null, $"Avatar UpdateAsync failed for user {userId}: {errors}");
                return BadRequest<AvatarUploadResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
            }

            _logger.LogInfo($"Avatar updated for user {userId}.");
            var response = Success(new AvatarUploadResponse { AvatarUrl = result.PreviewUrl });
            response.Message = _localizer[SharedResourcesKey.AvatarUploadedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UploadAvatarCommand");
            return ServerError<AvatarUploadResponse>(_localizer[SharedResourcesKey.AvatarUploadFailed]);
        }
    }
}
