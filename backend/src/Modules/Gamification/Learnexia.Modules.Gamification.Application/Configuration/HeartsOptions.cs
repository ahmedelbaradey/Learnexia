using Learnexia.Modules.Gamification.Application.Common;

namespace Learnexia.Modules.Gamification.Application.Configuration;

/// <summary>
/// Configuration for the hearts engine (P4-04). Bound from
/// <c>appsettings.json:Gamification:Hearts</c>. Mirrors <see cref="StreakOptions"/> shape.
///
/// Defaults:
///   <see cref="Cap"/> = 5 — standard Duolingo-style cap.
///   <see cref="RefillIntervalMinutes"/> = 30 — one heart refills every 30 minutes.
///   <see cref="RegenOnCorrectAnswerInPracticeMode"/> = true — a correct answer while at 0 hearts
///     restores +1 heart ("practice your way out" UX — D2).
/// </summary>
public sealed class HeartsOptions
{
    /// <summary>Config section key — <c>"Gamification:Hearts"</c>.</summary>
    public const string SectionName = "Gamification:Hearts";

    /// <summary>Maximum hearts a student may hold at once. Default = 5.</summary>
    public int Cap { get; set; } = GamificationConstants.Hearts.Cap;

    /// <summary>Minutes required for one heart to regenerate. Default = 30.</summary>
    public int RefillIntervalMinutes { get; set; } = GamificationConstants.Hearts.RefillIntervalMinutes;

    /// <summary>
    /// When true (default), a correct answer while in Practice Mode (<c>Hearts == 0</c>)
    /// restores +1 heart instead of awarding XP. Toggle via appsettings for A/B experiments.
    /// </summary>
    public bool RegenOnCorrectAnswerInPracticeMode { get; set; } = true;
}
