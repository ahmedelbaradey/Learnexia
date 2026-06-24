"""Confidence scoring + low-confidence flagging (BL-05-PY-5).

IMPORTANT (lead decision): the Python worker EMITS a 0..1 ``confidence`` on every
node + chunk and ADVISORY ``flags`` — it NEVER applies the 0.7 cutoff or withholds
anything. The .NET ``IngestJobAdvanceService`` owns the threshold + review-routing
(it decides what goes to ``IngestionReviewItem``). The ``threshold`` passed here is
used ONLY to annotate advisory flags, so the .NET side has a hint; it does not gate
node/chunk emission.

What this module does:
- Normalizes/clamps confidences into [0, 1].
- Builds a ``flags[]`` list for nodes/chunks whose confidence is below the advisory
  threshold (``kind=low_confidence_node`` / ``low_confidence_chunk``), and for any
  chunk that could not be mapped to a node (``kind=unmapped_chunk``, confidence 0).
"""

from __future__ import annotations

from .models import Chunk, Flag, FlagKind, HierarchyNode


def clamp(value: float) -> float:
    """Clamp a raw confidence signal into [0.0, 1.0]."""

    try:
        v = float(value)
    except (TypeError, ValueError):
        return 0.0
    if v < 0.0:
        return 0.0
    if v > 1.0:
        return 1.0
    return v


def build_flags(
    nodes: list[HierarchyNode],
    chunks: list[Chunk],
    *,
    threshold: float,
) -> list[Flag]:
    """Emit advisory low-confidence flags. Does NOT mutate or drop inputs."""

    flags: list[Flag] = []
    for node in nodes:
        if node.confidence < threshold:
            flags.append(
                Flag(
                    kind=FlagKind.LOW_CONFIDENCE_NODE.value,
                    ref=node.skill_key,
                    confidence=node.confidence,
                    reason=(
                        f"node confidence {node.confidence:.2f} below advisory threshold " f"{threshold:.2f}"
                    ),
                )
            )
    for index, chunk in enumerate(chunks):
        if not chunk.node_skill_key:
            flags.append(
                Flag(
                    kind=FlagKind.UNMAPPED_CHUNK.value,
                    ref=str(index),
                    confidence=0.0,
                    reason="chunk could not be mapped to a hierarchy node",
                )
            )
        elif chunk.confidence < threshold:
            flags.append(
                Flag(
                    kind=FlagKind.LOW_CONFIDENCE_CHUNK.value,
                    ref=str(index),
                    confidence=chunk.confidence,
                    reason=(
                        f"chunk confidence {chunk.confidence:.2f} below advisory threshold "
                        f"{threshold:.2f}"
                    ),
                )
            )
    return flags
