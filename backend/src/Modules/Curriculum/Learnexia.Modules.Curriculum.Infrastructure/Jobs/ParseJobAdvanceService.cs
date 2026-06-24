using System.Text.Json;
using System.Text.RegularExpressions;
using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Configuration;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
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
/// Hosted background poller that advances <see cref="CurriculumDocument"/> parse status
/// after the Python worker completes a <c>parse</c> <see cref="PipelineJob"/> (BL-02-BE-7).
///
/// <para><strong>Claim mechanics (FOR UPDATE SKIP LOCKED):</strong>
/// Each poll cycle opens a transaction and claims up to 10 unarchived Done/Failed parse jobs
/// atomically using raw SQL <c>FOR UPDATE SKIP LOCKED</c>.  Only rows still in
/// <c>Status IN ('Done','Failed')</c> are eligible — once advanced they are flipped to
/// <c>'Archived'</c> (Done jobs) or <c>'PermanentlyFailed'</c> (exhausted retries).
/// Multiple Host instances will skip rows locked by another poller instance.</para>
///
/// <para><strong>On Done:</strong>
/// BE-3 result-reader — reads <c>ResultJson</c>, sets <c>CurriculumDocument.Status=Done</c>,
/// <c>ParsedArtifactObjectKey</c>, <c>ParsedAt</c>.
/// Then BE-6 provenance tree — creates idempotent <see cref="ContentSource"/> +
/// <see cref="Chapter"/> rows from <c>ResultJson.chapters[]</c>.
/// The job row is flipped to <c>Status='Archived'</c> (archive-in-place, Q6).</para>
///
/// <para><strong>On Failed:</strong>
/// .NET owns the retry policy (ADR-0004 Q7). Increments <c>RetryCount</c>.
/// If <c>RetryCount &lt;= MaxRetries</c> → inserts a fresh <c>Pending</c> parse job (re-enqueue);
/// the failed job is flipped to <c>'PermanentlyFailed'</c> to prevent re-processing.
/// If retries exhausted → document Status=Failed + StatusReason; job=PermanentlyFailed.</para>
///
/// <para><strong>No-stranding guarantee:</strong>
/// ANY exception thrown during <see cref="ProcessJobAsync"/> (including deserialization errors,
/// field-validation rejections, and SaveChanges failures) is caught and triggers
/// <see cref="TerminateJobOnExceptionAsync"/>, which writes <c>PermanentlyFailed</c> to the job
/// and <c>Failed</c> to the document in a fresh transaction — ensuring no job can ever remain
/// at <c>Processing</c> indefinitely.  If even the terminal write fails, the error is logged
/// and the loop continues without crashing the BackgroundService.</para>
///
/// <para><strong>ResultJson trust model:</strong>
/// <c>ResultJson</c> is written by the separate Python worker and MUST be treated as
/// untrusted cross-process input.  All derived strings are validated and bounded against
/// their DB column lengths before assignment — see <see cref="MaxArtifactKeyLength"/>,
/// <see cref="MaxChapterTitleLength"/>, and <see cref="MaxStatusReasonLength"/>.
/// An over-long <c>artifact_key</c> is treated as a parse FAILURE for that document (not
/// silently truncated to a wrong key).  Free-text fields (<c>chapter.title</c>,
/// <c>StatusReason</c>) are safely truncated with an ellipsis marker.</para>
///
/// <para><strong>No Unit of Work:</strong>
/// explicit transaction per job (CLAUDE.md rule 3).</para>
///
/// <para>Config: <see cref="PipelineJobPollerConfiguration"/> from appsettings
/// <c>CurriculumPipeline</c> section.</para>
/// </summary>
public sealed class ParseJobAdvanceService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;
    private readonly PipelineJobPollerConfiguration _config;

    // Valid processed-job sentinel values (archive-in-place, Q6).
    // These are the string values written to PipelineJob.Status by THIS poller.
    // They are NOT produced by Python (Python only writes Done/Failed).
    private const string StatusArchived           = "Archived";           // Done job, successfully advanced
    private const string StatusPermanentlyFailed  = "PermanentlyFailed";  // exhausted retries or re-enqueued

    // ── Column-length constants (mirror the EF configurations exactly) ────────────────────────
    // Sourced from: CurriculumDocumentConfig.cs, ChapterConfig.cs, PipelineJobConfig.cs.
    // NEVER hardcode a different number — keep these in sync with the EF config.
    private const int MaxArtifactKeyLength  = 512;   // CurriculumDocument.ParsedArtifactObjectKey
    private const int MaxChapterTitleLength = 512;   // Chapter.Title
    private const int MaxStatusReasonLength = 1024;  // CurriculumDocument.StatusReason
    private const int MaxErrorMessageLength = 2048;  // PipelineJob.ErrorMessage (Python-written)

    // Ellipsis appended when free-text fields are truncated.
    private const string Ellipsis = "…";

    public ParseJobAdvanceService(
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger,
        IOptions<PipelineJobPollerConfiguration> config)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _config       = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInfo(
            $"ParseJobAdvanceService started. interval={_config.PollerIntervalSeconds}s maxRetries={_config.MaxRetries}.");

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
                _logger.LogError(ex, "ParseJobAdvanceService: unhandled exception in poll loop.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(_config.PollerIntervalSeconds),
                stoppingToken);
        }

        _logger.LogInfo("ParseJobAdvanceService stopped.");
    }

    // ── Poll cycle ────────────────────────────────────────────────────────────────────────────────

    private async Task AdvanceCompletedJobsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db        = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();
        var localizer = scope.ServiceProvider.GetRequiredService<IStringLocalizer<SharedResources>>();

        // ── Claim batch with FOR UPDATE SKIP LOCKED ───────────────────────────────────────────────
        // Runs in its own transaction so the row-level locks are held while we load the rows,
        // then atomically mark them as Processing before committing — preventing any concurrent
        // poller from re-claiming the same rows.
        //
        // Raw SQL is required because EF Core does not expose SKIP LOCKED via LINQ.
        // A CTE captures each row's OriginalStatus (Done/Failed) before the UPDATE overwrites it.
        const string claimCte = """
            WITH claimed AS (
                SELECT "Id", "Status" AS "OriginalStatus"
                FROM curriculum."PipelineJobs"
                WHERE "JobType" = 'parse'
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

        // Load the full job rows (now Status='Processing' — claimed by us).
        var jobIds = claimedInfos.Select(c => c.Id).ToList();
        var jobs = await db.PipelineJobs
            .Where(j => jobIds.Contains(j.Id) && j.Status == "Processing")
            .Include(j => j.Document)
            .ToListAsync(ct);

        // Build a lookup of original status (Done/Failed) before we overwrote it.
        var originalStatus = claimedInfos.ToDictionary(c => c.Id, c => c.OriginalStatus);

        // Process each claimed job in its own transaction.
        // The no-stranding guarantee: ProcessJobAsync catches all exceptions and delegates
        // to TerminateJobOnExceptionAsync, which writes PermanentlyFailed+Failed in a fresh
        // transaction — so a job NEVER stays at 'Processing' if something goes wrong.
        foreach (var job in jobs)
        {
            var wasSuccess = originalStatus.TryGetValue(job.Id, out var orig) &&
                             string.Equals(orig, "Done", StringComparison.OrdinalIgnoreCase);
            await ProcessJobAsync(db, job, wasSuccess, localizer, ct);
        }
    }

    // ── Per-job processing ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Processes one claimed job.  Guarantees a terminal state on exit:
    /// either the normal Archived/PermanentlyFailed path commits, or
    /// <see cref="TerminateJobOnExceptionAsync"/> fires and writes Failed/PermanentlyFailed.
    /// This method never propagates exceptions to the caller — it logs and moves on.
    /// </summary>
    private async Task ProcessJobAsync(
        CurriculumDbContext db,
        PipelineJob job,
        bool wasSuccessful,
        IStringLocalizer<SharedResources> localizer,
        CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            if (wasSuccessful)
            {
                await AdvanceDoneJobAsync(db, job, localizer, ct);
                job.Status = StatusArchived;  // archive-in-place (Q6)
            }
            else
            {
                await AdvanceFailedJobAsync(db, job, localizer, ct);
                job.Status = StatusPermanentlyFailed;  // prevent re-claim
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"ParseJobAdvanceService: failed to process job id={job.Id}; triggering terminal write.");

            // Guarantee: transition the job and its document to a terminal state so the job
            // does not remain at 'Processing'.  Use a fresh transaction — the previous one
            // may have been rolled back or left in an inconsistent state.
            await TerminateJobOnExceptionAsync(db, job, localizer, ct);
        }
    }

    /// <summary>
    /// Resilient terminal-state writer.  Called when <see cref="ProcessJobAsync"/> throws.
    /// Writes <c>PermanentlyFailed</c> to the job and <c>Failed</c> to the document in a
    /// fresh transaction using a bounded, constant <c>StatusReason</c> string so that this
    /// write cannot itself throw due to an over-long value.
    ///
    /// <para>If even this terminal write fails, the exception is logged and swallowed — the
    /// BackgroundService must not crash.</para>
    /// </summary>
    private async Task TerminateJobOnExceptionAsync(
        CurriculumDbContext db,
        PipelineJob job,
        IStringLocalizer<SharedResources> localizer,
        CancellationToken ct)
    {
        try
        {
            // Clear the change tracker before the terminal write: if the failed transaction left
            // entities in a modified-but-uncommitted state, a fresh SaveChangesAsync on the same
            // context would attempt to re-apply those stale changes.  Clearing ensures only the
            // deliberate terminal mutations (status + reason) are written.
            db.ChangeTracker.Clear();

            // Re-attach the job and document so we can modify them in the clean context.
            db.PipelineJobs.Attach(job);
            if (job.Document is not null)
                db.CurriculumDocuments.Attach(job.Document);

            await using var termTx = await db.Database.BeginTransactionAsync(ct);

            // Use a constant, bounded reason string — never ex.Message here.
            var terminalReason = localizer[SharedResourcesKey.CurriculumDocumentParseProcessingFailed].Value;

            job.Status = StatusPermanentlyFailed;
            if (job.Document is not null)
                MarkDocumentFailed(job.Document, terminalReason);

            await db.SaveChangesAsync();
            await termTx.CommitAsync(ct);

            _logger.LogInfo(
                $"ParseJobAdvanceService: job id={job.Id} and document id={job.Document?.Id} " +
                "transitioned to terminal state (PermanentlyFailed/Failed) after processing error.");
        }
        catch (Exception termEx)
        {
            // Log and swallow — do not let a failed terminal write crash the BackgroundService.
            _logger.LogError(termEx,
                $"ParseJobAdvanceService: terminal write also failed for job id={job.Id}. " +
                "The job may remain at 'Processing' — manual intervention required.");
        }
    }

    // ── BE-3: result-reader (Done path) ──────────────────────────────────────────────────────────

    private async Task AdvanceDoneJobAsync(
        CurriculumDbContext db,
        PipelineJob job,
        IStringLocalizer<SharedResources> localizer,
        CancellationToken ct)
    {
        var document = job.Document;

        ParseArtifactResult result;
        try
        {
            result = DeserializeAndValidateResult(job.ResultJson);
        }
        catch (ParseResultValidationException ex)
        {
            // ResultJson is invalid or contains a field that would violate a DB column constraint.
            // Treat as a parse failure for this document — do NOT truncate artifact_key to a
            // wrong value; mark the document Failed with a safe, bounded reason string.
            _logger.LogError(ex,
                $"ParseJobAdvanceService: ResultJson validation failed for job id={job.Id}. " +
                $"Detail: {ex.Message}");

            var safeReason = localizer[SharedResourcesKey.CurriculumParseResultInvalid].Value;
            MarkDocumentFailed(document, safeReason);
            return;
        }

        // BE-3: set document parse fields.
        document.Status                  = DocumentStatus.Done;
        document.StatusReason            = null;
        document.ParsedArtifactObjectKey = result.ArtifactKey;
        document.ParsedAt                = DateTimeOffset.UtcNow;

        _logger.LogInfo(
            $"ParseJobAdvanceService: document id={document.Id} parse Done. " +
            $"artifactKey={result.ArtifactKey} chapters={result.Chapters.Count}.");

        // BE-6: build provenance tree.
        await BuildProvenanceTreeAsync(db, document, result, ct);

        // Q8: parse→ingest hand-off (BL-05 BE-13 decision).
        // Enqueue a 'Pending' ingest job immediately after a successful parse-Done advance.
        // The ingest job is picked up by IngestJobAdvanceService once the Python ingest worker
        // processes it and writes 'Done'/'Failed' back to this same PipelineJobs outbox.
        // Idempotency: if an ingest job already exists for this document at Pending/Processing/Done,
        // we still enqueue — re-ingest is controlled by the ReIngest command (BE-9), not here.
        // The payload carries the artifact key so the Python ingest worker knows what to process.
        var ingestPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            artifact_key = result.ArtifactKey,
            document_id  = document.Id,
        });

        db.PipelineJobs.Add(new PipelineJob
        {
            JobType     = "ingest",
            Status      = "Pending",
            DocumentId  = document.Id,
            PayloadJson = ingestPayload,
            RetryCount  = 0,
        });

        document.IngestionStatus = IngestionStatus.InProgress;

        _logger.LogInfo(
            $"ParseJobAdvanceService Q8: enqueued ingest PipelineJob for document id={document.Id} " +
            $"artifactKey={result.ArtifactKey}.");
    }

    // ── BE-6: provenance tree builder ─────────────────────────────────────────────────────────────

    /// <summary>
    /// BE-6: Creates/upserts a <see cref="ContentSource"/> + <see cref="Chapter"/> rows.
    ///
    /// <para>Content type mapping (brief §Handoff):
    /// <c>image/*</c> → Image; PDF/DOCX → Book; other → Worksheet.</para>
    ///
    /// <para><strong>Idempotency:</strong>
    /// A <see cref="ContentSource"/> keyed on <c>(GradeId, Title=FileName)</c> is looked up first.
    /// Chapters are upserted on <c>(ContentSourceId, Number)</c>.</para>
    ///
    /// <para><strong>Chapter title safety:</strong>
    /// Titles derived from <c>ResultJson</c> (untrusted) are truncated to
    /// <see cref="MaxChapterTitleLength"/> chars with an ellipsis marker before assignment.
    /// This is safe because chapter titles are free text — a truncated title is still useful.</para>
    /// </summary>
    private async Task BuildProvenanceTreeAsync(
        CurriculumDbContext db,
        CurriculumDocument document,
        ParseArtifactResult result,
        CancellationToken ct)
    {
        if (result.Chapters.Count == 0)
        {
            _logger.LogInfo(
                $"ParseJobAdvanceService BE-6: no chapters for document id={document.Id}; skipping provenance tree.");
            return;
        }

        var sourceType = MapContentType(document.ContentType);

        // Idempotent: look up ContentSource by (GradeId, Title=FileName).
        var existingSource = await db.ContentSources
            .Where(cs => cs.GradeId == document.GradeId && cs.Title == document.FileName)
            .Include(cs => cs.Chapters)
            .FirstOrDefaultAsync(ct);

        ContentSource contentSource;
        if (existingSource is null)
        {
            contentSource = new ContentSource
            {
                Type     = sourceType,
                Title    = document.FileName,
                GradeId  = document.GradeId,
                Language = document.Language,
            };
            db.ContentSources.Add(contentSource);
            // Flush to get a ContentSource.Id before inserting chapters.
            await db.SaveChangesAsync();
        }
        else
        {
            contentSource = existingSource;
        }

        // Upsert chapters on (ContentSourceId, Number).
        // Chapter titles from ResultJson are truncated (with ellipsis) to the column max.
        foreach (var ch in result.Chapters)
        {
            var safeTitle = TruncateWithEllipsis(ch.Title, MaxChapterTitleLength);

            var existing = contentSource.Chapters
                .FirstOrDefault(c => c.Number == ch.Number);

            if (existing is null)
            {
                db.Chapters.Add(new Chapter
                {
                    ContentSourceId = contentSource.Id,
                    Number          = ch.Number,
                    Title           = safeTitle,
                    PageStart       = ch.PageStart,
                    PageEnd         = ch.PageEnd,
                });
            }
            else
            {
                existing.Title     = safeTitle;
                existing.PageStart = ch.PageStart;
                existing.PageEnd   = ch.PageEnd;
            }
        }

        _logger.LogInfo(
            $"ParseJobAdvanceService BE-6: provenance tree for document id={document.Id} " +
            $"upserted {result.Chapters.Count} chapter(s), ContentSource id={contentSource.Id}.");
    }

    private static ContentSourceType MapContentType(string contentType) =>
        contentType switch
        {
            var ct when ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                => ContentSourceType.Image,
            var ct when ct.Contains("pdf", StringComparison.OrdinalIgnoreCase)
                     || ct.Contains("word", StringComparison.OrdinalIgnoreCase)
                     || ct.Contains("openxmlformats", StringComparison.OrdinalIgnoreCase)
                => ContentSourceType.Book,
            _ => ContentSourceType.Worksheet,
        };

    // ── BE-7: retry policy (Failed path) ─────────────────────────────────────────────────────────

    private async Task AdvanceFailedJobAsync(
        CurriculumDbContext db,
        PipelineJob job,
        IStringLocalizer<SharedResources> localizer,
        CancellationToken ct)
    {
        var document     = job.Document;
        var newRetryCount = job.RetryCount + 1;

        if (newRetryCount <= _config.MaxRetries)
        {
            // Re-enqueue: insert a fresh Pending parse job with the same payload.
            db.PipelineJobs.Add(new PipelineJob
            {
                JobType     = "parse",
                Status      = "Pending",
                DocumentId  = document.Id,
                PayloadJson = job.PayloadJson,
                RetryCount  = newRetryCount,
            });

            document.Status       = DocumentStatus.Processing;
            document.StatusReason = null;

            _logger.LogInfo(
                $"ParseJobAdvanceService: document id={document.Id} parse failed " +
                $"(attempt {newRetryCount}/{_config.MaxRetries}); re-enqueuing.");
        }
        else
        {
            // Dead-letter: exhausted retries.
            // Surface the Python-supplied ErrorMessage (sanitized + bounded to the column max)
            // as the StatusReason, or fall back to a localized generic reason when it is absent.
            var reason = !string.IsNullOrWhiteSpace(job.ErrorMessage)
                ? TruncateWithEllipsis(
                    SanitizeControlChars(job.ErrorMessage),
                    MaxStatusReasonLength)
                : localizer[SharedResourcesKey.CurriculumDocumentParseProcessingFailed].Value;

            MarkDocumentFailed(document, reason);

            _logger.LogError(null,
                $"ParseJobAdvanceService: document id={document.Id} PERMANENTLY FAILED after " +
                $"{_config.MaxRetries} retries. jobId={job.Id}. Error: {job.ErrorMessage}");
        }
    }

    private static void MarkDocumentFailed(CurriculumDocument document, string reason)
    {
        document.Status       = DocumentStatus.Failed;
        document.StatusReason = reason;
    }

    // ── ResultJson deserialization + validation ───────────────────────────────────────────────────

    /// <summary>
    /// Deserializes and validates <c>ResultJson</c> written by the Python worker.
    ///
    /// <para>All derived strings are checked against their DB column maxes:
    /// <list type="bullet">
    ///   <item><c>artifact_key</c> &gt; <see cref="MaxArtifactKeyLength"/> → throws
    ///         <see cref="ParseResultValidationException"/> (REJECT — do not truncate to a wrong key).</item>
    ///   <item><c>chapters[].title</c> &gt; <see cref="MaxChapterTitleLength"/> → recorded as-is here;
    ///         caller truncates before DB assignment.</item>
    /// </list>
    /// Missing/null/extra fields are handled gracefully — no unhandled exception on malformed JSON.</para>
    /// </summary>
    /// <exception cref="ParseResultValidationException">
    /// Thrown when the JSON is malformed, a required field is missing/null, or a field value
    /// violates a DB column constraint (e.g., artifact_key too long).
    /// </exception>
    private static ParseArtifactResult DeserializeAndValidateResult(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            throw new ParseResultValidationException("ResultJson is null or empty.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(resultJson);
        }
        catch (JsonException ex)
        {
            throw new ParseResultValidationException($"ResultJson is not valid JSON: {ex.Message}", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            // ── artifact_key ─────────────────────────────────────────────────────────────────────
            // Required. REJECT if missing, null, empty, or over-long (an oversized/truncated key
            // points to the wrong storage object — this is a hard error, not a free-text truncation).
            if (!root.TryGetProperty("artifact_key", out var artifactKeyEl)
                || artifactKeyEl.ValueKind == JsonValueKind.Null)
            {
                throw new ParseResultValidationException(
                    "artifact_key is missing or null in ResultJson.");
            }

            var artifactKey = artifactKeyEl.GetString();
            if (string.IsNullOrWhiteSpace(artifactKey))
                throw new ParseResultValidationException(
                    "artifact_key is empty or whitespace in ResultJson.");

            if (artifactKey.Length > MaxArtifactKeyLength)
                throw new ParseResultValidationException(
                    $"artifact_key length {artifactKey.Length} exceeds the DB column maximum of {MaxArtifactKeyLength} chars. " +
                    "Rejecting to prevent storing a corrupted object key.");

            // ── chapters ─────────────────────────────────────────────────────────────────────────
            // Optional. Missing/null treated as empty list.  Per-chapter titles are returned
            // as-is here; the caller (BuildProvenanceTreeAsync) truncates before DB assignment.
            var chapters = new List<ParsedChapterItem>();
            if (root.TryGetProperty("chapters", out var chaptersEl)
                && chaptersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var ch in chaptersEl.EnumerateArray())
                {
                    int number;
                    try
                    {
                        number = ch.GetProperty("number").GetInt32();
                    }
                    catch (Exception ex)
                    {
                        throw new ParseResultValidationException(
                            $"A chapter entry has a missing or non-integer 'number' field: {ex.Message}", ex);
                    }

                    var title = ch.TryGetProperty("title", out var titleEl)
                        ? titleEl.GetString() ?? string.Empty
                        : string.Empty;

                    var pageStart = ch.TryGetProperty("page_start", out var ps)
                                   && ps.ValueKind == JsonValueKind.Number
                        ? ps.GetInt32() : (int?)null;
                    var pageEnd   = ch.TryGetProperty("page_end", out var pe)
                                   && pe.ValueKind == JsonValueKind.Number
                        ? pe.GetInt32() : (int?)null;

                    chapters.Add(new ParsedChapterItem(number, title, pageStart, pageEnd));
                }
            }

            // ── parse_status ─────────────────────────────────────────────────────────────────────
            var parseStatus = root.TryGetProperty("parse_status", out var statusEl)
                ? statusEl.GetString() ?? "done"
                : "done";

            // ── diagnostics ──────────────────────────────────────────────────────────────────────
            var diagnostics = root.TryGetProperty("diagnostics", out var diagEl)
                              && diagEl.ValueKind != JsonValueKind.Null
                ? diagEl.GetString()
                : null;

            return new ParseArtifactResult(artifactKey, chapters, parseStatus, diagnostics);
        }
    }

    // ── String safety helpers ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <paramref name="value"/> unchanged if its length is within <paramref name="maxLength"/>;
    /// otherwise truncates to <c>maxLength - 1</c> chars and appends <see cref="Ellipsis"/>.
    ///
    /// <para>Used for free-text fields where a truncated value is still informative
    /// (e.g., chapter titles, StatusReason from Python ErrorMessage).
    /// NOT used for storage keys — use REJECT for those.</para>
    /// </summary>
    private static string TruncateWithEllipsis(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        // Append ellipsis within the budget.
        return value[..(maxLength - Ellipsis.Length)] + Ellipsis;
    }

    /// <summary>
    /// Strips ASCII control characters (U+0000–U+001F, U+007F) from <paramref name="value"/>.
    /// Applied to untrusted strings from the Python worker before persisting to the DB.
    /// Printable Unicode and whitespace (space, tab, newline) are kept.
    /// </summary>
    private static string SanitizeControlChars(string value) =>
        Regex.Replace(value, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", string.Empty);

    // ── Private DTO for the claim CTE result ─────────────────────────────────────────────────────

    /// <summary>Holds the result of the claim CTE (Id + OriginalStatus before the UPDATE).</summary>
    private sealed record ClaimedJobInfo
    {
        // EF Core SqlQueryRaw maps by column name (case-sensitive on Npgsql).
        // The CTE returns "Id" and "OriginalStatus" aliases.
        public int Id { get; init; }
        public string OriginalStatus { get; init; } = string.Empty;
    }

    // ── Domain exception for ResultJson validation failures ───────────────────────────────────────

    /// <summary>
    /// Thrown by <see cref="DeserializeAndValidateResult"/> when the <c>ResultJson</c> from
    /// the Python worker is malformed or violates a DB column constraint.
    ///
    /// <para>This is a domain-level validation exception, not an infrastructure exception.
    /// Callers catch it and transition the document to <c>Failed</c> without stranding
    /// the job at <c>Processing</c>.</para>
    /// </summary>
    private sealed class ParseResultValidationException : Exception
    {
        public ParseResultValidationException(string message) : base(message) { }
        public ParseResultValidationException(string message, Exception inner) : base(message, inner) { }
    }
}
