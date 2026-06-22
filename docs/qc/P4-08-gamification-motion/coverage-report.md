# P4-08 — Gamification Screens & Motion — Coverage Report

**Story:** `user-stories/Phase-3-Gamification/P4-08-gamification-screens-and-motion.md`
**Design Spec:** `design-system/ui_kits/gamification/P4-08.md`
**Test catalog:** `docs/qc/P4-08-gamification-motion/frontend-test-cases.md`
**Surface:** student-app web PWA (Expo), child role. Backend: none new (consumes P4-02..P4-07 endpoints + dashboard DTO).

---

## 1. Summary

- **Total cases:** 41 (all frontend / web-E2E; no backend surface in this story).
- **By section:** Reduce-motion 15 · A11y 9 · RTL/EN 6 · Level-up/XP 4 · Badge unlock 3 · League promo/demotion 5 · Missions/Hearts/Streak motion 9 · Negative/edge 5. *(Section totals overlap where a case carries multiple tags; the 41 figure is distinct case IDs TC-01..TC-84.)*
- **By priority:** P0 = 24 · P1 = 14 · P2 = 3.
- **By tag:** `[REDUCE-MOTION]` = 15 (TC-01..15) · `[A11Y]` = 9 (TC-20..28) · `[RTL]` = 6 (TC-30..35) · `[TRIGGER]` = 6 diff-gated (TC-40, 50, 60, 61, 70, + 13).
- **Emphasis (per lead):** reduce-motion + a11y are the deepest sections — every one of the Design Spec §7.3 18 matrix rows is mapped (see §3).

---

## 2. Acceptance-criterion → test-case traceability

| # | Acceptance criterion (story) | Covering case IDs | Verdict |
|---|---|---|---|
| AC-1 | Screens exist: Reward, Badge collection, League, Missions, Hearts/Practice | TC-01..14, 24, 30..33, 40..43, 50..52, 60..64, 70..78 (each screen exercised) | COVERED |
| AC-2a | Motion: **XP fill** | TC-01 (reduce), TC-08, TC-30, TC-41 | COVERED |
| AC-2b | Motion: **badge pop-in** | TC-03 (reduce), TC-50, TC-51 | COVERED (trigger-gated — see risks) |
| AC-2c | Motion: **confetti** | TC-03 (reduce=none), TC-23, TC-40, TC-50, TC-60, TC-62, TC-70 | COVERED |
| AC-2d | Motion: **shake on wrong answer** | TC-15 (out-of-scope per OD-4 — quiz screen, not P4-08) | NOT IN P4-08 SCOPE (documented) |
| AC-2e | Motion: **animated flame** | TC-04 (reduce static+glow), TC-76, TC-80 (Skia degrade) | COVERED |
| AC-3a | Animations respect kid-accessibility — **clear visual feedback on every action** | TC-20..28 (a11y), TC-21 (text-not-only-animation), TC-43/52/63/73 (no false celebration) | COVERED |
| AC-3b | **Perform smoothly on mobile** (NFR-1/NFR-7) | TC-80, TC-81 (graceful degrade); confetti budget enforced in `ConfettiLayer` (MAX_PARTICLES=24) | PARTIAL — perf FPS not E2E-measurable (see open questions OQ-4) |
| AC-4 | Screens render in **Arabic (RTL) and English** | TC-30..35, TC-83 | COVERED |

**Coverage verdict:** Every acceptance criterion has ≥1 P0/P1 case **except** AC-2d (wrong-answer shake), which the Design Spec OD-4 explicitly scopes to the quiz screen, not P4-08 — recorded as out-of-scope rather than a gap. AC-3b is partially covered (graceful degradation tested; raw FPS/perf is not an E2E concern — flagged).

---

## 3. Reduce-motion matrix coverage (Design Spec §7.3 — all 18 rows)

| # | §7.3 matrix row | Covering case | Status |
|---|---|---|---|
| 1 | RewardPopup card entrance | TC-03 | COVERED |
| 2 | Reward icon pop-in | TC-03 | COVERED |
| 3 | XP bar fill | TC-01, TC-08 | COVERED |
| 4 | XP bar end glow flash | TC-01 | COVERED |
| 5 | Level count-up | TC-02 | COVERED (trigger-gated) |
| 6 | Badge disc pop-in | TC-03 | COVERED |
| 7 | Legendary badge shimmer | TC-14 | COVERED (needs testID G8) |
| 8 | Confetti burst | TC-03, TC-23 | COVERED |
| 9 | Streak flame loop | TC-04 | COVERED |
| 10 | Streak HUD chip loop | TC-04 note (StreakFlame `sm`) | PARTIAL — HUD chip in DashboardHeader (OD-5 scope); covered via StreakFlame component behavior, flagged |
| 11 | Streak milestone stagger | TC-05 | COVERED |
| 12 | Mission row completion flash | TC-06 | COVERED |
| 13 | Mission hero shimmer pulse | TC-07 | COVERED |
| 14 | Heart-break scale+swap | TC-09 | COVERED |
| 15 | Heart-lost info card entrance | TC-10 | COVERED |
| 16 | League banner entrance | TC-11 | COVERED |
| 17 | League you-row pulse | TC-12 | COVERED |
| 18 | League row stagger | TC-12 | COVERED |
| (+) | Promo/demotion popup | TC-13 | COVERED (shared RewardPopup path) |
| (+) | Wrong-answer shake | TC-15 | OUT OF SCOPE (OD-4, quiz screen) |
| (+) | BadgeUnlockOverlay entrance | TC-03 | COVERED (shared RewardPopup path) |

All 18 named matrix rows are mapped. Row 10 (HUD chip) is partially in-scope (DashboardHeader is OD-5); row 7 needs a testID to be non-brittle.

---

## 4. Risk notes (where cases were weighted)

1. **Diff-driven celebrations are the highest-risk + hardest-to-test area.** Level-up, badge-unlock, promotion/demotion, and missions-complete all fire on a *change across two consecutive query responses*. The existing suite already SKIPPED the level-up case (GAM-FE-TC-50) as "not deterministically triggerable." I weighted heavily toward: (a) a documented sequential-`page.route()` trigger technique, (b) a non-trigger fallback assertion for every [TRIGGER] case, and (c) explicit **cold-start safety** cases (TC-43, 52, 63, 73) and **no-duplicate** cases (TC-64, 82) — because a *false* or *duplicate* celebration is worse for a kid UX than a missed one, and those are deterministically testable.
2. **Reduce-motion correctness is release-blocking and most of it IS deterministic** (the static branches render on first load with `reducedMotion: 'reduce'`, no diff needed). This is why 14 of 15 reduce-motion cases are P0 and do not depend on the trigger technique.
3. **No testID on RewardPopup / ConfettiLayer** (confirmed in code) makes overlay/confetti assertions brittle unless the gaps G1–G7 are filled. This is the top blocker to clean implementation of Sections 1–7.
4. **RTL numeral rules are a known correctness trap** (XP Latin-LTR vs prose Eastern-Arabic) — the existing suite's GAM-FE-TC-44 already soft-passed when the hero used Latin digits. TC-33 asserts the split explicitly per the §8.3 table; if the app falls back to Latin for prose level/streak, that is a real defect to surface (not a soft pass).
5. **Skia is never present on web** — flame hue-flicker (DG-3) and Skia confetti are native-only; the web PWA uses the Moti rect fallback. TC-80/81 confirm graceful degradation rather than asserting native-only behavior.

---

## 5. Open questions / assumptions for the lead

- **OQ-1 (blocking for [TRIGGER] cases):** Is there a reliable way to force a TanStack refetch in the harness (refetchOnWindowFocus enabled? a UI action that invalidates? deterministic polling interval)? If not, TC-40/42/50/51/60/61/62/70/71/82 must be marked BLOCKED and only their cold-start/no-duplicate counterparts will run. Precedent: GAM-FE-TC-50 skipped.
- **OQ-2:** Approve adding testIDs G1–G7 (RewardPopup card, confetti layer, badge-unlock overlay, league promo/demotion popups, streak flame, mission hero shimmer, heart slots) before the tester implements? Without them the celebration/confetti assertions are brittle text/role queries.
- **OQ-3:** Badge-unlock overlay is mounted by **Home (batch 3c)**, not the badges screen. Confirm the diff plumbing (`useDashboardDiff` `newBadgeCodes`) is actually wired on Home in the current build — if 3c diff plumbing is still a stub (the xp.tsx header notes "full diff plumbing is batch 3c's seam"), TC-50/51 are BLOCKED on unmerged wiring, not just trigger reproducibility.
- **OQ-4:** AC-3b "perform smoothly on mobile" / NFR-1 — is a perf/FPS smoke test expected, or is the particle budget (MAX_PARTICLES=24, enforced) + graceful degradation sufficient? Design Spec OD-7 recommends a `__DEV__` warning, no formal perf suite. Assuming no E2E FPS assertion unless told otherwise.
- **OQ-5:** Streak HUD-chip flame (matrix row 10) and OD-5 — is `DashboardHeader`'s chip flame in P4-08 scope for testing, or treated as a separate task? Currently covered only via StreakFlame `size="sm"` component behavior.
- **Assumption:** `applyWebDirection()` sets `document.documentElement.lang` not `html[dir]` (confirmed in existing suite TC-43/44). RTL assertions key off `lang`, not `dir`.
- **Assumption:** wrong-answer shake (AC-2d) is out of P4-08 scope per Design Spec OD-4 (quiz screen owns it). Flagged, not counted as a gap.

---

## 6. Handoff

- **`frontend-e2e-tester`** implements `frontend-test-cases.md` as a single spec `tests/e2e/specs/P4-08-gamification-motion.spec.ts`, added to `playwright.child.config.ts` `testMatch`, run `--workers=1`.
- Results (pass/fail per TC + defects + BLOCKED reasons) go into `docs/qc/P4-08-gamification-motion/execution-report.md` (template scaffolded in that folder).
- No backend test file — story exposes no new HTTP surface.
