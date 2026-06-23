"""MinerU / PaddleOCR fallback backend (PY-5) — behind the same ParserBackend seam.

RAG-Anything ships MinerU + PaddleOCR as its default parsers. They are NOT the
primary path (Azure DI is); they are the fallback when Azure DI is unavailable
or fails. Like Azure DI, the heavy deps are imported lazily and this backend is
devops-gated — in dev + CI the mock stands in. Chunks it produces are tagged
``parser_used=mineru`` so the fallback is traceable in the artifact (AC6).
"""

from __future__ import annotations

from ..app.logging import get_logger
from .artifact import ParsedArtifact, ParserUsed
from .base import ParserBackend, ParseRequest, ParserError

logger = get_logger(__name__)


class MineruFallbackParser:
    """Implements :class:`ParserBackend` using MinerU (RAG-Anything default).

    The real extraction wiring lands when the live OCR stack is provisioned;
    until then this raises a clear ParserError if invoked without MinerU
    installed, so the fallback path is observable rather than silently broken.
    """

    name = ParserUsed.MINERU.value

    def parse(self, request: ParseRequest) -> ParsedArtifact:
        try:
            import mineru  # noqa: F401  — lazy; only present once provisioned
        except ImportError as exc:
            raise ParserError(
                "MinerU fallback is not installed in this environment",
                code="mineru_unavailable",
                parser=self.name,
            ) from exc
        # Real MinerU extraction is wired at the devops flip (PY-7 gate).
        raise ParserError(
            "MinerU fallback extraction not yet wired (devops-gated)",
            code="mineru_not_wired",
            parser=self.name,
        )


_TYPE_CHECK: type[ParserBackend] = MineruFallbackParser  # type: ignore[assignment]
