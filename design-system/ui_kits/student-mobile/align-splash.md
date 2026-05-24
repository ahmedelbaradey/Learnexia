# Design Spec — Pixel-Alignment Pass: Splash Screen (app/index.tsx)

**Targets:**
- `design-system/screenshots/mobile/01-splash.png` (EN)
- `design-system/screenshots/mobile-ar/01-splash.png` (AR)

**Preview cards (canonical):**
- `design-system/preview/mobile-splash-anatomy.html`
- `design-system/preview/ar-splash.html`
- `design-system/preview/gradients.html`
- `design-system/preview/logo.html` / `logo-mark.html`
- `design-system/colors_and_type.css` (CSS token source of truth)

**Current implementation:** `apps/student-app/app/index.tsx`  
**i18n copy source:** `packages/shared/src/i18n/resources.ts`

---

## Delta Table

All rows cite the proving card/capture. Severity: **Blocker** (capture mismatch that changes the identity of the screen) / **Major** (visually wrong or missing) / **Minor** (pixel-level detail).

---

### BLOCKER

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---|---|---|---|---|
| B1 | **Hero mascot / brand mark (EN)** | No mascot — wordmark + subtitle only, no visual hero | Capture `mobile/01-splash.png` shows NO mascot either (wordmark + subtitle centered). AR capture `mobile-ar/01-splash.png` shows a large glowing star mascot glyph (88 px, `🌟` with `drop-shadow(0 0 20px rgba(250,204,21,0.6))`; 132×132 px radial-glow disc `rgba(250,204,21,0.35)→transparent`). Card `ar-splash.html` line 5–7 | **Blocker (AR)** | EN: no change needed. AR path: render the glowing star hero **above** the wordmark. Implement as a 132×132 `Stack` borderRadius=9999 with `background: radial-gradient(circle, rgba(250,204,21,0.35), rgba(168,85,247,0) 65%)` containing a `Text` `🌟` fontSize=88 with drop-shadow filter (via `style`). The mascot asset `assets/mascot-owl.svg` is a placeholder — use `🌟` until real art ships. |
| B2 | **Background gradient — native path** | `gradientStops.splashBg` = `['#4338CA','#3730A3','#0F172A']` angle 160 (a cold blue-indigo top→bottom linear). On native this is the only visible gradient (radial only applies on web via `style.backgroundImage`) | Capture shows a warm **deep violet-purple** center fading to dark — closer to the radial string `radial-gradient(circle at 50% 45%, #4F3FB0, #3B2C8F 40%, #241B6A 100%)` per `mobile-splash-anatomy.html` body style. The current `#4338CA` (`--lx-primary-press`) skews blue-indigo, not purple. `#4F3FB0` is the warm mid-purple needed. | **Blocker** | Update `gradientStops.splashBg` stops to `['#4F3FB0','#3B2C8F','#241B6A']` angle=160 so native matches the radial center colour. Also update `radialGradients.splashBg` to `radial-gradient(circle at 50% 45%, #4F3FB0 0%, #3B2C8F 40%, #241B6A 100%)`. File: `packages/design-system/src/tokens/gradients.ts` lines 42 and 53. |
| B3 | **Progress bar fill gradient** | `gradientStops.gradXp` = green→indigo `['#22C55E','#4F46E5']`. XP gradient used on the splash fill | Capture EN `mobile/01-splash.png` shows the fill as a **soft violet–light-indigo** (not green). Card `mobile-splash-anatomy.html` line 13: `linear-gradient(90deg,#C4B5FD,#818CF8)` — violet-200 → indigo-400. This is a **splash-specific** tint, NOT the XP gradient. AR capture confirms same violet fill. | **Blocker** | Introduce a new `gradientStops.splashProgress` token: `{ colors: ['#C4B5FD','#818CF8'], angle: 90 }` and `gradients.splashProgress: 'linear-gradient(90deg,#C4B5FD,#818CF8)'`. Use this in the splash progress bar instead of `gradXp`. File: `packages/design-system/src/tokens/gradients.ts` + `apps/student-app/app/index.tsx` line 120. |
| B4 | **Progress bar track color** | `backgroundColor="$bg"` = `#0F172A` (pure dark canvas — very nearly black) | Card `mobile-splash-anatomy.html` line 13: `background:rgba(0,0,0,0.35)` — semi-transparent black, producing a dark-but-not-solid track that lets the purple gradient bg show through. EN capture confirms the track looks like a faint dark strip, not a solid black bar. | **Blocker** | Change progress track `backgroundColor` from `"$bg"` to `rgba(0,0,0,0.35)` (pass via `style` prop since Tamagui tokens don't cover semi-transparent black on bg). Or add a token `colors.progressTrack: 'rgba(0,0,0,0.35)'` and wire it. |
| B5 | **Progress fill width** | `width="70%"` | Card sets `55%` fill. EN capture visually reads as ~55% (fill occupies just over half the track). 70% is noticeably wider and looks overfull for a "loading" state. | **Blocker** | Change `width` of the fill `GradientBox` from `"70%"` to `"55%"`. |

---

### MAJOR

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---|---|---|---|---|
| M1 | **Wordmark font size** | `fontSize={40}` | Card `mobile-splash-anatomy.html` line 9: `font-size:36px`. EN capture: wordmark reads as approximately H1-equivalent (32–36 px range). The `--lx-size-h1` token is 32 px; the card uses 36 px. Tamagui `$fontSize[7]` = 32 px. The current 40 px is larger than the card and makes the wordmark oversized vs the capture. | **Major** | Change `fontSize` to `36` (between H1 and display, no existing token — flag as gap). Or use `32` (`$fontSize[7]`) if strict tokenisation is required. |
| M2 | **Wordmark font weight** | `fontWeight="800"` (`--lx-weight-black`) | Card line 9: `font-weight:900`. The card spec uses Black/900 for the brand wordmark. 800 is ExtraBold (Poppins). Visual difference is perceptible — thinner strokes than capture. | **Major** | Change `fontWeight` to `"900"`. |
| M3 | **Wordmark letter-spacing** | No `letterSpacing` set (defaults to 0) | Card line 9: `letter-spacing:-0.02em` (`--lx-tracking-tight`). Capture shows slightly tighter tracking on "Learnexia". | **Major** | Add `letterSpacing={-0.02 * 36}` (≈ -0.72 px at 36 px) or use the CSS token `--lx-tracking-tight`. In Tamagui pass `letterSpacing={-0.72}`. |
| M4 | **Subtitle font weight** | No explicit weight — inherits `$body` default (400) | Card line 10: `font-weight:500`. Subtitle "AI Learning Adventure Begins" reads slightly heavier than body-regular in the capture. | **Major** | Add `fontWeight="500"` to the subtitle `Text`. |
| M5 | **Subtitle color** | `color="$fg2"` = `#CBD5E1` (body text) | Card line 10: `color:rgba(255,255,255,0.7)` — a warmer 70%-alpha white, visually lighter than `#CBD5E1` on the purple bg. Capture confirms subtitle is noticeably dimmer than the wordmark. | **Major** | Change subtitle `color` from `"$fg2"` to `rgba(255,255,255,0.7)` (pass inline). No current Tamagui token maps to this value — flag as design gap or introduce `colors.fg2Alpha: 'rgba(255,255,255,0.70)'`. |
| M6 | **Loading label color** | `color="$fg3"` = `#94A3B8` (muted slate-blue) | Card line 14: `color:rgba(255,255,255,0.7)` — same 70%-alpha white as subtitle. On the purple bg `#94A3B8` reads as blue-grey, not white-tinted, producing wrong hue. | **Major** | Change `color` from `"$fg3"` to `rgba(255,255,255,0.7)` inline. Same gap as M5. |
| M7 | **Footer "POWERED BY AI" color** | `color="$fg3"` = `#94A3B8` | Card line 16: `color:rgba(255,255,255,0.45)` — 45%-alpha white (dimmer than `$fg3` on this bg). Capture shows footer eyebrow as very faint. `#94A3B8` on purple bg reads as visible blue-grey, too prominent. | **Major** | Change `color` to `rgba(255,255,255,0.45)` inline. |
| M8 | **Footer letter-spacing** | `letterSpacing={2}` (2 px absolute) | Card line 16: `letter-spacing:0.18em`. At 12 px font size, 0.18em = 2.16 px. Very close but not identical — current value is equivalent to `0.167em` at 12 px. | **Minor** | Change `letterSpacing={2.2}` (0.18 × 12 ≈ 2.16, round to 2.2). |
| M9 | **Footer tagline — EN** | `t('common.splash.tagline')` = `'✦ Gamified Learning ✦'` | Capture EN shows: `✦ Gamified Learning ✦`. Copy matches. No delta on copy. Color: currently `color="$fg2"` (`#CBD5E1`) — capture reads tagline as dim white, consistent with `rgba(255,255,255,0.7)`. | **Major** | Change tagline `color` from `"$fg2"` to `rgba(255,255,255,0.7)` (same as subtitle). |
| M10 | **Footer tagline — AR** | `t('common.splash.tagline')` = `'✦ تعلّم بأسلوب اللعب ✦'` | AR capture shows footer as: `مدعوم بالذكاء الاصطناعي` (single line footer only, no ✦ tagline below — the tagline is not visible in the AR capture screenshot). The AR card `ar-splash.html` line 16 shows only the footer label, no tagline row. | **Major** | For AR, hide the tagline row (or the AR i18n key `common.splash.tagline` resolves to empty). If hiding conditionally by locale, add `tagline: ''` in the `ar.common.splash` block in resources.ts, and conditionally render `{tagline ? <Text>...</Text> : null}` in the component. |
| M11 | **DotPulse — dot size** | `width={10} height={10}` | Capture EN shows three dots at approximately 8–9 px diameter. Card `index.html` `@keyframes lxdot` uses dots that appear as small accent bullets — 8 px is the conventional size. The 10 px dots are slightly oversized. | **Major** | Change dot `width`/`height` from `10` to `8`. |
| M12 | **DotPulse — gap** | `gap="$2"` = 8 px | Capture shows dots spaced approximately 6–8 px apart. 8 px is acceptable but the dots themselves enlarging to 10px made the row wider than capture. At 8 px dots with 8 px gap the visual matches. No change needed if M11 is fixed. | **Minor** | No change if M11 fixed. If dots remain 10px, reduce gap to `"$1"` (4 px). |
| M13 | **DotPulse — active dot color** | `backgroundColor="$primary"` = `#4F46E5` for all three dots | Capture shows the **middle dot** as brighter/larger (appears as the "active" pulse state), while the first and third are slightly faded. The card `index.html` `lxdot` keyframe gives 0.3→1 opacity. The implementation already uses Moti `loop: true` with staggered `delay` — so the _color_ of all three is identical primary. In the EN capture the active (brighter) dot appears slightly different from the flanking dots; this may just be the animation state frozen mid-cycle. No color fix needed, but verify the middle dot briefly hits `opacity: 1` while others are at lower opacity — the Moti `repeatReverse: true` should achieve this. | **Minor** | Verify Moti loop gives correct staggered opacity. No token change needed. |
| M14 | **AR: appName shown as 'ليرنيكسيا'** | `t('common.appName')` resolves to `'ليرنيكسيا'` in AR locale | Both preview card `ar-splash.html` line 9 and AR capture show "Learnexia" in **Latin script with `dir="ltr"`** — the brand name is never transliterated. SKILL.md rule: "Latin+dir=ltr for brand name 'Learnexia'." AR resources.ts line 378 sets `appName: 'ليرنيكسيا'` which breaks this rule. | **Blocker** | Change `ar.common.appName` to `'Learnexia'` (Latin, no transliteration). In the splash `Text` rendering `{t('common.appName')}` add `dir="ltr"` when locale is AR: wrap with `writingDirection="ltr"` prop. File: `packages/shared/src/i18n/resources.ts` line 378, and `apps/student-app/app/index.tsx` wordmark Text. |
| M15 | **AR: wordmark font** | `fontFamily="$heading"` (Poppins) | AR card `ar-splash.html` line 9: `font-family:'Cairo'` for the wordmark element. The brand name Learnexia renders in Cairo on AR screens (though still Latin script). Ensures metric compatibility with the surrounding Arabic layout. | **Major** | In AR locale, switch wordmark `fontFamily` to `"$headingAr"` (Cairo). Conditionally: `fontFamily={locale === 'ar' ? '$headingAr' : '$heading'}`. |
| M16 | **AR: subtitle font** | `fontFamily="$body"` (Poppins) | AR card line 10: `font-family:'Tajawal'` for Arabic subtitle. Current `$body` resolves to Poppins which cannot render Arabic glyphs correctly. | **Blocker (AR)** | In AR locale, switch subtitle `fontFamily` to `"$bodyAr"` (Tajawal). |
| M17 | **AR: loading label font** | `fontFamily="$body"` (Poppins) | AR card line 14: `font-family:'Tajawal'`. Same issue — Arabic text in Poppins produces fallback glyphs. | **Blocker (AR)** | Switch loading label to `"$bodyAr"` in AR locale. |
| M18 | **AR: footer label font** | `fontFamily="$heading"` (Poppins) | AR card line 16: `font-family:'Cairo'`. Footer eyebrow in AR should use Cairo. | **Major (AR)** | Switch footer `fontFamily` to `"$headingAr"` in AR locale. |
| M19 | **AR: layout direction** | Comment says "Splash is brand chrome → LTR-always layout (RTL not required)". Screen container has no `dir` override. | AR capture shows all text is **right-aligned / RTL** within the center column — subtitle "تبدأ مغامرة التعلّم بالذكاء الاصطناعي" is centered, progress bar is direction:ltr (per SKILL.md rule 6 and card line 13). The outer wrapper should be `dir="rtl"` for AR so that flex-start/end invert correctly, but the progress bar track needs `direction: ltr`. The loading label and footer should also render RTL. The current LTR-always approach is wrong for AR. | **Blocker (AR)** | When locale is AR, apply `writingDirection="rtl"` on the root container. The progress `Stack` (track+fill) must explicitly keep `style={{ direction: 'ltr' }}`. Brand name Learnexia `Text` keeps `writingDirection="ltr"`. |
| M20 | **AR: loading label copy** | `t('common.splash.loading')` → `'جارٍ التحميل… ⚡'` | AR card line 14: `جارٍ التحميل… ⚡`. Copy matches perfectly. No delta. | — | No change needed. |
| M21 | **AR: tab bar visible** | Not rendered on splash | AR capture `mobile-ar/01-splash.png` shows a **bottom tab bar** with 5 items (أنا، الدوري، المهمات، المهارات، الرئيسية) and a home indicator. This is an artifact of the UI kit screenshot context (the click-through wraps all screens in the tab shell). The actual Expo splash has no tab bar — this is the ui_kit context, not a real app shell. | **Not applicable** | The tab bar in the AR capture is a UI kit frame artifact. Do NOT add a tab bar to the Expo splash screen. This is confirmed — splash has no tab bar in the EN capture. |

---

### MINOR

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---|---|---|---|---|
| m1 | **Star field — count** | 11 stars | Capture shows approximately 8–10 faint stars scattered across the field. 11 is close enough but slightly denser than the capture. | **Minor** | Remove 1–2 stars from the STARS array (suggest removing indices `{ top:'31%', left:'30%' }` and `{ top:'74%', left:'46%' }`). |
| m2 | **Star field — max size** | `size: 4` for two stars | Capture shows all dots as very small (2–3 px range). Size 4 produces slightly large visible circles rather than pinpoints. | **Minor** | Cap max size at 3. Change `size: 4` entries to `size: 3`. |
| m3 | **Gap between wordmark + subtitle and the DotPulse** | Stacked inside one flex-column `gap="$6"` (24 px) — single level | Card shows the progress bar + loading label grouped together 8 px below the wordmark block, with the DotPulse above. The grouping `margin-top:8px` in the card above the progress area suggests the subtitle-to-DotPulse gap is ≤ 16 px. Currently `gap="$6"` = 24 px between every child makes the spacing loose. | **Minor** | Restructure: wrap the DotPulse + progress bar + loading label in a sub-Stack with `gap="$3"` (12 px). Increase outer wrapper gap from `"$6"` to `"$8"` (32 px) only between the hero text block and the loader group, not between each individual element. |
| m4 | **Progress bar — gap between bar and loading label** | `gap="$6"` (inherited from parent column) between the progress bar and loading label | Card groups them with `gap:10px`. Currently they are spaced 24 px apart (via parent `gap`), which is too much — the loading label reads as floating away from the bar. | **Minor** | Resolved by m3 restructure: move loading label inside the loader sub-Stack so it is `gap: 10–12 px` below the bar. |
| m5 | **Footer vertical position** | `bottom={72}` absolute position | EN capture: footer sits approximately 80–90 px from bottom edge within the screen area (above the home indicator ~8 px + safe area ~34 px = 42 px; footer bottom edge appears ~80 px from screen bottom). 72 px is close; on devices with a 34 px home indicator safe area the effective clearance is 72 - 34 = 38 px which may overlap the indicator. | **Minor** | Change `bottom={72}` to `bottom={88}` to provide sufficient safe-area clearance on all notched devices. |
| m6 | **Footer gap between "POWERED BY AI" and tagline** | `gap="$2"` = 8 px | Card does not show a tagline below the footer label (single footer label only in the card). The capture also shows only the eyebrow + tagline as a single vertical group. 8 px is fine. | **Minor** | No change. |
| m7 | **EN: `appName` copy** | `t('common.appName')` → `'Learnexia'` | Capture: "Learnexia". Matches. | — | No change. |
| m8 | **EN: subtitle copy** | `t('common.splash.subtitle')` → `'AI Learning Adventure Begins'` | Card line 10: `AI Learning Adventure Begins`. Matches. | — | No change. |
| m9 | **EN: loading label copy** | `t('common.splash.loading')` → `'Loading… ⚡'` | Card line 14: `Loading… ⚡`. Matches. | — | No change. |
| m10 | **EN: footer eyebrow copy** | `t('common.splash.poweredBy')` → `'POWERED BY AI'` | Card line 16: `POWERED BY AI`. Matches. | — | No change. |
| m11 | **AR: subtitle copy** | `t('common.splash.subtitle')` → `'تبدأ مغامرة التعلّم بالذكاء الاصطناعي'` | Card line 10: `تبدأ مغامرة التعلّم بالذكاء الاصطناعي`. Matches. | — | No change. |
| m12 | **AR: footer copy** | `t('common.splash.poweredBy')` → `'مدعوم بالذكاء الاصطناعي'` | Card line 16: `مدعوم بالذكاء الاصطناعي`. Matches. | — | No change. |
| m13 | **Subtitle maxWidth** | `maxWidth={240}` | Card uses `max-width:380px` container; no inner max-width on the subtitle. EN capture: subtitle wraps to 2 lines ("AI Learning Adventure / Begins"). At 390 px screen width with 48 px horizontal padding the text column is 294 px wide; 240 px forces the wrap. This is intentional and matches the capture's 2-line break. | **Minor** | No change needed — the 2-line wrap is correct per capture. |
| m14 | **Subtitle fontSize** | `fontSize={15}` | Card uses `14px` (`--lx-size-body-sm`). 15 px is one pixel above the token value. | **Minor** | Change to `fontSize={14}` (`$fontSize[3]`). |

---

## Summary of New / Changed Token Requirements

| Token | Action | Value | File |
|---|---|---|---|
| `gradientStops.splashBg` | Update stops | `['#4F3FB0','#3B2C8F','#241B6A']` angle 160 | `packages/design-system/src/tokens/gradients.ts` |
| `radialGradients.splashBg` | Update center color | `radial-gradient(circle at 50% 45%, #4F3FB0 0%, #3B2C8F 40%, #241B6A 100%)` | same file |
| `gradientStops.splashProgress` | Add new | `{ colors: ['#C4B5FD','#818CF8'], angle: 90 }` | same file |
| `gradients.splashProgress` | Add new | `'linear-gradient(90deg,#C4B5FD,#818CF8)'` | same file |
| `colors.fg2Alpha` | Add new (optional) | `'rgba(255,255,255,0.70)'` | `packages/design-system/src/tokens/colors.ts` |
| `ar.common.appName` | Update value | `'Learnexia'` (Latin, no transliteration) | `packages/shared/src/i18n/resources.ts` |
| `ar.common.splash.tagline` | Update value | `''` (empty — hide in AR) | same file |

---

## RTL Convention Checklist (SKILL.md rule 4–6)

| Rule | Status | Fix |
|---|---|---|
| `dir="rtl"` on root when AR | Missing in current implementation | Add conditional `writingDirection` on root GradientBox |
| Brand name "Learnexia" always Latin + `dir="ltr"` | Broken — AR resources.ts uses `'ليرنيكسيا'` | Fix resources.ts + add `writingDirection="ltr"` to wordmark Text |
| Arabic fonts: Cairo (heading) / Tajawal (body) | Missing — Poppins used for all in AR | Add locale-conditional `fontFamily` to all Text nodes |
| Progress bar stays `direction:ltr` | Not implemented (no explicit direction set) | Add `style={{ direction: 'ltr' }}` to progress bar `Stack` |
| Eastern-Arabic numerals inline | Not applicable on splash (no numbers in copy) | No action needed |

---

## Motion Checklist

| Element | Card spec | Current | Delta |
|---|---|---|---|
| DotPulse animation | `0%,100%: opacity 0.3, scale 0.85; 50%: opacity 1, scale 1` (card `lxdot` keyframe) | Moti: `from {opacity:0.3, scale:0.8}` → `animate {opacity:1, scale:1}`, duration 600 ms, `loop+repeatReverse` | scale floor is `0.8` vs card `0.85` — **Minor**, change `scale:0.8` to `scale:0.85` in `DotPulse.tsx` |
| Progress bar fill | Card shows static 55% fill (anatomy card only; no animation keyframe defined for splash progress) | Static `width="55%"` fill after B5 fix | No motion delta — static fill is correct for the anatomy card. |
| Background glow | Radial center glow (hero/brand moment — allowed per SKILL.md) | Web: applied via `style.backgroundImage` radial string; native: linear 3-stop approximation | After B2 fix the native gradient is warmer — acceptable. No animation on bg required. |

---

## Implementation Handoff — Ordered Fix List

**Batch 1 (token changes — `packages/design-system`):**
1. `gradients.ts`: update `splashBg` linear stops to `['#4F3FB0','#3B2C8F','#241B6A']` and radial to `circle at 50% 45%, #4F3FB0…` (B2).
2. `gradients.ts`: add `splashProgress` linear and stop tokens (B3).

**Batch 2 (i18n fix — `packages/shared`):**
3. `resources.ts` line 378: `appName: 'Learnexia'` in AR block (M14/Blocker).
4. `resources.ts`: add `ar.common.splash.tagline: ''` (M10).

**Batch 3 (component changes — `apps/student-app/app/index.tsx` + `src/components/DotPulse.tsx`):**
5. Wordmark: `fontSize=36`, `fontWeight="900"`, `letterSpacing={-0.72}`, conditional `fontFamily` + `writingDirection="ltr"` in AR (B1 AR path, M1–M3, M14, M15).
6. Subtitle: `fontSize={14}`, `fontWeight="500"`, `color=rgba(255,255,255,0.7)` inline, conditional `fontFamily="$bodyAr"` in AR (M4, M5, m14, M16).
7. Root container: add `writingDirection="rtl"` when AR locale (M19).
8. Progress bar track: `style={{ backgroundColor: 'rgba(0,0,0,0.35)' }}` + `style={{ direction: 'ltr' }}` (B4, RTL rule).
9. Progress fill: use `gradientStops.splashProgress` instead of `gradXp`, `width="55%"` (B3, B5).
10. Loading label: `color=rgba(255,255,255,0.7)`, conditional `fontFamily="$bodyAr"` in AR (M6, M17).
11. Footer eyebrow: `color=rgba(255,255,255,0.45)`, `letterSpacing={2.2}`, conditional `fontFamily="$headingAr"` in AR (M7, M8, M18).
12. Footer tagline: `color=rgba(255,255,255,0.7)` (M9); conditional render `{tagline ? ... : null}` so AR hides it (M10).
13. Footer `bottom={88}` (m5).
14. DotPulse: dot size 8 px, scale floor `0.85` (M11, motion delta).
15. AR mascot hero: render 132×132 glow disc + `🌟` (88 px, drop-shadow) above wordmark when AR locale (B1 AR).
16. Star field: remove 2 stars, cap max size 3 (m1, m2).
17. Restructure inner layout for proper loader group spacing (m3, m4).

---

## Design Gaps (flagged, not silently fixed)

1. **`colors.fg2Alpha` / `rgba(255,255,255,0.70)`** — used on subtitle, loading label, tagline on the purple bg. No current Tamagui token covers alpha-white; the fg2 token is slate-blue. Either add a `fg2Alpha` token or accept inline style. Recommend adding the token in `packages/design-system/src/tokens/colors.ts`.
2. **Wordmark font size 36 px** — falls between `$fontSize[7]` (32 px = H1) and `$fontSize[8]` (48 px = display). No token covers 36 px. Either hardcode `36` or extend the fontSize scale with a `7.5` or named key like `wordmark: 36`. Recommend extending.
3. **`gradients.splashProgress`** — the violet-tinted progress fill (`#C4B5FD → #818CF8`) is a splash-specific gradient not covered by any named gradient in the design spec. Adding it as a new constant is the correct approach (not a spec-defined gradient).
4. **AR mascot asset** — `assets/mascot-owl.svg` is a placeholder. The AR splash uses `🌟` emoji as a temporary stand-in. Real character art must replace this before launch.
5. **`$headingAr` / `$bodyAr` font tokens in Tamagui** — the font config must expose `headingAr: 'Cairo'` and `bodyAr: 'Tajawal'` as Tamagui font family tokens for conditional locale switching in components. Verify these exist in `packages/design-system/src/fonts/index.ts`; if not, add them.
