namespace Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Dtos;

/// <summary>Per-day usage bucket for the trend sparkline in <see cref="TutorUsageDto"/>.</summary>
public record TutorUsageTrendBucketDto(
    DateTime Date,
    int Calls,
    decimal TotalEstimatedCostUsd);
