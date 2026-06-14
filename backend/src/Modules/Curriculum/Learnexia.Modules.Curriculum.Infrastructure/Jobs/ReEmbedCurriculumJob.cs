using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Curriculum.Infrastructure.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;

namespace Learnexia.Modules.Curriculum.Infrastructure.Jobs;

/// <summary>
/// Hangfire job — re-embeds all <c>chunk_embeddings_bge_m3</c> rows whose
/// <c>ModelVersion</c> does not match the configured active model version, replacing placeholder
/// or stale vectors with real BGE-M3 vectors from the self-hosted TEI endpoint.
///
/// <para><strong>WI-A2 — Idempotent:</strong>
/// Only processes rows where <c>ModelVersion != activeModelVersion</c>. Rows already stamped
/// with the active version are skipped. Re-running the job when all rows are up-to-date is a
/// safe no-op (zero DB writes, zero TEI calls).</para>
///
/// <para><strong>Parity stamp:</strong>
/// Each updated row is stamped with the configured <c>EmbeddingSettings.Model</c>,
/// <c>EmbeddingSettings.ModelVersion</c>, and provider identifier <c>"bge-m3-tei"</c>.
/// The retrieval query reads <c>IsActive = true</c>; both old and new rows keep <c>IsActive = true</c>
/// (there is one row per chunk — we UPDATE in place rather than INSERT + deactivate old rows,
/// matching the seeder's one-row-per-chunk invariant).</para>
///
/// <para><strong>Graceful degrade:</strong>
/// If <see cref="IEmbeddingProvider"/> returns <c>null</c> for a chunk (TEI failure, transient
/// network error), the row is skipped for this run and will be picked up on the next run.
/// A partial run leaves the remaining placeholder rows for the next invocation.</para>
///
/// <para><strong>Batch size:</strong>
/// Processes chunks in batches of <see cref="BatchSize"/> to bound memory usage and allow
/// partial progress on large corpora. Each batch commits independently (no UoW) per ADR-0001.</para>
///
/// <para>Mirrors <c>GamificationCacheRebuildJob</c>:
/// registered Transient; uses <see cref="IServiceScopeFactory"/> to create a fresh DI scope
/// so the Scoped <see cref="CurriculumDbContext"/> and <see cref="IEmbeddingProvider"/> are
/// always resolved in a non-HTTP-request scope.</para>
/// </summary>
public sealed class ReEmbedCurriculumJob
{
    private const int BatchSize = 50;
    private const string ActiveProvider = "bge-m3-tei";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;

    public ReEmbedCurriculumJob(
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Executes the re-embed pass. Scans <c>chunk_embeddings_bge_m3</c> for rows whose
    /// <c>ModelVersion</c> differs from the configured active version and re-embeds them
    /// via <see cref="IEmbeddingProvider.EmbedAsync"/>.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger.LogInfo("P3-07 ReEmbedCurriculumJob: starting re-embed pass.");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var embeddingProvider = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();
            var db                = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

            // Resolve the active model/version from the concrete BgeM3EmbeddingProvider.
            // The provider exposes these as internal properties so the job can stamp correctly.
            // If the provider is not fully configured, abort early with a clear log — no DB writes.
            if (embeddingProvider is not BgeM3EmbeddingProvider bgeProvider || !bgeProvider.IsConfigured)
            {
                _logger.LogWarn(
                    "P3-07 ReEmbedCurriculumJob: embedding provider is not configured (BaseUrl or ModelVersion missing). " +
                    "Job will not proceed. Configure Curriculum:Embedding:BaseUrl + ModelVersion and re-trigger.");
                return;
            }

            var activeModelVersion = bgeProvider.ActiveModelVersion;

            // Count stale rows before processing for summary logging.
            var staleCount = await db.ChunkEmbeddingsBgeM3
                .AsNoTracking()
                .CountAsync(e => e.ModelVersion != activeModelVersion, ct);

            if (staleCount == 0)
            {
                _logger.LogInfo(
                    $"P3-07 ReEmbedCurriculumJob: all rows are already stamped with ModelVersion='{activeModelVersion}'. " +
                    "Nothing to re-embed (idempotent no-op).");
                return;
            }

            _logger.LogInfo(
                $"P3-07 ReEmbedCurriculumJob: found {staleCount} rows to re-embed " +
                $"(ModelVersion != '{activeModelVersion}'). Processing in batches of {BatchSize}.");

            var processedCount = 0;
            var skippedCount   = 0;

            // Process in batches to avoid loading entire corpus into memory.
            // Each batch is a separate DB round-trip + commit (no UoW per ADR-0001).
            while (!ct.IsCancellationRequested)
            {
                // Re-fetch the next batch each iteration so we always process fresh stale rows.
                // Include the CurriculumChunk navigation so we have the chunk Content for embedding.
                var batch = await db.ChunkEmbeddingsBgeM3
                    .Include(e => e.Chunk)
                    .Where(e => e.ModelVersion != activeModelVersion)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (batch.Count == 0)
                    break;

                var processedBeforeBatch = processedCount;

                foreach (var embeddingRow in batch)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    var content = embeddingRow.Chunk?.Content;
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        _logger.LogWarn(
                            $"P3-07 ReEmbedCurriculumJob: ChunkEmbeddingBgeM3.Id={embeddingRow.Id} " +
                            "has no associated chunk content; skipping.");
                        skippedCount++;
                        continue;
                    }

                    Vector? vector;
                    try
                    {
                        vector = await embeddingProvider.EmbedAsync(content, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            $"P3-07 ReEmbedCurriculumJob: EmbedAsync threw for ChunkEmbeddingBgeM3.Id={embeddingRow.Id}; " +
                            "skipping this row (will retry on next run).");
                        skippedCount++;
                        continue;
                    }

                    if (vector is null)
                    {
                        // Fail-soft: TEI unavailable or transient error. Skip and retry next run.
                        _logger.LogWarn(
                            $"P3-07 ReEmbedCurriculumJob: EmbedAsync returned null for ChunkEmbeddingBgeM3.Id={embeddingRow.Id}; " +
                            "skipping (will retry on next run).");
                        skippedCount++;
                        continue;
                    }

                    // Update in place: stamp the real vector + active model metadata.
                    embeddingRow.Vector       = vector;
                    embeddingRow.Provider     = ActiveProvider;
                    embeddingRow.Model        = bgeProvider.ActiveModel;
                    embeddingRow.ModelVersion = activeModelVersion;

                    // Commit each row immediately (no UoW per ADR-0001).
                    await db.SaveChangesAsync(CurriculumChunkSeeder.SystemUserId);
                    processedCount++;
                }

                // No-progress guard: if an entire batch was skipped (persistent EmbedAsync
                // null/throw — e.g. TEI down or misconfigured), the same stale rows would
                // re-fetch every iteration and loop forever. Stop and let the admin re-trigger
                // once the endpoint is healthy (already-stamped rows are skipped on the next run).
                if (processedCount == processedBeforeBatch)
                {
                    _logger.LogWarn(
                        $"P3-07 ReEmbedCurriculumJob: a full batch of {batch.Count} rows made no progress " +
                        "(all skipped — embedding endpoint likely unavailable). Stopping; re-trigger when healthy.");
                    break;
                }
            }

            _logger.LogInfo(
                $"P3-07 ReEmbedCurriculumJob: completed. processed={processedCount} skipped={skippedCount} " +
                $"activeModelVersion='{activeModelVersion}'.");
        }
        catch (Exception ex)
        {
            // Outer fail-soft: job failure is logged but never re-thrown so Hangfire does not
            // retry endlessly. The admin can re-trigger via POST /api/Admin/Curriculum/ReEmbed.
            _logger.LogError(ex, "P3-07 ReEmbedCurriculumJob: unhandled error — job failed.");
        }
    }
}
