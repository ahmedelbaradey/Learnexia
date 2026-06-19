# P7-10 Platform Analytics — Backend Test Cases (api-tester)

> Surface: `AdminAnalyticsController` @ `GET api/Admin/Analytics/kpis?from=&to=` (AdminOnly). Façade handler `GetPlatformKpisQueryHandler` fans out to 4 `Shared.Contracts` platform-aggregate seams. Query — **not** auto-validated; range checks done in-handler.
>
> Existing suite: `backend/tests/Learnexia.IntegrationTests/P7_10_PlatformAnalytics_Tests.cs` — read first. Each case is **Covered** (cite `[Fact]`) or **GAP**.
>
> Handler facts: null `from` → now−30d; null `to` → now; `from >= to` → **400**; window > 365d → **400**; revenue/aiRequestVolume/retention/sessionDuration/quizzesCompleted carry **string N/A reasons**; numeric KPIs zeroed (never null) on empty window; subscriptions are **not windowed** (live table).

## Auth / authz matrix

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-10-01 | kpis anonymous → 401 | auth | P0 | none | GET kpis no bearer | 401 | **Covered** — `Auth1_Anonymous_Returns401` |
| BE-TC-10-02 | kpis parent → 403 | auth | P0 | parent JWT | GET kpis parent | 403 | **Covered** — `Auth2_Parent_Returns403` |
| BE-TC-10-03 | kpis basicuser → 403 | auth | P0 | basicuser JWT | GET kpis basicuser | 403 | **Covered** — `Auth3_BasicUser_Returns403` |
| BE-TC-10-04 | kpis admin → 200 | auth | P0 | admin JWT | GET kpis admin | 200 | **Covered** — `Auth4_Admin_Returns200` |

## Envelope + DTO shape

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-10-05 | BaseResponse envelope keys | functional | P1 | admin | GET kpis | statusCode/successed/message/data/errors present, successed=true | **Covered** — `Envelope_HasBaseResponseKeys` |
| BE-TC-10-06 | All KPI top-level fields present | functional | P0 | admin | GET kpis | fromUtc/toUtc/lessonsCompleted/totalAttempts/distinctActiveStudents/quizzesCompletedNaReason/bySubject/byGrade/missionsCompleted/xpEarnedInWindow/totalActiveSubscriptions/subscriptionsByTier/revenueNaReason/totalAiSafetyEvents/aiBlockedCount/aiFlaggedCount/aiRequestVolumeNaReason/retentionNaReason/sessionDurationNaReason | **Covered** — `DtoShape_HasAllKpiFields` |

## Honest-v1 N/A markers

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-10-07 | retentionNaReason is non-empty string, refs P5-03 | functional | P0 | admin | GET kpis | retentionNaReason String, non-empty, contains "P5-03" | **Covered** — `NaMarkers_RetentionNaReason_IsString` |
| BE-TC-10-08 | sessionDurationNaReason string, refs P5-03 | functional | P0 | admin | GET kpis | string, non-empty, contains "P5-03" | **Covered** — `NaMarkers_SessionDurationNaReason_IsString` |
| BE-TC-10-09 | revenueNaReason non-empty string (Fake provider) | functional | P0 | admin | GET kpis | string, non-empty | **Covered** — `NaMarkers_RevenueNaReason_IsString` |
| BE-TC-10-10 | aiRequestVolumeNaReason non-empty string (AiUsageLogs/P7-11) | functional | P0 | admin | GET kpis | string, non-empty | **Covered** — `NaMarkers_AiRequestVolumeNaReason_IsString` |
| BE-TC-10-11 | quizzesCompletedNaReason non-empty string | functional | P1 | admin | GET kpis | string, non-empty | **Covered** — `NaMarkers_QuizzesCompletedNaReason_IsString` |

## Real aggregation

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-10-12 | distinctActiveStudents present, non-neg int (activity proxy) | functional | P1 | admin | GET kpis | number ≥ 0 | **Covered** — `ActiveProxy_DistinctActiveStudents_IsNonNegativeInteger` |
| BE-TC-10-13 | Seeded Blocked SafetyEvent → totalAiSafetyEvents/aiBlockedCount ≥1 | persistence | P0 | seed Blocked SafetyEvent | GET kpis | both ≥ 1 | **Covered** — `RealAggregation_SeededSafetyEvent_ReflectedInAiKpis` |
| BE-TC-10-14 | Seeded Regenerated event → aiFlaggedCount ≥1 | persistence | P1 | seed Regenerated SafetyEvent | GET kpis | aiFlaggedCount ≥ 1 | **Covered** — `RealAggregation_SeededFlaggedSafetyEvent_ReflectedInFlaggedCount` |
| BE-TC-10-15 | Event 60d old excluded from 1d window | functional | P1 | seed event 60d ago | GET kpis 1d window | not counted | **Covered** — `RealAggregation_EventOutsideWindow_NotCounted` |
| BE-TC-10-16 | **Subscriptions are live (not windowed) — far-future window still reflects active subs** | functional | P2 | ≥1 active subscription | GET kpis far-future window | totalActiveSubscriptions reflects live count (not zeroed by window) | **GAP** — empty-platform test asserts subs ≥ 0 but does not assert subscriptions are *unaffected* by the window (the not-windowed contract) |
| BE-TC-10-17 | **subscriptionsByTier shape is a tier→count map/array** | functional | P2 | admin | GET kpis | subscriptionsByTier present & structurally valid (object/array, counts non-neg) | **GAP** — presence is checked but structure/contents are not |

## Range validation

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-10-18 | from == to → 400 | validation | P0 | admin | GET kpis from=to | 400; successed=false | **Covered** — `RangeInvalid_FromEqualsTo_Returns400` |
| BE-TC-10-19 | from > to → 400 | validation | P0 | admin | GET kpis from>to | 400; successed=false | **Covered** — `RangeInvalid_FromAfterTo_Returns400` |
| BE-TC-10-20 | window > 365d → 400 | boundary | P0 | admin | GET kpis 366d window | 400; successed=false | **Covered** — `RangeWindowTooLarge_Returns400` |
| BE-TC-10-21 | **window == exactly 365d → 200 (boundary inclusive)** | boundary | P1 | admin | GET kpis 365d window | 200 (handler uses `> MaxWindow`, so 365 exactly must pass) | **GAP** — only 366 (fail) is tested; the inclusive boundary at exactly 365 is unverified |
| BE-TC-10-22 | no params → 200, ~30d default window | functional | P1 | admin | GET kpis no params | 200; (toUtc−fromUtc)≈30d | **Covered** — `DefaultWindow_NoParams_Returns200` |
| BE-TC-10-23 | explicit valid 7d window → 200, echoes from/to | functional | P1 | admin | GET kpis 7d window | 200; fromUtc/toUtc echo inputs | **Covered** — `ValidExplicitWindow_Returns200` |
| BE-TC-10-24 | **only `from` supplied (to omitted) → 200, to defaults to now** | boundary | P2 | admin | GET kpis `?from=now-7d` | 200; toUtc ≈ now | **GAP** — only "both" and "neither" are tested; the one-sided default branch is uncovered |
| BE-TC-10-25 | **only `to` supplied (from omitted) → 200, from defaults to to−... or now−30d** | boundary | P2 | admin | GET kpis `?to=now` | 200; valid window | **GAP** — the other one-sided branch uncovered; also guards against `from >= to` false-positive when only `to` is set |
| BE-TC-10-26 | malformed date param (`from=notadate`) | negative | P2 | admin | GET kpis `?from=notadate` | 400 (model binding), not 500 | **GAP** — unparseable date input untested |

## Empty platform

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-10-27 | Far-future window → 200 + zeros, empty breakdown arrays | state | P0 | admin | GET kpis 2098 window | 200; numeric KPIs=0; bySubject/byGrade empty arrays | **Covered** — `EmptyPlatform_FutureDateWindow_Returns200WithZeros` |

## Breakdowns (the under-tested area)

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-10-28 | **bySubject carries real data after a completed attempt** | persistence | P1 | seed a completed Learning attempt for a subject in-window | GET kpis | bySubject contains the subject with count ≥1 | **GAP** — bySubject only asserted *empty* (empty window) or *present*; never populated with real data |
| BE-TC-10-29 | **byGrade carries real data after a completed attempt** | persistence | P1 | seed completed attempt at a grade | GET kpis | byGrade contains the grade with count ≥1 | **GAP** — same as above for the grade dimension |
| BE-TC-10-30 | **Language (ar/en) breakdown present** | functional | P1 | admin | GET kpis | a language breakdown facet exists per AC ("first-class breakdown dimension alongside subject and grade") | **GAP / spec-confirm** — the story AC names **language (ar/en)** as a breakdown, but the DTO shape test lists only bySubject + byGrade. Either the facet is missing (real AC gap) or named differently — confirm against `PlatformKpiSummaryDto` |
| BE-TC-10-31 | lessonsCompleted / totalAttempts increase after a real completed attempt | persistence | P2 | seed completed attempt in-window | GET kpis before/after | both counters increase | **GAP** — Learning aggregation only tested at *empty* (zeros); the positive aggregation path (real lessons) is not exercised |
| BE-TC-10-32 | missionsCompleted / xpEarnedInWindow reflect real engagement | persistence | P2 | seed a completed mission / XP award in-window | GET kpis | counters > 0 | **GAP** — engagement aggregation positive path untested |
