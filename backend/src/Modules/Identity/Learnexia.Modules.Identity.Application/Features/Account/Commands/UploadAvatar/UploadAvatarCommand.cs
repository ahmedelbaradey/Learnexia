using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Http;

namespace Learnexia.Modules.Identity.Application.Features.Account.Commands.UploadAvatar;

/// <summary>
/// Uploads the authenticated caller's avatar (P1-12 BE-4). Carries the multipart IFormFile. The file
/// is NOT FluentValidation-validated (its bytes can't be expressed as a property rule); type/size and
/// magic-byte checks run in the handler with localized errors. Self-scoped: the user is resolved from
/// the JWT (ICurrentUserService), never from the request — no id is accepted here.
/// </summary>
public record UploadAvatarCommand(IFormFile File) : ICommand<BaseResponse<AvatarUploadResponse>>;
