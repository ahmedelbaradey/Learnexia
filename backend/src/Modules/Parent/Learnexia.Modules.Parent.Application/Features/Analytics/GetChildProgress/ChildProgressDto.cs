using Learnexia.Shared.Contracts.Learning;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildProgress;

/// <summary>
/// E1 response DTO — child-level progress card shown on the parent dashboard "My Children" overview.
/// Covers all fields from <c>ChildStatsStub</c> in the FE gold contract.
/// </summary>
public sealed class ChildProgressDto
{
    /// <summary>The child's current XP level (1 for brand-new student).</summary>
    public int Level { get; init; }

    /// <summary>Total cumulative XP earned.</summary>
    public int TotalXp { get; init; }

    /// <summary>Current consecutive-day learning streak.</summary>
    public int CurrentStreak { get; init; }

    /// <summary>All-time best streak.</summary>
    public int BestStreak { get; init; }

    /// <summary>Overall mastery percentage across all practiced subjects (0–100).</summary>
    public int MasteryPercent { get; init; }

    /// <summary>
    /// The single weakest skill (highest-severity weak area).
    /// Null when the child has no weak areas yet.
    /// </summary>
    public WeakSkillDto? WeakestSkill { get; init; }

    /// <summary>Whether the child has completed at least one lesson today (UTC date).</summary>
    public bool ActiveToday { get; init; }

    /// <summary>Current energy balance snapshot (null-safe: zeroed when no billing data).</summary>
    public EnergySnapshotDto Energy { get; init; } = new();
}

/// <summary>Single weakest skill card shown on the progress view.</summary>
public sealed class WeakSkillDto
{
    public int SkillId { get; init; }
    public string SkillName { get; init; } = string.Empty;

    /// <summary>Subject code as int (0=MATH,1=SCIENCE,2=ARABIC,3=ENGLISH).</summary>
    public int SubjectCode { get; init; }
    public int MasteryPercent { get; init; }

    /// <summary>Severity level: 1=Low, 2=Medium, 3=High.</summary>
    public int Severity { get; init; }
}

/// <summary>Energy balance snapshot — mirrors <c>ChildEnergySnapshot</c> from the Billing seam.</summary>
public sealed class EnergySnapshotDto
{
    public int Remaining { get; init; }
    public int Allocated { get; init; }
    public int Spent { get; init; }
    public int PurchasedBalance { get; init; }
    public DateTime? CycleEndsAtUtc { get; init; }
}
