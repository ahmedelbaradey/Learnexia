# docs: barrier-to-entry gap analysis + 6 new stories + SRS/BRD/tasks reconciliation

## What
Analyzes `info/learnexia_barrier_to_entry_technical_implementation.md` (the four-layer moat strategy) against the product definition, then closes the gaps it surfaced across stories, SRS, BRD, and tasks.

## Why
The strategy's thesis — the moat is the **habit loop + curriculum data + behavior + gamification, not the AI model** — was only partially reflected in the backlog. Several named moats (re-engagement loop, behavioral profile, realtime gamification, data network effect, MVP skill graph) had no stories or FRs.

## Changes
- **New brief:** `docs/briefs/barrier-to-entry-gap-analysis.md` — strategy summary (BE1–BE7), traceability table, bidirectional gaps, recommendations.
- **6 new user stories** closing the partial gaps (two highest-impact first):
  - `P4-09` Re-engagement & habit notifications (BE4) — top gap
  - `P3-13` Adaptive student profile / behavioral modeling (BE2)
  - `P2-11` Author the skill dependency graph, hand-authored MVP slice (BE1)
  - `P4-10` Redis-backed realtime gamification state (BE3)
  - `P5-07` Data feedback / calibration loop (BE7)
  - `P4-11` Streak freeze, timed events & weekly challenges (BE4)
- **SRS** (`docs/SRS.md`): added FR-AD-5, FR-GM-8, FR-GM-9, FR-PA-4; extended FR-GM-7 (Redis realtime read model) and FR-CI-3 (MVP skill-graph slice); added data-model entities (§6) and traceability (§8); reconciled FR-GM-2/3 open notes.
- **BRD** (`docs/BRD.md`): closed §10 open question #4 (streak/hearts mechanics → streak freeze + config-driven dials).
- **Stale doc**: added a superseded banner to `info/learnexia_brd_technical_execution_plan.md` listing its divergences from current sources of truth.
- **Tasks**: `tasks/Backend/Phase-2-Learning-Core/P2-11-BE.md` backend breakdown; `tasks/README.md` coverage row + Phase 3–5 pending-scope note.
- Indexed all new stories in `user-stories/README.md`.

## Notes
- Docs/planning only — no code, no build/test gate.
- Phase 3–5 stories are pending task breakdown (the `tasks/` tree currently covers Phases 1–2).
- The three security-sensitive stories (P4-09, P3-13, P5-07) each note a required `security-auditor` pass before implementation.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
