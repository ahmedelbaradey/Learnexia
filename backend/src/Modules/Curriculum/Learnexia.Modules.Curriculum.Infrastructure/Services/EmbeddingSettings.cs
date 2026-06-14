namespace Learnexia.Modules.Curriculum.Infrastructure.Services;

/// <summary>
/// Configuration options for the BGE-M3 TEI (Text-Embeddings-Inference) endpoint.
/// Bind from <c>Curriculum:Embedding</c> in appsettings.
///
/// <para><strong>Parity requirement (FINAL LOCKED AI ARCHITECTURE):</strong>
/// <see cref="Model"/> and <see cref="ModelVersion"/> MUST match the values stamped on
/// <c>chunk_embeddings_bge_m3</c> rows (seeded by <c>CurriculumChunkSeeder</c>).
/// A mismatch produces incompatible vector spaces — the runtime embedding and the seeded
/// embeddings exist in different metric spaces, making cosine distances meaningless.
/// <c>BgeM3EmbeddingProvider</c> logs a parity-guard warning when mismatch is detected.</para>
///
/// <para><strong>Security:</strong> <see cref="AuthToken"/> is a secret — never commit a
/// non-empty value to the repository. Set via environment variable or secret manager.</para>
/// </summary>
public sealed class EmbeddingSettings
{
    public const string SectionName = "Curriculum:Embedding";

    /// <summary>
    /// Base URL of the self-hosted BGE-M3 TEI endpoint (e.g. "http://hetzner-host:8080").
    /// When empty or absent the provider logs a warning and returns null for every call.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Optional Bearer token for TEI endpoint authentication.
    /// Never committed — inject via environment variable.
    /// </summary>
    public string AuthToken { get; init; } = string.Empty;

    /// <summary>
    /// The embedding model slug served by the TEI endpoint (e.g. "bge-m3").
    /// Stamped on <c>chunk_embeddings_bge_m3.Model</c>; must match the seeder's
    /// <see cref="DeterministicEmbedding.PlaceholderModel"/> to pass the parity guard
    /// once real BGE-M3 replaces placeholder embeddings.
    /// </summary>
    public string Model { get; init; } = "bge-m3";

    /// <summary>
    /// The pinned TEI model version (e.g. "1.0").
    /// Stamped on <c>chunk_embeddings_bge_m3.ModelVersion</c>.
    /// </summary>
    public string ModelVersion { get; init; } = string.Empty;
}
