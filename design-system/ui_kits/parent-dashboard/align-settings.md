# Pixel-Alignment Delta Spec — Settings (parent dashboard web)

**Captures:** `design-system/screenshots/web/07-settings.png` (EN/LTR) · `design-system/screenshots/web-ar/07-settings.png` (AR/RTL)
**Preview cards consulted:** `web-sidebar.html`, `web-page-header.html`, `web-settings-tabs.html`, `components-input.html`, `ar-input.html`, `web-toggle.html`, `web-2fa-card.html`, `web-security-strip.html`, `web-linked-rows.html`, `web-plan-card.html`, plus token cards (`colors_and_type.css`, `tokens/index.ts`, `tokens/colors.ts`).
**Implementation diffed:** `apps/student-app/app/(parent)/settings.tsx`, `_components/SettingsWeb.tsx`, `_components/Sidebar.tsx`, `packages/ui/src/components/Tabs/index.tsx`, `Button/index.tsx`, `TextField/index.tsx`, `Select/index.tsx`.
**i18n cross-checked:** `packages/shared/src/i18n/resources.ts` (en + ar).
**Kit reference:** `ui_kits/parent-dashboard/PagesApp.jsx` (`SettingsWebPage` + `SettingsProfile` + helpers), `Components.jsx` (`PDSidebar`, `PDHeader`, `PDPanel`), `index-ar.html` (AR settings page).

Scope: six-tab rail + Profile panel (functional) + Language panel (functional) + coming-soon placeholder panel + Sidebar + page header, in EN/LTR and AR/RTL. Shared-component changes (Sidebar, PDHeader/page-header) are flagged [SHARED].

---

## Delta Table

Severity legend: **Blocker** (visible hard break from capture), **Major** (noticeable gap), **Minor** (fractional / polish).

---

### SECTION 1 — Sidebar (SHARED — also affects My Children / Overview screens)

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---------|---------|----------------------|----------|-----|
| S-01 | Sidebar background | `$bg` = `#0F172A` | Same — matches capture. No change. | — | — |
| S-02 | Sidebar border | `borderEndWidth=1 borderEndColor=$border` (`rgba(255,255,255,0.08)`) | Preview card (`web-sidebar.html`) uses `border-right:1px solid rgba(255,255,255,0.06)`. Capture reads as ~6% opacity. Current `$border` = `rgba(255,255,255,0.08)` is close but slightly brighter than the reference card. | Minor | Change `borderEndColor` to a new local `rgba(255,255,255,0.06)` const (no new token needed; this matches `--lx-border` 0.06 in CSS vars; the Tamagui color token `$border` was rounded to 0.08 — use the literal 0.06 here). |
| S-03 | Brand mark size | `width=32 height=32` | Preview card: `width:32px;height:32px` — matches. Kit (`Components.jsx` PDSidebar) uses 36px. Capture shows ~32–36px; 32 is acceptable. | Minor | Keep at 32px (matches `web-sidebar.html` reference card exactly). |
| S-04 | Brand wordmark font-size | `fontSize={20}` | Preview card (`web-sidebar.html`): `font-size:16px`. `Components.jsx` PDSidebar: `font-size:18px`. EN capture: "Learnexia" reads at approximately 16–18px. | Minor | Reduce to `fontSize={18}` (matching Components.jsx PDSidebar = 18px, `--lx-size-h3`=18px). Current 20px is one step too large. |
| S-05 | Brand wordmark font-weight | `fontWeight="800"` | Both card and Components.jsx: `font-weight:800`. Matches. | — | — |
| S-06 | Child-selector card radius | `borderRadius="$card"` = 20px | Preview card (`web-sidebar.html`): `border-radius:14px`. Components.jsx PDSidebar: `border-radius:16px`. Capture: rounded card, clearly less than 20px. Gap: `$card`=20px overshoots; the sidebar child card is more compact. | Major | Change child-selector card `borderRadius` to `$button` (16px, matching Components.jsx PDSidebar). This is not a design-system bug; the sidebar uses a smaller radius than a full page card. |
| S-07 | Child-selector card padding | `padding="$3"` = 12px | Components.jsx: `padding:12px`. Matches. | — | — |
| S-08 | Child-selector avatar size | `size="sm"` | Components.jsx: 36×36px circle. Preview `web-sidebar.html`: 32×32px. Capture shows ~32–36px diameter. `Avatar size="sm"` should resolve to 32–36px — acceptable. | — | — |
| S-09 | Child-selector name font-size | `fontSize={15}` | Components.jsx PDSidebar: `font-size:13px`. `web-sidebar.html`: `font-size:12px`. Capture: approximately 12–13px. | Major | Reduce to `fontSize={13}` (matching Components.jsx). Current 15px is clearly too large at sidebar width. **[SHARED]** |
| S-10 | Child-selector sub-label font-size | `fontSize={12}` | Components.jsx: `font-size:11px`. Preview card: `font-size:10px`. | Minor | Reduce to `fontSize={11}` to match Components.jsx. **[SHARED]** |
| S-11 | Child-selector chevron | `Text fontSize={16} scaleX={isRtl ? -1 : 1}` with `›` | Components.jsx: `font-size:16px` and uses `›`. Matches. In AR: the `scaleX={-1}` mirrors it but `›` is already a right-arrow glyph — when mirrored it faces left, pointing toward start-of-screen, which is the direction "inward" in RTL sidebar. Capture AR shows `‹` style. | Minor | RTL: prefer Unicode `‹` character at `scaleX=1` (no transform) rather than `›` with `scaleX=-1`, to avoid subpixel anti-aliasing artifacts on web. No layout change. |
| S-12 | Nav item gap | `gap="$1"` = 4px | Components.jsx and preview: `gap:2px`. | Minor | Change nav `YStack` gap from `$1` (4px) to 2px (literal, no token). Or use `gap={2}`. |
| S-13 | Nav item height / padding | `minHeight={48} paddingHorizontal="$3"` = 48px×12px | Components.jsx: `padding:'10px 12px'`. Preview card (`web-settings-tabs.html`): `padding:10px 14px`. Tab items in capture: ~36–40px tall visually. 48px minimum height pushes the rail too tall. | Major | Change `minHeight` to 40px (target per capture), keep `paddingHorizontal="$3"` (12px). Add `paddingVertical` of 10px explicitly: `paddingVertical={10}`. This keeps the 48px touch target via `hitSlop` rather than visual height. **[SHARED]** Nav items look identical to Settings tab items in the capture. |
| S-14 | Active nav item radius | `borderRadius="$button"` = 16px | Components.jsx PDSidebar: `border-radius:12px`. Preview cards: 12px. Capture: visibly 12px pill on nav items — not 16px. | Major | Change to `borderRadius={12}` (literal; this is between `$sm`=8 and `$button`=16; per spec rule "buttons=16" but nav chips are 12 per all preview cards). **[SHARED]** Note: `$button`=16 is for `<button>` CTA elements, not for nav-chip containers. |
| S-15 | Active nav item background | `$primarySoft` = `rgba(79,70,229,0.18)` | Components.jsx: `rgba(79,70,229,0.18)`. Preview `web-sidebar.html`: same. **Matches.** However the Sidebar uses `$primarySoft` while the Settings Tabs component uses `$primarySoftStrong` (`rgba(79,70,229,0.28)`). Capture: the sidebar nav active item is lighter than the settings tab active item. | Major | Keep Sidebar active nav item at `$primarySoft` (0.18) — already correct. The Settings Tabs component already uses `$primarySoftStrong` (0.28) — also correct. No code change. **[SHARED]** Confirm `Tabs.tsx` and `Sidebar.tsx` intentionally differ: Sidebar = 0.18, Tabs = 0.28. This is by design per the capture. |
| S-16 | Active nav item left border | `borderStartWidth={3} borderStartColor="$primary"` | Components.jsx PDSidebar does NOT have a left border accent — it only uses background tint. Preview `web-sidebar.html` also no border. **Capture**: no visible 3px left border on the sidebar nav active item. | Blocker | Remove `borderStartWidth` and `borderStartColor` from the Sidebar nav active row. The border-start accent belongs to the Settings Tabs rail only (confirmed by `web-settings-tabs.html` which also has no border — it uses background + color change). **[SHARED]** |
| S-17 | Active nav item label weight | `fontWeight={isActive ? '700' : '500'}` | Components.jsx: `fontWeight: active === i.id ? 700 : 500`. Matches. | — | — |
| S-18 | Active nav item label color | `color={isActive ? '$fg1' : '$fg2'}` (`#F8FAFC` / `#CBD5E1`) | Components.jsx: active=`#A5B4FC` (primaryLight), inactive=`#94A3B8` (fg3). Preview `web-sidebar.html`: active=`color:#A5B4FC`. Capture: active label is a soft indigo-blue, not white. | Blocker | Change Sidebar active label color from `$fg1` to `$primaryLight` (`#A5B4FC`). Inactive label color from `$fg2` to `$fg3` (`#94A3B8`). **[SHARED]** |
| S-19 | Inactive nav item label color | `color="$fg2"` = `#CBD5E1` | Preview: `#94A3B8`. Capture: muted grey, not `$fg2`. Fix in S-18. | Blocker | See S-18. |
| S-20 | Nav icon size | `fontSize={16}` | Components.jsx: `font-size:16px`. Matches. | — | — |
| S-21 | XP widget card radius | `$card` = 20px | Components.jsx: `border-radius:16px`. Preview `web-sidebar.html`: `border-radius:14px`. Capture: ~14–16px. | Major | Change XP widget `borderRadius` to `$button` (16px) to match Components.jsx. |
| S-22 | XP widget eyebrow font-size | `fontSize={10}` | Components.jsx: `font-size:11px`. | Minor | Change to `fontSize={11}`. |
| S-23 | XP widget value font-size | `fontSize={24}` | Components.jsx: `font-size:20px`. Preview card `web-sidebar.html`: `font-size:18px`. Capture: "+340 XP" is approximately 20–22px. | Major | Change to `fontSize={20}` matching Components.jsx. |
| S-24 | XP widget delta font-size | `fontSize={12}` | Components.jsx: `font-size:11px`. `web-sidebar.html`: `font-size:10px`. | Minor | Change to `fontSize={11}`. |
| S-25 | Sidebar overall padding | `paddingHorizontal="$4"` = 16px, `paddingVertical="$6"` = 24px | Components.jsx PDSidebar: `padding:'24px 16px'`. Preview `web-sidebar.html`: `padding:20px 14px`. Current matches Components.jsx better. Use `paddingHorizontal={16}` + `paddingVertical={24}`. Matches. | — | — |
| S-26 | AR: Sidebar border side | `borderEndWidth=1` (logical) | In RTL the border flips to left edge automatically via logical property — correct. AR capture confirms border is on right side (screen-left for RTL) which is the logical end. | — | — |
| S-27 | AR: child-selector card — role text | Current: `t('parent.childSelector.meta')` renders "Grade 3 · Level 12" | AR capture shows "ولي أمر" (parent role label) not child grade/level. The AR settings sidebar shows the *parent* info card (name=أحمد, sub=ولي أمر), not the child selector card. | Blocker | On the Settings screen, the sidebar child-selector card is showing a child's profile when it should show the parent's role label. **In the AR capture the child-selector card shows the authenticated parent's first name initial + name + "ولي أمر"** — this differs from other screens where the child context is shown. Investigation needed: the Settings screen may need to suppress the child context card OR the capture reflects a different sidebar state. Flag as design gap pending confirmation. See Design Gap DG-01. |
| S-28 | AR: nav labels font | `fontFamily="$heading"` = Poppins | AR must use Cairo for heading labels. The `writingDirection` is set but `fontFamily` stays as Poppins unless overridden by a locale-aware theme. | Blocker | `fontFamily` must resolve to Cairo in AR locale. Ensure `$heading` token resolves to Cairo in the AR font config. Check `packages/design-system/src/fonts/index.ts`. If not locale-switched at token level, callers must pass the resolved font family. See Design Gap DG-02. |

---

### SECTION 2 — Page Header [SHARED — also affects My Children / Overview / Reports]

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---------|---------|----------------------|----------|-----|
| H-01 | Page header border-bottom | Not explicitly set on `SettingsWeb` header `Stack` | `web-page-header.html`: `border-bottom:1px solid rgba(255,255,255,0.06)`. Capture: clear rule separating header from content area. | Major | Add `borderBottomWidth={1} borderBottomColor="rgba(255,255,255,0.06)"` to the header row Stack in `SettingsWeb`. **[SHARED pattern]** |
| H-02 | Page header padding | `padding="$6"` on outer SettingsWeb Stack (24px all-round) | `PDHeader` in Components.jsx: `padding:'20px 32px'`. `web-page-header.html`: `padding:20px 28px`. Capture: header top/bottom ~20px, left ~28–32px. | Major | Extract the header `Stack` from the outer `padding="$6"` wrapper: give the outer container `paddingTop={0} paddingHorizontal={0}` and set the header itself to `paddingVertical={20} paddingHorizontal={28}`. Content area below gets its own `padding="$6"`. Currently everything inherits the single `padding="$6"` which gives only 24px horizontally — 4px short. |
| H-03 | Page title font-size | `fontSize={26}` | `PDHeader` Components.jsx: `font-size:22px`. `web-page-header.html`: `font-size:20px`. Capture: "Settings" title ~22px. `--lx-size-h2`=24px. `fontSize={24}` = `$6` type token = H2. | Major | Change to `fontSize={24}` (`--lx-size-h2`, `$6` in the type scale, H2). Current 26 is between H2 and H1, not a spec token step. |
| H-04 | Page title font-weight | `fontWeight="800"` | Components.jsx: `font-weight:800`. Matches. | — | — |
| H-05 | Page subtitle font-size | `fontSize={14}` | `PDHeader` Components.jsx: `font-size:13px`. `web-page-header.html`: `font-size:12px`. Capture: subtitle is clearly small/muted. | Minor | Change to `fontSize={13}` (matching Components.jsx). |
| H-06 | Page subtitle color | `$fg3` = `#94A3B8` | Components.jsx: `#94A3B8`. Matches. | — | — |
| H-07 | Header gap between title block and controls | `gap="$4"` (16px) | Components.jsx: no explicit gap; uses `display:flex;justify-content:space-between`. Capture: controls pushed to far right with adequate spacing. | Minor | Change to `justifyContent="space-between"` (already set) and keep `gap="$4"`. No visual issue if controls are already space-between. |
| H-08 | "This week" select width | `width={150}` + `Select` component | Components.jsx uses `<select>` at auto width ~120px. Preview card (`web-page-header.html`): select visually ~130px. Capture: "This week ▾" select is compact, approximately 130px. Current 150px is slightly wide. | Minor | Change `width` to 130px or set `minWidth={100} maxWidth={140}`. |
| H-09 | "This week" select styling | `Select` component (custom dropdown) with `hideLabel` | `web-page-header.html`: `background:#1E293B; border:1px solid rgba(255,255,255,0.1); padding:7px 12px; border-radius:10px; font-size:13px; font-weight:600`. Select component uses `$card` bg, `$border` border, 52px height. | Major | The Select component height is 52px tall (by component default), but the header control row uses a compact `height:~36px` select in the preview card. Need to pass `size="sm"` equivalent or style override to make the select match the compact header appearance. Design Gap DG-03: `Select` component has no `size` prop — `sm` height does not exist. Workaround: wrap in a `Stack` with `scaleY` or add a `compact` variant. Flag to frontend. |
| H-10 | "Send Report" button radius | `Button variant="primary" size="sm"` → `$button` = 16px | `web-page-header.html`: `border-radius:10px`. Capture: button is clearly 10px radius, not 16px. | Major | The button spec rule is `$button`=16px for ALL buttons per SKILL.md. However the preview card (`web-page-header.html`) shows 10px. **The preview card is in conflict with the spec rule.** Resolution: follow the spec rule (SKILL.md, rule 4: "buttons 16"). The capture appears consistent with 16px at this size. Keep `borderRadius="$button"` = 16px. Flag the preview card as stale. No code change needed. |
| H-11 | "Send Report" button shadow | `Button` has no explicit shadow | `web-page-header.html`: `box-shadow:0 4px 12px rgba(99,102,241,0.4)`. `--lx-shadow-primary-glow`. Capture: subtle glow under the primary button. | Major | Add `boxShadow` to the "Send Report" `Button` via `style={{ boxShadow: '0 4px 12px rgba(99,102,241,0.4)' }}` (on web only via RN's `shadow*` props). The Button component does not add primary glow by default. See Design Gap DG-04. |
| H-12 | AR: Header layout direction | `flexDirection={rowDir}` flips to `row-reverse` in RTL | AR capture: title block is on the RIGHT, controls on the LEFT — correct for RTL. `row-reverse` achieves this. Matches. | — | — |
| H-13 | AR: Header title copy | `t('parent.settings.title')` = "الإعدادات" | AR capture: "الإعدادات" — matches. | — | — |
| H-14 | AR: Header subtitle copy | `t('parent.settings.subtitle')` = "إدارة حسابك وتفضيلاتك" | AR capture: "إدارة حسابك وتفضيلاتك" — matches. | — | — |

---

### SECTION 3 — Settings Tab Rail

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---------|---------|----------------------|----------|-----|
| T-01 | Tab rail width | `width={210} minWidth={180}` | `SettingsWebPage` in PagesApp.jsx: `gridTemplateColumns:'220px 1fr'` → left rail 220px. Preview `web-settings-tabs.html`: card `max-width:280px` but the rail itself is unrestricted. Capture: rail is approximately 180–200px. | Minor | Change to `width={220}` to match PagesApp.jsx reference. |
| T-02 | Tab item height / padding | `Tabs.tsx`: `minHeight={48}` | Preview `web-settings-tabs.html`: `padding:10px 14px` with no min-height → natural height ~36–40px. Capture: tab items are ~40px, not 48px. 48px makes the rail feel cramped vertically. | Major | Same fix as S-13: tab items should be visually 40px. Change `Tabs.tsx` `minHeight={48}` to `minHeight={40}` with `paddingVertical={10}` + `hitSlop` for a11y. See note: this is a shared component change that also affects any other future use. |
| T-03 | Tab item horizontal padding | `paddingHorizontal="$3"` = 12px | Preview: `padding:10px 14px` → horizontal 14px. | Minor | Change to `paddingHorizontal={14}` (literal; no token — 14px falls between `$3`=12 and `$4`=16). |
| T-04 | Active tab background | `$primarySoftStrong` = `rgba(79,70,229,0.28)` | Preview `web-settings-tabs.html`: `background:rgba(79,70,229,0.18)`. **Discrepancy:** the preview card uses 0.18, but the `Tabs.tsx` component doc says `$primarySoftStrong` (0.28) is the correct active-pill tint per design gap resolution. Capture: active tab "Profile" has a clearly darker indigo tint than the sidebar nav active item. | — | Keep `$primarySoftStrong` (0.28) in `Tabs.tsx`. The preview card (`web-settings-tabs.html`) uses 0.18 because it was authored before the gap resolution. The Tamagui component is correct. No change. |
| T-05 | Active tab label color | `color={isActive ? '$fg1' : '$fg2'}` | Preview `web-settings-tabs.html`: active=`#A5B4FC`, inactive=`#94A3B8`. Capture: active tab "Profile" label is indigo-blue (#A5B4FC / primaryLight), not white. | Blocker | Change `Tabs.tsx` active label color from `$fg1` to `$primaryLight` (`#A5B4FC`). Inactive label from `$fg2` to `$fg3` (`#94A3B8`). This is the same fix as S-18 but for the Tabs component. |
| T-06 | Active tab label weight | `fontWeight={isActive ? '700' : '500'}` | Preview: active=800, inactive=600. | Minor | Change to active=`'800'` (extraBold) and inactive=`'600'` (semiBold). |
| T-07 | Tab item border-radius | `borderRadius="$button"` = 16px | Preview: `border-radius:12px`. Capture: tab pill visually ~12px. Same issue as nav items. | Major | Change to `borderRadius={12}` (literal). See S-14 explanation. |
| T-08 | Active tab accent border | `borderStartWidth={isActive ? 3 : 0}` | Preview card `web-settings-tabs.html`: NO left border. Capture: no visible left border accent on tab items. | Blocker | Remove the `borderStartWidth` / `borderStartColor` logic from `Tabs.tsx`. The active state is communicated by background + label color only. |
| T-09 | Tab icons | `TAB_ICON` map uses `'👤'`, `'🔔'`, `'👨‍👧'`, `'🛡️'`, `'💎'`, `'🌐'` | Preview / PagesApp.jsx: same set but `Linked children`=`'👨‍👩‍👦'`, `Language`=`'🌍'`. Capture EN: icons match `👤 🔔 👨‍👩‍👦 🛡️ 💎 🌍`. | Minor | Fix icon mismatches: `LinkedChildren` → `'👨‍👩‍👦'` (family emoji, not `'👨‍👧'`); `Language` → `'🌍'` (earth globe Americas, not `'🌐'`). |
| T-10 | AR: tab labels | `t('parent.settings.tabs.*)` AR values | resources.ts AR: `profile`='الملف الشخصي', `notifications`='الإشعارات', `linkedChildren`='الأطفال المرتبطون', `security`='الأمان', `billing`='الخطة والفوترة', `language`='اللغة والمنطقة'. AR capture: 'الملف الشخصي' ✓, 'الإشعارات' ✓, 'الأطفال المربوطون' (note: capture uses "المربوطون" while i18n has "المرتبطون"). | Minor | AR copy discrepancy: i18n key `parent.settings.tabs.linkedChildren` = "الأطفال المرتبطون" but capture reads "الأطفال المربوطون". Update resources.ts AR to "الأطفال المربوطون" to match capture. Both are acceptable Arabic but the capture is the pixel target. |
| T-11 | AR: tab text alignment | `writingDirection={direction}` on label Text | RTL: text should align to right (reading start). The `writingDirection='rtl'` handles this. Ensure no explicit `textAlign="left"` overrides it in Tabs.tsx. Currently none — correct. | — | — |
| T-12 | AR: tab rail position | In RTL `flexDirection='row-reverse'` puts the rail on the RIGHT side of the content | AR capture: settings rail is on the RIGHT side — content panel on LEFT. Correct. The `rowDir = isRtl ? 'row-reverse' : 'row'` in `SettingsWeb` achieves this. | — | — |

---

### SECTION 4 — Profile Panel (PanelSurface + PanelHeader + form)

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---------|---------|----------------------|----------|-----|
| P-01 | Panel card radius | `borderRadius="$card"` = 20px | `PDPanel` Components.jsx: `border-radius:24px`. Preview `web-2fa-card.html`: `border-radius:24px`. Capture: profile card has clearly larger radius than a typical card — approximately 24px. | Major | Change `PanelSurface` `borderRadius` from `$card` (20px) to `$modal` (24px). |
| P-02 | Panel card padding | `padding="$6"` = 24px | Components.jsx PDPanel: `padding:22px`. Capture: approximately 22–24px internal padding. `$6`=24px is close. | Minor | Change to `padding={22}` (literal matching PDPanel exactly) to avoid adding extra whitespace vs. the capture. |
| P-03 | Panel card gap | `gap="$5"` = 20px | Components.jsx PDPanel: `gap:18px`. | Minor | Change to `gap={18}`. |
| P-04 | Panel card background | `$card` = `#1E293B` | Components.jsx: `#1E293B`. Matches. | — | — |
| P-05 | Panel card border | `borderWidth={1} borderColor="$border"` = `rgba(255,255,255,0.08)` | Components.jsx: `rgba(255,255,255,0.06)`. | Minor | Change `borderColor` to literal `rgba(255,255,255,0.06)` (matching PDPanel). No new token; same comment as S-02. |
| P-06 | Panel title font-size | `PanelHeader`: `fontSize={18}` | Components.jsx PDPanel: `font-size:16px`. Preview `web-2fa-card.html`: `font-size:16px`. Capture: "Profile" heading inside the card is approximately 16px. | Major | Change `PanelHeader` title `fontSize` from 18 to 16. |
| P-07 | Panel title font-weight | `fontWeight="800"` | Components.jsx PDPanel: `font-weight:800`. Matches. | — | — |
| P-08 | Panel subtitle font-size | `PanelHeader` subtitle `fontSize={13}` | Components.jsx: `font-size:12px`. | Minor | Change to `fontSize={12}`. |
| P-09 | Avatar size | `Avatar size="xl"` | Capture / `SettingsProfile` in PagesApp.jsx: `width:84px; height:84px`. "xl" must resolve to 84px. Verify `Avatar` token mapping. | Minor | Verify `Avatar size="xl"` = 84px. If not, set explicitly via `width={84} height={84}` style override. |
| P-10 | Avatar gradient | Avatar renders initials on a gradient background | Capture EN: orange→red gradient (`#FB923C→#EF4444`) same as `--lx-grad-reward` variant. Capture AR: same. The Avatar component should use the streak/danger gradient for the "A" initial. | Minor | No code change if Avatar already uses the orange-red gradient for initials. Confirm Avatar component gradient strategy — it should cycle through brand accent colors keyed to the user's name initial, not always orange. No spec change. |
| P-11 | "Upload photo" button | `variant="primary" size="sm" disabled` | `SettingsProfile` PagesApp.jsx: `btnPrimary()` (not disabled). Capture: button appears active (not greyed). But functional requirement says stub is disabled until BE-4 ships. | Minor | Intentional deviation: keep `disabled` per scope note. No visual fix needed; disabled opacity 0.4 is acceptable. |
| P-12 | "Remove" button | `variant="ghost" size="sm" disabled` | Capture: "Remove" button has visible border, not greyed. Intentional stub state. | Minor | Same as P-11. Intentional. |
| P-13 | Avatar + buttons gap | `gap="$4"` = 16px row | PagesApp.jsx: `gap:18px`. Capture: approximately 16–18px. | Minor | Change avatar row gap to `gap={18}`. |
| P-14 | Field grid gap | `gap="$4"` = 16px between the two flex columns | PagesApp.jsx field grid: `gap:14px`. Capture: field columns have approximately 14px gap. | Minor | Change field grid `Stack`s gap from `$4` (16px) to `gap={14}`. |
| P-15 | TextField height | `TextField` component: `height={52}` | Preview `components-input.html`: `.input { height:48px }`. `ar-input.html`: `height:48px`. Capture: input height visually ~48px. | Major | `TextField` and `Select` components both use 52px height. This overshoots the design spec input height of 48px from the preview cards. Change `TextField` and `Select` container `height` from 52px to 48px. Also change `TextInput` style height accordingly. |
| P-16 | TextField border-radius | `inputRadius = 14` (local constant) | Preview `components-input.html`: `.input { border-radius:14px }`. `ar-input.html`: 14px. Matches. | — | — |
| P-17 | Field label text-transform | `textTransform="uppercase"` | Preview `components-input.html`: `text-transform:uppercase`. Matches. Capture: field labels ("Full name", "Email", "Phone", "Country") are uppercased. | — | — |
| P-18 | Field label font-weight | `fontWeight="600"` | Preview: `font-weight:600`. Matches. | — | — |
| P-19 | Field label font-size | `fontSize={12}` = `--lx-size-small` | Preview: 12px. Matches. | — | — |
| P-20 | Field label color | `$fg3` = `#94A3B8` | Preview: `color:var(--lx-fg3)`. Matches. | — | — |
| P-21 | TextField background | `$card` = `#1E293B` | Preview: `background:#1E293B`. Matches. | — | — |
| P-22 | TextField border color (default) | `$border` = `rgba(255,255,255,0.08)` | Preview: `border:1px solid rgba(255,255,255,0.1)`. Minor difference. | Minor | Accept current — 0.08 vs 0.10 is imperceptible. No change. |
| P-23 | TextField focus ring | `borderColor="$borderFocus"` + `shadowColor glow` | Preview: `border-color:#4F46E5; box-shadow:0 0 0 4px rgba(99,102,241,0.25)`. Focus ring uses `0 0 0 4px` spread (not the 6px from spec). | Major | The `TextField` shadow implementation uses `shadowRadius={4}` + `shadowOffset={0,0}` which approximates the CSS `box-shadow:0 0 0 4px`. On web via RN-Web this may not render as a true CSS box-shadow spread. Verify web output. If not rendering, add a web-specific `boxShadow` style prop for web platform. |
| P-24 | Email field — display-only | `value={''}` (hardcoded empty) | Capture: email shows "ahmed@email.com". The profile response should include email. However the comment says "email is display-only — not part of the profile-update contract". | Major | Load `profile?.email` into the email TextField `value` (read from the profile response). The `useMyProfile` hook should return email. Currently hardcoded to empty `''` which shows a blank email field — clearly wrong vs. capture. |
| P-25 | Select chevron direction AR | `Select.tsx`: `▾` character with `scaleY={open ? -1 : 1}` | AR: the `▾` glyph is symmetric — `scaleY` flip is fine. In RTL layout the chevron should appear on the LEFT (logical end). `Select` wraps in `dir='rtl'` via `writingDirection` but the chevron Text is absolutely last in the XStack. With `flexDirection='row-reverse'` the chevron moves to left — correct. | — | — |
| P-26 | Country select — flag prefix | PagesApp.jsx shows `'🇸🇦 Saudi Arabia'`. Current code: `COUNTRIES.map((c) => ({ label: locale === 'ar' ? c.ar : c.en }))` | Capture EN: "SA Saudi Arabia" with country code prefix (not flag emoji). Capture AR: "SA السعودية" with flag+code. The `COUNTRIES` constant format determines this. | Minor | Verify `COUNTRIES` entries include flag emoji or country code prefixes. No code change in SettingsWeb needed — format comes from `@learnexia/shared COUNTRIES`. Flag to backend/shared layer. |
| P-27 | Action row direction | `flexDirection={rowDir} justifyContent="flex-end"` | PagesApp.jsx: `justifyContent:'flex-end'` row. Capture EN: "Cancel" left of "Save changes" (both right-aligned). Capture AR: "إلغاء" RIGHT of "حفظ التغييرات" (both left-aligned in RTL). | — | Current `row-reverse` + `flex-end` logic is correct for RTL. No change. |
| P-28 | Action row gap | `gap="$3"` = 12px | PagesApp.jsx: `gap:10px`. | Minor | Change to `gap={10}`. |
| P-29 | Action row padding-top | Not set explicitly | PagesApp.jsx: `paddingTop:6px`. Capture: slight top gap before action buttons. | Minor | Add `paddingTop={6}` to the action row Stack. |
| P-30 | "Save changes" button size | `size="sm"` = 40px height | Capture: buttons appear taller, approximately 44–48px. PagesApp.jsx uses `btnPrimary()` which has no explicit height set (inherits 52px from button component defaults). | Major | Change action buttons from `size="sm"` to `size="md"` to match the capture's button height. |
| P-31 | "Cancel" button variant | `variant="ghost"` | Capture: "Cancel" has visible border outline. `ghost` variant has `borderColor="$borderStrong"`. Matches. | — | — |
| P-32 | AR: profile panel — button order | RTL row-reverse: "حفظ التغييرات" renders first in DOM → appears on LEFT in RTL. "إلغاء" on RIGHT. | AR capture: "إلغاء" is on RIGHT side (farther from content edge), "حفظ التغييرات" on LEFT. With `flexDirection='row-reverse'` + DOM order [Cancel, Save], the Save button ends up on the LEFT (start of RTL row) and Cancel on the RIGHT — correct. | — | — |
| P-33 | AR: field labels in RTL | `textAlign="left"` on field label Text | The `TextField` label has hardcoded `textAlign="left"`. In RTL this should be `textAlign="right"`. | Blocker | Change `TextField` label `textAlign` from `"left"` to `dir === 'rtl' ? 'right' : 'left'`. Same fix applies to `Select` label. |
| P-34 | AR: email field — dir=ltr | Email value must stay `dir="ltr"` per SKILL.md rule (email addresses = Latin + ltr) | AR capture: email field value "ahmed@email.com" is visually left-to-right. The TextInput `writingDirection` must stay `'ltr'` for email regardless of locale. | Blocker | In `TextField`, when `keyboardType='email-address'` or `autoComplete='email'`, force `writingDirection='ltr'` and `textAlign='left'` on the TextInput style regardless of the `direction` prop. Pass a new `forceDirection` prop or handle in `ProfilePanel` by passing `direction='ltr'` to the email TextField. |
| P-35 | AR: phone field — dir=ltr | Phone number "+966 50 123 4567" must be LTR per SKILL.md | AR capture: phone field is LTR. Same fix as P-34. | Blocker | Same solution as P-34: `keyboardType='phone-pad'` / `autoComplete='tel'` fields → force `writingDirection='ltr'`, `textAlign='left'`. |

---

### SECTION 5 — Coming-Soon Panel (Notifications / Linked children / Security / Plan & billing tabs)

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---------|---------|----------------------|----------|-----|
| C-01 | Panel card style | Uses `PanelSurface` (inherits P-01–P-05 fixes) | Same surface as Profile panel — correct reuse. | — | — |
| C-02 | Emoji size | `fontSize={32}` for '🚧' | No preview card reference; reasonable for a placeholder. | — | — |
| C-03 | Title font-size | `fontSize={18}` | `--lx-size-h3`=18px. Acceptable for coming-soon headline. | — | — |
| C-04 | Body font-size | `fontSize={14}` | `--lx-size-body-sm`=14px. Acceptable. | — | — |
| C-05 | AR copy for coming-soon | `t('parent.settings.comingSoon.title')` = "قريباً" / body = "هذا القسم في الطريق. عُد قريباً للاطلاع عليه." | Reasonable AR copy. Not visible in AR capture (only Profile tab shown). | — | — |

---

### SECTION 6 — Language & Region Panel

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---------|---------|----------------------|----------|-----|
| L-01 | Panel card style | Uses `PanelSurface` | Inherit P-01–P-05 fixes. | — | — |
| L-02 | Two-column grid gap | `gap="$4"` = 16px | Same as P-14 fix: 14px. | Minor | Change to `gap={14}`. |
| L-03 | Language panel subtitle copy | EN: "Choose the language and region for your account" | PagesApp.jsx `SettingsLanguage`: "Affects your dashboard, not your children's apps". Capture (EN, language tab not visible — not tabbed there): spec choice. | Minor | The i18n key `parent.settings.language.subtitle` = "Choose the language and region for your account". PagesApp.jsx subtitle differs. Use the i18n value (resources.ts is the canonical copy source). |

---

### SECTION 7 — States and Interactions

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---------|---------|----------------------|----------|-----|
| I-01 | Tab item hover state | `hoverStyle={{ backgroundColor: isActive ? '$primarySoftStrong' : '$card' }}` | Preview: inactive hover → background lightens. `$card`=`#1E293B` is the card background, not a lighter tint. Hover should be `$cardSoft` (`#334155`) for inactive items. | Major | Change inactive hover: `hoverStyle={{ backgroundColor: isActive ? '$primarySoftStrong' : '$cardSoft' }}`. |
| I-02 | Tab item press | `pressStyle={{ scale: 0.98 }}` | SKILL.md: press = scale 0.95. But for a nav rail item (not a CTA button) 0.98 is more subtle — acceptable. | Minor | Keep 0.98 for tab nav (subtle); reserve 0.95 for CTA buttons per Button component. |
| I-03 | Input focus ring — web | See P-23. | See P-23. | Major | See P-23. |
| I-04 | Button hover style | `Button` with `hoverStyle={{ backgroundColor: '$primaryHover' }}` | SKILL.md: hover = brighten. `$primaryHover`=`#6366F1` (lighter indigo). Correct. | — | — |
| I-05 | Button press | `pressStyle={{ scale: 0.95 }}` | SKILL.md rule 9: 0.95. Matches. | — | — |
| I-06 | Select hover on options | `hoverStyle={{ backgroundColor: '$cardSoft' }}` | `$cardSoft`=`#334155`. Correct. | — | — |

---

### SECTION 8 — Typography & Font Resolution

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---------|---------|----------------------|----------|-----|
| F-01 | Latin body/label font | `$body` → Poppins | colors_and_type.css: `--lx-font-body:'Poppins','Tajawal'`. Matches. | — | — |
| F-02 | Latin heading font | `$heading` → Poppins | colors_and_type.css: `--lx-font-display:'Poppins','Cairo'`. Matches. | — | — |
| F-03 | Arabic heading font | `$heading` in AR locale | Must resolve to Cairo. SKILL.md: "headings → Cairo". | Blocker | See DG-02. Confirm `packages/design-system/src/fonts/index.ts` switches `$heading` to Cairo when locale=ar. |
| F-04 | Arabic body font | `$body` in AR locale | Must resolve to Tajawal. SKILL.md: "body → Tajawal". | Blocker | See DG-02. |
| F-05 | Numbers in AR inline text | XP widget: "+340 XP" — Arabic context should use Eastern-Arabic numerals per SKILL.md rule | SKILL.md: "Eastern-Arabic numerals for inline text… Exception: keep Latin numerals for technical strings like '820 / 1000 XP'". The XP value "+340 XP" may be considered a technical/reward string — Latin numerals are the exception. | Minor | Keep Latin numerals for "+340 XP" (exception). Eastern-Arabic only for conversational counts like "٣ أطفال" in plan card. No change to XP widget. |

---

### SECTION 9 — RTL / Layout Direction

| # | Element | Current | Target [token + card] | Severity | Fix |
|---|---------|---------|----------------------|----------|-----|
| R-01 | Outer grid direction | `flexDirection={rowDir}` in SettingsWeb | RTL: `row-reverse` → tab rail appears on RIGHT, content on LEFT. AR capture: correct. | — | — |
| R-02 | Panel internal rows | `flexDirection={rowDir}` in ProfilePanel | RTL: avatar + buttons → row-reverse. AR capture: avatar on RIGHT, upload/remove buttons on LEFT. Correct. | — | — |
| R-03 | Action buttons row | `flexDirection={rowDir} justifyContent="flex-end"` | RTL: both buttons move to LEFT side (logical start). AR capture: buttons are on LEFT side. Correct. | — | — |
| R-04 | Sidebar border side | Logical `borderEndWidth` | RTL: border appears on RIGHT (between content and sidebar which is on the right). AR capture confirms. | — | — |
| R-05 | "Learnexia" brand name | `Text` with no `dir` attribute | SKILL.md: brand name is Latin + `dir="ltr"`. Must render LTR in Arabic layout. | Blocker | Wrap the "Learnexia" brand Text in a Stack with explicit `direction="ltr"` style (or set `writingDirection='ltr'` on that Text) so it doesn't get reversed in RTL layout. |
| R-06 | Logo mark | `Image` — not mirrored | SKILL.md: do not mirror icons/avatars. Logo mark stays unmirrored in RTL. Current: no `scaleX` applied to logo image. Correct. | — | — |
| R-07 | Select dropdown panel position | Dropdown opens with `start={0} end={0}` — full width below trigger | RTL: positions correctly due to logical `start`/`end`. | — | — |

---

### SECTION 10 — i18n Copy Mismatches

| # | Key | EN value in code | AR value in code | Capture EN | Capture AR | Issue | Fix |
|---|-----|-----------------|-----------------|-----------|-----------|-------|-----|
| I18N-01 | `parent.settings.tabs.linkedChildren` | "Linked children" | "الأطفال المرتبطون" | "Linked children" ✓ | "الأطفال المربوطون" | AR copy mismatch vs capture | Update resources.ts AR to "الأطفال المربوطون" |
| I18N-02 | `parent.settings.profile.subtitle` | "This is how Learnexia knows you" | "هكذا تعرفك ليرنيكسيا" | "This is how Learnexia knows you" ✓ | "هكذا يعرفك Learnexia" (AR capture uses Latin "Learnexia") | Minor: AR copy has "ليرنيكسيا" (Arabic transliteration) but AR capture uses the Latin brand name. | Use Latin brand name in the AR copy: "هكذا يعرفك Learnexia". Update resources.ts AR. |
| I18N-03 | `parent.nav.settings` (sidebar label) | "Settings" | "الإعدادات" | "Settings" ✓ | "الإعدادات" ✓ | Matches. | — |

---

## Design Gaps (DG)

| ID | Description | Impact | Resolution |
|----|-------------|--------|------------|
| DG-01 | AR sidebar Settings screen shows parent's own info card ("أحمد / ولي أمر") instead of the child-selector card shown on EN capture and other AR screens. It is unclear if this is intentional (Settings = parent context, not child context) or a kit inconsistency. | Blocker (AR sidebar) | **Ask the designer/lead**: should the Settings screen sidebar suppress the child-selector card and show the parent's own account card instead? If yes, add a `variant="parentContext"` prop to Sidebar or conditionally render a different card when `activeKey === NAV_ITEM.Settings`. |
| DG-02 | Font switching for AR locale — the Tamagui theme tokens `$heading` and `$body` resolve to Poppins regardless of locale. There is no locale-driven font swap in the token system. | Blocker (all AR text) | `packages/design-system/src/fonts/index.ts` must be checked: does it define separate Cairo/Tajawal font faces keyed to the AR font alias? If not, the app must pass `fontFamily` explicitly based on locale in AR contexts. Frontend must verify and implement. |
| DG-03 | `Select` component has no `size` prop. The page-header period picker needs a compact (~36px) select, but the component is hardcoded to 52px height. | Major (header period picker) | Add a `size` prop to `Select` (`sm`=40px, `md`=52px) or create a wrapper `CompactSelect` for header use. |
| DG-04 | Primary button glow shadow (`0 4px 12px rgba(99,102,241,0.4)`) is not applied by the `Button` component in its `primary` variant — it uses no elevation. The web-page-header preview card and all web captures show this glow on primary CTAs. | Major (all primary buttons on web) | Add `boxShadow` (web) / `shadowColor + shadowRadius` (native) to the `Button` `primary` variant. Token: `--lx-shadow-primary-glow` = `0 8px 24px rgba(99,102,241,0.45)` (spec) or `0 4px 12px rgba(99,102,241,0.4)` (capture approximation). |
| DG-05 | `TextField` and `Select` height of 52px overshoots the design-spec preview card value of 48px. This affects every form on the platform. | Major (all forms) | Reduce to 48px as spec'd in `components-input.html`. |

---

## Implementation Handoff

### Files to change

| File | Changes (by delta ID) |
|------|-----------------------|
| `apps/student-app/app/(parent)/_components/Sidebar.tsx` | S-02 (border opacity), S-04 (wordmark 18px), S-06 (card radius $button), S-09 (name 13px), S-10 (sub 11px), S-11 (AR chevron), S-12 (gap 2px), S-13 (minHeight 40px, paddingVertical 10px), S-14 (item radius 12px), S-16 (remove borderStart accent), S-18+S-19 (label colors primaryLight/fg3), S-21 (XP widget radius $button), S-22 (eyebrow 11px), S-23 (XP value 20px), S-24 (delta 11px), S-27 (DG-01 — pending decision), R-05 (brand name dir=ltr) |
| `apps/student-app/app/(parent)/_components/SettingsWeb.tsx` | H-01 (header border-bottom), H-02 (header padding 20/28px), H-03 (title 24px), H-05 (subtitle 13px), H-08 (select width 130px), H-11 (Send Report glow shadow web), P-01 (PanelSurface radius $modal=24px), P-02 (padding 22px), P-03 (gap 18px), P-05 (border rgba 0.06), P-06 (panel title 16px), P-08 (panel sub 12px), P-13 (avatar row gap 18px), P-14 (field grid gap 14px), P-24 (email value from profile.email), P-27–P-29 (action row: gap 10px, paddingTop 6px), P-30 (action buttons size="md"), T-01 (rail width 220px), T-09 (icon fixes: 👨‍👩‍👦 and 🌍) |
| `packages/ui/src/components/Tabs/index.tsx` | T-02 (minHeight 40px, paddingVertical 10px), T-03 (paddingHorizontal 14px), T-05 (active label $primaryLight, inactive $fg3), T-06 (active weight 800, inactive 600), T-07 (radius 12px literal), T-08 (remove borderStart accent), I-01 (hover $cardSoft inactive) |
| `packages/ui/src/components/TextField/index.tsx` | P-15 (height 48px), P-33 (label textAlign per direction), P-34+P-35 (force ltr for email/phone), I-03 (web focus ring — boxShadow prop) |
| `packages/ui/src/components/Select/index.tsx` | P-15 (height 48px — trigger XStack), P-33 (label textAlign per direction), DG-03 (add size prop) |
| `packages/ui/src/components/Button/index.tsx` | DG-04 (primary variant box-shadow/glow on web) |
| `packages/shared/src/i18n/resources.ts` | I18N-01 (AR linkedChildren copy), I18N-02 (AR profile subtitle — use Latin Learnexia) |
| `packages/design-system/src/fonts/index.ts` | DG-02 — audit and implement locale-aware font switching |

### Tokens cited per fix

| Fix | CSS token | Tamagui token | Value |
|-----|-----------|---------------|-------|
| Active label color | `--lx-primary-light` (gap-fill) | `$primaryLight` | `#A5B4FC` |
| Inactive label color | `--lx-fg3` | `$fg3` | `#94A3B8` |
| Active bg (Tabs) | `--lx-primary-soft-strong` | `$primarySoftStrong` | `rgba(79,70,229,0.28)` |
| Hover bg (inactive tab/nav) | `--lx-card-soft` | `$cardSoft` | `#334155` |
| Panel card bg | `--lx-card` | `$card` | `#1E293B` |
| Panel card border | `--lx-border` (CSS) | literal `rgba(255,255,255,0.06)` | — |
| Panel radius | `--lx-radius-modal` | `$modal` | 24px |
| Button/nav chip radius | `--lx-radius-button` | `$button` | 16px |
| Nav chip radius (sidebar+tabs) | no token — literal | 12px | between sm and button |
| Primary glow | `--lx-shadow-primary-glow` | (constant, not token) | `0 4px 12px rgba(99,102,241,0.4)` |
| Input height | `--lx-space-12` adjacent | literal 48px | per `components-input.html` |

---

## Shared-Component Changes Summary

Changes marked **[SHARED]** affect screens beyond Settings:

1. **Sidebar.tsx** — nav item colors (S-18/S-19), nav item active background absence of border (S-16), nav item radius (S-14/S-13/S-12), child-selector font sizes (S-09/S-10), brand wordmark size (S-04), XP widget sizes (S-21–S-24). These all appear on My Children, Overview, Reports, and any future screen using `<Sidebar>`.

2. **Tabs/index.tsx** — active label color (T-05), active label weight (T-06), item radius (T-07), remove border accent (T-08), item height (T-02), hover state (I-01). `Tabs` is currently only used in the Settings screen but will be used by any future tabbed view.

3. **TextField/index.tsx** — height 48px (P-15), label textAlign per direction (P-33), forced ltr for email/phone (P-34/P-35), web focus ring (I-03). All auth and settings forms are affected.

4. **Select/index.tsx** — height 48px (P-15), label textAlign per direction (P-33). All select-containing forms affected.

5. **Button/index.tsx** — primary glow shadow (DG-04). All primary CTAs platform-wide.

These shared changes must be reviewed against the other screens before merging to avoid regressions on My Children / Overview / auth flows.
