# P4-08 — Gamification Screens & Motion — Frontend (Web-E2E) Test Cases

**Story:** `user-stories/Phase-3-Gamification/P4-08-gamification-screens-and-motion.md`
**Design Spec (source of truth for motion/timings/fallbacks):** `design-system/ui_kits/gamification/P4-08.md`
**Target agent:** `frontend-e2e-tester` (Playwright, web PWA at `:8081`, child role)
**Surface under test (real code):**
- `packages/ui/src/components/RewardPopup/index.tsx` (level-up / xp / badge-unlock overlay)
- `packages/ui/src/components/BadgeUnlockOverlay/index.tsx` (composes RewardPopup)
- `packages/ui/src/components/StreakFlame/index.tsx` (size-conditional pulse + reduce-motion static glyph+glow)
- `packages/ui/src/internal/ConfettiLayer.tsx`, `packages/ui/src/hooks/useReduceMotion.ts`
- `apps/student-app/app/(child)/{xp,league,missions,hearts,streak}.tsx`

---

## Harness preconditions (apply to EVERY case unless overridden)

- **Config:** `tests/e2e/playwright.child.config.ts` (isolated child config — targets `:8081` only; NOT the 3-webServer default config). Add the new spec to its `testMatch`.
- **Run:** `cd tests/e2e && npx playwright test --config=playwright.child.config.ts --workers=1 specs/P4-08-gamification-motion.spec.ts --reporter=list`. **Single worker / per-file** — Metro is fragile under load.
- **Seed:** real API `register parent -> Add-Child` per the existing `seedParentAndChild()` helper in `specs/P4-gamification-xp-streak-hearts.spec.ts`. Reuse it (or lift to a shared helper). Unique emails per run via `RUN_ID`.
- **Login:** real child UI flow via `signInAsChild(page, email, password)` (`/login?role=student`).
- **Determinism:** intercept DTOs with `page.route()`:
  - Dashboard (XP/streak/hearts): `**/api/Learning/Dashboard`
  - League: `**/api/Gamification/Leagues/Me`
  - Missions: `**/api/Gamification/Missions/Me`
  - Envelope shape: `{ successed: true, errors: null, data: {...} }` (note `successed` spelling).
- **Locale:** Arabic is default (child seeded `language: 'ar'`). `applyWebDirection()` sets `document.documentElement.lang` (NOT `html[dir]`) — assert `lang === 'ar'` for RTL, `lang === 'en'` for LTR. Switch EN via `localStorage.setItem('lx_locale','en')` + reload.
- **Reduce-motion:** create the context with `reducedMotion: 'reduce'` (Playwright maps this to `prefers-reduced-motion: reduce`, which `useReduceMotion()` reads on web via `matchMedia`). `page.emulateMedia({ reducedMotion: 'reduce' })` also works for mid-test toggling.

### Priority legend
- **P0** — blocks release (each celebration triggers / does not falsely trigger; reduce-motion correctness; no raw i18n keys).
- **P1** — should pass (RTL mirror rules, a11y roles/labels, edge states).
- **P2** — nice to have (fine-grained timing, decorative geometry).

### Tag legend
- **[REDUCE-MOTION]** — verifies a row of the Design Spec §7.3 18-row reduce-motion matrix.
- **[A11Y]** — accessibility (roles, labels, focus, dismissibility, text-not-only-animation).
- **[RTL]** — RTL/LTR mirror & numeral rules.
- **[TRIGGER]** — diff-driven celebration that requires sequential-response interception to fire (see "Triggering diff celebrations" below).

---

## Triggering diff-driven celebrations (read before Section 4–7)

Four celebrations fire only on a **change detected across two consecutive query responses** (cold-start safe — never on first load):

| Celebration | Screen | Diff source | Endpoint to sequence |
|---|---|---|---|
| Level-up RewardPopup | xp.tsx | `useDashboardDiff` `levelDelta > 0` | `**/api/Learning/Dashboard` |
| Badge unlock overlay | Home (3c) | `useDashboardDiff` `newBadgeCodes` | `**/api/Learning/Dashboard` |
| League promotion/demotion | league.tsx | `prevTierRef` tier change | `**/api/Gamification/Leagues/Me` |
| Missions complete | missions.tsx | mission status flip to Completed | `**/api/Gamification/Missions/Me` |

**Technique (the tester must implement):** install a `page.route()` handler whose response **changes on the Nth call** — first fulfilment returns the baseline DTO (cold-start, no celebration), then trigger a refetch (TanStack refetch on `refocus`/`invalidate`, or `query.refetch()` via a UI action / `window` focus event) and have the handler return the changed DTO on the 2nd call. Use a closure counter:

```
let call = 0;
await page.route('**/api/Gamification/Leagues/Me', async (route) => {
  call += 1;
  await route.fulfill({ ..., body: JSON.stringify(call === 1 ? bronzeDto : goldDto) });
});
```

Then drive a refetch (e.g. `await page.evaluate(() => window.dispatchEvent(new Event('focus')))` if refetchOnWindowFocus is on, or navigate away+back). **If a reliable refetch trigger cannot be produced in the harness, mark the case BLOCKED** with the reason (precedent: GAM-FE-TC-50 level-up was skipped as "not deterministically triggerable"). Each [TRIGGER] case below names a fallback assertion that does NOT need the second refetch, so partial coverage is still recorded.

---

## Section 1 — Reduce-Motion suite (CRITICAL) — maps Design Spec §7.3 matrix

All cases in this section run under a context created with `reducedMotion: 'reduce'`. One case per matrix row where feasible; grouped where the row is not independently observable on the web PWA. **Core assertion pattern:** the final/static state is present and complete (the celebration/screen is legible without motion), and NO looping/confetti animation runs.

### P4-08-TC-01 — [REDUCE-MOTION][P0] XP bar renders at target instantly (no fill animation)
- Matrix rows: *XP bar fill*, *XP bar end glow flash*.
- Preconditions: reduce-motion context; `mockDashboard({ xp: 200, level: 2 })`; AR locale.
- Steps: 1) sign in. 2) goto `/(child)/xp`. 3) wait for `xp-progress-card`. 4) read the `progressbar` element width / `accessibilityValue.now`.
- Expected: progress bar is at its target fill immediately (no 0%→target transition observable); `progressbar` role present with `now/max` matching `xpIntoLevel/windowSpan`; no glow-flash element animates. (Code: `xp.tsx` renders a static `TamStack width={fillPct}` when `reduceMotion`.)
- Traces to: AC "Animations respect kid-accessibility"; Spec §1.2, §7.3.

### P4-08-TC-02 — [REDUCE-MOTION][P0] Level count-up jumps to final value (no RAF tween)
- Matrix row: *Level count-up*.
- Preconditions: reduce-motion context. Level-up is diff-gated; the count-up only runs post-dismiss when `!reduceMotion`. Under reduce-motion `handleDismissCelebration` returns early.
- Steps: 1) attempt the level-up trigger ([TRIGGER] technique) OR assert the static hero. 2) read `xp-hero` text.
- Expected: hero level number equals the live `level` with no intermediate increment; even if the popup fired, dismiss shows the final level instantly. If trigger not reproducible, assert hero renders the final level statically (no count-up). 
- Traces to: Spec §1.3, §7.3.

### P4-08-TC-03 — [REDUCE-MOTION][P0] RewardPopup renders instantly, no confetti loop
- Matrix rows: *RewardPopup card entrance*, *Reward icon pop-in*, *Confetti burst*, *Promo/demotion popup*, *BadgeUnlockOverlay entrance* (all share the RewardPopup + ConfettiLayer fallback).
- Preconditions: reduce-motion context. Use any reproducible RewardPopup mount (missions-complete via [TRIGGER], or BLOCKED fallback: unit-level note that ConfettiLayer returns `null` under reduce-motion — `ConfettiLayer.tsx` line ~208 `if (reduceMotion || !active) return null`).
- Steps: 1) cause a RewardPopup to mount. 2) assert the card/title/CTA are visible immediately. 3) assert NO confetti particle nodes exist in the overlay.
- Expected: popup card + title + CTA present with no scale/opacity entrance; `ConfettiLayer` renders nothing (zero particle rects). The celebration is still complete and legible.
- Traces to: Spec §1.1, §1.4, §7.3.

### P4-08-TC-04 — [REDUCE-MOTION][P0] Streak flame is static WITH glow (no loop)
- Matrix row: *Streak flame loop*.
- Preconditions: reduce-motion context; `mockDashboard({ streak: 5 })`.
- Steps: 1) sign in. 2) goto `/(child)/streak`. 3) wait for `streak-hero`. 4) inspect the flame glyph element.
- Expected: 🔥 glyph rendered statically (no `MotiView` wrapper — `streak.tsx` returns bare `flameGlyph` when `reduceMotion`); the glow `textShadow` is still applied (radius 14). No scale loop. 
- Traces to: Spec §6.1, §7.3.

### P4-08-TC-05 — [REDUCE-MOTION][P0] Streak milestone markers render at final state (no stagger)
- Matrix row: *Streak milestone stagger*.
- Preconditions: reduce-motion context; `mockDashboard({ streak: 5 })`.
- Steps: 1) goto `/(child)/streak`. 2) wait for `streak-milestones`. 3) assert all 4 markers (3/7/14/30) are visible immediately.
- Expected: all markers at full scale/opacity instantly (`streak.tsx` returns bare `markerContent` when `reduceMotion`); reached markers show ✓, upcoming show the number.
- Traces to: Spec §6.3, §7.3.

### P4-08-TC-06 — [REDUCE-MOTION][P0] Mission row completion flash skipped
- Matrix row: *Mission row completion flash*.
- Preconditions: reduce-motion context; `mockMissions` populated with a daily row.
- Steps: 1) goto `/(child)/missions`. 2) inspect a `mission-row-*` element.
- Expected: the absolutely-positioned `$successSoft` flash overlay is NOT rendered (`missions.tsx` guards `{!reduceMotion ? <MotiView .../> : null}`). Row content still legible.
- Traces to: Spec §4.1, §7.3.

### P4-08-TC-07 — [REDUCE-MOTION][P0] Mission hero shimmer not rendered
- Matrix row: *Mission hero shimmer pulse*.
- Preconditions: reduce-motion context; `mockMissions` with at least one incomplete daily (so `heroXp > 0` and `missions-hero` shows).
- Steps: 1) goto `/(child)/missions`. 2) wait for `missions-hero`. 3) assert no white shimmer overlay node inside the hero.
- Expected: hero renders static at full opacity; the white shimmer `MotiView` is absent (guarded by `!reduceMotion`).
- Traces to: Spec §4.3, §7.3.

### P4-08-TC-08 — [REDUCE-MOTION][P0] Mission progress bar at target instantly
- Matrix row: *XP bar fill* family (mission row bars).
- Preconditions: reduce-motion context; `mockMissions` with an in-progress daily (ratio between 0 and 1).
- Steps: 1) goto `/(child)/missions`. 2) inspect the in-progress row's progress bar.
- Expected: fill rendered at target width via static `TamStack` (no `MotiView width 0%→target`); `progressbar` role present with correct `now/max`.
- Traces to: Spec §4.5, §7.3.

### P4-08-TC-09 — [REDUCE-MOTION][P0] Heart-break is instant gray-out (no scale, no glyph swap)
- Matrix row: *Heart-break scale+swap*.
- Preconditions: reduce-motion context; `mockDashboard({ hearts: 3 })`.
- Steps: 1) goto `/(child)/hearts?lost=1`. 2) inspect the first empty heart slot (index = `hearts`).
- Expected: lost slot renders at `opacity 0.3` immediately; glyph stays ❤️ (no 💔 mid-flash); no scale animation (`BigHeart` returns bare `node` when `reduceMotion`).
- Traces to: Spec §5.2, §7.3.

### P4-08-TC-10 — [REDUCE-MOTION][P1] Heart-lost info card renders at opacity 1 (no fade-in)
- Matrix row: *Heart-lost info card entrance*.
- Preconditions: reduce-motion context; `mockDashboard({ hearts: 3 })`.
- Steps: 1) goto `/(child)/hearts?lost=1`. 2) wait for `hearts-lost-card`.
- Expected: card visible immediately (reduce-motion branch renders the bare `XStack`, no `MotiView` fade). Title `hearts.lost.title` + sub `hearts.lost.sub` present.
- Traces to: Spec §5.4, §7.3.

### P4-08-TC-11 — [REDUCE-MOTION][P1] League banner renders at final position (no entrance)
- Matrix row: *League banner entrance*.
- Preconditions: reduce-motion context; `mockLeague` populated (tier Bronze, standings).
- Steps: 1) goto `/(child)/league`. 2) wait for `league-banner`.
- Expected: banner visible immediately at final position (`league.tsx` renders the bare `GradientBox` when `reduceMotion`, no translateY entrance).
- Traces to: Spec §3.2, §7.3.

### P4-08-TC-12 — [REDUCE-MOTION][P1] League rows render at final state; you-row pulse skipped
- Matrix rows: *League row stagger*, *League you-row pulse*.
- Preconditions: reduce-motion context; `mockLeague` with ≥6 standings incl. a you-row.
- Steps: 1) goto `/(child)/league`. 2) assert all `league-row-*` and `league-you-row` visible immediately.
- Expected: all rows at full opacity/position instantly (no translateX stagger); you-row shows highlight but no scale pulse (`renderRow` returns bare `rowContent` when `reduceMotion`).
- Traces to: Spec §3.3, §3.4, §7.3.

### P4-08-TC-13 — [REDUCE-MOTION][P1] Promotion/demotion popup uses RewardPopup reduce-motion fallback
- Matrix row: *Promo/demotion popup*.
- Preconditions: reduce-motion context; league tier-change [TRIGGER] (or BLOCKED fallback).
- Steps: 1) trigger tier change. 2) when the popup appears, assert no confetti + instant card.
- Expected: same as TC-03 (shared RewardPopup path). If trigger not reproducible, mark BLOCKED and cite TC-03 for the shared fallback proof.
- Traces to: Spec §3.1, §3.5, §7.3.

### P4-08-TC-14 — [REDUCE-MOTION][P2] Legendary badge shimmer skipped (static disc)
- Matrix row: *Legendary badge shimmer*.
- Preconditions: reduce-motion context; badges screen / BadgeUnlockOverlay with a legendary badge.
- Steps: 1) render a legendary badge (via badges screen or unlock overlay). 2) inspect disc.
- Expected: disc renders static, no hue-rotate loop. NOTE: shimmer is web-CSS / Skia (DG-3) — on the web PWA assert the disc is present and no infinite animation is attached. Likely needs a testID on the legendary disc (see gaps).
- Traces to: Spec §2.2, §7.3.

### P4-08-TC-15 — [REDUCE-MOTION][P2] Wrong-answer shake → border-flash only
- Matrix row: *Wrong-answer shake*.
- Status: **OUT OF SCOPE / BLOCKED** — the shake lives on the quiz answer element, not the hearts screen (Design Spec OD-4: FE-5 owns hearts-screen arrival motion only; the shake belongs to the quiz screen). Record as not-applicable-to-P4-08 with the OD-4 reference; do NOT write an executable case here.
- Traces to: Spec §5.3, OD-4, §7.3.

---

## Section 2 — Accessibility ([A11Y])

### P4-08-TC-20 — [A11Y][P0] RewardPopup announces as assertive alert with full content
- Preconditions: any reproducible RewardPopup mount (missions-complete [TRIGGER]; fallback BLOCKED).
- Steps: 1) mount popup. 2) read the card's `role` and `aria-label`.
- Expected: card has `accessibilityRole="alert"` (web `role="alert"`) + `accessibilityLiveRegion="assertive"`; `aria-label` includes the achievement detail, not just the title — e.g. missions: `missions.complete.a11y` "...You earned {xp} XP." (`RewardPopup` sets `accessibilityLabel`/`aria-label` on the card).
- Traces to: AC "kid-accessibility, clear visual feedback"; Spec §8.1.

### P4-08-TC-21 — [A11Y][P0] Achievement is conveyed in TEXT, not animation-only
- Preconditions: as TC-20.
- Steps: 1) mount popup. 2) read visible text nodes.
- Expected: title + subtitle (+ XP amount when `xpAmount>0`) are real text, present even with confetti suppressed (cross-check under reduce-motion in TC-03). No information lives only in the confetti/animation. Decorative emoji carry `accessibilityElementsHidden`.
- Traces to: AC "clear visual feedback on every action"; Spec §8.1, §8.2.

### P4-08-TC-22 — [A11Y][P0] Celebration is dismissible and does not trap focus permanently
- Preconditions: as TC-20.
- Steps: 1) mount popup. 2) locate the CTA button (by accessible name = ctaLabel). 3) click it. 4) assert the overlay unmounts.
- Expected: CTA present, reachable, and clicking calls `onDismiss` → overlay removed; underlying screen interactive again. (Code: `Button onPress={onDismiss}`.) NOTE: RewardPopup currently has no true focus-trap/Modal wrapper (comment: "true Modal wrapping is the app's responsibility — P1-09"); the case asserts dismissibility + that the page is not permanently blocked, and flags the missing focus-trap as an a11y observation, not a hard fail for P4-08.
- Traces to: Spec §8.1.

### P4-08-TC-23 — [A11Y][P0] Confetti is decorative — hidden from AT and non-interactive
- Preconditions: NON-reduce-motion context; a RewardPopup with confetti (missions-complete [TRIGGER] or BLOCKED).
- Steps: 1) mount popup with confetti. 2) inspect the confetti layer.
- Expected: layer has `accessible={false}` / `accessibilityElementsHidden` and `pointerEvents="none"` (`ConfettiLayer` web fallback sets these on the wrapping `Stack`); not announced; not focusable.
- Traces to: Spec §8.1.

### P4-08-TC-24 — [A11Y][P1] Hearts row exposes role=text + "X of Y" label + value
- Preconditions: `mockDashboard({ hearts: 3 })`.
- Steps: 1) goto `/(child)/hearts`. 2) read `hearts-row` attributes.
- Expected: `accessibilityRole="text"`, `aria-label` = `hearts.rowA11y` ("3 of 5 hearts remaining" / "٣ من ٥ قلوب متبقية"), `accessibilityValue {min:0,max:5,now:3}`. (Code present in `hearts.tsx`.)
- Traces to: Spec §8.1.

### P4-08-TC-25 — [A11Y][P1] Progress bars expose role=progressbar with value (XP + mission)
- Preconditions: `mockDashboard({ xp:200, level:2 })` for xp; `mockMissions` in-progress for missions.
- Steps: 1) on each screen, query `role=progressbar`. 2) read `aria-valuenow/min/max`.
- Expected: ≥1 `progressbar` on each screen with `accessibilityValue` matching the data; xp bar additionally has an `aria-label` describing progress (`xp.progress.barA11y`).
- Traces to: Spec §8.1.

### P4-08-TC-26 — [A11Y][P1] Streak flame container labelled; emoji hidden
- Preconditions: `mockDashboard({ streak:5 })`.
- Steps: 1) goto `/(child)/streak`. 2) read `streak-hero` aria-label; inspect 🔥 node.
- Expected: hero has a descriptive `aria-label` (`heroA11y` = eyebrow + days + meta); the 🔥 emoji has `accessibilityElementsHidden`. (StreakFlame component container `accessibilityRole="text"` + label when used in HUD.)
- Traces to: Spec §6, §8.1, §8.2.

### P4-08-TC-27 — [A11Y][P1] Touch targets ≥ 48px on celebration CTA + back buttons
- Preconditions: push screens reachable.
- Steps: 1) on xp/streak/hearts, measure the back `Pressable` bounding box. 2) on a RewardPopup, measure the CTA height + hitSlop.
- Expected: back buttons render at `minWidth/minHeight: 48`; RewardPopup CTA is 44px height + `hitSlop {top:4,bottom:4}` → effective ~52px. (Code present.)
- Traces to: Spec §8.1.

### P4-08-TC-28 — [A11Y][P1] Hearts-lost card announces via polite live region
- Preconditions: `mockDashboard({ hearts:3 })`.
- Steps: 1) goto `/(child)/hearts?lost=1`. 2) inspect `hearts-lost-card`.
- Expected: card carries `accessibilityLiveRegion="polite"` so the "lost a heart" message is announced without stealing focus. Title/sub are text.
- Traces to: Spec §5.4, §8.1.

---

## Section 3 — RTL (ar) + EN ([RTL])

### P4-08-TC-30 — [RTL][P0] XP & mission progress bars NEVER mirror (LTR-locked in AR)
- Preconditions: AR locale; `mockDashboard({ xp:200, level:2 })` + `mockMissions` in-progress.
- Steps: 1) on xp + missions screens, confirm `lang==='ar'`. 2) verify the progress fill grows from the visual LEFT (fill element is leading-left regardless of locale).
- Expected: bars are LTR-locked (fill from left) in AR; `progressbar` present. (Code: bars use fixed left-anchored fill, not `rowDir`.)
- Traces to: AC "render in Arabic (RTL) and English"; Spec §1.2, §4.5, §8.3.

### P4-08-TC-31 — [RTL][P0] League row stagger slides from logical-start (RTL positive X, LTR negative X)
- Preconditions: NON-reduce-motion; AR then EN; `mockLeague` with ≥6 standings.
- Steps: 1) AR: goto `/(child)/league`; 2) EN: switch locale + reload + goto league.
- Expected: rows animate in from the logical-start side — `fromX = isRtl ? 8 : -8` (`renderRow`). Hard to assert mid-flight; assert final layout correct + `lang` value, and (if observable) initial offset direction. Mark fine-grained offset P2 if not observable.
- Traces to: Spec §3.4, §8.3.

### P4-08-TC-32 — [RTL][P0] League zone arrows (↑/↓) do NOT mirror
- Preconditions: `mockLeague` with promotionCutoff/demotionCutoff set so both dividers render; AR locale.
- Steps: 1) goto `/(child)/league`. 2) read `league-zone-promotion` / `league-zone-demotion` label text.
- Expected: arrows render as ↑ (promotion) / ↓ (demotion) in BOTH locales — vertical semantics, never flipped. AR label "↑ منطقة الصعود" / "↓ منطقة الهبوط". (Code: arrows are in copy, not transform-mirrored; row uses `rowDir` for layout but the glyph stays.)
- Traces to: Spec §3.5, §8.3.

### P4-08-TC-33 — [RTL][P0] XP counters Latin even in AR; level/streak/rank prose Eastern-Arabic
- Preconditions: AR locale; `mockDashboard({ xp:200, level:3, streak:5 })`; `mockLeague` populated.
- Steps: 1) xp screen: read XP counter vs hero level. 2) streak: read day count. 3) league: read weekly XP vs rank.
- Expected: XP counters use Latin digits + `writingDirection: ltr` (e.g. "200"); level/streak/rank prose use Eastern-Arabic digits (٣, ٥) via `Intl('ar-EG')`. Per Spec §8.3 numeral table.
- Traces to: Spec §8.3.

### P4-08-TC-34 — [RTL][P1] All P4-08 celebration copy present in AR and EN — no raw i18n keys
- Preconditions: both locales; reproduce each surface where copy renders (popups + screens). For diff popups, use [TRIGGER] or assert the static screens.
- Steps: 1) walk `document.body` text nodes (reuse the GAM-FE-TC-31 TreeWalker regex). 2) extend the namespace regex to include the new keys' roots: `badges.unlock`, `league.promotion`, `league.demotion`, `xp.levelUp`, `missions.complete`, `hearts.lost`.
- Expected: zero raw keys matching `^(xp|streak|hearts|badges|missions|league|events|child|common)\.[\w.]+$` on any P4-08 surface in either locale. (Keys confirmed present in `packages/shared/src/i18n/resources.ts`: `badges.unlock.{title,cta}`, `league.promotion.{title,subtitle}`, `league.demotion.title`, EN+AR.)
- Traces to: AC "render in Arabic and English"; Spec §8.4.

### P4-08-TC-35 — [RTL][P1] Screen layout flips (row-reverse) in AR for hearts/league/missions rows
- Preconditions: AR then EN.
- Steps: 1) on hearts row, league row, mission row — confirm `lang==='ar'` and visual leading element sits on the right in AR, left in EN.
- Expected: `flexDirection: row-reverse` in AR (`rowDir` derived from `isRtl`). Avatar/emoji discs themselves are not internally mirrored.
- Traces to: Spec §8.3.

---

## Section 4 — Level-up / XP celebration ([TRIGGER])

### P4-08-TC-40 — [TRIGGER][P0] Level-up RewardPopup fires on levelDelta>0 across two dashboard refreshes
- Preconditions: NON-reduce-motion; sequential `**/api/Learning/Dashboard` (call 1 = level 2, call 2 = level 3, +XP).
- Steps: 1) goto `/(child)/xp`; baseline loads (no popup). 2) force a dashboard refetch. 3) wait for the level-up popup.
- Expected: popup appears with `xp.levelUp.title`, subtitle `xp.levelUp.subtitle` (level in Eastern-Arabic in AR), XP amount "+{xp} XP" (Latin), level-up palette confetti, CTA `xp.levelUp.cta`.
- Fallback (no refetch reproducible): **BLOCKED** — cite precedent GAM-FE-TC-50; record the trigger limitation. Verify instead that on a SINGLE load NO popup appears (cold-start safe, covered by TC-43).
- Traces to: AC "Motion specs: XP fill, confetti"; Spec §1.1.

### P4-08-TC-41 — [P0] XP bar fills (0→target) on a normal load (non-reduce-motion)
- Preconditions: NON-reduce-motion; `mockDashboard({ xp:200, level:2 })`; AR.
- Steps: 1) goto `/(child)/xp`. 2) observe the bar fill (MotiView width 0%→target over 700ms).
- Expected: bar animates to target then settles; final `progressbar` value correct. Contrast with TC-01 (reduce-motion instant).
- Traces to: AC "XP fill"; Spec §1.2.

### P4-08-TC-42 — [P1] Level count-up runs after popup dismiss (non-reduce-motion)
- Preconditions: as TC-40 with the trigger reproducible.
- Steps: 1) fire level-up. 2) dismiss popup. 3) observe the hero number tick from `fromLevel` to `toLevel` over ~600ms.
- Expected: hero increments to final level; ends on the live level. BLOCKED if trigger not reproducible.
- Traces to: Spec §1.3.

### P4-08-TC-43 — [P0] Cold-start safety: NO level-up popup on first XP-screen load
- Preconditions: fresh child OR `mockDashboard` single response; NON-reduce-motion.
- Steps: 1) goto `/(child)/xp`. 2) wait 3s.
- Expected: no RewardPopup mounts on first load (`useDashboardDiff` emits ZERO_DIFF on cold start). No false celebration.
- Traces to: AC "kid-accessibility"; Spec §1 (cold-start), negative/edge.

---

## Section 5 — Badge unlock overlay ([TRIGGER])

### P4-08-TC-50 — [TRIGGER][P0] BadgeUnlockOverlay fires on new badge across two dashboard refreshes
- Preconditions: NON-reduce-motion; sequential dashboard (call 1 = `recentBadges: []`, call 2 = `recentBadges:[{code,...}]` / newBadgeCodes). Overlay is mounted by Home (3c).
- Steps: 1) sign in (Home). 2) force refetch with the new badge. 3) wait for the overlay.
- Expected: overlay shows `badges.unlock.title` ("New Badge!" / "شارة جديدة!"), badge disc with `isNewlyEarned` pop-in, badge display name as subtitle, multicolor confetti, no XP row (`xpAmount=0`), CTA `badges.unlock.cta` ("Awesome!" / "رائع!").
- Fallback: **BLOCKED** if the diff cannot be forced; record limitation and verify cold-start (TC-52) instead.
- Traces to: AC "badge pop-in, confetti"; Spec §2.1.

### P4-08-TC-51 — [P0] Badge unlock overlay has no XP row and is dismissible
- Preconditions: overlay mounted (TC-50 trigger or BLOCKED).
- Steps: 1) mount overlay. 2) assert no "+N XP" text. 3) click CTA → overlay closes.
- Expected: XP row absent (composes RewardPopup with `xpAmount=0`); CTA dismisses. 
- Traces to: Spec §2.1, §8.1.

### P4-08-TC-52 — [P0] Cold-start safety: no badge overlay on first Home load
- Preconditions: fresh child / single dashboard response.
- Steps: 1) sign in. 2) wait 3s on Home.
- Expected: no BadgeUnlockOverlay on first load.
- Traces to: negative/edge; Spec §2.

---

## Section 6 — League promotion / demotion ([TRIGGER])

### P4-08-TC-60 — [TRIGGER][P0] Promotion popup fires when tier increases across two league refetches
- Preconditions: NON-reduce-motion; sequential `**/api/Gamification/Leagues/Me` (call 1 = tier `_1` Bronze, call 2 = tier `_3` Gold).
- Steps: 1) goto `/(child)/league` (baseline, no popup). 2) force refetch with the higher tier. 3) wait for the popup.
- Expected: RewardPopup with `league.promotion.title` ("You're Moving Up!" / "أنت تتقدم!") + subtitle `league.promotion.subtitle` interpolating the new tier name; full confetti (default ConfettiLayer). (Code: `league.tsx` `prevTierRef`, `kind='promotion'` when `currentTier > prev`.)
- Fallback: **BLOCKED** if refetch not reproducible; verify cold-start (TC-63) instead.
- Traces to: AC "promotion/demotion celebration motion"; Spec §3.1.

### P4-08-TC-61 — [TRIGGER][P0] Demotion popup fires (never-shaming, NO confetti)
- Preconditions: NON-reduce-motion; sequential league (call 1 = tier `_3`, call 2 = tier `_1`).
- Steps: 1) baseline. 2) force refetch with lower tier. 3) wait for popup.
- Expected: RewardPopup with `league.demotion.title` ("Keep Practicing!" / "استمر في التدرب!"), no subtitle, `xpAmount=0`, and NO confetti (variant `xp` + `xpAmount=0` → confetti self-gates). Title is encouraging, never shaming.
- Fallback: **BLOCKED** if refetch not reproducible.
- Traces to: AC "demotion celebration", NFR-6 kid-UX; Spec §3.1.

### P4-08-TC-62 — [P1] Demotion popup has no confetti particles (assert decorative absence)
- Preconditions: TC-61 reproduced.
- Steps: 1) demotion popup visible. 2) count confetti nodes.
- Expected: zero confetti rects (distinguishes demotion from promotion). BLOCKED if trigger unavailable.
- Traces to: Spec §3.1.

### P4-08-TC-63 — [P0] Cold-start safety: no promotion/demotion popup on first league load
- Preconditions: single league response (any tier); NON-reduce-motion.
- Steps: 1) goto `/(child)/league`. 2) wait 3s.
- Expected: no popup on first load (`prevTierRef` undefined → baseline only). No false celebration.
- Traces to: negative/edge; Spec §3.1.

### P4-08-TC-64 — [P1] No duplicate popup when tier is unchanged across refetches
- Preconditions: sequential league with the SAME tier on call 1 and call 2.
- Steps: 1) baseline. 2) force refetch (same tier). 3) wait.
- Expected: no popup (`if (prev === currentTier) return`). No duplicate/spurious celebration.
- Traces to: negative/edge; Spec §3.1.

---

## Section 7 — Missions complete ([TRIGGER]) + Hearts + Streak motion (non-reduce)

### P4-08-TC-70 — [TRIGGER][P0] Missions-complete RewardPopup fires when all dailies flip to Completed
- Preconditions: NON-reduce-motion; sequential `**/api/Gamification/Missions/Me` (call 1 = dailies with one InProgress, call 2 = all dailies Completed).
- Steps: 1) goto `/(child)/missions` (baseline). 2) force refetch with all dailies completed. 3) wait for popup.
- Expected: RewardPopup `missions.complete.title` ("Missions Complete!" / "المهمات مكتملة!"), subtitle `missions.complete.subtitle`, XP = sum of daily rewardXp (Latin), multicolor confetti, CTA `missions.complete.cta`, a11y `missions.complete.a11y`.
- Fallback: **BLOCKED** if refetch not reproducible; verify cold-start (TC-73) instead.
- Traces to: AC "mission-complete reward motion"; Spec §4.2.

### P4-08-TC-71 — [P0] Mission row green flash on a single row flipping to Completed (non-reduce-motion)
- Preconditions: NON-reduce-motion; sequential missions (one daily flips Completed, others not all done).
- Steps: 1) baseline. 2) refetch with the flip. 3) observe the row `$successSoft` flash (240ms).
- Expected: the flipped `mission-row-*` flashes green once; no full-screen popup (since not all dailies done). BLOCKED if trigger unavailable.
- Traces to: Spec §4.1.

### P4-08-TC-72 — [P0] Mission hero shimmer one-shot on mount (non-reduce-motion)
- Preconditions: NON-reduce-motion; `mockMissions` with incomplete dailies (`heroXp>0`).
- Steps: 1) goto `/(child)/missions`. 2) observe `missions-hero` shimmer (white overlay opacity 0→0.18→0, 600ms).
- Expected: a one-shot shimmer plays on mount, then hero rests at opacity 1. Contrast with TC-07 (reduce-motion: not rendered).
- Traces to: Spec §4.3.

### P4-08-TC-73 — [P0] Cold-start safety: no missions-complete popup on first load even if all dailies already Completed
- Preconditions: single missions response with all dailies Completed.
- Steps: 1) goto `/(child)/missions`. 2) wait 3s.
- Expected: no popup (cold start: `if (!prev) return` before any flip detection). No false celebration on a returning child whose missions were already done.
- Traces to: negative/edge; Spec §4.2 / §5.6.

### P4-08-TC-74 — [P0] Heart-break animation plays on `?lost=1` (non-reduce-motion)
- Preconditions: NON-reduce-motion; `mockDashboard({ hearts:3 })`.
- Steps: 1) goto `/(child)/hearts?lost=1`. 2) observe the first empty slot: scale 1→1.2→settle + ❤️→💔 swap at 80ms → back to ❤️ at ~480ms, opacity → 0.3.
- Expected: the break motion plays on the freshest-lost slot only; final state matches the other lost hearts (opacity 0.3, ❤️). The 💔 is a mid-animation flash. (Mid-flight 💔 may be hard to catch — assert the slot ends at opacity 0.3 and the card is present; mark the 💔 flash assertion P2.)
- Traces to: AC "heart-break motion"; Spec §5.2.

### P4-08-TC-75 — [P0] No heart-break when arriving WITHOUT `?lost=1`
- Preconditions: `mockDashboard({ hearts:3 })`.
- Steps: 1) goto `/(child)/hearts` (no param). 2) inspect slots.
- Expected: no breaking slot (`breakingIndex = -1`); empty hearts render statically at opacity 0.3; the `hearts-lost-card` is NOT shown.
- Traces to: negative/edge; Spec §5.2/§5.4.

### P4-08-TC-76 — [P0] Streak flame loops (non-reduce-motion) and is static at zero-state
- Preconditions: NON-reduce-motion; case A `mockDashboard({ streak:5 })`, case B `streak:0`.
- Steps: A) goto streak, observe 🔥 scale loop (1→1.04, 1.5s) + glow. B) `streak:0`, observe flame static at 0.4 opacity, no loop regardless of motion setting.
- Expected: A flame loops with glow; B flame static dimmed (`streak.tsx`: animation only when `!reduceMotion && hasStreak`). Zero-state never scolds (TC reuse: GAM-FE-TC-61 voice check).
- Traces to: AC "animated flame"; Spec §6.1, §6.2.

### P4-08-TC-77 — [P1] Streak milestone stagger plays on mount (non-reduce-motion)
- Preconditions: NON-reduce-motion; `mockDashboard({ streak:5 })`.
- Steps: 1) goto streak. 2) observe markers entrance (scale 0.8→1 + opacity, 80ms stagger).
- Expected: markers pop in staggered; all 4 end visible. Contrast TC-05 (reduce-motion instant).
- Traces to: Spec §6.3.

### P4-08-TC-78 — [P1] League banner + you-row entrance/pulse play on mount (non-reduce-motion)
- Preconditions: NON-reduce-motion; `mockLeague` with you-row.
- Steps: 1) goto league. 2) observe banner translateY entrance + you-row one-shot scale pulse.
- Expected: banner slides in (240ms), you-row pulses once after its stagger entrance; both end at rest. Contrast TC-11/TC-12.
- Traces to: Spec §3.2, §3.3.

---

## Section 8 — Negative / edge / graceful degradation

### P4-08-TC-80 — [P1] Skia-unavailable (web) → flame degrades gracefully (no crash, glyph+glow present)
- Preconditions: web PWA (Skia is never loaded on web — `tryLoadSkia` returns null); `mockDashboard({ streak:5 })`.
- Steps: 1) goto streak. 2) confirm flame renders (Moti scale loop, no Skia hue flicker — DG-3).
- Expected: flame glyph + glow render; no Skia error; hue-flicker absent on web is acceptable (DG-3). Screen fully functional.
- Traces to: AC "perform smoothly on mobile" / cross-platform; Spec DG-3, §7.1.

### P4-08-TC-81 — [P1] Confetti degrades when Moti unavailable (SSR/Node) — celebration still complete
- Preconditions: documented behavior — when both Skia and Moti are unavailable, `ConfettiLayer` and the RewardPopup card render without particles/spring but remain legible.
- Steps: assert via the reduce-motion proof (TC-03) + code reference (ConfettiLayer returns null without Moti). Web PWA in Chromium has Moti, so this is largely a defensive/observational case.
- Expected: no crash; popup text + CTA always present. Likely BLOCKED as a live web assertion (Moti is present); record as covered-by-design + TC-03.
- Traces to: Spec §7.1, §7.2.

### P4-08-TC-82 — [P1] No duplicate level-up popup on an xp-only refresh after a level-up
- Preconditions: NON-reduce-motion; sequential dashboard: call 1 level 2, call 2 level 3 (fires popup), call 3 level 3 with more XP only.
- Steps: 1) trigger level-up. 2) dismiss. 3) force an xp-only refetch (same level). 4) wait.
- Expected: no second popup on the xp-only refresh (`xp.tsx` uses `latestProgressRef` + depends only on `diff` identity to avoid re-firing). BLOCKED if multi-step refetch not reproducible.
- Traces to: negative/edge; Spec §1 trigger note.

### P4-08-TC-83 — [P1] Locale switch mid-session re-renders celebration copy correctly
- Preconditions: start AR, switch to EN via localStorage + reload on a screen with P4-08 copy.
- Steps: 1) AR screen. 2) switch to EN, reload. 3) re-read copy.
- Expected: copy flips locale (e.g. league zone labels, hearts.lost), `lang` flips ar→en, no raw keys, no stale-locale text.
- Traces to: Spec §8.3/§8.4; edge "locale-switch".

### P4-08-TC-84 — [P2] All screens render error/loading/empty states without motion artifacts
- Preconditions: mock 500 / empty per screen (reuse existing GAM error patterns).
- Steps: 1) on each P4-08 screen, force error then empty. 2) assert the `*-error` / `*-empty` / `*-loading` testIDs render and no celebration fires.
- Expected: states render cleanly; no popup/confetti during loading/error/empty.
- Traces to: negative/edge; existing screen states.

---

## testID gaps to request from `frontend` before implementation (flag, don't write brittle selectors)

| # | Missing testID | Where | Needed for | Suggested testID |
|---|---|---|---|---|
| G1 | RewardPopup card overlay | `packages/ui/src/components/RewardPopup/index.tsx` | TC-03, 20–23, 40, 50–51, 60–61, 70 — assert ANY celebration overlay + its confetti | `reward-popup` + `reward-popup-confetti` (+ a `data-variant` for level-up/xp/badge-unlock) |
| G2 | BadgeUnlockOverlay distinct id | `BadgeUnlockOverlay` | TC-50/51 distinguish badge unlock from other popups | `badge-unlock-overlay` |
| G3 | League promotion vs demotion popup id | `league.tsx` celebration block | TC-60/61/62 distinguish promotion (confetti) from demotion (no confetti) | `league-promo-popup` / `league-demotion-popup` |
| G4 | Confetti layer node | `ConfettiLayer.tsx` | TC-03, 23, 62 — assert presence/absence of particles | `confetti-layer` |
| G5 | Streak hero flame element | `streak.tsx` / `StreakFlame` | TC-04/76 — target the flame glyph (vs the day counter) | `streak-flame` |
| G6 | Mission hero shimmer overlay | `missions.tsx` | TC-07/72 — assert shimmer rendered vs not | `missions-hero-shimmer` |
| G7 | Individual heart slot | `hearts.tsx` `BigHeart` | TC-09/74 — target the breaking slot | `heart-slot-{index}` (+ `data-breaking`) |
| G8 | Legendary badge disc | `Badge` component | TC-14 — assert no shimmer loop | `badge-disc-legendary` |
| G9 | Hearts-lost card already has `hearts-lost-card`; OK | — | — | (no change) |

Without G1/G4, celebration-overlay and confetti assertions must fall back to text-content/role queries (brittle). **Recommend the lead approve adding G1–G7 testIDs before the tester implements Sections 1–7.**
