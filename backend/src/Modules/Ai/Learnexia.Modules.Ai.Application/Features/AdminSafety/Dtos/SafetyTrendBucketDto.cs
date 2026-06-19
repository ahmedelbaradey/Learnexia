namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;

/// <summary>
/// One time bucket in the safety trend response (P7-11 AC3).
/// Date is the start of the day (UTC, date-only, time truncated to midnight).
/// </summary>
public record SafetyTrendBucketDto(
    DateTime BucketDate,
    int TotalCount,
    int BlockedCount,
    int RegeneratedCount,
    int FallbackReturnedCount);
