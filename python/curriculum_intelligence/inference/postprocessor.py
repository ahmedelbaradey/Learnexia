"""Post-processing for inferred candidate edges (BL-03-PY-3).

Runs after the inferer (mock or live) and before serialization. Pure functions
over the candidate list — they normalize the batch so the .NET side receives a
clean, deterministic set:

- **dedup** — collapse duplicate ``(source, target, relationship_type)`` triples,
  averaging strength + confidence (so a backend that proposes the same edge twice
  does not inflate the queue). This is WITHIN one batch; the .NET idempotency
  check (BE-3) handles cross-batch dedup against existing ``KGSuggestion`` rows.
- **drop self-loops** — an edge whose source == target is meaningless.
- **enforce the 4-subject domain** — both endpoints' SkillKeys must carry a
  subject prefix in {math, science, arabic, english} (CLAUDE.md product decision —
  no Social Studies). The subject is the SkillKey's leading dotted segment.

The functions never raise on a bad candidate — they drop it (fail-soft), so one
malformed edge can't fail the whole job. ``post_process`` chains them in order.
"""

from __future__ import annotations

from .inferer import CandidateEdge
from .models import RelationshipType
from .scoring import clamp

# The locked 4-subject set (mirrors ``ingestion.models.ALLOWED_SUBJECT_CODES``).
ALLOWED_SUBJECT_CODES = ("math", "science", "arabic", "english")

_VALID_RELATIONSHIPS = {RelationshipType.PREREQUISITE.value, RelationshipType.RELATED.value}


def _subject_of(skill_key: str) -> str:
    """The leading dotted segment of a SkillKey is the subject code."""

    return (skill_key or "").split(".", 1)[0].strip().lower()


def drop_self_loops(edges: list[CandidateEdge]) -> list[CandidateEdge]:
    return [e for e in edges if e.source_skill_key and e.source_skill_key != e.target_skill_key]


def enforce_subject_domain(edges: list[CandidateEdge]) -> list[CandidateEdge]:
    """Keep only edges whose BOTH endpoints are in the 4-subject domain and whose
    relationship_type is one of the two valid kinds."""

    kept: list[CandidateEdge] = []
    for e in edges:
        if e.relationship_type not in _VALID_RELATIONSHIPS:
            continue
        if _subject_of(e.source_skill_key) not in ALLOWED_SUBJECT_CODES:
            continue
        if _subject_of(e.target_skill_key) not in ALLOWED_SUBJECT_CODES:
            continue
        kept.append(e)
    return kept


def dedup(edges: list[CandidateEdge]) -> list[CandidateEdge]:
    """Collapse duplicate ``(source, target, relationship_type)`` triples, averaging
    strength + confidence. Stable: first occurrence sets the position."""

    order: list[tuple[str, str, str]] = []
    acc: dict[tuple[str, str, str], list[CandidateEdge]] = {}
    for e in edges:
        key = (e.source_skill_key, e.target_skill_key, e.relationship_type)
        if key not in acc:
            acc[key] = []
            order.append(key)
        acc[key].append(e)

    merged: list[CandidateEdge] = []
    for key in order:
        group = acc[key]
        src, tgt, rel = key
        avg_strength = sum(clamp(g.strength) for g in group) / len(group)
        avg_confidence = sum(clamp(g.confidence) for g in group) / len(group)
        merged.append(
            CandidateEdge(
                source_skill_key=src,
                target_skill_key=tgt,
                relationship_type=rel,
                strength=round(avg_strength, 4),
                confidence=round(avg_confidence, 4),
            )
        )
    return merged


def post_process(edges: list[CandidateEdge]) -> list[CandidateEdge]:
    """Chain: drop self-loops -> enforce 4-subject domain + valid relationship -> dedup."""

    return dedup(enforce_subject_domain(drop_self_loops(edges)))
