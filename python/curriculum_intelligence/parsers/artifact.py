"""The normalized per-document artifact model + JSON serialization (PY-4).

Artifact JSON shape (the documented contract — brief AC4):

    {
      "document_id": <int>,
      "schema_version": "1.0",
      "source_object_key": "<guid>.pdf",
      "content_type": "application/pdf",
      "chapters": [
        {"number": 1, "title": "...", "page_start": 1, "page_end": 12}
      ],
      "chunks": [
        {
          "content_type": "text" | "image" | "table" | "equation" | "layout",
          "text": "...",                  # OCR'd / extracted text (diacritics preserved)
          "image_ref": "<key>" | null,    # object key for an extracted image, if any
          "diagram_caption": "..." | null,# VLM caption for images/diagrams
          "source_page": 1,
          "source_region": [x, y, w, h] | null,
          "chapter_number": 1 | null,
          "parser_used": "azure_di" | "mineru" | "paddleocr" | "mock"
        }
      ],
      "diagnostics": {...} | null
    }

The small ``chapters[]`` summary is returned INLINE in ``PipelineJobs.ResultJson``;
the full artifact (which can be many MB for a textbook) is written to MinIO and
referenced by its object key.
"""

from __future__ import annotations

import json
from dataclasses import asdict, dataclass, field
from enum import Enum

SCHEMA_VERSION = "1.0"


class ChunkType(str, Enum):
    TEXT = "text"
    IMAGE = "image"
    TABLE = "table"
    EQUATION = "equation"
    LAYOUT = "layout"


class ParserUsed(str, Enum):
    AZURE_DI = "azure_di"
    MINERU = "mineru"
    PADDLEOCR = "paddleocr"
    MOCK = "mock"


@dataclass
class Chapter:
    number: int
    title: str
    page_start: int | None = None
    page_end: int | None = None

    def to_dict(self) -> dict:
        return asdict(self)


@dataclass
class Chunk:
    content_type: str
    text: str | None = None
    image_ref: str | None = None
    diagram_caption: str | None = None
    source_page: int | None = None
    source_region: list[float] | None = None
    chapter_number: int | None = None
    parser_used: str = ParserUsed.MOCK.value

    def to_dict(self) -> dict:
        return asdict(self)


@dataclass
class ParsedArtifact:
    """The normalized representation produced by a parser backend, before
    serialization to the MinIO JSON object."""

    document_id: int
    source_object_key: str
    content_type: str
    chapters: list[Chapter] = field(default_factory=list)
    chunks: list[Chunk] = field(default_factory=list)
    diagnostics: dict | None = None
    schema_version: str = SCHEMA_VERSION

    def to_dict(self) -> dict:
        return {
            "document_id": self.document_id,
            "schema_version": self.schema_version,
            "source_object_key": self.source_object_key,
            "content_type": self.content_type,
            "chapters": [c.to_dict() for c in self.chapters],
            "chunks": [c.to_dict() for c in self.chunks],
            "diagnostics": self.diagnostics,
        }

    def to_json(self) -> str:
        # ensure_ascii=False keeps Arabic + tashkeel (U+064B–U+065F) as real
        # characters instead of \uXXXX escapes — verified by the diacritics test.
        return json.dumps(self.to_dict(), ensure_ascii=False, indent=2)

    def chapters_summary(self) -> list[dict]:
        """The small inline summary returned in ResultJson."""

        return [c.to_dict() for c in self.chapters]

    def parsers_used(self) -> list[str]:
        return sorted({c.parser_used for c in self.chunks})


def artifact_object_key(source_object_key: str) -> str:
    """Derive the artifact's MinIO key from the source key — path-safe, same bucket.

    e.g. ``abc123.pdf`` -> ``artifacts/abc123.pdf.artifact.json``. We strip any
    directory component from the source so a malicious key cannot place the
    artifact outside the ``artifacts/`` prefix.
    """

    base = source_object_key.replace("\\", "/").rsplit("/", 1)[-1]
    if not base or base in {".", ".."}:
        raise ValueError(f"cannot derive artifact key from {source_object_key!r}")
    return f"artifacts/{base}.artifact.json"
