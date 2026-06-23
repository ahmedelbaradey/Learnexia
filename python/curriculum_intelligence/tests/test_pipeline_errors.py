"""PY-5 — error handling, fallback recording, and terminal-failure shape."""

from __future__ import annotations

import json

from curriculum_intelligence.app.storage import InMemoryObjectStore
from curriculum_intelligence.parsers.artifact import Chunk, ChunkType, ParsedArtifact, ParserUsed
from curriculum_intelligence.parsers.base import ParseRequest, ParserError
from curriculum_intelligence.workers.pipeline import ParsePipeline


class _AlwaysFailParser:
    name = "always_fail"

    def parse(self, request: ParseRequest) -> ParsedArtifact:
        raise ParserError("primary down", code="azure_di_analyze_failed", parser="azure_di")


class _FallbackParser:
    name = "mineru"

    def parse(self, request: ParseRequest) -> ParsedArtifact:
        return ParsedArtifact(
            document_id=request.document_id,
            source_object_key=request.object_key,
            content_type=request.content_type,
            chunks=[
                Chunk(
                    content_type=ChunkType.TEXT.value,
                    text="عربي",
                    source_page=1,
                    parser_used=ParserUsed.MINERU.value,
                )
            ],
        )


class _AlsoFailFallback:
    name = "mineru"

    def parse(self, request: ParseRequest) -> ParsedArtifact:
        raise ParserError("fallback unavailable", code="mineru_unavailable", parser="mineru")


def _store_with(payload_key: str) -> InMemoryObjectStore:
    store = InMemoryObjectStore()
    store.seed(payload_key, b"%PDF-1.4")
    return store


def test_fallback_path_records_parser_used():
    store = _store_with("f.pdf")
    pipeline = ParsePipeline(_AlwaysFailParser(), _FallbackParser(), store)
    result = pipeline.run(1, json.dumps({"object_key": "f.pdf", "content_type": "application/pdf"}))
    assert result.success
    inline = json.loads(result.result_json)
    artifact = json.loads(store.get_object(inline["artifact_key"]).decode("utf-8"))
    assert artifact["chunks"][0]["parser_used"] == "mineru"
    assert inline["parsers_used"] == ["mineru"]


def test_terminal_failure_when_both_fail():
    store = _store_with("f.pdf")
    pipeline = ParsePipeline(_AlwaysFailParser(), _AlsoFailFallback(), store)
    result = pipeline.run(1, json.dumps({"object_key": "f.pdf", "content_type": "application/pdf"}))
    assert not result.success
    inline = json.loads(result.result_json)
    assert inline["parse_status"] == "failed"
    assert inline["diagnostics"]["code"] == "mineru_unavailable"
    # primary error preserved for diagnostics
    assert inline["diagnostics"]["primary_error"]["code"] == "azure_di_analyze_failed"
    assert result.error_message


def test_bad_payload_is_terminal_failure():
    store = _store_with("f.pdf")
    pipeline = ParsePipeline(_FallbackParser(), _FallbackParser(), store)
    result = pipeline.run(1, "{not valid json")
    assert not result.success
    assert json.loads(result.result_json)["diagnostics"]["code"] == "bad_payload"


def test_missing_object_is_download_failure():
    store = InMemoryObjectStore()  # empty — object not present
    pipeline = ParsePipeline(_FallbackParser(), _FallbackParser(), store)
    result = pipeline.run(1, json.dumps({"object_key": "missing.pdf", "content_type": "application/pdf"}))
    assert not result.success
    assert json.loads(result.result_json)["diagnostics"]["code"] == "download_failed"


def test_payload_rejects_unsafe_object_key():
    store = InMemoryObjectStore()
    pipeline = ParsePipeline(_FallbackParser(), _FallbackParser(), store)
    result = pipeline.run(
        1, json.dumps({"object_key": "../../etc/passwd", "content_type": "application/pdf"})
    )
    assert not result.success
    assert json.loads(result.result_json)["diagnostics"]["code"] == "bad_payload"
