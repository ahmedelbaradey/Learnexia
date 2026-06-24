"""Deterministic, fixture-driven mock extractor — the DEFAULT ingest backend.

It does NOT call Claude or any network service (mirrors ``MockParser``). It
derives a stable Grade→Unit→Lesson→Concept→Skill hierarchy from the BL-02
parsed-artifact, preserving Arabic + tashkeel verbatim. Two modes:

- **Fixture mode:** if the artifact carries an ``__ingest_fixture__`` block (our
  test artifacts), it echoes the declared hierarchy/metadata straight through —
  this lets tests assert exact nodes, grades, subjects, and confidences.
- **Synthetic mode:** otherwise it produces a deterministic single Unit→Lesson→
  Concept→Skill chain from the artifact's chapters/chunks, tagging the document's
  declared (or defaulted) grade + subject. Useful for smoke tests without
  bundling a full fixture.

Determinism is the whole point: re-running it for the same artifact yields the
same RawExtraction, so the derived SkillKeys are stable and the .NET upsert is
idempotent.
"""

from __future__ import annotations

from .extractor import ExtractionRequest, Extractor, ExtractorError, RawChunkSource, RawExtraction, RawNode
from .models import ALLOWED_SUBJECT_CODES, NodeType

# A deterministic diacritized Arabic synthetic lesson (tashkeel: FATHA/SHADDA/…).
_SYNTH_TITLE = "اَلْكُسُورُ"
_SYNTH_LESSON = "جَمْعُ الْكُسُورِ"


class MockExtractor:
    """Implements :class:`Extractor`."""

    name = "mock"

    def extract(self, request: ExtractionRequest) -> RawExtraction:
        fixture = request.artifact.get("__ingest_fixture__")
        if isinstance(fixture, dict):
            return self._from_fixture(request, fixture)
        return self._synthetic(request)

    # --- fixture path --------------------------------------------------------

    def _from_fixture(self, request: ExtractionRequest, fixture: dict) -> RawExtraction:
        if fixture.get("__raise__"):
            raise ExtractorError(
                fixture.get("__raise_message__", "fixture-induced extraction failure"),
                code=fixture.get("__raise_code__", "fixture_error"),
                backend=self.name,
            )

        raw_nodes = fixture.get("nodes", [])
        nodes: list[RawNode] = []
        for n in raw_nodes:
            subject_code = str(n["subject_code"]).strip().lower()
            self._assert_subject(subject_code)
            nodes.append(
                RawNode(
                    node_type=n["node_type"],
                    title=n["title"],
                    grade=int(n["grade"]),
                    subject_code=subject_code,
                    language=n.get("language", "ar"),
                    slug_source=n["slug_source"],
                    unit_slug_source=n["unit_slug_source"],
                    confidence=float(n.get("confidence", 0.95)),
                    parent_index=n.get("parent_index"),
                    difficulty=n.get("difficulty"),
                )
            )

        chunk_sources = [
            RawChunkSource(
                node_index=int(c["node_index"]),
                text=c["text"],
                source_page=c.get("source_page"),
                chapter_number=c.get("chapter_number"),
                confidence=float(c.get("confidence", 0.95)),
            )
            for c in fixture.get("chunk_sources", [])
        ]
        return RawExtraction(
            nodes=nodes,
            chunk_sources=chunk_sources,
            diagnostics={"extractor": self.name, "mode": "fixture"},
        )

    # --- synthetic path ------------------------------------------------------

    def _synthetic(self, request: ExtractionRequest) -> RawExtraction:
        artifact = request.artifact
        grade = int(artifact.get("grade", 5))
        subject_code = str(artifact.get("subject_code", "math")).strip().lower()
        self._assert_subject(subject_code)

        # Unit(0) → Lesson(1) → Concept(2) → Skill(3) — a stable single chain.
        nodes = [
            RawNode(
                node_type=NodeType.UNIT.value,
                title=_SYNTH_TITLE,
                grade=grade,
                subject_code=subject_code,
                language="ar",
                slug_source="fractions",
                unit_slug_source="fractions",
                confidence=0.92,
                parent_index=None,
            ),
            RawNode(
                node_type=NodeType.LESSON.value,
                title=_SYNTH_LESSON,
                grade=grade,
                subject_code=subject_code,
                language="ar",
                slug_source="add-fractions",
                unit_slug_source="fractions",
                confidence=0.9,
                parent_index=0,
            ),
            RawNode(
                node_type=NodeType.CONCEPT.value,
                title=_SYNTH_LESSON,
                grade=grade,
                subject_code=subject_code,
                language="ar",
                slug_source="add-fractions-concept",
                unit_slug_source="fractions",
                confidence=0.88,
                parent_index=1,
            ),
            RawNode(
                node_type=NodeType.SKILL.value,
                title=_SYNTH_LESSON,
                grade=grade,
                subject_code=subject_code,
                language="ar",
                slug_source="add-like-denominators",
                unit_slug_source="fractions",
                confidence=0.86,
                parent_index=2,
            ),
        ]

        # Build chunk sources from the artifact's text chunks, tied to the Skill node.
        chunk_sources: list[RawChunkSource] = []
        for ch in artifact.get("chunks", []):
            text = ch.get("text")
            if not text or ch.get("content_type") != "text":
                continue
            chunk_sources.append(
                RawChunkSource(
                    node_index=3,
                    text=text,
                    source_page=ch.get("source_page"),
                    chapter_number=ch.get("chapter_number"),
                    confidence=0.9,
                )
            )
        if not chunk_sources:
            # Always emit at least one diacritized chunk so downstream stages run.
            chunk_sources.append(
                RawChunkSource(node_index=3, text=_SYNTH_LESSON, source_page=1, confidence=0.9)
            )

        return RawExtraction(
            nodes=nodes,
            chunk_sources=chunk_sources,
            diagnostics={"extractor": self.name, "mode": "synthetic", "grade": grade},
        )

    @staticmethod
    def _assert_subject(subject_code: str) -> None:
        if subject_code not in ALLOWED_SUBJECT_CODES:
            raise ExtractorError(
                f"subject_code {subject_code!r} not in the 4-subject set {ALLOWED_SUBJECT_CODES}",
                code="invalid_subject",
                backend="mock",
            )


# Static type-check assurance that MockExtractor satisfies the protocol.
_: Extractor = MockExtractor()
