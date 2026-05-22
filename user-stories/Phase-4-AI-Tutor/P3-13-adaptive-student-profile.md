# Build the adaptive student profile (behavioral modeling)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 4 — AI Tutor (Week 6–7)
- **Epic:** Adaptivity & Student Modeling
- **Issue type:** Story
- **Story Points:** 8 — derivation jobs over captured signals + profile store + integration into prompts & adaptivity; the richest unbuilt moat layer.
- **Labels:** `adaptivity`, `engine`, `backend`, `data`, `student-modeling`
- **Requirements:** FR-AD-5 (behavioral student modeling — SRS §4.4)

## Description
As a student, I want the system to learn *how* I learn — not just whether I got answers right — so that explanations, pacing, and recommendations fit me personally.

> **Why this story exists:** the barrier-to-entry strategy describes a rich "Adaptive Learning Profile" (e.g. "visual learner, weak in fractions, bored after 8 minutes, improves with short quizzes"). Today only correctness/time/mastery are captured (`P2-08`, `P3-09`); the SRS already has a `StudentProfile.PreferredExplanationStyle` column (§6) that **no story populates**. This closes BE2 (gap 3a-2) and the BE5 "auto-adapt explanation style" nuance (gap 3a-3).

## Acceptance Criteria
- A `StudentProfile` derives, from already-captured `StudentAnswer` / `Attempt` / session signals (`P2-08`, `P5-03`), at minimum: **question-type affinity** (which formats the student succeeds at), **recurring-error clusters** (skills/concepts with repeated wrong patterns), an **attention-span / session-fatigue** signal ("accuracy drops after N minutes"), and a derived **`PreferredExplanationStyle`**.
- Derivation is **rule-based and explainable** (matching the deterministic adaptivity decision in BRD/CLAUDE), recomputed on a schedule and/or after each session — not a black-box model.
- The profile is queryable per student and exposed to `P3-03` (prompt builder consumes `PreferredExplanationStyle` + weak areas) and `P3-08` (adaptivity may shorten sessions / switch explanation style for fatigue-prone or struggling students).
- Cold-start is handled: a new student gets safe defaults; the profile fills in as data accumulates (feeds the BE7 network effect).
- All derived attributes are inspectable for a child (transparency) and never expose another child's data.

## Acceptance Criteria — out of scope
- No ML model training (rule-based only for MVP). No new raw-signal capture beyond what `P2-08`/`P5-03` already record.

## Notes
- **Security/privacy:** builds a behavioral profile of a minor — route through `security-auditor` (child-privacy, data minimization, purpose limitation).
- Depends on: `P2-08` (granular answers), `P3-09` (mastery), `P5-03` (session/engagement signals). Feeds: `P3-03` (prompts), `P3-08` (adaptivity), `P5-02` (weak areas), `P4-09`/`P4-11` (fatigue-aware nudges).
- Closes gaps **3a-2** and **3a-3**; turns the dormant `PreferredExplanationStyle` field into a live signal.
