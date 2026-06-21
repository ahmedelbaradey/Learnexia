namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;

/// <summary>
/// Response DTO for the <c>GET /api/Admin/AiSafety/evals</c> endpoint (P6-02 / P7-11-BE-3).
///
/// <para>Returns the latest offline eval run result from the committed artifact.
/// PII-light by design: no prompt/response text, no student identifiers —
/// only aggregate pass/fail metrics and a breach indicator.</para>
/// </summary>
public sealed record EvalResultsDto
{
    /// <summary>Unique identifier for this eval run (set at harness run time).</summary>
    public Guid RunId { get; init; }

    /// <summary>UTC timestamp when the harness wrote the artifact. Never <c>DateTime.Now</c>.</summary>
    public DateTime RanAt { get; init; }

    /// <summary>Total number of eval cases in the run.</summary>
    public int TotalCases { get; init; }

    /// <summary>Number of cases that passed.</summary>
    public int PassedCases { get; init; }

    /// <summary>Number of cases that failed.</summary>
    public int FailedCases { get; init; }

    /// <summary>Pass rate as a percentage (0–100).</summary>
    public double PassRate { get; init; }

    /// <summary>Fail rate as a percentage (0–100).</summary>
    public double FailRate { get; init; }

    /// <summary>Configured minimum pass-rate threshold (%).</summary>
    public double ThresholdPercent { get; init; }

    /// <summary>True when <see cref="PassRate"/> fell below <see cref="ThresholdPercent"/> — safety launch gate breached.</summary>
    public bool Breached { get; init; }

    /// <summary>Eval tier (e.g. "EvalOffline"; "EvalLive" for the devops launch-gate run).</summary>
    public string Tier { get; init; } = string.Empty;

    /// <summary>Human-readable note (breach detail or success message).</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>Per-check breakdown (check name → passed/total/passRate).</summary>
    public IReadOnlyDictionary<string, EvalCheckBreakdownDto> ByCheck { get; init; }
        = new Dictionary<string, EvalCheckBreakdownDto>();

    /// <summary>Per-subject breakdown (subject name → passed/total/passRate).</summary>
    public IReadOnlyDictionary<string, EvalCheckBreakdownDto> BySubject { get; init; }
        = new Dictionary<string, EvalCheckBreakdownDto>();

    /// <summary>Per-language breakdown (language code → passed/total/passRate).</summary>
    public IReadOnlyDictionary<string, EvalCheckBreakdownDto> ByLanguage { get; init; }
        = new Dictionary<string, EvalCheckBreakdownDto>();
}

/// <summary>Per-dimension pass/total breakdown entry in the eval results.</summary>
public sealed record EvalCheckBreakdownDto(int Passed, int Total, double PassRate);
