# Execution Plan — Phase 1/2/3 CARRYOVER backlog (Waves A–D)

> **Planner output, 2026-06-12.** Consumes `docs/briefs/p1-p2-p3-carryover.md` (analyzer brief) +
> `docs/dev/CARRYOVER-P1-P2-P3-LEAD-BRIEF.md` (lead waves A–D) + the locked lead decisions below.
> Consumed by the lead to dispatch agents batch by batch. Plan shape per `docs/plans/P4-02.md` precedent.

## Source

| Artifact | Path |
|---|---|
| Pipeline Brief (spine) | `docs/briefs/p1-p2-p3-carryover.md` |
| Lead wave brief | `docs/dev/CARRYOVER-P1-P2-P3-LEAD-BRIEF.md` |
| Carryover task files | `tasks/Backend/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-BE.md`, `tasks/Frontend/student-app/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-FE.md` |
| Gamification FE task files | `tasks/Frontend/student-app/Phase-3-Gamification/P4-0x-FE.md` (note: routes there say `(student)`; actual group is **`app/(child)/`**; P4-07 endpoint is **`Leagues/Me`** plural) |
| Design system | `design-system/SKILL.md`, `design-system/ui_kits/`, `design-system/screenshots/web/*` + `mobile/*`, `packages/ui`, `packages/design-system` |
| Parallelism rules | `docs/dev/PARALLELISM.md` (Mode A only this run — see §5) |
| Conventions / rulebook | `CLAUDE.md`, `docs/dev/CONVENTIONS.md`, `docs/dev/HANDOFF.md` |

---

## 0. Locked decisions (BAKED IN — no batch may re-open these)

| # | Decision |
|---|---|
| L1 | **Matching answer payload** = `{ "pairs": [{ "leftId": <id>, "rightId": <id> }], "attemptOrder": <int>, "timeMs": <int> }`. Comparator = **order-independent pair-set equality**. Seed includes **all 4 question types** (MCQ, TrueFalse, FillBlank, Matching). FE builds a **real tap/drag pairing UI**. |
| L2 | **Attempt-history (P2-09/G5) ships on BOTH surfaces**: a student screen in `app/(child)/` AND a parent-dashboard surface in `app/(parent)/`. |
| L3 | **Child app gets a NEW app-wide bottom TabBar** — user-approved per CLAUDE.md rule 8 (the one approved new visual pattern this backlog). It is the foundational nav shell that **gates the gamification screens**; planned as **B0-nav**, first Wave-B item, with its own designer spec. |
| L4 | **P4-09-FE is EXCLUDED** — superseded by Phase-9 (P9-01 push, P9-03 inbox, P9-04 parent controls). Former item "B7" is dropped from this plan and logged as carry-forward (§6). |
| L5 | **P4-08 branch: re-cut FRESH off `main`** — do NOT rebase the 137-behind `feat/P4-08-gamification-screens-motion`. Salvage `RewardPopup` / `ConfettiLayer` / `useReduceMotion` / `useDashboardDiff` **by copy**; regenerate the api-client `/Me` hooks against current `main`. |
| L6 | **"Send Report" = stub/toast** this wave (real delivery → P5-04 / Phase-9 email). |
| L7 | **CO-FE-4 marketing ar/RTL = DROPPED** — already on `main`; review-only re-run of the existing marketing e2e suite in D1. |
| L8 | **Parent-dashboard redesign IS on `main`** — A4 Reports and the A5 parent attempt-history surface build directly on the redesigned shell (`(parent)/_layout.tsx`, `activeChildStore` child-switcher). Brief Open-Q2 is resolved. |
| L9 | **Design-system first**: every UI item binds to `design-system/` tokens/components per `design-system/SKILL.md` + the relevant `design-system/ui_kits/<surface>/` kit, reusing `packages/ui` + `packages/design-system`. No new visual patterns beyond the L3 TabBar. Pixel targets = `design-system/screenshots/web/*` (and `mobile/*` for child screens). No API calls in components; server data via TanStack Query hooks in `packages/api-client`, never Zustand. |
| L10 | **Single working branch** `feat/p1-p2-p3-carryover` for the whole backlog — per-batch commits behind reviewer gates, PR(s) at wave boundaries (user instruction; risk flagged in §5). |

---

## 1. Task inventory

Stable batch IDs below are what the lead dispatches by. "DS?" = designer spec needed before the FE work.

| Batch ID | Item | Story / task ID | Stack | One-line scope | Agent pipeline | DS? | Sec-audit? |
|---|---|---|---|---|---|---|---|
| **1a** | A1 | P1-10-FE-6 (P1-10) | Next.js admin | Admin sign-in: uniform invalid-credentials + distinct localized locked/deactivated messages (`apps/admin-dashboard/app/login/page.tsx`) | frontend → security-auditor → reviewer | — (error-copy only) | **Yes** |
| **1b** | A2 | CO-FE-2 / P1-11-FE-15 (P1-11) | Expo web | Student/parent login: same uniform/distinct messaging, en+ar+RTL (`(auth)/_components/LoginForm.tsx`, `packages/shared/src/i18n/resources.ts`) | frontend → security-auditor → reviewer | — | **Yes** |
| **1b** | A3 | CO-FE-3 / P1-11-FE-16 (P1-11) | Expo web | Turnstile CAPTCHA on register, **only when server advertises it**; sends `captchaToken` (`RegisterForm.tsx`) | designer(micro) → frontend → security-auditor → reviewer | micro (placement vs `03-register.png`) | **Yes** |
| **1c** | C1 | CO-BE-1 (P2-06/07) | .NET Learning | Contract note: Matching question-content shape (left/right items, stable ids, localized text) + L1 payload + comparator semantics, in `Learning.Application`; linked from FE task | backend-feature → reviewer | — | — |
| **1c** | C2 | CO-BE-2 (P2-07) | .NET Learning | `AnswerComparator` Matching branch: order-independent pair-set equality; malformed JSON → 422, never unhandled; MCQ/TF/FillBlank unchanged (`Learning.Domain/Services/AnswerComparator.cs` `TODO P2-07.b`) | backend-feature → reviewer | — | — |
| **1c** | C3 | CO-BE-3 (P2-10) | .NET Learning | `LearningSeeder.SeedDemoLessonContentAsync` (`LearningSeeder.cs:455-473`): add TrueFalse + FillBlank + ≥1 Matching, valid `CorrectAnswer` jsonb, Published/active, idempotent; **P2 415-test suite stays green** | backend-feature → reviewer | — | — |
| **1c** | C4 | CO-BE-4 (P2-06) | Integration tests | api-tester: Matching submit e2e — correct (shuffled pair order ⇒ `IsCorrect=true`), wrong, malformed ⇒ 422; granular `StudentAnswer` row persisted (`Learnexia.IntegrationTests`) | api-tester → reviewer | — | — |
| **1d** | DS-pack | — | design | Designer spec pack (docs only, no code): ① **B0-nav child TabBar** spec, ② **P4-08 consolidated gamification screens + motion** spec, ③ **A4 Reports** spec, ④ **A5 attempt-history** spec (both surfaces per L2), ⑤ **C5 Matching pairing-interaction** spec, ⑥ A3 Turnstile micro-spec (feeds 1b) | designer → reviewer (spec sanity vs SKILL.md) | n/a | — |
| **2a** | B0-mot | P4-08 (re-cut) | Expo web | Fresh motion foundation off `main` per L5: copy `RewardPopup`/`ConfettiLayer`/`useReduceMotion`/`useDashboardDiff` into `packages/ui`; **single api-client regen** with NSwag `CustomOperationIds` `/Me` fix preserved; no screens | frontend → reviewer | uses DS-pack ② | — |
| **2b** | B0-nav | NEW (L3) | Expo web | App-wide bottom TabBar in `app/(child)/_layout.tsx` — tab inventory per DS-pack ① (e.g. Home / Learn / Missions / Badges / League per spec), RTL-aware, design tokens, route stubs for not-yet-built tabs | frontend → reviewer | uses DS-pack ① | — |
| **2c** | A4 | CO-FE-1 / P1-11-FE-9 (P1-11g) | Expo web | Parent Reports page on the redesigned shell (L8): KPI cards, per-subject mastery bars (Math/Science/Arabic/English only), date-range selector, Send-Report **toast stub** (L6); chart placeholders reserved for P5-05; en/ar, dark/light (`(parent)/reports.tsx`) | frontend → reviewer | uses DS-pack ③ | — |
| **2d** | A5 | P2-09 / G5 (reopened as CO-FE-7) | Expo web | `useStudentAttempts(studentId)` hook (endpoint `GET /api/Learning/Students/{studentId}/Attempts`, already typed); **child** "my attempts" screen + **parent** per-child attempt review surface (L2), `AttemptSummaryCard` reuse | frontend → security-auditor → reviewer | uses DS-pack ④ | **Yes** (IDOR / child data) |
| **2e** | C5 | CO-FE-5 / P2-06-FE-2 (P2-06) | Expo web | Real tap/drag pairing UI in `packages/ui/src/components/MatchingPanel/` + lesson-player wiring; submits L1 payload with `attemptOrder`/`timeMs`; Submit enabled only when all left items paired; pair/unpair visual state; RTL+en, mobile+desktop | frontend → reviewer | uses DS-pack ⑤ | — |
| **3a** | B1 | P4-02-FE | Expo web | XP screen + level-up moment: animated `XPBar` fill, count-up, confetti via B0-mot; live `GET /api/Gamification/Profile`; immediate dashboard reflection (cache invalidation) | frontend → reviewer | DS-pack ② | — |
| **3a** | B2 | P4-03-FE | Expo web | Streak screen/calendar + animated flame (current + longest) + streak-advanced feedback; dashboard `Streak` snapshot | frontend → reviewer | DS-pack ② | — |
| **3a** | B3 | P4-04-FE | Expo web | Hearts meter in lesson/quiz + heart-break/shake on wrong answer (consistent with P2-07 instant feedback, **wires heart-loss into the C5 MatchingPanel too**) + non-blocking Practice-Mode regain flow | frontend → reviewer | DS-pack ② | — |
| **3b** | B4 | P4-05-FE | Expo web | `BadgeTile` + badge-collection screen (earned/locked grid, rarity) + badge-earned popup; data `GET /api/Gamification/Badges/Me` | frontend → reviewer | DS-pack ② | — |
| **3b** | B5 | P4-06-FE | Expo web | `MissionCard` + missions screen (daily/weekly incl. completed/expired) + mission-complete feedback; data `GET /api/Gamification/Missions/Me` (backend MERGED — task-file "blocked" header is stale) | frontend → reviewer | DS-pack ② | — |
| **3b** | B6 | P4-07-FE | Expo web | League standings screen: tiers Bronze→Diamond, highlighted self, promotion/demotion cutlines, week countdown; data `GET /api/Gamification/Leagues/Me` (**plural**); keep/extend existing `LeaguePreviewRow` | frontend → reviewer | DS-pack ② | — |
| **3b** | B8 | P4-11-FE | Expo web | Freeze balance on streak UI (earned-only, no spend UI); timed-event banner + countdown from `DashboardDto.ActiveTimedEvents` (never call admin endpoint); weekly-challenge cards (`CHALLENGE_*` rows from `Missions/Me`) visually distinct | frontend → reviewer | DS-pack ② | — |
| **3c** | B-int | — | Expo web | **Serialized dashboard + nav integration**: wire B1–B6/B8 screens into `app/(child)/index.tsx` widgets (XP, streak, recent badges top-3, daily-mission widget replacing the null `MissionBanner`, league preview, timed-event banner) and final TabBar routes in `(child)/_layout.tsx`; `useDashboardDiff` celebrations | frontend → frontend-e2e-tester (smoke) → reviewer | — | — |
| **4a** | D1 | CO-FE-6 (P1-11-FE-13 / P2-06) | Playwright | Extend `tests/e2e/`: Reports, locked-login (student+admin), register-with-CAPTCHA (on/off), Matching quiz (happy/wrong/validation), gamification screens (badges/missions/league/streak/hearts: render, live data, RTL), attempt-history **incl. cross-student denial**, TabBar nav; ar+en, auth/role routing; pixel QA vs `design-system/screenshots/web/*`; marketing ar/RTL = review-only re-run | frontend-e2e-tester → reviewer | — | — |
| **4b** | Docs | §6 of brief | docs | Stale-tracker corrections (P4-06-FE header, P1-11-FE FE-9 ✅, `League/Me`→`Leagues/Me`, `(student)`→`(child)`) + final `docs/dev/HANDOFF.md` update | committer (with reviewer sign-off in 4a gate) | — | — |
| — | A6 | P1-12-FE Batch 3 | review-only | MERGED (PR #98). No build. Reviewer confirms in Gate 1; ops sets `GoogleAuth__ClientId` = FE `EXPO_PUBLIC_GOOGLE_CLIENT_ID`. Do NOT touch implicit-grant flow (PKCE = accepted debt) | reviewer note only | — | — |
| — | A7 | CO-FE-4 | DROPPED | On `main` already (L7); D1 re-runs the marketing suite as regression only | — | — | — |
| — | B7 | P4-09-FE | EXCLUDED | Superseded by P9-01/P9-03/P9-04 (L4); logged §6 | — | — | — |

---

## 2. Batches in dependency order

### Batch 1 — kick-off fan-out (Mode A: 1a ∥ 1b ∥ 1c ∥ 1d)
**Entry:** clean tree on `feat/p1-p2-p3-carryover` cut from latest `main` (which already contains the parent-dashboard redesign, L8). The pre-existing untracked `tests/e2e/playwright.*.config.ts` + parent-* spec files from the parent-dashboard work must be committed/removed **first** so they can't be swept into carryover commits.
- **1a (A1)** — admin lockout messaging. Disjoint app (`apps/admin-dashboard`), zero overlap with anything.
- **1b (A2+A3)** — one frontend agent, sequential internally (same `(auth)/_components/` dir + i18n file). A3 waits for DS-pack ⑥ micro-spec (designer can hand it over first, it's tiny).
- **1c (C1→C2→C3→C4)** — one backend lane, **strictly sequential inside the lane** (contract → comparator → seed → api-test). Learning module only; no Program.cs / Shared.Contracts / Directory.Packages.props edits expected — **stop and escalate if that assumption breaks**. C4 (api-tester) also re-runs the full P2 regression suite (415 tests) to prove C2/C3 didn't regress.
- **1d (DS-pack)** — designer produces specs ①–⑥ (docs only → parallel-safe). Priority order: ⑥ (unblocks 1b), ① + ② (gate Wave B), ③④⑤ (gate Batch 2). Specs land in `design-system/ui_kits/student-app/` (①②⑤) and `design-system/ui_kits/parent-dashboard/` (③④-parent; ④-child under student-app).

**Gate 1 (reviewer):** A1/A2/A3 ACs (anti-enumeration: FE never branches user-not-found vs wrong-password; distinct locked/deactivated keys en+ar; CAPTCHA only when advertised, off-behavior preserved) + **security-auditor PASS on A1/A2/A3 bundle**; C1–C4 ACs + green P2 suite + C4 results; DS-pack specs conform to `design-system/SKILL.md` and introduce no unapproved pattern beyond the L3 TabBar. A6 review-only confirmation recorded. **Commit per sub-lane on PASS.**

### Batch 2 — foundations + parent surfaces (Mode A: 2a ∥ 2b ∥ 2c ∥ 2d ∥ 2e)
**Entry:** Gate 1 PASS; DS-pack ①–⑤ approved. File sets are disjoint (see §3 i18n exception):
- **2a (B0-mot)** — fresh motion foundation per L5. **Sole owner of the api-client regen this wave** (preserves NSwag `CustomOperationIds = {Controller}Me`; verify `/Me` hooks keep their names post-regen). Old branch `feat/P4-08-gamification-screens-motion` is left untouched and retired in §6.
- **2b (B0-nav)** — TabBar in `(child)/_layout.tsx`. **Sole owner of `(child)/_layout.tsx` in this batch.** Tabs to not-yet-built screens use route stubs from the spec; B-int finalizes.
- **2c (A4)** — Reports on the redesigned parent shell. Reads existing parent/learning endpoints via api-client hooks; layout-first vs seed data acceptable; chart space reserved per `06-reports.png`; Send Report = toast (L6).
- **2d (A5)** — attempt-history hook + child screen + parent surface (L2). Child route file is NEW under `app/(child)/` (does not edit `_layout.tsx` — B-int adds any tab/entry); parent surface per DS-pack ④ (inside Reports or Activity nav per spec). **security-auditor: IDOR on `studentId`, child-data exposure, parent-link authz.**
- **2e (C5)** — MatchingPanel pairing UI (C1 contract landed in Batch 1). Wires lesson player submit path; heart-loss visual lands later via B3 (the brief's "whichever lands second wires it" — B3 is second by construction).

**Gate 2 (reviewer):** A4 vs DS-pack ③ + P1-11g ACs; A5 vs DS-pack ④ + **security-auditor PASS**; B0-mot primitives demo + regen diff sanity (operation-ids intact); B0-nav vs DS-pack ① (RTL, tokens, mobile/tablet/desktop); C5 vs DS-pack ⑤ + L1 payload byte-for-byte. **Commit per sub-lane on PASS.**

### Batch 3 — gamification screens (gated by 2a + 2b)
**Entry:** Gate 2 PASS. Three sequential sub-batches; **within 3a and 3b the screens run in parallel (Mode A) only because none of them edits `(child)/index.tsx` or `_layout.tsx`** — each builds its own route/screen + components and registers i18n keys per the §3 namespace partition.
- **3a — core loop (B1 XP ∥ B2 streak ∥ B3 hearts).** B3 additionally touches the lesson player (heart meter + MatchingPanel heart-loss wiring) — no other 3a item touches it.
- **3b — collections & competition (B4 badges ∥ B5 missions ∥ B6 league ∥ B8 events/challenges).** B8 and B5 both read `Missions/Me` (B8 filters `CHALLENGE_*`) — read-only overlap, safe.
- **3c — B-int (SEQUENTIAL, single agent).** The only batch allowed to edit `(child)/index.tsx` + finalize `(child)/_layout.tsx` tabs. Wires all dashboard widgets, replaces null `MissionBanner`, hooks `useDashboardDiff` celebrations, removes route stubs. frontend-e2e-tester runs a smoke pass (tab nav + each screen renders ar+en) before the gate.

**Common ACs for every 3x item (from brief §2 Wave B):** ar (RTL) + en; phone/tablet/laptop; design tokens only; visual feedback on every action; motion respects `useReduceMotion`, smooth on mobile (NFR-6/7); TanStack Query hooks only; live `/…/Me` data.

**Gate 3 (reviewer):** per-screen ACs (brief §2 B1–B8 bullets) + DS-pack ② conformance + smoke results. **Commit per sub-batch (3a, 3b, 3c) on PASS.**

### Batch 4 — close-out (sequential: 4a → 4b)
**Entry:** Gates 1–3 PASS; full stack (API + web PWA) running with C3 seed data.
- **4a (D1)** — full Playwright extension per inventory row; includes the attempt-history **cross-student denial** authz spec and Matching-flow specs that depend on C3 seed. Pixel QA vs `design-system/screenshots/web/*` closes P1-11-FE-13.
- **4b** — stale-tracker doc fixes + final HANDOFF.md update (decisions L1–L8 outcomes, carry-forward §6), included in the wave-D commit/PR per CLAUDE.md shared-memory rule.

**Gate 4 (reviewer):** e2e suite green (new + existing, marketing suite re-run green), pixel QA report, docs corrections verified. Then committer opens the final PR.

---

## 3. Shared-file serialization (explicit ownership)

| Shared file | Contended by | Owner / rule |
|---|---|---|
| `packages/shared/src/i18n/resources.ts` | A2, A3, A4, A5, B1–B8, C5 | **Namespace partition + one merge owner per batch.** Namespaces: `auth.*` (1b), `parent.reports.*` (2c), `attempts.*` (2d), `quiz.matching.*` (2e), `gamification.{xp,streak,hearts,badges,missions,league,events}.*` (3a/3b per screen), `nav.tabs.*` (2b). Within each parallel batch, ONE designated agent applies all key merges (Batch 2 → the A4 agent; 3a → B1 agent; 3b → B4 agent); others hand keys over instead of editing the file. Never two batches at once. |
| `app/(child)/_layout.tsx` | B0-nav, B1–B8 (routes), B-int | **2b owns it in Batch 2; 3c (B-int) owns it in Batch 3.** B1–B8 screen agents are FORBIDDEN from editing it — new screens self-register via their route files only; tab/entry wiring is B-int's. |
| `app/(child)/index.tsx` (dashboard) | B1–B6, B8 widgets, `useDashboardDiff` | **3c (B-int) is the sole editor**, full stop. Screen batches export widget components; B-int composes them. |
| `packages/api-client` (generated) | B0-mot, B1/B4/B5/B6/B8, A5, C5 | **2a (B0-mot) performs the single regen for the whole backlog**, preserving the NSwag Host `CustomOperationIds = {Controller}Me` fix (HANDOFF "Durable NSwag /Me fix"). No other agent regenerates. A5's endpoint is already typed; Wave C adds no endpoint (payload travels in the existing jsonb answer string) — if any backend change forces a regen, it routes back through 2a's owner. |
| `backend/.../LearningSeeder.cs` | C3 only | **1c owns it.** QC-sensitive (Phase-2 QC fixed Published-state seeding here): C4 + the 415-test P2 suite must pass before Gate 1. |
| `apps/student-app/app/(parent)/` shell | A4 (`reports.tsx`), A5 parent surface | Redesigned shell already on `main` (L8). 2c owns `reports.tsx`; 2d owns its parent attempt-history file(s); if DS-pack ④ places attempt-history *inside* Reports, 2c+2d collapse into one agent — lead decides at dispatch from the spec. Neither edits `(parent)/_layout.tsx` (no nav change approved beyond child TabBar). |
| `tests/e2e/` configs + specs | D1, pre-existing untracked parent-dashboard files | **4a owns all `tests/e2e/` edits.** Batch-1 entry criterion: dispose of the currently untracked `playwright.parent-verify/quick-capture/settings-*.config.ts` + `parent-*.spec.ts` files (commit on their own branch or delete) so the carryover committer never sweeps them. |
| `Program.cs` / `.sln` / `Shared.Contracts` / `Directory.Packages.props` | none expected | **Zero edits anticipated in the whole backlog.** Any batch that finds it must stop and escalate to the lead before touching them. |
| Worktree hygiene | all parallel agents | **No `git stash` / `reset` / `checkout` in implementer/reviewer subagents** (memory rule — single shared worktree on one branch makes this fatal). |

---

## 4. Review gates & security

| Gate | After | Mandatory checks |
|---|---|---|
| Gate 1 | Batch 1 | reviewer PASS per lane; **security-auditor on A1+A2+A3** (anti-enumeration uniformity, locked/deactivated message keys don't leak cause, Turnstile token handling + fail-closed-when-advertised/fail-open-when-off behavior); **api-tester C4** (Matching correct/wrong/malformed-422 + `StudentAnswer` row) + 415-test P2 regression green; DS-pack conformance. |
| Gate 2 | Batch 2 | reviewer PASS per lane; **security-auditor on A5** (IDOR on `studentId`, linked-parent-only access, no child-PII overexposure); api-client regen diff check (`/Me` operation ids preserved); design-spec conformance for A4/A5/B0-nav/C5. |
| Gate 3 | Batch 3 | reviewer PASS per sub-batch (3a, 3b, 3c) against brief §2 Wave-B ACs + DS-pack ②; frontend-e2e-tester smoke on 3c (tabs + screens render, ar/RTL + en, live data). No security-sensitive items (B7 excluded). |
| Gate 4 | Batch 4 | reviewer PASS on **frontend-e2e-tester D1 full run** (new specs + existing suites + marketing review-only re-run + pixel QA), docs corrections, HANDOFF update present in the PR. |

Critical/High security findings **block the gate** (CLAUDE.md 4b). Reviewer gates additionally enforce: `Successed` envelope untouched (C2), no design pattern introduced without approval (TabBar is the only one, L3), CONVENTIONS.md checklist.

---

## 5. Branch / PR strategy (user-instructed deviation — risks flagged)

**Instruction:** everything runs on the single branch `feat/p1-p2-p3-carryover`; committer commits **per batch after its reviewer gate** and opens **PR(s) at wave boundaries** (suggested: PR-1 after Gate 1 [Wave A polish + Wave C backend], PR-2 after Gate 3 [Wave B], PR-3 after Gate 4 [Wave D]) — NOT per-story branches as CLAUDE.md/PARALLELISM.md normally require.

Risks the lead accepts (logged, with mitigations):
1. **No Mode-B isolation** — all parallel agents share one worktree; a misbehaving agent can clobber siblings. Mitigation: strict §3 file ownership, no-stash rule, disjoint file sets per batch, commit immediately after each gate so good work is anchored.
2. **Coarse revert granularity** — a post-merge problem reverts a whole batch commit, not a story. Mitigation: one commit per sub-lane where gates allow (Gate 1/2 say "commit per sub-lane").
3. **PR review burden** — wave-boundary PRs are large (Wave B especially). Mitigation: per-batch commits with conventional messages give reviewable commit-by-commit history; PR description maps commits → batch IDs → story IDs.
4. **`main` drift** — long-lived branch across 4 batches; if `main` moves (e.g. Phase-9 work starts), rebase risk grows. Mitigation: if PR-1 merges early, fast-forward/rebase the branch on `main` **between batches only**, never mid-batch.
5. **A blocking finding stalls the whole pipeline** — e.g. a Gate-2 security failure on A5 blocks Batch 3 sitting on the same branch. Mitigation: Batch-3 work may proceed on the branch while A5 is remediated *only if* the fix touches no §3 shared file; otherwise serialize.
6. **Branch hygiene at cut time** — current repo state shows `feat/parent-dashboard-uiux` checked out with untracked e2e files. The carryover branch must be cut from **latest `main` after the parent-dashboard merge (L8 asserts it's merged — verify before cutting)**, with the untracked files dispositioned first (§3, Batch-1 entry).

---

## 6. Blockers / carry-forward (explicit, not silently dropped)

| Item | Status | Where it goes |
|---|---|---|
| **P4-09-FE** (push registration, in-app inbox, deep-links, parent nudge controls) | EXCLUDED this backlog (L4) | Phase 9: P9-01 (push/token), P9-03 (inbox), P9-04 (parent per-child controls); deep-link/web-fallback per P9-02. Notifications backend (PR #80) sits ready. |
| Reports **charts** (20-day, time-of-day) | Deferred by design; layout reserves space (2c) | P5-05 parent analytics. |
| **"Send Report" real delivery** | Toast stub only (L6) | P5-04 weekly reports / Phase-9 email. |
| **OAuth implicit-grant → PKCE** | Accepted pre-prod debt — do not re-architect | Pre-production hardening pass. |
| **`GoogleAuth__ClientId` ops config** (= FE `EXPO_PUBLIC_GOOGLE_CLIENT_ID`) | Not a code task | Ops/env checklist; noted in HANDOFF. |
| **Old `feat/P4-08-gamification-screens-motion` branch** | Superseded by 2a re-cut (L5) | Delete after Gate 3 confirms salvaged components shipped; note in HANDOFF. |
| **Native (non-web) QA of TabBar + gamification screens** | D1 covers web PWA only (Playwright) | Native QA pass when native builds enter scope. |
| **P7-09/10/11, native avatar picker, app-side i18n phase** | Out of scope per lead brief | Unchanged. |
| **Attempt-history ID bookkeeping** | A5 reopens P2-09-FE scope; tracked here as CO-FE-7 | 4b records the ID mapping in the task tracker + HANDOFF. |
