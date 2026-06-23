"""Backend selection — mock (default) vs Azure DI (live, devops-gated).

Selection rule (ADR-0004 §5):
- ``PARSER_BACKEND=mock`` (default) → :class:`MockParser`, used in dev + CI.
- ``PARSER_BACKEND=azure_di`` AND Azure DI endpoint+key present → live
  :class:`AzureDocumentIntelligenceParser`.
- ``PARSER_BACKEND=azure_di`` but credentials missing → fall back to the mock
  with a loud warning (so a misconfigured live deploy degrades, not crashes).

The MinerU fallback is selected at RUNTIME by the worker on an Azure DI failure
(PY-5), not by config — so it is not returned here.
"""

from __future__ import annotations

from ..app.config import ParserBackendKind, Settings
from ..app.logging import get_logger
from .base import ParserBackend
from .fallback_parser import MineruFallbackParser
from .mock_parser import MockParser

logger = get_logger(__name__)


def build_parser(settings: Settings) -> ParserBackend:
    """Return the primary parser backend per config."""

    if settings.parser_backend is ParserBackendKind.AZURE_DI:
        if settings.azure_di.is_configured:
            # Imported here so the SDK is only required when actually selected.
            from .azure_di_parser import AzureDocumentIntelligenceParser

            logger.info("parser backend = azure_di (live)")
            return AzureDocumentIntelligenceParser(settings.azure_di)
        logger.warning(
            "PARSER_BACKEND=azure_di but AZURE_DI_ENDPOINT/AZURE_DI_KEY are unset; "
            "falling back to the mock parser"
        )
    logger.info("parser backend = mock (default)")
    return MockParser()


def build_fallback_parser(settings: Settings) -> ParserBackend:
    """Return the fallback backend used when the primary (Azure DI) fails.

    In dev/CI (mock primary) there is nothing to fall back from, so the mock is
    reused; with a live Azure DI primary this is MinerU.
    """

    if settings.parser_backend is ParserBackendKind.AZURE_DI and settings.azure_di.is_configured:
        return MineruFallbackParser()
    return MockParser()
