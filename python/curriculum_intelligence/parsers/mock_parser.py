"""Deterministic, fixture-driven mock parser — the DEFAULT backend in dev + CI.

It does NOT call Azure DI or any network service. Behaviour:

- If the uploaded bytes are a JSON "fixture document" (our test files), it
  echoes the fixture's declared chapters/chunks straight through — this lets
  tests assert exact diacritics, source refs, and parser_used.
- Otherwise it emits a small deterministic Arabic artifact derived from the
  document id, including at least one tashkeel character, so smoke tests pass
  without bundling binary samples.

``parser_used`` is reported as ``azure_di`` by default so the smoke test's
primary-path assertion holds even under the mock (the mock simulates the Azure
DI primary path). A fixture may override ``parser_used`` to exercise the
fallback (e.g. ``mineru``).
"""

from __future__ import annotations

import json

from .artifact import Chapter, Chunk, ChunkType, ParsedArtifact, ParserUsed
from .base import ParserBackend, ParseRequest, ParserError

# A short diacritized Arabic phrase: "the lesson" with full tashkeel.
# Contains U+064E (FATHA), U+0651 (SHADDA), U+064F (DAMMA), U+0652 (SUKUN).
_ARABIC_DIACRITIZED = "اَلدَّرْسُ الأَوَّلُ: مُقَدِّمَةٌ فِي الرِّيَاضِيَّاتِ"


class MockParser:
    """Implements :class:`ParserBackend`."""

    name = "mock"

    def parse(self, request: ParseRequest) -> ParsedArtifact:
        fixture = self._try_load_fixture(request.file_bytes)
        if fixture is not None:
            return self._from_fixture(request, fixture)
        return self._synthetic(request)

    # --- fixture path --------------------------------------------------------

    @staticmethod
    def _try_load_fixture(data: bytes) -> dict | None:
        try:
            text = data.decode("utf-8")
        except UnicodeDecodeError:
            return None
        text = text.strip()
        if not text.startswith("{"):
            return None
        try:
            doc = json.loads(text)
        except json.JSONDecodeError:
            return None
        if isinstance(doc, dict) and doc.get("__fixture__") == "parsed_document":
            return doc
        return None

    def _from_fixture(self, request: ParseRequest, fixture: dict) -> ParsedArtifact:
        if fixture.get("__raise__"):
            raise ParserError(
                fixture.get("__raise_message__", "fixture-induced failure"),
                code=fixture.get("__raise_code__", "fixture_error"),
                parser=self.name,
            )
        chapters = [
            Chapter(
                number=c["number"],
                title=c["title"],
                page_start=c.get("page_start"),
                page_end=c.get("page_end"),
            )
            for c in fixture.get("chapters", [])
        ]
        chunks = [
            Chunk(
                content_type=ch.get("content_type", ChunkType.TEXT.value),
                text=ch.get("text"),
                image_ref=ch.get("image_ref"),
                diagram_caption=ch.get("diagram_caption"),
                source_page=ch.get("source_page"),
                source_region=ch.get("source_region"),
                chapter_number=ch.get("chapter_number"),
                parser_used=ch.get("parser_used", ParserUsed.AZURE_DI.value),
            )
            for ch in fixture.get("chunks", [])
        ]
        return ParsedArtifact(
            document_id=request.document_id,
            source_object_key=request.object_key,
            content_type=request.content_type,
            chapters=chapters,
            chunks=chunks,
            diagnostics={"backend": self.name, "mode": "fixture"},
        )

    # --- synthetic path ------------------------------------------------------

    def _synthetic(self, request: ParseRequest) -> ParsedArtifact:
        chapters = [Chapter(number=1, title="مُقَدِّمَة", page_start=1, page_end=1)]
        chunks = [
            Chunk(
                content_type=ChunkType.TEXT.value,
                text=_ARABIC_DIACRITIZED,
                source_page=1,
                source_region=[0.0, 0.0, 1.0, 0.5],
                chapter_number=1,
                parser_used=ParserUsed.AZURE_DI.value,
            ),
            Chunk(
                content_type=ChunkType.IMAGE.value,
                image_ref=f"images/{request.document_id}-fig1.png",
                diagram_caption="رَسْمٌ تَوْضِيحِيٌّ لِمُثَلَّثٍ قَائِمِ الزَّاوِيَة",
                source_page=1,
                source_region=[0.0, 0.5, 1.0, 0.5],
                chapter_number=1,
                parser_used=ParserUsed.AZURE_DI.value,
            ),
        ]
        return ParsedArtifact(
            document_id=request.document_id,
            source_object_key=request.object_key,
            content_type=request.content_type,
            chapters=chapters,
            chunks=chunks,
            diagnostics={"backend": self.name, "mode": "synthetic"},
        )


# Static type-check assurance that MockParser satisfies the protocol.
_: ParserBackend = MockParser()
