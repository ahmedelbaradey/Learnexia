# Carry-over (Backend) — Phase 1 & 2 gap closure, scheduled into the Phase 3 wave

> **Type:** carry-over / cleanup batch (not a gamification story). These are the **Phase 1/2 backend gaps** surfaced by the 2026-06-08 feature gap-analysis (verified against `origin/main`), pulled into the Phase-3 wave so they ship alongside gamification. Each task keeps its **original story ID**.
> **Source:** feature gap-analysis (Phase 1 + Phase 2, both stacks). Backend Phase 1 is complete — the only backend gap is the quiz **Matching** type (Phase 2).
> Pair with the FE carry-over: [../../Frontend/student-app/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-FE.md](../../Frontend/student-app/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-FE.md).

## Tasks
| ID | Origin | Task | Artifact | Deps | Est (h) |
|---|---|---|---|---|---|
| CO-BE-1 | P2-06-BE / P2-07-BE | **Define the Matching answer payload shape** (e.g. `{left[], right[], pairs:[{l,r}]}`) — coordinate the wire-contract with the FE renderer (CO-FE-5) before coding | contract note in `Learning.Application` + shared with FE | — | 2 |
| CO-BE-2 | P2-07-BE | **Implement `AnswerComparator` Matching comparison** — replace the `OrdinalIgnoreCase` fall-through (`AnswerComparator.cs:41-44`, `TODO P2-07.b`) with real pair-mapping equality (order-independent); return correct `IsCorrect` + per-pair feedback | `backend/src/Modules/Learning/.../Domain/Services/AnswerComparator.cs` | CO-BE-1 | 4 |
| CO-BE-3 | P2-10-BE | **Seed ≥1 Matching question** (plus optionally TrueFalse/FillInBlank) so the path has real demo data — `SeedDemoLessonContentAsync` currently seeds only `QuestionType.MCQ` (`LearningSeeder.cs:455-473`) | `backend/src/Modules/Learning/.../Persistence/Seed/LearningSeeder.cs` | CO-BE-2 | 2 |
| CO-BE-4 | P2-06 | **api-tester**: submit a Matching answer end-to-end (correct + wrong + malformed → 422), assert `IsCorrect` + the granular `StudentAnswer` row | `Learnexia.IntegrationTests` | CO-BE-1..3 | 3 |

## Acceptance-criteria coverage
- Matching answer shape defined + comparator implemented (order-independent pair equality) → **CO-BE-1, CO-BE-2** (closes P2-06/P2-07 backend gap)
- Matching question exercised with seeded demo data → **CO-BE-3** (closes P2-10 gap)
- Running-API validation of the Matching submit path → **CO-BE-4**

## Notes
- **Learning-module-scoped**; mirror existing Learning patterns; deferred commit + `UnitOfWorkBehavior` (ADR 0001); `BaseResponse<T>`/`Successed`. Matching is the **only** Phase-1/2 backend gap — everything else (curriculum, unlock engine, quiz MCQ/TrueFalse/FillInBlank, instant feedback, granular answers, dashboard, skill graph, account settings, all Phase-1 Identity/Parent/Notifications incl. P1-12 + P1-13 lockout/CAPTCHA) is implemented on `main`.
- **Explicitly NOT in this carry-over (deferred to later phases, not gaps):** `HintAvailable`/AI-hint affordance (`SubmitAnswerCommandHandler.cs:150`, → Phase 4), Plan & billing stub (`AccountController` `GET /Plan`, → payments), P6-06 hardening follow-ups.
- The FE Matching renderer (CO-FE-5) and CO-BE-1/CO-BE-2 must agree on the payload shape — do CO-BE-1 first and share it.
