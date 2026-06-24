"""The ``EdgeInferer`` protocol — the seam behind which LightRAG/LLM edge
inference lives (BL-03-PY-2).

Mirrors ``ingestion/extractor.py``'s ``Extractor`` exactly: a single ``Protocol``
that both the deterministic :class:`MockEdgeInferer` (default in dev + CI) and the
live :class:`LightRagEdgeInferer` implement, so the inference pipeline is agnostic
to which one runs. The backend is selected by config (``INFERER_BACKEND`` env),
defaulting to the mock (ADR-0004 §5 mock-now/flip-later posture).

The inferer turns a list of curriculum nodes (the BL-05 ingest hierarchy, keyed by
SkillKey) into *raw* candidate edges (source/target SkillKey + relationship +
advisory strength/confidence). It does NOT dedup, drop self-loops, enforce the
4-subject domain, or clamp — those are downstream stages (postprocessor / scoring).
It returns ``RawInference`` (loosely-typed candidate edges), which the pipeline
normalizes + post-processes into the frozen ``ResultJson`` contract.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Protocol, runtime_checkable


class InferenceError(Exception):
    """Raised by an inferer backend on an unrecoverable inference failure.

    The worker turns this into a terminal ``Failed`` infer_edges job with
    diagnostics; it does NOT retry (retry policy is owned by .NET, ADR-0004 §3).
    """

    def __init__(self, message: str, *, code: str = "infer_error", backend: str | None = None) -> None:
        super().__init__(message)
        self.code = code
        self.backend = backend


@dataclass(frozen=True)
class InputNode:
    """One curriculum node fed to inference — the BL-05 ingest hierarchy shape.

    ``skill_key`` is the stable identity (``{subjectCode}.grade{N}.{unitSlug}.{leafSlug}``)
    the .NET enqueue side wrote into the job payload; it is what every emitted edge
    references (the .NET side resolves it back to a ``KnowledgeNode.Id``).
    """

    skill_key: str
    title: str
    node_type: str
    subject_code: str
    grade: int
    difficulty: int | None = None


@dataclass(frozen=True)
class InferenceRequest:
    """Everything an inferer needs to propose edges for one build batch.

    ``nodes`` is the curriculum hierarchy for a document's subject/grade(s).
    ``document_id`` is the curriculum doc id (provenance / diagnostics only).
    """

    document_id: int
    nodes: list[InputNode]


@dataclass
class CandidateEdge:
    """One proposed edge, before dedup / self-loop drop / domain enforcement / clamp.

    Strength + confidence are advisory raw signals (clamped + post-processed
    downstream; the .NET side re-clamps again — never trusted).
    """

    source_skill_key: str
    target_skill_key: str
    relationship_type: str
    strength: float
    confidence: float


@dataclass
class RawInference:
    """The inferer's loosely-typed output: candidate edges + a model id + diagnostics."""

    inference_model: str
    edges: list[CandidateEdge] = field(default_factory=list)
    diagnostics: dict = field(default_factory=dict)


@runtime_checkable
class EdgeInferer(Protocol):
    """LightRAG/LLM-driven prerequisite+related edge inferer."""

    name: str

    def infer(self, request: InferenceRequest) -> RawInference:
        """Propose candidate edges over the node list. Raise
        :class:`InferenceError` on unrecoverable failure."""
        ...
