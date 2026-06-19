# P7-11 AI-Safety & Quality Monitoring Dashboard — Backend Test Cases (api-tester)

> **EXECUTION DEFERRED.** P7-11 lives on the unmerged branch `feat/P7-11-ai-safety-dashboard` (PR #184), not on `main`. Its build wave already had `api-tester` coverage. These cases are **designed for completeness** so the catalog is whole; `api-tester` should implement/verify them only once #184 merges to `main` (and reconcile against whatever the build-wave suite already contains to avoid duplication).
>
> Surface (per `tasks/Backend/Phase-7-Admin-Console/P7-11-BE.md` §Contract for Frontend): `AdminAiSafetyController` @ `api/Admin/AiSafety` (AdminOnly):
> - `GET signals?from=&to=&subject=&language=` → `BaseResponse<SafetySignalSummaryDto>`
> - `GET evals?from=&to=` → `BaseResponse<EvalResultsDto>`
> - `GET usage?from=&to=&subject=&grade=` → `BaseResponse<TutorUsageDto>`
> - `GET flagged?from=&to=&page=&pageSize=` → `BaseResponse<PagedResult<FlaggedOutputDto>>`
>
> Each case is marked **Covered (build wave)** [assumed — verify against #184's suite on merge], **GAP** (likely new), or **CONFIRM-ON-MERGE** (verify whether #184 already covers it). Since the branch's test file is not on `main`, all "Covered" claims are provisional and must be reconciled by api-tester post-merge.
>
> Honest-degrade contract: P3-01 (AI Gateway), P6-02 (eval set) are not yet built → usage/cost + eval facets degrade to zero/empty/N/A. Safety **signals** read `ai.SafetyEvents` (real data now). Queries are NOT auto-validated — range/paging validated in-handler.

## Auth / authz (every route)

| ID | Title | Type | Pri | Steps | Expected | Status |
|---|---|---|---|---|---|---|
| BE-TC-11-01 | signals anonymous → 401 | auth | P0 | GET signals no bearer | 401 | CONFIRM-ON-MERGE |
| BE-TC-11-02 | signals parent → 403 | auth | P0 | GET signals parent | 403 | CONFIRM-ON-MERGE |
| BE-TC-11-03 | signals basicuser → 403 | auth | P0 | GET signals basicuser | 403 | CONFIRM-ON-MERGE |
| BE-TC-11-04 | signals admin → 200 | auth | P0 | GET signals admin | 200 | CONFIRM-ON-MERGE |
| BE-TC-11-05 | evals admin → 200, non-admin → 401/403 | auth | P0 | GET evals each role | 200/401/403 | CONFIRM-ON-MERGE |
| BE-TC-11-06 | usage admin → 200, non-admin → 401/403 | auth | P0 | GET usage each role | 200/401/403 | CONFIRM-ON-MERGE |
| BE-TC-11-07 | flagged admin → 200, non-admin → 401/403 | auth | P0 | GET flagged each role | 200/401/403 | CONFIRM-ON-MERGE |

## Signals (real-data facet)

| ID | Title | Type | Pri | Steps | Expected | Status |
|---|---|---|---|---|---|---|
| BE-TC-11-08 | signals envelope + DTO shape | functional | P0 | GET signals admin | BaseResponse keys; data has totalOutputs, blockedCount, blockedRate, flaggedCount, flaggedRate, breakdownByReason[] | CONFIRM-ON-MERGE |
| BE-TC-11-09 | Seed Blocked SafetyEvent → blockedCount ≥1 | persistence | P0 | seed Blocked event; GET signals | blockedCount ≥1, totalOutputs ≥1 | CONFIRM-ON-MERGE |
| BE-TC-11-10 | Seed Regenerated → flaggedCount ≥1 | persistence | P1 | seed Regenerated; GET signals | flaggedCount ≥1 | CONFIRM-ON-MERGE |
| BE-TC-11-11 | **blockedRate/flaggedRate are correct ratios (rate = count/total)** | functional | P1 | seed N events, M blocked; GET signals | blockedRate ≈ M/N (and 0 when total=0, not NaN/div-by-zero) | **GAP** — rate arithmetic + zero-total guard are classic edge bugs; verify they aren't merely count fields |
| BE-TC-11-12 | **breakdownByReason groups by reason code** | functional | P1 | seed events w/ distinct ReasonCodes; GET signals | breakdownByReason has an entry per reason with correct counts | **GAP** — reason/category breakdown (AC) likely thin in build wave |
| BE-TC-11-13 | **subject + language filters narrow signals** | functional | P1 | GET signals?subject=Math&language=ar | only matching events counted | **GAP / CONFIRM** — `SafetyEvent` historically carried **no subject/grade/language** (see P7-09 OQ-3); if the filter is a no-op or the columns are absent, this surfaces a real AC gap |
| BE-TC-11-14 | Empty window → 200, zeroed signals (not 500, not null) | state | P0 | GET signals far-future window | totalOutputs=0, rates=0, breakdown empty | CONFIRM-ON-MERGE |
| BE-TC-11-15 | Event outside window excluded | functional | P1 | seed 60d-old event; GET 1d window | not counted | CONFIRM-ON-MERGE |

## Evals (degrade facet — P6-02 not built)

| ID | Title | Type | Pri | Steps | Expected | Status |
|---|---|---|---|---|---|---|
| BE-TC-11-16 | evals envelope + DTO shape | functional | P1 | GET evals admin | BaseResponse; data has runs[] of {runId, passRate, failRate, threshold, breached, ranAt} | CONFIRM-ON-MERGE |
| BE-TC-11-17 | **evals degrades gracefully (empty runs[]) when P6-02 absent** | state | P0 | GET evals admin | 200; runs=[] (or N/A marker); not 500 | **GAP / CONFIRM** — the honest-degrade contract for the eval facet must be asserted |
| BE-TC-11-18 | threshold-breach indicator semantics | functional | P2 | (when eval data exists) GET evals | breached=true when passRate < threshold | **GAP** — deferred until P6-02 producer exists; design-only |

## Usage / cost (degrade facet — P3-01 not built)

| ID | Title | Type | Pri | Steps | Expected | Status |
|---|---|---|---|---|---|---|
| BE-TC-11-19 | usage envelope + DTO shape | functional | P1 | GET usage admin | BaseResponse; data has requestVolume, tokenCostSeries[], avgLatency | CONFIRM-ON-MERGE |
| BE-TC-11-20 | **usage degrades gracefully (zero/empty + N/A) when AiUsageLogs absent** | state | P0 | GET usage admin | 200; requestVolume=0/N/A, series empty; not 500 | **GAP / CONFIRM** — request-volume N/A is the same `AiUsageLogs` gap P7-10 already markers; verify P7-11 mirrors it |
| BE-TC-11-21 | usage subject/grade filters accepted | functional | P2 | GET usage?subject=&grade= | 200, filters applied (or no-op when no data) | **GAP** |

## Flagged drill-in (paged, minimal PII)

| ID | Title | Type | Pri | Steps | Expected | Status |
|---|---|---|---|---|---|---|
| BE-TC-11-22 | flagged paged envelope | functional | P1 | GET flagged admin | BaseResponse<PagedResult<FlaggedOutputDto>>; paging keys present | CONFIRM-ON-MERGE |
| BE-TC-11-23 | Seed flagged/blocked events → appear in list | persistence | P1 | seed events; GET flagged | rows w/ contentRef, verdict, reason, occurredAt | CONFIRM-ON-MERGE |
| BE-TC-11-24 | **flagged list carries NO unnecessary child PII** | privacy | P0 | GET flagged | rows expose only contentRef/verdict/reason/occurredAt; no prompt/response text, no child name/email | **GAP / CONFIRM** — child-safety sensitive; AC demands minimal PII. Must be explicitly asserted |
| BE-TC-11-25 | flagged pageSize clamped ≤100 | boundary | P1 | GET flagged?pageSize=9999 | pageSize ≤100 | **GAP / CONFIRM** |
| BE-TC-11-26 | flagged empty window → 200 empty page | state | P1 | GET flagged far-future window | 200, empty, successed=true | CONFIRM-ON-MERGE |

## Range validation (queries — in-handler)

| ID | Title | Type | Pri | Steps | Expected | Status |
|---|---|---|---|---|---|---|
| BE-TC-11-27 | from ≥ to → 400 (each endpoint) | validation | P1 | GET signals/evals/usage/flagged from=to | 400, successed=false | **GAP / CONFIRM** — verify the in-handler range guard exists on all four routes, mirroring P7-10 |
| BE-TC-11-28 | window > max → 400 | boundary | P2 | GET with oversized window | 400 | **GAP / CONFIRM** — confirm P7-11 has a max-window guard like P7-10's 365d |
| BE-TC-11-29 | no params → 200 default window | functional | P2 | GET signals no params | 200, default window applied | CONFIRM-ON-MERGE |
| BE-TC-11-30 | malformed date param → 400 not 500 | negative | P2 | GET signals?from=notadate | 400 | **GAP** |
