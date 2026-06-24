"""The ingest pipeline: raw extraction → SkillKey-anchored nodes → chunks → flags
(BL-05-PY-1).

This is the unit of work the ingest poller runs once a job is claimed. Like the
parse ``ParsePipeline`` it is pure w.r.t. the DB (it never touches the outbox) and
writes NO entity rows (Q6) — it returns an :class:`IngestionResult` the .NET side
persists. It downloads the BL-02 artifact from MinIO, runs the configured
extractor (mock default / Claude live), derives stable SkillKeys, semantically
chunks the content, and emits confidences + advisory flags.

Idempotency: SkillKeys are DERIVED deterministically from
``{subjectCode}.grade{N}.{unitSlug}.{leafSlug}``, so re-running ingestion for the
same document yields byte-identical node identities — the anchor the .NET upsert
matches on.
"""

from __future__ import annotations

import json
from dataclasses import dataclass

from ..app.logging import get_logger
from ..app.storage import ObjectStore, assert_safe_object_key
from .chunker import chunk_text
from .confidence import build_flags, clamp
from .extractor import ExtractionRequest, Extractor, ExtractorError, RawExtraction, RawNode
from .models import Chunk, HierarchyNode, IngestionResult
from .skill_key import build_skill_key

logger = get_logger(__name__)


@dataclass
class IngestPayload:
    """The ``PayloadJson`` contract the .NET enqueue side writes for an ingest job:
    ``{"artifact_key": "artifacts/<guid>.pdf.artifact.json", "document_id": <int>}``.
    Nothing else is trusted."""

    artifact_key: str
    document_id: int

    @classmethod
    def parse(cls, payload_json: str, *, fallback_document_id: int) -> IngestPayload:
        try:
            data = json.loads(payload_json)
        except json.JSONDecodeError as exc:
            raise ExtractorError(f"invalid PayloadJson: {exc}", code="bad_payload") from exc
        if not isinstance(data, dict):
            raise ExtractorError("PayloadJson must be an object", code="bad_payload")
        artifact_key = data.get("artifact_key")
        if not isinstance(artifact_key, str) or not artifact_key:
            raise ExtractorError("PayloadJson.artifact_key is required", code="bad_payload")
        try:
            assert_safe_object_key(artifact_key)
        except ValueError as exc:
            raise ExtractorError(f"unsafe artifact_key: {exc}", code="bad_payload") from exc
        document_id = data.get("document_id")
        if not isinstance(document_id, int):
            document_id = fallback_document_id
        return cls(artifact_key=artifact_key, document_id=document_id)


@dataclass
class IngestPipelineResult:
    """Outcome of running the ingest pipeline for one job (mirrors PipelineResult)."""

    success: bool
    result_json: str
    error_message: str | None = None


class IngestPipeline:
    """Runs extraction + chunking + scoring for one document → IngestionResult."""

    def __init__(
        self,
        extractor: Extractor,
        storage: ObjectStore,
        *,
        max_chunk_chars: int = 1200,
        confidence_threshold: float = 0.7,
    ) -> None:
        self._extractor = extractor
        self._storage = storage
        self._max_chunk_chars = max_chunk_chars
        self._threshold = confidence_threshold

    def run(self, document_id: int, payload_json: str) -> IngestPipelineResult:
        try:
            payload = IngestPayload.parse(payload_json, fallback_document_id=document_id)
        except ExtractorError as exc:
            return self._failure(exc)

        try:
            artifact_bytes = self._storage.get_object(payload.artifact_key)
        except Exception as exc:  # noqa: BLE001
            return self._diag_failure("artifact_download_failed", str(exc))

        try:
            artifact = json.loads(artifact_bytes.decode("utf-8"))
            if not isinstance(artifact, dict):
                raise ValueError("artifact JSON must be an object")
        except Exception as exc:  # noqa: BLE001
            return self._diag_failure("bad_artifact_json", str(exc))

        try:
            extraction = self._extractor.extract(
                ExtractionRequest(document_id=payload.document_id, artifact=artifact)
            )
        except ExtractorError as exc:
            return self._failure(exc)
        except Exception as exc:  # noqa: BLE001 — never leak an untyped error to the outbox
            return self._diag_failure("extractor_error", str(exc))

        result = self._assemble(extraction)
        logger.info(
            "ingested document_id=%s nodes=%d chunks=%d flags=%d (extractor=%s)",
            payload.document_id,
            len(result.nodes),
            len(result.chunks),
            len(result.flags),
            self._extractor.name,
        )
        return IngestPipelineResult(success=True, result_json=result.to_json())

    # --- internals -----------------------------------------------------------

    def _assemble(self, extraction: RawExtraction) -> IngestionResult:
        """Turn the raw extraction into the frozen ResultJson model."""

        # Derive a stable SkillKey per raw node + remember the per-index key so
        # parent links and chunk mappings resolve to keys (not array indices).
        keys: list[str] = [self._key_for(n) for n in extraction.nodes]

        nodes: list[HierarchyNode] = []
        for index, raw in enumerate(extraction.nodes):
            parent_key = (
                keys[raw.parent_index]
                if raw.parent_index is not None and 0 <= raw.parent_index < len(keys)
                else None
            )
            nodes.append(
                HierarchyNode(
                    node_type=raw.node_type,
                    skill_key=keys[index],
                    parent_skill_key=parent_key,
                    title=raw.title,
                    grade=raw.grade,
                    subject_code=raw.subject_code,
                    language=raw.language,
                    difficulty=raw.difficulty,
                    confidence=clamp(raw.confidence),
                )
            )

        chunks: list[Chunk] = []
        for source in extraction.chunk_sources:
            node_key = keys[source.node_index] if 0 <= source.node_index < len(keys) else ""
            for piece in chunk_text(source.text, max_chunk_chars=self._max_chunk_chars):
                chunks.append(
                    Chunk(
                        content=piece,
                        node_skill_key=node_key,
                        confidence=clamp(source.confidence),
                        content_type="text",
                        source_page=source.source_page,
                        chapter_number=source.chapter_number,
                    )
                )

        flags = build_flags(nodes, chunks, threshold=self._threshold)
        diagnostics = dict(extraction.diagnostics)
        diagnostics.update(
            {
                "node_count": len(nodes),
                "chunk_count": len(chunks),
                "flag_count": len(flags),
                "advisory_threshold": self._threshold,
            }
        )
        return IngestionResult(nodes=nodes, chunks=chunks, flags=flags, diagnostics=diagnostics)

    @staticmethod
    def _key_for(raw: RawNode) -> str:
        return build_skill_key(
            subject_code=raw.subject_code,
            grade=raw.grade,
            unit_slug_source=raw.unit_slug_source,
            leaf_slug_source=raw.slug_source,
        )

    @staticmethod
    def _failure(exc: ExtractorError) -> IngestPipelineResult:
        diagnostics = {"code": exc.code, "message": str(exc), "backend": exc.backend}
        return IngestPipelineResult(
            success=False,
            result_json=json.dumps(
                {"ingest_status": "failed", "diagnostics": diagnostics}, ensure_ascii=False
            ),
            error_message=f"{exc.code}: {exc}",
        )

    @staticmethod
    def _diag_failure(code: str, message: str) -> IngestPipelineResult:
        return IngestPipelineResult(
            success=False,
            result_json=json.dumps(
                {"ingest_status": "failed", "diagnostics": {"code": code, "message": message}},
                ensure_ascii=False,
            ),
            error_message=f"{code}: {message}",
        )
