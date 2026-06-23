"""The parse pipeline: claimed job → artifact in MinIO → ResultJson (PY-3/4/5).

This is the unit of work the poller runs once a job is claimed. It is pure with
respect to the DB (it does not touch the outbox) — it takes a job's payload +
the storage/parser dependencies and returns either a success result or a typed
failure, so it is trivially unit-testable.
"""

from __future__ import annotations

import json
from dataclasses import dataclass

from ..app.logging import get_logger
from ..app.storage import ObjectStore, assert_safe_object_key
from ..parsers.artifact import ParsedArtifact, artifact_object_key
from ..parsers.base import ParserBackend, ParseRequest, ParserError

logger = get_logger(__name__)


@dataclass
class ParsePayload:
    """The ``PayloadJson`` contract written by the .NET upload side:
    ``{"object_key": "...", "content_type": "..."}`` — nothing else is trusted."""

    object_key: str
    content_type: str

    @classmethod
    def parse(cls, payload_json: str) -> ParsePayload:
        try:
            data = json.loads(payload_json)
        except json.JSONDecodeError as exc:
            raise ParserError(
                f"invalid PayloadJson: {exc}", code="bad_payload"
            ) from exc
        if not isinstance(data, dict):
            raise ParserError("PayloadJson must be an object", code="bad_payload")
        object_key = data.get("object_key")
        content_type = data.get("content_type")
        if not isinstance(object_key, str) or not object_key:
            raise ParserError("PayloadJson.object_key is required", code="bad_payload")
        if not isinstance(content_type, str) or not content_type:
            raise ParserError("PayloadJson.content_type is required", code="bad_payload")
        # Validate up-front so a malicious key never reaches storage. A path-unsafe
        # key is a payload-level problem → surface it as a typed bad_payload failure
        # (not an uncaught ValueError) so the worker writes a terminal Failed job.
        try:
            assert_safe_object_key(object_key)
        except ValueError as exc:
            raise ParserError(f"unsafe object_key: {exc}", code="bad_payload") from exc
        return cls(object_key=object_key, content_type=content_type)


@dataclass
class PipelineResult:
    """Outcome of running the pipeline for one job."""

    success: bool
    result_json: str
    error_message: str | None = None


def _build_result_json(artifact_key: str, artifact: ParsedArtifact, *, parse_status: str) -> str:
    """The small inline ``ResultJson`` the .NET advance poller reads."""

    return json.dumps(
        {
            "artifact_key": artifact_key,
            "chapters": artifact.chapters_summary(),
            "parse_status": parse_status,
            "parsers_used": artifact.parsers_used(),
            "diagnostics": artifact.diagnostics,
        },
        ensure_ascii=False,
    )


class ParsePipeline:
    """Runs parse + fallback + artifact write for one document."""

    def __init__(
        self,
        parser: ParserBackend,
        fallback_parser: ParserBackend,
        storage: ObjectStore,
    ) -> None:
        self._parser = parser
        self._fallback = fallback_parser
        self._storage = storage

    def run(self, document_id: int, payload_json: str) -> PipelineResult:
        try:
            payload = ParsePayload.parse(payload_json)
        except ParserError as exc:
            return self._failure(exc)

        try:
            file_bytes = self._storage.get_object(payload.object_key)
        except Exception as exc:  # noqa: BLE001
            return PipelineResult(
                success=False,
                result_json=json.dumps(
                    {
                        "parse_status": "failed",
                        "diagnostics": {"code": "download_failed", "message": str(exc)},
                    },
                    ensure_ascii=False,
                ),
                error_message=f"download_failed: {exc}",
            )

        request = ParseRequest(
            document_id=document_id,
            object_key=payload.object_key,
            content_type=payload.content_type,
            file_bytes=file_bytes,
        )

        artifact = self._parse_with_fallback(request)
        if isinstance(artifact, PipelineResult):  # failure short-circuit
            return artifact

        # Serialize + write the (potentially large) artifact to MinIO.
        try:
            artifact_key = artifact_object_key(payload.object_key)
            self._storage.put_object(
                artifact_key,
                artifact.to_json().encode("utf-8"),
                content_type="application/json; charset=utf-8",
            )
        except Exception as exc:  # noqa: BLE001
            return PipelineResult(
                success=False,
                result_json=json.dumps(
                    {
                        "parse_status": "failed",
                        "diagnostics": {"code": "artifact_write_failed", "message": str(exc)},
                    },
                    ensure_ascii=False,
                ),
                error_message=f"artifact_write_failed: {exc}",
            )

        logger.info(
            "parsed document_id=%s chunks=%d chapters=%d parsers=%s -> %s",
            document_id,
            len(artifact.chunks),
            len(artifact.chapters),
            artifact.parsers_used(),
            artifact_key,
        )
        return PipelineResult(
            success=True,
            result_json=_build_result_json(artifact_key, artifact, parse_status="done"),
        )

    # --- internals -----------------------------------------------------------

    def _parse_with_fallback(self, request: ParseRequest):
        """Try the primary parser; on a typed failure, try the fallback (PY-5)."""

        try:
            return self._parser.parse(request)
        except ParserError as primary_exc:
            logger.warning(
                "primary parser %s failed (code=%s): %s — trying fallback",
                self._parser.name,
                primary_exc.code,
                primary_exc,
            )
            try:
                return self._fallback.parse(request)
            except ParserError as fallback_exc:
                return self._failure(fallback_exc, primary=primary_exc)

    @staticmethod
    def _failure(exc: ParserError, *, primary: ParserError | None = None) -> PipelineResult:
        diagnostics = {
            "code": exc.code,
            "message": str(exc),
            "parser": exc.parser,
        }
        if primary is not None:
            diagnostics["primary_error"] = {"code": primary.code, "message": str(primary)}
        return PipelineResult(
            success=False,
            result_json=json.dumps(
                {"parse_status": "failed", "diagnostics": diagnostics}, ensure_ascii=False
            ),
            error_message=f"{exc.code}: {exc}",
        )
