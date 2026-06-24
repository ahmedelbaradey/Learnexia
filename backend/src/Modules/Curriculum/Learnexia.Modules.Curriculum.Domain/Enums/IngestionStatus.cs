namespace Learnexia.Modules.Curriculum.Domain.Enums;

/// <summary>
/// Ingestion-axis lifecycle of a <see cref="Entities.CurriculumDocument"/>.
/// Stored as int in the database via HasConversion&lt;int&gt;() — this is the .NET-internal
/// ingestion lifecycle and is NEVER read by the Python poller (the poller reads PipelineJob.Status,
/// which is a STRING). Do NOT convert this to a string column.
///
/// This is deliberately SEPARATE from <see cref="DocumentStatus"/>, which tracks the parse axis.
/// Parse and ingest are distinct pipeline stages — a document can be parse-Done before ingestion
/// starts, and can be re-ingested independently of re-parsing.
///
/// Values: NotStarted=0, InProgress=1, Done=2, Failed=3.
/// (BL-05 BE-1)
/// </summary>
public enum IngestionStatus
{
    NotStarted = 0,
    InProgress = 1,
    Done       = 2,
    Failed     = 3,
}
