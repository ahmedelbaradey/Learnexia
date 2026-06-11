# Design Spec — parent-dashboard-uiux (Parent web dashboard UI/UX redesign)

> Source of truth: in-repo `design-system/` (SKILL.md core rules + Skill 8/9, `ui_kits/parent-dashboard/index.html` · `index-ar.html` · `DashboardComponents.jsx` · `PagesApp.jsx` · `AddChildModal.jsx`, preview cards `web-recommendations.html` / `ar-web-recommendations.html` / `web-weak-areas-list.html` / `web-linked-rows.html` / `web-add-child-modal.html` / `ar-web-add-child-modal.html`), grounded against the Pipeline Brief `docs/briefs/parent-dashboard-uiux.md`.
> Where `index.html` / preview cards conflict with the older `align-*.md` docs, **index/preview wins** (brief §Summary). Captures may show stale subjects (Reading/Art) and a Teacher role — apply product overrides (4 subjects Math/Science/Arabic/English; parent-driven; no teacher).
> Default product locale = **Arabic**, default theme = **dark**. Every screen specced EN/LTR **and** AR/RTL.
> **No app/Tamagui code here** — this is the build spec the `frontend` agent consumes. Tokens cited are the existing `packages/design-system/src/tokens/*` (which mirror `colors_and_type.css`).

---

## 0. State of the code (what already exists — do NOT rebuild)

Read before building; much of the align-* delta work already landed:

- **Sidebar.tsx** already has the align fixes: `$nav` (12px) nav radius, `$primaryLight`/`$fg3` active/inactive labels, no left-border accent, `$cardInner` (16-ish via 14 token) child-card radius, brand `writingDirection="ltr"`, `$xp` widget at the spec sizes, `borderSubtle` divider that flips by `isRtl`. The **nav order is still EN-source order** (`MyChildren` then `Overview`) — workstream A must reorder for AR.
- **OverviewWeb.tsx** already has the KPI tiles (28px value, 0.88 tracking, soft icon chips, `$xpSoft`/`$streakSoft`) and a `FocusAreasCard`. It is **missing the Recommendations panel** and the **2-col side-by-side row** (workstream C).
- **SettingsWeb.tsx** Profile panel already: avatar upload wired (`useUploadAvatar`/`useRemoveAvatar`, hidden web file input, preview via `profile.avatarUrl`), **email already read-only** (`disabled forceLtr`) with a helper line, Save via `useUpdateProfile`. LinkedChildrenPanel + LanguagePanel already exist.
- **LanguagePanel.tsx** currently **persists on change** (no Save button) — workstream D moves it behind an explicit Save.
- **Tokens already present** (brief GAP-01/GAP-02 are RESOLVED): `$nav`=12, `$cardInner`=14, `$xpSoft`, `$streakSoft`, `$primarySoft`, `$primarySoftStrong`, `$borderSubtle`, `$primaryLight`, `$successSoft`, `$warningSoft`, `$dangerSoft`. **No new color/radius tokens are required by this work.** See §Token table.
- **No shared parent shell today**: `_layout.tsx` is a bare `Stack`; each page composes its own `<Stack flexDirection="row"><Sidebar/><ScrollView/></Stack>` and relies on the document `dir="rtl"` to flip (no explicit `row-reverse`).
- **No Modal primitive in `@learnexia/ui`.** The in-repo overlay convention is RN `<Modal transparent>` + `$overlay` scrim (used by `ChangeLearningLanguageModal` + `EditChildSheet`). The Add-Child modal mirrors that shell — **not a new pattern**.

So this spec is mostly: (A) introduce the **shared shell** + child-switcher + global switcher + brand scrollbar + AR nav reorder; (C) **add the Recommendations panel + side-by-side**; (D) **Language & Region Save button** + linked-children corners; (E) the **Add-Child modal**.

---

## ⚠️ Patterns flagged for lead approval (CLAUDE.md rule 8)

Both are pre-approved by the **lead decisions** in the task, but called out explicitly so frontend has cover:

1. **Shared parent web shell** rendered from `_layout.tsx` (sidebar + scroll container + shell header holding the global switcher + child-switcher). This is a new structural/compound shape. **Approved by lead decision** ("SHARED PARENT WEB SHELL"). Build it; do not invent further nesting.
2. **Active-child switcher** = new FE state (`activeChildId`, persisted). **Approved by lead decision** ("BUILD A CHILD-SWITCHER"). Use the same Zustand+localStorage shape as `localeStore` — no new abstraction.

The **Add-Child modal** is NOT a new pattern (reuses the RN-`Modal`+scrim overlay convention already in the repo). No further flags.

---

## A. Shared parent shell

### A.1 Shell anatomy & layout

Single shell rendered from `app/(parent)/_layout.tsx`, wrapping every `(parent)` route (`overview`, `children`, `reports`, `settings`, and the `index` fallback). Reference composition: `AppShell` in `PagesApp.jsx` (L554) + the kit `index.html` frame.

```
┌──────────────────────────────────────────────────────────────┐
│  SHELL HEADER (web, ≥768, height 56, sticky)                  │
│  [child-switcher pill ........]      [lang seg][theme btn]    │  ← see A.3 / A.4
├──────────┬───────────────────────────────────────────────────┤
│          │                                                    │
│ SIDEBAR  │  CONTENT SCROLL CONTAINER (the only scroll region) │
│ 240px    │   page body renders here (PDHeader + sections)     │
│ fixed-   │   brand scrollbar; scrolls to its content end      │
│ height   │                                                    │
│          │                                                    │
│ XP widget│                                                    │
└──────────┴───────────────────────────────────────────────────┘
```

- **Outer frame:** `flexDirection="column"`, `flex={1}`, `backgroundColor="$bg"` (`#0F172A`). Row 1 = shell header; Row 2 = `flexDirection="row"` holding Sidebar + content.
- **Direction:** keep the current approach — the document `dir`/`writingDirection` flips the row once (sidebar → right in AR). **Do NOT add `row-reverse`** (that double-flips). This matches the existing `overview.tsx`/`settings.tsx` comment. The shell header inner row uses `rowDir` (`row-reverse` for RTL) for its OWN children only (switcher vs child-switcher swap sides).
- **Sidebar:** `width={240}`, `height="100%"`, `backgroundColor="$bg"`, divider hairline `$borderSubtle` (`rgba(255,255,255,0.06)`) on the side facing content (`borderRightWidth` in LTR / `borderLeftWidth` in RTL — already implemented). Sidebar does NOT scroll with the content; the XP widget stays pinned (`marginTop="auto"`). On very short viewports the sidebar may scroll internally with the **same brand scrollbar**.
- **Content scroll container:** `flex={1}`, `minWidth={0}`, the page's `ScrollView` lives here with `contentContainerStyle={{ flexGrow: 1, paddingBottom: 48 }}`. **This is the single vertical scroll region** — fixes the "clipped / dead-scroll" AC. Each page body must NOT impose its own competing flex that traps height (audit the `flex` chain: `ScrollView style={{flex:1}}` + `contentContainerStyle flexGrow:1`).
- **Narrow (<768):** no shell header bar and no sidebar; the existing mobile `ScreenHeader` + stacked content is kept. The global switcher + child-switcher move INTO the mobile `ScreenHeader` region (switcher right-aligned in the header; child-switcher as a full-width pill directly under it). Content scrolls in the page `ScrollView`.

### A.2 Sidebar nav — order, icons, RTL (the AC)

Nav items (reference `PDSidebar` in `DashboardComponents.jsx` L6 + `web-sidebar.html`). Logical source order in **EN**:

| # | key | icon (emoji placeholder — flag) | label EN | label AR |
|---|-----|------|----------|----------|
| 1 | `overview` | 📊 | Overview | نظرة عامة |
| 2 | `myChildren` | 👨‍👩‍👦 | My Children | أطفالي |
| 3 | `reports` | 📝 | Reports | التقارير |
| 4 | `activity` | 🎯 | Activity | النشاط |
| 5 | `subjects` | 📚 | Subjects | المواد |
| 6 | `settings` | ⚙️ | Settings | الإعدادات |

**LEAD DECISION (the AC):** Arabic puts **Overview (نظرة عامة) BEFORE My Children (أطفالي)**. The cleanest implementation is to make **Overview first in the source `NAV` array for both locales** (it reads naturally in EN too — Overview is the post-login home per workstream B). The current array has `MyChildren` first; **reorder so `Overview` is index 0, `MyChildren` index 1.** Do not branch the order by locale — a single source order that is Overview-first satisfies both EN and the AR AC, and the `dir`/`text-align:right` mirroring handles the rest.

> **Intended deviation from the kit:** `index-ar.html` (L281–282, L324–325…) renders أطفالي ABOVE نظرة عامة (it kept EN DOM order and just mirrored). The lead decision overrides the kit here. Flagged so the e2e tester asserts نظرة عامة is the FIRST nav row in AR.

**Per-item visual (already correct in code, confirm against `web-sidebar.html` L12):**
- Row: `flexDirection={rowDir}`, `alignItems="center"`, `gap="$3"` (12), `minHeight={40}`, `paddingVertical={10}`, `paddingHorizontal={12}`, `borderRadius="$nav"` (12), `hitSlop` to keep a 48px touch target.
- **Icon on the logical START** (before the label in reading order). Because the row is `rowDir` and the icon is the first child, it sits on the right in AR automatically — **no `scaleX` on the icon, no double-flip**. Emoji are not mirrored (SKILL.md RTL rule 7).
- Active: `backgroundColor="$primarySoft"` (`rgba(79,70,229,0.18)`), label `color="$primaryLight"` (`#A5B4FC`) `fontWeight="700"`; inactive label `color="$fg3"` (`#94A3B8`) `fontWeight="600"`. **No left-border accent** (removed — `web-sidebar.html` has none). Label `fontSize={14}`, `fontFamily="$heading"` (Cairo in AR), `writingDirection={direction}`.
- Hover (inactive): `backgroundColor="$cardSoft"` (brighten, never darken). Press: `scale 0.95`.
- Brand row: logo-mark `36×36` (**owl/mark is a placeholder — flag**) + "Learnexia" `fontSize={18}` `fontWeight="800"` **`writingDirection="ltr"`** (brand stays Latin LTR in AR).

### A.3 Global language + theme switcher — placement & visual

**Placement: in the SHELL HEADER, logical END** (top-right in EN, top-left in AR), mirroring the marketing/login switcher position. Reuse the existing **`LocaleThemeControls`** component (`app/(auth)/_components/LocaleThemeControls.tsx`) verbatim — it already renders the segmented language pill + the ☀️/🌙 theme button and drives `useLocaleStore` + `useThemeStore`. **No new state.** Pass `direction` so it lays out `row-reverse` in AR.

Visual (from `LocaleThemeControls`, unchanged):
- **Language segment:** `backgroundColor="$card"`, `borderRadius="$pill"`, `borderWidth=1` `borderColor="$border"`, `padding=3`, `gap=3`. Each option: `height=32`, `paddingHorizontal="$3"`, `borderRadius="$pill"`; active option `backgroundColor="$primary"` (`#4F46E5`) with `color="$fg1"`; inactive `transparent` with `color="$fg3"`. Label `fontSize=13` `fontWeight="700"` `fontFamily="$heading"`. `accessibilityRole="radiogroup"`.
- **Theme button:** `36×36`, `borderRadius="$pill"`, `borderWidth=1 borderColor="$border"`, `backgroundColor="$card"`, hover `$cardSoft`. Glyph ☀️ (dark active) / 🌙 (light active), `fontSize=16`.

> **Theme persistence (brief Q-A2, must fix):** `themeStore` does NOT persist today. To satisfy AC-A "persists across hard reload", add `localStorage` persistence to `themeStore` **mirroring `localeStore` exactly** (key e.g. `lx_theme`, web-only, swallow errors, hydrate at module eval). This is in-pattern, not a new abstraction.
> **Light-theme caveat (brief Q-A3):** SKILL.md caveat #4 — light theme is not implemented (only `--lx-bg-light` token). Ship the toggle as-is; it may look incomplete in light. **Full light-theme support is OUT OF SCOPE** — flag in deliverable.

Switching locale flips `dir`/RTL app-wide on web with no reload (already wired); switching theme flips instantly. Both reflect on every parent page because the shell renders the control once.

### A.4 Active-child switcher (NEW — lead decision)

A compact **child pill + dropdown** in the SHELL HEADER, logical START (top-left in EN, top-right in AR). It selects the active child that Overview / Reports / Settings read.

**Closed (pill) visual** — reuses the sidebar child-card grammar (`PDSidebar` child card, `web-sidebar.html` L6) but as a compact header pill:
- `flexDirection={rowDir}`, `alignItems="center"`, `gap="$2"`, `height={40}`, `paddingHorizontal={10}`, `borderRadius="$cardInner"` (14), `backgroundColor="$card"`, `borderWidth=1` `borderColor="$border"`, hover `$cardSoft`, press `scale 0.95`.
- Leading: `Avatar name={child.fullName} size="sm"` (32–36, color-initial fallback — gradient NOT mirrored in RTL).
- Middle (column): name `fontSize={13}` `fontWeight="700"` `color="$fg1"` `fontFamily="$heading"` `writingDirection={direction}`; meta `fontSize={11}` `color="$fg3"` = "Grade N · Level L" / "الصف N · المستوى L" (Eastern-Arabic numerals in AR via `formatNumber('ar-EG')`).
- Trailing: chevron `›` (EN) / `‹` (AR) `color="$fg3"` `fontSize={16}` — use the literal RTL glyph, **no `scaleX`**.

**Open (dropdown menu)** — floating popover under the pill (overlay; glass/blur is allowed only on floating overlays per brand law, but a plain `$card` popover is fine here — keep it simple):
- `backgroundColor="$card"`, `borderRadius="$card"` (20 — it's a card-class popover) or `$modal` if it floats high; `borderWidth=1` `borderColor="$border"`, soft float shadow `0 8px 24px rgba(0,0,0,0.25)`. Width = pill width (min 240).
- One row per linked child: avatar + name + meta, `paddingVertical={10} paddingHorizontal={12}`, `borderRadius="$nav"` (12), hover `$cardSoft`. The **active** child row: `backgroundColor="$primarySoft"`, name `color="$primaryLight"`, a trailing ✓ (`$primary`).
- Footer row inside the dropdown: "**+ Add child**" / "**+ أضف طفلاً**" → opens the **Add-Child modal** (workstream E). Same pill-ghost grammar as `PDPanel` action.
- States: default / hover (`$cardSoft`) / selected (`$primarySoft` + ✓) / focus (focus ring `--lx-focus-ring` 2px `$primary` + 4px glow) / loading (single skeleton row) / empty (no children → pill shows "Add a child" CTA that opens the modal; mirrors the OverviewWeb empty-state).

**State & persistence (NEW FE state):**
- A Zustand `activeChildStore` with `{ activeChildId, setActiveChildId }`, **web-persisted to `localStorage`** (key e.g. `lx_active_child`) — same shape as `localeStore`. On native it's in-memory.
- `useMyChildren()` feeds the list. Resolution: `activeChildId` if it's still a valid linked child, else fall back to `children[0]`. When the active child is unlinked, reset to `children[0]`.
- **Overview / Reports / Settings(sidebar child-card)** read the active child from this store instead of always `children[0]`. The sidebar child-selector card mirrors the active child and tapping it opens the same dropdown (or routes to My Children — keep current route-on-tap as a fallback, but the header pill is the canonical switcher).
- Result for workstream F: selecting child 1 vs child 2 yields distinct `getChildStatsStub(id)` cards (XP/streak/mastery/focus), because the stub is keyed on child id.

### A.5 Brand scrollbar (SKILL.md Skill 9 / `index.html` L65–75)

Apply globally to the web app (the content scroll container + any internal scroll region). Exact values from `index.html`:

```css
* { scrollbar-width: thin; scrollbar-color: #4F46E5 transparent; }
*::-webkit-scrollbar { width: 10px; height: 10px; }          /* vertical + horizontal */
*::-webkit-scrollbar-track { background: rgba(255,255,255,0.03); border-radius: 9999px; }
*::-webkit-scrollbar-thumb {
  background: linear-gradient(180deg, #6366F1, #4F46E5);      /* indigo gradient pill */
  border-radius: 9999px;
  border: 2px solid #0F172A;                                  /* = $bg, carves the pill */
}
*::-webkit-scrollbar-thumb:hover { background: linear-gradient(180deg, #818CF8, #6366F1); }  /* lighter on hover */
*::-webkit-scrollbar-corner { background: transparent; }
```

- Inject once at the web app root (a global stylesheet / web `<style>`), not per-component. Hex values are sanctioned literals here (scrollbar pseudo-elements can't take Tamagui tokens), but they equal `$primary`/`$primaryHover`/`$bg`.
- AC: every parent page scrolls to its content end with this scrollbar; no clipped content, no dead region.

---

## B. Routing (no UI surface — behavior only)

Post-login parent-with-children lands on `/(parent)/overview` (was `/(parent)`). Single edit in `useAuthRoute.ts` (+ mirror in `useGroupGuard.ts`). No design surface — listed for completeness; the shell + Overview spec covers what they land on.

---

## C. Overview page

Reference: `OverviewWebPage` in `index.html` (L89–154), `web-recommendations.html`, `web-weak-areas-list.html`. Current `OverviewWeb.tsx` has everything EXCEPT the Recommendations panel and the side-by-side row.

### C.1 Section order (per `index.html`)

1. **PDHeader** — "{Child}'s progress" / "تقدّم {name}" + date range + "This week" select + "Send Report". (Already built; 22px title, 12px date range, `$fg3`.)
2. **KPI row** — 4 `PDStatCard`-equivalent tiles (`gridTemplateColumns: repeat(4,1fr) gap 14`). Already built (28px value, 0.88 tracking, soft icon chips).
3. **Daily activity (1.5fr) + Subject mastery (1fr)** row, `gap 20`. (Already built; chart deferred to P5.)
4. **NEW 2-col row: "Areas to focus on" (1fr) + "Recommendations from Lexi" (1fr)**, `gap 20`, `alignItems: start`.

### C.2 Side-by-side row (the AC — `index.html` L136)

```
gridTemplateColumns: '1fr 1fr', gap: 20, alignItems: 'start'   (stack to 1 col on narrow)
```

Implementation (Tamagui, no new pattern): wrap `FocusAreasCard` and the new `RecommendationsCard` in a `<Stack flexDirection={rowDir} flexWrap="wrap" gap="$5" alignItems="flex-start">`, each child `flex={1} minWidth={320}`. On narrow widths `flexWrap` stacks them to one column. **Bucketed radii:** both are cards → `borderRadius="$card"` (20). Inner recommendation rows → `borderRadius="$button"` (16, per preview card). No raw 90° corners anywhere.

> **Supersede note:** `align-overview.md` "Intentional Deviation #4" said do NOT add Recommendations. `index.html` L136–150 now renders it → **add it.** The brief resolves in favor of index.html.

### C.3 Recommendations-from-Lexi panel (NEW — mirror `PDRecommendation`)

Panel shell = same grammar as `FocusAreasCard` (PDPanel): `borderRadius="$card"` (20), `backgroundColor="$card"` (`#1E293B`), `borderWidth=1` `borderColor="$border"`, `padding={22}`, `gap="$5"`. Header: title `fontSize={16}` (panel-title size, per `PDPanel`/`web-recommendations` — note FocusAreas currently uses 18; both panels in this row should match at **16** to mirror `PDPanel`) `fontWeight="800"` `$fg1`; subtitle `fontSize={12}` `$fg3`. No "See all" action on this panel.

Rows — exact values from `web-recommendations.html` L5–15 + `PDRecommendation` (`DashboardComponents.jsx` L229):
- Row: `flexDirection={rowDir}`, `gap={12}` (kit uses 14 inner; preview uses 12 — use **12** per preview), `padding={14}`, `backgroundColor="$bg"` (`#0F172A`), `borderWidth=1` `borderColor="rgba(255,255,255,0.04)"` (hairline), `borderRadius="$button"` (16).
- Icon chip: `40×40`, `borderRadius="$nav"` (12), `flexShrink:0`, soft tinted bg + matching icon color per accent (see table). Icon `fontSize={18}`.
- Text column (`flex:1`): title `fontSize={13}` `fontWeight="700"` `$fg1` `writingDirection={direction}`; body `fontSize={12}` `$fg3` `marginTop={3}` `lineHeight={1.45}`; CTA button `marginTop={10}` `fontSize={12}` `fontWeight="700"` color = accent, transparent bg, no border, label "{cta} →" (arrow flips to ← in AR).

**Three stub rows (Phase-5 stub copy — ships styled, NOT wired to an endpoint, per brief Q-C1):**

| # | icon | accent | EN title | EN body | EN CTA | AR title | AR body | AR CTA |
|---|------|--------|----------|---------|--------|----------|---------|--------|
| 1 | 🎯 | `$primary` `#4F46E5`, chip `$primarySoft` (≈ `rgba(79,70,229,0.13)`) | Practice subtraction 5 min/day | Short daily reps will lift accuracy from 42% in about a week. | Plan it | تدرّب على الطرح ٥ دقائق يومياً | تكرار يومي قصير يرفع الدقّة من ٤٢٪ خلال أسبوع تقريباً. | خطّط له |
| 2 | 📖 | `$purple` `#A855F7`, chip `$purpleSoft` (≈ `rgba(168,85,247,0.13)`) | Read with a parent on Fridays | {name} reads 28% faster when reading aloud with a grown-up. | Schedule | اقرأ مع أحد الوالدين أيام الجمعة | يقرأ {name} أسرع بنسبة ٢٨٪ عند القراءة بصوت عالٍ مع شخص بالغ. | حدّد موعداً |
| 3 | 🎉 | `$xp` `#FACC15`, chip `$xpSoft` (`rgba(250,204,21,0.13)`) | Celebrate the 7-day streak | A weekend reward keeps motivation high. Suggest a screen-time bonus. | Send praise | احتفِ بسلسلة الـ٧ أيام | مكافأة في نهاية الأسبوع تبقي الحماس عالياً. اقترح مكافأة وقت شاشة. | أرسل تشجيعاً |

- Emoji 🎯 (mission) / 🎉 (reward) / 📖 are semantic per SKILL.md rule 9. Numbers in AR body = Eastern-Arabic; `٪` not `%`.
- All copy needs **i18n keys** (`parent.overview.recommendations.*`). Flag to frontend to add EN+AR resources. These are stub strings — no endpoint.

### C.4 RTL / a11y for Overview
- Sidebar on right (document `dir`), bars stay LTR (`flexDirection="row"` on bar tracks — already done in MasteryBar/FocusAreas), Eastern-Arabic numerals + `٪`, Cairo headings / Tajawal body.
- Recommendation rows: `accessibilityLabel` = `{title} {body}` composed; CTA arrow flips. Touch targets ≥44.

---

## D. Settings page

Reference: `SettingsWebPage` in `PagesApp.jsx` + `align-settings.md` + preview `web-linked-rows.html`. Header/tabs/Profile already aligned (§0). Remaining deltas:

### D.1 Email read-only locked state + reason (lead decision)

Already disabled in code; this spec pins the **visible disabled styling + helper copy**:
- Field: `disabled` (opacity 0.4 per brand law disabled rule; no glow), `forceLtr` (email is a Latin technical string, LTR even in AR — SKILL.md), `textAlign="left"`. Value source: `useMe`/profile email (read defensively — `AccountProfileResponse` has no `email`; brief Q-D3). If no email is available, show a static "—" rather than an empty box.
- **Helper line directly under the field**, `fontSize={12}` `color="$fg3"` `fontFamily="$body"` `writingDirection={direction}` `textAlign` per direction:
  - EN: **"Your email is your sign-in and can't be changed here."**
  - AR: **"بريدك الإلكتروني هو معرّف تسجيل الدخول ولا يمكن تغييره من هنا."**
- A small 🔒 (lock) leading the helper is optional and semantic-OK. **No edit affordance, no "change email" link.** i18n key `parent.settings.profile.emailLockedHelper` (flag to add EN+AR).

### D.2 Parent profile-image upload (avatar + 📷 badge + dashed drop-zone, live preview)

Current code uploads via a hidden web `<input type=file>` + `useUploadAvatar`/`useRemoveAvatar` and previews from `profile.avatarUrl`. The design upgrade is the **affordance** to match the kit (`SettingsProfile` + the AddChild photo block):
- **Avatar circle** `84×84` `borderRadius=9999`, color-initial gradient fallback (NOT mirrored in RTL), with a **📷 badge** bottom on the logical END: `24×24` circle, `backgroundColor="$primary"`, `border: 2px solid $card`, `fontSize=11`, positioned `bottom:-2`, `[isRtl?'left':'right']:-2`.
- **Dashed drop-zone** beside the avatar (mirrors the AddChild upload zone, `AddChildModal.jsx` L69): `flex:1`, `border: 1.5px dashed rgba(99,102,241,0.45)`, `borderRadius="$cardInner"` (14), `padding: 12px 14px`, `backgroundColor: rgba(79,70,229,0.05)`, cursor pointer. Content: ⬆️ glyph + title "Upload a photo"/"Change photo" (`$primaryLight` `fontWeight=700` `fontSize=13`) + sub "PNG or JPG · or pick a color below" (`$fg3` `fontSize=11`). Clicking anywhere in the zone triggers the file picker.
- **Live preview:** on pick, show the chosen file on the avatar (web `URL.createObjectURL`) immediately, before/independent of upload completing (currently it waits for `avatarUrl`). Pending overlay (⏳) while the mutation is in flight (already present). Remove (`useRemoveAvatar`) shown only when a photo is set, `variant="danger" size="sm"`.
- Helper/feedback line below: success `$success`, error `$danger`, idle `$fg3` (already present).
- EN copy: "Upload a photo" / "Change photo" / "Remove". AR: "ارفع صورة" / "تغيير الصورة" / "إزالة".

### D.3 Language & Region section — Save button (lead decision)

Move language persistence behind an explicit **Save** button (currently persists on `onChange`):
- Keep the two-`Select` grid (Display language + Region). Selecting the language updates LOCAL form state only; it does **not** call the mutation or flip the app yet.
- **Action row** at the panel bottom, `flexDirection={rowDir}` `justifyContent="flex-end"` `gap={10}` `paddingTop={6}`: a single primary **Save** button (`variant="primary" size="md"`, primary glow). EN "Save" / AR "حفظ". (Optionally a ghost "Cancel" that resets to loaded — match the Profile panel grammar.)
- On Save: persist language via `useUpdateUserLanguage` AND apply it (`setLocale` web / restart-prompt native, same logic as today, just gated by Save). Show the existing success strip (`$successSoft`).
- **Region has no backend endpoint (brief Q-D2)** → region is **UI-only**; Save persists language only. **Flag** a small helper or note that region is local-only. i18n key `parent.settings.language.regionUiOnlyNote` (optional).
- Subtitle copy: use the i18n resource value (align-settings L-03), e.g. EN "Affects your dashboard, not your children's apps" / AR equivalent.

### D.4 Linked-children section — corners & missing parts (`web-linked-rows.html`)

Per `web-linked-rows.html` / `SettingsLinked` (`PagesApp.jsx` L396):
- Each linked-child **row**: `flexDirection={rowDir}`, `alignItems="center"`, `gap={14}`, `padding: 14px 16px`, `backgroundColor="$bg"` (`#0F172A`), `borderRadius="$cardInner"` (14 — **rounded, not sharp**), `borderWidth=1` `borderColor="rgba(255,255,255,0.04)"`.
- Avatar `44×44` `borderRadius=9999` color-initial.
- Name `fontSize=14` `fontWeight="800"` `$fg1`; a **Grade pill** `padding:2px 7px` `borderRadius="$pill"` `backgroundColor="$primarySoft"` `color="$primaryLight"` `fontWeight=800` `fontSize=10` ("Grade N" / "الصف N"); a **language pill** `backgroundColor="rgba(255,255,255,0.06)"` `color="$fg3"` `fontSize=10` ("EN"/"AR"). Email meta line `fontSize=12` `$fg3` `marginTop=3` (email LTR-pinned).
- Trailing: **Edit** (ghost, `height=36`) + **Remove** (ghost, `height=36`, `color="$danger"` `borderColor="rgba(239,68,68,0.3)"`).
- Panel surface `borderRadius="$modal"` (24), header with **"+ Add child"** action pill → **opens the Add-Child modal** (workstream E — was `router.push('/(onboarding)/add-child')`).
- The existing inline Edit/Unlink/learning-language strips keep their `borderRadius=14` (`$cardInner`) — already consistent.

### D.5 Settings RTL/a11y
- Tab rail on the right in AR (document `dir`), active tab `$primarySoftStrong` + `$primaryLight` label (Tabs component already correct). Email + phone LTR-pinned. Cairo/Tajawal. Field labels `textAlign` per direction. ✓ all per `align-settings.md` (already largely landed).

---

## E. Add-Child MODAL (web)

Reference: `AddChildModal.jsx` + `web-add-child-modal.html` + `ar-web-add-child-modal.html` + SKILL.md Skill 8. **Reuses the in-repo RN-`Modal`+scrim overlay convention** (as `ChangeLearningLanguageModal`/`EditChildSheet`) — not a new pattern.

### E.1 Shell, scrim, motion
- **Scrim:** fixed inset, `backgroundColor: rgba(5,8,22,0.7)` (≈ `$overlay`), `backdrop-filter: blur(4px)` (blur allowed — floating overlay per brand law), centered. `direction` = locale dir.
- **Card:** `width=480` `maxWidth="92vw"`, `backgroundColor: #15161D` (modal surface — a touch deeper than `$card`; matches the kit's modal bg; sanctioned literal), `borderRadius="$modal"` (24), `borderWidth=1` `borderColor="rgba(255,255,255,0.08)"`, shadow `0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.06)`, `overflow:hidden`.
- **Motion:** pop-in `scale 0.92→1` + fade over 280ms `cubic-bezier(0.34,1.56,0.64,1)` (spring overshoot per brand law). Backdrop click closes (Cancel-equivalent); ✕ and hardware-back close. (Optional: trap focus like RewardPopup.)

### E.2 Anatomy (top → bottom)

**Header** (`padding: 22px 24px 16px`, `borderBottom: 1px solid rgba(255,255,255,0.05)`):
- Leading mark `44×44` `borderRadius="$cardInner"` (14) Level-Up gradient `linear-gradient(135deg,#A855F7,#6366F1)` + 👶, glow `0 6px 16px rgba(99,102,241,0.4)`.
- Title "Add a child" / "أضف طفلاً" `fontFamily` display (Cairo in AR) `fontWeight=800` `fontSize=18` `$fg1`; sub "They'll log in with the email you set" / "سيسجّل الدخول بالبريد الذي تحدّده" `fontSize=12` `$fg3`.
- ✕ close button `32×32` `borderRadius="$sm"` (10≈8) `backgroundColor: rgba(255,255,255,0.05)` `$fg3`.

**Body** (`padding: 20px 24px`, `flexDirection:column`, `gap=16`, `maxHeight≈60vh`, scrolls with the **brand scrollbar**):

1. **Photo upload** (`AddChildModal.jsx` L64): avatar `64×64` circle (photo cover OR color-initial fallback), 📷 badge `24×24` `$primary` border-`#15161D`, logical-END corner. Beside it the dashed drop-zone (same as D.2 but `borderRadius=14`): ⬆️ + "Upload a photo"/"ارفع صورة" (`$primaryLight`) + "PNG or JPG · or pick a color below". Live preview via `URL.createObjectURL`.
2. **Child's name** — input `height=46` `backgroundColor: #0B0C12` (modal input bg — deeper field; sanctioned literal matching kit) `border: 1px solid rgba(255,255,255,0.1)` (`$borderInput`) `borderRadius="$cardInner"`-ish 12 → use `12` literal/`$nav` `color="$fg1"` `fontSize=15`. Label `fontWeight=700` `fontSize=12` `$fg2`.
3. **Login email** — same input, **`dir="ltr"`** (Latin), pre-validated highlight: `borderColor="$primary"` + `box-shadow: 0 0 0 3px rgba(99,102,241,0.2)`.
4. **Grade — 6 plant-emoji TILES** (`grid repeat(6,1fr) gap 6`): each tile `height=46` `borderRadius="$nav"` (12), unselected `backgroundColor: #0B0C12` + `border 1px rgba(255,255,255,0.1)`; **selected = Level-Up gradient** `linear-gradient(135deg,#A855F7,#6366F1)` + glow `0 4px 12px rgba(99,102,241,0.4)`, no border. Tile content: plant emoji `[🌱🌿🌳🌲🍃🌴][g-1]` `fontSize=15` + number `fontWeight=800` `fontSize=11` (EN `1–6` / AR `١–٦`), selected `#fff` else `$fg3`. **NEVER a `<select>`.** (Reuse the existing `GradePicker` from `@learnexia/ui` which already renders these tiles.)
5. **Language — 2 FLAG tiles** (`flex row gap 8`): each `flex:1` `height=48` `borderRadius="$nav"` (12). Selected = `backgroundColor: rgba(79,70,229,0.18)` (`$primarySoft`) + `border 1.5px solid $primary` + `$fg1` label; unselected = `#0B0C12` + `border 1px rgba(255,255,255,0.1)` + `$fg3`. 🇪🇬 **AR** / 🇺🇸 **EN**, flag `fontSize=18` + label `fontWeight=700` `fontSize=14`.
6. **(Optional, kept from kit) avatar-color swatches** — 5 circles `36×36` (`#FB923C #A855F7 #22C55E #38BDF8 #FB7185`), selected ring `0 0 0 3px #15161D, 0 0 0 5px {color}`. Picking a color clears the photo. Label "…or pick an avatar color".

**Footer** (`padding: 16px 24px 22px`, `borderTop: 1px solid rgba(255,255,255,0.05)`, `flex row gap 10`):
- **Cancel** (ghost): `flex:1` `height=48` `borderRadius="$cardInner"`-ish 14 (kit uses 14; brand rule says buttons=16 — **use `$button` 16** to honor the rule and flag the kit's 14 as stale) `transparent` `border 1px rgba(255,255,255,0.12)` `$fg2` `fontWeight=700` `fontSize=15`.
- **Primary CTA** (personalized): `flex:2` `height=48` `borderRadius="$button"` (16) `backgroundColor="$primary"` `#fff` `fontWeight=800` `fontSize=15`, glow `0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)`. Label **"Add {name} →"** / **"أضف {name} ←"** (arrow flips; falls back to "Add child"/"أضف الطفل" when name empty). One primary action per modal (brand law).

### E.3 Extra backend-required fields (brief Q-E1 — IMPORTANT)

The design HTML omits **password, learningLanguage (axis B), country** but `AddChildCommand` requires them. **Extend the modal minimally, tokens-only, matching the kit field styling** (the same `#0B0C12` input + `12px` label grammar). Placement, inserted into the body flow so the designed blocks stay intact:

- **Password** — add a standard input (`secureTextEntry`) **directly under Login email** (it pairs naturally with the login identity). Label "Login password" / "كلمة مرور الدخول". Optional inline password-strength meter (reuse `PasswordStrengthMeter`) under it — consistent with register.
- **Learning language (axis B)** — the child's medium of instruction, distinct from the UI language flag tiles. Add **after the flag tiles** as a labelled `Select` (reuse `LanguageSelect`), label "Learning language" / "لغة التعلّم" + the existing helper "Math & Science are taught in this language" / the AR equivalent (reuse `onboarding.addChild.learningLanguageHelper`). Keep the **auto-fill rule** from `AddChildForm`: picking a flag-tile (axis A) does not force axis B, but selecting learning language can pre-fill the flag tile until the parent touches it — keep the existing `appLanguageTouched` behavior to avoid surprises. (If simpler: keep the two axes fully independent; just ensure both are collected.)
- **Country** — add a `Select` (reuse the `COUNTRIES` options) at the **end of the body**, label "Country" / "الدولة", placeholder. Country code stays LTR.

Validation reuses `addChildSchema` (fires on submit, not per keystroke — Skill 8). Submit via `useAddChild` → `POST /api/Parent/Add-Child` with `{ fullName, email, password, grade, language, learningLanguage, country, avatar? }`. On success: close modal, the new child appears in My-Children (and is selectable in the child-switcher). Error → inline `ServerErrorBanner` inside the modal body.

### E.4 Entry points that become this modal (brief inventory)
- **Settings → Linked children "Add child" CTA** (`LinkedChildrenPanel` `handleAddChild`) → open modal (was `router.push('/(onboarding)/add-child')`).
- **Overview empty-state "Add child"** (`OverviewWeb` L190–196) → open modal.
- **Child-switcher dropdown footer "+ Add child"** → open modal.
- **Onboarding `(onboarding)/add-child`** (first-run, multi-child, no sidebar) → **keep its own full-screen flow** (brief Q-E2 recommendation). Modal is dashboard-context only.
- **`(parent)/link-child.tsx`** (link EXISTING child) → **untouched, out of scope.**

### E.5 Modal RTL/a11y
- Card `dir="rtl"`; Cairo titles/labels, Tajawal inputs; flag tiles unchanged; grade numbers Eastern-Arabic; CTA arrow ←; email/country LTR-pinned; 📷 badge + dashed-zone corner flip to logical end. Focus ring on every field; ✕ + Cancel + backdrop all dismiss; the primary CTA is the single primary action. Touch targets ≥48 (tiles are 46–48). The selected grade/flag tiles announce `selected` via `accessibilityState`.

---

## Token / decision table (color · gradient · radius)

**All tokens this work needs already exist** — brief GAP-01/GAP-02 are resolved in `packages/design-system/src/tokens/*`. Confirmed present, no additions required:

| Need | Token | Value | Status |
|------|-------|-------|--------|
| Nav-chip / inner-tile radius (sidebar nav, grade/flag tiles, recommendation chip) | `$nav` | 12 | ✅ exists |
| Child-card / focus-row / drop-zone / linked-row radius, header child-pill | `$cardInner` | 14 | ✅ exists |
| All buttons + modal CTAs + recommendation row card | `$button` | 16 | ✅ exists |
| Cards (KPI, panels, popover) | `$card` (radius) | 20 | ✅ exists |
| Modal + settings panels | `$modal` | 24 | ✅ exists |
| Pills (lang segment, grade/lang pills, ghost actions, bars) | `$pill` | 9999 | ✅ exists |
| KPI XP / streak icon-chip tints | `$xpSoft` / `$streakSoft` | `rgba(250,204,21,0.13)` / `rgba(251,146,60,0.13)` | ✅ exists |
| Active nav/tab/child-row tint + label | `$primarySoft` / `$primaryLight` | `rgba(79,70,229,0.18)` / `#A5B4FC` | ✅ exists |
| Recommendation purple chip | `$purpleSoft` / `$purple` | `rgba(168,85,247,0.18)` / `#A855F7` | ✅ exists |
| Hairline dividers (sidebar/header/rows) | `$borderSubtle` | `rgba(255,255,255,0.06)` | ✅ exists |
| Input border | `$borderInput` | `rgba(255,255,255,0.10)` | ✅ exists |
| Level-Up gradient (selected grade tile, modal header mark, add-child hero) | `gradients.levelUp` constant | `#A855F7 → #6366F1` | ✅ exists |

**Sanctioned literals (not tokens, intentional):** modal surface `#15161D`, modal input bg `#0B0C12`, modal scrim `rgba(5,8,22,0.7)`, scrollbar hex (= `$primary`/`$primaryHover`/`$bg`), hairlines `rgba(255,255,255,0.04/0.05/0.06)`, dashed-zone `rgba(99,102,241,0.45)`/`rgba(79,70,229,0.05)`, glow shadows `rgba(99,102,241,0.4)`. These come straight from the kit/preview cards and are already used in shipped components.

**New FE state (not tokens):** `activeChildStore` (Zustand + localStorage, mirrors `localeStore`); `themeStore` gains localStorage persistence.

---

## RTL deltas per surface

| Surface | RTL behavior |
|---------|-------------|
| Shell | document `dir` flips the sidebar↔content row ONCE — sidebar on right; **do NOT add `row-reverse`** (double-flip). Shell-header inner row uses `rowDir` so switcher (logical END) and child-switcher (logical START) swap sides. |
| Sidebar nav | Overview نظرة عامة FIRST (lead AC, deviates from `index-ar.html`); icon on logical start (no `scaleX`); labels right-aligned, Cairo; "Learnexia" stays LTR; chevron literal `‹`. |
| Child-switcher | pill/dropdown mirror; avatar gradient NOT mirrored; meta numerals Eastern-Arabic; chevron `‹`. |
| Overview | bars stay LTR (`flexDirection="row"` on tracks); `٪` not `%`; recommendation CTA arrow ←; numerals Eastern-Arabic. |
| Settings | tab rail on right; email + phone + country LTR-pinned; helper text right-aligned; Cairo/Tajawal. |
| Add-Child modal | card `dir="rtl"`; CTA arrow ←; email/country LTR; grade numerals ١–٦; 📷 badge + dashed-zone corner flip to logical end; flag tiles unchanged. |
| Scrollbar | symmetric — applies in both directions; horizontal scroll thumb identical. |

---

## Responsive (390 / 768 / 1024)

- **≥1024 / ≥768 (web shell):** shell header (56) + 240px sidebar + content. Overview KPI row 4-up; activity+mastery 1.5fr/1fr; focus+recommendations **2-col 1fr/1fr**. Settings 220px tab rail + panel.
- **<768 (narrow / 390):** no shell header bar, no sidebar — mobile `ScreenHeader` holds the switcher (logical end) + a full-width child-switcher pill under it; content stacks in the page `ScrollView`. Overview rows collapse to 1 column (KPI tiles wrap 2×2 then 1-up via `flexWrap` + `minWidth`); focus+recommendations **stack to 1 column**. Settings tab rail wraps above the panel.
- **Add-Child modal:** centered, `width=480` `maxWidth=92vw`; body `maxHeight≈60vh` scrolls; grade tiles stay `repeat(6,1fr)` (shrink, don't wrap); flag tiles 2-up. On phone it's a near-full-width sheet (or the mobile bottom-sheet variant — out of scope here).
- **2-col → 1-col breakpoint:** the focus/recommendations row uses `flexWrap` with each child `minWidth={320}` → it naturally drops to one column below ~680px content width.

---

## Accessibility / kid-UX (parent surface, but keep the floor)

- Touch targets ≥44–48 (nav rows hitSlop to 48; modal tiles 46–48; switcher 40 pill with adequate hit area).
- Focus ring `--lx-focus-ring` (2px `$primary` + 4px glow) visible on keyboard nav for every switcher option, child-row, modal field, tile, and CTA.
- High contrast: `$fg1` on `$card`/`$bg`; never darken on hover (brighten to `$cardSoft`).
- One primary action per screen (Overview "Send Report"; Settings panel "Save"; modal personalized CTA).
- Live regions for Save success / avatar success-error / add-child errors (already used).
- Numbers weight 800 + tabular-nums (KPI, XP widget, grade tiles).
- Disabled email: opacity 0.4, no glow, helper explains why — no dead-end confusion.

---

## Design gaps / open questions (flag — do NOT silently fix app code)

1. **Mascot/owl + logo-mark are placeholders** (SKILL.md caveat #1) — flag wherever the brand mark renders (sidebar, modal header uses 👶 emoji, not the mark — OK).
2. **Emoji-as-icons** throughout (nav, KPI, tiles) are the sanctioned substitution for Lucide line icons (SKILL.md caveat #3). Acceptable; note for a future icon pass.
3. **Light theme not implemented** (Q-A3) — the dark/light toggle ships but light is incomplete. Out of scope; flag.
4. **Region persistence has no endpoint** (Q-D2) — Save persists language only; region is UI-only. Flag in the panel.
5. **Email value source** (Q-D3) — `AccountProfileResponse` lacks `email`; confirm `useMe` exposes it, else show "—". Contract gap, surface — don't paper over.
6. **AR nav order deviates from `index-ar.html`** (kit shows أطفالي first) — this spec follows the **lead AC** (Overview first). Flag so e2e asserts the lead order, not the kit's.
7. **Child-switcher is genuinely new FE state** (Q-F1) — required to make workstream F's per-child switching verifiable. Lead-approved; mirror `localeStore`.
8. **`themeStore` persistence** (Q-A2) — add localStorage mirroring `localeStore`; web-only.

---

## Implementation handoff (per piece → target)

| Piece | Target |
|-------|--------|
| Shared shell (header + sidebar + scroll container) | `apps/student-app/app/(parent)/_layout.tsx` (new shell) + the 4 page wrappers drop their own row/scroll into it |
| Global switcher placement (reuse `LocaleThemeControls`) | shell header in `_layout.tsx`; component already in `app/(auth)/_components/LocaleThemeControls.tsx` |
| Theme persistence | `apps/student-app/src/providers/themeStore.ts` (add localStorage, mirror `localeStore.ts`) |
| Active-child store | new `apps/student-app/src/providers/activeChildStore.ts` (Zustand + localStorage) |
| Child-switcher UI (pill + dropdown) | new `apps/student-app/app/(parent)/_components/ChildSwitcher.tsx`; wired by Overview/Reports/Settings + sidebar card |
| Brand scrollbar CSS | web global stylesheet at the app root |
| Sidebar nav reorder (Overview first) + icon alignment | `apps/student-app/app/(parent)/_components/Sidebar.tsx` (`NAV` array) |
| Recommendations panel | new `apps/student-app/app/(parent)/_components/RecommendationsCard.tsx` (mirror `FocusAreasCard` grammar) |
| Overview side-by-side row | `apps/student-app/app/(parent)/_components/OverviewWeb.tsx` (`OverviewBody`) |
| Language & Region Save button | `apps/student-app/app/(parent)/_components/settings/LanguagePanel.tsx` |
| Email locked helper copy | `SettingsWeb.tsx` ProfilePanel + i18n `resources.ts` (EN+AR) |
| Avatar upload affordance (📷 badge + dashed zone + instant preview) | `SettingsWeb.tsx` ProfilePanel |
| Linked-row corners | `apps/student-app/app/(parent)/_components/settings/LinkedChildrenPanel.tsx` |
| Add-Child modal | new `apps/student-app/app/(parent)/_components/AddChildModal.tsx` (RN `Modal`+scrim, reuse `GradePicker`/`LanguageSelect`/`Select`/`TextField`/`PasswordStrengthMeter`); repoint CTAs in LinkedChildrenPanel + OverviewWeb + ChildSwitcher |
| Recommendations + email-locked + region-note copy | `packages/shared/src/i18n/resources.ts` (EN + AR) |
| Tokens | none new — all exist in `packages/design-system/src/tokens/*` |

---

Design spec ready for frontend.
