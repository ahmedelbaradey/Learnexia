# Handoff — Phase 1 web frontend + dev environment

> Living handoff for leads/agents picking up the web frontend + backend work. Last updated 2026-06-02 (**P4-07 FE Batch 5 — dashboard LeaguePreview flip ready for committer. P4-06 — commit + PR ready. P4-05 merged via PR #77. P4-04 ready for committer. P4-03 merged via PR #75. FE: P2-09-FE merged via PR #74.**).
> Captures what's done, the decisions, the load-building config, and what's next. If you change any of these, update this file.

## P4-07 — Weekly leagues (FE Batch 5 — LeaguePreview dashboard flip, commit + PR ready)

**Branch:** `feat/P4-07-weekly-leagues`.

**What shipped (FE-only, Batch 5):**

Minimal dashboard data flip for the `LeaguePreview` section (plan task B5-1 scope: FE-2 + FE-4). No new component promoted to `@learnexia/ui`; no motion or animations (P4-08 owns those).

- **api-client snapshot verified** — `LeaguePreviewDto` with `tierName`, `rank`, `totalPlayers`, `xpThisWeek` fields was already present in `packages/api-client/swagger.json` + `nswag-client.ts` from P2-09. No regen or patch needed.
- **`apps/student-app/app/(child)/index.tsx`** — `LeaguePreviewRow` inline component added (screen-local, not promoted). Replaces the P2-09 TODO comment block. Renders `tierName` (mapped via i18n keys) + rank text when `dashboardQuery.data?.leaguePreview` is non-null. Hidden when null (brand-new student / BE not yet on P4-07).
- **`packages/shared/src/i18n/resources.ts`** — Added 7 new keys under `child.home.*` in both EN and AR locales:
  - `leagueTier.{bronze,silver,gold,diamond}` — maps BE's `LeagueTier.ToString()` strings to localized display names.
  - `leaguePreview.{rankLabel,rankUnknown,a11y}` — rank display + accessibility label.

**Key decisions:**
- **No api-client patch** — `LeaguePreviewDto` shape was already correct in the snapshot.
- **Tier name mapping** — BE sends `LeagueTier.ToString()` = "Bronze"/"Silver"/"Gold"/"Diamond" (D14 in plan). FE maps these lowercase strings to i18n keys; unknown values fall back to the raw string.
- **Null guard** — `leaguePreview` is still rendered conditionally. When BE has not yet shipped the league engine (or brand-new student), the row is hidden. No fallback "Bronze" row (per D13 in plan — sentinel is BE responsibility).
- **No new design** — FE-1 (tier badge primitives), FE-3 (full league screen), FE-5 (RTL pass) are P4-08.

**Test results:** `pnpm` not in PATH (same as all prior batches). Direct `tsc` run shows only pre-existing workspace-resolution errors (all `Cannot find module '@learnexia/*'` + `--jsx` flag issues) — same errors exist on all other unmodified files. No new type errors from the changes.

**Deferred items (P4-08):**
- Medal/tier icons, motion, league screen, promotion/demotion animations.
- RTL-specific polish pass.

---

## P4-06 — Complete daily/weekly missions (Batch 8 — commit + PR ready)


## P4-07 — Weekly leagues (Batches 0-5 — ApplyAward refactor + league engine + endpoint + FE flip, commit + PR ready)

**Branch:** `feat/P4-07-weekly-leagues`.

**What shipped — Phase-3 Gamification sixth story. The first competitive layer. Sixth event-consumer feature in Gamification module, completing the reward economy:**

### Batch 0 — ApplyAward chokepoint refactor (critical predecessor work)

- **StudentXpProfile.ApplyAward expanded to 4-arg signature** — now a single chokepoint for ALL XP additions across all 5 prior sources. Signature: `ApplyAward(int amount, int newLevel, XpReason reason, DateTime occurredAtUtc)`. Raises new `XpAwardedDomainEvent(StudentId, Amount, TotalXpAfter, Reason, OccurredAtUtc)`.
- **RecordBadgeEarned + RecordMissionCompleted now delegate to ApplyAward** — refactored to call ApplyAward instead of mutating TotalXp directly. Ensures event is raised from all XP paths.
- **Semantic change: LastAwardAtUtc uses event timestamp, not wall-clock** — critical for week-boundary correctness when events are replayed/retried.
- **85/85 P4-02..P4-06 regression PASSED post-refactor** — zero assertion updates needed.

### Schema (Batch 1)

- **AddLeagueAndLeagueMembership migration (20260601183834):**
  - Leagues table — cohort aggregator with Tier, PeriodKey, GroupIndex, unique on (Tier, PeriodKey, GroupIndex).
  - LeagueMemberships table — per-student per-week with WeeklyXp, JoinedAtUtc, TierAfter, ParticipantStatus, unique on (PeriodKey, StudentXpProfileId) and (LeagueId, StudentXpProfileId).
  - LeagueXpDeltaLogs table — idempotency ledger with unique on (LeagueMembershipId, OriginEventId).
  - StudentXpProfile.CurrentTier field (LeagueTier int, default Bronze=1).
  - MembershipStatus enum (Active=1, Promoted=2, Demoted=3, Stayed=4).

### Engine (Batches 2-4)

- **LeagueStandings pure static** — ComputeCutoffs(size) + Apply(members, tier). Handles tier extremes + small-cohort scaling (floor(size * 7/30) promote, floor(size * 5/30) demote for size >= 12; 0/0 for size < 5).
- **StudentXpProfile.UpdateTier mutation method** — encapsulates tier change during rollover.
- **LeagueOptions config** — CohortSize=30, PromoteCount=7, DemoteCount=5, PromotionJobCron="15 0 * * 1", TimeZoneId="UTC".
- **14 new IGamificationRepository methods** — GetOrCreateLeagueAsync, GetCurrentLeagueForStudentAsync, CreateLeagueMembershipAsync, IncrementLeagueMembershipXpAsync (with idempotency), GetLeagueStandingsAsync, UpdateLeaguePromotionAsync, GetStudentMembershipsForRolloverAsync, CreateLeagueMembershipsForNextWeekAsync, graph-nav attach methods.
- **LeaguePlacementService (Infrastructure)** — GetOrCreateMembershipAsync: transactional find-or-create cohort + insert membership with graph-nav pattern.
- **IncrementLeagueXpCommand + handler** — narrowed idempotency catch, period key derived from request.OccurredAtUtc (post-review fix for week-boundary correctness), no-op when no membership (lazy placement dashboard-driven).
- **XpAwardedLeagueHandler notification handler** — in own try/catch per ADR 0002 §3, consumes XpAwardedDomainEvent, fans-out to IncrementLeagueXpCommand.
- **IStudentLeagueQuery cross-module seam** with LAZY INSTANTIATION — on null membership, calls LeaguePlacementService to trigger cohort creation on first dashboard read of week.

### Cross-module + API + Dashboard (Batch 5)

- **LeagueTierDto drift enum** in Shared.Contracts — parity-tested (4/4 enum drift unit tests).
- **DashboardDto.LeaguePreview wired** — GetDashboardQueryHandler injects IStudentLeagueQuery, replaces null with real snapshot.
- **GET /api/Gamification/Leagues/Me endpoint** — JWT-only IDOR-proof. Returns MyLeagueResponse: CurrentTier, Rank, TotalPlayers, WeekStart/EndUtc, Standings(30-row cohort), PromotionCutlineRank=7, DemotionCutlineRank=26. DisplayName anonymized to "Student #N" (no PII).
- **LeagueRolloverJob Hangfire** — "15 0 * * 1" UTC Monday 00:15 (after StreakSweep 00:05 + MissionRollover 00:10). For each cohort: rank members, promote top-7, demote bottom-5, update StudentXpProfile.CurrentTier. Idempotent.
- **FE: LeaguePreviewRow component** — dashboard row using leaguePreview data + i18n tier names EN/AR.

### Post-review fixes applied

- **#2 should-fix:** IncrementLeagueXpCommandHandler period-key now from request.OccurredAtUtc (was wall-clock, broke week boundaries). 23/23 tests green.
- **#4 nits:** stale TODO P4-07 comments removed from DashboardProfile.cs, LeaguePreviewDto.cs.

### Lead-approved decisions

- **D1:** ApplyAward 4-arg chokepoint refactor.
- **D2:** Anonymization = "Student #N" (no PII).
- **D3:** Top-7/bottom-5 cutoffs (Duolingo gentler standard).
- **D4:** Endpoint + minimal FE flip bundled; full screen P4-08.
- **D5:** Reuse MissionPeriodCalculator for weekly key.

### Accepted MVP risks

- **R1:** Concurrent placement may overfill cohort by 1 (race window, bounded, unique constraint prevents double-membership).
- **D15:** XP earned before first dashboard load not credited (lazy placement trade-off).
- **JoinOrder collision:** two students could get same display name under concurrent placement (UX flaw, not data corruption).
- **XpAwardedDomainEvent ghost on retry (ADR 0002 §3):** single delivery via IsolatedNotificationPublisher, accepted.

### Test results

- **27/27 LeagueStandings unit** + **4/4 enum drift** = 31/31 unit.
- **23/23 P4-07 integration** (lazy placement, XP increment, idempotency, rankings, tier extremes, endpoint, anon, IDOR, auth).
- **85/85 P4-02..P4-06 regression** (ApplyAward refactor transparent).
- **108/108 full P4 suite ✅**

### Security: PASS (0 blocking, all Info/Low)

### Graph-nav convention (5th instance)

- AttachLeague + AttachLeagueMembership (mirrors Membership pattern).

### Deferred

- **P4-08:** Full league screen, tier badges, motion.
- **P4-09:** Promotion/demotion nudges.
- **P4-10:** Redis hot-path read model.
- **P7-03:** Admin tier override.
- **LeaguePlacementServiceTests.cs:** Service no longer pure-static (depends IGamificationRepository); behavior covered by 23/23 integration tests (T2/T3/T11/T12 lazy placement/concurrent/tier-tracking).

## P4-06 — Complete daily/weekly missions (Batch 8 — commit + PR ready)

**Branch:** `feat/P4-06-missions` (ready for committer).

**What shipped:**

**Phase-3 Gamification fifth story — second periodic-state layer on top of XP/streak/hearts/badges engines. Adds daily (5 templates) + weekly (3 templates) structured replayable goal system with progress tracking, auto-expiry, and reward chaining.**

- **Schema:** `AddMissionDefinitionStudentMissionProgressLog` migration adds three tables to `gamification` schema: `MissionDefinitions` catalog (unique on Code, FullAuditedEntity, 8 seed rows: 5 daily + 3 weekly); `StudentMissions` per-period instance (CASCADE delete from StudentXpProfile, RESTRICT from catalog, unique on (StudentXpProfileId, MissionDefinitionId, PeriodStartUtc)); `MissionProgressLogs` idempotency ledger (CASCADE from StudentMission, unique on (StudentMissionId, OriginEventId)).
- **`XpReason.MissionCompleted = 6`; `MissionTargetType` enum (CompleteLessons, CorrectAnswers, EarnXp, MaintainStreak, CompleteUnit).**
- **`MissionPeriodCalculator`** pure static — UTC-normalized + ISO 8601 week math. Daily key "D:yyyy-MM-dd", weekly key "W:ISOyyyy-WW". 10 unit tests.
- **`IncrementMissionProgressCommand` + handler** — probe → row-lock after → fetch under lock → per-mission idempotency check → ApplyProgress → inline completion (XpAward + RecordMissionCompleted + MarkCompleted) when target reached. Avoids nested-transaction issues from a separate command. Narrowed unique-constraint catches on both progress-log and mission-instance races (F2 fix applied).
- **3 notification handlers** in `Features/Missions/EventHandlers/` — `LessonCompletedMissionHandler` (+1 CompleteLessons, cross-module), `AnswerSubmittedMissionHandler` (+N CorrectAnswers when IsCorrect, cross-module), `StreakAdvancedMissionHandler` (+1 MaintainStreak, in-module). Each in own try/catch per ADR 0002 §3.
- **Cascade chain semantics** — Mission XP bonus can push student past level threshold → `StudentLeveledUpDomainEvent` → `StudentLeveledUpBadgeHandler` (P4-05) may award LEVEL_* badges. Bounded, terminates.
- **Practice Mode counts** — `LessonCompletedMissionHandler` + `AnswerSubmittedMissionHandler` fire regardless of Hearts. `StreakAdvancedDomainEvent` by-construction unreachable in PM (upstream gate), so MaintainStreak missions stay at 0 in PM.
- **`IStudentMissionsQuery` cross-module seam** with **lazy instantiation** — first dashboard read of period creates today's daily + this-week's weekly rows. Narrowed constraint-name catch (F2 fix). Sentinel zero-state for brand-new students.
- **`MissionStatusDto`/`MissionTargetTypeDto`/`MissionTypeDto`** drift-enums in `Shared.Contracts.Gamification` with parity unit test (F3 fix — no domain enum leak on API surface).
- **`DashboardDto`** — old `DailyMission` placeholder removed; `DailyMissions: IReadOnlyList<MissionSummary>?` + `WeeklyMission: MissionSummary?` appended positional, default-valued. Non-breaking.
- **`GET /api/Gamification/Missions/Me`** — JWT-only via `[Authorize]`. Returns `MyMissionsResponse { Daily, Weekly }` with full metadata using DTO enums (F3 fix).
- **`MissionRolloverJob`** Hangfire — `5 0 * * *` daily + `10 0 * * 1` Monday weekly. Bulk ExecuteUpdateAsync. Registered Transient.
- **Graph-nav convention 4th instance** — `AttachStudentMission` + `AttachMissionDefinition` repo methods (mirrors XpAward → HeartLoss → StudentBadge pattern).
- **`MissionSeeder`** idempotent atomic seed of 8 missions (5 daily + 3 weekly) at startup.

**Security follow-ups applied:**
- **F1 (Medium — comment):** Documented row-lock + missions-query scope alignment.
- **F2 (Medium):** Narrowed `EnsureMissionsForPeriodAsync` + `IncrementMissionProgressCommandHandler` catches to specific constraint names only (bare 23505 fallback removed by reviewer).
- **F3 (Medium):** `MissionStateDto` uses `Shared.Contracts` DTO enums (no domain enum leak).
- **F5 (Low):** Row-lock moved AFTER missions probe (no-op contention fix).

**Lead-approved decisions:**
- **D1:** 8-mission MVP (5 daily + 3 weekly).
- **D2:** Lazy instantiation on dashboard read; Hangfire rollover job defensive closeout only.
- **D3:** Practice Mode lesson completions count toward missions.
- **D4:** Dashboard surface = `DailyMissions` list + single `WeeklyMission`.

**Test results:**
- 10/10 MissionPeriodCalculator unit tests + 9/9 MissionEnumDrift unit tests = **19/19**
- **23/23** P4-06 integration tests (catalog seed, brand-new student, lazy instantiation, idempotency on dashboard re-read, /Missions/Me, anonymous 401, lesson→progress, 3 lessons→complete, idempotency, correct→progress, wrong→no progress, streak→MaintainStreak, Practice Mode counts, rollover expires, rollover idempotent, rollover preserves current, IDOR×2, level-up chain, weekly accumulates, enum drift)
- **62/62** P4-02/03/04/05 regression
- **11/11** P2-09 dashboard regression (verifies old `DailyMission` placeholder removal didn't break contract)
- **Full P4 suite: 85/85**

**Deferred items (next stories):**
- P4-07 (leagues), P4-08 (FE mission/badge/league screens + motion), P4-09 (notification nudges), P4-10 (Redis), P4-11 (streak freeze + weekly challenges), P7-03 (admin mission editor).

**In-cycle bug fixed:** Graph-nav 4th instance (`AttachStudentMission` + `AttachMissionDefinition`); inline completion to avoid nested-transaction issues from a separate CompleteMissionCommand.

---


## P4-05 — Earn badges (Batch 8 — commit + PR ready)

**Branch:** `feat/P4-05-earn-badges` (ready for committer).

**What shipped:**

**Phase-3 Gamification fourth story — first consumer of the domain events that P4-02 (XP) + P4-03 (streak) + P4-04 (hearts) shipped. Adds the achievements layer on top of the XP/streak/hearts engines.**

- **10-badge catalog** seeded idempotently at startup: FIRST_LESSON, STREAK_3/7/14/30, LEVEL_5/10/20, LEGENDARY_50, STREAK_100. `BadgeSeeder.SeedAsync` runs in `GamificationModule.InitializeAsync` after migrations in all environments.
- **New schema:** `AddBadgeDefinitionAndStudentBadge` migration adds `BadgeDefinitions` table (catalog; unique on Code, FullAuditedEntity) + `StudentBadges` table (append-only ledger, CreationAuditedEntity, unique constraint `(StudentXpProfileId, BadgeDefinitionId)`, CASCADE delete from profile, RESTRICT from catalog). `XpReason.BadgeEarned = 5`. `BadgeTriggerType` enum (FirstLesson, Streak, Level).
- **`BadgePredicateEvaluator`** pure static service (total function) — matches badge definitions against a trigger type + value, skipping already-earned ones. Mirrors `LazyHeartRefiller` / `StreakDayCalculator` / `LevelCurve` shape. 12 unit tests.
- **`AwardBadgeCommand` + handler** — row-lock + dual-layer idempotency (HasBadgeAsync pre-check + UX_StudentBadges_* unique constraint). Writes ledger row + rarity-scaled XP bonus via `XpAward(Reason=BadgeEarned)`. Narrowed `DbUpdateException` catch for safer error handling. `AttachBadgeDefinition` graph-navigation fix (3rd instance — now documented convention for any new entity navigating an existing untracked aggregate).
- **3 notification handlers** — `LessonCompletedBadgeHandler` (cross-module, Learning event), `StreakAdvancedBadgeHandler` (in-module domain event), `StudentLeveledUpBadgeHandler` (in-module). Each in own try/catch per ADR 0002 §3.
- **Cascade chain semantics** — A badge XP bonus can push the student past a level threshold, raising `StudentLeveledUpDomainEvent`, which awards the LEVEL_* badge in turn. Bounded by `alreadyEarned` filter — terminates in ≤ N badges.
- **Practice Mode by-construction** — STREAK_*/LEVEL_* badges cannot fire in Practice Mode (Hearts=0) because upstream `AdvanceStreakCommandHandler` + `AwardLessonCompletedXpCommandHandler` short-circuit at Hearts=0, never raising domain events. FIRST_LESSON CAN fire in PM since `LessonCompletedIntegrationEvent` fires regardless of Hearts.
- **`IStudentBadgesQuery` cross-module read seam** — sentinel `StudentBadgesSnapshot(0, [])` for brand-new students. `BadgeRarityDto` re-declared in `Shared.Contracts.Gamification` with parity enum drift unit test.
- **`DashboardDto` extended** — `BadgesCount: int` + `RecentBadges: IReadOnlyList<BadgeSummary>?` (positional appended, default-valued — non-breaking). Learning dashboard wiring updated.
- **New endpoint `GET /api/Gamification/Badges/Me`** — JWT-only via `[Authorize]`. Returns all 10 catalog definitions annotated with `IsEarned: bool` + `AwardedAtUtc: DateTime?`. IDOR-proof (no studentId param).

**Security follow-ups applied (security-auditor PASS-with-notes):**
- **F1 (Medium):** `AwardBadgeCommand.OriginEventType` field added for audit-trail forensics. All 3 notification handlers pass the actual triggering event type name.
- **F2 (Medium):** Confirmed `BadgeSeeder` already atomic (single `SaveChangesAsync` at end).
- **F4 (Low):** XML comment on `StudentBadge` corrected (RESTRICT delete, not CASCADE).
- **F5 (Low):** `AwardedAtUtc != default(DateTime)` validator rule added.

**Lead-approved decisions:**
- **D1:** All 10 badges (8 MVP + 2 Legendary stretch). Stretch ones appear locked from day 1.
- **D2:** Both count + recent 3 on `DashboardDto`.
- **D3:** XP bonus scaled by rarity: Common +20, Rare +50, Epic +100, Legendary +250 via `GamificationConstants.XpRewards.ForRarity`.
- **D4:** `GET /api/Gamification/Badges/Me` endpoint shipped (not deferred to FE story).

**Test results:**
- BadgePredicateEvaluator unit tests: 12/12
- BadgeRarityDto enum drift assertion in unit tests
- P4-05 integration tests: 17/17 (catalog seeded, FIRST_LESSON award, idempotency, streak cascade, level up, recent DESC ordering, IDOR × 2, Practice Mode by-construction, badge XP chain, 3-concurrent stress, dashboard envelope, seeder idempotency)
- P4-02/03/04 regression: 45/45 (T3/T4 + P403-T1/T13 + P404-H10 assertions updated for +20 FIRST_LESSON XP)
- Full P4 suite: 62/62
- Full integration suite: 560/568 (only 8 pre-existing failures unchanged — P2-02 TC-1, P2-04 TC-09, P2-09 C11, AC-DEF-2, AC-RL-6, AC-4 WeakPassword, AC-2c, TC-1/ForGrade)

**New conventions to carry forward:**
- **`[NotificationHandler<TDomainEvent>] → [mediator.Send(Command)] → [UoW commits]` pattern** — generalizes for ANY in-module domain event consumer. Future stories (e.g. P4-08+) can mirror this shape.
- **Graph-navigation attach pattern** — now the third instance (XpAward → HeartLoss → StudentBadge). Established as documented convention: always `_repo.AttachEntity(existing)` before `Entity.Create()` when an entity navigates an existing untracked aggregate to prevent EF duplicate-INSERT.

**Deferred items (next stories):**
- P4-06 (missions), P4-07 (leagues), P4-08 (FE badge pop-in + collection screen), P4-09 (BadgeEarned nudge consumer), P4-10 (Redis), P7-03 (admin badge catalog editor).

---


## P4-04 — Hearts + Practice Mode (Batch 3b FE — dashboard data flip)

**Branch:** `feat/P4-04-hearts-practice-mode` (current — ready for reviewer/committer).

**What changed:**

- **swagger.json** — manually updated committed snapshot: `DashboardDto` now includes `level` (int, from P4-02 BE — was missing from snapshot), `hearts` (int, P4-04), `inPracticeMode` (bool, P4-04). Regen was blocked (no pnpm/nswag runtime in CI shell); manually patched as documented fallback.
- **`nswag-client.ts`** — manually added `level?: number`, `hearts?: number`, `inPracticeMode?: boolean` to `DashboardDto` interface (only these 3 fields added; rest of file untouched).
- **`DashboardHeader`** (`packages/ui`) — 3 new optional props: `inPracticeMode`, `practiceModeLabel`, `practiceModeAccessibilityLabel`. Inline pill: `$warningSoft` bg / `$warning` text, `borderRadius={9999}`, rendered between Hearts and StreakFlame when `inPracticeMode && practiceModeLabel`. No animation.
- **`apps/student-app/app/(child)/index.tsx`** — `hearts={3}` replaced with `dashboardQuery.data?.hearts ?? 5`; `weeklyLevel={1}` replaced with `dashboardQuery.data?.level ?? 1`; `inPracticeMode`/`practiceModeLabel`/`practiceModeAccessibilityLabel` props wired. `statsA11y` hearts value also wired to real data.
- **`packages/shared/src/i18n/resources.ts`** — added `child.home.practiceMode` + `child.home.practiceModeA11y` in both EN and AR.

**Key decisions:**
- **Fallback `?? 5` for hearts** — BE contract is non-null int with default 5 (cap). The `?? 5` handles the TS optional typing from nswag `markOptionalProperties: true`.
- **Fallback `?? false` for inPracticeMode** — same reason.
- **`$warningSoft` / `$warning` tokens** — existing semantic tokens used by MissionBanner and other components. Matches the amber/yellow design intent without introducing new tokens.
- **Pill is inline in `DashboardHeader`** — not promoted to a new primitive (scope tight per task instructions).
- **No regen via nswag** — nswag runtime requires .NET 9 installed and pnpm installed; neither available in CI shell. Manual edit of swagger.json snapshot + nswag-client.ts documented and scoped to exactly the 3 new fields.

**Important for next regen:** When the backend next emits swagger (via `refresh:swagger`), the snapshot will include `level`/`hearts`/`inPracticeMode`. Running `gen:api` will regenerate the full file from scratch (overwriting the manual edits). The hand-added JSDoc comments in `nswag-client.ts` will be lost but the fields themselves will be present from the swagger.

**Not in scope (P4-08):**
- Hearts animation / shake on depletion
- Regeneration countdown timer
- "Out of hearts" bottom sheet

---

## Wave 13 — Phase 2 FE closer: student home dashboard (P2-09-FE, ready for PR)

**Branch:** `feat/W13-P2-09-FE` (based off `feat/W12-P2-05-06-07-FE`, PR pending).

**What's on the branch:**
- **BE annotations** — `DashboardController.Get` + `StudentsController.studentAttempts(studentId)` got `[ProducesResponseType(typeof(BaseResponse<TDto>), 200)]`. Behavior unchanged; NSwag now emits typed clients.
- **api-client regenerated** — `dashboard()` returns `DashboardDtoBaseResponse`; `attempts(studentId)` returns `AttemptListItemDtoListBaseResponse`. New type re-exports: `DashboardDto`, `ContinueTargetDto`, `DailyMissionDto`, `LeaguePreviewDto`.
- **1 new `@learnexia/api-client` hook**: `useDashboard()` — single endpoint, BE composes Continue/streak/XP/etc. server-side (we don't compose client-side).
- **3 new `@learnexia/ui` primitives**:
  - `DashboardHeader` — greeting + grade caption + stats strip (Hearts/StreakFlame/XPBar). `childName` is informational-only (optional); `greetingText` is the rendered string the caller composes.
  - `ContinueCard` — tap-to-resume; renders subject icon + lesson title + chevron CTA; logical `end={14}` boss badge; hidden when `continue=null`.
  - `MissionBanner` — built but **never rendered in Phase 2** (`dashboardQuery.data.dailyMission` is always `null`; Phase 4 wires it).
- **`SubjectsListSection`** extracted from W11 `(child)/index.tsx` into `apps/student-app/app/(child)/_components/SubjectsListSection.tsx` (W11 logic intact: defensive 4-subject filter, shimmer/error/empty, RTL). Helper moved to `(child)/_components/subjects.ts`.
- **`apps/student-app/app/(child)/index.tsx`** rewritten as dashboard composition: TopBar → DashboardHeader → ContinueCard (conditional) → SubjectsListSection. Hearts fixed `3` (TODO P4-04); streak/XP default `0` (TODO P4-02/03). Loading state composes `meQuery.isLoading || dashboardQuery.isLoading` for both header AND subjects section.
- **i18n** — expanded `child.home.*` namespace with 18 new EN+AR keys (greeting, gradeCaption, continueTitle, continueCta, yourSubjects, welcomeEmpty, errorRetry, statsA11y, etc.). No fork — extends existing namespace.
- **Reviewer FAIL → fixes applied** — 3 blockers cleared: dropped dead required `childName` from `DashboardHeader` (now optional), replaced ContinueCard physical `right`/`left` with logical `end={14}`, added Wave 13 section to HANDOFF (this).

**Key decisions:**
- **Single dashboard endpoint, no client-side composition.** BE resolves Continue (most-recent-attempt → engine → first Available lesson → cross-subject fallback). Avoids client-side races + duplicate heuristics.
- **All Phase-4 features stub-only** (hearts decrement / streak increment / XP / mission / league) — display surfaces, no endpoint calls, TODO comments with story IDs.
- **`MissionBanner` built but not rendered** — Phase 4 will mount it when BE returns non-null `dailyMission`.
- **`SubjectsListSection`** is a new local component (not promoted to `@learnexia/ui`) since only one consumer.

**Non-blocking follow-ups** (chore PR / next wave):
- `ContinueCard` could swap `Pressable` → Tamagui `Stack` w/ `hoverStyle`/`pressStyle` for web hover lift (mirroring `LessonCard` pattern).
- Stats strip `accessibilityRole="summary"` should be `"group"` via `Platform.OS === 'web' ?` gate (W12 carry-forward).
- Dashboard mount fade-in (240ms `opacity 0→1`, reduced-motion gated) per design spec §5.
- Append boss suffix to `continueA11y` when `continueTarget.isBoss`.
- Consolidate `SubjectKey` type (duplicated in `SubjectRow` + `ContinueCard`).
- `useDashboard` invalidation seam on `LessonCompletedIntegrationEvent` will be wired in P4-02 wave when XP/streak/hearts go live.

---

## Wave 12 — Phase 2 FE lesson + quiz + feedback (P2-05/06/07-FE, ready for PR)

**Branch:** `feat/W12-P2-05-06-07-FE` (based off `feat/W11-P2-02-P2-03-FE`, PR pending).

**What's on the branch:**
- **BE annotations** — `LessonsController` + `QuizzesController` got `[ProducesResponseType(typeof(BaseResponse<TDto>), 200)]` on the 5 student-facing endpoints (single-lesson GET, Attempt, Answers, Complete, Abandon). Behavior unchanged; NSwag now emits typed clients (was `Promise<void>`).
- **api-client regenerated** — new typed methods `lessonsGET(id)`, `attempt(lessonId)`, `answers(attemptId,body)`, `complete(attemptId)`, `abandon(attemptId)`. New types: `SingleLessonResponse`, `StartAttemptResponse`, `SubmitAnswerCommand`, `SubmitAnswerResponse`, `AttemptSummaryDto`, `QuestionType` enum.
- **5 new `@learnexia/api-client` hooks**: `useLesson(lessonId)`, `useStartAttempt()`, `useSubmitAnswer(attemptId)`, `useCompleteAttempt()`, `useAbandonAttempt()`. Extended `queryKeys.learning.lesson(id)` + `learning.dashboard()` (forward-compat for W13).
- **8 new `@learnexia/ui` primitives**: `QuestionCard`, `MCQOption`, `TrueFalseChoice`, `FillInBlank`, `MatchingPanel` (stub — BE has no Matching seed), `AnswerFeedbackStrip` (alert + live region), `AttemptSummaryCard`, `ProgressDots` (progressbar role).
- **Lesson Player** at `apps/student-app/app/(child)/lessons/[lessonId].tsx` — single route, 3-stage state machine (`intro → quiz → summary`):
  - **Intro**: `useLesson(lessonId)`, hearts widget (fixed 3 — Wave 3 wires decrement), Start CTA.
  - **Quiz**: `useStartAttempt` on Start; one question at a time via plain `switch(questionType)` (NOT Strategy); locked-after-submit; correct → 800ms auto-advance, incorrect → "Next" CTA.
  - **Summary**: `useCompleteAttempt` on last advance; `AttemptSummaryCard` with score/accuracy/duration + "+10 XP" stub (TODO P4-02 — no XP endpoint). "Back to subject" navigates to `/(child)/subjects/{?subjectId}`; "Try again" re-fires `useStartAttempt`.
  - Abandon called fire-and-forget on unmount mid-quiz (idempotent).
  - Hint button visible-disabled with "Hint coming in v2" helper (TODO P3-05 — no hint endpoint).
- **Navigation seam** — `apps/student-app/app/(child)/subjects/[subjectId]/index.tsx` now passes `?subjectId=` on lesson tap-Available so Summary can route back cleanly.
- **i18n** — added 37 EN+AR keys under `child.lessons.intro.*`, `child.quiz.*`, `child.feedback.*`, `child.summary.*`, `child.lessons.a11y.*`. Deleted obsolete `child.lessons.stub.*`.
- **Reviewer PASS** → `docs/briefs/W12-P2-05-06-07-FE-review.md`. 0 blockers. Polish applied inline: removed dead constants/variables (nit-1, nit-2), added `maxWidth=720` + centering to all 3 stages (should-fix #2). Carry-forward: reduced-motion gate (should-fix #1) and a11y region/group roles (nits 3+4 — RN's `AccessibilityRole` union doesn't include those web ARIA values; defer to a web-only polish PR).

**Key decisions:**
- **Switch-on-questionType, not Strategy** — plain JSX switch in render. Adheres to rule #8.
- **Single-route view-state machine** over multi-route (cleaner back-stack; spec recommended).
- **Hearts/XP/Hint slots are display-only** — Wave 3 (Gamification) and Wave 4 (AI Tutor) own the real wiring.
- **MatchingPanel = stub** because BE has zero Matching questions seeded (P2-08 brief).
- **Abandon = fire-and-forget mutation** (BE is idempotent on terminal).

**Non-blocking follow-ups** (chore PR):
- Wire `AccessibilityInfo.isReduceMotionEnabled()` into `AnswerFeedbackStrip` translate + lesson screen 1200ms timer (currently always 800ms).
- `AttemptSummaryCard` + `QuestionCard` web ARIA roles (`region`/`group`) need a web-only Platform.OS gate or a custom `aria-*` prop bypass since RN's TS union rejects them.
- Replace `xpStub` "+10 XP" when Wave 3 XP service lands.
- Implement real Matching renderer when BE seeds Matching questions.
- Confetti / mascot illustration on Summary (deferred to W14 polish).
- Markdown-rendering in question stem (currently plain text).

---

## Wave 11 — Phase 2 FE student-facing browse (P2-02-FE + P2-03-FE, ready for PR)

**Branch:** `feat/W11-P2-02-P2-03-FE` (off main, PR pending).

**What's on the branch:**
- **BE `MeResponse.Grade : int?`** — Identity `MeResponse` DTO + `GetMeQueryHandler` populate `Grade` from `User.Grade` (already on the entity). 2 new integration tests in `P1_09_Me_Tests.cs` (child Grade returned, parent null). All 18 P1-09 tests green.
- **BE `[ProducesResponseType]` on `SubjectsController`** — the 3 student-facing endpoints (`ForGrade`, `{id}/Lessons`, `{id}/SkillTree`) gained `[ProducesResponseType(typeof(BaseResponse<List<...Dto>>), 200)]` so NSwag emits typed clients (previously `Promise<void>`). Pattern matches Identity's `UsersController.Me`.
- **api-client regenerated** — new methods `forGrade`, `lessons`, `skillTree`; new types `StudentSubjectDto`, `UnitWithLessonsDto`, `LessonInUnitDto`, `ConceptNodeDto`, `SkillNodeDto`, `MissingPrerequisiteDto`, `NodeState` enum (int: 0=Locked, 1=Available, 2=Completed); `MeResponse.grade?: number`.
- **3 new `@learnexia/api-client` hooks**: `useSubjectsForGrade(grade)`, `useSubjectLessons(subjectId)`, `useSubjectSkillTree(subjectId)`. New `queryKeys.learning.*` namespace.
- **4 new `@learnexia/ui` primitives** + 1 Badge variant:
  - `SubjectRow` — student-facing subject card.
  - `LessonCard` — vertical card; state pill via `NodeState`; logical `end={14}` lock + Boss badges.
  - `SkillTreeNode` — 72px disc + state visuals + `hasMissingPrereqs` + `isBoss` overlay.
  - `Badge variant="boss"` — 👑 Boss pill.
  - `SegmentedTabs` — horizontal segmented control (sibling of `Tabs`).
- **New tokens** in `colors.ts`: per-subject tint + 3 glow shadow tokens.
- **i18n** — EN + AR under `child.subjects.*`, `child.skillTree.*`, `child.lessons.stub.*`.
- **Student-app screens:**
  - `(child)/index.tsx` — Subjects list (grade from `useMe`, defensive 4-subject filter, shimmer skeletons gated on `meQuery.isLoading || subjectsQuery.isLoading` so no empty-state flash).
  - `(child)/subjects/[subjectId]/_layout.tsx` + `index.tsx` (Lessons) + `tree.tsx` (Skill Tree) — `SegmentedTabs` shell + Unit-grouped lessons + concept-grouped skill nodes. In-memory boss derivation by joining lessons + tree on `skillId`.
  - `(child)/lessons/[lessonId].tsx` — STUB (Wave 12 replaces).
  - `(child)/_components/WhyLockedSheet.tsx` — inline (NOT in `@learnexia/ui`); web modal / native bottom sheet; tokens via `colors` import.
- **Reviewer PASS after fixes** → `docs/briefs/W11-P2-02-P2-03-FE-review.md`. Fixed: 3 raw-hex/physical-position blockers (`WhyLockedSheet` CTA + overlay + card bg via tokens; `LessonCard` `end={14}` logical pos), should-fix Me loading flash, should-fix RTL chevron in lesson stub.

**Key decisions:**
- `SegmentedTabs` shipped as a **sibling primitive** to `Tabs` (not a refactor) per design spec "smaller diff" guidance.
- Boss derivation is **in-memory** (not BE join) — both queries already fire on the screen.
- Lesson screen is a stub until Wave 12.
- No `api-tester`/`security-auditor` (no new BE endpoints with new risk surface; covered by existing P1-09 + P2-02 BE tests).

**Non-blocking follow-ups** (chore PR):
- `SkillTreeNode` still has 3 raw-hex disc colors + shadow strings (tokens added but not yet wired). Wire next pass.
- `WhyLockedSheet.lockedItemName` prop declared but not rendered.
- Native pulse animation on `SkillTreeNode` Available state = web-only CSS keyframe (no native pulse this wave).
- `useSubjectsForGrade` empty-state copy when BE returns zero subjects for a valid grade (currently identical to no-grade state).

---

## Wave 10 — Phase 2 FE start (P2-12-FE, merged via PR #69)

---

## Wave 10 (BE track) — Phase 3 Gamification kickoff (P4-02-BE, merged via PR #73)

### P4-02 — Earn XP and level up ✅ Merged via PR #73

**What's on main (PR #73):**

**Phase 3 Gamification kickoff — waking up the Gamification module skeleton and landing the first real business feature: XP engine + ledger + level computation.**

- **Module wake-up** ✅ Added 4 Gamification csproj (Domain/Application/Infrastructure/Api) to `Learnexia.Modular.sln` + `Modules\Gamification` solution folder. Added `using Learnexia.Modules.Gamification.Api` + `builder.Services.AddGamificationModule(builder.Configuration)` to `Program.cs`. Added Gamification's `AssemblyReference` to the cross-module MediatR scan in `AddCrossModuleMediatR()`.

- **New `gamification` schema** ✅ `StudentXpProfiles` table: `Id (int)`, `StudentId (int, unique)`, `XpTotal (int, default 0)`, `Level (int, default 1)`, `UpdatedAt (DateTime)` + FullAuditedEntity columns. `XpAwards` table (append-only ledger): `Id (int)`, `StudentId (int)`, `Amount (int)`, `Reason (XpReason enum, int)`, `OriginEventId (uuid)`, `OriginLessonId (int?, nullable)`, `OriginSkillId (int?, nullable)` + FullAuditedEntity columns. Migration `20260530042656_InitGamification`. **Idempotency at DB layer:** unique index `UX_XpAwards_OriginEventId_Reason` on `(OriginEventId, Reason)` — prevents double-award for duplicate event delivery.

- **XP rules (lead-approved SRS examples)** ✅ `GamificationConstants.XpRewards`: `CorrectAnswer = 10`, `LessonCompleted = 50`, `QuizCompleted = 20` (stub — no quiz boundary yet), `StreakBonus = 30` (stub — P4-03 owns streak engine). Stored in static class at `Domain/Constants/GamificationConstants.cs`.

- **Level curve (lead-approved table-based ramp)** ✅ `LevelCurve` pure static service at `Domain/Services/LevelCurve.cs`. Table: `[0, 100, 250, 500, 1000, 2000, 4000, 7000, 11000, 16000]` cumulative XP thresholds for L1–L10. L11+ formula: `10 + ((xp - 16000) / 5000)` (floor). 32 unit tests in `LevelCurveTests.cs`. Testable in isolation — no DB access.

- **Integration-event handlers** ✅ `LessonCompletedIntegrationEventHandler` + `AnswerSubmittedIntegrationEventHandler` at `Application/IntegrationEventHandlers/`. Both subscribe to cross-module events from Learning (P2-07 producers) via `INotificationHandler<T>`. Each handler sends an internal `ICommand` via `IMediator` (Pattern A — runs through Gamification's `UnitOfWorkBehavior` for clean commit boundary and audit stamping). Idempotency: pre-check + catch on unique-constraint violation (AC4).

- **New `GET /api/Gamification/Profile`** ✅ JWT-only endpoint (no studentId param; IDOR-proof by construction). Returns `StudentProfileDto { XpTotal: int, Level: int, XpToNextLevel: int }`. Fresh students (no `StudentXpProfile` row yet) see clean L1 + 0 XP, not 404.

- **`IStudentXpQuery` cross-module read seam** ✅ Defined in `Shared.Contracts/Gamification/IStudentXpQuery.cs` (returns `StudentXpSnapshot? { XpTotal: int, Level: int }`). Implemented in `Gamification.Infrastructure/Queries/StudentXpQuery.cs` against `GamificationDbContext`. Learning's `GetDashboardQueryHandler` now injects `IStudentXpQuery` and reads real XP + Level instead of the P2-09 zero-state placeholders `(Xp: 0, Streak: 0)`. Brand-new students still see `(0, 1)` via null mapping. **New field:** `DashboardDto.Level : int = 1` added to positional record (appended last, maintains compat).

- **Cross-module UoW assembly-filter guard (bug fix)** ✅ **Critical fix discovered during P4-02 implementation.** All 4 module `UnitOfWorkBehavior` implementations (Identity/Learning/Parent/Gamification) now early-return if the command's assembly isn't theirs. **Without this guard, nested `mediator.Send` across modules causes `BeginTransaction on already-in-transaction` failures.** This latent bug was never triggered before P4-02 because no cross-module command dispatch existed. Applied retroactively to Identity, Learning, and Parent modules in this PR.

- **Security follow-ups (per security-auditor PASS)** ✅ Applied 3 findings from this PR's security audit:
  - **F1 (Medium):** Row-lock strategy changed from `FOR UPDATE SKIP LOCKED` to `FOR UPDATE` (block-and-wait prevents lost-update race on `StudentXpProfile.XpTotal`).
  - **F2 (Medium):** Removed child accuracy% from Info logs (child-privacy minimization).
  - **F3 (Low):** Removed dead `CorrectAnswerCount` field from `AwardLessonCompletedXpCommand`.

**Test results:**
- LevelCurve unit tests: **32/32** ✅
- P4-02 integration tests: **16/16** ✅ (T1 correct-answer award, T2 wrong-answer no-award, T3 100% lesson, T4 50% lesson, T5/T6 idempotency, T7/T8 level-up, T9 zero-state, T10 real values, T11 IDOR, T11b sibling-handler isolation, T12 dashboard real XP, T13 dashboard zero-state, envelope + auth sanity)
- Full integration suite: **517/520** (only 3 pre-existing failures, same as `main`: P2-02 TC-1, P2-04 TC-09, P2-09 C11)

**Key decisions locked (all lead-approved):**
- **Q1:** NEW Gamification module — wake up the existing skeleton (approved to add to `.sln` + DI + MediatR).
- **Q2:** `IStudentXpQuery` via `Shared.Contracts/Gamification/` — mirrors `IParentChildQuery` pattern. Learning injects it; future P4-10 swaps implementation for Redis without changing dashboard handler.
- **Q3:** XP values from SRS FR-GM-1 examples: `+10/+50/+20/+30`; table-based level curve approved (L1–L10 via table, L11+ formula).
- **Q4:** Ship `GET /api/Gamification/Profile` endpoint.
- **Q5:** Pattern A — notification handler → `ICommand` → UoW (decoupled from producer's UoW).
- **Q6:** Add `Level` to `DashboardDto` positional record.
- **Q7:** `SELECT ... FOR UPDATE` row-lock on `StudentXpProfile` in command handler.
- **Q3.bis:** `LessonCompleted` XP fires unconditionally on completion (regardless of correct-answer count).

**New conventions to carry forward:**
- **UoW assembly-filter guard is now mandatory** for all modules' `UnitOfWorkBehavior`. Future modules must early-return if the command assembly doesn't match theirs — prevents cross-module transaction interference.
- **Cross-module event handler pattern:** send an `ICommand` via `IMediator` (Pattern A), not direct DbContext writes (decouples commits, enables audit stamping and domain-event dispatch).

**Not in scope (next stories):**
- Streak (P4-03), Hearts (P4-04), Badges (P4-05), Missions (P4-06), Leagues (P4-07).
- XP bar UI animations / confetti (P4-08).
- Redis hot-path read model (P4-10).
- Frontend dashboard render of `Level` field (folded into P2-09-FE or separate FE story).

**Pre-existing test failures (tracked separately, not regressions):**
- P2-02 TC-1, P2-04 TC-09, P2-09 C11 — logged; not blocking Phase 3.

### P4-03 — Maintain a daily streak ✅ Batches 1–7 complete, open as PR #75

**What's on branch `feat/P4-03-daily-streak` (ready for PR):**

- **Schema:** `AddStreakColumns` migration adds `CurrentStreak`, `LongestStreak`, `LastActivityDateUtc : DateOnly?` to `gamification.StudentXpProfiles`. Migration timestamp `20260530091454`.
- **`ISystemClock` abstraction** in `Shared.Kernel/Abstractions/` (universal date-testability primitive; UTC impl `SystemClock` in Gamification.Infrastructure).
- **`StreakDayCalculator`** pure static service with `Transition` enum (`NoOp | FirstActivity | Advance | Reset | OutOfOrder`) — total function, no exceptions. `Classify(lastActivityDate, today)` is the single source of truth for the day-boundary decision.
- **Domain mutation methods** on `StudentXpProfile`: `AdvanceStreak(today)` + `ResetStreakAndStart(today)`. Streak setters narrowed to `internal set`.
- **`AdvanceStreakCommand` + handler** — handler calls `StreakDayCalculator.Classify` and switches on `Transition`. Idempotency via `HasXpAwardAsync` pre-check + narrowed `DbUpdateException when constraintName` catch (F2 fix).
- **`LessonCompletedIntegrationEventHandler` extended** — `AwardLessonCompletedXpCommand` and `AdvanceStreakCommand` each sent in their own try/catch (failure isolation per ADR 0002 §3).
- **StreakBonus +30 XP** rides via existing `XpAward` ledger with `Reason = XpReason.StreakBonus = 4`. Same `UX_XpAwards_OriginEventId_Reason` unique index covers idempotency.
- **`StreakSweepJob`** Hangfire recurring at `5 0 * * *` UTC (00:05 daily UTC). Bulk `ExecuteUpdateAsync` resets `CurrentStreak=0` for `LastActivityDateUtc < today - 1 day`. Registered Transient, uses `IServiceScopeFactory.CreateAsyncScope` for fresh DbContext per run. **Does NOT raise `StreakBrokenDomainEvent`** — bypass of EF change tracker is intentional, deferred to P4-09.
- **`IStudentStreakQuery` cross-module seam** in `Shared.Contracts/Gamification/` (mirrors P4-02's `IStudentXpQuery`). Returns `StudentStreakSnapshot(CurrentStreak, LongestStreak, LastActivityDateUtc)` — no StudentId field (F8 cleanup applied from start).
- **Learning dashboard wiring**: `GetDashboardQueryHandler` injects `IStudentStreakQuery`, dashboard `Streak` field now real. Brand-new students still see `Streak=0` via null mapping.
- **`StreakOptions` config** (`Gamification:Streak` in appsettings) with `TimeZoneId="UTC"` + `DailyJobCron="5 0 * * *"`. TZ-aware calculator means future per-user TZ is a config swap.

**Test results:**
- `StreakDayCalculatorTests`: 13/13 unit tests
- `P4_03_DailyStreak_Tests`: 15/15 integration tests (advance / reset / same-day no-op / idempotency / sweep job / dashboard wiring / cross-student isolation / AnswerSubmitted no-advance)
- `P4_02_EarnXpAndLevelUp_Tests` regression: 16/16 (T3/T4 updated to include +30 StreakBonus in expected totals — correct behavioral change)
- Full integration suite: **532/535** (3 pre-existing failures unchanged)

**Lead-approved decisions:**
- **D1:** Day-boundary = **UTC** (Identity has no TimeZoneId yet; defer per-user TZ).
- **D2:** Activity trigger = **lesson completion only** (`AnswerSubmittedIntegrationEvent` is XP-only, doesn't touch streak).
- **D3:** StreakBonus +30 XP fires **every day the streak advances** (including day-1 brand-new and post-reset day-1).
- **D4:** Sweep job **ships in P4-03** — handler is source of truth (lazy advance/reset on next activity); Hangfire is defensive observability.

**Security follow-ups applied in this PR:**
- **F1 (Medium):** `AdvanceStreakCommandHandler` now calls `StreakDayCalculator.Classify` and switches on `Transition` (was inline if/else duplicating the calculator's logic). Calculator is now total via new `OutOfOrder` transition.
- **F2 (Medium):** `catch (DbUpdateException)` narrowed via `when` clause checking constraint name — unrelated DB errors no longer silently swallowed.
- **F3 (Low):** `StreakSweepJob` registration changed Scoped → Transient.

**Not in scope (future stories):**
- Streak freeze / weekly challenges → P4-11
- `StreakBrokenDomainEvent` consumer + sweep-time domain dispatch → P4-09
- Redis hot-path read model → P4-10
- Per-user TZ (requires Identity schema change) → no story yet
- Hearts (P4-04), Badges (P4-05), Missions (P4-06), Leagues (P4-07)
- Gamification UI motion (P4-08), Re-engagement notifications (P4-09)

---

## Wave 10 (FE track) — Phase 2 FE start (P2-12-FE, merged via PR #69)

### P2-12-FE — Parent Settings tabs (Notifications / Linked children / Security / Plan)

**Branch:** `feat/W10-P2-12-FE-settings-tabs` — merged to main.

**What's on the branch:**
- **`Switch` primitive** added to `@learnexia/ui` — 44×24 track + 20px thumb, on=`$primary` w/ `$primaryGlow`, off=`$cardSoft`, thumb=`$fg1`, 160ms `cubic-bezier(0.16,1,0.3,1)`, logical-RTL thumb via `insetInlineStart`, `accessibilityRole="switch"` + `accessibilityState={checked,disabled}`, 44px min touch target, focus outline 2px `$primary`. Mirrors `CheckboxField` prop shape.
- **8 new `@learnexia/api-client` hooks** + new `queryKeys`: `useNotificationPreferences`, `useUpdateNotificationPreferences` (optimistic w/ rollback), `useUpdateChild`, `useUnlinkChild`, `useChangePassword` (targets `/api/Users/Account/ChangePassword` — NOT the stale admin `changePasswordForUser`), `useMySessions`, `useSignOutOtherSessions` (invalidates sessions), `useMyPlan`.
- **api-client regenerated** against running BE — `myChildren` route moved to `/api/Parent/My-Children` (the legacy `/api/Users/Parent/*` shape is gone). All P2-12 endpoints present.
- **4 Settings panels** under `apps/student-app/app/(parent)/_components/settings/`:
  - `NotificationsPanel.tsx` — 4-row × 2-toggle (Email/Push) grid for the 4 BE categories (WeeklyReport / StreakAtRisk / ProductAnnouncement / Achievement). Optimistic toggle with rollback. Full-array PUT body (BE validator requires all 4 categories distinct).
  - `LinkedChildrenPanel.tsx` — `ChildCard` per child + inline Edit form (fullName/grade/language/country) + **inline Unlink confirm strip** (NOT a Dialog, per rule #8). Add Child CTA → `/(onboarding)/add-child`. Empty state when no children.
  - `SecurityPanel.tsx` — Change-password form (current/new/confirm + `PasswordStrengthMeter`, `forceLtr`, correct `autoComplete` attrs) + Sessions list (truncated 8-char id in `dir="ltr"`, locale-formatted `expiresAt`, Active/Expired pill) + Sign-out-others CTA (success strip counts other sessions captured pre-mutation).
  - `PlanPanel.tsx` — read-only plan name + status badge; "Manage subscription" disabled with `TODO(P2-12-PAYMENTS)` until a payments BE lands.
- **i18n** — every new copy slot keyed in EN + AR under `parent.settings.{notifications,linkedChildren,security,billing}.*`.
- **`SettingsWeb.tsx`** — `renderActivePanel()` switch replaces the 4 `ComingSoonPanel` stubs; Profile + Language untouched.
- **Security audit** ✅ PASS-WITH-FOLLOWUPS — `docs/briefs/W10-P2-12-FE-security-audit.md`. 0 Critical/High. Fixed inline: F-01 (i18n key for "No active sessions"), F-02 (`refetch()` → `invalidateQueries`), F-04 (stale `sessions.length - 1` count captured pre-mutation). Carry-forward: F-03 (missing `Stack.Screen name="settings"` in `(parent)/_layout.tsx` — pre-existing gap), F-04 (toolchain `tar` advisory — not bundled to runtime).
- **Reviewer** ✅ PASS conditional — `docs/briefs/W10-P2-12-FE-review.md`. All blockers (i18n, security gate, HANDOFF) cleared. Build/type-check/lint clean across `@learnexia/{api-client,ui,shared}` + `student-app`.

**Key decisions:**
- **No Dialog primitive** — Unlink uses inline confirm strip inside `ChildCard` per rule #8 (no design-pattern unilateral additions).
- **No `Badge` variant extension** — plan/session status pills are inline `Stack`+`Text` w/ same tokens (`$successSoft`/`$success`, `$dangerSoft`/`$danger`, `$cardSoft`/`$fg3`) since `Badge` only ships achievement-disc variants today.
- **No payments integration** — Plan tab is read-only; Manage CTA disabled.
- **Edit-child form opens with empty grade/language/country** because `LinkedChildResponse` only exposes `{id, fullName, email}` — the BE seam doesn't return grade/language/country on parent's My-Children list (carry-forward to BE if product wants pre-fill).
- **Sessions list shows truncated id only** — BE `SessionInfo` has no device/IP/UA metadata. Carry-forward if richer audit UI needed (P6-06).
- **Brand new Switch primitive added directly on this branch** (rather than cherry-picking from the un-merged `feat/design-system-pixel-align`).

**Non-blocking follow-ups** (recorded above; route to a chore PR):
- F-03: declare `<Stack.Screen name="settings" />` in `(parent)/_layout.tsx` (pre-existing gap, not introduced by W10).
- F-04: track `tar` upgrade via `expo` release cadence (toolchain only, not bundled).
- Extract panel `PanelSurface`/`PanelHeader` to `settings/shared.tsx` when convenient (currently duplicated across 4 panels + `SettingsWeb`).
- `Switch.hideLabel` uses `opacity: 0` (keeps label in layout flow); design spec suggested `clip` — fine for now since Notifications never passes `hideLabel`.

---

## Wave 9 — Phase 2 backend (in progress)

### P2-03 — Navigate the skill tree (boss flag) ✅ Batches 1–3 complete, PR pending

**What's on branch `feat/P2-03-navigate-skill-tree` (ready for PR):**
- **Schema** ✅ `Lesson.IsBoss : bool` non-nullable with `defaultValue: false`. Migration `20260529231653_AddLessonIsBoss` in `learning` schema (single `AddColumn` op).
- **`LearningSeeder.MarkBossLessonsAsync`** ✅ called from `SeedAsync` after `SeedDemoLessonContentAsync`. Marks the highest-`SequenceOrder` lesson in each Unit as boss (one per Unit). Idempotent + drift-prevention (also resets `IsBoss = false` if the wrong lesson got marked). 66 boss rows / 162 total / 66 units (one per unit, confirmed in tests).
- **3 DTOs extended** ✅ `LessonInUnitDto.IsBoss` (`{ get; init; }`), `SingleLessonResponse.IsBoss` (`{ get; set; }` matching parent `LessonDto` style), `ContinueTargetDto.IsBoss` (positional record member, appended last).
- **2 handlers populate `IsBoss`** ✅ `GetSubjectLessonsQueryHandler` in 3 construction sites (authenticated happy path, authenticated defensive fallback, anonymous fallback); `GetDashboardQueryHandler` in `TryResolveContinueForSubjectAsync`.
- **AutoMapper profiles** — verified: `Lesson → SingleLessonResponse` flows `IsBoss` by-name (no `ForMember` needed); `LessonInUnitDto` and `ContinueTargetDto` are hand-projected.
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_03_SkillTreeBoss_Tests.cs` — 5 cases: seeder boss-count == unit-count, Math G1 Lessons endpoint per-unit boss invariant, boss-lesson GET returns `isBoss=true`, non-boss GET returns `false`, seeder idempotency. **One-line edit** to `P2_09_HomeDashboard_Tests.cs` C03 — asserts `continue.isBoss == false` for the fresh-student case (root lesson is `SequenceOrder=1`, not a boss). **Full Wave-7+8+9 regression: 87/87 PASS** (~3m, Testcontainers Postgres pg16).

**Key decisions:**
- **Q1 → `Lesson.IsBoss` (NOT `Skill.IsBoss`)** — story says "end-of-unit challenge"; units own lessons.
- **Q4 — `NodeState` enum unchanged** at Locked/Available/Completed. Boss is orthogonal (a boss lesson can be in any of the 3 states).
- **Q3 — Seeder rule:** highest-`SequenceOrder` lesson per Unit.
- **Q8 — Skip `HasBoss` rollup** on `SkillNodeDto`/`ConceptNodeDto` — FE renders boss on lesson cards only.
- **Q11 — No admin endpoint** to toggle `IsBoss` — deferred to P7-03.

**Status check — BE-1 and BE-2 were ALREADY DONE via P2-04:**
- **BE-1 (per-node state):** `LearningPathEngine` + `GetSubjectSkillTreeQueryHandler` already compute `Locked/Available/Completed` for skills + concepts + lessons. 95% shipped via PR #63.
- **BE-2 (why-locked):** `SkillNodeDto.MissingPrerequisites` and `LessonInUnitDto.MissingPrerequisites` already populated. 100% shipped.
- **BE-3 (boss flag):** the only real new work in P2-03. Done.

**Non-blocking follow-ups** (carry forward):
- P7-03 admin curriculum console: provide UI to toggle `IsBoss` per lesson.

### P2-09 — Home dashboard ✅ Merged via PR #67

Wave-9 story 2, now on main. `GET /api/Learning/Dashboard` returns XP/Streak (= 0 in Phase 2; TODOs for P4-02/P4-03), Mission/League (= null; TODOs for P4-06/P4-07), and `Continue` (most-recent-Attempt subject → engine → first Available lesson; cross-subject fallback Math/Science/Arabic/English; default Grade-1 Math when no attempts). New repo method `GetMostRecentActivitySubjectIdAsync`. 11 integration tests including cross-student IDOR isolation. See `docs/briefs/P2-09.md` + `docs/plans/P2-09.md`.

### P2-09 — Home dashboard ✅ Batches 1–2 complete, PR pending

**What's on branch `feat/P2-09-home-dashboard` (ready for PR):**
- **`DashboardController`** ✅ new `GET /api/Learning/Dashboard` `[Authorize]` (any role; per-student via `_currentUser.UserId` — no studentId param, IDOR-proof by construction).
- **`GetDashboardQuery` + Handler** ✅ parameterless query → `DashboardDto { Xp:int=0, Streak:int=0, DailyMission:DailyMissionDto?=null, LeaguePreview:LeaguePreviewDto?=null, Continue:ContinueTargetDto? }`. Continue resolution: most-recent-Attempt subject → engine → first Available lesson (SequenceOrder ASC then Id ASC); if no Available, cross-subject fallback Math/Science/Arabic/English; falls back to Grade 1 Math when student has no attempts. Returns `Continue=null` if nothing Available anywhere.
- **`DTOs`** ✅ at `Application/Features/Dashboard/Dtos/` — `DashboardDto`, `ContinueTargetDto (SubjectId, SubjectName, LessonId, LessonName, UnitName, SkillId?, SkillName?, NodeState)`, `DailyMissionDto (Type, Target?, Progress?)`, `LeaguePreviewDto (TierName?, Rank?, TotalPlayers?, XpThisWeek?)` — Mission + League are nullable wrappers; Phase-4 owners (P4-06/P4-07) will populate.
- **`ILearningRepository`** ✅ extended with `GetMostRecentActivitySubjectIdAsync(int studentId, CT) → Task<int?>` (AsNoTracking; correlated subquery `Attempts → Lessons → Unit.SubjectId`). Reuses the 5 P2-04 repo methods for the engine inputs.
- **No new migration.** Read-only aggregation over existing P2-01/P2-08/P2-10/P2-11 schema.
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_09_HomeDashboard_Tests.cs` — 11 cases: anonymous 401, fresh-student happy path, continue shape, XP/Streak/Mission/League null-state, most-recent-attempt drives Continue (Math + Science), cross-student IDOR isolation, idempotency, seeder smoke, envelope `"successed":` camelCase. All 11 PASS. **Full Wave-7+8+9 regression (excl. P2-05): 71/71 PASS** (~1m44s, Testcontainers Postgres pg16).

**Key decisions:**
- **Q3 → Option A (most-recent activity)** — query `Attempts` for student, order by `StartedAt DESC`, take first, join `Lesson → Unit → SubjectId`. Fallback Grade 1 Math when no attempts.
- **Q5 — XP/Streak = 0** with `TODO P4-02 / P4-03` comments. Phase-2 zero-state by design.
- **Q6 — Mission/League = null** (typed nullable wrappers, NOT "ComingSoon" shells). FE renders "Coming soon" conditionally.
- **Q9 — No caching.** ~5 DB queries per request worst case. Flagged for P6-06 perf pass (Redis with short TTL keyed on `(studentId, subjectId)`).
- **Q11 — Added one repo method** (`GetMostRecentActivitySubjectIdAsync`) for clean separation; alternative was inline LINQ in handler.

**Non-blocking follow-ups** (carry forward):
- Phase-2 zero-state for XP/Streak/Mission/League will become live in P4-02/P4-03/P4-06/P4-07.
- Dashboard performance — Redis cache per `(studentId, subjectId)` in P6-06.
- File overlap with P2-05 (PR #66): both add methods to `ILearningRepository.cs`. Additive merge — git auto-handles when both PRs land.

### P2-05 — Open and complete a lesson ✅ Merged via PR #66

Wave-9 story 1, now on main. Added `Lesson.Explanation` + `Lesson.Visual` columns (migration `AddLessonContent`), `GET /api/Learning/Lessons/{id}` `[Authorize]` route with `QuickCheck` field, `LearningSeeder.SeedDemoLessonContentAsync` for 4 Grade-1 root lessons (Math/Science/Arabic/English) with hand-authored content + 1 MCQ each, full e2e completion-flow integration test, `ex.Message` leak fix in `GetLessonQueryHandler` (Q12). See `docs/briefs/P2-05.md` + `docs/plans/P2-05.md` for the full record.

**P2-05 carry-forwards still open** (filed on main but not fixed in #66):
- Remove the old `GET /api/Learning/Lessons?id={id}` back-compat action in a future hardening wave.
- Fix `ex.Message` leak in `GetSubjectLessonsQueryHandler` (sibling to the one fixed) → P6-06.
- `QuizQuestion` has no `Order` column — "first by `Id ASC`" is the quick-check selection rule. Fragile when P3-05 generates multiple questions per lesson.
- `StartAttempt` lock-enforcement gap (R3) — `StartAttempt` does NOT currently enforce `LearningPathEngine`-derived `Locked` state → hardening wave.
- `LessonsController` does NOT have a `[Route(...)]` attribute today — current convention works; verify if routing convention changes.

### P2-03 — Navigate the skill tree ⏸️ Pending start

Wave-9 story 3. BE-1 + BE-2 may already be substantially done by P2-04 (engine surfaces `MissingPrerequisites`); BE-3 (boss-node flag) needs a `Lesson` schema change. P2-05's migration is now on main, so the schema base is clear — P2-03 can start whenever the lead is ready.

## Wave 8 — Phase 2 backend ✅ Fully merged

All Wave-8 work is merged to main (P2-04 via PR #63, P2-07 via PR #64). Original Wave-8 briefs preserved below for historical reference.

### P2-07 — Instant answer feedback ✅ Batches 1–5 complete, PR pending

**What's on branch `feat/P2-07-instant-answer-feedback` (ready for PR):**
- **`AnswerComparator`** ✅ pure static at `Learning.Domain/Services/AnswerComparator.cs` — plain `switch` on `QuestionType` (no design pattern). MCQ: `OrdinalIgnoreCase` (preserves P2-08 behavior); TrueFalse: `bool.TryParse` both sides + equality; FillInBlank: trim + `OrdinalIgnoreCase`; Matching: string-compare fallthrough with `TODO P2-07.b` (no matching questions seeded today). Null/whitespace inputs return `false` (no throw). 12 unit tests in `AnswerComparatorTests.cs`.
- **`SubmitAnswerCommandHandler`** ✅ uses `AnswerComparator.AreEqual(...)` for correctness; injects `IPublisher`; publishes `AnswerSubmittedIntegrationEvent` after `AddAsync` and before return (direct publish per ADR 0002 Option B, NOT outbox). Guarded on `question.SkillId.HasValue` — null skips with `_logger.LogWarn` + `TODO P3-09`. Try/catch around `Publish` is fail-soft (publisher exception is logged via `_logger.LogError(ex, msg)`; user request still succeeds).
- **`CompleteAttemptCommandHandler`** ✅ same pattern. Loads `Lesson.SkillId` via the new `GetLessonSkillIdAsync` repo method; publishes `LessonCompletedIntegrationEvent` (7 fields: `EventId, OccurredOnUtc, StudentId, LessonId, SkillId, AccuracyPercentage:int (rounded from double), CorrectAnswerCount`). Same null-skip + fail-soft pattern. `AbandonAttemptCommandHandler` is **NOT** touched — abandonment is not a completion event.
- **`ILearningRepository` extended** ✅ `GetLessonSkillIdAsync(int lessonId, CT) → Task<int?>` (AsNoTracking, single projection).
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_07_InstantAnswerFeedback_Tests.cs` — 13 cases via in-test `INotificationHandler<T>` capture (factory layered with `WithWebHostBuilder` — `LearnexiaWebAppFactory` not modified). Covers MCQ/TrueFalse/FillInBlank correctness, event-captured-on-success-with-SkillId, NO event on null-SkillId, NO event on rejection paths (duplicate/IDOR/state guards), `LessonCompletedIntegrationEvent` happy + null-SkillId, idempotent Complete doesn't re-fire, handler isolation (throwing subscriber doesn't fail the API), envelope still `"successed":` camelCase, Abandon doesn't publish. Full Wave-7+Wave-8 regression suite: 60/60 PASS.
- **Security audit** ✅ `docs/briefs/P2-07-security-audit.md` — PASS, 0 Critical/High. Event payloads carry IDs only (no `CorrectAnswer`/`AnswerPayload`/PII). `ex.Message` not leaked. Log lines contain IDs only. Ghost-event-on-rollback documented as accepted Phase-2 trade-off per ADR 0002.

**Key decisions:** Per-type correctness via plain `switch` (no Strategy). Direct `IPublisher.Publish` inside the UoW transaction (Option B), matching the Identity precedent. Skip event when `SkillId IS NULL` (don't extend the cross-module event contract with a sentinel). Fail-soft try/catch around publish (publisher failure must NOT fail the user request). `CorrectAnswerCount` on `LessonCompletedIntegrationEvent` is the 7th field — initially missed by Batch 3 spec, corrected in implementation. Adjusted FillInBlank integration test to use JSON-encoded strings (`CorrectAnswer` is `jsonb`; bare words are invalid JSON) — whitespace-trim still covered by unit tests.

**Non-blocking follow-ups** (carry forward): switch the 4 new log lines to structured-logging placeholder syntax (`"... {AttemptId}"` instead of `$"...AttemptId={attempt.Id}"`) for observability — security-audit F-01 Low. P2-08 inherited: still no `MaximumLength` validator on `AnswerPayload` (recommended for Phase 3 scale-up).

### P2-04 — Unlock rules / Learning Path Engine ✅ Merged via PR #63

Wave-8 story 1 — `LearningPathEngine` (pure static memoized DFS) + 5 AsNoTracking repo methods + JWT-aware wiring into P2-02 handlers + `[Authorize]` tightening on `Subjects/{id}/{Lessons,SkillTree}`. See git log + `docs/briefs/P2-04.md` + `docs/plans/P2-04.md` for full details. **Breaking change**: those two endpoints now return 401 to unauthenticated callers.

### P2-04 — Unlock rules / Learning Path Engine ✅ Batches 1–4 complete, PR pending

**What's on branch `feat/P2-04-unlock-rules-learning-path-engine` (ready for PR):**
- **Engine** ✅ `Learning.Domain/Services/LearningPathEngine.cs` — pure static, three-color memoized DFS over Prerequisite edges. Caller pre-fetches inputs (no DI, no DB). Inputs: `IReadOnlyList<Lesson>`, `IReadOnlyList<KnowledgeNode>`, `IReadOnlyList<KnowledgeEdge>`, `IReadOnlyDictionary<int, SkillMastery> mastery`, `IReadOnlySet<int> completedLessonIds`, `IReadOnlyDictionary<int, Skill> skillsById` (separate from `SkillMastery` so the 3-param mastery record stays tiny). Returns `IReadOnlyDictionary<int, LessonUnlockStateDto>` keyed by `Lesson.Id`. 12 unit tests cover acyclic / cycle / self-loop / null-SkillId / no-prereqs / partial-mastery / exact-threshold / cross-grade / completed-lesson.
- **DTOs at `Domain/Services/`** (next to engine — not under `Application/Features/.../Dtos/`): `SkillMastery (SkillId, AccuracyPercentage:double, TotalAnswers)`, `LessonUnlockStateDto (LessonId, NodeState, IReadOnlyList<MissingPrerequisiteDto>)`, `MissingPrerequisiteDto (PrereqSkillId, PrereqSkillName, PrereqNodeId, RequiredAccuracy:int, CurrentAccuracy:decimal)`.
- **Repository extension** ✅ `ILearningRepository` + `LearningRepository` got 5 new AsNoTracking methods: `GetSubjectKnowledgeNodesAsync`, `GetSubjectKnowledgeEdgesAsync` (returns edges whose both endpoints are in the subject), `GetSkillMasteryForStudentInSubjectAsync` (returns mastery rows for EVERY skill in the subject — zero-row skills get `TotalAnswers=0` so the engine has the threshold), `GetCompletedLessonIdsForStudentInSubjectAsync`, `GetSubjectLessonsAsync`.
- **Wired into 2 existing P2-02 handlers** ✅ `GetSubjectSkillTreeQueryHandler` + `GetSubjectLessonsQueryHandler` now branch on `_currentUser.UserId.HasValue`: authenticated → run engine + project real `NodeState` + `MissingPrerequisites`; anonymous → fall back to existing placeholder (now never reached after Batch 4). Skill-level `NodeState` aggregated from its lessons (Completed > Available > Locked); Concept-level aggregated from its skills.
- **DTOs extended** ✅ `LessonInUnitDto` got `State : NodeState` (new) + `MissingPrerequisites : IReadOnlyList<MissingPrerequisiteDto>` (defaults to empty). `IsLocked` kept for back-compat, marked `[Obsolete("Replaced by LearningPathEngine in P2-04. Will be removed in P2-09 or P6-06.")]`. `SkillNodeDto.MissingPrerequisites` added as nullable (null when anonymous).
- **Auth tightening** ✅ `[Authorize]` added to `GET /api/learning/Subjects/{id}/SkillTree` AND `GET /api/learning/Subjects/{id}/Lessons`. `GET /api/learning/Subjects/ForGrade` stays anonymous. **BREAKING CHANGE:** any client currently calling the two gated endpoints without a JWT will start getting 401. FE wiring already uses auth.
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_04_LearningPath_Tests.cs` — 12 cases (anonymous 401 gate × 2; fresh-student root-Available/downstream-Locked × 2; root-mastery unlocks next-skill; `MissingPrerequisites` shape; completed-lesson state; cross-student isolation; anonymous ForGrade still 200; unknown-subject 404; null-SkillId lesson Available; envelope camelCase). P2-02 tests updated to pass Student JWT on the 7 now-gated cases. All 24 green (~66s, Testcontainers Postgres).
- **2 new localized message keys** in `SharedResources*.resx` + `SharedResourcesKey.cs`: `LearningPathSubjectNotFound`, `LearningPathUnauthorized`.

**Key decisions:** Mastery = `AccuracyPercentage >= MasteryThreshold` (int 0..100) AND `TotalAnswers >= 1`. Completion = ≥1 `Attempt.Status=Completed` for that `(student, lesson)`. Lessons with `SkillId IS NULL` → `Available`. Skills with no prereq edges → `Available` (root nodes). `MissingPrerequisites` = immediate prereqs only (no transitive closure). `Strength` ignored in v1 (kept on schema). Edge of next concern: `Lesson.IsLocked` boolean is deprecated but still in the DB and DTO — removal scheduled for P2-09 or P6-06. P2-07 (sibling Wave-8 story) also touches `ILearningRepository.cs` — ship P2-04 first, rebase P2-07 on top.

## Wave 7 — Phase 2 backend ✅ Fully merged

All 3 stories merged to main (P2-11 via PR #60, P2-08 via PR #61, P2-02 via PR #62). See git log for full details. Original Wave 7 brief and decisions preserved below for historical reference.

### P2-11 — Skill dependency graph ✅ Batches 1–4 complete, PR pending

### P2-11 — Skill dependency graph ✅ Batches 1–4 complete, PR pending

**What's on main (PR #56):**
- `KnowledgeNode` entity — wraps `Skill` via nullable `SkillId?` FK (filtered unique index `UX_KnowledgeNodes_SkillId WHERE SkillId IS NOT NULL`). Fields: Name, NodeType (Skill/Concept/Review enum), SubjectId FK, GradeId FK, Difficulty (int 1–5).
- `KnowledgeEdge` entity — self-referential directed edge. Fields: SourceNodeId, TargetNodeId, RelationshipType (Prerequisite/Related enum), Strength (decimal 0–1, default 1.0). Both FKs `DeleteBehavior.Restrict`; SkillId FK `SetNull`.
- Migration `AddSkillGraphTables` (learning schema).

**What's on branch `feat/P2-11-skill-dependency-graph` (ready for PR):**
- **BE-3** ✅ `SkillGraphValidator.AssertAcyclic` (static, three-color DFS over Prerequisite edges only) at `Learning.Domain/Services/SkillGraphValidator.cs` + 6 unit tests (acyclic / cycle / self-loop / related-excluded / empty / mixed) — all green.
- **BE-5** ✅ `GetPrerequisitesQuery` + `GetUnlockedByQuery` CQRS handlers under `Learning.Application/Features/KnowledgeGraph/` + `KnowledgeNodeDto` + `KnowledgeGraphProfile` (placed in `Application/Mapping/` to match the existing convention, not under `Features/`); `KnowledgeGraphController` exposing `GET /api/Learning/KnowledgeGraph/Prerequisites/{nodeId}` + `/UnlockedBy/{nodeId}` (both `[Authorize]`). Repository extended on `ILearningRepository` with `GetPrerequisiteNodesAsync`, `GetUnlockedByNodeAsync`, `KnowledgeNodeExistsAsync`. Localized `KnowledgeNodeNotFound` key added in en-US + ar-EG resources.
- **BE-4** ✅ `LearningSeeder.SeedSkillGraphAsync` — maps every seeded `Skill` → `KnowledgeNode` (idempotent on `SkillId`, Difficulty=3 default); authors 7 Prerequisite edges across Math G1→G6 (skipped chains where a P2-10 skill name doesn't exist, e.g. "Place Value", "Division" — documented inline). Calls `SkillGraphValidator.AssertAcyclic(existing.Concat(@new))` before save; on cycle detection logs error + skips save (does NOT crash startup). Uses `GetService<ILoggerManager>()` (null-tolerant) so existing seeder unit tests keep working with a minimal service provider.
- **BE-6 DESCOPED** — no wiring to P2-04/P3-08/P3-10; the query API IS the integration seam; P2-04 consumes it when built (Wave 8).
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_11_KnowledgeGraph_Tests.cs` — 6 tests (Prerequisites happy path, UnlockedBy happy path, unknown nodeId ≠ 500, unauthenticated → 401, seed smoke check, `"successed":` envelope literal) all green against Testcontainers PostgreSQL.

**Key decisions:** KnowledgeNode wraps (not replaces) Skill; within-subject edges only in demo seed; BE-6 seam only. **Skill Name strings must not be renamed** (P2-10 seeder + P2-11 use them as lookup keys). Math prereq chain skips Division (no Division skill seeded in P2-10) — jumps G3 Multiplication → G5 Fractions; revisit when P2-10 fills out Division skills. BL-01..05 deferral now recorded in `user-stories/README.md` (AC-7).

### P2-08 — Record granular answers ✅ Batches 1–4 complete, security PASS, PR pending

**What's on main (PR #58):**
- Migration `AddAttemptQueryIndexes` — composite `(StudentId, Status)` on `learning.Attempts`; `(AttemptId, QuestionId)` on `learning.StudentAnswers`. Schema from P2-06 already had all needed columns (zero gaps).
- `AttemptStatus` has `Abandoned=3`.

**What's on branch `feat/P2-08-record-granular-answers` (ready for PR):**
- **BE-1** ✅ `SubmitAnswerCommand` → `POST /api/Learning/Quizzes/{attemptId}/Answers` `[Authorize(Roles="Student")]`. Cross-lesson injection guard (`question.LessonId == attempt.LessonId`), re-answer guard (duplicate `(AttemptId, QuestionId)` → 424), case-insensitive correctness check, returns `{isCorrect, correctAnswer:null-when-correct, hintAvailable:false}`. TODO comment for P2-07 `AnswerSubmittedIntegrationEvent`.
- **BE-2/3** ✅ `CompleteAttemptCommand` + `AbandonAttemptCommand` → `POST …/Complete` and `POST …/Abandon` `[Authorize(Roles="Student")]`. Both idempotent on terminal state (re-call returns current snapshot); cross-terminal rejected (Complete on Abandoned → 424 and vice versa). `RecomputeAggregates` private helper duplicated in both handlers (plan-authorized; not a shared service). Returns `AttemptSummaryDto`. TODO comment for P2-07 `LessonCompletedIntegrationEvent`.
- **BE-4** ✅ `GetStudentAttemptsQuery` → `GET /api/Learning/Students/{studentId}/Attempts` `[Authorize]` (new `StudentsController`) + `GetSkillStatsQuery` → `GET /api/Learning/Skills/{skillId}/Stats?studentId=` `[Authorize]` (appended to existing `SkillsController`). Both enforce per-student IDOR guard (`studentId == _currentUser.UserId`). `AttemptListItemDto` and `SkillStatsDto` both omit `CorrectAnswer` entirely. Skill-stats zero-data case returns zeroed DTO (not 404/500); questions with null `SkillId` silently excluded (correct behavior).
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_08_RecordGranularAnswers_Tests.cs` — 17 test cases (all 6 SubmitAnswer + 3 Complete + 3 Abandon + 2 GetStudentAttempts + 3 GetSkillStats per plan Batch 5) — all green (~30s, Testcontainers Postgres + Student-role JWT via parent→child onboarding flow).
- **Security audit** ✅ `docs/briefs/P2-08-security-audit.md` — 0 Critical/High; all 7 focus areas PASS (JWT-derived StudentId, ownership, IDOR, no `CorrectAnswer` leak, no `ex.Message` leak, `TimeSpentSeconds ≤ 3600`, cross-lesson guard). 2 Low + 4 Info findings documented, none blocking.
- **Bug fix surfaced + applied:** `RecomputeAggregates` was computing negative `DurationSeconds` because Npgsql returns `timestamp with time zone` columns with `Kind == Local`. Fixed by normalizing `attempt.StartedAt.ToUniversalTime()` before subtracting `DateTime.UtcNow` (+ `Math.Max(0, …)` belt-and-suspenders). Comment in both handlers explains the Kind=Local rationale.

**Key decisions:** P2-08 owns `SubmitAnswerCommand`; P2-07 (Wave 8) extends it with feedback. DurationSeconds = server-side `UtcNow - StartedAt.ToUniversalTime()`; per-answer TimeSpentSeconds advisory (validated ≥0, ≤3600). Reject duplicate QuestionId in same attempt. Validators: Submit/Complete/Abandon all enforce `AttemptId > 0`; SubmitAnswer also enforces `AnswerPayload` not-empty + `TimeSpentSeconds` 0..3600 range. 14 new localized message keys (en-US + ar-EG).

### P2-02 — Browse subjects & lessons ✅ Batch 1 merged (PR #57), api-tester PR pending

**What's on main (PR #57):**
- `NodeState` enum at `Domain/Enums/NodeState.cs` — `Locked=0`, `Available=1`, `Completed=2` (placeholder from `Lesson.IsLocked`; P2-03/P2-04 replace the logic)
- `GET /api/learning/Subjects/ForGrade?grade={1-6}` → `GetSubjectsForGradeQuery`
- `GET /api/learning/Subjects/{id}/Lessons` → `GetSubjectLessonsQuery` (nested Units→Lessons, SequenceOrder)
- `GET /api/learning/Subjects/{id}/SkillTree` → `GetSubjectSkillTreeQuery` (Concepts+Skills with placeholder NodeState)
- No migration — P2-01 schema + P2-10 seed already in place

**What's on branch `feat/P2-02-browse-subjects-lessons` (ready for PR):**
- **Integration tests** ✅ `backend/tests/Learnexia.IntegrationTests/P2_02_BrowseSubjectsAndLessons_Tests.cs` — 12 cases: ForGrade happy paths (G1 + G6) returning 4 subjects each, out-of-range grade=99 → 400 (handler guards 1..6), missing param → 400, item shape (id/name/gradeNumber); Lessons happy path (5 units × 3 lessons for Math G1), order-by-SequenceOrder, unknown subject → 404; SkillTree happy path (5 concepts × 3 skills for Math G1), `state` field present + value ∈ {0,1,2}, unknown subject → 404; envelope `"successed":` camelCase check. All green (~55s, Testcontainers Postgres).

**Confirmed contract:** `grade` query param validated 1..6 in handler (out-of-range → 400, not empty list). `NodeState` serializes as int (no `JsonStringEnumConverter` registered). `SkillNodeDto.State` JSON key is `"state":` (not `"nodeState":`). Endpoints are anonymous-callable today — no `[Authorize]` yet.

**Deferred follow-ups:** Grade JWT claim seam (P6-06); `Concept/Skill.SequenceOrder` columns (P2-11 follow-up; currently ordered by Id); `[Authorize]` on new actions (hardening wave).

### Cloud-env worktree note
Worktrees at `/home/user/Learnexia.worktrees/{P2-11,P2-08,P2-02}` (branches off `claude/phase2-backend-wave7-U48WT`). **Direct `git commit` from the main session's Bash tool fails inside worktrees** (signing server 400 "missing source"). Workaround: dispatch a background `committer` subagent — background agents sign successfully. Main checkout commits without issue.

## TL;DR
- The repo now runs natively in **WSL2** (`~/projects/learnexia`). Clean install + `dotnet build` + Expo web/native bundling are validated.
- The Expo **student-app web** now boots, translates (ar/en), and talks to the backend end-to-end (register/login → 200 + JWT).
- **P1-11** (parent web pages, pixel-perfect from `design-system/screenshots/`) is planned + two screens built: **Login** and **Register**.
- All **new backend** the design implies is deferred to **P1-12 "Batch 2"** (Identity-scoped, parallel-safe with the Phase 2 BE lead) — see "For the backend lead".


## P2-06 — Take a quiz (folded into Learning module)
> Committed on `feat/P2-06-assessment-quiz`; pending Wave-6 PR. Build green, integration + unit tests pass, reviewer PASS.

**Lead decision:** quiz/assessment functionality lives in the **Learning** module (schema `learning`), NOT a separate Assessment module. A separate Assessment module was scaffolded then deleted per lead instruction. **Ask before creating new modules** — all quiz work goes in Learning going forward.

**New domain entities (Learning.Domain):**
- `QuizQuestion` — polymorphic question record with `QuestionType` (MCQ/TrueFalse/Matching/FillInBlank), `Content` (JSON blob), `CorrectAnswer`, `Order`, and `GeneratedBy` (Human/AI). Linked to a `Lesson`.
- `Attempt` — student quiz attempt record; status `AttemptStatus` (NotStarted/InProgress/Completed/Abandoned); links to a `Lesson` and `StudentId`.
- `StudentAnswer` — per-question answer record inside an attempt.

**Migration:** `AddQuizTables` (learning schema) — creates `quiz_questions`, `attempts`, `student_answers` tables in the `learning` schema.

**New endpoint:**
- `POST /api/Learning/Quizzes/{lessonId}/Attempt` — `[Authorize(Roles="Student")]` — creates a new `InProgress` attempt (or resumes an existing one) and returns the lesson's questions **without** the `CorrectAnswer` field. Enforces: lesson-existence check (404), Student-role-only (403), no-answer-leak.

**4 question types modeled** (MCQ / TrueFalse / Matching / FillInBlank) with a per-type content validator (`QuizQuestionContentValidator` helper) and unit tests in `Modules.Learning.UnitTests/QuizQuestionTypeValidationTests.cs`.

**`AttemptService.StartNewAsync` explicit SaveChangesAsync:** calls `LearningDbContext.SaveChangesAsync` directly (not waiting for UoW) to obtain the DB-generated `AttemptId` before returning questions — mirrors the `LinkParentStudentService` precedent. UoW's later save is a no-op.

**Secret hygiene (no new secrets introduced):**
- Remote dev DB connection string lives ONLY in gitignored `appsettings.Development.local.json`.
- `Program.cs` now loads optional `appsettings.{Environment}.local.json` at startup (before other config, optional:true so the app runs without it).
- Tracked `appsettings.Development.json` keeps the localhost default only. **Never commit the .local.json file.**
- Remote DB (75.119.158.102:5346/learnexia): all 5 module schemas migrated; NOT seeded yet. To seed, run `dotnet run --project backend/src/Host/Learnexia.Host -- --environment Development --MinIOConfiguration:Enabled false` (or add a `Bash(dotnet run:*)` allow-rule for the seeding agent).

**P6-06 pre-existing deferrals (NOT introduced by P2-06):**
- F2: JWT `CHANGE_ME` secret in `appsettings.json` should be env-driven + startup-guarded.
- F6: `RequireHttpsMetadata=false` should be Development-only.
- F9: `DbContext` audit stamp uses `DateTime.Now` (should be `UtcNow`).
- F11: MinIO default credentials should be env-driven.
- MSB3277: EF 10.0.0/10.0.8 version conflict to resolve in `Directory.Packages.props`.

## P2-10 — Seed demo subjects & skill trees
> Committed on `feat/P2-10-seed-demo-data`; pending Wave-6 PR. Dev-only idempotent seeder; unit tests green.

- **Seeder location:** `backend/src/Modules/Learning/Learnexia.Modules.Learning.Infrastructure/Persistence/Seed/LearningSeeder.cs`
- **Activation:** runs at startup ONLY in Development, via `IHostEnvironment.IsDevelopment()` inside `LearningModule.InitializeAsync`. The environment check lives in `LearningModule` (not in the seeder) so the seeder is environment-neutral and unit tests can call it directly.
- **Coverage:** all **6 grades × 4 subjects** (Math, Science, Arabic, English; **NO Social Studies**). Math is the deepest tree: 5 units / 15 lessons / 5 concepts / 15 skills per grade; the other three subjects use 2 units / 4 lessons / 2 concepts / 4 skills per grade.
- **Idempotent:** natural-key checks on Subject.Name + Grade; re-running the seeder in an already-seeded DB adds zero rows.
- **`SystemUserId = 0`** convention for all seed-authored rows (matches the broader platform convention for system-generated data).
- **P2-11 extension seam:** Skill `Name` strings are stable lookup keys — P2-11 (skill dependency graph) will use them to attach prerequisite edges. **Do NOT rename skill name strings** after the seeder ships.
- **Demo-ready:** P2-02 (browse subjects/lessons) and P2-03 (navigate skill tree) can now be demoed against a populated DB. Run the backend in `Development` mode to auto-seed.

## P2-12 — Account settings (3-module refactor)
> Committed on `feat/P2-12-account-settings-apis`; pending Wave-6 PR. Build green, 39/39 integration tests pass, security-auditor 2 High findings remediated.

**Architecture:** the original Identity-only plan was restructured (lead decision) into **3 modules + a Shared.Contracts seam**:

- **NEW `Parent` module** (schema `parent`) — owns ALL parent↔child family code: `AddChild`, `LinkChild`, `UpdateChild`, `ListMyChildren`, plus new `UnlinkChild`. Identity's `Family/` handlers, `FamilyScope` authz handler, `ParentController`, and `ParentStudents` entity are **fully removed** from Identity. Route base changed from `/api/Users/Parent/*` to **`/api/Parent/*`**.
- **`Shared.Contracts` seams** — `IChildAccountService` (implemented in `Identity.Infrastructure`) is the ONLY cross-module bridge for child-account create/read/update (mirrors `IUserLookup`). `IParentChildQuery` (implemented in `Parent`) is the reverse seam so Identity `GetMe` can still return `HasChildren`.
- **`Notifications` module** — gained `NotificationPreference` entity (schema `notifications`) + `GET /api/Notifications/Preferences` and `PUT /api/Notifications/Preferences`. Categories: `WeeklyReport`, `StreakAtRisk`, `ProductAnnouncement`, `Achievement` x `Email`/`Push`. First `GET` returns defaults (not persisted until first `PUT`).
- **`Identity` module** — kept account-security endpoints: `POST /api/Users/Account/ChangePassword` (now invalidates OTHER sessions + revokes refresh token; rate-limited 5/15m), `GET /api/Users/Account/Sessions`, `POST /api/Users/Account/Sessions/SignOutOthers`, `GET /api/Users/Account/Plan` (STUB returning `{planName:"Free",status:"Active"}` — replace when payments module lands, **TODO P2-12-PAYMENTS**).

**Migrations applied locally (3 total):**
- `InitialParent` — creates `parent` schema + `ParentStudent` table in the Parent module.
- `AddNotificationPreferences` — creates `notifications.NotificationPreferences` table.
- `DropParentStudent` — drops `identity.ParentStudents` table from Identity.

**Production follow-up:** `identity."ParentStudents"` rows are **NOT** copied to `parent."ParentStudent"` (dev rows are disposable; lead-accepted). A data-copy migration **must** be written before applying `DropParentStudent` to any environment with real link data.

**Known gaps (non-blocking):**
- `Notifications.Application` does not register `ValidationBehavior` per-module (masked by global registration — functionally OK).
- MSB3277 EF version-conflict warning on `Parent.Api` / `Learning.Api` (track in `Directory.Packages.props` alignment).
- `RequireHttpsMetadata` + MinIO default creds deferred to **P6-06**.


## ⚠️ Load-bearing config — do NOT "clean up"
These exist because the WSL clean install drifts dependencies past the Expo SDK 52 pins. Removing them reintroduces a hard crash.
- **`.npmrc` → `auto-install-peers=false`** — stops `*` / `^18||^19` peers grabbing **react-dom 19 / expo 56**, which breaks React 18 ("Should have a queue" hook crash). Requires `@babel/preset-env` to be an explicit dep of student-app (it is).
- **root `package.json` → `pnpm.overrides`**: `inline-style-prefixer ^6.0.4` (keeps web SSR resolving past rnw 0.21's v7), `react`/`react-dom` `18.3.1`.
- **i18n is initialized at module load** in `apps/student-app/app/_layout.tsx` (NOT in a useEffect) — react-i18next changes its hook count unready→ready, so initializing mid-mount crashes. Keep `initI18n()` at module scope.
- **i18n resources are one flat namespace** (`packages/shared/src/i18n/config.ts`) — components use dotted keys like `t('auth.login.title')`. `i18next ^24` / `react-i18next ^15.4` aligned across student-app + `@learnexia/shared` (a major mismatch caused a duplicate react-i18next instance).
- **Backend error envelopes are camelCase** — `ErrorHandlerMiddleWare` serializes with `JsonNamingPolicy.CamelCase` so error responses match the `BaseResponse` success shape (the typed client parses them).
- **Postgres MUST be a pgvector image** (`pgvector/pgvector:pg15` in `docker/docker-compose.yaml`, pinned to pg15 to match staging/prod). The **Catalog** migration `DEMO_PgvectorProof` runs `CREATE EXTENSION vector`; on a plain `postgres` image it fails at startup with `0A000: extension "vector" is not available`. If you stand up a DB elsewhere (e.g. a manual `docker run`), use the pgvector image — not `postgres:15-alpine`. (This bit the remote server until its container was swapped to `pgvector/pgvector:pg15`.)
- **Remote shared DB:** `learnexia` @ `75.119.158.102:5344` runs `pgvector/pgvector:pg15`; fully migrated + seeded (24 subjects / 162 lessons / 162 skills / 13 roles). Its connection string lives ONLY in gitignored `appsettings.Development.local.json` (loaded via the optional `appsettings.{Environment}.local.json` line in `Program.cs`) — never commit it.
- **Regenerating `@learnexia/api-client` needs the .NET 9 runtime** — `nswag` 14.x ships a **Net90** binary and self-checks the runtime, so it won't run on net10 alone. Install side-by-side: `dotnet-install.sh --runtime dotnet --channel 9.0` **and** `--runtime aspnetcore --channel 9.0`. Then: start the backend, `SWAGGER_URL=http://localhost:5080/swagger/v2/swagger.json pnpm --filter @learnexia/api-client refresh:swagger` → `pnpm --filter @learnexia/api-client gen:api` (the default SWAGGER_URL is https://localhost:7080; override to the HTTP :5080 dev URL).

## How to run the stack (dev)
1. **Postgres (pgvector)** — `docker compose -f docker/docker-compose.yaml up -d postgres` (or an existing pgvector container on `localhost:5432`, DB `Learnexia`, `postgres/admin`). Redis is **not** required for dev (connection string empty).
2. **Backend** — from `backend/src/Host/Learnexia.Host`:
   `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 AllowedOrigins=http://localhost:8081,http://127.0.0.1:8081 dotnet run --no-launch-profile`
   (HTTP avoids the untrusted dev cert in WSL; `AllowedOrigins` must list the web origin because CORS uses `AllowCredentials`.)
3. **Frontend** — from `apps/student-app`: `npx expo start --port 8081`. The API base URL is set via `apps/student-app/.env.local` (`EXPO_PUBLIC_API_BASE_URL=http://localhost:5080`, gitignored). Web at http://localhost:8081; LAN/device via `exp://<lan-ip>:8081`.
4. Default locale is **Arabic** (product is Arabic-first). Default theme is **dark**.

## What's built / merged to main
- Dev-env + bootstrap fixes (deps, i18n, auth error handling) — earlier PRs.
- **P1-11 planning docs** (story, tasks, pixel audit, designer pixel-perfect rule) + **P2-12** (settings tabs) + **P1-12** (Batch-2 BE) + the **gap analysis**.
- **Login** screen pixel-perfect (split layout, persona toggle, social buttons UI-only, theme/lang switches) + shared `SplitFormScaffold`.
- **Register** screen pixel-perfect + `packages/ui` `CheckboxField` (merged).
- **My Children** screen pixel-perfect (parent `Sidebar` + child-selector, family-summary strip, child cards, dashed add-card) + new `packages/ui` primitives **`Avatar`, `KPIStatCard`, `MasteryBar`, `GradientBox`** (PR #29, merged). Per-child + family stats are **Phase-5 stubs** (`parentDashboardStubs.ts`, TODO(P5)) since `LinkedChildResponse` only exposes id/fullName/email.
- **Splash** screen pixel-perfect (`app/index.tsx`): removed the mascot; purple gradient bg + star field, wordmark + subtitle, `DotPulse`, decorative progress bar, "Loading… ⚡", "POWERED BY AI / Gamified Learning" footer. Boot logic (i18n init + `useAuthRoute` guard, hook order) preserved (PR #31). Added `splashBg` gradient tokens.
- **Dashboard / Overview** screen pixel-perfect **minus the chart** (`(parent)/overview.tsx` + cards): header, 4 KPI tiles w/ deltas, subject-mastery (4 product subjects), "Areas to focus on"; the **daily-activity chart is a placeholder** (pending merge). Stats are Phase-5 stubs. **Charts were carved out to Phase 5 → [P5-05-FE](../../tasks/Frontend/student-app/Phase-5-Parent-Analytics/P5-05-FE.md)** (BarChart primitive + daily/20-day/time-of-day + wire real analytics). NB: KPI tiles built inline (not `KPIStatCard` — it lacks a delta slot) to stay pixel-perfect.
- **Settings** screen pixel-perfect (`(parent)/settings.tsx`): six-tab rail via new `packages/ui` **`Tabs`** primitive; **Profile** + **Language & region** functional; the other four tabs (Notifications/Linked/Security/Plan) are "coming soon" → **P2-12**. **Profile is now wired to the real backend** (P1-12-FE-1, pending merge): `useMyProfile`/`useUpdateProfile` hooks load + **save** fullName/phone/country via `GET`/`PUT /api/Users/Account/Profile` (api-client regenerated from #40), success/error states, avatar shows `avatarUrl`. **Avatar upload/remove stays a stub** until BE-4; email is display-only (not in the profile command).
- **Reports** = **blank placeholder** only (`(parent)/reports.tsx`) wired to the sidebar — full Reports + charts deferred (`P1-11-FE-9` / `P5-05-FE`) per product call (pending merge).
- **Landing** scaffolded **`apps/marketing-site`** as a Next.js 15 app (mirrors `admin-dashboard`) + the Landing page pixel-perfect to `01-landing.png` (nav, hero headline/CTAs/trust row, phone mockup). CTAs link to the student app via `NEXT_PUBLIC_APP_URL` (default `http://localhost:8081` → `/register`, `/login`). English-only (RTL scoped out for marketing); design-system tokens/fonts wired via `app/globals.css`. build/type-check/lint pass (pending merge). **This completes the P1-11 screen set.**
- **P1-11 pixel-perfect QA pass** ([P1-11-qa-pass.md](../../design-system/ui_kits/parent-dashboard/P1-11-qa-pass.md)) + fixes (pending merge): closed the Blocker (shared sidebar **"THIS WEEK +XP"** widget) + 4 Majors (Login brand **social SVG icons**, `FamilySummaryStrip` **AvatarStack** of children vs mascot, **per-subject mastery colors**, Register eyebrow `$primary`) + most Minors. New: `AvatarStack`, `SocialIcons`, `primarySoftStrong` token, `MasteryBar.accent`, `Avatar xl`, `Select.hideLabel`. Deferred minors: country **flag prefixes** (GAP-06 — no `flag` in COUNTRIES), a couple of `ScreenHeader` tablet deltas. Social icons are token-styled marks (no SVG transformer wired yet — swap for licensed vectors later).
- **Design system — Arabic/RTL + atomic-component preview pass** (`design-system/`): added an **Arabic (RTL) capture set** (`screenshots/mobile-ar/` 24, `screenshots/web-ar/` 7 — same screens as English) and **`index-ar.html`** RTL versions of both UI kits (`ui_kits/parent-dashboard`, `ui_kits/student-mobile`). New **`design-system/preview/`** with ~81 **atomic component cards** (per-component HTML, both stacks): 29 `mobile-*`, 25 `web-*` (English) + 27 `ar-*` (Arabic RTL) on a shared `_base-ar.css`. Updated the kit JSX (`Components/PagesApp/PagesPublic/Screens/ScreensAuth/ScreensExtra/index.html`) + `screenshots/README.md` (now documents EN+AR captures + the preview cards). **For `frontend`/`designer` agents:** these are the per-component RTL/Arabic source of truth alongside the screen captures — cite the matching `preview/*.html` / `screenshots/*-ar/*` when building RTL or component-level work.
- **P1-11 pixel-alignment v2 — full preview-card + EN/AR pass** (branch `feat/design-system-pixel-align`, pending PR): re-aligned all 7 built surfaces (Login, Register, My Children, Overview, Settings, Splash, Landing) to the new `design-system/preview/*.html` atomic cards + `screenshots/{web,web-ar,mobile,mobile-ar}/`, in **both EN (LTR) and Arabic (RTL)**. Per-screen delta specs live in `design-system/ui_kits/parent-dashboard/align-*.md` + `student-mobile/align-splash.md`. **Updated `.claude/agents/designer.md`** to make the preview cards co-canonical with screenshots and fold in the `README.md`/`SKILL.md` brand law (10 rules, voice/tone, emoji semantics, Eastern-Arabic-numeral RTL conventions + Latin exceptions, copy cheat sheet, UI-kit click-through refs, motion specs, fraction-detail extraction checklist). **New tokens** (mirrored in `colors_and_type.css` + `packages/design-system/src/tokens/*`): `primaryLight`, `fg4`, `purpleLight`, `fg2Alpha`, `xpSoft`, `streakSoft`, `borderInput`, `borderSubtle`, `radius.nav`(12), `radius.cardInner`(14), `fontSize.wordmark`(36), `gradBrandPanel`, `splashProgress`, and a **corrected warm `splashBg`** (was cold blue-indigo). **Shared primitives** updated (MasteryBar accent/LTR/height, Tabs active-pill + no border-stripe + radius 12, Select radius 8 + `size`, Button radius 16 + press 0.95 + primary glow, TextField height 48 + `forceLtr`) + **new `PasswordStrengthMeter`** (the P1-11-FE-14 primitive). Shared `Sidebar` re-styled. Reviewers PASS; typecheck + lint + marketing build green. **Deferred follow-ups:** Login "Show/Hide" password as TEXT in label row (needs shared `TextField` change — still emoji reveal); Settings email needs BE `email` on `AccountProfileResponse`; DG-01 AR Settings sidebar parent-context prop; `parent.linkChild.explanation` AR still transliterates "Learnexia"; KPIStatCard value weight 800 vs spec 900; Landing AR/RTL appendix (marketing EN-only); splash 🌟 = placeholder mascot. **Process note:** an implementer subagent ran `git stash` in the shared worktree mid-parallel-batch and reverted everyone's uncommitted work into a stash; recovered by restoring `Sidebar.tsx` + `resources.ts`. **Never let implementer/reviewer agents run `git stash`/`reset`/`checkout` — shared worktree.**
- **Phase 7 — Admin Console backlog** (PR #21, merged): 12 admin stories `P7-01..P7-12` (curriculum mgmt, user/account mgmt, content moderation, analytics/AI-safety oversight) — the feature set behind the P1-10 shell — each with BE + admin-dashboard (Next.js) task files in `…/Phase-7-Admin-Console/`. Added a real **`FR-ADM-1..12`** group to [SRS §4.9](../SRS.md) (note: `FR-ADM`, not `FR-AD` = Adaptivity) and expanded §3 + the goal matrix; all P7 stories trace to it. **Backlog/spec only — nothing implemented (all P7 rows in PROGRESS.md are 🔲).** Handoff/decisions for whoever builds it: [docs/briefs/P7-admin-console.md](../briefs/P7-admin-console.md) (PR #24).

## Key decisions (so you don't relitigate them)
- **Pixel-perfect to `design-system/screenshots/`** is the bar. The `designer` agent has a rule: when a capture exists it's the highest-priority target (cite it, match it, express in `--lx-*` tokens). See `.claude/agents/designer.md`.
- **Subjects = Math / Science / Arabic / English** everywhere (the dashboard/reports captures show "Reading"/"Art" — that's mock data; use the 4 product subjects).
- **Scope trims:** Child Home → **P2-09** (not P1-11); secondary Settings tabs (Notifications/Linked/Security/Plan) → **P2-12** (back + front).
- **All new backend → P1-12 "Batch 2" + P1-13 hardening: ✅ BUILT & MERGED** (profile/`Me`, avatar upload [MinIO], Google OAuth, password reset, update-child, register country+consent; lockout, sign-in anti-enumeration, admin seed). See the "Backend — … DONE" section below. FE can now light up the UI-first surfaces (regenerate the api-client).
- Per CLAUDE.md: **ask before adding any design pattern**; mirror existing shapes (Catalog backend, existing component/hook shapes frontend).

## For the backend lead (P1-12, Batch 2) — ✅ DONE (retained for traceability)
> All items below are **built & merged** — see the "Backend — … DONE" section for PRs/details. Kept here as the original gap list.
All Identity-module-scoped, parallel-safe with your Phase 2 BE work. Stories + tasks:
- `user-stories/Phase-1-Foundation/P1-12-web-account-backend-batch2.md` + `tasks/Backend/Phase-1-Foundation/P1-12-BE.md`.
- Gaps found while building the UI: **profile read/update + enriched `/Me`** (no `Phone` column today), **avatar upload** (no storage/`AvatarUrl`), **OAuth** (Google/Apple/Microsoft), **password reset**, **update-child** (no UpdateChild command exists), **register country + terms-consent** (`RegisterParentCommand` takes only `{email,password,fullName}`).
- Source analysis: `docs/briefs/phase-1-design-gap-analysis.md`.

## What's next (web FE)
- **P1-11 screen set is complete**: Login, Register, My Children, Splash, Dashboard (chart-less), Settings, Landing all built; Reports is a deliberate blank placeholder. Remaining P1-11 follow-ups are the **UI-first wiring once P1-12 BE lands** (profile save, avatar, social/forgot, edit-child) and the **CAPTCHA/lockout FE** (`P1-11-FE-15/16`, after P1-13 BE).
- **Charts moved to Phase 5** ([P5-05-FE](../../tasks/Frontend/student-app/Phase-5-Parent-Analytics/P5-05-FE.md)); `P1-11-FE-2` retired into it. **Full Reports** (KPIs/mastery/charts) = `P1-11-FE-9` + P5-05-FE when picked up.
- Remaining shared primitives (`P1-11-FE-14`): **Switch**, **PasswordStrengthMeter** (Avatar, KPIStatCard, Sidebar, MasteryBar, GradientBox, CheckboxField, **Tabs** now built).
- Per-child/family analytics stats are stubbed (`(parent)/_components/parentDashboardStubs.ts`) until **Phase 5** (P5-01/P5-05) lands real data.

## Backend — P1-12 Batch 2 + P1-13 hardening: ✅ DONE (merged to main)
> The Phase-1 backend leftover is complete and on `main` (all Identity-module-scoped, parallel-safe). Every story ran **security-auditor + api-tester + reviewer**; the integration suite is green (**334 tests**, incl. real PostgreSQL + MinIO containers). Source: [phase-1-design-gap-analysis.md](../briefs/phase-1-design-gap-analysis.md) + [phase-1-backend-gap-analysis.md](../briefs/phase-1-backend-gap-analysis.md).
- **P1-13a** (PR #33) — Notifications email delivery: `IEmailSender` + SMTP adapter + dev log-sink; `UserRegistered` → best-effort welcome email.
- **IUserLookup** (PR #35) — Identity seam in `Shared.Contracts` so Notifications can resolve a recipient email.
- **P1-13** (PR #39) — hardening: account **lockout** engaged; sign-in **anti-enumeration** + no `ex.Message` leak (⚠️ sign-in errors are now **uniform** — FE must NOT branch on not-found vs wrong-password); config/env-driven **Admin seed** (legacy `superadmin`/`basicuser` dev-only). **BE-4 CAPTCHA NOT built** — see "Still open".
- **P1-12** (PRs #40, #43, #44, #45, #46): BE-3 migration (reused `PhoneNumber`/`Nationality`, added `AvatarUrl` + `AcceptedTermsAtUtc`); BE-1/2 profile read/update + enriched `/Me`; BE-9 register `country`+terms-consent; BE-8 edit-child (family-scope, 403 on non-own); BE-4 **avatar via self-hosted MinIO** (`HttpClient` + hand-rolled **AWS SigV4**, **NO MinIO SDK** — "AWS SigV4" is just the S3 signing algo, no AWS dependency; storage lives in **`Shared.Kernel`** as `IStorageService`, stream-based, registered at the Host → reuse it for ANY future upload e.g. BL-01); BE-5 **Google** social sign-in (`Google.Apis.Auth`, ID-token flow); BE-6 password reset (anti-enumeration + session invalidation, email via the `Shared.Contracts` event seam).

### ⚠️ Load-bearing backend config — set via ENV in staging/prod (do NOT commit real values)
- **MinIO:** `MinIOConfiguration__AccessKey` / `__SecretKey` (self-hosted `minio` container in `docker/docker-compose.yaml`; dev defaults `minioadmin`; private `avatars` bucket; presigned URLs).
- **Google:** `GoogleAuth__ClientId` (sign-in audience; inert/fail-closed if unset).
- **Admin seed:** `AdminSeed__Email` / `__Password` (no-op if unset; no committed credential).
- **Password reset:** `ClientAppBaseUrl` (reset-link origin; dev default `http://localhost:3000`).
- **Email:** `Email__Provider=Smtp` + `Email__Host/__UserName/__Password` for real delivery (dev = `None`/log sink).

### Still open (backend)
- **P1-13 BE-4 — CAPTCHA on register**: ✅ BUILT (Cloudflare Turnstile `TurnstileCaptchaVerifier` + `ICaptchaVerifier`; config-gated, fail-closed). Ships `Captcha:Enabled=false` by default; **PR #65 now fail-fasts in Production/Staging** unless enabled + secret set. FE consumer `P1-11-FE-16`.
- **Hardening follow-ups** (non-blocking; in the per-PR security briefs): **per-IP throttle on auth endpoints ✅ tightened in PR #65** (env-gated; prod/staging: sign-in 50/5m, register 10/15m, forgot 5/15m, reset 10/15m); forgot-password **timing-oracle** decouple (email send still synchronous in-request) — ⏳ P6-06 AC-5; **localize** reset + welcome emails (English-only) — ⏳ P6-06; MinIO presign TTL ✅ already 60m.

### Phase-1 security follow-up audit (2026-05-29) — branch `audit/phase-1` → PR #65
Verified every Phase-1 security-audit follow-up against `main` (all original audits were PASS / PASS-WITH-FOLLOWUPS — **zero Critical/High**). ~10 of ~18 follow-ups already applied (timing-oracle dummy-hash, CRLF guard, email PII masking, SMTP fail-fast, MinIO no-`ex.Message`/TTL→60m/detected-Content-Type, no raw-Identity-error concat on register, per-endpoint rate limits, GuardJwtSecret).
- **Fixed in PR #65:** **B1** CAPTCHA prod-guard (`GuardCaptcha` in Identity `DependencyInjection.cs`); **G1/B2** env-gated auth rate limits (`Host/Extensions/ServiceExtensions.cs` `ConfigureRateLimitingOptions(IConfiguration)`; Dev/Testing keep the prior 100/s rules verbatim so the integration suite is unaffected). Build green; Testcontainers suite NOT run this session (no Docker) — reviewer/api-tester to run before merge.
- **Routed to P6-06** (`user-stories/Phase-6-Stabilization/P6-06-...md`, new AC-7): **G2** — JWT bearer does NOT validate any per-request server state, so an already-issued access token survives sign-out/password-reset until expiry (only the refresh-token cache + sessions are dropped). Chosen design: **SessionId per-request validation** via `JwtBearerEvents.OnTokenValidated` against `ISessionManagementService` (preserves P2-12 "ChangePassword keeps current session"); explicitly NOT security-stamp validation. Load-bearing auth → full pipeline.
- **Still outstanding (Low/Info, mostly P6-06):** `RequireHttpsMetadata=false` not env-gated; DB password default in `appsettings.json` no fail-fast; no `[RequestSizeLimit]` on avatar upload; child `Email` echoed in Added/Updated/LinkedChildResponse DTOs; Google auto-link w/o confirmation + auto-stamped consent; CORS `?? "*"` + `AllowCredentials()` fallback unguarded.

### FE now unblocked (regenerate the `api-client`)
Profile save (`/Account/Profile`), avatar upload/remove (`/Account/Avatar`), Google button (`/Authentication/Google-SignIn`), forgot/reset (`/Authentication/Forgot-Password` + `Reset-Password`), edit-child (`/Parent/Update-Child`), register `country`+`acceptedTerms`. Sign-in errors are uniform now (`P1-11-FE-15` / `P1-10-FE-6`).

### Backend → Frontend coverage gap analysis (new, 2026-05-24)
> The reverse of the FE-design gap analysis: starting from every Phase-1 **backend capability**, does a FE story/task consume it? Brief: [docs/briefs/phase-1-frontend-coverage-gap-analysis.md](../briefs/phase-1-frontend-coverage-gap-analysis.md) (grounded in the real Identity/Notifications controllers).
- **Headline:** most backend is already FE-covered — the earlier design gap analysis routed every design-implied backend gap into **P1-12 (Batch 2)**, and **P1-12-FE already plans that wiring** (FE-1..5). Those are deferred, not gaps.
- **Real FE gaps found → tasks added (no new story needed):**
  - **F2 (sign-in contract change, highest value):** P1-13-BE-1/2 change Sign-In (locked-account message + uniform "invalid credentials" anti-enumeration) but no FE consumed it → added **P1-11-FE-15** (student login) + **P1-10-FE-6** (admin login). **Both must land after P1-13-BE-1/2 merge.**
  - **F1 (register country+consent wiring):** P1-12-BE-9 persists `country`+terms-consent but no FE task wired the collected fields → added **P1-12-FE-7** (Batch 2, after BE-9 + api-client regen).
- **CAPTCHA on register (P1-13-BE-4) — confirmed in P1 scope (2026-05-24):** added **P1-11-FE-16** — Register integrates the bot-challenge and sends the token when the server advertises the requirement; **lands after P1-13-BE-4 merges**. (P1-13-BE-4 stays in P1, no longer deferred to P6.)
- **Resolved non-gaps:** student-app sign-out is already covered by **P1-02-FE-3** (`useSignOut`); email-verification UX is N/A (BYPASSED by lead decision); the AdminOnly UserManagement/Authorzation surface is correctly deferred to the Phase 7 Admin Console.

## Workflow notes
- Branch per change; **PRs to main**, the user merges. **Don't stack PRs on an unmerged base and then merge the base first** — the stacked changes get stranded (this happened to Register; it was re-PR'd straight to main). Now that Login is in main, branch new screens **off main**.
- Git identity isn't set in this WSL checkout — commits use a per-invocation `-c user.name/email` override (`Ahmed Elbaradey <elbaradeyahmed1985@gmail.com>`); set it permanently if you prefer.
- Pixel-perfect verification needs a browser; headless Chromium wouldn't download in this env, so screenshot review has been done by the human. The error overlay's **Log 1 of N** is the root error (later logs cascade).
- **Activate the auto-load hook on first pull:** a committed `SessionStart` hook (`.claude/settings.json`) auto-loads this file into context — but if your session was already open when you pulled it, run **`/hooks`** once (or restart Claude Code / start a new session) to load it. New sessions after that pick it up automatically.
