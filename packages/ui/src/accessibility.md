# Kid-Accessibility Baseline — `@learnexia/ui`

This baseline (NFR-6, Design Spec §3c) is applied to **every** interactive
component in this package. It is enforced in code where possible and documented
as a usage rule where it depends on screen composition.

## Touch targets

| Rule | Implementation |
|---|---|
| Min interactive size 48×48px | `Button` md/full are 52px tall (≥48). `Card` (when `onPress`) sets `minHeight: 48`. |
| Small visuals reach 48px via hitSlop | `Button` `sm` (40px) adds `hitSlop {top/bottom:4}`. AITutorBubble chips (30px) add `hitSlop {top/bottom:9}`. RewardPopup CTA (44px) adds `hitSlop {top/bottom:4}`. |
| Non-interactive components | XPBar, Hearts, StreakFlame, Badge (non-tappable) have no touch-target requirement. |

## Focus & roles

- **Web focus ring:** `shadows.focusRing` (`0 0 0 2px #4F46E5, 0 0 0 6px rgba(99,102,241,0.45)`) is the canonical `:focus-visible` ring. Applied by the host's global stylesheet; the token is exported from `@learnexia/design-system`.
- **Native:** every interactive element sets `accessible` + `accessibilityRole`:
  - Button / Card(pressable) / AITutorBubble chips → `button`
  - XPBar → `progressbar` + `accessibilityValue {min,max,now}`
  - Hearts → `text`/`meter` + `accessibilityValue`
  - StreakFlame → `text`
  - Badge → `image`
  - RewardPopup → `alert` + `accessibilityLiveRegion="assertive"`
  - AITutorBubble typing → `accessibilityLiveRegion="polite"`, label "AI tutor is typing"

## Required accessible labels

All non-text interactive / status components require an `accessibilityLabel`
prop **in TypeScript** (no default — the caller must supply meaningful text):
`Button`, `XPBar`, `Hearts`, `StreakFlame`, `Badge`, `AITutorBubble`,
`RewardPopup`. Decorative inner glyphs (heart/flame/owl/emoji) are
`accessibilityElementsHidden`.

## Contrast pairings (verified against tokens, WCAG AA ≥ 4.5:1 for text)

| Foreground | Background | Use |
|---|---|---|
| `$fg1` #F8FAFC | `$primary` #4F46E5 | Primary button label |
| `$fgInverse` #0F172A | `$secondary` #22C55E | Success button label |
| `$fg1` #F8FAFC | `$danger` #EF4444 | Danger button label |
| `$fg1` / `$fg2` | `$card` #1E293B / `$bg` #0F172A | All card/body text |
| `$xp` #FACC15 | `$bg` #0F172A | XP counter (yellow on dark — high contrast) |

Muted/disabled text uses the gap-fill `$fg4` #64748B; this is intentionally
lower-emphasis and only applied to disabled controls / locked badge labels (not
to primary readable content).

## Press feedback (no silent presses)

Every pressable has a visible state change:
- `Button` / `Card` / chips: press scale (0.95 / 0.98) via `pressStyle` (+ Reanimated on native).
- `Button` `loading`: a visible `Spinner` (Moti opacity pulse) replaces the label; the button is non-pressable while loading.

## Single primary action per screen (usage rule)

Only **one** `Button variant="primary"` should be the high-emphasis element
visible without scrolling on a given screen. `secondary` / `ghost` / `success` /
`danger` are supporting. This is a composition rule the **screen author** must
follow — it is not enforced by the component.

## RTL

All components use **logical** layout props (`marginStart`/`marginEnd`,
`borderBottomStartRadius`/`EndRadius`, `start`/`end`) and flip directional rows
via `flexDirection: dir === 'rtl' ? 'row-reverse' : 'row'`, reading direction
from `@learnexia/shared/i18n` (`directionForLocale`). No raw `left`/`right`.

## Gap-resolution notes affecting a11y

- **Backdrop blur (Gap 5):** AI bubble / RewardPopup fall back to a
  semi-transparent (non-blurred) surface when Skia is unavailable — contrast is
  preserved by the solid-ish background, so legibility never depends on blur.
- **Legendary hue-rotate (Gap 9):** static on native (no motion) — color/contrast
  unaffected; the animation is purely decorative.
