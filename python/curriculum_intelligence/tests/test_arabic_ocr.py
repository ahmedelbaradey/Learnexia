"""PY-6 — Arabic OCR smoke test (against the mock fixture in CI).

Asserts: Arabic text non-empty, source refs present, >=1 diacritic
(U+064B-U+065F), and parser_used == azure_di on the primary path.
"""

from __future__ import annotations

from curriculum_intelligence.app.config import Settings
from curriculum_intelligence.app.storage import InMemoryObjectStore
from curriculum_intelligence.parsers.factory import build_fallback_parser, build_parser
from curriculum_intelligence.workers.pipeline import ParsePipeline

TASHKEEL = range(0x064B, 0x0660)


def _has_diacritic(text: str) -> bool:
    return any(ord(ch) in TASHKEEL for ch in text)


def _run(file_bytes: bytes):
    settings = Settings.from_env()  # mock backend by default
    store = InMemoryObjectStore()
    store.seed("page.pdf", file_bytes)
    pipeline = ParsePipeline(
        parser=build_parser(settings),
        fallback_parser=build_fallback_parser(settings),
        storage=store,
    )
    import json

    result = pipeline.run(99, json.dumps({"object_key": "page.pdf", "content_type": "application/pdf"}))
    assert result.success, result.error_message
    # read the artifact back out of the store
    inline = json.loads(result.result_json)
    artifact = json.loads(store.get_object(inline["artifact_key"]).decode("utf-8"))
    return inline, artifact


def test_arabic_smoke_against_fixture(arabic_fixture_bytes):
    inline, artifact = _run(arabic_fixture_bytes)

    text_chunks = [c for c in artifact["chunks"] if c["content_type"] == "text"]
    assert text_chunks, "expected at least one text chunk"
    text = text_chunks[0]["text"]

    # (1) Arabic non-empty
    assert text and any("؀" <= ch <= "ۿ" for ch in text)
    # (2) source refs present
    assert text_chunks[0]["source_page"] is not None
    # (3) at least one diacritic survives
    assert _has_diacritic(text)
    # (4) primary parser path
    assert text_chunks[0]["parser_used"] == "azure_di"

    # inline ResultJson carries the chapters summary
    assert inline["parse_status"] == "done"
    assert inline["chapters"] and inline["chapters"][0]["number"] == 1


def test_arabic_smoke_synthetic_path():
    # A non-fixture PDF triggers the synthetic mock path; it must still produce
    # diacritized Arabic so the smoke assertions hold with no bundled samples.
    inline, artifact = _run(b"%PDF-1.7 binary-ish content")
    text = next(c["text"] for c in artifact["chunks"] if c["content_type"] == "text")
    assert _has_diacritic(text)
    assert inline["parsers_used"] == ["azure_di"]
