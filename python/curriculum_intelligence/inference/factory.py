"""Inferer selection — mock (default) vs LightRAG (live, devops-gated).

Selection rule (mirrors ``ingestion/factory.py`` / ADR-0004 §5):
- ``INFERER_BACKEND=mock`` (default) → :class:`MockEdgeInferer`, used in dev + CI.
- ``INFERER_BACKEND=lightrag`` AND ``ANTHROPIC_API_KEY`` present → live
  :class:`LightRagEdgeInferer`.
- ``INFERER_BACKEND=lightrag`` but the key is missing → fall back to the mock with
  a loud warning (a misconfigured live deploy degrades, it does not crash — and CI
  can never accidentally call out to the LLM).
"""

from __future__ import annotations

from ..app.config import InfererBackendKind, Settings
from ..app.logging import get_logger
from .inferer import EdgeInferer
from .mock_inferer import MockEdgeInferer

logger = get_logger(__name__)


def build_inferer(settings: Settings) -> EdgeInferer:
    """Return the edge-inference backend per config."""

    if settings.inference.inferer_backend is InfererBackendKind.LIGHTRAG:
        if settings.anthropic_configured and settings.anthropic_api_key:
            # Imported here so the SDK is only required when actually selected.
            from .lightrag_inferer import LightRagEdgeInferer

            logger.info("inferer backend = lightrag (live, model=%s)", settings.inference.anthropic_model)
            return LightRagEdgeInferer(settings.inference, settings.anthropic_api_key)
        logger.warning(
            "INFERER_BACKEND=lightrag but ANTHROPIC_API_KEY is unset; " "falling back to the mock inferer"
        )
    logger.info("inferer backend = mock (default)")
    return MockEdgeInferer(inference_model=settings.inference.inference_model)
