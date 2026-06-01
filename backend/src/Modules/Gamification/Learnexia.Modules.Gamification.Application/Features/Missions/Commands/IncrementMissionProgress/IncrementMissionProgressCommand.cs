using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Missions.Commands.IncrementMissionProgress;

/// <summary>
/// Increments progress on all active <c>StudentMission</c> rows for a student that match
/// <see cref="TargetType"/>. Sent by the three notification handlers
/// (<c>LessonCompletedMissionHandler</c>, <c>AnswerSubmittedMissionHandler</c>,
/// <c>StreakAdvancedMissionHandler</c>) after an appropriate learning event fires.
///
/// Idempotency: two-layer defense —
///   (a) <see cref="Abstractions.IGamificationRepository.HasMissionProgressLogAsync"/> pre-check
///       per mission row,
///   (b) DB unique index <c>UX_MissionProgressLogs_StudentMissionId_OriginEventId</c> catches races.
///
/// Inline completion logic: when <c>Progress &gt;= Target</c> is crossed inside this handler,
/// the mission is completed in the SAME UoW transaction (no nested command / nested transaction).
/// This avoids nested-transaction issues with Npgsql (which does not support nested
/// <c>BeginTransactionAsync</c> without savepoints on a single DbContext).
///
/// Runs through <c>UnitOfWorkBehavior</c> for an atomic write of
/// <c>MissionProgressLog</c> rows + <c>StudentMission.Progress/Status</c> mutations +
/// optional <c>XpAward</c> insert + domain-event dispatch after commit.
/// </summary>
public sealed record IncrementMissionProgressCommand(
    int StudentId,
    MissionTargetType TargetType,
    int IncrementBy,
    Guid OriginEventId,
    string OriginEventType,
    DateTime OccurredAtUtc) : ICommand<BaseResponse<Unit>>;
