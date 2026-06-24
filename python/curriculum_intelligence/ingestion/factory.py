"""Extractor selection — mock (default) vs Claude (live, devops-gated).

Selection rule (mirrors ``parsers/factory.py`` / ADR-0004 §5):
- ``EXTRACTOR_BACKEND=mock`` (default) → :class:`MockExtractor`, used in dev + CI.
- ``EXTRACTOR_BACKEND=claude`` AND ``ANTHROPIC_API_KEY`` present → live
  :class:`ClaudeExtractor`.
- ``EXTRACTOR_BACKEND=claude`` but the key is missing → fall back to the mock
  with a loud warning (a misconfigured live deploy degrades, it does not crash —
  and CI can never accidentally call out to Claude).
"""

from __future__ import annotations

from ..app.config import ExtractorBackendKind, Settings
from ..app.logging import get_logger
from .extractor import Extractor
from .mock_extractor import MockExtractor

logger = get_logger(__name__)


def build_extractor(settings: Settings) -> Extractor:
    """Return the hierarchy-extraction backend per config."""

    if settings.ingestion.extractor_backend is ExtractorBackendKind.CLAUDE:
        if settings.anthropic_configured and settings.anthropic_api_key:
            # Imported here so the SDK is only required when actually selected.
            from .claude_extractor import ClaudeExtractor

            logger.info("extractor backend = claude (live, model=%s)", settings.ingestion.anthropic_model)
            return ClaudeExtractor(settings.ingestion, settings.anthropic_api_key)
        logger.warning(
            "EXTRACTOR_BACKEND=claude but ANTHROPIC_API_KEY is unset; " "falling back to the mock extractor"
        )
    logger.info("extractor backend = mock (default)")
    return MockExtractor()
