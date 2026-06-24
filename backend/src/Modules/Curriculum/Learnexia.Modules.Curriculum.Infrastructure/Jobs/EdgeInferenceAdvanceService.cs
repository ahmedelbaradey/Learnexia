using System.Text.Json;
using System.Text.RegularExpressions;
using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Configuration;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Curriculum.Infrastructure.Jobs;

/// <summary>
/// Hosted background poller that claims <c>Done</c>/<c>Failed</c> <c>infer_edges</c>
/// <see cref="PipelineJob"/> rows and writes <c>KGSuggestion{Pending}</c> entries
/// from the Python worker's <c>ResultJson</c> (BL-03-BE-3).
///
/// <para><strong>Claim mechanics (FOR UPDATE SKIP LOCKED):</strong>
/// Each poll cycle opens a transaction and claims up to 10 unarchived Done/Failed infer_edges jobs
/// atomically, mirroring <see cref="IngestJobAdvanceService"/>.</para>
///
/// <para><strong>On Done:</strong>
/// Deserializes and validates <c>ResultJson</c> (untrusted Python output, DEFENSIVE).
/// Re-clamps Strength and Confidence to [0,1] server-side (never trust the Python value —
/// mirrors <c>IngestJobAdvanceService.ClampConfidence</c>).
/// Resolves <c>source_skill_key</c> + <c>target_skill_key</c> → <c>KnowledgeNode.Id</c>
/// via <see cref="IKnowledgeNodeReader"/> (fail-soft: drops + logs unresolved SkillKeys).
/// Writes idempotent <c>KGSuggestion{Pending}</c> on <c>(SourceNodeId, TargetNodeId, RelationshipType)</c>:
/// query-then-insert (mirrors <c>EdgeDuplicateExistsAsync</c> pattern — no unique index migration needed).
/// Flips the job to <c>'Archived'</c>.</para>
///
/// <para><strong>On Failed:</strong>
/// Increments <c>RetryCount</c>. If retries remain → fresh Pending infer_edges job; if exhausted →
/// <c>PermanentlyFailed</c>. Policy identical to <see cref="IngestJobAdvanceService"/>.</para>
///
/// <para><strong>No-stranding guarantee:</strong>
/// ANY exception caught during processing triggers <see cref="TerminateJobOnExceptionAsync"/>,
/// which writes <c>PermanentlyFailed</c> in a fresh transaction.</para>
///
/// <para><strong>Decision E invariant:</strong>
/// This service NEVER writes <c>KnowledgeEdge</c>. Only suggestions are written. The only path
/// to <c>KnowledgeEdge</c> is admin <see cref="ApproveKGSuggestionCommand"/> via
/// <see cref="IKnowledgeEdgeWriter"/>.</para>
///
/// <para><strong>Frozen ResultJson contract (BL-05 divergence guard):</strong>
/// <code>
/// { "inference_model": "&lt;string&gt;",
///   "edges": [ { "source_skill_key": "&lt;string&gt;", "target_skill_key": "&lt;string&gt;",
///                "relationship_type": "Prerequisite" | "Related",
///                "strength": &lt;float 0..1&gt;, "confidence": &lt;float 0..1&gt; } ] }
/// </code>
/// </para>
///
/// <para><strong>Config:</strong> <see cref="InferEdgesJobPollerConfiguration"/> from appsettings
/// <c>CurriculumPipeline</c> section keys <c>InferEdgesPollerIntervalSeconds</c>,
/// <c>InferEdgesMaxRetries</c>.</para>
/// </summary>
public sealed class EdgeInferenceAdvanceService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly InferEdgesJobPollerConfiguration _config;

    private const string StatusArchived          = "Archived";
    private const string StatusPermanentlyFailed = "PermanentlyFailed";

    // ── Column-length constants ────────────────────────────────────────────────────────────────────
    private const int MaxInferenceModelLength = 128;  // KGSuggestionConfig.InferenceModel max 128
    private const int MaxSkillKeyLength       = 256;  // mirrors IngestJobAdvanceService
    private const int MaxErrorMessageLength   = 2048; // PipelineJobConfig
    private const string Ellipsis = "…";

    public EdgeInferenceAdvanceService(
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        IOptions<InferEdgesJobPollerConfiguration> config)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _config       = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInfo(
            $"EdgeInferenceAdvanceService started. interval={_config.InferEdgesPollerIntervalSeconds}s " +
            $"maxRetries={_config.InferEdgesMaxRetries}.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AdvanceCompletedJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EdgeInferenceAdvanceService: unhandled exception in poll loop.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_config.InferEdgesPollerIntervalSeconds),
                stoppingToken);
        }

        _logger.LogInfo("EdgeInferenceAdvanceService stopped.");
    }

    // ── Poll cycle ────────────────────────────────────────────────────────────────────────────────

    private async Task AdvanceCompletedJobsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db         = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();
        var nodeReader = scope.ServiceProvider.GetRequiredService<IKnowledgeNodeReader>();

        // ── Claim batch with FOR UPDATE SKIP LOCKED ───────────────────────────────────────────────
        const string claimCte = """
            WITH claimed AS (
                SELECT "Id", "Status" AS "OriginalStatus"
                FROM curriculum."PipelineJobs"
                WHERE "JobType" = 'infer_edges'
                  AND "Status" IN ('Done', 'Failed')
                ORDER BY "Id"
                LIMIT 10
                FOR UPDATE SKIP LOCKED
            )
            UPDATE curriculum."PipelineJobs" j
            SET "Status" = 'Processing'
            FROM claimed
            WHERE j."Id" = claimed."Id"
            RETURNING j."Id", claimed."OriginalStatus"
            """;

        List<ClaimedJobInfo> claimedInfos;
        await using var claimTx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            claimedInfos = await db.Database
                .SqlQueryRaw<ClaimedJobInfo>(claimCte)
                .ToListAsync(ct);

            await claimTx.CommitAsync(ct);
        }
        catch
        {
            await claimTx.RollbackAsync(ct);
            throw;
        }

        if (claimedInfos.Count == 0)
            return;

        var jobIds = claimedInfos.Select(c => c.Id).ToList();
        var jobs = await db.PipelineJobs
            .Where(j => jobIds.Contains(j.Id) && j.Status == "Processing")
            .ToListAsync(ct);

        var originalStatus = claimedInfos.ToDictionary(c => c.Id, c => c.OriginalStatus);

        foreach (var job in jobs)
        {
            var wasSuccess = originalStatus.TryGetValue(job.Id, out var orig) &&
                             string.Equals(orig, "Done", StringComparison.OrdinalIgnoreCase);
            await ProcessJobAsync(db, job, wasSuccess, nodeReader, ct);
        }
    }

    // ── Per-job processing ────────────────────────────────────────────────────────────────────────

    private async Task ProcessJobAsync(
        CurriculumDbContext db,
        PipelineJob job,
        bool wasSuccessful,
        IKnowledgeNodeReader nodeReader,
        CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            if (wasSuccessful)
            {
                await AdvanceDoneJobAsync(db, job, nodeReader, ct);
                job.Status = StatusArchived;
            }
            else
            {
                await AdvanceFailedJobAsync(db, job, ct);
                // The failed-path sets job.Status internally (Archived or PermanentlyFailed).
                // Ensure it ends at PermanentlyFailed when we reach max retries.
                if (job.Status != StatusPermanentlyFailed)
                    job.Status = StatusArchived; // re-enqueued — archive the current failed job
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"EdgeInferenceAdvanceService: failed to process job id={job.Id}; triggering terminal write.");
            await TerminateJobOnExceptionAsync(db, job, ct);
        }
    }

    private async Task TerminateJobOnExceptionAsync(
        CurriculumDbContext db,
        PipelineJob job,
        CancellationToken ct)
    {
        try
        {
            db.ChangeTracker.Clear();
            db.PipelineJobs.Attach(job);

            await using var termTx = await db.Database.BeginTransactionAsync(ct);

            job.Status = StatusPermanentlyFailed;

            await db.SaveChangesAsync();
            await termTx.CommitAsync(ct);

            _logger.LogInfo(
                $"EdgeInferenceAdvanceService: job id={job.Id} transitioned to PermanentlyFailed " +
                "after processing error.");
        }
        catch (Exception termEx)
        {
            _logger.LogError(termEx,
                $"EdgeInferenceAdvanceService: terminal write also failed for job id={job.Id}. " +
                "The job may remain at 'Processing' — manual intervention required.");
        }
    }

    // ── Done path ─────────────────────────────────────────────────────────────────────────────────

    private async Task AdvanceDoneJobAsync(
        CurriculumDbContext db,
        PipelineJob job,
        IKnowledgeNodeReader nodeReader,
        CancellationToken ct)
    {
        InferenceJobResultParsed result;
        try
        {
            result = DeserializeAndValidateResult(job.ResultJson);
        }
        catch (InferResultValidationException ex)
        {
            _logger.LogError(ex,
                $"EdgeInferenceAdvanceService: ResultJson validation failed for job id={job.Id}. " +
                $"Detail: {ex.Message}");
            // Mark the job as permanently failed — bad ResultJson is not retryable.
            job.Status = StatusPermanentlyFailed;
            return;
        }

        _logger.LogInfo(
            $"EdgeInferenceAdvanceService: job id={job.Id} Done. " +
            $"model={result.InferenceModel} edges={result.Edges.Count}.");

        if (result.Edges.Count == 0)
        {
            _logger.LogInfo(
                $"EdgeInferenceAdvanceService: job id={job.Id} has no edges to process.");
            return;
        }

        // ── Resolve SkillKeys → KnowledgeNode.Ids via the cross-module read seam ────────────────
        var allSkillKeys = result.Edges
            .SelectMany(e => new[] { e.SourceSkillKey, e.TargetSkillKey })
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resolvedNodes = await nodeReader.GetNodesBySkillKeysAsync(allSkillKeys, ct);
        var nodeIdBySkillKey = resolvedNodes
            .Where(n => !string.IsNullOrWhiteSpace(n.SkillKey))
            .ToDictionary(n => n.SkillKey!, n => n.NodeId, StringComparer.OrdinalIgnoreCase);

        // ── Write KGSuggestion{Pending} per edge (idempotent query-then-insert) ────────────────
        int written   = 0;
        int skipped   = 0;
        int duplicate = 0;

        foreach (var edge in result.Edges)
        {
            // Fail-soft: drop edges where either SkillKey could not be resolved.
            if (!nodeIdBySkillKey.TryGetValue(edge.SourceSkillKey, out var sourceNodeId))
            {
                _logger.LogInfo(
                    $"EdgeInferenceAdvanceService: unresolved SourceSkillKey='{edge.SourceSkillKey}' " +
                    $"for job id={job.Id}; dropping edge.");
                skipped++;
                continue;
            }

            if (!nodeIdBySkillKey.TryGetValue(edge.TargetSkillKey, out var targetNodeId))
            {
                _logger.LogInfo(
                    $"EdgeInferenceAdvanceService: unresolved TargetSkillKey='{edge.TargetSkillKey}' " +
                    $"for job id={job.Id}; dropping edge.");
                skipped++;
                continue;
            }

            // Map relationship type string → CurriculumRelationshipType.
            var relType = MapRelationshipType(edge.RelationshipType);

            // Idempotency: query-then-insert on (SourceNodeId, TargetNodeId, RelationshipType).
            // Mirrors AddKnowledgeEdgeCommandHandler.EdgeDuplicateExistsAsync — no unique index migration.
            // Treat Pending, Approved, OR Rejected existing suggestions as duplicates (no re-add after reject;
            // re-build after rejection requires a new BuildKnowledgeGraphSuggestionsCommand).
            var exists = await db.KGSuggestions
                .AnyAsync(s =>
                    s.SourceNodeId       == sourceNodeId  &&
                    s.TargetNodeId       == targetNodeId  &&
                    s.RelationshipType   == relType,
                    ct);

            if (exists)
            {
                duplicate++;
                continue;
            }

            var safeModel = TruncateWithEllipsis(result.InferenceModel, MaxInferenceModelLength);

            var suggestion = new KGSuggestion
            {
                SourceNodeId              = sourceNodeId,
                TargetNodeId              = targetNodeId,
                RelationshipType          = relType,
                // Server-side re-clamp: NEVER trust the Python value (mirrors ClampConfidence).
                Strength                  = ClampValue(edge.Strength),
                InferenceModel            = safeModel,
                Status                    = KGSuggestionStatus.Pending,
                SourceCurriculumDocumentId = null,  // no direct doc linkage from infer_edges payload
            };

            db.KGSuggestions.Add(suggestion);
            await db.SaveChangesAsync(ct);
            written++;
        }

        _logger.LogInfo(
            $"EdgeInferenceAdvanceService: job id={job.Id} → " +
            $"written={written} duplicate={duplicate} skipped={skipped}.");
    }

    // ── Failed path ───────────────────────────────────────────────────────────────────────────────

    private async Task AdvanceFailedJobAsync(
        CurriculumDbContext db,
        PipelineJob job,
        CancellationToken ct)
    {
        var newRetryCount = job.RetryCount + 1;

        if (newRetryCount <= _config.InferEdgesMaxRetries)
        {
            db.PipelineJobs.Add(new PipelineJob
            {
                JobType     = "infer_edges",
                Status      = "Pending",
                PayloadJson = job.PayloadJson,
                RetryCount  = newRetryCount,
            });

            _logger.LogInfo(
                $"EdgeInferenceAdvanceService: job id={job.Id} failed " +
                $"(attempt {newRetryCount}/{_config.InferEdgesMaxRetries}); re-enqueuing.");
        }
        else
        {
            job.Status = StatusPermanentlyFailed;

            _logger.LogError(null,
                $"EdgeInferenceAdvanceService: job id={job.Id} PERMANENTLY FAILED after " +
                $"{_config.InferEdgesMaxRetries} retries. Error: {job.ErrorMessage}");
        }
    }

    // ── ResultJson deserialization ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deserializes the <c>ResultJson</c> written by the Python infer_edges worker.
    /// FROZEN CONTRACT (BL-05 divergence guard — cross-process contract, do NOT change field names):
    /// <code>
    /// { "inference_model": "&lt;string&gt;",
    ///   "edges": [ { "source_skill_key": "&lt;string&gt;", "target_skill_key": "&lt;string&gt;",
    ///                "relationship_type": "Prerequisite" | "Related",
    ///                "strength": &lt;float 0..1&gt;, "confidence": &lt;float 0..1&gt; } ] }
    /// </code>
    /// </summary>
    private static InferenceJobResultParsed DeserializeAndValidateResult(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            throw new InferResultValidationException("ResultJson is null or empty.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(resultJson);
        }
        catch (JsonException ex)
        {
            throw new InferResultValidationException($"ResultJson is not valid JSON: {ex.Message}", ex);
        }

        using (doc)
        {
            var root  = doc.RootElement;
            var model = GetStringSafe(root, "inference_model") ?? "unknown";
            var edges = ParseEdgeArray(root);
            return new InferenceJobResultParsed(model, edges);
        }
    }

    private static List<ParsedEdgeItem> ParseEdgeArray(JsonElement root)
    {
        var result = new List<ParsedEdgeItem>();
        if (!root.TryGetProperty("edges", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var el in arr.EnumerateArray())
        {
            var sourceSkillKey   = GetStringSafe(el, "source_skill_key");
            var targetSkillKey   = GetStringSafe(el, "target_skill_key");
            var relationshipType = GetStringSafe(el, "relationship_type") ?? "Related";
            // Server-side re-clamp: never trust LLM-emitted floats outside [0,1].
            var strength         = ClampValue(GetDecimalSafe(el, "strength",   0.0m));
            var confidence       = ClampValue(GetDecimalSafe(el, "confidence", 0.0m));

            // Skip malformed edges (missing SkillKeys).
            if (string.IsNullOrWhiteSpace(sourceSkillKey) || string.IsNullOrWhiteSpace(targetSkillKey))
                continue;

            // Self-loop guard (defence-in-depth; Python post-processor should catch this too).
            if (string.Equals(sourceSkillKey, targetSkillKey, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(new ParsedEdgeItem(
                sourceSkillKey,
                targetSkillKey,
                relationshipType,
                strength,
                confidence));
        }

        return result;
    }

    // ── JSON helpers ──────────────────────────────────────────────────────────────────────────────

    private static string? GetStringSafe(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind == JsonValueKind.Null)
            return null;
        return v.GetString();
    }

    private static decimal GetDecimalSafe(JsonElement el, string prop, decimal fallback)
    {
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Number)
            return fallback;
        return v.TryGetDecimal(out var d) ? d : fallback;
    }

    private static decimal ClampValue(decimal value)
        => value < 0.0m ? 0.0m : value > 1.0m ? 1.0m : value;

    private static CurriculumRelationshipType MapRelationshipType(string? raw) =>
        raw?.Trim() switch
        {
            "Prerequisite" => CurriculumRelationshipType.Prerequisite,
            _              => CurriculumRelationshipType.Related,
        };

    private static string TruncateWithEllipsis(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? string.Empty;
        return value[..(maxLength - Ellipsis.Length)] + Ellipsis;
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────────────────────────

    private sealed record ClaimedJobInfo
    {
        public int Id { get; init; }
        public string OriginalStatus { get; init; } = string.Empty;
    }

    private sealed record InferenceJobResultParsed(
        string InferenceModel,
        List<ParsedEdgeItem> Edges);

    private sealed record ParsedEdgeItem(
        string SourceSkillKey,
        string TargetSkillKey,
        string RelationshipType,
        decimal Strength,
        decimal Confidence);

    private sealed class InferResultValidationException : Exception
    {
        public InferResultValidationException(string message) : base(message) { }
        public InferResultValidationException(string message, Exception inner) : base(message, inner) { }
    }
}
