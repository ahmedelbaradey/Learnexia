# P7-10 Platform Analytics — Coverage Report

**Story:** `user-stories/Phase-7-Admin-Console/P7-10-platform-analytics-dashboard.md`
**Buildability brief:** `docs/briefs/P7-10-analytics-buildability.md`
**Controller:** `backend/src/Modules/Identity/.../Controllers/AdminAnalyticsController.cs` (`api/Admin/Analytics`, AdminOnly)
**Handler:** `GetPlatformKpisQueryHandler` (in-handler range validation; 4 Shared.Contracts seams)
**Existing suite:** `backend/tests/Learnexia.IntegrationTests/P7_10_PlatformAnalytics_Tests.cs`

## Counts

| Bucket | Total | Covered | GAP |
|---|---|---|---|
| Backend | 32 | 18 | **14** |
| Frontend (reference) | 12 | n/a | n/a |

## Acceptance-criteria → coverage matrix

| AC (story) | Backend case IDs | Verdict |
|---|---|---|
| AC-1 KPI cards (active users, retention, lessons/quizzes, engagement) over selectable range | 10-06, 10-12, 10-13, 10-22, 10-23; **10-31/10-32 real-data GAP** | Partial — fields present + AI-safety real-data covered; learning/engagement positive aggregation uncovered |
| AC-2 Charts/trends w/ date-range + breakdown by subject/grade/**language (ar/en)** | 10-28, 10-29 (subject/grade GAP), **10-30 language GAP** | **Gap — breakdowns barely tested; language facet may be missing** |
| AC-3 Aggregates only, no individual child PII | (implicit — DTO has no per-child fields) | Covered by construction; assert via DTO shape 10-06 |
| AC-4 Fast summary query, no live-latency hit (NFR-1); cached allowed | (handler is parallel AsNoTracking fan-out; no perf test) | Not asserted (out of integration-test scope) |
| AC-5 Admin-only; non-admin → 403 | 10-01..04 | Covered |
| AC-6 Cross-module via contracts, not FK joins | (architectural — verified in code, not test) | Covered by construction |
| Honest-v1 N/A markers (retention/session/revenue/AI-volume/quizzes) | 10-07..11 | Covered |
| Range validation (from<to, ≤365d, defaults) | 10-18..27; **10-21 (==365), 10-24/25 (one-sided), 10-26 (malformed) GAP** | Mostly covered; boundary + one-sided defaults uncovered |

## Risk notes

1. **Breakdowns are the real coverage hole.** AC-2 makes subject, grade, and **language (ar/en)** first-class breakdown dimensions. The suite only asserts `bySubject`/`byGrade` are **present** (DTO shape) and **empty** (empty window). It never seeds real learning data and asserts the breakdown carries it (10-28/10-29/10-31). More importantly, **the language (ar/en) facet appears absent from the DTO shape test** (only bySubject + byGrade are listed) while the story names language as first-class — 10-30 is a spec/coverage confirm that may surface a real AC gap.
2. **N/A-marker contract is well covered** — this is the load-bearing "honest v1" behaviour and the existing suite tests every marker. Low risk here.
3. **Range validation is strong but boundary-thin.** 366d (fail) is tested; exactly 365d (must pass) is not (10-21). One-sided `from`-only / `to`-only defaulting (10-24/10-25) is untested and is where off-by-one default-window bugs hide.
4. **Positive real-data aggregation is only proven for AI safety.** Learning lessons/attempts and engagement missions/XP are only ever asserted at zero (empty window). If a seam's group-by or window predicate is wrong, the empty-window test would still pass — 10-31/10-32 close that.

## Prioritized backend GAP list for api-tester

**P1:**
- 10-21 window == exactly 365d → 200 (inclusive boundary)
- 10-28 bySubject carries real data after a completed attempt
- 10-29 byGrade carries real data after a completed attempt
- 10-30 language (ar/en) breakdown present (confirm against `PlatformKpiSummaryDto`; escalate if missing)

**P2:**
- 10-24 / 10-25 one-sided `from` / `to` defaulting → 200 valid window
- 10-26 malformed date param → 400 not 500
- 10-16 subscriptions live (not windowed) on far-future window
- 10-17 subscriptionsByTier structure valid
- 10-31 lessons/attempts counters increase after real attempt
- 10-32 missions/XP counters reflect real engagement

## Open questions / assumptions for the lead

- **Language breakdown (10-30):** the story AC names ar/en as a first-class breakdown, but `GetPlatformKpisQueryHandler` only maps `BySubject` and `ByGrade` into the DTO. Confirm whether a `ByLanguage` facet exists on `PlatformKpiSummaryDto`. If not, this is a genuine AC-2 gap to log (design-only here; no code change).
- Confirm the `PlatformAiSafetyStats` "Flagged" definition (ActionTaken ≠ "Blocked") matches the dashboard's intended flagged semantics — 10-14 depends on Regenerated counting as flagged (it does, per the contract doc).
- NFR-1 (no live-latency hit / caching) is not assertable in the integration suite; flagged as out of scope here — raise with the lead if a perf gate is wanted.
