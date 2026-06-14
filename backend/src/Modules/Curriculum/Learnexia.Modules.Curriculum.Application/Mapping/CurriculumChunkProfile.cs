using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.Retrieval.Queries.RetrieveChunks;
using Learnexia.Modules.Curriculum.Domain.Entities;

namespace Learnexia.Modules.Curriculum.Application.Mapping;

/// <summary>
/// AutoMapper profile for <see cref="CurriculumChunk"/> → <see cref="CurriculumChunkDto"/>.
///
/// <para><strong>Security:</strong> the raw embedding (<c>ChunkEmbeddingBgeM3.Vector</c>) is
/// deliberately excluded from this mapping — it is never surfaced through any DTO or API response.
/// The <c>Score</c> field is computed by the handler (1 - cosine distance) and injected separately;
/// it is NOT derived from the entity graph.</para>
///
/// <para>The mapping from <c>CurriculumChunk</c> to <c>CurriculumChunkDto</c> is used as a
/// convenience reference; the actual handler uses a <c>Select</c> projection with the Score field
/// computed inline. This profile is registered here for completeness and future use.</para>
/// </summary>
public sealed class CurriculumChunkProfile : Profile
{
    public CurriculumChunkProfile()
    {
        // Entity → DTO (partial; Score is handler-computed from cosine distance).
        CreateMap<CurriculumChunk, CurriculumChunkDto>()
            .ConstructUsing(src => new CurriculumChunkDto(
                ChunkId:  src.Id,
                Content:  src.Content,
                Metadata: src.Metadata,
                Score:    0.0)); // Score is injected by the handler, not from the entity
    }
}
