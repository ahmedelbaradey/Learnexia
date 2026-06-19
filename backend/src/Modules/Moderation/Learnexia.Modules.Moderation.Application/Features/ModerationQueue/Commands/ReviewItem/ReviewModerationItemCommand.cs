using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;
using Learnexia.Modules.Moderation.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Commands.ReviewItem;

/// <summary>
/// Review a moderation queue item: transition it to Approved, Rejected, or Flagged.
///
/// <para>Validation rules (enforced by <see cref="ReviewModerationItemCommandValidator"/>):</para>
/// <list type="bullet">
///   <item><see cref="Decision"/> must be a valid <see cref="ModerationStatus"/> that is NOT
///     <see cref="ModerationStatus.Pending"/> (Pending is the initial state, not a review decision).</item>
///   <item><see cref="Reason"/> is REQUIRED when <see cref="Decision"/> is
///     <see cref="ModerationStatus.Rejected"/>.</item>
///   <item>Only items in <see cref="ModerationStatus.Pending"/> or <see cref="ModerationStatus.Flagged"/>
///     may be re-reviewed; terminal items (<see cref="ModerationStatus.Approved"/> /
///     <see cref="ModerationStatus.Rejected"/>) return a validation failure.</item>
/// </list>
/// </summary>
public record ReviewModerationItemCommand(
    int Id,
    ModerationStatus Decision,
    string? Reason) : ICommand<BaseResponse<ModerationItemDto>>;
