"""The ``Extractor`` protocol — the seam behind which LLM hierarchy extraction
lives (BL-05-PY-2).

Mirrors ``parsers/base.py``'s ``ParserBackend`` exactly: a single ``Protocol``
that both the deterministic :class:`MockExtractor` (default in dev + CI) and the
live :class:`ClaudeExtractor` implement, so the ingest pipeline is agnostic to
which one runs. The backend is selected by config (``EXTRACTOR_BACKEND`` env),
defaulting to the mock (ADR-0004 §5 mock-now/flip-later posture).

The extractor turns the parsed-artifact text/structure into a *raw* hierarchy +
prerequisite signal. It does NOT chunk, score confidence-thresholds, or build
SkillKeys — those are downstream stages (chunker / confidence). It returns
``RawExtraction`` (loosely-typed proposed nodes + their source content), which
the pipeline normalizes into the frozen ``ResultJson`` contract.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Protocol, runtime_checkable


class ExtractorError(Exception):
    """Raised by an extractor backend on an unrecoverable extraction failure.

    The worker turns this into a terminal ``Failed`` ingest job with diagnostics;
    it does NOT retry (retry policy is owned by .NET, ADR-0004 §3).
    """

    def __init__(self, message: str, *, code: str = "extract_error", backend: str | None = None) -> None:
        super().__init__(message)
        self.code = code
        self.backend = backend


@dataclass(frozen=True)
class ExtractionRequest:
    """Everything an extractor needs to structure one parsed document.

    ``artifact`` is the BL-02 parsed-artifact JSON (already a dict — the pipeline
    downloads + parses it from MinIO). ``document_id`` is the curriculum doc id.
    """

    document_id: int
    artifact: dict


@dataclass
class RawNode:
    """One proposed hierarchy node, before SkillKey derivation + normalization.

    ``slug_source`` is an ASCII/Latin slug seed (English title / transliteration)
    used to build a stable SkillKey — the Arabic ``title`` slugifies to empty, so
    the extractor supplies a slug source explicitly (see skill_key.py).
    """

    node_type: str
    title: str
    grade: int
    subject_code: str
    language: str
    slug_source: str
    unit_slug_source: str
    confidence: float
    parent_index: int | None = None
    difficulty: int | None = None


@dataclass
class RawChunkSource:
    """Lesson/concept content tied to a proposed node, before semantic chunking."""

    node_index: int
    text: str
    source_page: int | None = None
    chapter_number: int | None = None
    confidence: float = 1.0


@dataclass
class RawExtraction:
    """The extractor's loosely-typed output: proposed nodes + their content."""

    nodes: list[RawNode] = field(default_factory=list)
    chunk_sources: list[RawChunkSource] = field(default_factory=list)
    diagnostics: dict = field(default_factory=dict)


@runtime_checkable
class Extractor(Protocol):
    """LLM-driven pedagogical-hierarchy extractor."""

    name: str

    def extract(self, request: ExtractionRequest) -> RawExtraction:
        """Structure the parsed artifact into a raw hierarchy. Raise
        :class:`ExtractorError` on unrecoverable failure."""
        ...
