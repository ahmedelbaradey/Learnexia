"""The ingest-lane data model + the ``ResultJson`` contract (BL-05-PY-4).

THE CONTRACT (frozen — the .NET ``IngestJobAdvanceService`` deserializes this):

    {
      "schema_version": "1.0",
      "nodes": [
        {
          "node_type": "Unit" | "Lesson" | "Concept" | "Skill",
          "skill_key": "math.grade5.fractions.add-fractions",   # stable id
          "parent_skill_key": "math.grade5.fractions.fractions" | null,
          "title": "...",                 # Arabic, diacritics preserved
          "grade": 5,
          "subject_code": "math",         # ∈ {math, science, arabic, english}
          "language": "ar" | "en",
          "difficulty": 1..5 | null,
          "confidence": 0.0..1.0          # emitted; .NET applies the 0.7 cutoff
        }
      ],
      "chunks": [
        {
          "content": "...",               # diacritized Arabic text, preserved
          "content_type": "text",
          "source_page": 1 | null,
          "chapter_number": 1 | null,
          "node_skill_key": "math.grade5.fractions.add-fractions",
          "confidence": 0.0..1.0
        }
      ],
      "flags": [
        {
          "kind": "low_confidence_node" | "low_confidence_chunk" | "unmapped_chunk",
          "ref": "<skill_key or chunk index>",
          "confidence": 0.0..1.0,
          "reason": "..."
        }
      ],
      "diagnostics": { "extractor": "...", "node_count": N, "chunk_count": N, ... }
    }

Field-naming notes for the .NET side:
- ``node_type`` values are PascalCase (``Unit``/``Lesson``/``Concept``/``Skill``)
  so they map straight onto the .NET pedagogical levels.
- ``subject_code`` is the lowercase 4-subject code; .NET resolves it to a
  ``SubjectId``. The worker ENFORCES the 4-subject set (no Social Studies).
- ``confidence`` is a 0..1 float on EVERY node + chunk. The Python worker NEVER
  applies the threshold — it only emits values + advisory ``flags``; the 0.7
  cutoff + review-routing is the .NET side's job (lead decision).
- Diacritics (U+064B–U+065F) survive — serialized with ``ensure_ascii=False``.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from enum import Enum

SCHEMA_VERSION = "1.0"

# The locked 4-subject set (CLAUDE.md product decision — no Social Studies).
ALLOWED_SUBJECT_CODES = ("math", "science", "arabic", "english")


class NodeType(str, Enum):
    UNIT = "Unit"
    LESSON = "Lesson"
    CONCEPT = "Concept"
    SKILL = "Skill"


class FlagKind(str, Enum):
    LOW_CONFIDENCE_NODE = "low_confidence_node"
    LOW_CONFIDENCE_CHUNK = "low_confidence_chunk"
    UNMAPPED_CHUNK = "unmapped_chunk"


@dataclass
class HierarchyNode:
    node_type: str
    skill_key: str
    title: str
    grade: int
    subject_code: str
    language: str
    confidence: float
    parent_skill_key: str | None = None
    difficulty: int | None = None

    def to_dict(self) -> dict:
        return {
            "node_type": self.node_type,
            "skill_key": self.skill_key,
            "parent_skill_key": self.parent_skill_key,
            "title": self.title,
            "grade": self.grade,
            "subject_code": self.subject_code,
            "language": self.language,
            "difficulty": self.difficulty,
            "confidence": round(float(self.confidence), 4),
        }


@dataclass
class Chunk:
    content: str
    node_skill_key: str
    confidence: float
    content_type: str = "text"
    source_page: int | None = None
    chapter_number: int | None = None

    def to_dict(self) -> dict:
        return {
            "content": self.content,
            "content_type": self.content_type,
            "source_page": self.source_page,
            "chapter_number": self.chapter_number,
            "node_skill_key": self.node_skill_key,
            "confidence": round(float(self.confidence), 4),
        }


@dataclass
class Flag:
    kind: str
    ref: str
    confidence: float
    reason: str

    def to_dict(self) -> dict:
        return {
            "kind": self.kind,
            "ref": self.ref,
            "confidence": round(float(self.confidence), 4),
            "reason": self.reason,
        }


@dataclass
class IngestionResult:
    """The structured output of one ingest run, serialized into ``ResultJson``."""

    nodes: list[HierarchyNode] = field(default_factory=list)
    chunks: list[Chunk] = field(default_factory=list)
    flags: list[Flag] = field(default_factory=list)
    diagnostics: dict = field(default_factory=dict)

    def to_dict(self) -> dict:
        return {
            "schema_version": SCHEMA_VERSION,
            "nodes": [n.to_dict() for n in self.nodes],
            "chunks": [c.to_dict() for c in self.chunks],
            "flags": [f.to_dict() for f in self.flags],
            "diagnostics": self.diagnostics,
        }

    def to_json(self) -> str:
        # ensure_ascii=False keeps Arabic + tashkeel as real characters, matching
        # the shipped artifact serializer.
        return json.dumps(self.to_dict(), ensure_ascii=False)
