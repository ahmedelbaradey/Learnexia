"""Parser normalization (PY-3-equivalent against the mock) + storage path-safety."""

from __future__ import annotations

import pytest

from curriculum_intelligence.app.storage import InMemoryObjectStore, assert_safe_object_key
from curriculum_intelligence.parsers.base import ParseRequest, ParserError
from curriculum_intelligence.parsers.mock_parser import MockParser


def test_mock_synthetic_emits_chapters_and_chunks():
    parser = MockParser()
    artifact = parser.parse(
        ParseRequest(
            document_id=7,
            object_key="x.pdf",
            content_type="application/pdf",
            file_bytes=b"%PDF-1.4",
        )
    )
    assert artifact.document_id == 7
    assert len(artifact.chapters) >= 1
    assert len(artifact.chunks) >= 1
    # every chunk carries source-reference traceability + parser_used
    for chunk in artifact.chunks:
        assert chunk.parser_used
        assert chunk.source_page is not None


def test_mock_echoes_fixture(arabic_fixture_bytes):
    parser = MockParser()
    artifact = parser.parse(
        ParseRequest(
            document_id=42,
            object_key="doc.pdf",
            content_type="application/pdf",
            file_bytes=arabic_fixture_bytes,
        )
    )
    assert [c.number for c in artifact.chapters] == [1, 2]
    text_chunk = next(c for c in artifact.chunks if c.content_type == "text")
    assert "اَلدَّرْسُ" in text_chunk.text
    assert text_chunk.parser_used == "azure_di"


def test_mock_fixture_can_simulate_failure(failing_fixture_bytes):
    parser = MockParser()
    with pytest.raises(ParserError) as exc_info:
        parser.parse(
            ParseRequest(
                document_id=1,
                object_key="doc.pdf",
                content_type="application/pdf",
                file_bytes=failing_fixture_bytes,
            )
        )
    assert exc_info.value.code == "azure_di_analyze_failed"


@pytest.mark.parametrize(
    "bad_key",
    ["", "/etc/passwd", "../secret", "a/../../b", "x\x00y", "./rel"],
)
def test_object_key_safety_rejects_dangerous_keys(bad_key):
    with pytest.raises(ValueError):
        assert_safe_object_key(bad_key)


def test_object_key_safety_allows_guid_keys():
    assert assert_safe_object_key("3f2504e0-4f89-11d3-9a0c-0305e82c3301.pdf").endswith(".pdf")


def test_in_memory_store_round_trip():
    store = InMemoryObjectStore()
    store.seed("k.pdf", b"hello")
    assert store.get_object("k.pdf") == b"hello"
    store.put_object("out.json", b"{}", content_type="application/json")
    assert "out.json" in store.keys
