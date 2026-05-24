using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Account.Commands.RemoveAvatar;

/// <summary>
/// Removes the authenticated caller's avatar (P1-12 BE-4): clears User.AvatarUrl (the stored object
/// key). The backing object is intentionally LEFT in storage — object DELETE is not part of the MVP
/// storage contract (orphan accepted; follow-up: delete + sweep). Self-scoped — the user is resolved
/// from the JWT, never the request body. Returns the (now-null) avatar URL so the FE re-renders the placeholder.
/// </summary>
public record RemoveAvatarCommand : ICommand<BaseResponse<AvatarUploadResponse>>;
