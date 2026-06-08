# P2-09 Home Dashboard — QC Test Plan & Coverage Report (Backend only)

> Run scope: **backend API surface only** (`GET /api/Learning/Dashboard`).
> Designer: `qc-test-designer` (Opus). Design-only — no test code, no execution.
> Implementer: **`api-tester`** → `backend-test-cases.md` → results into `execution-report.md`.
> No `frontend-test-cases.md` this run (the FE dashboard surface, P2-09-FE, is out of scope here).

## 1. Summary

- **Story:** P2-09 "See the home dashboard" (`user-stories/Phase-2-Learning-Core/P2-09-home-dashboard.md`).
- **In scope:** the single read endpoint `GET /api/Learning/Dashboard` `[Authorize]`, returning `BaseResponse<DashboardDto>`.
- **Out of scope:** FE rendering/RTL (P2-09-FE); the Gamification-module internals behind the Shared.Contracts seams (we assert the dashboard *contract shape*, not XP/streak/league/mission engine internals — those have their own P4-0x suites).
- **Key finding — CONTRACT DRIFT:** the live `DashboardDto` has **13 fields**, far past the 5-field Phase-2 brief. XP, streak, league, missions are now **wired live** (P4-02/03/06/07) and `level`, `hearts`, `inPracticeMode`, `badgesCount`, `recentBadges`, `weeklyMission`, `freezeBalance`, `activeTimedEvents` were added (P4-04/05/11). The story task file is marked **Done**. Test cases here target the **real current contract**, not the stale brief stubs.
- **Existing tests:** `backend/tests/Learnexia.IntegrationTests/P2_09_HomeDashboard_Tests.cs` already has 11 cases (C01–C11) but (a) only check a subset of the 13 fields, (b) frame `xp`/`streak`/`league`/`missions` as "always 0/null Phase-2 stubs" (now only true for brand-new students), and (c) never assert IDOR actively, the learning-language guard, the cross-subject fallback, or the engine-consistency invariant. This catalog **extends** the file.

### Counts

| Metric | Count |
|---|---|
| Total cases | **20** (BE-TC-01 … BE-TC-20) |
| Backend | 20 |
| Frontend | 0 (out of scope) |
| P0 | 8 (TC-01, 03, 04, 05, 08, 09, 12, 18) |
| P1 | 9 (TC-02, 06, 07, 10, 11, 14, 15, 16, 19) |
| P2 | 3 (TC-13, 17, 20) |
| Already covered by existing file | 7 (TC-01/03/05*/08/09/18/19/20 → C01/C03/C02+C04+C05/C03/C07/C09/C10/C11) |
| NEW cases to add | 11 (TC-02, 04, 06, 07, 11, 12, 13, 14, 15, 16, 17) |
| Expected BLOCKED / ManualVerify | up to 4 (TC-07, 11, 13, 17) — fixture-dependent |

## 2. Coverage matrix (acceptance criterion → case IDs)

| AC (from story) | Backend interpretation | Covered by | Gap? |
|---|---|---|---|
| **AC1 — Dashboard shows XP, streak, daily mission, continue, league preview** | Full `DashboardDto` shape (now 13 fields) present + correct types/zero-state | BE-TC-03, **BE-TC-04** (all 13 keys), BE-TC-05, BE-TC-06, BE-TC-07 | None — TC-04 is the load-bearing shape guard |
| **AC2 — Tapping "continue" opens the next unlocked lesson** | `continue` = first Available lesson, self-scoped, language-correct, engine-consistent | BE-TC-08, 09, 10, 11, 13, 14, 15, 16, 17 | None — TC-14 proves cross-endpoint consistency |
| **AC3 — Phase-4 widgets render gracefully (placeholders)** | Now *wired*; the graceful path is the brand-new `null`/sentinel state | BE-TC-05 (league null, missions null, badges 0), BE-TC-06 (hearts sentinel) | None (re-interpreted: "graceful" = null/sentinel for brand-new, populated for active) |
| **AC4 — Renders in Arabic (RTL) and English** | FE concern; backend slice = the learning-language guard picks the correct-language continue subject | BE-TC-15, 16, 17 | Backend slice covered; RTL rendering is P2-09-FE (not this run) |
| **(Implicit) auth / self-scoping / IDOR** | 401 anonymous; JWT-only studentId; no IDOR param | BE-TC-01, 02, 12, 18 | None — TC-12 actively proves IDOR structural impossibility |
| **(Implicit) empty/error state** | 200 on empty, never 404/500; read-only/idempotent | BE-TC-05, 13, 19 | TC-13 likely BLOCKED on fixture (documented, not dropped) |
| **(Product) 4 subjects, no teacher, no self-register** | seeder smoke + fallback set = exactly Math/Science/Arabic/English; Student-JWT-only reach | BE-TC-20 + header note | None |

**Coverage verdict:** every acceptance criterion has at least one P0/P1 case. **No uncovered criterion.** AC4's RTL-render portion is explicitly deferred to P2-09-FE (a different stack, not this backend run) — the backend-testable slice (language-correct continue subject) is covered by TC-15/16/17.

## 3. Risk notes (where cases are weighted, and why)

1. **Contract drift is the #1 risk.** The DTO grew from 5 → 13 fields across P4-02/03/04/05/06/07/11, yet the test file still frames the original Phase-2 stub semantics. A field could be dropped/renamed on an api-client regen and silently pass the current subset checks. **BE-TC-04 (all-13-keys present)** is the highest-value new case — it is the regression tripwire for the FE api-client contract.
2. **Self-scoping / IDOR.** Heaviest auth weighting (TC-01, 02, 12, 18). The endpoint is the daily re-entry surface for a child account; cross-child data leakage would be a privacy incident. The existing file asserts IDOR "by inspection" only — **TC-12** actively injects a `studentId` query param and proves it is ignored.
3. **Learning-language guard on continue.** A bilingual product where MATH/SCIENCE follow the learner's medium and ARABIC/ENGLISH are pinned. A regression here sends an Arabic-medium child into an English Math lesson. TC-15/16/17 weight this fork.
4. **Cross-subject fallback determinism** (TC-11) — the `FallbackSubjectCodeOrder` (MATH→SCIENCE→ARABIC→ENGLISH) is the tie-breaker that decides where an "exhausted" student goes next; fixture-costly but behaviorally important.
5. **Cross-module seam coupling.** XP/streak/hearts/badges/missions/league/timed-events all arrive via Shared.Contracts queries. Their *internal* correctness is owned by P4-0x suites; here we only assert the dashboard surfaces the never-null sentinels correctly for a brand-new student (TC-05/06) — so we don't double-test gamification engines, but we do catch a broken seam wiring.

## 4. Open questions / assumptions (lead to resolve before/while implementing)

1. **OQ-1 (framing):** The existing test file + the story task file call XP/streak/league/missions "Phase-2 stubs (always 0/null)". That is now FALSE for active students (P4-0x wired them). **Assumption:** we re-interpret AC1/AC3 against the *current* contract and assert zero/null only for **brand-new** students. Confirm this is the intended contract of record (it matches the code on `main`).
2. **OQ-2 (fixtures):** TC-07 (populated league), TC-11 (exhausted-Math fallback), TC-13 (degenerate `continue==null`), TC-17 (pinned-subject) need non-trivial seeds. **Assumption:** `api-tester` marks any of these BLOCKED/ManualVerify if no clean fixture exists, recording the blocker — rather than dropping the case. Confirm acceptable, or authorize a Gamification test seed helper for TC-07.
3. **OQ-3 (scope):** This run is backend-only per the lead's instruction; AC4's RTL rendering is therefore uncovered here by design. Confirm a separate `frontend-e2e-tester` pass (P2-09-FE) will own RTL/i18n, or whether to add a `frontend-test-cases.md` in a follow-up run.
4. **OQ-4 (`activeTimedEvents`/`recentBadges`/`weeklyMission` populated paths):** not designed as dedicated cases (gamification-internal, P4-11/P4-05/P4-06 own them). **Assumption:** asserting their *presence + brand-new null shape* (TC-04/05) is sufficient at the dashboard contract level. Confirm.

## 5. Handoff

| File | Owner | Action |
|---|---|---|
| `docs/qc/P2-09/README.md` | qc-test-designer | This plan + coverage report. |
| `docs/qc/P2-09/backend-test-cases.md` | qc-test-designer → **`api-tester`** | Implement/extend `P2_09_HomeDashboard_Tests.cs` (add the 11 NEW cases; keep C01–C11; relabel stale "always 0/null" comments). |
| `docs/qc/P2-09/execution-report.md` | **`api-tester`** | Fill pass/fail per BE-TC-NN + defects after running. Template scaffolded (empty) by qc-test-designer. |

**How `execution-report.md` gets filled:** `api-tester` runs the extended integration suite against the running API, then records per-case result (PASS/FAIL/BLOCKED), the regression-suite status, and any defect (with the failing assertion + observed envelope) into the templated file. `qc-test-designer` never fills results.
