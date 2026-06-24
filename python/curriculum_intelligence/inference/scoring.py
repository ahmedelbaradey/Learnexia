"""Strength + confidence scoring for inferred edges (BL-03-PY-3).

IMPORTANT (lead decision / Decision E): the Python worker EMITS 0..1 ``strength``
and ``confidence`` signals — it NEVER gates on them. The .NET
``EdgeInferenceAdvanceService`` RE-CLAMPS both to [0,1] (mirror ``ClampConfidence``)
and admin approval is the publish gate, so these values are advisory only.

What this module does:
- ``clamp`` — normalize a raw signal into [0, 1] (mirror ``ingestion/confidence.clamp``).
- ``score_prerequisite_strength`` — a deterministic strength heuristic for a
  prerequisite edge, derived from the difficulty + depth gap between the two nodes,
  so the mock backend produces stable, plausible, monotonic strengths without any
  network/LLM call. Used by the mock inferer; a live backend supplies its own.
"""

from __future__ import annotations


def clamp(value: float) -> float:
    """Clamp a raw strength/confidence signal into [0.0, 1.0]."""

    try:
        v = float(value)
    except (TypeError, ValueError):
        return 0.0
    if v < 0.0:
        return 0.0
    if v > 1.0:
        return 1.0
    return v


def score_prerequisite_strength(
    source_difficulty: int | None,
    target_difficulty: int | None,
    *,
    sequence_gap: int = 1,
) -> float:
    """Deterministic prerequisite-strength heuristic in [0, 1].

    A prerequisite link is STRONGER when the target is only slightly harder than
    the source and immediately downstream (small difficulty + sequence gap), and
    WEAKER as the gap widens (the relationship is more indirect). Difficulties
    default to a mid value (3 on the 1..5 scale) when unknown.

    The formula is intentionally simple + monotonic so it is fully reproducible in
    CI (no randomness, no model). A real LightRAG/LLM backend overrides this with
    its own confidence-derived strength.
    """

    src = source_difficulty if source_difficulty is not None else 3
    tgt = target_difficulty if target_difficulty is not None else 3
    diff_gap = abs(int(tgt) - int(src))
    gap = max(1, int(sequence_gap))
    # Start high for an immediate, single-step prerequisite; decay with both gaps.
    strength = 0.9 - 0.12 * (gap - 1) - 0.05 * diff_gap
    return round(clamp(strength), 4)
