namespace Learnexia.Modules.Curriculum.Domain.Enums;

/// <summary>
/// .NET-internal lifecycle of a <see cref="Entities.CurriculumDocument"/>.
/// Stored as int in the database via HasConversion&lt;int&gt;() — this is the .NET-internal doc
/// lifecycle and is NEVER read by the Python poller (the poller reads PipelineJob.Status,
/// which is a STRING — see PipelineJobConfig). Do NOT convert this to a string column.
///
/// Values: Received=0, Processing=1, Done=2, Failed=3.
/// (BL-01 BE-1, execution plan §Batch-1)
/// </summary>
public enum DocumentStatus
{
    Received   = 0,
    Processing = 1,
    Done       = 2,
    Failed     = 3,
}
