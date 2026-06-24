"""Grade-scoped, Arabic-boundary semantic chunking (BL-05-PY-3).

Splits node content into ``CurriculumChunk``-sized semantic units using
*linguistic* boundaries (Arabic sentence/paragraph punctuation), NOT a naive
token-count cut through the middle of a word. Hard rules (story ACs):

- **Tashkeel preservation:** U+064B–U+065F survive verbatim — we NEVER normalize,
  strip diacritics, or NFKD-fold the text. We only split on boundary punctuation.
- **Grade-scope:** every chunk inherits its source node's grade via the node's
  SkillKey; a chunk is produced per (node, content) pair, so a chunk can never
  span two grade levels by construction.

Boundary set: Arabic full stop ``۔``/``.``, Arabic question mark ``؟``, Arabic
semicolon ``؛``, exclamation ``!``, newlines (paragraph breaks). A long sentence
that still exceeds ``max_chunk_chars`` is split on whitespace word boundaries (a
last-resort soft cut that still never lands mid-word).
"""

from __future__ import annotations

import re

# Sentence terminators (Arabic + Latin) — keep the terminator with its sentence.
_SENTENCE_SPLIT = re.compile(r"(?<=[\.\!\?؟؛۔])\s+|\n+")
_WHITESPACE = re.compile(r"\s+")


def _split_sentences(text: str) -> list[str]:
    """Split on linguistic boundaries; preserve every character in between."""

    parts = [p.strip() for p in _SENTENCE_SPLIT.split(text)]
    return [p for p in parts if p]


def _soft_wrap(sentence: str, max_chars: int) -> list[str]:
    """Last-resort word-boundary wrap for a single over-long sentence."""

    if len(sentence) <= max_chars:
        return [sentence]
    words = _WHITESPACE.split(sentence)
    out: list[str] = []
    current = ""
    for word in words:
        candidate = f"{current} {word}".strip()
        if current and len(candidate) > max_chars:
            out.append(current)
            current = word
        else:
            current = candidate
    if current:
        out.append(current)
    return out


def chunk_text(text: str, *, max_chunk_chars: int) -> list[str]:
    """Greedily pack whole sentences into ``<= max_chunk_chars`` chunks.

    Sentence boundaries are respected first; only a single sentence that is itself
    longer than the limit is soft-wrapped on word boundaries. Returns the list of
    chunk strings in document order, diacritics fully intact.
    """

    text = (text or "").strip()
    if not text:
        return []

    chunks: list[str] = []
    current = ""
    for sentence in _split_sentences(text):
        for piece in _soft_wrap(sentence, max_chunk_chars):
            candidate = f"{current} {piece}".strip()
            if current and len(candidate) > max_chunk_chars:
                chunks.append(current)
                current = piece
            else:
                current = candidate
    if current:
        chunks.append(current)
    return chunks
