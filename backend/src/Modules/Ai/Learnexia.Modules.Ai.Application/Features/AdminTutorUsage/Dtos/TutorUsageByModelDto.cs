namespace Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Dtos;

/// <summary>Per-model usage breakdown for <see cref="TutorUsageDto"/>.</summary>
public record TutorUsageByModelDto(
    string ModelId,
    int Calls,
    int TotalTokens,
    decimal TotalEstimatedCostUsd);
