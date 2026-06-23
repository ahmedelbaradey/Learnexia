using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Curriculum.Domain.Entities;

/// <summary>
/// PipelineJobs is the DB-outbox seam consumed by the Python poller (curriculum-system-of-record.md §4b).
/// JobType/Status are STRINGS — a cross-process contract; do not convert to int enums.
///
/// Cross-process seam between .NET and the Python curriculum-intelligence service.
/// .NET writes Pending rows; Python polls, claims atomically
/// (UPDATE ... WHERE Status='Pending'), and writes results. .NET BackgroundService advances
/// document status on Done/Failed. No external broker required.
/// See curriculum-system-of-record.md §4b for the upgrade path (broker swap).
///
/// Design rules:
/// - <see cref="Id"/> is int identity (module consistency; Q4 resolution).
/// - <see cref="JobType"/> and <see cref="Status"/> are strings — cross-process contract.
///   Valid JobType values: 'parse', 'ingest', 'embed'.
///   Valid Status values: 'Pending', 'Processing', 'Done', 'Failed', 'PermanentlyFailed'.
/// - <see cref="DocumentId"/> is an intra-module FK → CurriculumDocuments.Id (same schema — FK allowed).
///   ON DELETE CASCADE: deleting a document removes its jobs.
/// - <see cref="PayloadJson"/> carries only object_key + content_type — no secrets, no unsanitized client input.
/// - <c>CreatedAt</c> is inherited as <c>DateTime</c> from <c>CreationAuditedEntity</c> (module convention;
///   all existing curriculum entities use the base DateTime CreatedAt — do not shadow it).
/// </summary>
public class PipelineJob : AggregateRoot
{
    /// <summary>
    /// Type of work to be performed. String — cross-process contract with Python.
    /// Values: 'parse' | 'ingest' | 'embed'.
    /// </summary>
    public string JobType { get; set; } = null!;

    /// <summary>
    /// Current status of this job. String — cross-process contract with Python poller.
    /// Values: 'Pending' | 'Processing' | 'Done' | 'Failed' | 'PermanentlyFailed'.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// FK → CurriculumDocuments.Id. Intra-module ref (same curriculum schema — real FK is allowed).
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// JSON payload for the Python worker. Contains only object_key + content_type.
    /// e.g. {"object_key":"&lt;guid&gt;.pdf","content_type":"application/pdf"}.
    /// No secrets, no unsanitized client input.
    /// </summary>
    public string PayloadJson { get; set; } = null!;

    /// <summary>
    /// JSON output written by the Python worker on completion. Null until Done/Failed.
    /// </summary>
    public string? ResultJson { get; set; }

    /// <summary>
    /// Human-readable error message on failure. Null on success.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the Python worker claimed (locked) this job. Null until claimed.
    /// Set atomically by Python via UPDATE ... WHERE Status='Pending'.
    /// </summary>
    public DateTimeOffset? ClaimedAt { get; set; }

    /// <summary>
    /// When processing completed (Done or Failed). Null until finished.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Number of times this job has been attempted (retried). Default 0.
    /// Incremented by the Python poller on each claim attempt.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    // Navigation — owning document (intra-module FK, cascade delete).
    public CurriculumDocument Document { get; set; } = null!;
}
