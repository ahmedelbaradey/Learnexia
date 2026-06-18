# Pipeline Brief — Phase-5 Parent-Scoped READ API (unblock the FAKED parent-app analytics)

> **Status: SCOPING/UNDERSTANDING pass only.** This brief is a proposal. Per CLAUDE.md rule #9, the
> user stories + BE/FE task files for this gap do **not** exist yet and must NOT be authored until the
> lead signs off on the breakdown below.

## Summary & traceability
- **One-line task:** Add **parent-scoped, per-child READ endpoints** (`/api/Parent/...`) so the parent app's analytics / gamification / energy / activity screens read **real** child data instead of the deterministic stubs in `parentDashboardStubs.ts`. The data already exists in Gamification / Learning / Billing; the fix is a **read-only fan-out** behind parent-owns-child authz.
- **User stories (source of truth):** maps to existing `P5-01` (weekly report generator), `P5-02` (weak-area detection), `P5-05` (parent dashboard — the FE consumer), `P5-06` (grade transition). **Gap:** the *dashboard/KPI/energy/activity READ endpoints themselves* are not covered by any current P5 story — see "Mapping" for the proposed **new story P5-08**.
- **FR-IDs:** FR-PA-1 (weekly report), FR-PA-2 (weak areas), FR-PA-3 (analytics capture — P5-03), FR-ID-2/FR-LR-1 (grade transition — P5-06).
- **BRD goal:** **G3** (engage parents / give them visibility) primary; **G1/G5** (retention, outcomes) via the KPIs.
- **Epic / phase:** Parent & Analytics — Phase 5 (Week 8). **Backend-only build for this lead** (frontend is a separate lead; `P5-05-FE` already exists and defines the contract the FE expects).

## Business context & value
- **Who benefits:** the **parent** (primary). The parent app already renders My Children cards, an Overview (KPIs / mastery / focus areas / recommendations / daily activity), Reports (XP trend / time-of-day), Helper Energy, and an Activity feed — **all faked**. The parent currently sees plausible-but-fake numbers.
- **Value:** turns the already-shipped, pixel-perfect parent UI into a truthful product surface. This is the single biggest "looks done but isn't" gap in the parent experience.
- **Success measure:** every `TODO(P5)` / `TODO(P5-05)` / `TODO(P-activity)` / `TODO(Batch D)` stub in `apps/student-app/app/(parent)/_components/parentDashboardStubs.ts` is replaced by a typed call to a real `/api/Parent/...` endpoint, and the numbers reconcile with the child's own self-scoped views.

## The exact FE contract being unblocked (authoritative field list)
`apps/student-app/app/(parent)/_components/parentDashboardStubs.ts` is the **gold-standard contract** — it enumerates every field the FE needs. Summarised:
- **`ChildStatsStub`** (My Children card): `grade, level, xp, streakDays, masteryPercent, weakestTopicKey, activeToday, energy, locale`.
- **`FamilyTotalsStub`** (family "this week" strip): `activeLearners, lessonsCompleted, totalXp, bestStreakDays, badgesEarned`.
- **`OverviewKpiStub`** (Overview "this week" + WoW deltas): `timeLearningMinutes(+delta), xpEarned(+deltaPercent), lessonsDone(+delta), streakDays(+delta)`.
- **`SubjectMasteryStub[]`**: `{subject ∈ {Math,Science,Arabic,English}, percent}`.
- **`FocusAreaStub[]`**: `{topicKey, subject, percent, severity ∈ {high,medium}}` (= weak areas; **P5-02**).
- **`EnergyBalanceStub`** + **`EnergyUsageStub[]`**: `{balance, cap, resetsInDays, dailyCap}` + per-helper `{kind ∈ hints|explain|deep|practice, count}`.
- **`ActivityEventStub[]`**: `{kind, category ∈ badge|energy|alert, childName, minutesAgo, amount?}`.
- **Reports charts:** daily-XP series (Mon–Sun), 20-day XP trend, time-of-day buckets (`P5-05-FE-2/3`).

## Data-availability finding (the load-bearing result of this pass)
**Every required datum already exists** in a source module. Nothing here needs a brand-new fact table; it needs **new cross-module READ seams + time-windowed aggregate queries**:

| Datum | Source module | Where it lives today | Derivable? |
|---|---|---|---|
| level / totalXp | Gamification | `StudentXpProfile` (via `IStudentXpQuery`) | ✅ seam exists |
| streak / longest / freeze | Gamification | `IStudentStreakQuery` | ✅ seam exists |
| badges (count + recent) | Gamification | `IStudentBadgesQuery` | ✅ seam exists |
| missions / league | Gamification | `IStudentMissionsQuery` / `IStudentLeagueQuery` | ✅ seams exist |
| **daily-XP series, weekly XP + WoW** | Gamification | **`XpAward`** (append-only ledger, `XpAmount` + `OccurredAtUtc`) | ✅ **derivable — NEW time-windowed seam** |
| mastery % per skill / per subject | Learning | `StudentSkillMastery` (`MasteryDto.MasteryPercentage`, `MasteryStatus`) | ✅ data exists; **NEW cross-module seam** (today only in-process `IMasteryService` + Student-only HTTP) |
| **weak areas** (severity) | Learning | mastery `NeedsReview` = accuracy `<50%` is **exactly** the P5-02 bar (`MasteryEngine`/`MasteryStatus`) | ✅ **derivable — P5-02 + NEW seam** |
| lessons completed (count, window) | Learning | `Attempt` (`Status`, `CompletedAt`) + completed-lesson reads | ✅ derivable — NEW windowed seam |
| time-learning (minutes), time-of-day, accuracy | Learning | `Attempt.DurationSeconds`, `Attempt.CompletedAt`, `Attempt.AccuracyPercentage` | ✅ derivable — NEW windowed seam |
| energy balance / cap / daily-cap | Billing | `ICreditSpendService.GetBalanceAsync` → `EnergyBalance` | ✅ seam exists |
| energy weekly usage per helper kind | Billing | `CreditTransaction` (`ReasonCode`, `OccurredAtUtc`) | ✅ derivable — NEW windowed seam |
| activeToday | Learning (or Gamification) | derive from last `Attempt.CompletedAt` / streak `LastActivityDateUtc` | ✅ derivable |
| activity feed events | Gamification + Billing (+ Learning) | XpAward/StudentBadge/level-up + CreditTransaction + lesson completes | ⚠️ **needs a decision** — no unified feed today (see OQ-6) |
| grade / locale | Identity/Parent | child profile (already in `IChildLearningProfileQuery` / Parent link) | ✅ exists |

**Reference implementation pattern (reuse this exactly):** `GetUserActivitySummaryQueryHandler`
(`backend/src/Modules/Identity/.../Users/Queries/AdminGetUserActivity/`) already fans out to **five**
Gamification seams with **per-seam try/catch graceful degradation** (a failing seam → null field, never a
500). The new Parent handlers should mirror this shape. The in-process `IMasteryService` doc-comment
(`Learning.Application/Services/IMasteryService.cs`) **explicitly flags P5-02** as needing a new
`Shared.Contracts` seam — confirming the approach below.

## Acceptance criteria (testable; the reviewer checks these)
- **AC1 — Endpoints exist & are parent-scoped.** Every endpoint below returns `BaseResponse<T>` (`Successed` flag) and is gated `[Authorize(Roles="Parent")]` (Admin/SuperAdmin may also call for support, matching the existing `ParentController`).
- **AC2 — IDOR-safe.** `parentUserId` is **always** resolved from the JWT (`ICurrentUserService`), never from the request. Every per-child endpoint calls `IParentChildQuery.IsParentOfChildAsync(parentId, childId)` and returns a **generic 403** when false — **without** distinguishing "not your child" from "child doesn't exist" (per the seam's anti-IDOR contract).
- **AC3 — Graceful degradation.** A child with no activity / brand-new profile returns clean zero-state (level 1, xp 0, streak 0, empty weak-areas, etc.) — never 404/500/garbled — and an empty week is stated clearly (mirrors P5-01 AC4 + the seam sentinel contracts).
- **AC4 — Module isolation.** No Parent→{Gamification,Learning,Billing} project reference. All cross-module reads go through `Shared.Contracts` seams only. No cross-module FK.
- **AC5 — Localized + envelope + logging.** Localized messages (`IStringLocalizer<SharedResources>`, ar-EG + en-US), `ILoggerManager` (not `ILogger<T>`), Option-C persistence (no EF/DbContext in Application; reads behind Infrastructure services).
- **AC6 — Field parity with the FE contract.** Response DTOs cover every field enumerated in "The exact FE contract" above (the FE swaps stubs 1:1).
- **AC7 — Weak areas (if P5-02 in scope).** Weak areas derive from mastery `<50%` (`NeedsReview`) + recent accuracy, carry a severity, and resolved areas drop off (P5-02 ACs).
- **AC8 — Performance.** Per-child endpoint stays within NFR-1; fan-out reads run concurrently where safe; consider a short cache for the family summary (OQ-8).

## Endpoint inventory (finalized proposal — `/api/Parent` base)
> Routes shown without the `api/` prefix for readability; the controller route base is `api/Parent` (existing `ParentController` convention). `{childId}` is validated via `IParentChildQuery` on every call.

| # | Method & route | Response DTO (key fields) | Source modules | Story |
|---|---|---|---|---|
| E1 | `GET /Parent/Children/{childId}/Progress` | `level, totalXp, currentStreak, longestStreak, masteryPercent, weakestSkill{id,name,severity}, activeToday, energy{balance,cap,dailyCap,resetsInDays}` | Gamification + Learning + Billing | **P5-08** (new) |
| E2 | `GET /Parent/Family/Summary` | `activeLearners, lessonsCompletedThisWeek, totalXp, bestStreakDays, badgesEarned` (+ per-child mini-cards array for My Children) | Gamification + Learning | **P5-08** (new) |
| E3 | `GET /Parent/Children/{childId}/WeeklyKpis` | `timeLearningMinutes(+deltaMinutes), xpEarned(+deltaPercent), lessonsDone(+delta), streakDays(+delta)` (WoW = this-week vs prior-week) | Learning + Gamification | **P5-08** (new) |
| E4 | `GET /Parent/Children/{childId}/SubjectMastery` | `[{subject, percent}]` for Math/Science/Arabic/English | Learning | **P5-08** (new) |
| E5 | `GET /Parent/Children/{childId}/WeakAreas` (a.k.a. focus areas) | `[{skillId, topicKey, subject, percent, severity}]` | Learning (P5-02 logic) | **P5-02** |
| E6 | `GET /Parent/Children/{childId}/Reports?period=` | `dailyXp[7], xpTrend[20], timeOfDay[buckets]` (+ latest weekly report when P5-01 lands) | Gamification (`XpAward`) + Learning (`Attempt`) | **P5-08** (reports read) / **P5-01** (stored report) |
| E7 | `GET /Parent/Children/{childId}/Energy` | `balance, cap, resetsInDays, dailyCap, weeklyUsage[{kind,count}]` | Billing | **P5-08** (new) — see OQ-3 |
| E8 | `GET /Parent/Children/{childId}/Activity` *(or `/Parent/Family/Activity`)* | `[{kind, category, childName, occurredAtUtc, amount?}]` | Gamification + Billing (+ Learning) | **P5-08** (new) — see OQ-6 |
| E9 | `GET /Parent/Children/{childId}/WeeklyReport` | stored weekly report (XP, skills improved, weak areas, recommendations) | aggregate, persisted by job | **P5-01** |

**Recommendation:** ship E1–E4 + E6 (live reads) first — they unblock the bulk of the FE and depend only on
seams. E5 (weak areas) needs the P5-02 derivation. E7/E8 carry open questions. E9 is the P5-01 scheduled-report read.

## Mapping to existing P5 stories — and the proposed NEW story
- **P5-01 (Generate a weekly student report)** — owns **E9** (the *persisted, scheduled* report) and the stored-report half of E6. Hangfire infra already exists (Billing/Curriculum jobs), so the "scheduled job" dependency is met. P5-01 does **not** cover the live dashboard reads.
- **P5-02 (Detect and rank weak areas)** — owns **E5** and the weak-area inputs to E1/E6/E9. The detection logic (mastery `<50%` + severity) is the P5-02 deliverable; the data exists (`StudentSkillMastery`). **E5 is genuinely blocked on P5-02 being built** (today the cross-module weak-area seam is `EmptyWeakAreasQuery` — a P3-09 placeholder that returns `[]`).
- **P5-05 (View the parent dashboard, FE)** — the **consumer**. Its task file `P5-05-FE.md` already lists the exact contract ("Contract from Backend (Phase 5)") and is blocked on these BE endpoints. No change to P5-05 needed.
- **P5-06 (Grade transition)** — **out of scope here** (it's a write/command, already storied). Listed only because the dashboard hosts its control.
- **GAP → propose NEW story `P5-08` "Parent-scoped per-child read API (dashboard/KPI/energy/activity)".** E1–E4, E6 (live), E7, E8 are **not covered** by P5-01 (report) or P5-02 (weak areas). Folding ~7 read endpoints + 4–5 new cross-module seams into P5-01 would overload a "report generator" story and blur traceability. **Recommendation: author a new `P5-08` for the live read API**, keep E5→P5-02 and E9→P5-01. (Alternative if the lead prefers fewer stories: fold E1–E4/E6/E7/E8 into an **expanded P5-01** retitled "Parent reports & read API" — call this out as the lead's choice; see OQ-1.)

## Proposed BE task breakdown (for lead sign-off — do NOT author yet)
> IDs are proposals. Dependency order top→bottom. "Module" = where the code lands.

**A. Cross-module read seams (foundational; mostly parallelizable, but serialize the Program.cs/DI edits):**
- `P5-08-BE-1` — **Gamification:** add `IStudentXpTimeSeriesQuery` seam (daily-XP series + weekly-XP-with-WoW from `XpAward`). *(Gamification.Infrastructure + Shared.Contracts)*
- `P5-08-BE-2` — **Learning:** add `IStudentLearningStatsQuery` seam (lessons-completed count, time-learning minutes, time-of-day buckets, accuracy — windowed, from `Attempt`). *(Learning + Shared.Contracts)*
- `P5-08-BE-3` — **Learning:** add `IStudentMasterySummaryQuery` seam (per-subject mastery % + overall mastery %) — the cross-module surface the in-process `IMasteryService` doc flags as missing. *(Learning + Shared.Contracts)*
- `P5-02-BE-1` — **Learning:** weak-area detection service (mastery `<50%`/`NeedsReview` + recent accuracy → severity), and **re-wire `IStudentWeakAreasQuery`** from `EmptyWeakAreasQuery` to the real implementation. *(Learning + Shared.Contracts)*
- `P5-08-BE-4` — **Billing:** add `IChildEnergyUsageQuery` seam (weekly per-helper-kind usage from `CreditTransaction`); reuse existing `ICreditSpendService.GetBalanceAsync` for balance. *(Billing + Shared.Contracts)* — gated on OQ-3.

**B. Parent read controller + handlers (fan-out; depend on A):**
- `P5-08-BE-5` — **Parent:** `ParentReadController` (or extend `ParentController`) + `GetChildProgressQuery` handler (**E1**) — fans out to xp/streak/mastery/weakarea/energy seams with per-seam try/catch (mirror `GetUserActivitySummaryQueryHandler`). Includes the `IParentChildQuery.IsParentOfChildAsync` IDOR gate.
- `P5-08-BE-6` — **Parent:** `GetFamilySummaryQuery` (**E2**) — iterate `GetChildIdsForParentAsync`, aggregate.
- `P5-08-BE-7` — **Parent:** `GetChildWeeklyKpisQuery` (**E3**) — WoW deltas from the time-series + learning-stats seams.
- `P5-08-BE-8` — **Parent:** `GetChildSubjectMasteryQuery` (**E4**).
- `P5-02-BE-2` — **Parent:** `GetChildWeakAreasQuery` (**E5**) — consumes `IStudentWeakAreasQuery`/the P5-02 seam.
- `P5-08-BE-9` — **Parent:** `GetChildReportsQuery` (**E6** live charts: daily/20-day/time-of-day).
- `P5-08-BE-10` — **Parent:** `GetChildEnergyQuery` (**E7**) — gated on OQ-3.
- `P5-08-BE-11` — **Parent:** `GetActivityFeedQuery` (**E8**) — gated on OQ-6 (feed strategy).

**C. P5-01 scheduled report (separate track; can run in parallel with B once A is done):**
- `P5-01-BE-1..n` — weekly-report aggregate model + persistence, Hangfire recurring job (prior-week window, per linked child), empty-week handling, and the **E9** read endpoint. Consumes the same A-seams + P5-02. *(Keep under P5-01.)*

**Each backend task** follows `docs/dev/FEATURE_PLAYBOOK.md` + Option-C (CONVENTIONS §7), returns `BaseResponse<T>`, localized, `ILoggerManager`, and ships unit tests; HTTP-exposing tasks get an `api-tester` pass; the parent-data fan-out is **security-sensitive** (IDOR) → `security-auditor` before the gate.

## Handoff → db-migration
- **Likely ZERO new tables for the live read API (E1–E8).** All reads derive from existing tables: `StudentXpProfile`, `XpAward`, `LeagueMembership`, `StudentBadge`, `StudentMission` (Gamification); `StudentSkillMastery`, `Attempt`/`StudentAnswer` (Learning); `CreditTransaction`, `EnergyBalance` source (Billing).
- **Indexing review (the only likely DB work):** confirm/add indexes supporting the new windowed aggregates — e.g. `XpAward (StudentXpProfileId, OccurredAtUtc)`, `Attempt (StudentId, CompletedAt)` filtered on `Status=Completed`, `CreditTransaction (… , OccurredAtUtc)` filtered on Spend rows. Flag to `db-migration` to verify against existing indexes before adding.
- **P5-01 DOES need a table:** a persisted `WeeklyReport` (or report-snapshot) entity in the owning module for E9 (so the scheduled job has somewhere to write). Schema-per-module; loose int `StudentId` (no cross-module FK). Owning module = TBD (OQ-5: Parent vs a new Analytics module — **do NOT create a new module without asking**, per memory `ask-before-new-modules`).

## Handoff → backend-feature
- **New `Shared.Contracts` seams** (loose `int` ids, read-only, sentinel/empty on no-data — mirror existing seam contracts):
  - `Gamification.IStudentXpTimeSeriesQuery` → `GetDailyXpAsync(int studentId, DateOnly fromUtc, DateOnly toUtc, ct)` → `IReadOnlyList<DailyXp(DateOnly Day, int Xp)>`; `GetWeeklyXpAsync(int studentId, DateOnly weekStartUtc, ct)` → `WeeklyXp(int ThisWeekXp, int PriorWeekXp)`.
  - `Learning.IStudentLearningStatsQuery` → `GetStatsAsync(int studentId, DateTime fromUtc, DateTime toUtc, ct)` → `LearningStats(int LessonsCompleted, int TimeLearningMinutes, double AvgAccuracy, IReadOnlyList<TimeOfDayBucket> ByHour)`; plus `GetLastActivityUtcAsync` for `activeToday`.
  - `Learning.IStudentMasterySummaryQuery` → `GetByStudentAsync(int studentId, ct)` → `MasterySummary(int OverallPercent, IReadOnlyList<SubjectMastery(string SubjectCode, int Percent)>)`.
  - `Learning` weak-area: prefer **re-wiring the existing `Ai.IStudentWeakAreasQuery`** (already designed, today `EmptyWeakAreasQuery`) to the real P5-02 impl, OR add a Parent-facing `IStudentWeakAreasReadQuery` if the AI seam's `Subject`-scoped signature doesn't fit the dashboard's all-subjects need. **Decide in P5-02** (OQ-7).
  - `Billing.IChildEnergyUsageQuery` → `GetWeeklyUsageAsync(int childId, DateTime fromUtc, DateTime toUtc, ct)` → `IReadOnlyList<EnergyUsage(string Kind, int Count)>` (Kind ∈ hints/explain/deep/practice, mapped from `CreditReasonCode`).
- **Implementations** live in each source module's `*.Infrastructure` (Option C), registered in that module's DI; **consumed only** by the Parent handlers. Registration edits to `Program.cs`/module DI must be **serialized** if other pipelines run in parallel (per PARALLELISM.md).
- **Parent handlers:** CQRS queries (not commands → not auto-validated; validate `childId` + window inline). Resolve `parentId` from JWT; call `IsParentOfChildAsync` first; fan out with per-seam try/catch; map to response DTOs. Reuse `GetUserActivitySummaryQueryHandler` as the template.
- **No new design patterns** without lead approval (rule #8) — this is plain seam-injection + fan-out, which matches the existing admin-activity shape, so no pattern approval should be needed.

## Handoff → frontend (other lead — for awareness; this lead builds BE only)
- The FE work is **already storied** as `P5-05-FE` (charts + wire real data). When these endpoints land, the FE swaps `parentDashboardStubs.ts` 1:1. No new FE story needed for the read API itself; `frontend-e2e-tester` should re-run the parent flows once wired.
- **Field-name parity matters:** the BE DTOs should align with the stub field names (or the FE maps them) — coordinate the exact JSON shape via the typed `api-client` regen. The activity feed and energy-usage `kind` enums must match the FE's `ACTIVITY_KIND` / `EnergyUsageStub.kind` literals.

## Open questions / assumptions / risks (for the lead → user)
- **OQ-1 (story shaping):** New story **P5-08** for the live read API, vs **expanding P5-01** to "Parent reports & read API"? (Brief recommends a new P5-08; E5→P5-02, E9→P5-01.) **Needs lead decision before any story/task authoring.**
- **OQ-2 (KPI definitions):** Confirm exact windows: is "this week" a **rolling 7 days** or **calendar week (Mon–Sun, child timezone)**? WoW delta = vs the immediately prior 7 days / prior calendar week? "Time learning" = sum of `Attempt.DurationSeconds`? "Lessons completed" = distinct completed lessons or completed attempts? These drive E2/E3/E6 and the P5-01 report.
- **OQ-3 (include energy read?):** The energy meter is Billing/Phase-10 territory. Include **E7** in this Phase-5 wave (the data + `ICreditSpendService` exist today), or defer to a Phase-10 read task? The FE stub is explicitly DISPLAY-ONLY. **Recommend: include read-only balance + weekly usage now** (low cost, big FE unblock) unless the lead wants Phase-10 to own all energy surfaces.
- **OQ-4 (weak areas need P5-02 built first):** **Yes.** E5 (and the weak-area inputs to E1/E6/E9) are blocked on P5-02 — today the cross-module weak-area seam is the `EmptyWeakAreasQuery` placeholder. Confirm P5-02 is in this wave (recommended) or E1/E6 ship with weak-area omitted initially.
- **OQ-5 / OQ-6 (owning module + activity feed):** (5) Which module owns the **persisted WeeklyReport** (P5-01) and the new Parent read controller — the **Parent** module, or a new **Analytics** module? **Do not scaffold a new module without asking** (memory: `ask-before-new-modules`). Recommend Parent module for the read controller; report persistence TBD. (6) The **activity feed (E8)** has no unified source today — options: (a) compose on-read by merging XpAward/StudentBadge/level-up + CreditTransaction + lesson-completes across seams (read-time, no new table), or (b) defer E8 to a dedicated activity story. Recommend (a) read-time compose for MVP, or defer if the lead wants it clean.
- **OQ-7 (weak-area seam reuse):** Re-wire the existing `Ai.IStudentWeakAreasQuery` (subject-scoped) to the real P5-02 impl, or add a new all-subjects Parent-facing read seam? Decide in P5-02.
- **OQ-8 (caching/perf):** `GET /Parent/Family/Summary` fans out N children × several seams. Acceptable for MVP family sizes, or add a short Redis cache (Redis is already in the stack)? Flag for the planner.
- **Risk — `EnergyBalance.GetBalanceAsync` side effect:** its doc says it "creates an empty account if none exists (idempotent bootstrap)" — i.e. a **read endpoint may write**. Confirm this is acceptable from a parent-triggered read, or add a pure read overload. (Low risk, but note for `security-auditor`/reviewer.)
- **Assumption:** mastery (P3-09) is **built** in Learning (`StudentSkillMastery` + `MasteryEngine`), so E4/E5 have real data — verified in this pass. The blocker is the missing *cross-module seam*, not missing data.

## Recommended pipeline order (first cut — the `planner` finalizes)
1. **(Now) Lead sign-off** on story shaping (OQ-1, OQ-5) → then **author** `P5-08` (+ extend P5-01/P5-02) user stories + BE/FE task files (this is the rule-#9 gate; nothing builds before it).
2. **`analyzer` → `planner`** per story.
3. **Batch 1 (parallel where independent, serialize DI/Program.cs edits):** the cross-module read seams — `P5-08-BE-1/2/3`, `P5-02-BE-1`, `P5-08-BE-4`. `db-migration` runs first only if index changes / the P5-01 `WeeklyReport` table are needed.
4. **Batch 2 (after seams):** Parent read controller + handlers — `P5-08-BE-5..11`, `P5-02-BE-2`. `security-auditor` (IDOR) audits this batch.
5. **Batch 3 (parallel track):** P5-01 scheduled report + E9.
6. **`api-tester`** validates the running endpoints (IDOR, zero-state, window math) after each backend batch; **`reviewer`** gates each batch against the ACs above; **`committer`** opens the PR per story.
7. **(Other lead)** `P5-05-FE` wiring + `frontend-e2e-tester` once endpoints are live.

---
### Key files referenced (absolute paths)
- FE contract / stubs: `E:\Wrokspace\Learnexia\apps\student-app\app\(parent)\_components\parentDashboardStubs.ts`
- FE task (consumer): `E:\Wrokspace\Learnexia\tasks\Frontend\student-app\Phase-5-Parent-Analytics\P5-05-FE.md`
- Reference fan-out handler: `E:\Wrokspace\Learnexia\backend\src\Modules\Identity\Learnexia.Modules.Identity.Application\Features\Users\Queries\AdminGetUserActivity\GetUserActivitySummaryQueryHandler.cs`
- Parent controller (route base `api/Parent`): `E:\Wrokspace\Learnexia\backend\src\Modules\Parent\Learnexia.Modules.Parent.Api\Controllers\ParentController.cs`
- IDOR seam: `E:\Wrokspace\Learnexia\backend\src\Shared\Learnexia.Shared.Contracts\Parent\IParentChildQuery.cs`
- Existing Gamification seams: `E:\Wrokspace\Learnexia\backend\src\Shared\Learnexia.Shared.Contracts\Gamification\` (`IStudentXpQuery`, `IStudentStreakQuery`, `IStudentBadgesQuery`, `IStudentMissionsQuery`, `IStudentLeagueQuery`)
- XP ledger (time-series source): `E:\Wrokspace\Learnexia\backend\src\Modules\Gamification\Learnexia.Modules.Gamification.Domain\Entities\XpAward.cs`
- Mastery (weak-area source): `E:\Wrokspace\Learnexia\backend\src\Modules\Learning\Learnexia.Modules.Learning.Domain\Services\MasteryEngine.cs`, `...\Enums\MasteryStatus.cs`, `...\Application\Services\IMasteryService.cs` (flags the P5-02 seam gap)
- Attempt (learning-stats source): `E:\Wrokspace\Learnexia\backend\src\Modules\Learning\Learnexia.Modules.Learning.Domain\Entities\Attempt.cs`
- Billing energy: `E:\Wrokspace\Learnexia\backend\src\Shared\Learnexia.Shared.Contracts\Billing\ICreditSpendService.cs`, `EnergyBalance.cs`; ledger `...\Modules\Billing\...\Domain\Entities\CreditTransaction.cs`
- AI weak-area placeholder (to re-wire in P5-02): `E:\Wrokspace\Learnexia\backend\src\Shared\Learnexia.Shared.Contracts\Ai\IStudentWeakAreasQuery.cs` + registration in `...\Modules\Ai\Learnexia.Modules.Ai.Application\DependencyInjection.cs`
