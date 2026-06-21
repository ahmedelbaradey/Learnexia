# Pipeline Brief — P5-05 Parent Dashboard (charts + wire real analytics) [+ P5-06 grade transition, P5-04 send report]

> Surface: **`apps/student-app`** (Expo universal — React-Native-Web + Tamagui), the **PARENT** area (`app/(parent)/…`). NOT the Next.js admin app.
> Stories: [P5-05](../../user-stories/Phase-5-Parent-Analytics/P5-05-parent-dashboard.md) (primary) · [P5-06](../../user-stories/Phase-5-Parent-Analytics/P5-06-grade-transition.md) · [P5-04](../../user-stories/Phase-5-Parent-Analytics/P5-04-report-delivery.md)
> FE task file: [P5-05-FE.md](../../tasks/Frontend/student-app/Phase-5-Parent-Analytics/P5-05-FE.md) (FE-1..FE-6)
> Design refs: `design-system/screenshots/web/05-dashboard.png`, `06-reports.png`

## Summary & traceability
- **One-line task:** Replace the parent dashboard's stubbed analytics with real Phase-5 data — hand-roll RN-compatible charts, author the 9 parent-analytics api-client hooks, and wire the grade-transition + send-report controls (currently no-op/toast stubs).
- **Stories / FR-IDs:** P5-05 (FR-PA-1, FR-PA-2) · P5-06 (FR-ID-2 extends, FR-LR-1) · P5-04 (FR-PA-1).
- **BRD goal:** G3 (parent visibility / engagement) primarily; P5-06 touches G1 (adaptive content match).
- **Epic / phase:** Parent Dashboard · Phase 5 — Parent + Analytics (Week 8).
- **Product-decision alignment:** 4 subjects (Math/Science/Arabic/English — the DTOs hardcode `SubjectCode` 0=MATH,1=SCIENCE,2=ARABIC,3=ENGLISH; no Social Studies); parent-driven; no teacher role; grade transition **re-scopes + preserves history**.

## Business context & value
- **Who benefits:** the **parent** — at-a-glance weekly visibility into each child's learning (KPIs, mastery, weak areas, trends, recommendations) plus the ability to keep the child's grade current.
- **Value:** drives parent engagement/retention (they read the report instead of forgetting to check — P5-04) and keeps content matched to the child's real level (P5-06).
- **Success measures:** parents can switch between children and see only that child's real data; charts render with real series; grade transition succeeds and the child's tree re-scopes on next load while history is retained; report-ready notification reaches linked parents.

## Acceptance criteria (testable)
**P5-05 (dashboard + charts + wiring):**
- [ ] Overview KPIs (time / XP / lessons / streak + WoW deltas) come from `GET /api/Parent/Children/{id}/WeeklyKpis` — not `getOverviewKpiStub`.
- [ ] Subject-mastery card shows real per-subject % (4 subjects + overall) from `…/SubjectMastery`; subject with no data renders 0 (not hidden).
- [ ] "Areas to focus on" lists real weak areas with severity from `…/WeakAreas`; empty list → graceful empty state.
- [ ] Recommendations panel shows real items from `…/Recommendations` (i18n keys resolved EN/AR); empty list → empty state.
- [ ] **Daily-activity bar chart** renders real Mon–Sun daily-XP bars from `…/Reports` (`DailyXpSeries`), replacing the `DailyActivityCard` placeholder; **Export CSV** produces a real CSV of the series.
- [ ] **Reports charts:** 20-day XP trend (`XpTrend20Day`, exactly 20 entries) + time-of-day breakdown (`TimeOfDayBuckets`, 4 buckets) render from `…/Reports`, replacing the two `ChartPlaceholderPanel` slots.
- [ ] A parent with multiple children can switch children (existing `activeChildStore`); every panel re-fetches for the active child only.
- [ ] **RTL/ar + en:** every panel + chart renders correctly in Arabic (RTL, Eastern-Arabic numerals via Intl) and English. Charts are direction-aware (axis/bars mirror; value labels stay LTR-safe like the existing mastery readout `dir=ltr` pattern).
- [ ] **Empty / first-week states:** all-zero KPIs, empty weak-areas, zero-filled chart series render without "chart-with-no-data" errors (every analytics endpoint returns 200 zero-state, never 404).
- [ ] Error per panel → generic error strip with retry; 403/404 never leak an IDOR oracle (mirror `ReportsWeb` ErrorStrip pattern).

**P5-06 (grade transition control):**
- [ ] Per-child grade-transition control with a **confirmation step** before mutating.
- [ ] On success, the child's curriculum/skill tree reflects the new grade on next load; history (XP/badges/streaks/mastery) remains visible.
- [ ] Invalid target grade (outside 1–6) → clear rejection message.
- [ ] Only a parent linked to that child can perform it (server-enforced).
- [ ] **BLOCKED — see Open Questions OQ-3:** there is **no parent-facing grade endpoint**; needs a product/scope decision before this AC is wireable.

**P5-04 (send report):**
- [ ] **BLOCKED — see OQ-4:** "Send Report" currently a local toast. There is **no report-send/resend HTTP endpoint**. P5-04's actual scope is *automatic* notification on report generation (Hangfire), not an on-demand FE button — confirm whether an FE "Send/Resend" action is in scope at all.

## Affected modules & data
- **No new backend entities/migrations for P5-05** — all 9 read endpoints + DTOs already exist (`ParentAnalyticsController`, `…/Features/Analytics/*`). This is a **frontend + api-client** story.
- **New FE artifacts:** a `BarChart` primitive; 9 (or subset) typed analytics query hooks; possibly a grade-transition mutation hook; i18n keys for new chart/recommendation/grade-transition copy.
- **Charting dependency:** **`react-native-svg` is NOT a dependency** anywhere in the repo (verified — no package.json references it; `recharts` also absent, confirming the constraint). See OQ-1.

### Verified backend contracts (read from controller + DTOs; `BaseResponse<T>` envelope, success flag `Successed`)
All routes are `[Authorize(Roles="Parent,Admin,SuperAdmin")]`, base `api/Parent`. ChildId from path; **acting parent resolved from JWT**, IDOR-guarded (`IsParentOfChildAsync`) → generic **403** on non-owned child. Empty/first-week → **200 zero-state, never 404**. Handlers are real (e.g. `WeeklyKpis` sums real `IStudentXpTimeSeriesQuery` + `IStudentLearningStatsQuery` seams with per-seam try/catch graceful degradation) — **not stubs** (per HANDOFF: "all backend shipped"). See OQ-3.5 for the live-data caveat.

| Panel | Route | Response shape (key fields) |
|---|---|---|
| Overview KPI strip | `GET …/Children/{id}/WeeklyKpis` | `WeeklyKpisDto`: `timeLearningMinutes, xpEarned, lessonsDone, streakDays, timeLearningDeltaMinutes, xpDelta, lessonsDelta, streakDelta` (int). **Note:** DTO field is `xpDelta` (absolute), but the current stub/UI uses `xpDeltaPercent` — FE must adapt the KPI delta copy. |
| Subject mastery | `GET …/Children/{id}/SubjectMastery` | `SubjectMasteryResponseDto`: `overallPercent`, `bySubject[]` = `{ subjectCode:int(0..3), percent:int }`, always 4 entries. |
| Areas to focus | `GET …/Children/{id}/WeakAreas` | `WeakAreasResponseDto.areas[]` = `{ skillId, skillName, subjectCode:int, masteryPercent, severity:int(1..3), suggestedNextAction:string(i18n key) }`. |
| Daily/20-day/time-of-day charts | `GET …/Children/{id}/Reports` | `ChildReportsDto`: `dailyXpSeries[]` + `xpTrend20Day[]` (each `{ day:"yyyy-MM-dd", xp:int }`, 20-day is exactly 20 zero-filled), `timeOfDayBuckets[]` = `{ bucket:"Morning|Afternoon|Evening|Night", totalXp:int }` (4). |
| Recommendations | `GET …/Children/{id}/Recommendations` | `RecommendationsDto.items[]` = `{ skillId, subjectCode:int, titleKey, bodyKey, ctaKey, severity:int, actionType:int(1..4), targetDifficulty:int(1..3) }` — **all text are i18n keys**, resolve EN/AR. |
| Child progress card | `GET …/Children/{id}/Progress` | `ChildProgressDto`: `level, totalXp, currentStreak, bestStreak, masteryPercent, weakestSkill?{…}, activeToday, energy{…}`. Feeds `ChildDashboardCard`/`MyChildren` (replaces `getChildStatsStub`). |
| Family "this week" strip | `GET …/Family/Summary` | `FamilySummaryDto`: `activeLearners, lessonsCompleted, totalXp, bestStreakDays, badgesEarned`. Feeds `FamilySummaryStrip` (replaces `getFamilyTotalsStub`). |
| Energy screen | `GET …/Children/{id}/Energy` | `ChildEnergyDto`: `remaining, allocated, spent, purchasedBalance, cycleEndsAtUtc?, weeklyUsage[]={kind,count}` (4). Feeds `EnergyWeb` (replaces energy stubs). |
| Activity feed | `GET …/Children/{id}/Activity` | `ActivityFeedDto` (verify shape before wiring `ActivityWeb`). |
| Stored weekly report | `GET …/Children/{id}/WeeklyReport?week=yyyy-MM-dd` | `WeeklyReportDto`: `reportFound:bool, weekStartUtc?, xpEarned, skillsImproved, weakAreas[], recommendations[], generatedAtUtc?`. `reportFound=false` = "not yet" zero-state. |
| **Grade transition (PARENT)** | **MISSING** | No parent route. Closest: **admin** `POST /api/Admin/Users/{childId}/grade` (Admin-only, body `{grade, reason?, confirm}`) and **parent** `PUT /api/Parent/Update-Child` (full child PUT carrying `Grade` 1–6). See OQ-3. |
| **Report send (P5-04)** | **MISSING** | No send/resend endpoint exists; P5-04 is auto-notification on generation, not an FE button. See OQ-4. |

`subjectCode` int→subject mapping: **0=Math, 1=Science, 2=Arabic, 3=English**. The FE already has `OVERVIEW_SUBJECT` keys + per-subject accents/label keys (`ReportsWeb`); add an int→key adapter.

## Handoff → db-migration
**None.** No new entities/fields/migrations. (P5-05 is FE+api-client only; P5-05-BE already shipped.) If OQ-3 resolves to "build a parent grade-transition endpoint," that is a separate backend story (P5-06-BE) — out of this brief's scope; flag to lead.

## Handoff → backend-feature
**None for P5-05.** Two conditional items pending Open-Questions answers:
- **If P5-06 in scope and no parent endpoint chosen:** a new backend command (parent-scoped grade transition, re-scope + preserve history, `ChildGradeChanged` integration event already reserved by P7-08) — a **separate BE story**, not this cycle's FE work. Do not implement unilaterally (CLAUDE rule 9 — ask first).
- **If P5-04 on-demand send is in scope:** a new backend send/resend command — also a separate BE story.

## Handoff → designer (Design Spec needed — there IS a UI surface)
Produce `design-system/ui_kits/parent-dashboard/P5-05-*.md` grounded in `web/05-dashboard.png` + `06-reports.png` and the existing parent-dashboard kit. Cover:
- **BarChart primitive spec:** labelled bars, gradient active bar (reuse `gradientStops`/`GradientBox` tokens), value labels, axis, empty/zero-state, loading skeleton; **RTL behavior** (bar order + axis mirror; value labels stay LTR). Variants needed: daily (Mon–Sun, 7 bars), 20-day trend (20 bars, denser), time-of-day (4 buckets) — decide single component + props vs variants.
- **Grade-transition control + confirmation dialog** (per-child): entry point (likely `ChildDashboardCard` footer / child card edit), confirm step copy, success/error states, invalid-grade message — **only if OQ-3 unblocks**.
- RTL/ar + en + dark/light for all new/changed panels.
- No new design pattern without asking (CLAUDE rule 8).

## Handoff → frontend
**Reuse (do NOT rebuild):** layout shells `OverviewWeb`/`ReportsWeb`, `KPIStatCard`, `MasteryBar`, `GradientBox`, `Avatar`, `ParentHeader`, `activeChildStore`, `useLocale`, `useMyChildren`, the `ReportsWeb` ErrorStrip/LoadingSkeleton/first-week patterns, the `formatNumber`/`formatDuration`/Intl ar-EG patterns, and `OVERVIEW_SUBJECT` keys + accents.

**New — api-client hooks (none exist yet; verified):**
- 9 query hooks for the analytics endpoints above. **Pattern:** the NSwag generated client does **NOT** contain these routes (stale), so hand-write hooks against the **raw `useApiClient().get<T>()`** exactly like `useOverrideChildGrade` uses `client.post<T>()`. Add query keys under a new `parentAnalytics` namespace in `query/queryKeys.ts`, keyed by childId. Export from `hooks/index.ts`.
- Prefer regenerating NSwag only if the team prefers typed-client coverage — **flag as a choice** (raw-client hooks are the lower-risk, established escape hatch). See OQ-5.

**Wiring (FE-4):** replace `parentDashboardStubs.ts` consumers 1:1 — `getOverviewKpiStub`→WeeklyKpis, `getSubjectMasteryStub`→SubjectMastery, `getFocusAreasStub`→WeakAreas, `getChildStatsStub`→Progress, `getFamilyTotalsStub`→Family/Summary, energy stubs→Energy, recommendations→Recommendations. Keep the stub file's typed enums (`OVERVIEW_SUBJECT`, severities) as adapters; delete the deterministic generators once wired. Mind the `xpDelta` (absolute) vs current `xpDeltaPercent` mismatch.

**Charts (FE-1/2/3):** build `BarChart` per the chosen altitude (OQ-2). If `react-native-svg` is approved (OQ-1), use it; otherwise hand-roll with Tamagui `Stack`/`GradientBox` height-scaled bars (the existing mastery bar already does flex-width fills — a vertical-bar analogue is feasible without SVG for simple bar charts). Direction-aware; tokens only.

**Grade transition (P5-06 / FE):** wire the `ChildDashboardCard` grade control + confirm dialog **only if OQ-3 unblocks**; otherwise leave the documented stub and report it blocked.

**Send Report (P5-04 / FE-5):** leave as-is or remove per OQ-4 decision. Per the design note in `ReportsWeb`, the period-select + Send Report were already removed from the header — confirm current intent with the lead.

## Handoff → frontend-e2e-tester
After the frontend batch, drive the running web PWA (Playwright) for:
- Overview + Reports render real charts/panels for a seeded parent+child (register via API — admin superadmin token lacks parent scope; create a parent + child + some attempts so series are non-zero).
- Child switching re-fetches per child; cross-child data isolation.
- Empty/first-week child → zero-state charts, no errors.
- RTL/ar vs en for every panel + chart (bar order, numerals, value-label direction).
- Per-panel error + retry; 403 path (request another family's child id) shows generic error, no oracle.
- Grade-transition confirm flow + invalid-grade message (only if unblocked).

## Open questions / assumptions / risks (FOR THE LEAD)
- **OQ-1 — Charting approach + new dependency (ASK FIRST, CLAUDE rule 8):** `react-native-svg` is **NOT** installed anywhere (verified; `recharts` also absent → web-DOM chart libs confirmed unusable on this stack). Options: (a) **add `react-native-svg`** (Expo-supported, the idiomatic RN charting primitive) — a new dependency requiring approval; (b) **hand-roll with Tamagui `Stack`/`GradientBox`** height-scaled bars (no new dep; fine for simple bar charts, the only chart types needed here). Recommendation: bar-only charts here don't strictly need SVG — (b) is lower-risk and dependency-free. **Need the lead's call.**
- **OQ-2 — `BarChart` altitude:** FE task calls for it in `packages/ui`. Shared primitive (`packages/ui/src/components/BarChart`) is the task's intent and right if it'll be reused (admin uses Recharts, so reuse is parent-app-only for now). App-local (`(parent)/_components/`) is faster and avoids a shared-package change if it's parent-only. **Recommend packages/ui per the task, but confirm** given it's currently single-consumer.
- **OQ-3 — P5-06 grade transition is NOT wireable as written. The FE task says "backend shipped" but it is NOT:** HANDOFF confirms "ChildGradeChanged (P5-06 **not yet built**)". The only grade endpoints are **admin-only** `POST /api/Admin/Users/{childId}/grade` (a Parent JWT will be 403'd / it's `[Authorize(AdminOnly)]`) and **parent** `PUT /api/Parent/Update-Child` which carries `Grade` (1–6) but is a generic child-profile PUT — **unverified whether it re-scopes the skill tree or emits `ChildGradeChanged`**. **Decision needed:** (a) wire FE to `Update-Child` if that path already re-scopes + preserves history (needs BE verification), or (b) spin up a proper **P5-06-BE** parent grade-transition story first, then wire FE. Until decided, P5-06 FE is **blocked**.
- **OQ-3.5 — Live-data sanity:** handlers are real (not hardcoded), but per-seam try/catch means a missing/failing seam silently degrades to zeros. **Recommend** the team smoke-test each endpoint with a seeded parent+child against `http://localhost:5080` (register parent+child via API for a parent-scoped JWT) to confirm which panels return non-zero data **before** the frontend batch — so empty panels in QA are diagnosed as "no data" vs "endpoint stub/seam gap," not chart bugs.
- **OQ-4 — P5-04 "Send Report" scope:** there is **no report-send/resend endpoint**; P5-04's actual AC is *automatic* notification when the weekly report is generated (Hangfire), which is backend-only. Is an on-demand FE "Send/Resend Report" button in scope at all this cycle? If yes it needs a new backend command (separate BE story). If no, leave/remove the toast stub. **Recommend: not in FE scope this cycle** — confirm.
- **OQ-5 — api-client hooks: raw client vs NSwag regen.** The generated client lacks all ParentAnalytics routes. Recommend hand-written hooks on the raw `useApiClient()` (matches `useOverrideChildGrade`); flag if the team wants an NSwag regen instead (larger diff, touches shared generated file).
- **OQ-6 — Scope confirmation:** Confirm this cycle = **P5-05 charts + wiring** (clearly in scope, unblocked) **+ P5-06 FE** (blocked on OQ-3) **+ P5-04 FE** (likely out of scope per OQ-4). Recommendation: ship **P5-05 (FE-1..FE-4, FE-6)** now; defer P5-06 FE until the endpoint question resolves; treat P5-04 FE as out-of-scope pending OQ-4.
- **Risk — `xpDelta` shape mismatch:** the WeeklyKpis DTO returns `xpDelta` (absolute), but the current Overview KPI copy expects `xpDeltaPercent`. FE must change the delta presentation (and i18n key) — minor but a real wiring divergence.

## Recommended pipeline order (first cut — `planner` finalizes)
1. **designer** — Design Spec for `BarChart` (+ grade-transition dialog if OQ-3 unblocks). (parallel-safe with step 2 hook scaffolding)
2. **frontend — Batch A (no design dep):** author the 9 api-client analytics hooks + query keys + int→subject adapter (FE-4 plumbing). Can start before/parallel to the design spec.
3. **frontend — Batch B (needs Design Spec):** `BarChart` primitive (FE-1) → daily-activity chart + Export CSV (FE-2) + reports charts (FE-3) → finish wiring panels to hooks (FE-4) → RTL/dark QA (FE-6).
4. **frontend — Batch C (conditional):** P5-06 grade-transition control (only if OQ-3 unblocks); P5-04 send button (only if OQ-4 in scope).
5. **frontend-e2e-tester** — flows above against the running PWA (seed a parent+child via API).
6. **reviewer** — gate against this brief's ACs + CONVENTIONS. (No `db-migration`, no `backend-feature`, no `api-tester` unless OQ-3/OQ-4 add a backend story; no `security-auditor` unless a parent grade-mutation endpoint is built.)
