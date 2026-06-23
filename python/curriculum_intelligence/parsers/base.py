"""The ``ParserBackend`` protocol — the seam behind which OCR/parse lives.

Every backend (mock, Azure DI primary, MinerU/PaddleOCR fallback) implements
this single interface, so the worker is agnostic to which one runs. The backend
is selected by config (``PARSER_BACKEND`` env), defaulting to the deterministic
mock in dev + CI (ADR-0004 §5).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Protocol, runtime_checkable

from .artifact import ParsedArtifact


@dataclass(frozen=True)
class ParseRequest:
    """Everything a backend needs to parse one document."""

    document_id: int
    object_key: str
    content_type: str
    file_bytes: bytes


class ParserError(Exception):
    """Raised by a backend on an unrecoverable parse failure.

    The worker turns this into a terminal ``Failed`` job with diagnostics; it
    does NOT retry (retry policy is owned by .NET).
    """

    def __init__(self, message: str, *, code: str = "parse_error", parser: str | None = None) -> None:
        super().__init__(message)
        self.code = code
        self.parser = parser


@runtime_checkable
class ParserBackend(Protocol):
    """OCR/parse + multimodal extraction backend."""

    name: str

    def parse(self, request: ParseRequest) -> ParsedArtifact:
        """Parse the document bytes into a normalized artifact. Raise
        :class:`ParserError` on unrecoverable failure."""
        ...
