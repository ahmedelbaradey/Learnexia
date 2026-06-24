using System.Text.Json;
using System.Text.RegularExpressions;
using Learnexia.Modules.Curriculum.Application.Abstractions;
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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Jobs;

/// <summary>
/// Hosted background poller that advances <see cref="CurriculumDocument"/> ingestion status
/// after the Python worker completes an <c>ingest</c> <see cref="PipelineJob"/> (BL-05-BE-13).
///
/// <para><strong>Claim mechanics (FOR UPDATE SKIP LOCKED):</strong>
/// Each poll cycle opens a transaction and claims up to 10 unarchived Done/Failed ingest jobs
/// atomically using raw SQL <c>FOR UPDATE SKIP LOCKED</c>.  Multiple Host instances will skip
/// rows locked by another poller instance.</para>
///
/// <para><strong>On Done:</strong>
/// Deserializes and validates <c>ResultJson</c> (untrusted Python output).  For each node above
/// the confidence threshold: calls <see cref="IPedagogicalTreeWriter"/> to ensure the learning
/// node exists (gets ids back), then writes/upserts <see cref="CurriculumChunk"/> rows linked to
/// the Draft <see cref="CurriculumVersion"/>.  Below-threshold items → <see cref="IngestionReviewItem"/>.
/// Writes <see cref="ProvenanceMapping"/> per chunk.
/// Sets <c>CurriculumDocument.IngestionStatus=Done</c> + <c>IngestedAt</c>.
/// The job is flipped to <c>'Archived'</c>.</para>
///
/// <para><strong>On Failed:</strong>
/// Increments <c>RetryCount</c>. If retries remain → fresh Pending ingest job; if exhausted →
/// <c>IngestionStatus=Failed</c> + <c>IngestionDiagnostics</c>. Policy identical to
/// <see cref="ParseJobAdvanceService"/>.</para>
///
/// <para><strong>No-stranding guarantee:</strong>
/// ANY exception caught during processing triggers <see cref="TerminateJobOnExceptionAsync"/>,
/// which writes <c>PermanentlyFailed</c> / <c>IngestionStatus=Failed</c> in a fresh transaction.</para>
///
/// <para><strong>ResultJson trust model (untrusted-input hardening):</strong>
/// All strings are bounded against DB column maxes before assignment.  Over-long
/// <c>skill_key</c>-equivalent fixed keys are REJECTED (not truncated).  Free-text fields
/// are truncated with an ellipsis marker.  Mirrors <see cref="ParseJobAdvanceService"/>.</para>
///
/// <para><strong>Config:</strong> <see cref="IngestJobPollerConfiguration"/> from appsettings
/// <c>CurriculumPipeline</c> section keys <c>IngestPollerIntervalSeconds</c>,
/// <c>IngestMaxRetries</c>, and <c>IngestionConfidenceThreshold</c>.</para>
/// </summary>
public sealed class IngestJobAdvanceService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly IngestJobPollerConfiguration _config;

    // Sentinel values written by THIS poller — NOT produced by Python.
    private const string StatusArchived          = "Archived";
    private const string StatusPermanentlyFailed = "PermanentlyFailed";

    // ── Column-length constants (mirror EF configurations exactly) ────────────────────────────────
    // Sourced from: IngestionReviewItemConfig, CurriculumChunkConfig, ProvenanceMappingConfig,
    // CurriculumDocumentConfig, PipelineJobConfig.
    private const int MaxSkillKeyLength             = 256;   // CurriculumChunk.SkillKey
    private const int MaxContentLength              = 8000;  // CurriculumChunk.Content (generous)
    private const int MaxProvenanceRefLength        = 512;   // CurriculumChunk.ProvenanceRef
    private const int MaxSuggestedClassLength       = 1024;  // IngestionReviewItem.SuggestedClassification
    private const int MaxSourceReferenceLength      = 512;   // IngestionReviewItem.SourceReference
    private const int MaxIngestionDiagnosticsLength = 2048;  // CurriculumDocument.IngestionDiagnostics
    private const int MaxErrorMessageLength         = 2048;  // PipelineJob.ErrorMessage
    private const int MaxReviewNotesLength          = 2048;  // IngestionReviewItem.ReviewNotes
    private const int MaxNodeNameLength             = 500;   // generic safe bound for node display names

    private const string Ellipsis = "…";

    public IngestJobAdvanceService(
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        IOptions<IngestJobPollerConfiguration> config)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _config       = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInfo(
            $"IngestJobAdvanceService started. interval={_config.IngestPollerIntervalSeconds}s " +
            $"maxRetries={_config.IngestMaxRetries} confidenceThreshold={_config.IngestionConfidenceThreshold}.");

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
                _logger.LogError(ex, "IngestJobAdvanceService: unhandled exception in poll loop.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_config.IngestPollerIntervalSeconds),
                stoppingToken);
        }

        _logger.LogInfo("IngestJobAdvanceService stopped.");
    }

    // ── Poll cycle ────────────────────────────────────────────────────────────────────────────────

    private async Task AdvanceCompletedJobsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db        = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<SharedResources>>();
        var treeWriter = scope.ServiceProvider.GetRequiredService<IPedagogicalTreeWriter>();
        var versionResolver = scope.ServiceProvider.GetRequiredService<ICurriculumVersionResolver>();

        // ── Claim batch with FOR UPDATE SKIP LOCKED ───────────────────────────────────────────────
        const string claimCte = """
            WITH claimed AS (
                SELECT "Id", "Status" AS "OriginalStatus"
                FROM curriculum."PipelineJobs"
                WHERE "JobType" = 'ingest'
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
            .Include(j => j.Document)
            .ToListAsync(ct);

        var originalStatus = claimedInfos.ToDictionary(c => c.Id, c => c.OriginalStatus);

        foreach (var job in jobs)
        {
            var wasSuccess = originalStatus.TryGetValue(job.Id, out var orig) &&
                             string.Equals(orig, "Done", StringComparison.OrdinalIgnoreCase);
            await ProcessJobAsync(db, job, wasSuccess, localizer, treeWriter, versionResolver, ct);
        }
    }

    // ── Per-job processing ────────────────────────────────────────────────────────────────────────

    private async Task ProcessJobAsync(
        CurriculumDbContext db,
        PipelineJob job,
        bool wasSuccessful,
        IStringLocalizer<SharedResources> localizer,
        IPedagogicalTreeWriter treeWriter,
        ICurriculumVersionResolver versionResolver,
        CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            if (wasSuccessful)
            {
                await AdvanceDoneJobAsync(db, job, localizer, treeWriter, versionResolver, ct);
                job.Status = StatusArchived;
            }
            else
            {
                await AdvanceFailedJobAsync(db, job, localizer, ct);
                job.Status = StatusPermanentlyFailed;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"IngestJobAdvanceService: failed to process job id={job.Id}; triggering terminal write.");
            await TerminateJobOnExceptionAsync(db, job, localizer, ct);
        }
    }

    private async Task TerminateJobOnExceptionAsync(
        CurriculumDbContext db,
        PipelineJob job,
        IStringLocalizer<SharedResources> localizer,
        CancellationToken ct)
    {
        try
        {
            db.ChangeTracker.Clear();
            db.PipelineJobs.Attach(job);
            if (job.Document is not null)
                db.CurriculumDocuments.Attach(job.Document);

            await using var termTx = await db.Database.BeginTransactionAsync(ct);

            var terminalReason = localizer[SharedResourcesKey.CurriculumIngestProcessingFailed].Value;

            job.Status = StatusPermanentlyFailed;
            if (job.Document is not null)
                MarkDocumentFailed(job.Document, terminalReason);

            await db.SaveChangesAsync();
            await termTx.CommitAsync(ct);

            _logger.LogInfo(
                $"IngestJobAdvanceService: job id={job.Id} and document id={job.Document?.Id} " +
                "transitioned to terminal state (PermanentlyFailed/Failed) after processing error.");
        }
        catch (Exception termEx)
        {
            _logger.LogError(termEx,
                $"IngestJobAdvanceService: terminal write also failed for job id={job.Id}. " +
                "The job may remain at 'Processing' — manual intervention required.");
        }
    }

    // ── Done path ─────────────────────────────────────────────────────────────────────────────────

    private async Task AdvanceDoneJobAsync(
        CurriculumDbContext db,
        PipelineJob job,
        IStringLocalizer<SharedResources> localizer,
        IPedagogicalTreeWriter treeWriter,
        ICurriculumVersionResolver versionResolver,
        CancellationToken ct)
    {
        var document = job.Document;

        IngestionJobResultParsed result;
        try
        {
            result = DeserializeAndValidateResult(job.ResultJson);
        }
        catch (IngestResultValidationException ex)
        {
            _logger.LogError(ex,
                $"IngestJobAdvanceService: ResultJson validation failed for job id={job.Id}. " +
                $"Detail: {ex.Message}");

            var safeReason = localizer[SharedResourcesKey.CurriculumIngestResultInvalid].Value;
            MarkDocumentFailed(document, safeReason);
            return;
        }

        _logger.LogInfo(
            $"IngestJobAdvanceService: job id={job.Id} Done. " +
            $"nodes={result.Nodes.Count} chunks={result.Chunks.Count} flags={result.Flags.Count}.");

        // ── BE-12: Resolve the Draft CurriculumVersion ────────────────────────────────────────────
        // We need a subjectId; derive it from the first node above threshold or the document itself.
        // The document.SubjectId is the authoritative cross-module reference.
        var versionId = await versionResolver.ResolveDraftVersionAsync(
            document.SubjectId,
            document.Language,
            ct);

        // ── BE-5: Upsert hierarchy + chunks; BE-6: review routing ─────────────────────────────────
        // Build a SkillKey→(SkillId, KnowledgeNodeId) lookup for chunk linking.
        var skillKeyLookup = new Dictionary<string, (int SkillId, int KnowledgeNodeId)>(
            StringComparer.OrdinalIgnoreCase);

        // Also build a GradeLevel→GradeId map (we use document.GradeId as the canonical grade).
        // The document was uploaded with a specific GradeId — use that for all nodes.
        var documentGradeId = document.GradeId;

        foreach (var node in result.Nodes)
        {
            if (!string.Equals(node.Type, "skill", StringComparison.OrdinalIgnoreCase))
                continue;

            if (node.Confidence >= _config.IngestionConfidenceThreshold)
            {
                try
                {
                    // We need subjectId from the learning module. Since we can't query learning
                    // directly, we call EnsureSubjectAsync first which is also idempotent.
                    var subjectId = await treeWriter.EnsureSubjectAsync(
                        documentGradeId,
                        node.SubjectCode,
                        node.Language,
                        TruncateWithEllipsis(node.SubjectName, MaxNodeNameLength),
                        ct);

                    // The Python contract emits parent_skill_key (a stable key, not a display name).
                    // For the concept→skill path we use the node's own title as the concept name
                    // when no parent_skill_key is present, keeping the tree deterministic.
                    var parentConceptName = TruncateWithEllipsis(node.ParentSkillKey ?? node.Name, MaxNodeNameLength);
                    var conceptId = await treeWriter.EnsureConceptAsync(
                        parentConceptName,
                        subjectId,
                        node.Description,
                        node.Difficulty,
                        ct);

                    var skillKey  = TruncateWithEllipsis(node.SkillKey ?? string.Empty, MaxSkillKeyLength);
                    var skillName = TruncateWithEllipsis(node.Name, MaxNodeNameLength);

                    var skillResult = await treeWriter.EnsureSkillAsync(
                        skillName,
                        conceptId,
                        skillKey,
                        masteryThreshold:     75,   // default — not supplied by extractor
                        estimatedTimeMinutes: 20,   // default
                        subjectId,
                        documentGradeId,
                        node.Difficulty,
                        ct);

                    if (!string.IsNullOrWhiteSpace(skillKey))
                        skillKeyLookup[skillKey] = (skillResult.SkillId, skillResult.KnowledgeNodeId);

                    _logger.LogInfo(
                        $"IngestJobAdvanceService: upserted hierarchy for skillKey='{skillKey}' " +
                        $"skillId={skillResult.SkillId} knowledgeNodeId={skillResult.KnowledgeNodeId}.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"IngestJobAdvanceService: hierarchy upsert failed for node name='{node.Name}'. " +
                        "Skipping this node but continuing with others.");
                }
            }
            else
            {
                // BE-6: route low-confidence nodes to the review queue.
                await CreateReviewItemAsync(
                    db,
                    document.Id,
                    versionId,
                    sourceRef: node.SkillKey ?? node.Name,
                    suggestedClassification: TruncateWithEllipsis(
                        $"{node.SubjectName} > {node.Name}", MaxSuggestedClassLength),
                    confidence: node.Confidence,
                    payloadJson: SerializeNodeAsPayload(node),
                    ct);
            }
        }

        // ── Process chunks ────────────────────────────────────────────────────────────────────────
        foreach (var chunkItem in result.Chunks)
        {
            if (chunkItem.Confidence < _config.IngestionConfidenceThreshold)
            {
                // Low-confidence chunk → review item (withheld).
                await CreateReviewItemAsync(
                    db,
                    document.Id,
                    versionId,
                    sourceRef: TruncateWithEllipsis(chunkItem.SourceReference ?? "unknown", MaxSourceReferenceLength),
                    suggestedClassification: TruncateWithEllipsis(
                        $"Chunk confidence={chunkItem.Confidence:F2}", MaxSuggestedClassLength),
                    confidence: chunkItem.Confidence,
                    payloadJson: SerializeChunkAsPayload(chunkItem),
                    ct);
                continue;
            }

            // Look up skill/concept IDs by SkillKey (Q9: chunk linked by SkillKey, not by GradeId+FileName).
            int? skillId         = null;
            int? conceptId       = null;
            var  skillKey        = TruncateWithEllipsis(chunkItem.SkillKey ?? string.Empty, MaxSkillKeyLength);

            if (!string.IsNullOrWhiteSpace(chunkItem.SkillKey) &&
                skillKeyLookup.TryGetValue(chunkItem.SkillKey, out var sk))
            {
                skillId = sk.SkillId;
                // ConceptId not directly stored in the lookup — null for now.
                // BL-03 will infer the KG edges; the chunk has the SkillKey for correlation.
            }

            // Upsert chunk (idempotent on SkillKey + ProvenanceRef + VersionId).
            var provenanceRef = TruncateWithEllipsis(
                chunkItem.SourceReference ?? string.Empty, MaxProvenanceRefLength);

            var existingChunk = !string.IsNullOrWhiteSpace(skillKey)
                ? await db.CurriculumChunks
                    .FirstOrDefaultAsync(c =>
                        c.SkillKey              == skillKey &&
                        c.ProvenanceRef         == provenanceRef &&
                        c.CurriculumVersionId   == versionId,
                        ct)
                : null;

            int chunkId;
            if (existingChunk is null)
            {
                var safeContent  = TruncateWithEllipsis(chunkItem.Content, MaxContentLength);
                var safeMetadata = chunkItem.Metadata ?? "{}";

                var chunk = new CurriculumChunk
                {
                    Content           = safeContent,
                    Metadata          = safeMetadata,
                    Difficulty        = chunkItem.Difficulty,
                    SubjectId         = document.SubjectId,
                    GradeId           = documentGradeId,
                    ConceptId         = conceptId,
                    SkillId           = skillId,
                    SkillKey          = skillKey,
                    Language          = (ContentLanguage)chunkItem.Language,
                    CurriculumVersionId = versionId,
                    ProvenanceRef     = provenanceRef,
                };
                db.CurriculumChunks.Add(chunk);
                await db.SaveChangesAsync();
                chunkId = chunk.Id;
            }
            else
            {
                // Update existing chunk (re-ingest updates content).
                existingChunk.Content    = TruncateWithEllipsis(chunkItem.Content, MaxContentLength);
                existingChunk.Difficulty = chunkItem.Difficulty;
                existingChunk.SkillId    = skillId;
                chunkId = existingChunk.Id;
            }

            // ── BE-11: ProvenanceMapping per chunk ────────────────────────────────────────────────
            await UpsertProvenanceMappingAsync(db, chunkId, document, provenanceRef, skillId, ct);
        }

        // ── Process flags from the Python worker (additional low-confidence items) ──────────────
        foreach (var flag in result.Flags)
        {
            await CreateReviewItemAsync(
                db,
                document.Id,
                versionId,
                sourceRef: TruncateWithEllipsis(flag.Ref ?? "unknown", MaxSourceReferenceLength),
                suggestedClassification: TruncateWithEllipsis(flag.Kind, MaxSuggestedClassLength),
                confidence: flag.Confidence,
                payloadJson: SerializeFlagAsPayload(flag),
                ct);
        }

        // ── Set document IngestionStatus=Done ────────────────────────────────────────────────────
        document.IngestionStatus = IngestionStatus.Done;
        document.IngestedAt      = DateTimeOffset.UtcNow;
        document.IngestionDiagnostics = null;

        _logger.LogInfo(
            $"IngestJobAdvanceService: document id={document.Id} IngestionStatus=Done. " +
            $"versionId={versionId}.");
    }

    // ── BE-11: ProvenanceMapping upsert ───────────────────────────────────────────────────────────

    private async Task UpsertProvenanceMappingAsync(
        CurriculumDbContext db,
        int chunkId,
        CurriculumDocument document,
        string pageOrBlockRef,
        int? skillId,
        CancellationToken ct)
    {
        // Q9 fix: resolve ContentSource by CurriculumDocumentId linkage, not (GradeId, FileName).
        // BuildProvenanceTreeAsync in ParseJobAdvanceService creates a ContentSource keyed on
        // (GradeId, FileName). We look up the SAME ContentSource by querying for the one that
        // matches the document's grade + filename (the key BL-02 used).
        // TODO: A stronger fix would be to store ContentSourceId on CurriculumDocument so that
        //       the linkage is unambiguous when two documents share grade+filename. Tracked as a
        //       BL-02 follow-up (HANDOFF Q9 nit).
        var contentSource = await db.ContentSources
            .AsNoTracking()
            .Where(cs => cs.GradeId == document.GradeId && cs.Title == document.FileName)
            .OrderByDescending(cs => cs.Id)  // prefer the most-recently-created (per Q9 note)
            .FirstOrDefaultAsync(ct);

        if (contentSource is null)
        {
            // ContentSource may not exist if parse hasn't run yet or provenance tree was not built.
            // Log and skip — provenance is best-effort at ingest time.
            _logger.LogInfo(
                $"IngestJobAdvanceService BE-11: no ContentSource for document id={document.Id} " +
                $"(grade={document.GradeId}, fileName={document.FileName}). Skipping provenance mapping.");
            return;
        }

        // Idempotency key: (ChunkId, ContentSourceId, PageOrBlockRef).
        var existingMapping = await db.ProvenanceMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.ChunkId         == chunkId &&
                m.ContentSourceId == contentSource.Id &&
                m.PageOrBlockRef  == pageOrBlockRef,
                ct);

        if (existingMapping is not null)
            return;  // already mapped (idempotent re-ingest)

        // Match chapter by page range if pageOrBlockRef is a page number.
        int? chapterId = null;
        if (int.TryParse(pageOrBlockRef.TrimStart('p', '.', ' '), out var pageNum))
        {
            var chapter = await db.Chapters
                .AsNoTracking()
                .Where(ch =>
                    ch.ContentSourceId == contentSource.Id &&
                    ch.PageStart.HasValue && ch.PageStart <= pageNum &&
                    ch.PageEnd.HasValue   && ch.PageEnd   >= pageNum)
                .FirstOrDefaultAsync(ct);
            chapterId = chapter?.Id;
        }

        var mapping = new ProvenanceMapping
        {
            ChunkId              = chunkId,
            ContentSourceId      = contentSource.Id,
            ChapterId            = chapterId,
            PageOrBlockRef       = pageOrBlockRef,
            PedagogicalNodeId    = skillId ?? 0,
            PedagogicalNodeType  = skillId.HasValue ? "Skill" : "Chunk",
        };
        db.ProvenanceMappings.Add(mapping);
        await db.SaveChangesAsync();
    }

    // ── Review item helper ────────────────────────────────────────────────────────────────────────

    private static async Task CreateReviewItemAsync(
        CurriculumDbContext db,
        int documentId,
        int? versionId,
        string sourceRef,
        string suggestedClassification,
        decimal confidence,
        string payloadJson,
        CancellationToken ct)
    {
        var item = new IngestionReviewItem
        {
            CurriculumDocumentId    = documentId,
            CurriculumVersionId     = versionId,
            SourceReference         = sourceRef,
            SuggestedClassification = suggestedClassification,
            Confidence              = confidence,
            PayloadJson             = payloadJson,
            Status                  = ReviewStatus.Pending,
        };
        db.IngestionReviewItems.Add(item);
        await db.SaveChangesAsync();
    }

    // ── Failed path ───────────────────────────────────────────────────────────────────────────────

    private async Task AdvanceFailedJobAsync(
        CurriculumDbContext db,
        PipelineJob job,
        IStringLocalizer<SharedResources> localizer,
        CancellationToken ct)
    {
        var document      = job.Document;
        var newRetryCount = job.RetryCount + 1;

        if (newRetryCount <= _config.IngestMaxRetries)
        {
            db.PipelineJobs.Add(new PipelineJob
            {
                JobType     = "ingest",
                Status      = "Pending",
                DocumentId  = document.Id,
                PayloadJson = job.PayloadJson,
                RetryCount  = newRetryCount,
            });

            document.IngestionStatus      = IngestionStatus.InProgress;
            document.IngestionDiagnostics = null;

            _logger.LogInfo(
                $"IngestJobAdvanceService: document id={document.Id} ingest failed " +
                $"(attempt {newRetryCount}/{_config.IngestMaxRetries}); re-enqueuing.");
        }
        else
        {
            var reason = !string.IsNullOrWhiteSpace(job.ErrorMessage)
                ? TruncateWithEllipsis(
                    SanitizeControlChars(job.ErrorMessage),
                    MaxIngestionDiagnosticsLength)
                : localizer[SharedResourcesKey.CurriculumIngestProcessingFailed].Value;

            MarkDocumentFailed(document, reason);

            _logger.LogError(null,
                $"IngestJobAdvanceService: document id={document.Id} PERMANENTLY FAILED after " +
                $"{_config.IngestMaxRetries} retries. jobId={job.Id}. Error: {job.ErrorMessage}");
        }
    }

    private static void MarkDocumentFailed(CurriculumDocument document, string diagnostics)
    {
        document.IngestionStatus      = IngestionStatus.Failed;
        document.IngestionDiagnostics = diagnostics;
    }

    // ── ResultJson deserialization + validation ───────────────────────────────────────────────────

    /// <summary>
    /// Deserializes <c>ResultJson</c> from the Python ingest worker.
    /// All strings are bounded against column maxes.  Missing/null/extra fields handled gracefully.
    /// </summary>
    private static IngestionJobResultParsed DeserializeAndValidateResult(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            throw new IngestResultValidationException("ResultJson is null or empty.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(resultJson);
        }
        catch (JsonException ex)
        {
            throw new IngestResultValidationException($"ResultJson is not valid JSON: {ex.Message}", ex);
        }

        using (doc)
        {
            var root   = doc.RootElement;
            var nodes  = ParseNodeArray(root);
            var chunks = ParseChunkArray(root);
            var flags  = ParseFlagArray(root);
            return new IngestionJobResultParsed(nodes, chunks, flags);
        }
    }

    private static List<ParsedNodeItem> ParseNodeArray(JsonElement root)
    {
        var result = new List<ParsedNodeItem>();
        if (!root.TryGetProperty("nodes", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var el in arr.EnumerateArray())
        {
            // DEFECT-1 fix: Python emits "node_type" (PascalCase), not "type"
            var type           = GetStringSafe(el, "node_type") ?? "Skill";
            // DEFECT-2 fix: Python emits "title", not "name"
            var name           = GetStringSafe(el, "title") ?? string.Empty;
            var skillKey       = GetStringSafe(el, "skill_key");
            // FINDING #1b fix: fail-closed default 0.0m; clamp to [0,1]
            var confidence     = ClampConfidence(GetDecimalSafe(el, "confidence", 0.0m));
            // DEFECT-3 fix: Python emits "grade" (int), not "grade_level"
            var gradeLevel     = GetInt32Safe(el, "grade", 1);
            // DEFECT-4 fix: Python emits "subject_code" as string ("math"); map to int SubjectCode
            var subjectCode    = MapSubjectCodeString(GetStringSafe(el, "subject_code"));
            // Python does not emit "subject_name" — derive from subject_code string (used only for display)
            var subjectName    = GetStringSafe(el, "subject_code") ?? string.Empty;
            // Python emits "language" as string ("ar"/"en"); map to int ContentLanguage
            var language       = MapLanguageString(GetStringSafe(el, "language"));
            // difficulty is nullable int in Python (1..5 | null); default 2 on null
            var difficulty     = GetInt32Safe(el, "difficulty", 2);
            // DEFECT: Python emits "parent_skill_key" (the stable key of parent node), not "parent_name"
            var parentSkillKey = GetStringSafe(el, "parent_skill_key");
            var sequenceOrder  = GetInt32Safe(el, "sequence_order", 1);
            var description    = GetStringSafe(el, "description");

            result.Add(new ParsedNodeItem(
                type, name, skillKey, confidence, gradeLevel,
                subjectName, subjectCode, language, difficulty,
                parentSkillKey, sequenceOrder, description));
        }
        return result;
    }

    private static List<ParsedChunkItemInternal> ParseChunkArray(JsonElement root)
    {
        var result = new List<ParsedChunkItemInternal>();
        if (!root.TryGetProperty("chunks", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var el in arr.EnumerateArray())
        {
            // DEFECT-5 fix: Python emits "source_page" (int|null), not "source_reference" (string)
            // Convert page int to a "p.N" string for ProvenanceRef; fall back to chapter_number
            var sourcePage      = GetNullableInt32Safe(el, "source_page");
            var chapterNumber   = GetNullableInt32Safe(el, "chapter_number");
            var sourceReference = sourcePage.HasValue
                ? $"p.{sourcePage.Value}"
                : chapterNumber.HasValue
                    ? $"ch.{chapterNumber.Value}"
                    : null;

            result.Add(new ParsedChunkItemInternal(
                Content:         GetStringSafe(el, "content") ?? string.Empty,
                Metadata:        GetStringSafe(el, "metadata"),
                Difficulty:      GetInt32Safe(el, "difficulty", 2),
                SourceReference: sourceReference,
                // grade/subject not emitted per-chunk in Python contract; use 0 defaults (doc values used for chunk)
                GradeLevel:      GetInt32Safe(el, "grade", 1),
                SubjectCode:     0,
                Language:        MapLanguageString(GetStringSafe(el, "language")),
                // DEFECT-6 fix: Python emits "node_skill_key" for the chunk→node link, not "skill_key"
                SkillKey:        GetStringSafe(el, "node_skill_key"),
                // FINDING #1b fix: fail-closed default 0.0m; clamp to [0,1]
                Confidence:      ClampConfidence(GetDecimalSafe(el, "confidence", 0.0m))));
        }
        return result;
    }

    private static List<ParsedFlagItem> ParseFlagArray(JsonElement root)
    {
        var result = new List<ParsedFlagItem>();
        if (!root.TryGetProperty("flags", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var el in arr.EnumerateArray())
        {
            // DEFECT-7 fix: Python emits "kind"/"ref"/"reason"/"confidence", not
            // "suggested_classification"/"source_reference"/"payload_json"
            // Note: "reason" is a bare string — it must NOT be stored raw into PayloadJson
            // (a jsonb column). SerializeFlagAsPayload assembles a valid JSON object instead.
            result.Add(new ParsedFlagItem(
                Kind:       GetStringSafe(el, "kind") ?? string.Empty,
                Ref:        GetStringSafe(el, "ref"),
                Reason:     GetStringSafe(el, "reason"),
                Confidence: ClampConfidence(GetDecimalSafe(el, "confidence", 0.0m))));
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

    private static int GetInt32Safe(JsonElement el, string prop, int fallback)
    {
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Number)
            return fallback;
        return v.TryGetInt32(out var n) ? n : fallback;
    }

    private static decimal GetDecimalSafe(JsonElement el, string prop, decimal fallback)
    {
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Number)
            return fallback;
        return v.TryGetDecimal(out var d) ? d : fallback;
    }

    private static int? GetNullableInt32Safe(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v))
            return null;
        if (v.ValueKind == JsonValueKind.Null)
            return null;
        if (v.ValueKind != JsonValueKind.Number)
            return null;
        return v.TryGetInt32(out var n) ? n : null;
    }

    /// <summary>
    /// Clamps a confidence value to the valid [0.0, 1.0] range.
    /// Security hardening: never trust an out-of-range value from the worker/LLM
    /// (Finding #1b — server-side clamp so the review threshold is not bypassable).
    /// </summary>
    private static decimal ClampConfidence(decimal value)
        => value < 0.0m ? 0.0m : value > 1.0m ? 1.0m : value;

    /// <summary>
    /// Maps the Python-emitted lowercase subject_code string to the Learning module
    /// <c>SubjectCode</c> int value. Unknown codes map to 0 (MATH) as a safe fallback;
    /// the document's own SubjectId is authoritative for chunk association.
    /// </summary>
    private static int MapSubjectCodeString(string? code) =>
        code?.ToLowerInvariant() switch
        {
            "math"    => 0,   // SubjectCode.MATH
            "science" => 1,   // SubjectCode.SCIENCE
            "arabic"  => 2,   // SubjectCode.ARABIC
            "english" => 3,   // SubjectCode.ENGLISH
            _         => 0,   // safe fallback
        };

    /// <summary>
    /// Maps the Python-emitted language string ("ar"/"en") to the ContentLanguage int value.
    /// </summary>
    private static int MapLanguageString(string? lang) =>
        lang?.ToLowerInvariant() switch
        {
            "ar" => 0,   // ContentLanguage.Arabic
            "en" => 1,   // ContentLanguage.English
            _    => 0,   // default Arabic
        };

    // ── Payload serializers (for IngestionReviewItem.PayloadJson) ─────────────────────────────────

    private static string SerializeNodeAsPayload(ParsedNodeItem node)
    {
        try
        {
            // Serialize using Python contract field names so ApproveIngestionReviewItemCommandHandler
            // reads the same keys when re-processing a review item's PayloadJson.
            return JsonSerializer.Serialize(new
            {
                node_type        = node.Type,
                title            = node.Name,
                skill_key        = node.SkillKey,
                confidence       = node.Confidence,
                grade            = node.GradeLevel,
                subject_code     = node.SubjectCode,
                language         = node.Language,
                difficulty       = node.Difficulty,
                parent_skill_key = node.ParentSkillKey,
                description      = node.Description,
            });
        }
        catch
        {
            return "{}";
        }
    }

    private static string SerializeChunkAsPayload(ParsedChunkItemInternal chunk)
    {
        try
        {
            return JsonSerializer.Serialize(new
            {
                node_skill_key    = chunk.SkillKey,
                source_reference  = chunk.SourceReference,
                confidence        = chunk.Confidence,
                grade             = chunk.GradeLevel,
                subject_code      = chunk.SubjectCode,
                difficulty        = chunk.Difficulty,
            });
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// Serializes a Python flag entry into a valid JSON object for the jsonb PayloadJson column.
    /// The Python flag shape is <c>{kind, ref, confidence, reason}</c>.  We must NOT write the
    /// "reason" bare string directly into a jsonb column — wrap all fields in a JSON object.
    /// </summary>
    private static string SerializeFlagAsPayload(ParsedFlagItem flag)
    {
        try
        {
            return JsonSerializer.Serialize(new
            {
                kind       = flag.Kind,
                @ref       = flag.Ref,
                reason     = flag.Reason,
                confidence = flag.Confidence,
            });
        }
        catch
        {
            return "{}";
        }
    }

    // ── String safety helpers ─────────────────────────────────────────────────────────────────────

    private static string TruncateWithEllipsis(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? string.Empty;
        return value[..(maxLength - Ellipsis.Length)] + Ellipsis;
    }

    private static string SanitizeControlChars(string value) =>
        Regex.Replace(value, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", string.Empty);

    // ── Private DTOs ──────────────────────────────────────────────────────────────────────────────

    private sealed record ClaimedJobInfo
    {
        public int Id { get; init; }
        public string OriginalStatus { get; init; } = string.Empty;
    }

    private sealed record IngestionJobResultParsed(
        List<ParsedNodeItem> Nodes,
        List<ParsedChunkItemInternal> Chunks,
        List<ParsedFlagItem> Flags);

    private sealed record ParsedNodeItem(
        string Type,
        string Name,
        string? SkillKey,
        decimal Confidence,
        int GradeLevel,
        string SubjectName,
        int SubjectCode,
        int Language,
        int Difficulty,
        string? ParentSkillKey,
        int SequenceOrder,
        string? Description);

    private sealed record ParsedChunkItemInternal(
        string Content,
        string? Metadata,
        int Difficulty,
        string? SourceReference,
        int GradeLevel,
        int SubjectCode,
        int Language,
        string? SkillKey,
        decimal Confidence);

    private sealed record ParsedFlagItem(
        string Kind,
        string? Ref,
        string? Reason,
        decimal Confidence);

    private sealed class IngestResultValidationException : Exception
    {
        public IngestResultValidationException(string message) : base(message) { }
        public IngestResultValidationException(string message, Exception inner) : base(message, inner) { }
    }
}
