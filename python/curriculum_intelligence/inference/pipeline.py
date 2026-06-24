"""The infer-edges pipeline: node payload → candidate edges → post-process → ResultJson
(BL-03-PY-1).

This is the unit of work the infer poller runs once a job is claimed. Like the
parse/ingest pipelines it is pure w.r.t. the DB (it never touches the outbox) and
writes NO entity rows (Decision E) — it returns an :class:`InferenceResult` the
.NET ``EdgeInferenceAdvanceService`` persists to ``KGSuggestion{Pending}``.

Unlike the ingest lane (which downloads a MinIO artifact), the infer lane reads its
node hierarchy DIRECTLY from the job ``PayloadJson`` — the .NET enqueue side
(``BuildKnowledgeGraphSuggestionsCommand``) has already loaded the ``KnowledgeNode``
rows for the document's subject/grade(s) and shaped them by SkillKey, so there is no
storage round-trip. Nothing in the payload is trusted; bad payloads → terminal Failed.

Flow: parse payload → build InputNodes → infer (mock default / LightRAG live) →
post-process (drop self-loops, enforce 4-subject domain, dedup) → serialize the
frozen ``{inference_model, edges}`` contract.
"""

from __future__ import annotations

import json
from dataclasses import dataclass

from ..app.logging import get_logger
from .inferer import EdgeInferer, InferenceError, InferenceRequest, InputNode
from .models import InferenceResult, InferredEdge
from .postprocessor import post_process
from .scoring import clamp

logger = get_logger(__name__)


@dataclass
class InferPayload:
    """The ``PayloadJson`` contract the .NET enqueue side writes for an infer_edges job:

        {"document_id": <int>,
         "nodes": [{"skill_key": "...", "title": "...", "node_type": "Skill",
                    "subject_code": "math", "grade": 4, "difficulty": 3}, ...]}

    Nothing else is trusted. ``nodes`` is required + non-empty.
    """

    document_id: int
    nodes: list[InputNode]

    @classmethod
    def parse(cls, payload_json: str, *, fallback_document_id: int) -> InferPayload:
        try:
            data = json.loads(payload_json)
        except json.JSONDecodeError as exc:
            raise InferenceError(f"invalid PayloadJson: {exc}", code="bad_payload") from exc
        if not isinstance(data, dict):
            raise InferenceError("PayloadJson must be an object", code="bad_payload")

        raw_nodes = data.get("nodes")
        if not isinstance(raw_nodes, list) or not raw_nodes:
            raise InferenceError("PayloadJson.nodes is required and non-empty", code="bad_payload")

        document_id = data.get("document_id")
        if not isinstance(document_id, int):
            document_id = fallback_document_id

        nodes: list[InputNode] = []
        for n in raw_nodes:
            if not isinstance(n, dict):
                raise InferenceError("each node must be an object", code="bad_payload")
            skill_key = n.get("skill_key")
            if not isinstance(skill_key, str) or not skill_key:
                raise InferenceError("node.skill_key is required", code="bad_payload")
            difficulty = n.get("difficulty")
            nodes.append(
                InputNode(
                    skill_key=skill_key,
                    title=str(n.get("title", "")),
                    node_type=str(n.get("node_type", "Skill")),
                    subject_code=str(n.get("subject_code", "")).strip().lower(),
                    grade=int(n.get("grade", 0)),
                    difficulty=int(difficulty) if isinstance(difficulty, int) else None,
                )
            )
        return cls(document_id=document_id, nodes=nodes)


@dataclass
class InferPipelineResult:
    """Outcome of running the infer pipeline for one job (mirrors IngestPipelineResult)."""

    success: bool
    result_json: str
    error_message: str | None = None


class InferencePipeline:
    """Runs edge inference + post-processing for one build batch → InferenceResult."""

    def __init__(self, inferer: EdgeInferer, *, min_confidence: float = 0.0) -> None:
        self._inferer = inferer
        self._min_confidence = clamp(min_confidence)

    def run(self, document_id: int, payload_json: str) -> InferPipelineResult:
        try:
            payload = InferPayload.parse(payload_json, fallback_document_id=document_id)
        except InferenceError as exc:
            return self._failure(exc)

        try:
            raw = self._inferer.infer(InferenceRequest(document_id=payload.document_id, nodes=payload.nodes))
        except InferenceError as exc:
            return self._failure(exc)
        except Exception as exc:  # noqa: BLE001 — never leak an untyped error to the outbox
            return self._diag_failure("inferer_error", str(exc))

        result = self._assemble(raw)
        logger.info(
            "inferred document_id=%s nodes=%d edges=%d (inferer=%s model=%s)",
            payload.document_id,
            len(payload.nodes),
            len(result.edges),
            self._inferer.name,
            result.inference_model,
        )
        return InferPipelineResult(success=True, result_json=result.to_json())

    # --- internals -----------------------------------------------------------

    def _assemble(self, raw) -> InferenceResult:
        """Post-process the raw candidates and map to the frozen ResultJson model."""

        cleaned = post_process(raw.edges)

        edges: list[InferredEdge] = []
        for c in cleaned:
            # Optional advisory floor (default 0.0 → keep everything); the worker
            # never gates structurally — the .NET side owns the real cutoff.
            if clamp(c.confidence) < self._min_confidence:
                continue
            edges.append(
                InferredEdge(
                    source_skill_key=c.source_skill_key,
                    target_skill_key=c.target_skill_key,
                    relationship_type=c.relationship_type,
                    strength=clamp(c.strength),
                    confidence=clamp(c.confidence),
                )
            )

        diagnostics = dict(raw.diagnostics)
        diagnostics.update(
            {
                "candidate_count": len(raw.edges),
                "edge_count": len(edges),
                "min_confidence": self._min_confidence,
            }
        )
        return InferenceResult(inference_model=raw.inference_model, edges=edges, diagnostics=diagnostics)

    @staticmethod
    def _failure(exc: InferenceError) -> InferPipelineResult:
        diagnostics = {"code": exc.code, "message": str(exc), "backend": exc.backend}
        return InferPipelineResult(
            success=False,
            result_json=json.dumps(
                {"infer_status": "failed", "diagnostics": diagnostics}, ensure_ascii=False
            ),
            error_message=f"{exc.code}: {exc}",
        )

    @staticmethod
    def _diag_failure(code: str, message: str) -> InferPipelineResult:
        return InferPipelineResult(
            success=False,
            result_json=json.dumps(
                {"infer_status": "failed", "diagnostics": {"code": code, "message": message}},
                ensure_ascii=False,
            ),
            error_message=f"{code}: {message}",
        )
