# Build personalized tutor prompts

- **Project:** Learnexia
- **Sprint / Phase:** Phase 3 — AI Tutor (Week 5–6)
- **Epic:** Prompt Builder & Tutor
- **Issue type:** Story
- **Story Points:** 5 — context assembly from several sources + per-subject templates; quality-critical.
- **Labels:** `ai`, `prompt`, `backend`
- **Requirements:** FR-AI-5

## Description
As a student, I want the tutor to take my grade, age, language, weak areas, and curriculum context into account, so that explanations and questions fit me personally and in a child-safe tone.

## Acceptance Criteria
- The Prompt Builder injects grade, age, language, retrieved curriculum context, weak areas, and a child-safe tone into every prompt.
- Subject-specific templates exist for the 4 MVP subjects: Math step-by-step, Science visual, Arabic & English language vocab/grammar.
- Given Arabic language preference, then prompts produce Arabic output.
- Missing optional context (e.g., no weak areas yet) degrades gracefully without breaking the prompt.

## Notes
- Covers A2.1 + A2.4. Student context injection per architecture (PLAN §8). Weak areas come from FR-AD (P3-09).
- **Product decision (overrides BRD §4):** the Social-Studies "storytelling" template is dropped — MVP is 4 subjects.
