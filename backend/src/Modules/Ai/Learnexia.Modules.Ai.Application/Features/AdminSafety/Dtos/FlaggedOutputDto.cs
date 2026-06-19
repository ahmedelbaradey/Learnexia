namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;

/// <summary>
/// PII-light row in the flagged-outputs drill-in list (P7-11 AC2).
///
/// No raw prompt text, no response text, no student name/email (PII-light design per P3-02 Q5/Q6).
/// <see cref="ContentRef"/> is the <c>SafetyEvent.Id</c> — an opaque numeric reference.
/// </summary>
public record FlaggedOutputDto(
    int ContentRef,
    string TaskKind,
    string ActionTaken,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> FailedChecks,
    string ModelId,
    int? StudentId,
    DateTime OccurredAtUtc);
