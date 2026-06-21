namespace Learnexia.Modules.Learning.Application.Features.Calibration.Dtos;

/// <summary>
/// Paged list DTO for a recalibration proposal (BE-5 GET /Proposals).
///
/// <see cref="ReasonLocalized"/> is the localized string for the stored stable <c>ReasonCode</c>
/// — the reason code is never returned raw to the client. Localized at read time in the handler
/// via <c>IStringLocalizer&lt;SharedResources&gt;</c> (P5-07 OQ-6 decision).
/// </summary>
public sealed class CalibrationProposalDto
{
    public int    Id                  { get; init; }
    public int    QuestionId          { get; init; }
    public string QuestionText        { get; init; } = null!;
    public string AuthoredDifficulty  { get; init; } = null!;
    public string ProposedDifficulty  { get; init; } = null!;
    public double EvidencePValue      { get; init; }
    public int    EvidenceSampleSize  { get; init; }
    public string ReasonCode          { get; init; } = null!;
    public string ReasonLocalized     { get; init; } = null!;
    public string Status              { get; init; } = null!;
    public DateTime? ResolvedAtUtc    { get; init; }
    public int?   ResolvedByUserId    { get; init; }
    public DateTime CreatedAt         { get; init; }
}

/// <summary>
/// Paged list DTO for a flagged question (BE-5 GET /Flagged).
///
/// <see cref="FlagReasonLocalized"/> is the localized string for the stored stable
/// <c>FlagReason</c> code — the code is never returned raw to the client.
/// </summary>
public sealed class FlaggedQuestionDto
{
    public int     QuestionId          { get; init; }
    public string  QuestionText        { get; init; } = null!;
    public int     LessonId            { get; init; }
    public string  GeneratedBy         { get; init; } = null!;
    public double? EvidencePValue      { get; init; }
    public int?    EvidenceSampleSize  { get; init; }
    public string  FlagReason          { get; init; } = null!;
    public string  FlagReasonLocalized { get; init; } = null!;
    public string  QualityState        { get; init; } = null!;
}
