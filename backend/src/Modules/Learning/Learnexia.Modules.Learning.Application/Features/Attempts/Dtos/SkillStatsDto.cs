namespace Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;

/// <summary>
/// DTO returned by GetSkillStatsQuery — aggregated per-student, per-skill statistics.
/// Zero-data case returns all numeric fields as 0.0 / 0 (never 404 or 500).
/// SECURITY: CorrectAnswer is intentionally absent; this DTO must NEVER contain it.
/// </summary>
public class SkillStatsDto
{
    public int SkillId { get; set; }
    public int StudentId { get; set; }
    public int TotalAnswers { get; set; }
    public int CorrectAnswers { get; set; }
    public double AccuracyPercentage { get; set; }
    public double AvgTimeSpentSeconds { get; set; }
    public double HintUsageRate { get; set; }   // hints used / total answers expressed as 0..100 percentage
}
