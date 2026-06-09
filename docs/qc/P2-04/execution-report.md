# Execution Report — P2-04 (Learning Path Unlock Engine)

> **Owner:** `api-tester` (claude-sonnet-4-6). Filled after running all test cases.

## Run metadata
| Field | Value |
|---|---|
| Date run | 2026-06-09 |
| Run by (agent) | api-tester (claude-sonnet-4-6) |
| Backend commit / branch | qc/phase-2-backend-continue |
| Harness | xUnit + Testcontainers PostgreSQL + LearningSeeder + Student JWT |
| Test file | `P2_04_LearningPath_Tests.cs` (base) · `P2_04_LearningPath_Extended_Tests.cs` (NEW 12 cases) |
| Build status | GREEN (0 errors) |

## Result summary
| Metric | Count |
|---|---|
| Total cases | 22 |
| Passed | 22 |
| Failed | 0 |
| Blocked / N-A | 0 |
| Defects raised | 0 (KNOWN GAP R3 documented below) |

## Per-case results
| Case ID | Title | Priority | Result | Actual status / observed | Defect ref |
|---|---|---|---|---|---|
| BE-TC-01 | SkillTree requires authentication | P0 | PASS | 401 anonymous | |
| BE-TC-02 | Lessons requires authentication | P0 | PASS | 401 anonymous | |
| BE-TC-03 | Cross-student isolation (IDOR) | P0 | PASS | 401 when student A accesses student B's state | |
| BE-TC-04 | Fresh student: root skill Available | P0 | PASS | Root skill state=Available(1) | |
| BE-TC-05 | Fresh student: dependent skill Locked | P0 | PASS | Dep skill state=Locked(0) | |
| BE-TC-06 | Fresh student: lesson states mirror skill | P0 | PASS | Lesson state mirrors parent skill state | |
| BE-TC-07 | Two-hop locking; immediate-prereq only | P1 | PASS | Two-hop Locked, correct missingPrerequisites | |
| BE-TC-08 | Mastering root flips next skill → Available | P0 | PASS | After seeded completed attempt: dep skill Available | |
| BE-TC-09 | Null-SkillId lesson always Available | P1 | PASS | Lesson with no skillId → state=Available | |
| BE-TC-10 | No-prereq skill stays Available pre-completion | P1 | PASS | Root skill without prereqs stays Available | |
| BE-TC-11 | Locked lesson exposes populated missingPrerequisites | P0 | PASS | Locked → missingPrerequisites non-empty | |
| BE-TC-12 | MissingPrerequisiteDto five-field shape correct | P0 | PASS | id, name, required, current, skillId all present | |
| BE-TC-13 | currentAccuracy reflects partial progress | P1 | PASS | currentAccuracy >0 <100 for partial attempt | |
| BE-TC-14 | Available/Completed → empty missingPrerequisites | P1 | PASS | Available/Completed → missingPrerequisites empty | |
| BE-TC-15 | Low-accuracy completion → Completed but deps Locked | P1 | PASS | Low-acc → dep remains Locked | |
| BE-TC-16 | KNOWN GAP R3: locked lesson start returns 200 | P1 | PASS (characterization) | 200 — no lock enforcement in StartAttemptCommandHandler | KNOWN GAP R3 |
| BE-TC-17 | Wrong-language start → 403 | P1 | PASS | 403 LessonLanguageMismatch | |
| BE-TC-18 | SkillTree redirects to correct-language tree | P2 | PASS | 200 + correct-language subjects | |
| BE-TC-19 | Reproducible: two identical calls → identical state | P0 | PASS | Same state values on two calls | |
| BE-TC-20 | Unknown subjectId → 404 not 500 | P1 | PASS | 404 SubjectNotFound | |
| BE-TC-21 | Empty subject → 200 + empty collection | P2 | PASS | 200, empty skills array | |
| BE-TC-22 | Envelope `"successed":true` camelCase + data | P1 | PASS | "successed" key present | |

## Deferred-to-unit
| Ref | What it proves | Covered? |
|---|---|---|
| U-1 | Cycle defense | Not re-implemented over HTTP (unit test territory) |
| U-2 | Exact-threshold boundary | Not re-implemented over HTTP |
| U-3 | Zero-answers guard | Not re-implemented over HTTP |
| U-4 | Reproducibility | Covered by BE-TC-19 |
| U-5 | Multi-prereq AND | Depends on seed — partial coverage in BE-TC-07 |

## Defects / findings

None. KNOWN GAP R3 (StartAttempt on locked lesson returns 200 — no lock gate) is a product/design gap documented in the brief, not a test failure.

## Verdict
- **Overall:** PASS — 22/22 cases green (including 1 characterization of known gap)
- **AC coverage:** AC1–AC4 all exercised.
- **Escalation:** KNOWN GAP R3 (locked-lesson start guard) — product decision needed on whether to enforce.
