# Execution Report — P2-05-FE (Open & complete a lesson, web PWA)

> **Owner: `frontend-e2e-tester`.** The QC author (`qc-test-designer`) scaffolds this template only and never fills results. Fill this in after implementing `frontend-test-cases.md` as `tests/e2e/specs/P2-05-FE.spec.ts` and running it against the live stack (Expo web `:8081` + backend `:5080`).
> Do NOT edit the test-case catalog to make a case pass. If a case is undrivable, mark it BLOCKED with the concrete reason. If it reveals a bug, mark FAIL and file a defect below.

## Run metadata (fill on run)

| Field | Value |
|---|---|
| Date / time | _TBD_ |
| Branch | _TBD_ |
| Commit | _TBD_ |
| Expo web | http://localhost:8081 (Node 20, `EXPO_OFFLINE=1`) |
| Backend | http://localhost:5080 (Development, seeded) |
| Playwright projects | chromium / mobile (Pixel 7) — _TBD which ran_ |
| Spec file | `tests/e2e/specs/P2-05-FE.spec.ts` |
| Seed lesson used `(lessonId, subjectId)` | _TBD (per OQ-2)_ |

## Result summary (fill on run)

| Metric | Count |
|---|---|
| Total cases | 28 |
| Passed | _TBD_ |
| Failed | _TBD_ |
| Blocked | _TBD_ |
| Not run | _TBD_ |

## Per-case results (fill on run)

| Case | Title | Priority | Result (PASS/FAIL/BLOCKED) | Notes / defect ref |
|---|---|---|---|---|
| FE-TC-01 | Open lesson → intro renders title + Start CTA | P0 | _TBD_ | |
| FE-TC-02 | Explanation renders + null fallback | P1 | _TBD_ | |
| FE-TC-03 | Visual block present/omitted | P2 | _TBD_ | |
| FE-TC-04 | Signed-out + parent cannot reach lesson route | P0 | _TBD_ | |
| FE-TC-05 | Start CTA → quiz stage | P0 | _TBD_ | |
| FE-TC-06 | Progress label + dots advance | P1 | _TBD_ | |
| FE-TC-07 | Complete last question → summary | P0 | _TBD_ | |
| FE-TC-08 | Completion records progress (node Completed) | P0 | _TBD_ | |
| FE-TC-09 | Completion invalidates dashboard Continue | P2 | _TBD_ | |
| FE-TC-10 | Return to tree → node not-locked / advanced | P2 | _TBD_ | |
| FE-TC-11 | Resume an in-progress lesson | P1 | _TBD (expected BLOCKED — feature absent)_ | |
| FE-TC-12 | One primary action per stage (Submit/Next) | P1 | _TBD_ | |
| FE-TC-13 | Loading state during lesson GET | P1 | _TBD_ | |
| FE-TC-14 | Error state on lesson GET failure | P0 | _TBD_ | |
| FE-TC-15 | Retry on error recovers | P1 | _TBD_ | |
| FE-TC-16 | 404 lesson not found | P1 | _TBD_ | |
| FE-TC-17 | Empty lesson (0 questions) → empty state | P1 | _TBD_ | |
| FE-TC-18 | Back mid-quiz fires abandon; node NOT completed | P0 | _TBD_ | |
| FE-TC-19 | Browser back mid-quiz also abandons | P1 | _TBD_ | |
| FE-TC-20 | Network error on Start → intro stays, recoverable | P1 | _TBD_ | |
| FE-TC-21 | Arabic default → RTL across stages | P0 | _TBD_ | |
| FE-TC-22 | English child → LTR, Western numerals | P1 | _TBD_ | |
| FE-TC-23 | No raw i18n keys leak (Arabic) | P0 | _TBD_ | |
| FE-TC-24 | No raw i18n keys leak (English) | P1 | _TBD_ | |
| FE-TC-25 | Hearts widget present across stages | P2 | _TBD_ | |
| FE-TC-26 | Kid-UX: large targets + instant feedback | P2 | _TBD_ | |
| FE-TC-27 | Summary "Try again" → fresh attempt | P1 | _TBD_ | |
| FE-TC-28 | Summary "Back to lessons" → subject route | P1 | _TBD_ | |

## Defects found (fill on run)

> One row per defect. Severity: Critical / High / Medium / Low. File back to `frontend` (UI) — this folder is design+report only.

| ID | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| _DEF-P205FE-01_ | _TBD_ | _TBD_ | _TBD_ | open |

## testIDs added / requested during the run (fill on run)

> Track which OQ-1 hooks `frontend` added (unblocking cases) and which remain outstanding.

| testID | Added? | Unblocks |
|---|---|---|
| `lesson-start-cta` | _TBD_ | FE-TC-01/05/17/20 |
| `quiz-stage` / `quiz-progress-label` | _TBD_ | FE-TC-05/06/21/22/27 |
| `quiz-submit-cta` / `quiz-next-cta` | _TBD_ | FE-TC-06/12 |
| `lesson-summary-card` / `summary-back-cta` / `summary-retry-cta` | _TBD_ | FE-TC-07/08/27/28 |
| `lesson-loading` / `lesson-error` / `lesson-404` / `lesson-empty` / `lesson-back` | _TBD_ | FE-TC-13/14/15/16/17/18 |
| _other_ | _TBD_ | |

## Lead decisions still pending (carry from README §4)

- OQ-1 — testIDs to be added by `frontend` (dominant).
- OQ-2 — deterministic seed `(lessonId, subjectId)`.
- OQ-3 — resume scope + intended UX (blocks FE-TC-11).
- OQ-6 — route-mock permitted for error/404/empty/network states.
