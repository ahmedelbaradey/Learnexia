"""PY-8 — diacritics-preservation e2e: tashkeel survives every stage.

raw fixture text -> mock parse -> normalize -> serialize -> MinIO bytes ->
deserialize -> final artifact text still carries the exact U+064B-U+065F marks.
"""

from __future__ import annotations

import json

from curriculum_intelligence.app.config import Settings
from curriculum_intelligence.app.storage import InMemoryObjectStore
from curriculum_intelligence.parsers.factory import build_fallback_parser, build_parser
from curriculum_intelligence.workers.pipeline import ParsePipeline

from .conftest import ARABIC_DIACRITIZED_TEXT

TASHKEEL = range(0x064B, 0x0660)


def _tashkeel_chars(text: str) -> list[str]:
    return [ch for ch in text if ord(ch) in TASHKEEL]


def test_tashkeel_survives_full_pipeline(arabic_fixture_bytes):
    settings = Settings.from_env()
    store = InMemoryObjectStore()
    store.seed("d.pdf", arabic_fixture_bytes)
    pipeline = ParsePipeline(
        parser=build_parser(settings),
        fallback_parser=build_fallback_parser(settings),
        storage=store,
    )

    expected_marks = _tashkeel_chars(ARABIC_DIACRITIZED_TEXT)
    assert expected_marks, "fixture sanity: the source text must contain tashkeel"

    result = pipeline.run(5, json.dumps({"object_key": "d.pdf", "content_type": "application/pdf"}))
    assert result.success

    inline = json.loads(result.result_json)
    raw_bytes = store.get_object(inline["artifact_key"])

    # (3) no encoding step silently stripped the marks — bytes are utf-8, not ASCII-escaped
    raw_text = raw_bytes.decode("utf-8")
    assert "\\u" not in raw_text

    artifact = json.loads(raw_text)
    final_text = next(c["text"] for c in artifact["chunks"] if c["content_type"] == "text")

    # (1)/(2) the exact diacritized word from the source appears in the final artifact
    assert "اَلدَّرْسُ" in final_text
    final_marks = _tashkeel_chars(final_text)
    # every expected mark type is still present
    assert set(expected_marks).issubset(set(final_marks))
