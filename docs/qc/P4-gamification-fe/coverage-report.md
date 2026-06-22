# Phase 3 — Gamification · Student-App Frontend QC Coverage Report

**Run folder:** `docs/qc/P4-gamification-fe/`
**Scope:** Student-app (Expo/Tamagui web PWA) gamification surfaces only. Backend is fully shipped and separately tested — these are **frontend** cases. Design-only deliverable: the `frontend-e2e-tester` implements `frontend-test-cases.md` and records results in `execution-report.md`.

## 1. Summary

| Metric | Count |
|---|---|
| Total cases | **96** |
| By priority | P0: 30 · P1: 47 · P2: 19 |

**Per-screen / group counts**

| Group | Screen / file | Cases | IDs |
|---|---|---|---|
| 0. Shell nav/auth | `(child)/_layout.tsx` + TabBar | 12 | TC-01…12 |
| 1. Dashboard entry points | `(child)/index.tsx` | 12 | TC-20…31 |
| 2. XP & Level | `(child)/xp.tsx` (P4-02) | 12 | TC-40…51 |
| 3. Streak (+ freeze) | `(child)/streak.tsx` (P4-03/P4-11) | 11 | TC-60…70 |
| 4. Hearts & Practice | `(child)/hearts.tsx` (P4-04) | 11 | TC-80…90 |
| 5. Badges | `(child)/badges.tsx` (P4-05) | 12 | TC-100…111 |
| 6. Missions | `(child)/missions.tsx` (P4-06) | 14 | TC-120…133 |
| 7. League | `(child)/league.tsx` (P4-07) | 12 | TC-140…151 |
| 8. Events/timed/challenges | `(child)/events.tsx` (P4-11/P4-12) | 18 | TC-160…177 |
| 9. Cross-screen/global | all | 7 | TC-190…196 |

Note: P4-08 (screens & motion) and P4-12 (participation) are **cross-cutting** — their cases live inside the relevant per-screen groups (celebration/motion/RTL cases) rather than a standalone section. See traceability below.

## 2. Acceptance-criteria traceability (FE-observable slices only)

> Backend-only criteria (ledger writes, background jobs, idempotency, event emission, no-double-award, cross-module read seams) are **out of FE scope** — listed as *N/A (BE)* so no FE gap is implied.

### P4-02 XP & Levels
| AC (FE slice) | Covered by |
|---|---|
| Level computed from XP, updates on dashboard/XP bar | TC-23, TC-40, TC-41, TC-42 |
| XP/level surfaced honestly (Latin XP, Arabic-digit level) | TC-43, TC-44, TC-46 |
| XP ledger / no-double-award | N/A (BE) |

### P4-03 Streaks
| AC (FE slice) | Covered by |
|---|---|
| Streak state visible (animated flame) | TC-60, TC-62, TC-69 |
| Zero/reset state surfaced (non-shaming) | TC-61 |
| Streak surfaced on dashboard | TC-22, TC-62 |
| Background continuity job | N/A (BE) |

### P4-04 Hearts & Practice Mode
| AC (FE slice) | Covered by |
|---|---|
| Hearts shown; depletion visible | TC-81, TC-82, TC-83 |
| Zero hearts → Practice Mode, not hard-block | TC-84, TC-85, TC-28 |
| Hearts surfaced in UI | TC-21, TC-80, TC-87 |
| Heart-loss triggered by AnswerSubmitted | N/A (BE) |

### P4-05 Badges
| AC (FE slice) | Covered by |
|---|---|
| Earned + locked visible in collection | TC-100, TC-101, TC-102, TC-103 |
| Earning triggers reward popup | TC-111 |
| Per-rarity / hints / dates | TC-104, TC-105, TC-108 |
| Award-by-rule / no-duplicates | N/A (BE) |

### P4-06 Missions
| AC (FE slice) | Covered by |
|---|---|
| Daily/weekly, objective, reward, progress shown | TC-122, TC-123, TC-124, TC-127 |
| Completion grants reward & reflects | TC-125, TC-128 |
| Expired close out | TC-126 |
| Progress from learning events / issuance schedule | N/A (BE) |

### P4-07 Leagues
| AC (FE slice) | Covered by |
|---|---|
| Tier grouping + ranked by weekly XP visible | TC-142, TC-143, TC-147 |
| Standings show current position | TC-143, TC-149, TC-25 |
| Anonymization ("Student #N") | TC-144 |
| Promotion/demotion zones visible | TC-145, TC-148 |
| Promotion job idempotent / scheduled | N/A (BE) |

### P4-08 Gamification screens & motion
| AC (FE slice) | Covered by |
|---|---|
| Screens exist (Reward, Badges, League, Missions, Hearts/Practice) | TC-04, TC-05, TC-07, all per-screen "renders" P0s |
| Motion: XP fill, badge pop-in, confetti, flame, celebration | TC-50, TC-62/69, TC-111, TC-128, TC-167, TC-193 |
| Kid-accessibility / reduced motion | TC-51, TC-69, TC-90, TC-133, TC-195 |
| Renders in Arabic (RTL) and English | TC-43…45, 66/67, 89, 110, 129, 147/148, 174/175, 190/191/192 |

### P4-11 Streak-freeze / timed events / weekly challenges
| AC (FE slice) | Covered by |
|---|---|
| Streak freeze: limited count, earned, no parallel economy | TC-64, TC-161 |
| Timed event surfaces w/ countdown, ends cleanly | TC-163, TC-169, TC-26 |
| Weekly challenges: progress + reward, distinct from missions | TC-170, TC-127 (exclusion side) |
| Config-driven dials / domain events | N/A (BE) |

### P4-12 Timed-event participation
| AC (FE slice) | Covered by |
|---|---|
| Join-by-playing (lazy participation, no row yet) | TC-164 |
| Progress toward target (in-progress) | TC-165 |
| Completion state + celebration | TC-166, TC-167 |
| Eligibility/participant read seams, lifecycle events | N/A (BE) |

**Coverage verdict:** every FE-observable acceptance-criterion slice across P4-02..08/11/12 has ≥1 P0/P1 case. **No FE gaps.** The only uncovered criteria are pure backend concerns (explicitly marked N/A above).

## 3. "Needs seeding" matrix

Render-only = a freshly seeded child (parent register → add child) suffices; the screen renders its first-time/empty/loading state. Activity-seeded = the child must have real gamification state, which requires extra setup beyond add-child.

| Seeding tier | What's needed | Cases |
|---|---|---|
| **Render-only** (add-child suffices) | login + visit screen; empty/loading/first-time/zero states | TC-01…12, 20, 30, 31, 40, 41, 47-49, 60, 61, 65, 68, 70, 80, 81, 87-89, 100, 101, 106, 108-110, 120, 121, 131, 132, 140, 141, 150, 151, 160, 162, 171, 173, 176, 190-192, 194, 196 |
| **Forced-failure (route mock)** | `page.route` abort/500 on a specific endpoint | TC-29, 47, 68, 88, 109, 131, 150, 172, 173 |
| **Activity-seeded — XP/streak/hearts** | child needs XP / streak days / depleted hearts (drive lessons/answers via API, or direct gamification seed) | TC-23(real bar), 28, 42-46, 50, 62-64, 66, 67, 82-86, 90, 24(freeze), 161(>0) |
| **Activity-seeded — badges** | child must have ≥1 earned badge (award via rule/seed) | TC-102-105, 110, 111, 107(unknown code) |
| **Activity-seeded — missions** | issued daily/weekly missions + progress/completed/expired states | TC-122-130, 133 |
| **Activity-seeded — league** | child placed in a league with standings (weekly XP + league run) | TC-25, 142-149 |
| **Admin-seeded — timed events** | superadmin creates+activates a `TimedEvent` (admin endpoints) | TC-26, 163, 164, 168, 169, 174, 175, 177 |
| **Admin + contribution-seeded — participation** | active event **plus** the child contributes to reach in-progress/completed (hardest; same blocker the existing P4-12 spec flags as SKIP) | TC-165, 166, 167 |
| **Multi-event combo** | several reward events in one dashboard refresh | TC-193 |

**Seeding pointers for the e2e stage:**
- Parent/child seed + child UI login helpers already exist in `carryover-d1.spec.ts` (`seedParentAndChild`, `loginAsChild`) and `P4-12-timed-event-participation.spec.ts`.
- Timed-event admin flow already implemented in `P4-12-timed-event-participation.spec.ts`: superadmin Sign-In → `POST /api/Admin/Gamification/TimedEvents` → `.../{id}/activate`.
- **freezeBalance>0**, **league placement**, **earned badges**, **mission progress**, and **participation in-progress/completed** have **no direct UI path** and likely need either a gamification seed endpoint or driving real learning activity (multiple lessons/correct answers within a window). The e2e lead should confirm the fastest seed route before implementing the activity-seeded tier; otherwise these cases are `BLOCKED` with the seed-gap reason (acceptable, but note it).

## 4. Overlap with existing specs (reuse, don't rewrite)

| Existing spec | What it already covers | New GAM cases that EXTEND it (don't duplicate) |
|---|---|---|
| `tests/e2e/specs/carryover-d1.spec.ts` §4 (4a–4r) | TabBar 4 tabs + nav; push screens reachable + bar hidden; each screen renders one of loading/empty/error/data; RTL dir=rtl smoke; XP Latin-digit spot-check; dashboard chips navigate; celebration popup BLOCKED note (4p) | Nav/auth (TC-01…12) reuse 4a–4h directly. Per-screen four-state P0s (TC-40/60/80/100/120/140/160) are the same shape — extend the existing smoke into the populated/empty-specific variants. TC-09/10 (signed-out + parent role) are NOT in carryover — new. Celebration cases (TC-50/111/128/167/193) extend the 4p blocked note with concrete trigger plans. |
| `tests/e2e/specs/P4-12-timed-event-participation.spec.ts` (P412-01…11) | Events screen renders; timed empty card; join-by-playing banner; name+countdown; no "+0 XP"; no raw keys ar+en; protected route redirect; in-progress/completed SKIPPED (seed gap) | TC-160/162/163/164/174/175/176/9 overlap — reuse the seed + admin-event helpers. TC-161 (freeze earned/zero note), TC-168 (overflow), TC-169 (ended drop-off), TC-170/171/172 (weekly-challenge cards + section isolation), TC-167 (completion celebration) are NEW. TC-165/166 (in-progress/completed) match the existing SKIP — same seeding blocker; keep as BLOCKED unless contribution seeding lands. |

**Net new screens with little/no existing dedicated spec:** XP (only Latin-digit spot-check exists), Streak, Hearts, Badges, Missions, League — the bulk of groups 2–7 are genuinely new comprehensive coverage the lighter passes lack.

## 5. Risk notes (where cases are weighted)

1. **Privacy / anonymization (P0):** league must only ever show "Student #N" — TC-144 is the single highest-value privacy assertion (real child names leaking to peers would be a Critical defect). Weighted P0.
2. **Practice-mode-never-blocks (P0):** the product promise is "no hard block at 0 hearts." TC-84/85 verify the soft-regain framing and that the CTA continues learning — a regression here breaks the core kid-UX contract.
3. **Numerals correctness (P0/P1):** XP-Latin vs prose-Eastern-Arabic is a recurring, easy-to-regress SKILL.md rule across 6 screens — covered per-screen (TC-43/44, 66, 129, 147, 174) plus global TC-190/192.
4. **Four-state honesty (P0):** every screen has explicit loading/empty/error/populated branches with honest placeholders (no fabricated calendars/progress/rivals). Heavily covered because data-dependent states are the most fragile under seeding variance.
5. **Celebration timing (P1, fragile):** diff-driven popups (level-up/badge/mission/event-complete) are non-deterministic across refetches — flagged as likely BLOCKED, mirroring the existing 4p note; weighted P1 not P0 to avoid release-blocking on a known harness limitation.
6. **Section error isolation (P1):** events screen isolates challenge-section failures from freeze/timed (TC-172) — a real defect class if a single failed query blanks the whole screen.

## 6. Open questions / assumptions (lead to resolve before implementation)

1. **Seed route for activity states.** Is there a gamification seed/admin endpoint to set XP / streak / freezeBalance / earned badges / mission progress / league placement directly, or must the e2e stage drive real learning activity? This decides whether ~35 activity-seeded cases run or are BLOCKED. (Assumption: no direct seed yet → activity-seeded tier mostly BLOCKED on first run, matching the existing P4-12 SKIP precedent.)
2. **Contribution seeding for participation in-progress/completed (TC-165/166/167).** The existing P4-12 spec marks these SKIPPED for lack of contribution seeding. Confirm whether a "qualifying action" can be triggered hermetically; if not, keep BLOCKED.
3. **Reduced-motion + dark-mode emulation.** Assumption: Playwright `emulateMedia({ reducedMotion, colorScheme })` is acceptable for TC-51/69/90/133/195/196. Confirm the app reads these (it uses `useReduceMotion`).
4. **Route form for push screens.** Assumption: `/(child)/<route>` works in Expo web, with `/<route>` fallback (per existing specs). Confirm canonical URL so the runner doesn't try both blindly.
5. **Parent-role redirect target (TC-10).** Confirm where `useGroupGuard` sends a parent hitting a `(child)` route (parent home vs login) so the assertion is precise.

## 7. Handoff

- **`frontend-test-cases.md`** → `frontend-e2e-tester` (implements all GAM-FE-TC-NN as Playwright tests, reusing `carryover-d1.spec.ts` + `P4-12-…` helpers/specs where overlap is noted).
- **`execution-report.md`** → created as an empty template by the QC stage; the `frontend-e2e-tester` fills the Status per case (PASS/FAIL/BLOCKED/SKIP) + a defect list after running. The QC designer never fills results.
- No backend cases in this run (backend shipped + tested separately) — `backend-test-cases.md` intentionally omitted.
