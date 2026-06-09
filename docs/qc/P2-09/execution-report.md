# P2-09 Home Dashboard — Execution Report (filled by `api-tester`)

> Scaffolded empty by `qc-test-designer`. **`api-tester` fills this after running** the extended
> `backend/tests/Learnexia.IntegrationTests/P2_09_HomeDashboard_Tests.cs`. `qc-test-designer` never fills results.
> Source catalog: `docs/qc/P2-09/backend-test-cases.md`.

## Run metadata

| Field | Value |
|---|---|
| Date | _(fill)_ |
| Run by | api-tester |
| Branch / commit | _(fill)_ |
| Test project | `backend/tests/Learnexia.IntegrationTests` |
| Command | _(fill — e.g. `dotnet test ...`)_ |
| Full-suite regression result | _(PASS / FAIL — list any broken pre-existing test)_ |

## Per-case results

| Case ID | Title | Priority | Result (PASS/FAIL/BLOCKED) | Notes / defect ref |
|---|---|---|---|---|
| BE-TC-01 | Anonymous → 401 | P0 | _(fill)_ | (exists: C01) |
| BE-TC-02 | Malformed/expired bearer → 401 | P1 | _(fill)_ | (new) |
| BE-TC-03 | Authenticated → 200 + success envelope | P0 | _(fill)_ | (exists: C06) |
| BE-TC-04 | All 13 `DashboardDto` keys present | P0 | _(fill)_ | (new — contract tripwire) |
| BE-TC-05 | Fresh student zero/default state well-formed | P0 | _(fill)_ | (extend C02/C04/C05) |
| BE-TC-06 | hearts=5 / inPracticeMode=false sentinel | P1 | _(fill)_ | (new) |
| BE-TC-07 | leaguePreview shape when populated | P1 | _(fill)_ | (new — may BLOCK on fixture) |
| BE-TC-08 | Continue shape + nodeState=Available | P0 | _(fill)_ | (exists: C03) |
| BE-TC-09 | Most-recent-activity → Math continue | P0 | _(fill)_ | (exists: C07) |
| BE-TC-10 | Most-recent-activity → Science continue | P1 | _(fill)_ | (exists: C08) |
| BE-TC-11 | Cross-subject fallback (Math exhausted) | P1 | _(fill)_ | (new — may BLOCK/ManualVerify) |
| BE-TC-12 | IDOR: passed studentId ignored | P0 | _(fill)_ | (new — active IDOR proof) |
| BE-TC-13 | Degenerate empty → continue=null, 200 | P2 | _(fill)_ | (new — likely BLOCKED on fixture) |
| BE-TC-14 | Engine consistency vs SkillTree endpoint | P1 | _(fill)_ | (new — cross-endpoint invariant) |
| BE-TC-15 | Ar-medium → Math/Ar continue | P1 | _(fill)_ | (new — language guard) |
| BE-TC-16 | En-medium → Math/En continue | P1 | _(fill)_ | (new — language guard) |
| BE-TC-17 | Pinned subjects (ARABIC=Ar, ENGLISH=En) | P2 | _(fill)_ | (new — may ManualVerify) |
| BE-TC-18 | Cross-student isolation | P0 | _(fill)_ | (exists: C09) |
| BE-TC-19 | Read-only / idempotent two reads | P1 | _(fill)_ | (exists: C10) |
| BE-TC-20 | Grade-1 bilingual seeder smoke (4 subjects) | P2 | _(fill)_ | (exists: C11) |

## Result roll-up

| Bucket | Count |
|---|---|
| PASS | _(fill)_ |
| FAIL | _(fill)_ |
| BLOCKED / ManualVerify | _(fill)_ |
| Total | 20 |

## Defects found

> One row per defect. Include the failing case ID, the assertion, the observed envelope/value, and severity.

| # | Case ID | Severity | Summary | Observed | Expected |
|---|---|---|---|---|---|
| _(none yet)_ | | | | | |

## Blocked-case log (why a case could not run)

| Case ID | Blocker | Path to unblock |
|---|---|---|
| _(fill — e.g. BE-TC-07/11/13/17 fixture gaps)_ | | |

## Verdict

_(api-tester: overall PASS/FAIL for the P2-09 backend slice, plus any contract-drift or defect that should block the reviewer gate.)_
