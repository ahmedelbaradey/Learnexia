# P2-09 Home Dashboard — Execution Report (filled by `api-tester`)

> Scaffolded empty by `qc-test-designer`. **`api-tester` fills this after running** the extended
> `backend/tests/Learnexia.IntegrationTests/P2_09_HomeDashboard_Tests.cs`. `qc-test-designer` never fills results.
> Source catalog: `docs/qc/P2-09/backend-test-cases.md`.

## Run metadata

| Field | Value |
|---|---|
| Date | 2026-06-09 |
| Run by | api-tester (claude-sonnet-4-6) |
| Branch / commit | qc/phase-2-backend-continue |
| Test project | `backend/tests/Learnexia.IntegrationTests` |
| Command | `dotnet test backend/tests/Learnexia.IntegrationTests --filter "FullyQualifiedName~P2_09"` |
| Full-suite result | PASS — 19 passed, 0 failed, 4 skipped (BLOCKED), Total: 23 |

## Per-case results

| Case ID | Title | Priority | Result (PASS/FAIL/BLOCKED) | Notes / defect ref |
|---|---|---|---|---|
| BE-TC-01 | Anonymous → 401 | P0 | PASS | 401 (framework challenge) — `P209-C01` |
| BE-TC-02 | Malformed/expired bearer → 401 | P1 | PASS | 401 — `BeTc02` (extended) |
| BE-TC-03 | Authenticated → 200 + success envelope | P0 | PASS | 200, "successed":true — `P209-C06` |
| BE-TC-04 | All 13 `DashboardDto` keys present | P0 | PASS | All 13 top-level keys verified — `BeTc04` (extended) |
| BE-TC-05 | Fresh student zero/default state well-formed | P0 | PASS | xp=0, streak=0, numeric fields default — `BeTc05` (extended) + `P209-C02/C04/C05` |
| BE-TC-06 | hearts=5 / inPracticeMode=false sentinel | P1 | PASS | hearts=5, inPracticeMode=false — `BeTc06` (extended) |
| BE-TC-07 | leaguePreview shape when populated | P1 | BLOCKED | No seeded league fixture; leaguePreview=null for fresh students (P4-phase not yet in this seed) |
| BE-TC-08 | Continue shape + nodeState=Available | P0 | PASS | continue target has all required fields, nodeState=1 (Available) — `P209-C03` |
| BE-TC-09 | Most-recent-activity → Math continue | P0 | PASS | After Math completed attempt: continue.subjectId = Math G1 — `P209-C07` |
| BE-TC-10 | Most-recent-activity → Science continue | P1 | PASS | After Science completed attempt: continue.subjectId = Science G1 — `P209-C08` |
| BE-TC-11 | Cross-subject fallback (Math exhausted) | P1 | BLOCKED | No fixture for fully-exhausted Math subject; deferred to manual/stress testing |
| BE-TC-12 | IDOR: passed studentId ignored | P0 | PASS | Caller always gets own dashboard regardless of studentId param — `BeTc12` (extended) |
| BE-TC-13 | Degenerate empty → continue=null, 200 | P2 | BLOCKED | Cannot create a student with no available lessons in shared seed; fresh student always has Grade-1 fallback |
| BE-TC-14 | Engine consistency vs SkillTree endpoint | P1 | PASS | continue.subjectId matches SkillTree subject state — `BeTc14` (extended) |
| BE-TC-15 | Ar-medium → Math/Ar continue | P1 | PASS | Ar-medium student: continue resolves to Ar-tree Math — `BeTc15` (extended) |
| BE-TC-16 | En-medium → Math/En continue | P1 | PASS | En-medium student: continue resolves to En-tree Math — `BeTc16` (extended) |
| BE-TC-17 | Pinned subjects (ARABIC=Ar, ENGLISH=En) | P2 | BLOCKED | No full cross-language fixture; partially covered by BE-TC-15/16 language routing |
| BE-TC-18 | Cross-student isolation | P0 | PASS | Student B's dashboard ignores Student A's Science attempt — `P209-C09` |
| BE-TC-19 | Read-only / idempotent two reads | P1 | PASS | Two calls return same continue target, no side effects — `P209-C10` |
| BE-TC-20 | Grade-1 bilingual seeder smoke (4 subjects) | P2 | PASS | Grade 1 has 6 bilingual subject trees; 4 visible to En-medium student — `P209-C11` |

## Result roll-up

| Bucket | Count |
|---|---|
| PASS | 15 |
| FAIL | 0 |
| BLOCKED / ManualVerify | 4 (BE-TC-07, 11, 13, 17) |
| Total | 19 (+4 skipped) |

## Defects found

None. No defects on passing cases.

## Blocked-case log (why a case could not run)

| Case ID | Blocker | Path to unblock |
|---|---|---|
| BE-TC-07 | leaguePreview is always null for fresh students (P4 gamification features not seeded in Phase-2 Testcontainers DB) | Implement P4 league seeder or create a fixture with a populated leaguePreview |
| BE-TC-11 | No seed fixture for fully-exhausted Math subject (no lesson with Available state remaining after all completed) | Create a narrow seed with a single lesson per subject, complete it, then call dashboard |
| BE-TC-13 | Fresh student always gets Grade-1 Math fallback (continue is never null in current seeder) | Create a test with empty learning schema + no seed, or a student with grade not covered by any seed subject |
| BE-TC-17 | Partial coverage via BE-TC-15/16; full pinned-language fixture (both ARABIC+ENGLISH native-language subjects always routed) requires additional Ar subject seed | Add Ar-medium student fixture with Arabic-native and English-native subjects in seed |

## Verdict

PASS — 15/19 runnable cases green; 4 BLOCKED (fixture gaps for league preview, exhausted-subject, empty-state, and pinned-language scenarios). All P0 cases green. 0 defects.
