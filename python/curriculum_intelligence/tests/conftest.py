"""Shared pytest fixtures.

Ensure the repository's ``python/`` dir is importable as the package root so
``import curriculum_intelligence`` resolves both locally and in CI without an
editable install.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

# python/curriculum_intelligence/tests/conftest.py -> python/ is parents[2]
_PYTHON_ROOT = Path(__file__).resolve().parents[2]
if str(_PYTHON_ROOT) not in sys.path:
    sys.path.insert(0, str(_PYTHON_ROOT))


# A diacritized Arabic fixture document the mock parser echoes verbatim.
# "اَلدَّرْسُ" carries FATHA (U+064E), SHADDA (U+0651), SUKUN (U+0652), DAMMA (U+064F).
ARABIC_DIACRITIZED_TEXT = "اَلدَّرْسُ الأَوَّلُ فِي الْعُلُومِ"


@pytest.fixture
def arabic_fixture_bytes() -> bytes:
    """A `__fixture__` JSON document understood by MockParser."""

    doc = {
        "__fixture__": "parsed_document",
        "chapters": [
            {"number": 1, "title": "الْفَصْلُ الْأَوَّلُ", "page_start": 1, "page_end": 10},
            {"number": 2, "title": "الْفَصْلُ الثَّانِي", "page_start": 11, "page_end": 20},
        ],
        "chunks": [
            {
                "content_type": "text",
                "text": ARABIC_DIACRITIZED_TEXT,
                "source_page": 1,
                "source_region": [0.1, 0.1, 0.8, 0.2],
                "chapter_number": 1,
                "parser_used": "azure_di",
            },
            {
                "content_type": "image",
                "image_ref": "images/42-fig1.png",
                "diagram_caption": "مُثَلَّثٌ قَائِمُ الزَّاوِيَة",
                "source_page": 2,
                "source_region": [0.0, 0.3, 1.0, 0.4],
                "chapter_number": 1,
                "parser_used": "azure_di",
            },
        ],
    }
    return json.dumps(doc, ensure_ascii=False).encode("utf-8")


@pytest.fixture
def failing_fixture_bytes() -> bytes:
    doc = {
        "__fixture__": "parsed_document",
        "__raise__": True,
        "__raise_code__": "azure_di_analyze_failed",
        "__raise_message__": "simulated Azure DI outage",
    }
    return json.dumps(doc).encode("utf-8")
