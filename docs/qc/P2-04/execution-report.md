# Execution Report — P2-04 (Learning Path Unlock Engine)

> **Owner:** `api-tester` (fills this after implementing + running `backend-test-cases.md`).
> qc-test-designer scaffolds this empty template only — it never fills results.
> Test catalog: [`backend-test-cases.md`](./backend-test-cases.md) · Plan: [`README.md`](./README.md)

## Run metadata
| Field | Value |
|---|---|
| Date run | _TBD_ |
| Run by (agent) | _TBD_ |
| Backend commit / branch | _TBD_ |
| Harness | xUnit + Testcontainers PostgreSQL (pg16) + seeded LearningSeeder graph + Student-role JWT |
| Test file | `backend/tests/Learnexia.IntegrationTests/...` (_TBD_) |
| Build status | _TBD_ |

## Result summary
| Metric | Count |
|---|---|
| Total cases | 22 |
| Passed | _TBD_ |
| Failed | _TBD_ |
| Blocked / N-A | _TBD_ |
| Defects raised | _TBD_ |

## Per-case results
| Case ID | Title | Priority | Result (Pass/Fail/Blocked) | Actual status / observed | Defect ref |
|---|---|---|---|---|---|
| BE-TC-01 | SkillTree requires authentication | P0 | | | |
| BE-TC-02 | Lessons requires authentication | P0 | | | |
| BE-TC-03 | Cross-student isolation (IDOR) | P0 | | | |
| BE-TC-04 | Fresh student: root skill Available | P0 | | | |
| BE-TC-05 | Fresh student: dependent skill Locked | P0 | | | |
| BE-TC-06 | Fresh student: lesson states mirror skill | P0 | | | |
| BE-TC-07 | Two-hop locking; immediate-prereq only | P1 | | | |
| BE-TC-08 | Mastering root flips next skill → Available | P0 | | | |
| BE-TC-09 | Null-SkillId lesson always Available | P1 | | | |
| BE-TC-10 | No-prereq skill stays Available pre-completion | P1 | | | |
| BE-TC-11 | Locked lesson exposes populated missingPrerequisites | P0 | | | |
| BE-TC-12 | MissingPrerequisiteDto five-field shape correct | P0 | | | |
| BE-TC-13 | currentAccuracy reflects partial (below-threshold) progress | P1 | | | |
| BE-TC-14 | Available/Completed → empty missingPrerequisites | P1 | | | |
| BE-TC-15 | Low-accuracy completion → Completed but dependents stay Locked | P1 | | | |
| BE-TC-16 | **GAP:** starting a locked lesson is NOT rejected (document actual) | P1 | | | |
| BE-TC-17 | Wrong-language start → 403 LessonLanguageMismatch | P1 | | | |
| BE-TC-18 | SkillTree redirects to correct-language tree | P2 | | | |
| BE-TC-19 | Reproducible: two identical calls → identical state | P0 | | | |
| BE-TC-20 | Unknown subjectId → 404 not 500 | P1 | | | |
| BE-TC-21 | Empty subject → 200 + empty collection | P2 | | | |
| BE-TC-22 | Envelope `"successed":true` camelCase + data | P1 | | | |

## Deferred-to-unit (verify covered by `LearningPathEngineTests`, do not re-implement over HTTP)
| Ref | What it proves | Covered by unit test? (Y/N + name) |
|---|---|---|
| U-1 | Cycle defense — no infinite loop | |
| U-2 | Exact-threshold boundary (80.0 vs 79.99) | |
| U-3 | Zero-answers guard (threshold 0) | |
| U-4 | Pure reproducibility (same inputs → identical output) | |
| U-5 | Multi-prereq AND, one unmet → Locked + 1 missing | |

## Defects / findings
> One row per defect. Reference the case ID. Include status code, envelope, and repro.

| # | Case | Severity | Description | Expected | Actual |
|---|---|---|---|---|---|
| | | | | | |

## Notes / blockers encountered
- _Record any case marked Blocked and why (e.g. no empty-subject fixture for BE-TC-21; no cross-language fixture for BE-TC-17/18)._
- _BE-TC-16: record the exact HTTP status returned when starting a locked lesson. If 200, this confirms the no-guard gap (OQ-1 in README) and should be escalated to the lead as a product decision, not filed as a test failure._

## Verdict
- **Overall:** _PASS / FAIL / PASS-WITH-GAPS — TBD_
- **AC coverage verdict:** _TBD (AC1–AC4 all exercised? gaps?)_
- **Escalations for lead:** _TBD (e.g. OQ-1 locked-start guard decision)._
