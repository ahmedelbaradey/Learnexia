"""Stable SkillKey derivation — the idempotency anchor for ingested nodes.

Format (grounded in BL-04 — `tasks/Backend/Backlog-Phase-2-Plus/BL-04-BE.md`
and `docs/dev/HANDOFF.md`):

    {subjectCode}.grade{N}.{unitSlug}.{skillSlug}

e.g. ``math.grade4.fractions.add-unlike-denominators``.

Slugify rule (matches the .NET `BackfillSkillKeys` migration exactly so both
sides derive the SAME key for the same node): lowercase, spaces → hyphens,
strip every character that is not ``[a-z0-9-]``, collapse repeated hyphens, and
trim leading/trailing hyphens. Arabic letters are NOT in ``[a-z0-9]`` so an
Arabic title slugifies to empty — the caller is expected to pass an ASCII/Latin
slug source (an English/transliterated title or an explicit slug from the
extractor); we keep this rule identical to .NET rather than inventing an
Arabic-aware transliteration that the .NET side would not reproduce.

The key is DERIVED (deterministic) — re-running ingestion for the same document
produces byte-identical keys, which is what makes the .NET natural-key upsert
idempotent (it matches existing nodes by SkillKey).
"""

from __future__ import annotations

import re

_NON_SLUG = re.compile(r"[^a-z0-9]+")
_MULTI_HYPHEN = re.compile(r"-{2,}")


def slugify(value: str) -> str:
    """Lowercase / hyphenate / strip per the .NET `BackfillSkillKeys` rule."""

    lowered = (value or "").strip().lower()
    hyphenated = _NON_SLUG.sub("-", lowered)
    collapsed = _MULTI_HYPHEN.sub("-", hyphenated)
    return collapsed.strip("-")


def build_skill_key(
    subject_code: str,
    grade: int,
    unit_slug_source: str,
    leaf_slug_source: str,
) -> str:
    """Compose a stable ``{subjectCode}.grade{N}.{unitSlug}.{leafSlug}`` key.

    ``unit_slug_source`` / ``leaf_slug_source`` are slugified; ``subject_code``
    is already a short lowercase code (``math``/``science``/``arabic``/``english``).
    A node that has no distinct leaf (e.g. a Unit node) passes its own title as
    the leaf source, giving ``…{unitSlug}.{unitSlug}`` — still stable + unique
    per node-level because the level prefix differs via the parent chain.
    """

    subject = slugify(subject_code) or "unknown"
    unit_slug = slugify(unit_slug_source) or "unit"
    leaf_slug = slugify(leaf_slug_source) or unit_slug
    return f"{subject}.grade{int(grade)}.{unit_slug}.{leaf_slug}"
