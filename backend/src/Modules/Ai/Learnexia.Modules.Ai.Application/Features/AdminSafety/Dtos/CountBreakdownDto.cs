namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;

/// <summary>
/// A single facet bucket: label + count used in all breakdown lists of <see cref="SafetySignalSummaryDto"/>.
/// </summary>
public record CountBreakdownDto(string Label, int Count);
