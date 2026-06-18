using Learnexia.Modules.Moderation.Domain.Enums;

namespace Learnexia.Modules.Moderation.Api.Controllers;

/// <summary>
/// Request body for <c>POST api/Admin/Moderation/{id}/Review</c>.
/// Separated from <see cref="Application.Features.ModerationQueue.Commands.ReviewItem.ReviewModerationItemCommand"/>
/// so the Id comes from the route (not the body) — IDOR guard.
/// </summary>
public record ReviewModerationItemRequest(
    /// <summary>Review decision: Approved, Rejected, or Flagged.</summary>
    ModerationStatus Decision,
    /// <summary>Free-text reason (required when Decision=Rejected; max 2000 chars).</summary>
    string? Reason);
