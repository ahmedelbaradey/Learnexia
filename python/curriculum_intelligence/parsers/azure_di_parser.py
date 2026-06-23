"""Azure Document Intelligence backend (PRIMARY) — wired but devops-gated (PY-2).

This is the REAL implementation. It is never exercised without a provisioned
Azure DI resource: the factory only selects it when ``PARSER_BACKEND=azure_di``
AND both ``AZURE_DI_ENDPOINT`` + ``AZURE_DI_KEY`` are present. CI runs the mock.

It calls ``prebuilt-layout`` with the ``ar`` locale and preserves diacritics
(``ensure_ascii=False`` end-to-end; no NFKC normalization that would strip
U+064B–U+065F). On Azure DI failure the worker (PY-5) falls back to MinerU and
records ``parser_used=mineru`` per chunk.

The ``azure-ai-documentintelligence`` SDK is imported lazily so the package can
be imported (and the mock used) without the SDK installed.
"""

from __future__ import annotations

from ..app.config import AzureDocumentIntelligenceConfig
from ..app.logging import get_logger
from .artifact import Chapter, Chunk, ChunkType, ParsedArtifact, ParserUsed
from .base import ParserBackend, ParseRequest, ParserError

logger = get_logger(__name__)


class AzureDocumentIntelligenceParser:
    """Implements :class:`ParserBackend` using Azure DI ``prebuilt-layout`` (ar)."""

    name = "azure_di"

    def __init__(self, config: AzureDocumentIntelligenceConfig) -> None:
        if not config.is_configured:
            raise ParserError(
                "Azure DI is not configured (endpoint/key missing); use the mock backend",
                code="azure_di_unconfigured",
                parser=self.name,
            )
        self._config = config
        self._client = None

    def _get_client(self):
        if self._client is None:
            # Lazy import — only when the live backend is actually selected.
            from azure.ai.documentintelligence import DocumentIntelligenceClient
            from azure.core.credentials import AzureKeyCredential

            self._client = DocumentIntelligenceClient(
                endpoint=self._config.endpoint,  # type: ignore[arg-type]
                credential=AzureKeyCredential(self._config.key),  # type: ignore[arg-type]
            )
        return self._client

    def parse(self, request: ParseRequest) -> ParsedArtifact:
        try:
            from azure.ai.documentintelligence.models import AnalyzeDocumentRequest

            client = self._get_client()
            poller = client.begin_analyze_document(
                model_id=self._config.model_id,
                analyze_request=AnalyzeDocumentRequest(bytes_source=request.file_bytes),
                locale=self._config.locale,
            )
            result = poller.result()
        except ParserError:
            raise
        except Exception as exc:  # noqa: BLE001 — turned into a typed ParserError
            raise ParserError(
                f"Azure DI analyze failed: {exc}",
                code="azure_di_analyze_failed",
                parser=self.name,
            ) from exc

        return self._normalize(request, result)

    # --- normalization: Azure DI result -> ParsedArtifact --------------------

    def _normalize(self, request: ParseRequest, result) -> ParsedArtifact:
        chunks: list[Chunk] = []

        # Paragraphs → text chunks with page + bounding-region source refs.
        for para in getattr(result, "paragraphs", None) or []:
            region = self._first_region(getattr(para, "bounding_regions", None))
            page = region[0] if region else None
            chunks.append(
                Chunk(
                    content_type=ChunkType.TEXT.value,
                    text=getattr(para, "content", None),  # diacritics preserved verbatim
                    source_page=page,
                    source_region=region[1] if region else None,
                    parser_used=ParserUsed.AZURE_DI.value,
                )
            )

        # Tables → table chunks (serialized cell text).
        for table in getattr(result, "tables", None) or []:
            region = self._first_region(getattr(table, "bounding_regions", None))
            cells = " | ".join(getattr(c, "content", "") or "" for c in getattr(table, "cells", []) or [])
            chunks.append(
                Chunk(
                    content_type=ChunkType.TABLE.value,
                    text=cells,
                    source_page=region[0] if region else None,
                    source_region=region[1] if region else None,
                    parser_used=ParserUsed.AZURE_DI.value,
                )
            )

        # Figures → image chunks (captioning handled later by the VLM seam).
        for fig in getattr(result, "figures", None) or []:
            region = self._first_region(getattr(fig, "bounding_regions", None))
            chunks.append(
                Chunk(
                    content_type=ChunkType.IMAGE.value,
                    source_page=region[0] if region else None,
                    source_region=region[1] if region else None,
                    parser_used=ParserUsed.AZURE_DI.value,
                )
            )

        chapters = self._infer_chapters(result)

        return ParsedArtifact(
            document_id=request.document_id,
            source_object_key=request.object_key,
            content_type=request.content_type,
            chapters=chapters,
            chunks=chunks,
            diagnostics={"backend": self.name, "model": self._config.model_id, "locale": self._config.locale},
        )

    @staticmethod
    def _first_region(bounding_regions):
        if not bounding_regions:
            return None
        br = bounding_regions[0]
        page = getattr(br, "page_number", None)
        polygon = getattr(br, "polygon", None)
        return (page, list(polygon) if polygon else None)

    @staticmethod
    def _infer_chapters(result) -> list[Chapter]:
        """Heuristic chapter detection from section headings. The benchmark/live
        tuning (PY-7) refines this; for the wired-but-gated build a single
        document-spanning chapter is the safe default if no headings are found."""

        chapters: list[Chapter] = []
        sections = getattr(result, "sections", None) or []
        number = 0
        for _section in sections:
            number += 1
            chapters.append(Chapter(number=number, title=f"Section {number}"))
        if not chapters:
            page_count = len(getattr(result, "pages", None) or []) or None
            chapters = [Chapter(number=1, title="Document", page_start=1, page_end=page_count)]
        return chapters


# Note: not instantiated at import time — requires config + SDK.
_TYPE_CHECK: type[ParserBackend] = AzureDocumentIntelligenceParser  # type: ignore[assignment]
