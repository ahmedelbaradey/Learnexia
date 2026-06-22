# P5-01 — Weekly Report Generation — Backend Test Cases

> Story: [P5-01 Generate a weekly student report](../../../user-stories/Phase-5-Parent-Analytics/P5-01-weekly-report-generator.md)
> Task: [P5-01-BE](../../../tasks/Backend/Phase-5-Parent-Analytics/P5-01-BE.md)
> Surface under test: the **GENERATION path** — `WeeklyReportJob` (Hangfire sweep) → `IWeeklyReportGeneratorService` / `WeeklyReportGeneratorService` → persisted `WeeklyReport` row + `WeeklyRecapReadyIntegrationEvent`. **Localization is verified at READ** (`GetWeeklyReportQueryHandler`).
> Target agent: **api-tester**
> File type: integration tests in `backend/tests/Learnexia.IntegrationTests/` (new file `P5_01_WeeklyReportGeneration_Tests.cs`), mirroring `P5_08_ParentReadApi_IntegrationTests` (Testcontainers Postgres, `[Collection("IntegrationTests")]`, `ApplyMigrationsAndSeedAsync`).

## Scope and de-duplication (read before implementing)

The READ endpoint `GET /Parent/Children/{id}/WeeklyReport` is **already covered** by `P5_08_ParentReadApi_IntegrationTests` (E9-HAPPY / E9-NO-REPORT / E9-WEEK-PARAM / E9-IDEMPOTENT / E9-IDOR). **Do not re-implement those.** This file's gap focus is the **generation correctness** path against real data and the recommendation localize-at-read contract that P5-08 does not assert.

Already covered by **unit tests** — DO NOT duplicate as integration cases (re-listed in the coverage report for traceability):
- `WeeklyReportGeneratorServiceTests` (Parent.UnitTests): GEN-01 first-run create, GEN-02 idempotent rerun-update, GEN-03 no-activity zeroed row, GEN-04 XP-seam-failure graceful degrade, GEN-05 structured `{code, skillName}` recommendation persistence (no prose).
- `WeeklyRecapPublishTests` (Parent.UnitTests): RC-01..RC-06 — publish-on-active, publish-when-skills>0/xp=0, suppress-on-pure-zero, event field correctness, publish-throws fail-soft, row-persisted-despite-publish-failure.

The cases below are **integration-level** (real Postgres, real seams, real DI, real localizer) and exercise the end-to-end aggregation + event wiring + read localization that the in-memory unit tests cannot.

## Seeding notes (binding for the implementer)

- Use the P5-08 harness helpers: `RegisterParentAsync`, `AddChildAsync` (handles seat provisioning via `SeatTestSupport`). Seed curriculum with `LearningSeeder.SeedAsync` + `BadgeSeeder` where multi-subject mastery/XP is needed (mirror `P9_06_HabitLoop_Tests.InitializeAsync`).
- "Real activity" for a child means: seed mastery rows (`StudentSkillMastery`) and XP/attempt data in the source modules so the cross-module seams (`IStudentXpTimeSeriesQuery`, `IStudentMasterySummaryQuery`, `IStudentAllSubjectsWeakAreasQuery`) return non-zero data for the report week. Prefer seeding through the same seams the existing P5-08 / P9-06 tests use; if direct DbContext seeding is required, seed in the source module's DbContext within a DI scope.
- Trigger generation by resolving `IWeeklyReportGeneratorService` from a scope and calling `GenerateAsync(childId, weekStartUtc)` — this is the proven approach in P5-08 E9 (the Hangfire job is a thin wrapper; see GEN-INT-07 for the job-level sweep).
- `weekStartUtc` must be a **Monday 00:00 UTC**; reuse the `GetLastMonday()` helper from P5-08.

---

## Test cases

### GEN-INT-01 — Multi-subject active week generates exactly one report with correct aggregates
- **Type:** functional / persistence
- **Priority:** P0
- **Traces to:** AC1 (report covers XP earned, skills improved, weak areas, recommendations); AC2 (prior week)
- **Preconditions / seed:** Register parent P, add child C. Seed activity across ≥2 of the 4 subjects within the report week: XP totalling a known value (e.g. 240), mastery on several skills (some ≥ threshold so `SkillsImproved` > 0), and ≥1 weak skill so the weak-area snapshot is non-empty.
- **Steps:**
  1. Resolve `IWeeklyReportGeneratorService`; call `GenerateAsync(C, lastMonday)`.
  2. Query `ParentDbContext.WeeklyReports` for `(ChildId == C && WeekStartUtc == lastMonday)`.
- **Expected result:** Exactly **one** row. `XpEarned` equals the seeded weekly XP sum (240). `SkillsImproved` > 0. `WeakAreasJson` deserialises to a non-empty array whose items carry `skillId`, `skillName`, `subjectCode`, `masteryPercent`, `severity`. `GeneratedAtUtc` is set (within the test run window). `RecommendationsJson` is a non-empty array.

### GEN-INT-02 — Recommendations persisted as STABLE CODES, not localized prose
- **Type:** persistence / contract
- **Priority:** P0
- **Traces to:** AC1 (recommendations); P5-01-BE-2 design (localize-at-read pattern, mirror P5-07)
- **Preconditions / seed:** Child C with ≥1 High-severity weak skill (mastery < 30%) and ≥1 Medium/Low weak skill, so both recommendation codes appear.
- **Steps:**
  1. `GenerateAsync(C, lastMonday)`.
  2. Read the persisted `WeeklyReport.RecommendationsJson` directly from `ParentDbContext`.
- **Expected result:** JSON contains `REVIEW_CONCEPT` (for the High weak area) and `PRACTICE_SKILL` (for Medium/Low), each with a `skillName` field. JSON does **NOT** contain rendered prose (no `"Review concept for"`, no `"Practice skill:"`, no Arabic prose). Confirms write stores codes, render happens at read.

### GEN-INT-03 — Zero-activity week: report row written but NO recap event published
- **Type:** negative / boundary / persistence
- **Priority:** P0
- **Traces to:** AC4 (no-activity week states it clearly, no garbled data); P5-04 link (recap suppression); FR-GM-8 never-shaming
- **Preconditions / seed:** Freshly-linked child C with **no** activity seeded (no XP, no mastery, no attempts). Reset `_factory.PushSender` before the run.
- **Steps:**
  1. `GenerateAsync(C, lastMonday)`.
  2. Inspect `WeeklyReports` for `(C, lastMonday)`.
  3. Query `NotificationsDbContext.Notifications` for `RecipientExternalUserId == C && Code == "WEEKLY_RECAP"` (allow a short `Task.Delay` for the in-process publish, mirror P9-06).
- **Expected result:** Exactly one `WeeklyReport` row exists with `XpEarned == 0`, `SkillsImproved == 0`, `WeakAreasJson == "[]"`, `RecommendationsJson == "[]"`. **No** `WEEKLY_RECAP` notification row is written (recap suppressed at producer). No exception thrown.
- **Note:** Distinct from the P5-08 E9-NO-REPORT case, which asserts the *read* zero-state when no row exists at all. Here a row DOES exist (job processed the week) but the nudge is suppressed.

### GEN-INT-04 — Active week publishes WeeklyRecapReady targeting the correct child, then a WEEKLY_RECAP inbox row appears
- **Type:** functional / integration (event wiring)
- **Priority:** P0
- **Traces to:** AC1/AC2 (report generated); bridges to P5-04 AC1
- **Preconditions / seed:** Parent P + child C with real activity (XP > 0 OR SkillsImproved > 0). Reset push sender.
- **Steps:**
  1. `GenerateAsync(C, lastMonday)`.
  2. `Task.Delay(~400ms)` for in-process MediatR dispatch (mirror P9-06 TC-04).
  3. Query `Notifications` for `RecipientExternalUserId == C && Code == "WEEKLY_RECAP"`.
- **Expected result:** ≥1 `WEEKLY_RECAP` notification row exists with `Category == NotificationCategory.WeeklyReport`. (P5-04 cases assert the parent-targeting + content rendering in depth — keep this case to the "generation triggers delivery" wiring only.)
- **Overlap note:** This is the producer half. The consumer/delivery assertions live in `P5-04`. Keep this case lightweight to avoid duplicating P9-06 TC-04/TC-06.

### GEN-INT-05 — Idempotent re-run for the same (child, week) overwrites with fresher data, no duplicate row
- **Type:** persistence / regression
- **Priority:** P1
- **Traces to:** AC2 (scheduled job; safe re-run); P5-01-BE-2 (idempotent upsert), unique `(ChildId, WeekStartUtc)`
- **Preconditions / seed:** Child C with activity producing XP_1.
- **Steps:**
  1. `GenerateAsync(C, lastMonday)` → record `XpEarned` (XP_1) and row `Id`.
  2. Mutate seeded activity so the seam now returns XP_2 (≠ XP_1) for the same week.
  3. `GenerateAsync(C, lastMonday)` again.
  4. Query `WeeklyReports` for `(C, lastMonday)`.
- **Expected result:** Still **exactly one** row (same `Id`); `XpEarned == XP_2` (overwritten). `GeneratedAtUtc` refreshed. Confirms the find-or-new upsert respects the unique constraint at the real DB (not just in-memory like the unit test).
- **Note:** Complements P5-08 E9-IDEMPOTENT (which asserts one-row after two identical calls). This case additionally asserts the **overwrite-with-fresher-data** semantic on the real DB.

### GEN-INT-06 — Recommendation codes localize at READ in EN and AR
- **Type:** RTL-i18n / persistence (localize-at-read)
- **Priority:** P0
- **Traces to:** AC1 (recommendations); P5-08 AC "localized EN + AR"; P5-01-BE-2 localize-at-read
- **Preconditions / seed:** Parent P + child C with ≥1 High and ≥1 Medium/Low weak skill; generate the report for the week.
- **Steps:**
  1. `GenerateAsync(C, lastMonday)`.
  2. `GET /api/Parent/Children/{C}/WeeklyReport` with parent JWT and `Accept-Language: en` (or culture header per harness convention).
  3. Repeat with `Accept-Language: ar`.
- **Expected result:** Both calls → 200 + `Successed == true`, `data.reportFound == true`, `data.recommendations` non-empty. The EN response renders English recommendation strings (each containing the skill name); the AR response renders Arabic strings (each containing the skill name). The two locales produce **different** rendered text for the same persisted codes, and **neither** contains a raw resource key (no `WeeklyReportRecReviewConcept` literal) nor the bare code (`REVIEW_CONCEPT`). Confirms localize-at-read works against the real `IStringLocalizer`.
- **Blocker check:** Confirm the harness supports per-request culture (the P5-08 file does not exercise locale headers). If culture cannot be set per request in the integration harness, mark this case **BLOCKED — harness-locale** and cover localization at the handler-unit level instead (note in execution-report).

### GEN-INT-07 — Hangfire job sweep generates the prior-week report for every linked child (fail-soft)
- **Type:** functional / persistence / negative (fail-soft)
- **Priority:** P1
- **Traces to:** AC2 (generated by a scheduled background job for the prior week); P5-01-BE-3
- **Preconditions / seed:** Parent P1 with child C1 (active), parent P2 with child C2 (active). Optionally a child C3 whose source data will fault (e.g. no seeded subjects) to prove fail-soft does not abort the sweep.
- **Steps:**
  1. Resolve `WeeklyReportJob` from DI; call `RunAsync()`.
  2. Query `WeeklyReports` for the prior-week Monday.
- **Expected result:** A `WeeklyReport` row exists for **each** linked child (C1, C2, and C3 — C3 a valid zeroed/degraded row). The job completes without throwing even if one child's aggregation fails (fail-soft per child). `WeekStartUtc` for all rows equals the job-computed **prior** Monday (last Monday − 7), not the current week.
- **Note:** Asserts the job's prior-week window math + distinct-child enumeration + fail-soft loop, which the service-level unit/integration cases do not. If the job cannot be invoked directly in the harness, mark **BLOCKED — job-invocation** and document.

### GEN-INT-08 — Distinct child enumeration: a child linked to two parents is processed once
- **Type:** boundary / persistence
- **Priority:** P2
- **Traces to:** AC2 (per-student report); P5-01-BE-3 (`.Distinct()` on `ParentStudents.StudentId`)
- **Preconditions / seed:** One child C linked to two parents P1 and P2 (two `ParentStudent` rows, same `StudentId`). Seed C with activity.
- **Steps:**
  1. Run `WeeklyReportJob.RunAsync()` (or call `GenerateAsync` once per the distinct id the job would resolve).
  2. Query `WeeklyReports` for `(C, priorMonday)`.
- **Expected result:** Exactly **one** `WeeklyReport` row for C (no duplicate from the double linkage). Confirms `.Distinct()` enumeration + unique constraint together prevent double rows.
- **Note:** If the two-parents-one-child linkage cannot be set up via the public API (seat/linkage constraints), seed `ParentStudent` rows directly via `ParentDbContext` and call the job; otherwise mark **BLOCKED — linkage-seed**.

---

## Priority summary
- **P0:** GEN-INT-01, GEN-INT-02, GEN-INT-03, GEN-INT-04, GEN-INT-06
- **P1:** GEN-INT-05, GEN-INT-07
- **P2:** GEN-INT-08

Total new integration cases: **8** (GEN-01..05 + RC-01..06 unit cases already exist and are NOT re-implemented here).
