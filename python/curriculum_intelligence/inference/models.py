"""The infer-edges-lane data model + the ``ResultJson`` contract (BL-03-PY-4).

THE FROZEN CROSS-PROCESS CONTRACT (the .NET ``EdgeInferenceAdvanceService``
deserializes this — emit these EXACT snake_case keys; this is the BL-05-divergence
guard, do not deviate):

    {
      "inference_model": "lightrag-mock-v1",     # provenance string
      "edges": [
        {
          "source_skill_key": "math.grade4.division.long-division",
          "target_skill_key": "math.grade4.fractions.add-unlike-denominators",
          "relationship_type": "Prerequisite",   # "Prerequisite" | "Related" (PascalCase)
          "strength":   0.82,                     # 0..1 float
          "confidence": 0.91                      # 0..1 float
        }
      ]
    }

Field-naming notes for the .NET side:
- ``source_skill_key`` / ``target_skill_key`` use the SkillKey stable identity
  (``{subjectCode}.grade{N}.{unitSlug}.{leafSlug}`` — the SAME derivation as the
  BL-05 ``ingestion/skill_key.py``; the .NET side resolves SkillKey -> node id).
- ``relationship_type`` is EXACTLY ``"Prerequisite"`` or ``"Related"`` (PascalCase
  string on the wire, consistent with the ``JobType``/``Status`` string contract);
  .NET maps it onto the ``CurriculumRelationshipType`` enum.
- ``strength`` + ``confidence`` are 0..1 floats. The Python worker NEVER gates on
  them; the .NET side RE-CLAMPS both to [0,1] (mirror ``ClampConfidence``) — the
  model's values are advisory only.
- A terminal failure writes the failure shape consistent with the parse + ingest
  lanes (``Status='Failed'`` + diagnostics); the worker NEVER touches RetryCount
  (.NET owns retry, ADR-0004 §3).
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from enum import Enum


class RelationshipType(str, Enum):
    """The two relationship kinds, PascalCase on the wire (matches .NET)."""

    PREREQUISITE = "Prerequisite"
    RELATED = "Related"


@dataclass
class InferredEdge:
    """One inferred edge — keyed by SkillKey, scored, ready for the .NET side."""

    source_skill_key: str
    target_skill_key: str
    relationship_type: str
    strength: float
    confidence: float

    def to_dict(self) -> dict:
        return {
            "source_skill_key": self.source_skill_key,
            "target_skill_key": self.target_skill_key,
            "relationship_type": self.relationship_type,
            "strength": round(float(self.strength), 4),
            "confidence": round(float(self.confidence), 4),
        }


@dataclass
class InferenceResult:
    """The structured output of one infer-edges run, serialized into ``ResultJson``."""

    inference_model: str
    edges: list[InferredEdge] = field(default_factory=list)
    diagnostics: dict = field(default_factory=dict)

    def to_dict(self) -> dict:
        # Only the two frozen keys are part of the cross-process contract the .NET
        # side reads; ``diagnostics`` is additive (the deserializer ignores unknown
        # keys, mirroring the ingest lane's diagnostics block).
        return {
            "inference_model": self.inference_model,
            "edges": [e.to_dict() for e in self.edges],
            "diagnostics": self.diagnostics,
        }

    def to_json(self) -> str:
        # ensure_ascii=False keeps any non-ASCII content as real characters,
        # matching the parse/ingest serializers (SkillKeys are ASCII, but stay
        # consistent across the three lanes).
        return json.dumps(self.to_dict(), ensure_ascii=False)
