# Design Spec — W10 P2-12-FE · Parent Settings tabs (Notifications · Linked children · Security · Plan & billing)

> Build the four "coming soon" panels inside the **existing six-tab Settings shell**
> (`apps/student-app/app/(parent)/_components/SettingsWeb.tsx`). The shell, page header
> and tab rail are **already built and aligned** in P1-11 v2 — do **not** touch them.
> This spec covers only the four `PanelSurface` bodies the frontend swaps in for
> `ComingSoonPanel`, plus the new `Switch` primitive they rely on.

## Pixel sources (cite both EN + AR)
- **Captures:** `design-system/screenshots/web/07-settings.png` (EN/LTR Profile tab visible — composition target for the rail + panel surface; the 4 new panels reuse the same surface) · `design-system/screenshots/web-ar/07-settings.png` (AR/RTL twin).
- **Preview cards consulted (verbatim values pulled below):**
  - `design-system/preview/web-toggle.html` — Switch track/thumb visuals, on-state primary glow.
  - `design-system/preview/web-2fa-card.html` — Security panel anatomy (heading 16/800, body 13, divider, action row).
  - `design-system/preview/web-security-strip.html` — sessions strip pattern (truncated id, expiresAt, status badge).
  - `design-system/preview/web-linked-rows.html` — Linked-children row composition (avatar + name + meta + action icons).
  - `design-system/preview/web-plan-card.html` — Plan panel anatomy (plan name 22/800, status pill, disabled Manage CTA).
  - `design-system/preview/components-input.html` + `ar-input.html` — input field height 48, radius 14, label 12/600 upper, focus ring.
  - `design-system/preview/components-buttons.html` — Button radius 16 (`$button`), `md` height 52, ghost border `$borderStrong`, primary glow.
  - `design-system/preview/mobile-password-meter.html` — strength meter palette (used inside the Security panel).
  - Token source: `design-system/colors_and_type.css` (all `--lx-*` values cited below mirror the Tamagui `$*` tokens 1:1).
- **Kit composed reference:** `design-system/ui_kits/parent-dashboard/index.html` + `index-ar.html` (Settings page mock — confirms the shell + panel mounting).
- **Implementation reference (already on `main`, must mirror):** `apps/student-app/app/(parent)/_components/SettingsWeb.tsx` — `PanelSurface`, `PanelHeader`, `ProfilePanel`, `LanguagePanel` set the in-panel grammar these 4 new panels copy.
- **Prior alignment work this spec depends on:** `design-system/ui_kits/parent-dashboard/align-settings.md` (P1-11 v2 deltas — radius `$modal`, padding 22, gap 18, border `rgba(255,255,255,0.06)`, panel title 16/800, subtitle 12/$fg3, field grid gap 14, action row gap 10 + paddingTop 6).

## Scope (what's in vs. out)
**In** — four panel bodies + new `Switch` primitive + Plan status `Badge` variant usage + sessions strip + inline Unlink confirm strip + inline Edit form inside `ChildCard`.

**Out** (matches plan's carry-forward log):
- No new shared `Dialog` / `Modal` primitive — Unlink confirmation is an **inline strip inside the `ChildCard` row** (per Q UI-2). If the frontend hits a case that needs a true modal, it must stop and ask the lead before adding a Dialog primitive (CLAUDE.md rule #8).
- No `ToggleGroup` / headless model around `Switch` — it is a plain primitive wrapper (per Q UI-1).
- No payments UX — Plan Manage CTA is disabled with `TODO(P2-12-PAYMENTS)`.
- No native OS push-permission flow on the Push toggle — preference persistence only.
- No device/IP/UA in the sessions list — BE only exposes `{sessionId, isActive, isExpired, expiresAt, remainingSeconds}`.
- No grade/language/country pre-fill in the Linked-children Edit form — `LinkedChildResponse` lacks those fields; form opens with empty defaults (Q L-1).

## North-star reminders that govern every panel
- **Dark canvas** (`$bg` `#0F172A`) → card **lighter** (`$card` `#1E293B`); never darken on interaction (Brand Law 1, 10).
- **Every button is 16px radius** (`$button`); **panel is 24px radius** (`$modal`); chips/badges are pill (Brand Law 4).
- **One primary action per panel.** Press = scale 0.95 / 80ms; hover (web) = brighten + 1.02; disabled = opacity 0.4, drop glow, no layout shift (Brand Law 10).
- **Focus ring** = 2px `$primary` + outer 4px `$primaryGlow` (~30% indigo). Always visible on keyboard nav (Brand Law 6).
- **Voice:** short, second-person, encouraging. Title Case on buttons/headings, sentence case on body. Exclamation only on real wins — none in these panels (Brand Law 8). AR copy comes from the SKILL.md cheat sheet, not invented.
- **Logical RTL** — flex rows use `flexDirection={rowDir}` (lets `dir=rtl` flip once); paddings/borders use `paddingStart`/`borderStartWidth`/`borderEndWidth`; never hard-code `left`/`right`. Email + sessionId stay `forceLtr` + Latin numerals (technical strings).

---

# 0. New shared primitive — `Switch` (`packages/ui/src/components/Switch/index.tsx`)

A brand-new primitive: a touch-target wrapped 44×24 pill track + 20px white thumb, mirrors `CheckboxField`'s prop shape. **No abstraction beyond a token-driven wrapper around Tamagui's native `Switch`** (per Q UI-1 + CLAUDE.md #8).

## Prop shape (mirrors `CheckboxField`)

```ts
export interface SwitchProps {
  value: boolean;
  onValueChange: (next: boolean) => void;
  /** Optional inline label rendered to the logical START of the track. */
  label?: string;
  /** If true, label is visually hidden but kept in DOM for a11y. */
  hideLabel?: boolean;
  disabled?: boolean;
  direction?: Direction;             // 'ltr' | 'rtl'
  locale?: string;                   // resolved → direction if `direction` absent
  /** REQUIRED for a11y (composed from t() — e.g. "Email — Weekly report"). */
  accessibilityLabel: string;
  testID?: string;
}
```

## Visuals — pulled verbatim from `web-toggle.html`

| Element | Property | Token / value | Card line |
|---|---|---|---|
| Track | width × height | **44 × 26 px** (matches `web-toggle.html` track: `width:44px;height:26px`) | line 6, 10 |
| Track | radius | `9999` (pill) | `border-radius:9999px` |
| Track ON | background | `$primary` (`#4F46E5`) | `background:#4F46E5` |
| Track ON | outer glow | `0 0 12px rgba(99,102,241,0.40)` → web-only via `boxShadow`; on native fall back to `shadowColor=$primaryGlow shadowRadius=8 shadowOpacity=1` | `box-shadow:0 0 12px rgba(99,102,241,0.4)` |
| Track OFF | background | `$cardSoft` (`#334155`) | `background:#334155` |
| Track DISABLED | — | `opacity: 0.4` on the whole component | Brand Law 10 |
| Thumb | width × height | **20 × 20 px** | `width:20px;height:20px` |
| Thumb | radius | `9999` (circle) | `border-radius:50%` |
| Thumb | background | `$fg1` (`#F8FAFC`) | `background:#fff` |
| Thumb | shadow | `0 2px 6px rgba(0,0,0,0.30)` (use `$shadowSoft` if present, else literal — sibling-consistent) | `box-shadow:0 2px 6px rgba(0,0,0,0.3)` |
| Thumb position OFF | logical-start inset | **3 px** from track start | `left:3px;top:3px` |
| Thumb position ON | logical-end inset | **3 px** from track end (track 44 − thumb 20 − 3 = **21 px** from start) | `left:21px;top:3px` |
| Focus ring | outline | 2 px `$primary` + outer 4 px `$primaryGlow` (~30%) — Brand Law 6 | matches `--lx-focus-ring` |
| Outer touch target | minHeight | **44 × 44 px** (Brand Law 10 / Skill 8) — track centred inside | a11y |

## RTL behaviour
- Container row uses `flexDirection={rowDir}` so label + track swap sides via the parent's `dir`.
- Thumb position is expressed with **logical CSS-in-JS** props on web: `insetInlineStart: 3` (OFF) / `insetInlineStart: 21` (ON). On native (RN), drive the thumb's `left` value from `dir === 'rtl' ? (trackW − thumbW − 3) : 3` for OFF and the inverse for ON. Either way, **ON state = thumb at logical end**, OFF = logical start.
- **Do NOT mirror** the glow — it is symmetric.

## Motion
- Thumb `transform: translateX(...)` + track `background-color` transition: **160 ms** `cubic-bezier(0.16, 1, 0.3, 1)` (snappy spring-out, no overshoot — sub-200 ms keeps the rapid-toggle micro-interaction).
- Press (the whole touch target) follows the global rule: `pressStyle={{ scale: 0.95 }}` for 80 ms.
- **No** confetti / shimmer / haptic — Switch is a chrome control, not a reward moment (Brand Law 3).

## States table
| State | Visuals | a11y |
|---|---|---|
| default ON | track `$primary` + primary glow, thumb logical-end | `accessibilityState={{ checked: true, disabled: false }}` |
| default OFF | track `$cardSoft`, no glow, thumb logical-start | `{ checked: false, disabled: false }` |
| hover (web, enabled) | track brightens ~8% (`$primaryHover` `#6366F1` when ON, `$borderStrong` `rgba(255,255,255,0.16)` when OFF) | — |
| press | `scale 0.95` for 80 ms (Brand Law 10) | — |
| focus (keyboard) | 2 px `$primary` outline + outer 4 px `$primaryGlow` | visible on Tab |
| disabled | wrapper `opacity: 0.4`, no glow, `pointer-events: none` | `accessibilityState.disabled = true` |
| loading (optimistic) | identical to the target state — **no spinner**; rollback shake (60 ms ±6 px) only if the mutation errors out (Brand Law 12 motion) | — |

## a11y
- `accessibilityRole="switch"` on the track.
- `accessibilityState={{ checked: value, disabled }}` always present.
- `accessibilityLabel` is **required** — composed by callers as `"{categoryLabel} — {channelLabel}"` (e.g. `"Email — Weekly report"`, `"البريد — التقرير الأسبوعي"`).
- Minimum 44×44 px touch target via `hitSlop` (matches `CheckboxField` pattern).
- Label, when provided as `string`, renders as a `Text` with `writingDirection={dir}` and `textAlign={dir === 'rtl' ? 'right' : 'left'}`. With `hideLabel`, render the same node but visually hidden (Tamagui `opacity: 0; pointer-events: none; width:1px; height:1px;` clip — same trick as VisuallyHidden).

## Implementation handoff
- **File:** `packages/ui/src/components/Switch/index.tsx` (NEW).
- Export from `packages/ui/src/index.ts`.
- Tokens only — no raw hex except the 3 px/20 px/44 px geometry (sibling-consistent literals, matching `CheckboxField`'s `22 px` literal box size + `boxRadius=6`).
- If the frontend hits a need to wrap multiple `Switch`es into a group with shared state (`ToggleGroup`), **stop and ask the lead** — that would be a new design pattern.

---

# 1. Notifications panel — `_components/settings/NotificationsPanel.tsx`

**Tab key:** `SETTINGS_TAB.Notifications` · **Icon:** `🔔` · **i18n root:** `parent.settings.notifications.*`.

## Layout (per breakpoint, EN/LTR — AR mirrors via `rowDir`)
```
PanelSurface (extracted from SettingsWeb — radius $modal, bg $card, border rgba(255,255,255,0.06), padding 22, gap 18, borderWidth 1)
├── PanelHeader { title: t('…notifications.title'), subtitle: t('…notifications.subtitle') }
├── Success strip  [conditional — slides in below header after a successful save]
├── ServerErrorBanner  [conditional — replaces success strip on failure]
└── YStack gap 14  (4 category rows)
    └── For each category (Weekly Report / Streak At Risk / Product Announcement / Achievement):
        Stack flexDirection={rowDir} alignItems="center" gap 16 paddingVertical 14 paddingHorizontal 16
              backgroundColor "$bgElevated"  (#111B33 — one notch DEEPER than the panel surface so rows read as inset chips)
              borderRadius 14  (sibling literal — matches web-toggle row card)
              borderWidth 1 borderColor "rgba(255,255,255,0.06)"
        ├── YStack flex 1 gap 2  (label + helper, writingDirection={direction})
        │   ├── Text  $fg1   14 / 700  fontFamily $body   t('…category.{key}.label')
        │   └── Text  $fg3   12 / 400  fontFamily $body   t('…category.{key}.helper')
        └── XStack flexDirection={rowDir} gap 24 alignItems="center"  (two switches; gap 24 keeps them visually balanced)
            ├── YStack alignItems="center" gap 6
            │   ├── Text $fg3 11 / 600 textTransform "uppercase" letterSpacing 0.04em — t('…channel.email')
            │   └── <Switch value={pref.emailEnabled} onValueChange={…} accessibilityLabel={`${categoryLabel} — ${emailLabel}`} direction={direction} />
            └── YStack alignItems="center" gap 6
                ├── Text $fg3 11 / 600 textTransform "uppercase" letterSpacing 0.04em — t('…channel.push')
                └── <Switch value={pref.pushEnabled} … accessibilityLabel={`${categoryLabel} — ${pushLabel}`} direction={direction} />
```

Breakpoint behaviour:
- ≥1024 px (desktop): row stays as above — label-block flex 1, switches column-pair at the logical end.
- 768 px: identical — the row is tight but holds.
- 390 px (mobile fallback for the web PWA): rows **wrap** the switch pair below the label-block (`flexWrap="wrap"`). The PanelSurface itself reaches `minWidth 320`; below 480 px width, give the switch column-pair `marginStart={56}` (visual indent under the label) — sibling-consistent with how mobile inputs stack in `mobile-password-meter.html`.

## Component composition
- `PanelSurface` + `PanelHeader` — **extracted** as siblings in `SettingsWeb.tsx` today; reuse via import. If extraction is awkward, replicate the tiny JSX inline (per plan's Extract Note).
- `Switch` — the new primitive defined in §0.
- `ServerErrorBanner` (app-local) + `useServerError` — matches Profile panel pattern.

## States
| State | Visual |
|---|---|
| loading (`isPending` first paint) | Inside the PanelSurface, a centered `Text $fg3 14 fontFamily $body` "Loading…" — **mirrors the existing `ProfilePanel` skeleton** (Brand Law 5 / sibling consistency). No spinner — the four rows materialise together when the GET resolves. |
| empty (BE never returns empty — always 4 defaults) | N/A — defensive only. If the array is empty, render the 4 defaults locally (all OFF) and let the first toggle PUT seed the BE. |
| success (after `useUpdateNotificationPreferences` succeeds) | Strip at top: `backgroundColor "$successSoft"`, `borderRadius "$sm"` (8), `padding 12`, `accessibilityLiveRegion="polite"`. Text `$success 14 fontFamily $body` — copy from `…saveSuccess`. Auto-dismiss after 3 s (clear via local state). |
| error (mutation rejects) | `ServerErrorBanner` via `useServerError` with `byStatus: { 400: 'parent.settings.notifications.saveError' }`. Rollback the optimistic toggle on rejection. |
| optimistic toggle (in-flight) | Switch shows the **target** state immediately — no spinner / no hourglass. If the PUT errors, rollback (the `Switch` snaps back) + 60 ms ±6 px shake on the row only (Brand Law 12 — wrong-feedback motion). |
| focused (keyboard) | Switch focus ring per §0. |
| disabled (BE down + no cache) | Whole panel content `opacity 0.4`, switches non-interactive. Header stays full-opacity. |

## Tokens (every visible property)
| Element | Property | Token |
|---|---|---|
| PanelSurface bg | background | `$card` |
| PanelSurface border | borderColor | literal `rgba(255,255,255,0.06)` (matches existing align-settings P-05) |
| PanelSurface radius | borderRadius | `$modal` (24) |
| Row bg | background | `$bgElevated` (`#111B33`) — one tier deeper than the panel, gives the row card a soft inset look without darkening the panel itself |
| Row border | borderColor | literal `rgba(255,255,255,0.06)` |
| Row radius | borderRadius | `14` (sibling literal — matches `web-toggle.html` row card) |
| Category label | color / size / weight / family | `$fg1` / 14 / 700 / `$body` |
| Category helper | color / size / weight / family | `$fg3` / 12 / 400 / `$body` |
| Channel eyebrow ("EMAIL"/"PUSH") | color / size / weight | `$fg3` / 11 / 600, uppercase, `letterSpacing 0.04em` |
| Success strip bg / fg | `$successSoft` / `$success` | |
| Error banner | (uses `ServerErrorBanner` tokens) | |

## Motion
- Row enter on first load: parent panel fades in 250 ms ease-out (matches the existing tab-switch transition the rail performs).
- Switch toggle: 160 ms `cubic-bezier(0.16, 1, 0.3, 1)` (§0).
- Success strip: slide-in from top + fade 250 ms ease-out; auto-dismiss after 3 s (fade-out 200 ms).
- Error rollback: switch snap-back + row shake 60 ms ±6 px (Brand Law 12).
- **No** confetti — not a reward moment.

## RTL
- Outer row uses `flexDirection={rowDir}` so in AR the switch pair is on the logical-start side and the label-block on the logical-end side.
- Switch column-pair internal: `flexDirection={rowDir}` (same trick).
- All text `writingDirection={direction}`.
- AR fonts: title via `$heading` → Cairo (when DG-02 from align-settings ships locale-aware fonts); body via `$body` → Tajawal.
- Eyebrows ("EMAIL"/"PUSH") in AR are **localized full words** — `البريد` / `الإشعارات الفورية` — NOT uppercased Latin abbreviations. Arabic uppercase doesn't exist; drop `textTransform: uppercase` when `dir === 'rtl'`.

## a11y
- Each `Switch` has `accessibilityLabel` composed as `"{t(category.label)} — {t(channel.{email|push})}"`.
- Row container has `accessibilityRole="group"` with `accessibilityLabel={t(category.label)}` so screen readers announce "Weekly report — group, Email switch off, Push switch on".
- Minimum 44 px touch target per switch (§0).
- Success strip `accessibilityLiveRegion="polite"`.

## EN + AR copy (verbatim — frontend `t()` keys)
| Key | EN | AR |
|---|---|---|
| `parent.settings.notifications.title` | Notifications | الإشعارات |
| `parent.settings.notifications.subtitle` | Choose what we ping you about | اختر ما نُبلغك به |
| `parent.settings.notifications.category.weeklyReport.label` | Weekly report | التقرير الأسبوعي |
| `parent.settings.notifications.category.weeklyReport.helper` | A weekend recap of your child's progress | ملخص أسبوعي عن تقدّم طفلك |
| `parent.settings.notifications.category.streakAtRisk.label` | Streak at risk | السلسلة في خطر |
| `parent.settings.notifications.category.streakAtRisk.helper` | Heads-up before today's streak breaks | تنبيه قبل أن تنكسر سلسلة اليوم |
| `parent.settings.notifications.category.productAnnouncement.label` | Product announcements | إعلانات المنتج |
| `parent.settings.notifications.category.productAnnouncement.helper` | New features and seasonal events | الميزات الجديدة والفعاليات الموسمية |
| `parent.settings.notifications.category.achievement.label` | Achievements | الإنجازات |
| `parent.settings.notifications.category.achievement.helper` | Badges, level-ups, and big wins | الشارات وتجاوز المستويات والإنجازات الكبيرة |
| `parent.settings.notifications.channel.email` | Email | البريد |
| `parent.settings.notifications.channel.push` | Push | الإشعارات الفورية |
| `parent.settings.notifications.saveSuccess` | Saved. Your preferences are up to date. | تم الحفظ. تفضيلاتك محدّثة. |
| `parent.settings.notifications.saveError` | We couldn't save that. Try again. | تعذّر الحفظ. حاول مرّة أخرى. |

---

# 2. Linked children panel — `_components/settings/LinkedChildrenPanel.tsx`

**Tab key:** `SETTINGS_TAB.LinkedChildren` · **Icon:** `👨‍👩‍👦` · **i18n root:** `parent.settings.linkedChildren.*`.

## Layout
```
PanelSurface
├── XStack flexDirection={rowDir} justifyContent="space-between" alignItems="center" gap 12
│   ├── PanelHeader { title, subtitle }
│   └── <Button variant="primary" size="sm" onPress={() => router.push('/(onboarding)/add-child')}>
│         t('…addChild')   (label: "Add child" / "إضافة طفل")
│       </Button>
├── (loading) centered "Loading…" — same shape as Profile loading state
├── (empty) YStack alignItems="center" gap 12 paddingVertical 32:
│   ├── Text 32px '👨‍👩‍👦'  accessibilityElementsHidden
│   ├── Text $fg1 16/800 fontFamily $heading textAlign center — t('…emptyTitle')
│   ├── Text $fg3 13 fontFamily $body textAlign center maxWidth 320 — t('…emptyBody')
│   └── <Button variant="primary" size="md"> t('…addChild') </Button>
└── (populated) YStack gap 12  (per-child block)
    For each linked child:
    YStack gap 8
    ├── <ChildCard variant="editable" child={{ fullName, meta: child.email }} onEdit={…} onRemove={…} direction={direction} accessibilityLabel={…} />
    ├── (if editingId === child.id)   InlineEditForm — see §2.1
    └── (if unlinkingId === child.id) InlineUnlinkStrip — see §2.2
```

### 2.1 Inline Edit form (expands BELOW the `ChildCard` row)
```
Stack flexDirection="column" gap 14 padding 18
      backgroundColor "$bgElevated"
      borderRadius 14
      borderWidth 1 borderColor "rgba(255,255,255,0.06)"
      marginTop -4  (visually attaches under the ChildCard — sibling literal)
├── XStack gap 14 flexWrap="wrap" flexDirection={rowDir}
│   ├── <TextField flex 1 minWidth 240 label={t('…fullName')} value={…} onChangeText={…} direction={direction} disabled={mutation.isPending} />
│   └── <Select   flex 1 minWidth 240 label={t('…grade')} options={[1..6 with localized labels]} direction={direction} />
├── XStack gap 14 flexWrap="wrap" flexDirection={rowDir}
│   ├── <Select flex 1 minWidth 240 label={t('…language')} options={[{value:'ar',label:t('common.locale.ar')}, {value:'en',label:t('common.locale.en')}]} />
│   └── <Select flex 1 minWidth 240 label={t('…country')} options={COUNTRIES.map(...)} placeholder={t('…countryPlaceholder')} />
├── <ServerErrorBanner /> (conditional)
└── XStack flexDirection={rowDir} justifyContent="flex-end" gap 10 paddingTop 6
    ├── <Button variant="ghost"   size="md" onPress={collapseEdit}> t('common.cancel') </Button>
    └── <Button variant="primary" size="md" loading={mutation.isPending} disabled={!isValid}> t('common.save') </Button>
```

Behaviour:
- Open via `ChildCard.onEdit` → `setEditingId(child.id)`.
- Grade / language / country default to **empty** (Q L-1 — `LinkedChildResponse` doesn't carry them). Parent must pick. Fullname pre-fills from `child.fullName`.
- Submit → `useUpdateChild({ childId, fullName, grade, language, country })`. On success: collapse, invalidate `family.myChildren`, show **per-row success strip** (`$successSoft` chip below the ChildCard, auto-dismiss 3 s) with `t('…updateSuccess')`.
- Press feedback / hover / disabled per Brand Law 10.

### 2.2 Inline Unlink confirm strip (expands BELOW the `ChildCard` row)
```
Stack flexDirection={rowDir} alignItems="center" gap 12 padding 14
      backgroundColor "$dangerSoft"   (rgba(239,68,68,0.18))
      borderRadius 14
      borderStartWidth 3 borderStartColor "$danger"   (LOGICAL — flips automatically in RTL)
      marginTop -4
├── Text flex 1 $fg1 13 fontFamily $body writingDirection={direction}
│       — t('…unlinkConfirmBody', { name: child.fullName })
│       — "Unlink Sara? They'll lose access to their progress." (EN)
│       — "هل تريد فك ارتباط سارة؟ سيفقدون الوصول إلى تقدّمهم." (AR)
├── <Button variant="ghost"   size="sm" onPress={() => setUnlinkingId(null)}> t('common.cancel') </Button>
└── <Button variant="ghost"   size="sm" textColor="$danger" onPress={confirmUnlink}> t('…unlink') </Button>
```

Behaviour:
- Open via `ChildCard.onRemove` → `setUnlinkingId(child.id)`.
- Confirm → `useUnlinkChild({ childId })`.
  - On `400` (last-parent guard): **keep strip open**, swap the body text for `t('…unlinkLastParentError')` rendered in `$danger 12`. Frontend reads the BE error message via `useServerError({ byStatus: { 400: 'parent.settings.linkedChildren.unlinkLastParentError' } })`.
  - On success: close strip, invalidate `family.myChildren`, fade-out the whole child block (250 ms) before removal.
- This is NOT a modal — per Q UI-2 / plan §P10, do not introduce a Dialog primitive.

## Component composition
- `ChildCard` (variant `editable`) — already supports `onEdit` + `onRemove` + `meta`.
- `Button` (primary/ghost, sm/md).
- `TextField`, `Select`.
- `ServerErrorBanner` + `useServerError`.

## States
| State | Visual |
|---|---|
| loading | Centered `Text $fg3 14` "Loading…" inside PanelSurface (mirrors Profile). |
| empty | Centered illustration emoji + title + body + Add child CTA (Brand Law 8 voice — friendly). |
| populated | Stack of `ChildCard`s + inline forms/strips. |
| edit-open | Edit form expands under the row (250 ms slide+fade); other rows stay visible. |
| unlink-open | Confirm strip expands under the row (250 ms). |
| edit-pending | Save button shows `loading` spinner (Button primitive's built-in `loading` prop); fields disabled. |
| unlink-pending | Both strip buttons disabled (opacity 0.4); confirm shows `loading`. |
| edit-success | Per-row success chip (`$successSoft`) auto-dismisses; row reloads with new name. |
| unlink-success | Row + child block fades out 250 ms, then is removed from the list. |
| unlink-last-parent error | Strip stays open, body swaps to `t('…unlinkLastParentError')` in `$danger`. |
| add-child route missing | Button still renders; tap triggers `router.push('/(onboarding)/add-child')`. Confirm route exists (it does — `MyChildrenWeb.tsx` uses the same target). |

## Tokens
| Element | Property | Token |
|---|---|---|
| PanelSurface | (same as §1) | `$card` / `$modal` |
| Add-child button | variant primary, size sm | `$primary` bg, `$button` radius, primary glow |
| Inline form bg | background | `$bgElevated` |
| Inline form border | borderColor | literal `rgba(255,255,255,0.06)` |
| Inline form radius | borderRadius | `14` (sibling) |
| Unlink strip bg | background | `$dangerSoft` |
| Unlink strip start accent | `borderStartWidth: 3 borderStartColor: $danger` | logical RTL |
| Unlink destructive button text | color | `$danger` (button variant stays `ghost` — destructive color in label only, not a new variant) |
| Empty title | `$fg1` 16/800 `$heading` | |
| Empty body | `$fg3` 13/400 `$body` | |

## Motion
- Edit form expand: max-height 0 → auto + opacity 0 → 1, 250 ms ease-out.
- Unlink strip expand: same.
- Row removal on unlink: 250 ms fade + height collapse.
- Save / Unlink button press: scale 0.95 / 80 ms.

## RTL
- All four `XStack flexDirection={rowDir}` rows flip side correctly.
- `ChildCard` already handles its own RTL (avatar at logical start, actions at logical end).
- Unlink strip's `borderStartWidth` accent flips automatically — accent on the **logical start** (left in LTR, right in RTL).
- Form field grid `flexWrap="wrap"` keeps two-column layout at ≥768 px, stacks vertically below.
- AR child names render in Arabic (e.g. "سارة"); email stays Latin + `forceLtr` (technical string).

## a11y
- Add-child Button `accessibilityLabel={t('…addChild')}`.
- `ChildCard` already carries `accessibilityRole="button"` + `accessibilityLabel`.
- Edit/Unlink IconButtons inside `ChildCard` already meet 48×48 px touch.
- Confirm strip: confirm `Button accessibilityLabel={t('…unlinkConfirmAria', { name })}` so screen readers say "Unlink Sara, button".
- Inline form: `accessibilityRole="form"` (web only), with each field's existing `accessibilityLabel`.

## EN + AR copy
| Key | EN | AR |
|---|---|---|
| `parent.settings.linkedChildren.title` | Linked children | الأطفال المرتبطون |
| `parent.settings.linkedChildren.subtitle` | Manage who is on your account | أدِر من على حسابك |
| `parent.settings.linkedChildren.addChild` | Add Child | إضافة طفل |
| `parent.settings.linkedChildren.emptyTitle` | No children yet | لا يوجد أطفال بعد |
| `parent.settings.linkedChildren.emptyBody` | Link your first child to start tracking their progress. | اربط طفلك الأول لتبدأ متابعة تقدّمه. |
| `parent.settings.linkedChildren.edit` | Edit | تعديل |
| `parent.settings.linkedChildren.unlink` | Unlink | فك الارتباط |
| `parent.settings.linkedChildren.unlinkConfirmTitle` | Unlink {name}? | فك ارتباط {name}؟ |
| `parent.settings.linkedChildren.unlinkConfirmBody` | They'll lose access to their progress on your account. | سيفقد الوصول إلى تقدّمه على حسابك. |
| `parent.settings.linkedChildren.unlinkLastParentError` | You can't unlink the only parent on this child's account. | لا يمكنك فك ارتباط ولي الأمر الوحيد لهذا الطفل. |
| `parent.settings.linkedChildren.updateSuccess` | Saved. | تم الحفظ. |
| `parent.settings.linkedChildren.updateError` | We couldn't save that. Try again. | تعذّر الحفظ. حاول مرّة أخرى. |
| `parent.settings.linkedChildren.unlinkSuccess` | {name} has been unlinked. | تم فك ارتباط {name}. |
| `parent.settings.linkedChildren.unlinkError` | We couldn't unlink. Try again. | تعذّر فك الارتباط. حاول مرّة أخرى. |
| `parent.settings.linkedChildren.fullName` | Full name | الاسم الكامل |
| `parent.settings.linkedChildren.grade` | Grade | الصف |
| `parent.settings.linkedChildren.language` | Language | اللغة |
| `parent.settings.linkedChildren.country` | Country | الدولة |
| `parent.settings.linkedChildren.countryPlaceholder` | Choose a country | اختر دولة |

Grade labels (i18n): `parent.settings.linkedChildren.gradeOption.{1..6}` → EN `"Grade {n}"`, AR `"الصف {n}"` with `{n}` rendered as **Eastern-Arabic numerals** (`١..٦`) in the AR locale per SKILL.md Skill 4 (in-line text rule).

---

# 3. Security panel — `_components/settings/SecurityPanel.tsx`

**Tab key:** `SETTINGS_TAB.Security` · **Icon:** `🛡️` · **i18n root:** `parent.settings.security.*`.

## Layout — two sub-sections in one `PanelSurface`, divider between
```
PanelSurface
├── PanelHeader { title: t('…security.title'), subtitle: t('…security.subtitle') }
│
├── ─── Change-password sub-section ───
│   YStack gap 14
│   ├── (full-width row) <TextField forceLtr secureTextEntry showHideToggle
│   │       label={t('…currentPassword')} autoComplete="current-password" value={…} … />
│   ├── (full-width row) <TextField forceLtr secureTextEntry showHideToggle
│   │       label={t('…newPassword')}     autoComplete="new-password" value={newPwd} … />
│   ├── <PasswordStrengthMeter value={newPwd} direction={direction} />
│   ├── (full-width row) <TextField forceLtr secureTextEntry showHideToggle
│   │       label={t('…confirmPassword')} autoComplete="new-password" value={…} … />
│   ├── (inline error text — local mismatch hint)
│   │     Text $danger 12 $body writingDirection={direction}
│   │       — t('…mismatchError') when newPwd !== confirmPwd
│   │       — t('…sameAsCurrent')  when newPwd === currentPwd
│   ├── <ServerErrorBanner />  (conditional)
│   ├── (success strip — same shape as Notifications) — copy: t('…saveSuccess')
│   └── XStack flexDirection={rowDir} justifyContent="flex-end" gap 10 paddingTop 6
│        └── <Button variant="primary" size="md" loading={…} disabled={!isValid}> t('…save') </Button>
│
├── ─── Divider ─── (Stack height 1 marginVertical 8 backgroundColor "rgba(255,255,255,0.06)")
│
└── ─── Active sessions sub-section ───
    YStack gap 12
    ├── XStack flexDirection={rowDir} justifyContent="space-between" alignItems="flex-start" gap 12
    │   ├── YStack gap 2
    │   │   ├── Text $fg1 14/800 $heading — t('…sessionsTitle')
    │   │   └── Text $fg3 12/400 $body  — t('…sessionsSubtitle')
    │   └── <Button variant="ghost" size="sm" loading={signOutMutation.isPending}> t('…signOutOthers') </Button>
    ├── (loading) centered "Loading…"
    ├── (empty — no sessions returned, defensive) "No active sessions" $fg3 13 centered
    └── (populated) YStack gap 8 — for each session:
        Stack flexDirection={rowDir} alignItems="center" gap 12 paddingVertical 12 paddingHorizontal 14
              backgroundColor "$bgElevated"
              borderRadius 14
              borderWidth 1 borderColor "rgba(255,255,255,0.06)"
        ├── YStack flex 1 gap 2
        │   ├── Text $fg1 13/700 $body forceLtr (fontVariant ['tabular-nums'])
        │   │     — truncated id "{sessionId.slice(0,8)}…"   e.g. "a3f1e0c2…"
        │   └── Text $fg3 12/400 $body  forceLtr (Latin date)
        │     — t('…expiresAt', { date: new Date(expiresAt).toLocaleString(locale) })
        └── <Badge variant={isActive ? 'success' : 'danger'}> t(isActive ? '…sessionActive' : '…sessionExpired') </Badge>
```

## Component composition
- `PanelSurface` + `PanelHeader`.
- `TextField` (with the existing `secureTextEntry` + show/hide toggle + `forceLtr`).
- `PasswordStrengthMeter` (existing, used in Register / P1-11 v2).
- `Button` (primary md / ghost sm).
- `Badge` (success / danger variants — already exported from `@learnexia/ui`).
- `ServerErrorBanner`.

## States
| State | Visual |
|---|---|
| change-password — fields empty | Save button `disabled` (opacity 0.4, no glow). |
| change-password — invalid (mismatch / same as current) | Inline `$danger 12` hint under the affected field; Save disabled. |
| change-password — submitting | Save button `loading` (spinner inside the button); fields disabled. |
| change-password — success | Clear all 3 fields; success strip `$successSoft` shows `t('…saveSuccess')` ("Password updated — other sessions have been signed out.") for 4 s; `useMySessions` is invalidated by the success path so the sessions list refreshes. |
| change-password — error (400 wrong current) | `ServerErrorBanner` with `t('…saveErrorCurrent')`; fields stay populated. |
| change-password — error (400 policy fail) | `ServerErrorBanner` with `t('…saveErrorPolicy')`. |
| sessions — loading | "Loading…" centered. |
| sessions — empty (defensive) | "No active sessions" $fg3 13 centered. |
| sessions — populated | Strip list per row. |
| sessions — signing-out-others | Sign-out button `loading`; rows stay visible until refetch. |
| sessions — signing-out success | Re-fetch fires; old rows fade out + new (single — your own session) row fades in (250 ms). Success strip on the right of the sub-header for 3 s: `t('…signOutOthersSuccess', { count })` — "Signed out {count} other sessions." / "تم تسجيل خروج {count} جلسة أخرى." |

## Tokens
| Element | Property | Token |
|---|---|---|
| Section divider | bg / height | literal `rgba(255,255,255,0.06)` / 1 px |
| Session row bg | background | `$bgElevated` |
| Session row radius | borderRadius | `14` |
| Session id text | color / family / fontVariant | `$fg1` / `$body` / `tabular-nums` |
| Session expiresAt | color | `$fg3` |
| Active badge | variant `success` → bg `$successSoft` / fg `$success` | |
| Expired badge | variant `danger`  → bg `$dangerSoft`  / fg `$danger`  | |
| Strength meter colours | use the existing component's palette (`$danger` weak → `$accent` fair → `$secondary` strong) — see `mobile-password-meter.html` | |

## Motion
- Strength meter: width transition 200 ms ease-out (matches existing primitive).
- Sessions list re-fetch: 250 ms cross-fade between old and new list.
- Form submit success: clear-form is **instant**, then success strip slides in 250 ms.

## RTL
- Outer two sub-sections stack vertically — no flip.
- Sub-header row uses `flexDirection={rowDir}` so the Sign-out CTA appears at the logical-end.
- Session row uses `flexDirection={rowDir}` — the truncated id stays `forceLtr` (Latin string) regardless of locale: wrap the id `Text` in a `<Stack direction="ltr">` so the inner truncation reads L→R even inside an RTL row. The badge sits at the logical-end.
- `expiresAt` rendered via `toLocaleString(locale)` — in AR locale this auto-renders Eastern-Arabic numerals for the date; that **is desired** for the date (in-line reading text per SKILL.md Skill 4).
- Password fields are `forceLtr` (already the project pattern for passwords).
- AR fonts: headings → Cairo (DG-02), body → Tajawal.

## a11y
- All 3 password `TextField`s have `accessibilityLabel` matching the label (existing primitive behaviour). NO `accessibilityValue` on password fields — screen readers must not announce the value.
- Web `autocomplete` attributes: `current-password` on field 1, `new-password` on fields 2 + 3.
- `Save` Button `accessibilityLabel={t('…save')}` + `accessibilityState={{ disabled: !isValid }}`.
- Sessions section: outer container `accessibilityRole="region"`; each session row `accessibilityRole="listitem"`. Sub-section container has `accessibilityRole="list"`.
- Sign-out button: `accessibilityLabel={t('…signOutOthers')}`.
- Inline mismatch hints `accessibilityLiveRegion="polite"` so the message announces when typed.

## EN + AR copy
| Key | EN | AR |
|---|---|---|
| `parent.settings.security.title` | Security | الأمان |
| `parent.settings.security.subtitle` | Manage your password and sessions | أدِر كلمة المرور والجلسات |
| `parent.settings.security.currentPassword` | Current password | كلمة المرور الحالية |
| `parent.settings.security.newPassword` | New password | كلمة المرور الجديدة |
| `parent.settings.security.confirmPassword` | Confirm new password | تأكيد كلمة المرور الجديدة |
| `parent.settings.security.save` | Update Password | تحديث كلمة المرور |
| `parent.settings.security.saveSuccess` | Password updated. Other sessions have been signed out. | تم تحديث كلمة المرور. تم تسجيل خروج الجلسات الأخرى. |
| `parent.settings.security.saveErrorCurrent` | That current password isn't right. Try again. | كلمة المرور الحالية غير صحيحة. حاول مرّة أخرى. |
| `parent.settings.security.saveErrorPolicy` | Your new password doesn't meet our policy. | كلمة المرور الجديدة لا تستوفي السياسة. |
| `parent.settings.security.mismatchError` | The two new passwords don't match. | كلمتا المرور غير متطابقتين. |
| `parent.settings.security.sameAsCurrent` | New password can't match your current one. | كلمة المرور الجديدة لا يمكن أن تطابق الحالية. |
| `parent.settings.security.sessionsTitle` | Active sessions | الجلسات النشطة |
| `parent.settings.security.sessionsSubtitle` | Devices currently signed in to your account | الأجهزة المسجّلة دخولًا حاليًا على حسابك |
| `parent.settings.security.signOutOthers` | Sign Out Other Sessions | تسجيل خروج الجلسات الأخرى |
| `parent.settings.security.signOutOthersSuccess` | Signed out {count} other sessions. | تم تسجيل خروج {count} جلسة أخرى. |
| `parent.settings.security.sessionActive` | Active | نشطة |
| `parent.settings.security.sessionExpired` | Expired | منتهية |
| `parent.settings.security.expiresAt` | Expires {date} | تنتهي {date} |

---

# 4. Plan & billing panel — `_components/settings/PlanPanel.tsx`

**Tab key:** `SETTINGS_TAB.Billing` · **Icon:** `💎` · **i18n root:** `parent.settings.billing.*`.

## Layout
```
PanelSurface
├── PanelHeader { title: t('…billing.title'), subtitle: t('…billing.subtitle') }
├── (loading) centered "Loading…"
└── (loaded) YStack gap 18
    ├── XStack flexDirection={rowDir} alignItems="center" gap 12 flexWrap="wrap"
    │   ├── Text $fg3 12/600 textTransform "uppercase" letterSpacing 0.04em  — t('…planLabel')   "PLAN"
    │   ├── Text $fg1 22/800 $heading writingDirection={direction}            — {plan.planName}    e.g. "Free" / "مجاني"
    │   └── <Badge variant={plan.status === 'Active' ? 'success' : 'neutral'}> {t(plan.status === 'Active' ? '…statusActive' : '…statusInactive')} </Badge>
    ├── Text $fg2 14/400 $body lineHeight 22 maxWidth 480 writingDirection={direction}
    │       — t('…upgradeComingSoon')
    │       — "You're on the Free plan. Paid upgrades land soon — we'll let you know." (EN)
    │       — "أنت على الخطة المجانية. خطط الاشتراك قادمة قريبًا، سنُعلمك." (AR)
    └── XStack flexDirection={rowDir} alignItems="center" gap 10 paddingTop 6
        ├── {/* TODO(P2-12-PAYMENTS): wire to checkout once the payments BE ships. */}
        │   <Button variant="primary" size="md" disabled accessibilityLabel={t('…manage')}> t('…manage') </Button>
        └── Text $fg4 12/400 $body writingDirection={direction} — t('…managePending')
```

## Component composition
- `PanelSurface` + `PanelHeader`.
- `Badge` (variants `success` + `neutral` — the latter falls back to `$cardSoft` bg / `$fg3` fg, the standard neutral pill).
- `Button` primary `md` **disabled** (opacity 0.4, no glow per Brand Law 10).

## States
| State | Visual |
|---|---|
| loading | Centered "Loading…" inside PanelSurface (mirrors Profile). |
| loaded — Free / Active | Plan name "Free" / "مجاني" + `success` Badge. |
| loaded — non-Active | Plan name + `neutral` Badge with `…statusInactive`. |
| loaded — error (404 / 500) | `ServerErrorBanner` at top of the panel; "Loading…" replaced by the banner. |
| manage CTA | Always **disabled** in W10. Hover gives no brighten (disabled stays at opacity 0.4). |

## Tokens
| Element | Property | Token |
|---|---|---|
| PLAN eyebrow | `$fg3` / 12 / 600 / uppercase / `letterSpacing 0.04em` | |
| Plan name | `$fg1` / 22 / 800 / `$heading` | (between H3=18 and H2=24 — pulled from `web-plan-card.html` `font-size:22`) |
| Status pill (active) | bg `$successSoft` / fg `$success` (Badge `success` variant) | |
| Status pill (other) | bg `$cardSoft` / fg `$fg3` (Badge `neutral` variant) | |
| Body copy | `$fg2` / 14 / 400 / `$body` / lineHeight 22 | |
| Disabled Manage CTA | `Button primary` with `disabled={true}` → opacity 0.4, no glow | Brand Law 10 |
| Helper line | `$fg4` / 12 / 400 / `$body` | "Coming soon" caption |

## Motion
- Initial render: panel fades in 250 ms ease-out (tab transition).
- Disabled Manage CTA: NO hover/press animation (disabled = no interaction feedback).
- Loading→loaded transition: 200 ms cross-fade.

## RTL
- Top row `flexDirection={rowDir}` so PLAN eyebrow + plan name + badge flow logical-start → end.
- Eyebrow in AR: localized to the full word `الخطة` (Arabic has no Latin-style uppercase eyebrows — drop `textTransform: uppercase` when `dir === 'rtl'`).
- Plan name "Free" → "مجاني" (AR copy).
- Manage CTA + helper line: `flexDirection={rowDir}` keeps the button at logical-start, helper after.
- AR fonts: plan name uses `$heading` → Cairo (DG-02); body → Tajawal.
- Numerals: none on this panel today.

## a11y
- `PanelHeader` already carries the section heading semantics.
- Plan name `accessibilityRole="text"` (default).
- Status `Badge` has `accessibilityLabel={t(active ? '…statusActive' : '…statusInactive')}`.
- Disabled Manage Button: `accessibilityState={{ disabled: true }}`, `accessibilityLabel={t('…manage')}`. **Do NOT add `aria-describedby` pointing at the helper line** — keep semantics simple; the helper reads naturally after the button.

## EN + AR copy
| Key | EN | AR |
|---|---|---|
| `parent.settings.billing.title` | Plan & billing | الخطة والفوترة |
| `parent.settings.billing.subtitle` | Your current plan and upgrade options | خطتك الحالية وخيارات الترقية |
| `parent.settings.billing.planLabel` | Plan | الخطة |
| `parent.settings.billing.statusActive` | Active | نشطة |
| `parent.settings.billing.statusInactive` | Inactive | غير نشطة |
| `parent.settings.billing.manage` | Manage Subscription | إدارة الاشتراك |
| `parent.settings.billing.managePending` | Subscription management coming soon. | إدارة الاشتراك قريبًا. |
| `parent.settings.billing.upgradeComingSoon` | You're on the Free plan. Paid upgrades land soon — we'll let you know. | أنت على الخطة المجانية. خطط الاشتراك قادمة قريبًا، سنُعلمك. |

Plan-name display:
- `Free` (EN) ↔ `مجاني` (AR). Map via a local constant `PLAN_NAME_AR: { Free: 'مجاني', Pro: 'برو', … }`. The BE returns the Latin string; the FE localizes the **display label** while keeping the wire value Latin.

---

# 5. Cross-panel implementation handoff

## Files the frontend must create
- `packages/ui/src/components/Switch/index.tsx` — the new primitive (§0).
- `packages/ui/src/index.ts` — add the `Switch` export.
- `apps/student-app/app/(parent)/_components/settings/NotificationsPanel.tsx`.
- `apps/student-app/app/(parent)/_components/settings/LinkedChildrenPanel.tsx`.
- `apps/student-app/app/(parent)/_components/settings/SecurityPanel.tsx`.
- `apps/student-app/app/(parent)/_components/settings/PlanPanel.tsx`.
- Hooks per plan B1-hooks.
- i18n keys per plan B1-i18n + the verbatim copy tables above.

## Files the frontend must edit
- `apps/student-app/app/(parent)/_components/SettingsWeb.tsx` — replace the `else` branch with a `switch(activeTab)` per the plan. **Do NOT touch the page header, tab rail, sidebar, or `PanelSurface`/`PanelHeader` definitions** (already aligned in P1-11 v2).
- `packages/api-client/src/query/queryKeys.ts` — extend per plan.
- `packages/api-client/src/hooks/index.ts` — re-export new hooks.

## What to mirror (not invent)
- The Profile panel's `PanelSurface` (`borderRadius="$modal"`, `padding 22`, `gap 18`, `borderColor "rgba(255,255,255,0.06)"`).
- Field grid pattern: `flexDirection={rowDir}` + `gap 14` + `flexWrap="wrap"` + `flex 1 minWidth 240` per column.
- Action row pattern: `justifyContent="flex-end"`, `gap 10`, `paddingTop 6`, primary md + ghost md.
- Success strip pattern: `$successSoft` bg + `$success` text + `borderRadius "$sm"` + `padding 12` + `accessibilityLiveRegion="polite"`.
- Error pattern: `ServerErrorBanner` + `useServerError` with `byStatus` map.
- Loading pattern: centered `Text $fg3 14 $body "Loading…"` inside the PanelSurface — no spinner primitive.

## What NOT to introduce (CLAUDE.md #8 — ask first)
- A shared `Dialog`/`Modal` primitive — Unlink uses an inline strip.
- A `ToggleGroup` headless model — Switch is a leaf primitive.
- A new Button variant for "destructive" — use `ghost` + `$danger` text color on the label.
- A new Skeleton/Spinner component — use the centred "Loading…" text pattern.
- A new `Banner` primitive — reuse `ServerErrorBanner` + the success strip pattern.

# 6. Design gaps / open questions
- **DG-W10-01 — Locale-aware font swap (carry-forward from align-settings DG-02).** AR text in these 4 panels assumes `$heading` resolves to Cairo and `$body` to Tajawal. If the Tamagui font config still resolves both to Poppins regardless of locale, all four panels will ship in Poppins for AR — visible regression. Frontend must verify `packages/design-system/src/fonts/index.ts` and either flip token resolution by locale or pass `fontFamily="$arHeading"`/`"$arBody"` explicitly in AR contexts. **Blocks pixel-perfect AR.**
- **DG-W10-02 — `Switch` glow on native.** The on-state primary glow renders cleanly on web via `boxShadow`. On native (RN), the closest equivalent is `shadowColor + shadowRadius + shadowOpacity` which produces a softer effect. The W10 target is web PWA only — native parity can ship later. Flagged so the implementer doesn't try to match pixel-perfectly on iOS/Android.
- **DG-W10-03 — Truncated session id readability.** Truncating to 8 chars + ellipsis (e.g. `a3f1e0c2…`) is fine for distinguishing sessions but provides no human meaning. A follow-up (P6-06) should add `userAgent` / `lastSeen` to `SessionInfo`. No action this wave.
- **DG-W10-04 — Plan-name localization.** The BE returns the plan name as a Latin string (`"Free"`). For W10 we map it on the FE via a small `PLAN_NAME_AR` constant. When the catalog grows, this should move to the BE response or an i18n namespace. No action this wave.
- **DG-W10-05 — Eastern-Arabic numerals in the date column.** `toLocaleString('ar')` will emit Eastern-Arabic digits by default on most engines; this is correct per SKILL.md Skill 4. **However** the truncated session id stays Latin + `forceLtr` (technical string). Frontend must wrap the id `Text` in a `dir="ltr"` Stack to prevent the parent RTL row from reversing the truncated string.

Design spec ready for frontend.
