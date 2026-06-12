# Pipeline Brief — Phase 1/2/3 CARRYOVER backlog (Waves A–D)

> **Analyzer output, 2026-06-12.** Consolidated brief for the whole carryover backlog defined in
> `docs/dev/CARRYOVER-P1-P2-P3-LEAD-BRIEF.md`, grounded in
> `tasks/Backend/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-BE.md`,
> `tasks/Frontend/student-app/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-FE.md`,
> `docs/briefs/phase-1-2-3-gap-after-p7.md`, `docs/briefs/phase-1-2-fe-gap-analysis.md`,
> the per-story files in `user-stories/` + `tasks/`, and a presence check of the actual code on `main`.
> Consumed by `planner` → `docs/plans/p1-p2-p3-carryover.md`.

## 0. Decisions already made (BAKED IN — do not re-open)

Recorded in `docs/dev/HANDOFF.md` (2026-06-12 entry):

1. **Matching answer payload** = `{ "pairs": [{ "leftId": <id>, "rightId": <id> }], "attemptOrder": <int>, "timeMs": <int> }`.
   Comparator = **order-independent pair-set equality**. Demo seed must include **all 4 question types**
   (MCQ, TrueFalse, FillBlank, Matching). FE builds a **real tap/drag pairing UI** (replaces the
   empty-payload `MatchingPanel` stub).
2. **Attempt-history (P2-09 / G5) is IN SCOPE now** — not deferred to Phase 5.
3. **Marketing landing ar/RTL (CO-FE-4) already exists on `main`**
   (`apps/marketing-site/app/[locale]/` for `/en`+`/ar`, `middleware.ts`, `lib/copy.ts`,
   `LanguageSwitcher` — verified present) → **DROPPED from this backlog, review-only. No build work.**

---

## 1. Scope & traceability

Statuses verified against `main` (git log, controllers, screens) on 2026-06-12 — not just tracker cells.
"FE absent" = no consumer/screen on `main`.

### Wave A — Phase-1/2 FE polish

| Item | Story / task ID | Capability (one line) | Backend status | FE status | Security-sensitive? |
|---|---|---|---|---|---|
| A1 | **P1-10-FE-6** (P1-10) | Admin sign-in: uniform "invalid credentials" + distinct localized "account locked/deactivated" message | ✅ merged (P1-13-BE-1/2; P7 suspend/delete states) | 🔲 absent (`apps/admin-dashboard/app/login/page.tsx` shows generic error only) | **Yes** (auth surface, anti-enumeration) |
| A2 | **CO-FE-2 / P1-11-FE-15** (P1-11) | Student/parent web sign-in: same distinct `LoginTooManyFailedAttempts` + `LoginAccountDeactivated` messaging (en/ar) | ✅ merged | 🔲 absent (`(auth)/_components/LoginForm.tsx` generic-only) | **Yes** (auth surface) |
| A3 | **CO-FE-3 / P1-11-FE-16** (P1-11) | Register sends Turnstile `captchaToken` when the server advertises the requirement (config-gated) | ✅ merged (`TurnstileCaptchaVerifier` + `RegisterParentCommand.CaptchaToken`, P1-13-BE-4/6) | 🔲 absent (`RegisterForm.tsx` never sends token) | **Yes** (auth/bot-defense) |
| A4 | **CO-FE-1 / P1-11-FE-9** (P1-11g) | Parent **Reports** page, chart-less: KPIs, subject-mastery bars, date-range selector, "Send Report" action (charts → P5-05) | ✅ data endpoints on main (parent/learning read paths; layout-first against seed data acceptable per story) | 🔲 stub (`(parent)/reports.tsx` = 96-line "comingSoon" tile on `main`; the task-file ✅ for FE-9 is **stale**) | No (parent-scoped reads; reviewer checks authz) |
| A5 | **G5 attempt-history** (P2-09 / FE gap G5) | Consume `GET /api/Learning/Students/{studentId}/Attempts` — past-attempt review hook + screen | ✅ merged (`Learning.Api/Controllers/StudentsController.cs:25`) | 🔲 absent (no hook, no screen; `AttemptSummaryCard` primitive exists in `packages/ui`) | **Yes** (child learning data; IDOR on `studentId`) |
| A6 | **P1-12-FE Batch 3** (P1-12) | Google OAuth + RTL/dark QA | ✅ | ✅ **MERGED** (PR #98, `e32982b` on `main`) → **review-only**; residual = ops config `GoogleAuth__ClientId` must equal FE `EXPO_PUBLIC_GOOGLE_CLIENT_ID` (HANDOFF). PKCE migration = accepted pre-prod debt, do NOT re-architect | n/a |
| A7 | **CO-FE-4** (P1-11b) | Marketing landing ar/RTL | n/a | ✅ **EXISTS on main** → **DROPPED** (decision #3, review-only) | n/a |

### Wave B — Phase-3 Gamification FE (the bulk; every backend merged)

| Item | Story / task ID | Capability | Backend status | FE status | Security-sensitive? |
|---|---|---|---|---|---|
| B0 | **P4-08 motion branch** | Rebase or re-cut `feat/P4-08-gamification-screens-motion` — api-client `/Me` hooks + `RewardPopup`/`ConfettiLayer`/`useReduceMotion`/`useDashboardDiff` plumbing (no screens) | — (FE-only) | 🟡 WIP branch, **137 commits behind / 1 ahead of `main`** (predates P7/P8 + NSwag `/Me` operation-id fix) | No |
| B1 | **P4-02-FE** (P4-02) | XP + level-up moment: live `XPBar`, level-up celebration, dashboard reflects immediately | ✅ merged (PR #73; `GET /api/Gamification/Profile`) | 🔲 header number only | No |
| B2 | **P4-03-FE** (P4-03) | Daily-streak screen/calendar + animated flame + streak-advanced feedback | ✅ merged (PR #75; dashboard `Streak` snapshot) | 🔲 header count only | No |
| B3 | **P4-04-FE** (P4-04) | Hearts meter in lesson/quiz + heart-loss feedback + non-blocking Practice-Mode regain flow | ✅ merged (PR #76; `Hearts`/`InPracticeMode` on dashboard+profile) | 🔲 header count + pill only | No |
| B4 | **P4-05-FE** (P4-05) | Badge collection screen (earned + locked grid) + recent-badges dashboard row | ✅ merged (PR #77; `GET /api/Gamification/Badges/Me`) | 🔲 **zero UI** (no consumer of Badges/Me) | No |
| B5 | **P4-06-FE** (P4-06) | Daily/weekly mission list + progress + dashboard mission widget (replaces null `MissionBanner`) | ✅ **MERGED** (PR #78, merge `e2530a0` on `main`; `GET /api/Gamification/Missions/Me`) → **NOT blocked** — the task-file header "backend 🟡 in progress" is **stale** | 🔲 absent | No |
| B6 | **P4-07-FE** (P4-07) | Weekly-league standings screen (tier, ranks, highlighted self, promotion/demotion cutlines, week countdown) | ✅ merged (`GET /api/Gamification/Leagues/Me` — note **plural**; the task file's `League/Me` is wrong) | 🟡 partial (inline `LeaguePreviewRow` on dashboard only) | No |
| B7 | **P4-09-FE** (P4-09) | Re-engagement in-app nudge surface (inbox feed + deep-link into streak/mission/lesson) + parent per-child controls | ✅ merged (PR #80; Notifications module `InboxController`/`PreferencesController`/`DevicesController`) | 🔲 absent | **Yes** (child-directed messaging, parent consent authority — story mandates security-auditor) |
| B8 | **P4-11-FE** (P4-11) | Streak-freeze balance affordance + timed-event banner/countdown + weekly-challenge cards | ✅ merged (`8f77beb`; `FreezeBalance` on dashboard streak snapshot, `ActiveTimedEvents` on `DashboardDto`, weekly challenges via `Missions/Me` `CHALLENGE_*` rows) | 🔲 absent | No |
| — | P4-10 | Redis realtime | ✅ merged | **No FE by design** — out of scope | n/a |

### Wave C — Matching questions, full-stack (decision #1 unblocks all of it)

| Item | Origin | Capability | Status | Security-sensitive? |
|---|---|---|---|---|
| C1 | **CO-BE-1** (P2-06/P2-07) | Contract note: Matching **question-content** shape (left/right items with ids) + the decided answer payload; shared with FE | 🔲 absent (payload now decided — remaining work = content-shape note in `Learning.Application`) | No |
| C2 | **CO-BE-2** (P2-07) | `AnswerComparator` order-independent pair-set equality (replace `OrdinalIgnoreCase` fall-through, `TODO P2-07.b` at `Learning.Domain/Services/AnswerComparator.cs:~48`) | 🔲 absent (TODO verified still present on `main`) | No (malformed-payload 422 path covered by api-tester) |
| C3 | **CO-BE-3** (P2-10) | Seed demo questions for **all 4 types** — `SeedDemoLessonContentAsync` currently MCQ-only (`LearningSeeder.cs:455-473`); decision #1 expands this to TrueFalse + FillBlank + Matching | 🔲 absent | No |
| C4 | **CO-BE-4** (P2-06) | api-tester: Matching submit end-to-end (correct / wrong / malformed→422), assert `IsCorrect` + granular `StudentAnswer` row | 🔲 absent (`Learnexia.IntegrationTests`) | No |
| C5 | **CO-FE-5 / P2-06-FE-2** (P2-06) | Real tap/drag pairing UI in `packages/ui/src/components/MatchingPanel/` + lesson player wiring; submits the decided payload | 🔲 stub on `main` (verified: "coming soon", empty-string payload) | No |

### Wave D — E2E + QA close-out

| Item | Origin | Capability | Status |
|---|---|---|---|
| D1 | **CO-FE-6** (P1-11-FE-13 / P2-06) | Extend `tests/e2e/` Playwright harness: Reports, locked-login messaging (student + admin), register-with-CAPTCHA, Matching quiz flow, **new gamification screens**, attempt-history; ar/RTL + en, auth/role routing, happy + error paths; pixel QA vs `design-system/screenshots/web/*` | Harness exists (`tests/e2e/playwright.config.ts`, 19 specs incl. P2-06-FE/P2-09-FE to extend); new flows uncovered. Landing-ar/RTL coverage = **review-only** (98/98 marketing specs already pass per HANDOFF) |

---

## 2. Acceptance criteria per item (distilled — what the implementer must satisfy)

### Wave A

**A1 — P1-10-FE-6** (`apps/admin-dashboard/app/login/page.tsx`)
- Single **uniform** "invalid credentials" message for all credential failures — never branch user-not-found vs wrong-password (anti-enumeration; FE must not branch on cause).
- **Distinct localized** "account locked" message after lockout, and the P7 suspended/deactivated state maps to a distinct "account deactivated" message. Drive off `BaseResponse.Successed=false` + message key.
- en/ar localized; existing role-gating/redirect behavior unchanged.

**A2 — CO-FE-2 / P1-11-FE-15** (`apps/student-app/app/(auth)/_components/LoginForm.tsx` + `packages/shared/src/i18n/resources.ts`)
- Same contract as A1 on the universal-app login: uniform invalid-credentials + distinct `accountLocked` branch (key `auth.login.errors.accountLocked`, en + ar) + deactivated/suspended branch; optional retry-after hint.
- RTL renders correctly; error states match `design-system/screenshots/web/02-login.png`.

**A3 — CO-FE-3 / P1-11-FE-16** (`RegisterForm.tsx`)
- Cloudflare Turnstile challenge rendered **only when the server advertises the requirement** (config-gated by P1-13-BE-4); resulting token sent as `captchaToken` on Register-Parent.
- Localized (en/ar) + RTL; challenge/validation errors surfaced; no challenge and no token when the feature is off (current behavior preserved).

**A4 — CO-FE-1 / P1-11-FE-9** (`apps/student-app/app/(parent)/reports.tsx`)
- Replace the "coming soon" stub with: KPI cards, per-subject mastery bars (`MasteryBar`/`KPIStatCard` exist in `packages/ui`), date-range selector, "Send Report" action (stub action acceptable per task file).
- 20-day + time-of-day **charts stay deferred to P5-05** — layout reserves placeholder space per `design-system/screenshots/web/06-reports.png`; subjects = Math/Science/Arabic/English only (ignore the capture's "Reading/Art" mock data).
- en (LTR) + ar (RTL), dark + light; data via `api-client` hooks (no API calls in components); layout-first against seed data acceptable.

**A5 — G5 attempt-history** (new hook + screen)
- `useStudentAttempts(studentId)` hook over `GET /api/Learning/Students/{studentId}/Attempts` (typed in api-client already, per gap analysis).
- A review surface listing past quiz attempts (`AttemptSummaryCard` primitive exists). **Surface placement (student vs parent) is Open Question 1.**
- Authz: only the owning student / linked parent can view — e2e must assert the cross-student case fails.

**A6 — P1-12-FE Batch 3** — no build. Reviewer confirms merged state; ops sets backend `GoogleAuth__ClientId` = FE web client ID (HANDOFF requirement). Do not touch the implicit-grant flow.

### Wave B (common ACs for B1–B8, from P4-08 story + per-task files)

- Every screen renders **Arabic (RTL) + English**, phone/tablet/laptop; design-system tokens only; kid-accessibility: clear visual feedback on every action; motion respects `useReduceMotion` and performs smoothly on mobile (NFR-6/7).
- Data via TanStack Query hooks in `packages/api-client` (regen keeps the **NSwag `CustomOperationIds = {Controller}Me`** host config — without it `/Me` methods regress to positional names; see HANDOFF "Durable NSwag /Me fix").
- Task files name routes `app/(student)/…` — the actual route group on `main` is **`app/(child)/`**; use it.
- Dashboard integrations must read the live `/…/Me` endpoints (they already reflect P7-13 admin `IsActive` gating).

Per item:
- **B0** — rebase/re-cut the motion branch onto `main` first; deliverables = working `RewardPopup`/`ConfettiLayer`/`useReduceMotion`/`useDashboardDiff` + `/Me` hooks compatible with the post-P8 api-client.
- **B1 P4-02** — XP bar animated fill wired to live XP/level; level-up moment (XP fill, count-up, confetti); XP/level update on the dashboard immediately after a learning action (cache invalidation/optimistic).
- **B2 P4-03** — streak state visible as animated flame (current + longest); streak-advanced feedback after qualifying activity; dashboard count live.
- **B3 P4-04** — hearts-remaining in lesson/quiz UI; wrong answer → heart-break + shake (consistent with P2-07 instant feedback); zero hearts → **non-blocking** Practice-Mode "practice to earn hearts back" experience.
- **B4 P4-05** — `BadgeTile` (earned vs locked, rarity, icon key); collection screen with earned + locked grid; badge-earned reward popup; recent-badges row on dashboard (top-3).
- **B5 P4-06** — `MissionCard` (title, icon, `progress/target` bar, reward XP, status; daily vs weekly variants); missions screen with daily + weekly lists incl. completed/expired states; dashboard daily-mission widget replaces the null placeholder; mission-complete feedback (progress fill → reward). FE computes `progress% = progress*100/target`.
- **B6 P4-07** — standings list with the student's highlighted position, tier badges Bronze→Silver→Gold→Diamond, promotion/demotion cutlines, week countdown; consumes `Leagues/Me` ("Student #N" anonymized names come from the server); dashboard preview row already exists — keep/extend.
- **B7 P4-09** — in-app **inbox** screen (nudge feed) as the degraded channel; deep-link a nudge to streak / daily-mission / continue-lesson; **parent settings**: per-child category toggles, quiet hours, daily cap (child cannot change them); Arabic-first, child-safe, never-shaming copy. Push/web-push registration scope = Open Question 3 (overlaps Phase-9 P9-01).
- **B8 P4-11** — freeze **balance** shown on the streak UI; freezes are **earned-only** (locked lead decision — no spend/purchase UI; auto-consume is server-side, FE explains it); timed-event banner with countdown to start/end + reward callout, ends cleanly (data = `DashboardDto.ActiveTimedEvents` — the only student-facing source; `GET /api/admin/timed-events` is AdminOnly, do not call it); weekly-challenge cards (the `CHALLENGE_*` weekly missions from `Missions/Me`) visually distinct from daily missions and league.

### Wave C

- **C1 CO-BE-1** — write the contract note: Matching **question content** JSON (left/right item lists with stable ids, localized text) + the decided answer payload + comparator semantics; place in `Learning.Application`, link from the FE task. The answer payload itself is **decided** (§0.1) — do not redesign.
- **C2 CO-BE-2** — `AnswerComparator` Matching branch: parse the payload, compare as an order-independent **set** of (leftId,rightId) pairs against the stored correct mapping; correct ⇒ `IsCorrect=true`; wrong/incomplete/duplicate-left ⇒ false; malformed JSON ⇒ validation failure (422 path), never an unhandled exception. Keep MCQ/TrueFalse/FillBlank behavior unchanged (415 P2 regression tests exist — keep green).
- **C3 CO-BE-3** — `LearningSeeder` demo lesson content gains TrueFalse + FillBlank + **≥1 Matching** question (all 4 types present), `CorrectAnswer` stored as valid JSON (it's `jsonb`), rows seeded Published/active so student paths see them; seeder stays idempotent.
- **C4 CO-BE-4** — integration tests against the running API: start quiz with the seeded Matching question; submit correct (pairs in shuffled order ⇒ `IsCorrect=true`), wrong, and malformed (⇒ 422); assert the granular `StudentAnswer` row persists the payload.
- **C5 CO-FE-5** — `MatchingPanel` real pairing UI: tap-to-pair (mobile-friendly) and/or drag; visual pair state + unpair; Submit enabled only when all left items are paired; payload per §0.1 with `attemptOrder`/`timeMs` filled like the other renderers; works with P2-07 instant feedback + (post-B3) heart loss; RTL + en; mobile + desktop (P2-06 AC "each question type renders and accepts input correctly on mobile and desktop").

### Wave D

- **D1 CO-FE-6** — new/extended Playwright specs in `tests/e2e/specs/`: Reports page (data render, range selector, RTL), locked-login (student + admin variants), register-with-CAPTCHA (advertised-on and off), Matching quiz happy + wrong + validation, gamification screens (badges/missions/league/streak/hearts at minimum: render, live data, RTL), attempt-history (incl. cross-student denial). Both `ar` + `en`, auth/role routing, happy + error paths. Pixel QA pass vs `design-system/screenshots/web/*` closes the long-open P1-11-FE-13 🟡. Landing ar/RTL = review-only re-run of the existing marketing suite.

---

## 3. Per-agent handoffs

**Design-system constraint (applies to every UI item):** UI = `design-system/` tokens/components per `design-system/SKILL.md` + the relevant `design-system/ui_kits/<surface>/` kit; reuse `packages/ui` + `packages/design-system`; **no new design patterns without lead approval (CLAUDE.md rule 8)**; pixel targets = `design-system/screenshots/web/*` (and `mobile/*` for child screens). Fonts/RTL per P1-11 story rules. No API calls in components; server data in TanStack Query, never Zustand.

| Item | designer | db-migration | backend-feature | frontend | api-tester | frontend-e2e-tester | security-auditor |
|---|---|---|---|---|---|---|---|
| A1 admin locked-msg | — (error-state copy only; reuse login design) | — | — | ✅ (admin-dashboard, Next.js) | — | ✅ (Wave D) | ✅ (auth messaging / anti-enumeration check) |
| A2 login locked-msg | — | — | — | ✅ | — | ✅ (Wave D) | ✅ (bundled with A1) |
| A3 CAPTCHA | small spec (Turnstile placement on `03-register.png`) | — | — | ✅ | — | ✅ (Wave D) | ✅ (token handling, fail-open/closed behavior) |
| A4 Reports | ✅ **required** (new page vs `06-reports.png`; spec → `design-system/ui_kits/parent-dashboard/`) | — | — | ✅ | — | ✅ (Wave D) | — |
| A5 attempt-history | ✅ (new surface, placement per Open Q1) | — | — (endpoint exists) | ✅ | optional (endpoint already integration-tested?) — assert authz if touched | ✅ (Wave D, incl. IDOR case) | ✅ (child data / IDOR) |
| A6 P1-12 B3 | — | — | — | — | — | — | — (review-only + ops config) |
| B0 motion rebase | — (P4-08 design spec needed before B1–B8 motion: `design-system/ui_kits/student-app/P4-08.md` — motion timings, confetti, reduce-motion, RTL) | — | — | ✅ | — | — | — |
| B1–B6, B8 gamification screens | ✅ **one consolidated design stage** for the gamification surface (P4-08 spec covers screens + motion; per-task files defer celebration motion to it) | — | — | ✅ | — | ✅ (Wave D) | — |
| B7 P4-09 nudges | ✅ (inbox + parent-controls panels) | — | — | ✅ | — | ✅ (Wave D) | ✅ **mandatory** (child-directed messaging, consent, no PII in payloads) |
| C1–C3 Matching BE | — | — (no schema change — payload + seed only; confirm in planning) | ✅ (Learning module only; mirror Learning patterns; ADR-0001 deferred commit; `BaseResponse`/`Successed`) | — | ✅ (C4) | — | — |
| C5 Matching UI | ✅ (pairing interaction spec, tap vs drag, RTL) | — | — | ✅ | — | ✅ (Wave D) | — |
| D1 e2e close-out | — | — | — | — | — | ✅ (owns it) | — |

Reviewer gates every batch (CLAUDE.md fixed order). Committer: per-story branches `feat/<ID>` + PR + HANDOFF.md update in the same PR; the three decisions are already recorded in HANDOFF.

---

## 4. Dependency order & parallelism

### Hard dependencies
- **B0 (P4-08 rebase/re-cut) gates all of B1–B8** — it carries the `/Me` hooks + motion primitives every screen consumes. It is 137 commits behind `main` / 1 ahead; re-cut may be cheaper than rebase (planner's call, see Open Q4).
- **P4-08 design spec gates the Wave-B frontend batches** (per `P4-08-FE.md`: "Run designer first").
- **C1 → C2 → C3 → C4** strictly sequential (contract → comparator → seed → api-test). **C5 needs only C1** (the payload is already decided; C5 can start once the question-content shape note exists) but **its e2e (D1 Matching flow) needs C3 seed data**.
- **B3 (hearts) interacts with C5** at the lesson player (heart-loss on wrong answer) — not blocking, but whichever lands second wires the integration.
- **D1 runs last** (needs A1–A5, B1–B8, C5 merged).
- **A4 (Reports) and A5-if-parent-surface sit on the unmerged `feat/parent-dashboard-uiux` shell** (redesigned `(parent)/_layout.tsx`, `activeChildStore` child-switcher) — merge order must be decided first (Open Q2).

### Safe parallel lanes (Mode A / worktrees per PARALLELISM.md)
- **Lane 1 (admin app):** A1 — `apps/admin-dashboard` only, zero overlap with everything else.
- **Lane 2 (auth screens):** A2 + A3 as one batch (same `(auth)/_components` + i18n file).
- **Lane 3 (parent web):** A4 (+ A5 if parent-surface) — after the Open-Q2 shell decision.
- **Lane 4 (backend, Wave C C1–C4):** Learning module only — fully parallel with all FE lanes; no Program.cs / `Shared.Contracts` / `Directory.Packages.props` change expected (payload travels as the existing jsonb answer string; flag immediately if that assumption breaks).
- **Lane 5 (gamification, Wave B):** B0 first, then screen batches; **B1–B4, B6, B8 dashboard integrations all edit `app/(child)/index.tsx` + `(child)/_layout.tsx`** — see hazards; run screens-in-parallel only if dashboard wiring is serialized into a dedicated integration batch.

### Cross-item shared-file hazards (serialize edits)
1. **`packages/shared/src/i18n/resources.ts`** — touched by A2, A3, A4, A5, every B item, C5. Highest-contention file; one batch at a time or partition by key namespace with a merge owner.
2. **`apps/student-app/app/(child)/index.tsx`** (dashboard) + **`(child)/_layout.tsx`** (nav/routes) — B1–B6, B8 all integrate here; the P4-08 branch also modifies `index.tsx`. Recommend one "dashboard integration" batch after the individual screens.
3. **`packages/api-client` regen** — whole-file generated artifact (B0/B1/B4/B5/B6/B8, A5, C5 want hooks). Regen **once per wave** by a single agent, preserving the Host `CustomOperationIds` `/Me` fix; never two parallel regens.
4. **`(parent)/_layout.tsx` / parent shell** — A4/A5 vs the unmerged `feat/parent-dashboard-uiux` branch (and B7's parent settings panel touches `(parent)/settings.tsx`, which that branch also reworked).
5. **`tests/e2e/` configs** — Wave D plus the untracked spec/config files currently sitting in the worktree (`playwright.parent-verify.config.ts` etc. from the parent-dashboard work) — committer must not sweep unrelated untracked files into carryover PRs.
6. **Backend:** `LearningSeeder.cs` (C3) is also a QC-sensitive file (Phase-2 QC fixed Published-state seeding there) — keep the P2 suite (415 tests) green; no Program.cs or cross-module edits anticipated in the whole backlog.
7. **No `git stash`/`reset`/`checkout` in parallel implementer/reviewer agents** (memory rule — shared worktrees).

### Suggested batch order (input to planner, not binding)
1. Wave A lanes 1–2 + Wave C backend lane in parallel; A4 after the Open-Q2 decision.
2. B0 rebase + P4-08 design spec (parallel with 1).
3. Wave B screen batches (B1+B2+B3 core loop → B4+B5+B6 collections/competition → B8 → B7 last, it has the security gate), with a serialized dashboard-integration step; C5 alongside once C1 is written.
4. D1 close-out.

---

## 5. Open questions for the lead (do NOT invent answers)

1. **Attempt-history (A5) surface placement** — decision #2 says in-scope *now*, but the original G5 question is unanswered: student-facing "my past attempts" in `app/(child)/`, parent-facing "child attempt review" in `app/(parent)/` (e.g. the Activity nav item or inside Reports), or both? Also: file it under a new `CO-FE-7` ID or reopen `P2-09-FE`? Affects designer spec, route, and the e2e authz matrix.
2. **`feat/parent-dashboard-uiux` merge order** — the redesigned parent shell (shared `_layout.tsx`, active-child switcher, `activeChildStore`, settings rework) is an unmerged branch off `main` (commit `b8c5356`, reviewer-PASSed, no PR open). Should it merge **before** A4 (Reports) / B7 (parent settings) so they build on the new shell, or do A4/B7 build on `main`'s shell and rebase later? Building Reports against the old shell then re-platforming is wasted work.
3. **P4-09-FE vs Phase-9 boundary** — Phase 9 (scoped 2026-06-11, not implemented) carves out P9-01 (push/token registration), P9-02 (deep-link + web fallback), P9-03 (in-app inbox), P9-04 (parent per-child controls) — i.e. most of `P4-09-FE.md`'s task list. The lead brief's Wave B words it as "in-app nudge surface". Confirm the carryover scope: inbox + deep-link + parent controls now (P4-09-FE-2/3/4/5) and **push registration (P4-09-FE-1) deferred to P9-01**, or the full P4-09-FE including push?
4. **P4-08 branch: rebase vs re-cut** — 137 behind / 1 ahead, predates the P8 localization foundation and the NSwag `/Me` operation-id fix (its generated-client hooks are likely incompatible). Planner/frontend may prefer cherry-picking the motion primitives onto a fresh branch. Confirm the lead has no attachment to preserving the branch history.
5. **Reports "Send Report" action depth** — task file says "Send Report stub" is acceptable; P1-11g AC just says "a Send Report action". Confirm a disabled/toast stub is acceptable for this wave (real delivery presumably lands with P5-04 weekly reports / Phase-9 email work).
6. **Wave-B screen inventory vs nav** — adding badges/missions/league/streak screens implies child-app navigation changes (tab bar or dashboard entry points). The mobile captures show a bottom TabBar that was "intentionally not built yet" (P1/P2 audit note). Does the carryover include introducing the TabBar (a designer decision with app-wide impact), or are the new screens reached via dashboard tap-throughs only for now?

---

## 6. Stale-tracker corrections found while verifying (for the committer/lead to fix alongside)

- `tasks/Frontend/student-app/Phase-3-Gamification/P4-06-FE.md` header says "backend 🟡 in progress on `feat/P4-06-missions`" — **stale**: merged via PR #78 (`e2530a0`). P4-06-FE is **not blocked**.
- `tasks/Frontend/student-app/Phase-1-Foundation/P1-11-FE.md` marks FE-9 (Reports) ✅ — **stale/incorrect**: `main`'s `(parent)/reports.tsx` is a "comingSoon" stub (verified).
- `tasks/Frontend/student-app/Phase-3-Gamification/P4-07-FE.md` contract names `GET /api/Gamification/League/Me` — actual route is **`Leagues/Me`** (plural).
- P4-xx-FE task files target `app/(student)/…` — actual route group is **`app/(child)/`**.
