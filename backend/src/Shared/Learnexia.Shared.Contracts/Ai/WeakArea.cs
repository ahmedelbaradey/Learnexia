namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Represents a curriculum area where a student has demonstrated weakness.
/// Produced by P3-09 (student mastery / weak-area computation).
/// Used by the prompt builder (P3-03) to personalise hints and explanations.
/// </summary>
/// <param name="SkillId">The skill identifier from the curriculum graph.</param>
/// <param name="SkillName">Human-readable skill label (localized per language).</param>
/// <param name="MasteryScore">
/// Normalised mastery score [0, 1]. A score below the module's weak-area threshold
/// identifies this as a weak area. Provided here so templates can optionally surface it.
/// </param>
public sealed record WeakArea(int SkillId, string SkillName, double MasteryScore);
