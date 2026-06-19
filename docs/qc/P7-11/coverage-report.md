# P7-11 AI-Safety & Quality Monitoring Dashboard — Coverage Report

> **BACKEND EXECUTION DEFERRED.** P7-11 is on the unmerged branch `feat/P7-11-ai-safety-dashboard` (PR #184) — **not on `main`**. Its build wave already produced `api-tester` coverage. These cases are designed for catalog completeness only. `api-tester` runs them **after #184 merges to `main`**, reconciling against the build-wave suite to avoid duplication. No execution should be attempted against `main` today (the controller/handlers don't exist there).

**Story:** `user-stories/Phase-7-Admin-Console/P7-11-ai-safety-monitoring-dashboard.md`
**Task/contract:** `tasks/Backend/Phase-7-Admin-Console/P7-11-BE.md`
**Controller (on branch):** `AdminAiSafetyController` @ `api/Admin/AiSafety` (AdminOnly)

## Counts

| Bucket | Total | Designed | Status |
|---|---|---|---|
| Backend | 30 | 30 | **Execution deferred until PR #184 merges** |
| Frontend (reference) | 12 | 12 | n/a |

Of the 30 backend cases: ~13 are **CONFIRM-ON-MERGE** (likely already in the build-wave suite — verify, don't duplicate), ~17 are **GAP / GAP-CONFIRM** (probable additions, especially rate arithmetic, breakdowns, degrade-state assertions, and PII).

## Acceptance-criteria → coverage matrix (provisional)

| AC (story) | Backend case IDs | Verdict (pre-merge) |
|---|---|---|
| AC-1 Safety-signal aggregates (blocked/flagged by reason/subject/language) over range | 11-08..15 | Designed; **rate arithmetic (11-11), reason breakdown (11-12), subject/language filters (11-13) are likely gaps** |
| AC-2 Eval pass/fail rate + trend + threshold-breach (P6-02) | 11-16..18 | Degrade-state (11-17) is the key assertion until P6-02 exists |
| AC-3 Tutor usage & cost (volume/tokens/latency) by subject/grade (P3-01) | 11-19..21 | Degrade-state (11-20) key until AI Gateway exists |
| AC-4 Drill-in recent blocked/flagged, minimal PII | 11-22..26 | **PII minimality (11-24) is P0 and must be explicitly asserted** |
| AC-5 Aggregates via read-model, cross-module via contracts, cached, no latency hit | (architectural; perf out of integration scope) | Verify no cross-schema joins in code review |
| AC-6 Admin-only; non-admin → 403 | 11-01..07 | Designed across all four routes |

## Risk notes

1. **The honest-degrade contract is the highest-value thing to assert and the easiest to get wrong.** Two of three signal sources (P3-01 usage/cost, P6-02 evals) don't exist yet. The dashboard must return 200 with zeroed/empty/N/A — never 500, never zero-presented-as-real. Cases 11-17 and 11-20 lock this. Verify the build wave actually asserts the **degrade** path and not just the happy path.
2. **Rate arithmetic + zero-total guard (11-11).** blockedRate/flaggedRate as ratios invite div-by-zero / NaN on an empty platform. Confirm the build wave tests the *value*, not just presence.
3. **PII minimality on the flagged drill-in (11-24)** is child-safety-critical and an explicit AC. Must be asserted, not assumed.
4. **subject/language/grade filters (11-13, 11-21)** depend on `SafetyEvent` carrying those dimensions — historically it did **not** (only `StudentId`, `TaskKind`; see P7-09 OQ-3). If the filters are no-ops or the columns are absent, that is a real AC-1 gap to surface, not a passing test.

## Prioritized backend list for api-tester (post-merge)

**Step 0 (on merge):** diff these cases against #184's existing test file; mark CONFIRM-ON-MERGE cases as Covered where the build wave already has them.

**P0 (verify or add):**
- 11-17 evals degrade gracefully (empty/N/A, not 500)
- 11-20 usage/cost degrade gracefully (request-volume N/A)
- 11-24 flagged drill-in carries no unnecessary child PII
- 11-01..07 full auth matrix across all four routes

**P1:**
- 11-11 blocked/flagged rate arithmetic + zero-total guard
- 11-12 breakdownByReason grouping
- 11-13 subject/language signal filters (surface as AC gap if no-op)
- 11-25 flagged pageSize clamp; 11-27 from≥to → 400 on all routes

**P2:**
- 11-21 usage subject/grade filters; 11-28 max-window guard; 11-30 malformed date → 400; 11-18 threshold-breach (when P6-02 lands)

## Open questions / assumptions for the lead

- **Reconciliation, not duplication:** the P7-11 build wave already ran `api-tester`. These cases are the *target* coverage; the actual delta to implement is only what the build-wave suite is missing. api-tester must read #184's test file first.
- **Subject/grade/language on `SafetyEvent`:** confirm whether #184 enriched the safety signal with these dimensions or whether the filters are accepted-but-ignored. Determines whether 11-13/11-21 are real tests or AC-gap notes.
- **Exact route prefix / DTO field names** (`api/Admin/AiSafety`, SafetySignalSummaryDto fields) are taken from the task contract; verify against #184's actual controller on merge.
