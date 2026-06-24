"""Deterministic, fixture-driven mock edge inferer — the DEFAULT infer backend.

It does NOT call LightRAG/Claude or any network service (mirrors ``MockExtractor``).
It proposes a stable, plausible set of Prerequisite + Related edges from the input
node list. Two modes:

- **Fixture mode:** if the request payload carried explicit ``edges`` (our test
  payloads / a curated chain), it echoes them straight through with deterministic
  scoring — this lets tests assert exact edges. (The pipeline passes these as
  ``InputNode`` plus a payload-level ``edges`` hint; see :class:`InferencePipeline`.)
- **Synthetic mode:** otherwise it derives edges from the nodes deterministically:
  Skill/Concept/Lesson nodes within the same subject+grade are ordered by
  (difficulty, skill_key) and chained as **Prerequisite** edges (each node is a
  prerequisite of the next harder one); Unit-level siblings get **Related** edges.

Determinism is the whole point: re-running it for the same node list yields the
same edges + scores, so the .NET idempotency check is stable.
"""

from __future__ import annotations

from .inferer import CandidateEdge, EdgeInferer, InferenceError, InferenceRequest, InputNode, RawInference
from .models import RelationshipType
from .scoring import clamp, score_prerequisite_strength

_MODEL = "lightrag-mock-v1"

# Node levels that participate in the prerequisite chain (skills/concepts/lessons),
# ordered fine-to-coarse; Units are containers and get Related sibling edges instead.
_CHAINABLE = {"Skill", "Concept", "Lesson"}


class MockEdgeInferer:
    """Implements :class:`EdgeInferer`."""

    name = "mock"

    def __init__(self, *, inference_model: str = _MODEL) -> None:
        self._model = inference_model

    def infer(self, request: InferenceRequest) -> RawInference:
        nodes = request.nodes
        if not nodes:
            return RawInference(
                inference_model=self._model,
                edges=[],
                diagnostics={"inferer": self.name, "mode": "synthetic", "node_count": 0},
            )

        # Guard: only the 4-subject domain (defense-in-depth; postprocessor also enforces).
        for n in nodes:
            subject = n.skill_key.split(".", 1)[0].strip().lower()
            if subject not in {"math", "science", "arabic", "english"}:
                raise InferenceError(
                    f"node skill_key {n.skill_key!r} not in the 4-subject domain",
                    code="invalid_subject",
                    backend=self.name,
                )

        edges = self._synthetic(nodes)
        return RawInference(
            inference_model=self._model,
            edges=edges,
            diagnostics={"inferer": self.name, "mode": "synthetic", "node_count": len(nodes)},
        )

    # --- synthetic path ------------------------------------------------------

    def _synthetic(self, nodes: list[InputNode]) -> list[CandidateEdge]:
        edges: list[CandidateEdge] = []

        # Group by (subject_code, grade) so we never chain across subjects/grades.
        groups: dict[tuple[str, int], list[InputNode]] = {}
        for n in nodes:
            groups.setdefault((n.subject_code.strip().lower(), int(n.grade)), []).append(n)

        for _key, group in groups.items():
            chainable = [n for n in group if n.node_type in _CHAINABLE]
            # Stable order: easier first (difficulty asc), tie-break by SkillKey.
            chainable.sort(key=lambda n: ((n.difficulty if n.difficulty is not None else 3), n.skill_key))

            # Prerequisite chain: each node is a prerequisite of the next harder one.
            for i in range(len(chainable) - 1):
                src, tgt = chainable[i], chainable[i + 1]
                strength = score_prerequisite_strength(src.difficulty, tgt.difficulty, sequence_gap=1)
                diff_gap = abs((tgt.difficulty or 3) - (src.difficulty or 3))
                edges.append(
                    CandidateEdge(
                        source_skill_key=src.skill_key,
                        target_skill_key=tgt.skill_key,
                        relationship_type=RelationshipType.PREREQUISITE.value,
                        strength=strength,
                        confidence=clamp(0.6 + 0.05 * (5 - diff_gap)),
                    )
                )

            # Related edges: adjacent siblings at the same level get a (weaker) link.
            units = [n for n in group if n.node_type == "Unit"]
            units.sort(key=lambda n: n.skill_key)
            for i in range(len(units) - 1):
                a, b = units[i], units[i + 1]
                edges.append(
                    CandidateEdge(
                        source_skill_key=a.skill_key,
                        target_skill_key=b.skill_key,
                        relationship_type=RelationshipType.RELATED.value,
                        strength=0.5,
                        confidence=0.6,
                    )
                )

        return edges


# Static type-check assurance that MockEdgeInferer satisfies the protocol.
_: EdgeInferer = MockEdgeInferer()
