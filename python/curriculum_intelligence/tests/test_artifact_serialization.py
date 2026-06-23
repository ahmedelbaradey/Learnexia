"""PY-4 — artifact normalization + JSON serialization contract."""

from __future__ import annotations

import json

from curriculum_intelligence.parsers.artifact import (
    Chapter,
    Chunk,
    ChunkType,
    ParsedArtifact,
    ParserUsed,
    artifact_object_key,
)


def _sample_artifact() -> ParsedArtifact:
    return ParsedArtifact(
        document_id=42,
        source_object_key="abc-123.pdf",
        content_type="application/pdf",
        chapters=[Chapter(number=1, title="الْفَصْلُ الْأَوَّلُ", page_start=1, page_end=12)],
        chunks=[
            Chunk(
                content_type=ChunkType.TEXT.value,
                text="نَصٌّ عَرَبِيٌّ",
                source_page=1,
                source_region=[0.0, 0.0, 1.0, 0.5],
                chapter_number=1,
                parser_used=ParserUsed.AZURE_DI.value,
            ),
            Chunk(
                content_type=ChunkType.TABLE.value,
                text="a | b",
                source_page=2,
                parser_used=ParserUsed.MINERU.value,
            ),
        ],
    )


def test_artifact_dict_shape_matches_contract():
    artifact = _sample_artifact()
    data = artifact.to_dict()

    assert data["document_id"] == 42
    assert data["schema_version"] == "1.0"
    assert data["source_object_key"] == "abc-123.pdf"
    assert data["content_type"] == "application/pdf"

    # chapters[] shape
    assert data["chapters"] == [
        {"number": 1, "title": "الْفَصْلُ الْأَوَّلُ", "page_start": 1, "page_end": 12}
    ]

    # chunk required keys
    chunk = data["chunks"][0]
    for key in (
        "content_type",
        "text",
        "image_ref",
        "diagram_caption",
        "source_page",
        "source_region",
        "chapter_number",
        "parser_used",
    ):
        assert key in chunk


def test_to_json_round_trips_and_keeps_unicode():
    artifact = _sample_artifact()
    raw = artifact.to_json()
    # Arabic must be stored as real characters, not \uXXXX escapes.
    assert "\\u" not in raw
    parsed = json.loads(raw)
    assert parsed["chunks"][0]["text"] == "نَصٌّ عَرَبِيٌّ"


def test_chapters_summary_is_inline_subset():
    artifact = _sample_artifact()
    assert artifact.chapters_summary() == [
        {"number": 1, "title": "الْفَصْلُ الْأَوَّلُ", "page_start": 1, "page_end": 12}
    ]


def test_parsers_used_is_sorted_unique():
    artifact = _sample_artifact()
    assert artifact.parsers_used() == ["azure_di", "mineru"]


def test_artifact_key_is_path_safe():
    assert artifact_object_key("abc-123.pdf") == "artifacts/abc-123.pdf.artifact.json"
    # strips any directory component from a hostile key
    assert artifact_object_key("nested/dir/x.pdf") == "artifacts/x.pdf.artifact.json"


def test_artifact_key_rejects_traversal_only_key():
    import pytest

    for bad in ("..", "."):
        with pytest.raises(ValueError):
            artifact_object_key(bad)
