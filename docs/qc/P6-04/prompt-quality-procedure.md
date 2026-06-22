# P6-04 — Prompt-Quality Validation Procedure (AC2, devops-gated)

> Story P6-04 (AC2): "Prompt tuning is validated against a sample so explanations/questions meet a quality bar across subjects." **This requires live model output + human review → it is a devops/launch-gate run with real keys, NOT executed in this story (no keys here).** This doc defines the sample, the quality bar, and how to run it. Complements P6-02 Gate-B (safety) — this is about output **quality**, that is about **safety**.

## Why this is not automatable / not in CI
The 4-intent AI Helper (Explain / Hint / Why-Wrong-Simplify / Similar-Example) produces free-form Arabic/English text from a real LLM. Whether that text is **pedagogically good** (correct, grade-appropriate, on-topic, well-toned, RTL-clean) is a **human judgement** against real model output — it can't be asserted offline against canned responses. The offline `Ai.EvalTests` harness validates **safety parse/map/fail-closed** (P6-02); **quality** needs the live tier + a reviewer.

## Sample set (the quality bar is checked against this)
Minimum **8 cells**: 4 subjects (Math, Science, Arabic, English) × 2 languages (ar, en), and within each, exercise the 4 intents on a representative grade-appropriate prompt. Target ≥ 3 prompts/cell ⇒ ~24–32 generations per intent-pass. Draw prompts from the seeded curriculum (real lessons/skills) so grounding is exercised.

## Quality bar (reviewer rubric — each generation scored pass/fail)
1. **Correct** — factually right; no hallucinated facts; consistent with the grounded curriculum chunk (RAG on).
2. **Grade-appropriate** — vocabulary/complexity matches the child's grade (the `Grade` JWT claim now drives this — AI-DEFECT-1 resolved).
3. **On-topic & intent-faithful** — Explain explains; Hint nudges **without revealing** the answer; Why-Wrong addresses the actual mistake; Similar-Example stays analogous.
4. **Tone** — encouraging, kid-friendly, non-judgemental (matches the tone-frame system prompt).
5. **Language/RTL** — fluent Arabic (not MT-stilted), correct RTL, no language bleed.
6. **Latency** — within the NFR-1 **< 4 s** budget (cross-check the P6-01/devops AI run).

**Pass threshold:** ≥ 95% of generations pass the rubric, **zero** "incorrect/harmful" outputs (a single factually-wrong or answer-revealing generation = a tuning bug to fix before launch).

## How to run (devops, with live keys)
1. Provision keys + RAG per `docs/dev/AI-ACTIVATION-RUNBOOK.md` (provider key → BGE-M3 TEI → re-embed → `ContextProvider=Rag`).
2. Drive the 4 intents over the sample set against the running keyed Host (the SSE endpoints `POST /api/AiTutor/{Explain,Hint,...}`), capturing each full SSE response. (The `Ai.EvalTests` `EvalLive` harness from P6-02 #215 can be extended to dump generations for review, or use a small script.)
3. A human (ar + en literate) scores each generation against the rubric.
4. Record results + any tuning changes (prompt edits live in `PromptBuilder` / the tone-frame templates) and re-run until the bar is met.
5. Sign off in `launch-exit-criteria.md`.

## Status
**NOT RUN here** (no keys). Backend is ready (prompt builder, 4 intents, RAG, tone-frame, Grade-claim grounding all merged). This is a **devops/launch-gate** activity owned alongside the AI flip-to-live.
