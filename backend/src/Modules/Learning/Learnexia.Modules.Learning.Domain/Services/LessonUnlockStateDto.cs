using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Per-lesson unlock state computed by <see cref="LearningPathEngine"/>.
/// <see cref="MissingPrerequisites"/> is populated only when <see cref="State"/> is
/// <see cref="NodeState.Locked"/>; it is an empty list for <see cref="NodeState.Available"/>
/// and <see cref="NodeState.Completed"/>.
/// </summary>
public sealed record LessonUnlockStateDto(
    int LessonId,
    NodeState State,
    IReadOnlyList<MissingPrerequisiteDto> MissingPrerequisites);
