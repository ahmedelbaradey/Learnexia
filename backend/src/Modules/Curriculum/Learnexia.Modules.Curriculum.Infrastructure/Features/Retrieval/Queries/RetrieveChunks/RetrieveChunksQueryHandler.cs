using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Application.Features.Retrieval.Queries.RetrieveChunks;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.Retrieval.Queries.RetrieveChunks;

/// <summary>
/// Handles <see cref="RetrieveChunksQuery"/>: embeds the query text via <see cref="IEmbeddingProvider"/>
/// and performs a server-side pgvector cosine-distance JOIN across the three curriculum tables.
///
/// <para><strong>SQL translation verification:</strong>
/// <c>e.Vector.CosineDistance(queryVector)</c> is an instance/extension method on <c>Pgvector.Vector</c>
/// provided by <c>Pgvector.EntityFrameworkCore</c> (0.3.x). EF translates this to the
/// <c>&lt;=&gt;</c> operator in the generated SQL — never client-evaluated. The <c>ORDER BY</c>
/// and <c>LIMIT</c> are pushed to PostgreSQL, and the HNSW index on
/// <c>chunk_embeddings_bge_m3.Vector</c> with <c>vector_cosine_ops</c> is used by the
/// planner for approximate nearest-neighbour search.</para>
///
/// <para><strong>Similarity floor (OQ-7):</strong>
/// configured via <see cref="RetrievalSettings.SimilarityFloor"/>. The floor is expressed as
/// a <em>cosine distance</em> threshold (0 = identical, 2 = opposite).
/// Chunks whose cosine distance exceeds the floor are discarded — the returned list may be empty.
/// An empty list is a valid result (AC-4 "no context" signal), NOT an error.</para>
///
/// <para><strong>Queries skip ValidationBehavior</strong> — this handler guards inputs itself.</para>
/// </summary>
public sealed class RetrieveChunksQueryHandler
    : BaseResponseHandler, IQueryHandler<RetrieveChunksQuery, BaseResponse<RetrievalResult>>
{
    private readonly CurriculumDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILoggerManager _logger;
    private readonly RetrievalSettings _settings;

    public RetrieveChunksQueryHandler(
        CurriculumDbContext db,
        IEmbeddingProvider embeddingProvider,
        ILoggerManager logger,
        IOptions<RetrievalSettings> settings)
    {
        _db                = db;
        _embeddingProvider = embeddingProvider;
        _logger            = logger;
        _settings          = settings.Value;
    }

    public async Task<BaseResponse<RetrievalResult>> Handle(
        RetrieveChunksQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Input guards (queries skip ValidationBehavior).
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                _logger.LogWarn("P3-07 RetrieveChunksQuery: Text is empty; returning empty result.");
                return Success(new RetrievalResult([]));
            }

            if (request.GradeId < 0)
            {
                _logger.LogWarn(
                    $"P3-07 RetrieveChunksQuery: Invalid GradeId={request.GradeId}. Returning empty.");
                return Success(new RetrievalResult([]));
            }

            var topK = request.TopK > 0 ? request.TopK : 5;

            // Step 1: embed the query text. Null = provider unavailable → empty result (fail-soft).
            var queryVector = await _embeddingProvider.EmbedAsync(request.Text, cancellationToken);
            if (queryVector is null)
            {
                _logger.LogWarn("P3-07 RetrieveChunksQuery: embedding provider returned null; returning empty result.");
                return Success(new RetrievalResult([]));
            }

            // Step 2: server-side pgvector JOIN query.
            // EF.Functions.CosineDistance translates to the <=> operator in SQL (never client-eval).
            // ORDER BY pushes to PostgreSQL; HNSW index on Vector with vector_cosine_ops is used by planner.
            // CurriculumVersion.Status = Active is the visibility gate (Decision C).
            var capturedVector = queryVector; // capture for lambda
            var query = _db.ChunkEmbeddingsBgeM3
                .AsNoTracking()
                .Where(e => e.IsActive)
                .Join(
                    _db.CurriculumChunks.AsNoTracking(),
                    e => e.ChunkId,
                    c => c.Id,
                    (e, c) => new { Embedding = e, Chunk = c })
                .Join(
                    _db.CurriculumVersions.AsNoTracking()
                        .Where(v => v.Status == CurriculumVersionStatus.Active),
                    ec => ec.Chunk.CurriculumVersionId,
                    v => v.Id,
                    (ec, v) => new { ec.Embedding, ec.Chunk, Version = v })
                .Where(ecv =>
                    (request.GradeId == 0 || ecv.Chunk.GradeId == request.GradeId) &&
                    (request.SubjectId == 0 || ecv.Chunk.SubjectId == request.SubjectId))
                .Where(ecv =>
                    // WeakAreaSkillId filter: include chunks that either have no SkillId tag (general
                    // curriculum chunks applicable to any skill) or are explicitly tagged for this skill.
                    // Seeded chunks have SkillId = null (general); BL-05 backfills specific SkillId values.
                    request.WeakAreaSkillId == null ||
                    ecv.Chunk.SkillId == null ||
                    ecv.Chunk.SkillId == request.WeakAreaSkillId);

            // Apply vector ordering and limit server-side (translated to SQL <=> and LIMIT).
            // CosineDistance is an instance/extension method on Pgvector.Vector (Pgvector.EntityFrameworkCore 0.3.x).
            // It translates to the <=> operator in the generated SQL — never client-evaluated.
            // The HNSW index on chunk_embeddings_bge_m3.Vector with vector_cosine_ops is used by the planner.
            var rawResults = await query
                .OrderBy(ecv => ecv.Embedding.Vector.CosineDistance(capturedVector))
                .Take(topK)
                .Select(ecv => new
                {
                    ecv.Chunk.Id,
                    ecv.Chunk.Content,
                    ecv.Chunk.Metadata,
                    Distance = ecv.Embedding.Vector.CosineDistance(capturedVector),
                })
                .ToListAsync(cancellationToken);

            // Step 3: apply similarity floor (in .NET — the distance values are already materialized).
            // Floor is a cosine DISTANCE threshold; reject rows where distance > floor.
            // Convert distance to similarity score: score = 1 - distance (range [0, 1]).
            var similarityFloor = _settings.SimilarityDistanceFloor;
            var chunks = rawResults
                .Where(r => r.Distance <= similarityFloor)
                .Select(r => new CurriculumChunkDto(
                    ChunkId:  r.Id,
                    Content:  r.Content,
                    Metadata: r.Metadata,
                    Score:    1.0 - r.Distance))
                .ToList();

            _logger.LogInfo(
                $"P3-07 RetrieveChunksQuery: grade={request.GradeId} subject={request.SubjectId} " +
                $"skill={request.WeakAreaSkillId} topK={topK} rawCount={rawResults.Count} " +
                $"passedFloor={chunks.Count} floor={similarityFloor}");

            return Success(new RetrievalResult(chunks));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "P3-07 RetrieveChunksQuery: unhandled error during retrieval.");
            return ServerError<RetrievalResult>();
        }
    }
}
