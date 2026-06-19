namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;

/// <summary>
/// Aggregate safety-signal summary over a date range (P7-11 AC1).
///
/// <para>Subject/language breakdown is N/A in this slice — <c>SafetyEvent</c> has no
/// subject or language column (P7-11 brief OQ-1). Those facets remain empty until a
/// follow-up schema change adds the columns.</para>
/// </summary>
public record SafetySignalSummaryDto(
    DateTime From,
    DateTime To,
    int TotalEvents,
    int BlockedCount,
    double BlockedRate,
    int RegeneratedCount,
    double RegeneratedRate,
    int FallbackReturnedCount,
    double FallbackReturnedRate,
    IReadOnlyList<CountBreakdownDto> BreakdownByAction,
    IReadOnlyList<CountBreakdownDto> BreakdownByReasonCode,
    IReadOnlyList<CountBreakdownDto> BreakdownByModelId,
    IReadOnlyList<CountBreakdownDto> BreakdownByTaskKind);
