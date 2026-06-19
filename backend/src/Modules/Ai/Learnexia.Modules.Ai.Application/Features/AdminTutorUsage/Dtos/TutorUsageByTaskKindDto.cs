namespace Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Dtos;

/// <summary>Per-task-kind usage breakdown for <see cref="TutorUsageDto"/>.</summary>
public record TutorUsageByTaskKindDto(
    string TaskKind,
    int Calls,
    int TotalTokens,
    decimal TotalEstimatedCostUsd);
