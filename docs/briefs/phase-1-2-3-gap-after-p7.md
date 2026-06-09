# Phases 1–3 — Gap Analysis (post-Phase-7 backend)

> Written 2026-06-09. Code-grounded audit of **Phase 1 (Foundation, P1-xx)**, **Phase 2 (Learning Core, P2-xx)**, and **Phase 3 (Gamification, P4-xx)** as they stand on `main` after the Phase-7 admin backend merged. Verified against actual code in `backend/src/Modules/**` and `apps/**`/`packages/**` — not just `tasks/PROGRESS.md` (whose story-level cells are stale in a few places, noted below).
>
> ⚠️ **Repo naming quirk:** PROGRESS.md labels **"Phase 3" = Gamification = `P4-xx` IDs**; the **AI Tutor uses `P3-xx` IDs** and is labeled "Phase 4". This brief covers the three named phases (Foundation/Learning/Gamification) and adds a one-paragraph AI-Tutor status (§5) to cover the ambiguity.

## Headline

**Backend across Phases 1–3 is essentially complete.** Almost every remaining gap is **frontend** — and the single largest is that the **entire Gamification frontend (Phase 3) is unbuilt** while its backend is fully shipped. There is exactly **one real backend gap** (quiz Matching) plus a deferred hardening bundle (P6-06) and the accepted G2 token-revocation gap. Phase 7 added new filters/fields but did **not** break Phases 1–3.

---

## 1. Phase 1 — Foundation (P1-xx)

**Backend: 100% done.** All gaps are frontend, small, and unblocked (their backends are merged).

| Story | BE | FE | Note |
|---|:--:|:--:|---|
| P1-01..09 | ✅ | ✅ | Register / refresh+sign-out / onboarding / link-child / RBAC / infra / design-system / auth screens — all shipped |
| P1-10 Admin sign-in | ✅ | 🟡 | works + role-gates, but **no account-locked/suspended message** (P1-10-FE-6) |
| P1-11 Parent web | — | 🟡 | screens real **except** `(parent)/reports.tsx` + `(parent)/index.tsx` are "coming soon" stubs; lockout/CAPTCHA UI open |
| P1-12 Web account (Batch 2) | ✅ | ✅ | **board's 🟡 is stale** — forgot/reset, Google OAuth, avatar, edit-child all merged (PRs #96/#97/#105); only Apple/MS buttons are intentional placeholders |
| P1-12b / P1-13a / P1-13 / P1-13b | ✅ | — | IUserLookup seam, email delivery, lockout/CAPTCHA/admin-seed, rate-limiting — all merged |

**Open FE sub-tasks (the real remaining work):**
- **P1-10-FE-6 + P1-11-FE-15** — login (admin + student) maps *all* 400/401/403 to a generic "invalid credentials" banner. The backend returns distinct `LoginTooManyFailedAttempts` and `LoginAccountDeactivated` (incl. the new P7 suspended/deleted state) — neither is surfaced. *Medium impact (recovery UX).*
- **P1-11-FE-9** — parent-web **Reports** page is a blank "coming soon" stub (chart-less KPI build; charts themselves are correctly deferred to P5-05). *Low–Med.*
- **P1-11-FE-16** — **Register CAPTCHA** UI: backend Turnstile verifier + `RegisterParentCommand.CaptchaToken` exist; `RegisterForm.tsx` never sends the token. *Low.*

**Cross-cutting backend follow-ups still open:**
- **P6-06 hardening bundle (🔲)** — email localization, `RequireHttpsMetadata` env-gate, CORS guard, Redis-backed rate-limiting. (Timing-oracle is already mitigated in `SignInCommandHandler`; CAPTCHA prod-gating + rate-limiting are already merged.)
- **G2 — access-token revocation (accepted gap)** — no `OnTokenValidated`/blocklist; short-lived access JWTs survive sign-out/suspend/delete until natural expiry.

---

## 2. Phase 2 — Learning Core (P2-xx)

**Both stacks effectively complete.** All P2 stories are ✅ on both BE and (where applicable) FE, with **one real gap**.

**The only real gap — quiz "Matching" question type** (three coupled pieces, all still open):
- **BE comparator** — `Domain/Services/AnswerComparator.cs` still carries the `TODO` and falls through to an `OrdinalIgnoreCase` string compare instead of order-independent pair equality.
- **BE seed** — `Persistence/Seed/LearningSeeder.cs:466` seeds only `QuestionType.MCQ`; no Matching (or TrueFalse/FillInBlank) demo data exists.
- **FE renderer** — `packages/ui/.../MatchingPanel` is a "🧩 coming soon" stub; the lesson player routes Matching to it and **submits an empty answer payload**.

Tracked as `CARRYOVER-P1-P2-gaps-{BE,FE}` (scheduled into the Phase-3 wave; none applied yet). The other 3 question types work end-to-end. *Low product impact, but a stated P2-06 acceptance-criteria miss.*

Everything else attributed to Phase 2 is **deferred-by-design**, not a gap: lesson-player Hearts/XP are hardcoded stubs (Phase 3), AI hints are a Phase-4 stub, the dashboard MissionBanner is null (Phase 3).

**Phase-8 localization:** all 6 student read handlers apply the `learning_language` JWT-claim language guard; the student app references subjects by Id only (server-side scoping) — **no FE gap**.

**Phase-7 impact (verified clean):** the new `IsDeleted != true` (global) + `IsActive == true` + `LifecycleState == Published` filters are present on **every** student read path (browse, skill-tree, lesson, quiz start+resume, dashboard). Existing content was backfilled Published+active, so it still renders; the lesson player handles filtered/empty results gracefully (empty-state / 404 branch). `CorrectAnswer` is admin-only — the student attempt path is unaffected.

---

## 3. Phase 3 — Gamification (P4-xx) — the big gap

**Backend: 100% done** — 6 controllers, 7 Hangfire jobs, all engines (XP/level, streak, hearts/Practice-Mode, badges, missions, leagues, Redis read-model, streak-freeze/timed-events). **Frontend: essentially absent** — only dashboard-header *numbers* and the league *preview row* are merged.

| Story | BE | FE | FE evidence |
|---|:--:|:--:|---|
| P4-01 emit domain events | ✅ | — | infra |
| P4-02 XP & level up | ✅ | 🔲 | number in dashboard header only; no screen/level-up moment |
| P4-03 daily streak | ✅ | 🔲 | count in header only; no streak screen/calendar |
| P4-04 hearts & Practice Mode | ✅ | 🔲 | count + pill in header; no regain-via-practice flow |
| P4-05 badges | ✅ | 🔲 | **no badge UI at all** (no consumer of `/Badges/Me`) |
| P4-06 missions | ✅ | 🔲 | MissionBanner hard-wired null; no mission UI |
| P4-07 weekly leagues | ✅ | 🟡 | only inline `LeaguePreviewRow`; **no standings screen** |
| P4-08 screens & motion | — | 🔲 | nothing merged; WIP only on a stale branch |
| P4-09 re-engagement nudges | ✅ | 🔲 | BE jobs emit notifications; no in-app nudge surface |
| P4-10 Redis realtime | ✅ | — | server-side perf layer; **no FE contract by design** |
| P4-11 streak-freeze/timed events | ✅ | 🔲 | BE jobs + seeds; no FE |

**Backend endpoints with no merged FE consumer:** `GET /Gamification/Badges/Me`, `/Missions/Me`, `/Leagues/Me` (full standings), `/Profile`.

**Unmerged WIP — `feat/P4-08-gamification-screens-motion`:** adds *plumbing, not screens* — api-client hooks (`useGetMy{Badges,Missions,League,Profile}`) + motion primitives (`RewardPopup`, `ConfettiLayer`, `useReduceMotion`, `useDashboardDiff`). It modifies only `app/(child)/index.tsx` (no new screens). **The branch has diverged from `main` (predates P7/P8) and must be rebased** before any gamification FE can land.

**Deferred-by-design (not gaps):** SignalR/push (P4-10 decided server-side caching only), XP-shop/coin economy, parent-grantable freezes (admin-grantable shipped in P7-13). **Accepted MVP risks:** P4-07 cohort-overfill race (R1), XP-before-dashboard ordering (D15), JoinOrder rank collision, `XpAwardedDomainEvent` retry ghost.

**Phase-7 impact (integrated cleanly):** P7-13 admin overrides (league tier / badge+mission catalog `IsActive` / timed events / streak-freeze) + the `AdminActionPerformedEvent` relay, and P7-07's fail-soft `Account*` consumers, all live in the Gamification module. **`IsActive` now gates awards** (verified in `GamificationRepository`/`StudentMissionsQuery`), so the future badges/missions screens must read the live `/…/Me` endpoints (which already reflect active state) — a forward requirement, no current bug (no FE exists to get it wrong).

---

## 4. Phase-7 impact on Phases 1–3 (summary)

Phase 7 added fields/filters but didn't regress Phases 1–3. The only behavioral *leak* into an earlier surface: a now-suspendable account's **sign-in rejection isn't distinctly shown** in the FE — fold into the P1 account-locked message work. `/Me` and `LinkedChildResponse` contracts are unaffected.

## 5. AI Tutor (P3-xx, "Phase 4") — NOT started

If "phase 3" meant the **AI Tutor**: it is **entirely unbuilt, zero code**. Every `P3-01..P3-13` is 🔲, and `backend/src/Modules/` contains only `Gamification, Identity, Learning, Moderation, Notifications, Parent` — **no AI/Gateway/Tutor module**. So the AI gateway, safety layer, tutor prompts, RAG/vector search, adaptivity, spaced repetition, adaptive quizzes, mastery tracking, and tutor UI are all greenfield. This layer also **blocks** P7-09/10/11 and Phase-5 analytics.

---

## 6. Bottom line — what's missing, ranked

1. **The entire Gamification frontend (Phase 3)** — biggest *functional* gap; backend built and unconsumed (missions, badges, league standings, XP/level + motion, hearts/Practice-Mode flow, nudges). Enabling hooks/motion are on a stale branch needing a rebase.
2. **AI Tutor phase (P3-xx)** — entirely unbuilt; the largest *remaining build* overall and the blocker for P7-09/10/11 + Phase 5.
3. **Quiz Matching (Phase 2)** — the one real backend gap (comparator + seed) plus its FE renderer.
4. **Phase-1 FE polish** — account-locked/suspended message, Register CAPTCHA UI, parent-web Reports page.
5. **P6-06 security hardening + G2 token revocation** — deferred, not done.

**Suggested sequencing:** (a) rebase + land the Gamification FE (P4-08 → P4-05/06/07 screens) to make the shipped backend usable; (b) the small Phase-1 FE polish (cheap, improves auth UX, incl. the P7 suspended-account message); (c) quiz Matching (closes the last P2 AC); (d) the AI-Tutor phase as the next major backend build (also unblocks the remaining admin-console stories).
