using System.Text.Json;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="IStudentRecommendationsQuery"/> by reading the latest persisted
/// <c>StudentRecommendation</c> row from the Learning DbContext (P5-09-BE-5).
///
/// <para>Cross-module isolation: Parent and Ai modules inject
/// <see cref="IStudentRecommendationsQuery"/> from <c>Shared.Contracts</c>; they never
/// reference this adapter or any Learning project directly.</para>
///
/// <para>Sentinel contract: never null, never throws. An empty list is returned when:
/// <list type="bullet">
///   <item>No recommendation row exists for the student (cold-start / first-week).</item>
///   <item>The stored JSON fails to deserialise (corrupted row — degrade gracefully).</item>
/// </list>
/// </para>
///
/// <para>Registered as Scoped in <c>AddLearningInfrastructure</c>.</para>
/// </summary>
public sealed class StudentRecommendationsQueryAdapter : IStudentRecommendationsQuery
{
    private readonly ILearningRepository _repository;
    private readonly ILoggerManager      _logger;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public StudentRecommendationsQueryAdapter(
        ILearningRepository repository,
        ILoggerManager logger)
    {
        _repository = repository;
        _logger     = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecommendationItem>> GetLatestAsync(
        int studentId,
        CancellationToken ct = default)
    {
        try
        {
            var row = await _repository.GetLatestRecommendationAsync(studentId, ct);

            if (row is null)
                return Array.Empty<RecommendationItem>();

            if (string.IsNullOrWhiteSpace(row.ItemsJson) || row.ItemsJson == "[]")
                return Array.Empty<RecommendationItem>();

            var items = JsonSerializer.Deserialize<RecommendationItem[]>(row.ItemsJson, JsonOpts);
            return items ?? Array.Empty<RecommendationItem>();
        }
        catch (Exception ex)
        {
            // Sentinel: never throws to callers — log and return empty (mirrors IStudentAllSubjectsWeakAreasQuery).
            _logger.LogError(ex,
                $"IStudentRecommendationsQuery.GetLatestAsync failed for studentId={studentId}. Returning empty list.");
            return Array.Empty<RecommendationItem>();
        }
    }
}
