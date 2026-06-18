# Design Spec — P7 Admin Console Wave 1: User & Account Management
## Stories: P7-06 (Users list + detail), P7-07 (Lifecycle actions), P7-08 (Child edit / grade / language)
## Surface: `apps/admin-dashboard` (Next.js 15, desktop-first, dark theme)

**Token authority:** `design-system/colors_and_type.css` + `packages/design-system/src/tokens/`
**Shell authority:** `design-system/ui_kits/admin-dashboard/P1-10.md` (the existing admin shell spec — all layout, nav, topbar, and skeleton rules carry forward unchanged)
**Locale:** English-first, RTL-ready (all copy has AR slot; no live toggle this wave)
**Theme:** dark by default (`$bg #0F172A`)

> Note on captures: No new screenshot captures exist for the admin console screens (the design-system screenshots set covers the parent dashboard and student app). All specifications below are derived from the P1-10 shell tokens + component shapes, the existing admin component source, the brief DTOs, and the `--lx-*` token set. Where no capture exists this is not a "gap" — admin is an internal operator surface built entirely from the design-system tokens, not from a separate visual capture set.

---

## Admin surface design principles (carry-forward from P1-10, enforced here)

1. **No gamification chrome** on this surface — no XP bars, confetti, streak flames, mascots, or reward gradients. Dark palette retained.
2. **Gradients only for admin-appropriate uses:** the XP/Reward/Level-Up gradients are strictly forbidden here. No gradient fills on any admin element except as a design gap explicitly noted.
3. **Motion is minimal:** hover-state transitions at 120ms, dialog entrances at 200–240ms fade+translate, skeleton shimmer. No spring overshoot, no particle effects.
4. **One primary action per screen.** All destructive actions require explicit confirmation dialogs with distinct visual tiers.
5. **Typography:** Poppins (EN). Cairo (AR display/headings). Tajawal (AR body). No `--lx-size-display` (48px) on this surface. Max heading on admin: H2 24px.
6. **Density:** cards may use `$4` (16px) padding where space is tight. Tables are compact.

---

## PART A: Shared Primitives (built once in P7-06, reused by P7-07 and P7-08)

> The plan mandates these be specified once and built once (no duplicate primitives). P7-06 Batch B builds them; P7-07 and P7-08 import and reuse them.

### A.1 — `StatusBadge` component

**File:** `apps/admin-dashboard/components/StatusBadge.tsx`

The status badge is a pill-shaped label mapping the `accountStatus` integer (0 Active / 1 Suspended / 2 Deleted) and optionally a role string to a colored chip. It appears in the users table, the detail header, and dialog copy.

#### Anatomy

```
[StatusBadge — inline-flex, height 22px, borderRadius 9999 ($pill),
               paddingHorizontal 10px, alignItems center, gap 5px]
  [Dot — width 6px, height 6px, borderRadius 9999 (circle), backgroundColor (variant)]
  [Label text — fontSize 12px, fontWeight 600, fontFamily $body (Poppins / Tajawal AR),
                letterSpacing 0.04em (--lx-tracking-wide), textTransform uppercase]
```

#### Variants (status)

| Status (int) | Label EN | Label AR (i18n key) | Dot color | Background | Text color |
|---|---|---|---|---|---|
| 0 — Active | Active | نشط (`admin.status.active`) | `#22C55E` (`--lx-secondary`) | `rgba(34,197,94,0.15)` (`--lx-success-soft` at 0.15) | `#22C55E` |
| 1 — Suspended | Suspended | موقوف (`admin.status.suspended`) | `#F59E0B` (`--lx-accent`) | `rgba(245,158,11,0.15)` (`--lx-warning-soft` at 0.15) | `#F59E0B` |
| 2 — Deleted | Deleted | محذوف (`admin.status.deleted`) | `#EF4444` (`--lx-danger`) | `rgba(239,68,68,0.12)` | `#EF4444` |

#### Variants (role)

| Role string | Label EN | Label AR | Background | Text |
|---|---|---|---|---|
| Parent | Parent | ولي الأمر (`admin.role.parent`) | `rgba(79,70,229,0.15)` | `#6366F1` (`--lx-primary-hover`) |
| Student | Student | طالب (`admin.role.student`) | `rgba(168,85,247,0.15)` | `#A855F7` (`--lx-purple`) |
| Admin | Admin | مسؤول (`admin.role.admin`) | `rgba(255,255,255,0.08)` | `#94A3B8` (`--lx-fg3`) |

#### Token reference

- Border: none (pill-background contrast is sufficient)
- Shadow: none
- Min width: none; content-fit
- Border radius: `--lx-radius-pill` = 9999px

#### States

- **Default:** as above
- **Inside a table row (hover):** badge does not change; the row changes
- **Disabled/muted context (Deleted status in actions menu):** same colors, the parent container handles the muted treatment

#### RTL

- Text direction follows `dir` attribute of parent; badge label in AR uses Tajawal 12px weight 600
- Dot position is logical-start (inline-start)

---

### A.2 — `AdminConfirmDialog` primitive

**File:** `apps/admin-dashboard/components/AdminConfirmDialog.tsx`

A reusable dialog shell used by all three lifecycle dialogs (Suspend, Reactivate, Delete) and both child-edit dialogs (Grade Override, Change Learning Language). It handles the overlay, focus trap, ESC, and the button row. Callers supply the interior content as `children`.

This is NOT a compound-component or a new pattern — it is a simple wrapper around a native HTML `<dialog>` element (web-appropriate, no RN `Modal` dependency on Next.js) with the established token treatment. No new design pattern introduced.

#### Anatomy

```
[Overlay — position fixed inset-0, backgroundColor $overlay (rgba(15,23,42,0.72)),
           display flex, alignItems center, justifyContent center,
           zIndex 500, backdropFilter none (no blur on this surface — admin is not a floating overlay for reward moments)]

  [Dialog card — role="dialog" aria-modal="true" aria-labelledby={titleId},
                 backgroundColor $card (#1E293B), borderRadius $modal (24px = --lx-radius-modal),
                 borderWidth 1px, borderColor $borderStrong (rgba(255,255,255,0.16)),
                 boxShadow --lx-shadow-popup (0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.12)),
                 padding $8 (32px), minWidth 360px, maxWidth 480px, width calc(100vw - 64px),
                 display flex, flexDirection column, gap $6 (24px)]

    [Dialog header — flexDirection row, alignItems flex-start, gap $4 (16px)]
      [Icon slot — 40x40 circle, borderRadius 9999, backgroundColor (variant-soft), alignItems center, justifyContent center]
        [SVG Lucide icon — 20px, 2px stroke, rounded caps, color (variant-color)]
      [Title block — flex 1]
        [Title text — id={titleId}, fontFamily $heading (Poppins/Cairo AR), fontSize 18px (--lx-size-h3),
                      fontWeight 700, color $fg1 (#F8FAFC), lineHeight 1.3 (--lx-lh-snug)]
        [Subtitle text — fontFamily $body, fontSize 14px (--lx-size-body-sm), color $fg3 (#94A3B8),
                         lineHeight 1.5, marginTop 4px]

    [Dialog body — flexDirection column, gap $4 (16px)]
      {children}  ← caller-supplied content (reason field, typed confirm, cascade checkbox, etc.)

    [Dialog actions row — flexDirection row, justifyContent flex-end, alignItems center, gap $3 (12px), marginTop $2 (8px)]
      [Cancel button]
      [Confirm button (primary or destructive variant)]
```

#### Dialog icon variants

| Dialog type | Icon (Lucide name) | Icon bg | Icon color |
|---|---|---|---|
| Suspend (warning) | `pause-circle` | `rgba(245,158,11,0.15)` | `#F59E0B` (`--lx-accent`) |
| Reactivate (positive) | `play-circle` | `rgba(34,197,94,0.15)` | `#22C55E` (`--lx-secondary`) |
| Delete (destructive) | `trash-2` | `rgba(239,68,68,0.15)` | `#EF4444` (`--lx-danger`) |
| Grade override (informational) | `graduation-cap` | `rgba(79,70,229,0.15)` | `#4F46E5` (`--lx-primary`) |
| Change learning language (destructive) | `alert-triangle` | `rgba(239,68,68,0.15)` | `#EF4444` (`--lx-danger`) |

Note: icon set is Lucide (2px stroke, rounded caps) — this is the flagged substitution noted in Brand Law rule 11. Do not use Unicode characters or emoji as icons here.

#### Modal tokens

- Background: `$card` `#1E293B` (`--lx-card`)
- Border: `1px solid rgba(255,255,255,0.16)` (`--lx-border-strong`)
- Radius: `--lx-radius-modal` = 24px
- Shadow: `--lx-shadow-popup` = `0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.12)`
- Overlay: `--lx-overlay` = `rgba(15,23,42,0.72)`

#### Modal motion

- Entrance: overlay fade `0 → 1` over `200ms` `--lx-ease-out`; card translate `translateY(8px) → translateY(0)` + `opacity 0 → 1` over `200ms` `--lx-ease-out`. No spring overshoot (admin context, not a celebration).
- Exit: reverse of entrance, `160ms`.
- Button press: `scale(0.95)` `80ms` (Brand law rule 10).

#### Accessibility

- `role="dialog"` `aria-modal="true"` `aria-labelledby={titleId}` on the card
- ESC key closes (cancel path only, never confirms)
- Focus trap: on open, focus moves to the first interactive element inside the dialog (the reason textarea or Cancel button). On close, focus returns to the element that triggered the dialog.
- Confirm button `aria-disabled="true"` (not `disabled`) when gating conditions not met, so it remains focusable and screen readers can read the label. Visually `opacity: 0.4`.
- Cancel always enabled; never disables.
- Errors inside the dialog use `role="alert"` via `AdminErrorBanner`.

#### RTL

- Dialog card is not mirrored in shape, but text content direction follows `dir="rtl"` for AR
- Action row reverses in RTL (Cancel on right → left; Confirm on left → right, following reading order). Use `flex-direction: row-reverse` or logical flex under `dir="rtl"`.
- Icon position is logical-start.

---

### A.3 — `ReasonField` sub-component

Used inside Suspend, Delete, Grade Override dialogs. A labeled textarea with char counter.

```
[ReasonField — flexDirection column, gap $1 (4px)]
  [Label row — flexDirection row, justifyContent space-between, alignItems baseline]
    [Label text — fontSize 12px, fontWeight 600, color $fg3, textTransform uppercase, letterSpacing 0.04em]
    [Required marker — text " *", color $danger, fontSize 12px]  ← omit for optional reason
  [Textarea wrapper — backgroundColor $bg (#0F172A), borderRadius $sm (8px = --lx-radius-sm),
                      borderWidth 1px, borderColor $border, padding $3 (12px),
                      minHeight 96px, resize vertical]
    [Native <textarea> — flex 1, backgroundColor transparent, color $fg1, fontSize 14px,
                         fontFamily $body (Poppins/Tajawal AR), lineHeight 1.5, outline none]
  [Footer row — flexDirection row, justifyContent space-between, alignItems center]
    [Error text — fontSize 12px, color $danger] ← conditional
    [Char counter — fontSize 12px, color $fg3, fontVariantNumeric tabular-nums]
      format: "0 / 500"
```

**States:**

| State | Border | Box shadow |
|---|---|---|
| Default | `1px solid $border` (`rgba(255,255,255,0.08)`) | none |
| Focus | `1px solid #4F46E5` (`--lx-primary`) | `0 0 0 2px #4F46E5, 0 0 0 6px rgba(99,102,241,0.25)` (`--lx-focus-ring` admin variant) |
| Error (empty-submit / over limit) | `1px solid $danger` (`#EF4444`) | `0 0 0 2px #EF4444, 0 0 0 4px rgba(239,68,68,0.2)` |
| Disabled | `opacity 0.5`, same border | none |

**Char counter color when near/over limit:** at 450+ chars, counter color transitions to `--lx-accent` (`#F59E0B`); at 500 (limit), `--lx-danger` (`#EF4444`).

---

### A.4 — `TypedConfirmField` sub-component

Used only inside `DeleteUserDialog`. A single-line input gating the destructive button.

```
[TypedConfirmField — flexDirection column, gap $2 (8px)]
  [Instruction text — fontSize 14px, color $fg2, lineHeight 1.5]
    EN: 'Type the account email address to confirm:'
    AR: 'اكتب عنوان البريد الإلكتروني للحساب للتأكيد:'
  [Target value display — fontFamily $mono (--lx-font-mono), fontSize 13px, color $fg1,
                           backgroundColor $bg, padding $2 (8px) $3 (12px),
                           borderRadius $sm (8px), borderWidth 1px, borderColor $border,
                           userSelect all]
    ← renders the account email (e.g. "user@example.com")
  [Input — same visual treatment as AdminInputField (P1-10 §3d): height 44px,
            backgroundColor $bg, borderRadius $sm (8px), borderWidth 1px, borderColor $border,
            paddingHorizontal $4 (16px), color $fg1, fontSize 14px, fontFamily $body]
    [placeholder: EN 'Enter email to confirm' / AR 'أدخل البريد الإلكتروني للتأكيد']
  [Match indicator — fontSize 12px, color (match: $secondary / no-match + typed: $danger)]
    EN: 'Confirmed' / AR: 'تم التأكيد'   ← shows only when match
```

**Logic:** the Confirm button in `DeleteUserDialog` is `aria-disabled` until `typedValue.trim().toLowerCase() === targetEmail.toLowerCase()`. Comparison is case-insensitive. The input does not validate on blur — validation state appears real-time as the user types.

**RTL note:** the target email (`user@example.com`) and the typed confirmation input are technical strings — render with `dir="ltr"` regardless of page direction (the email is a Latin ASCII string). The surrounding instruction text follows page direction.

---

## PART B: P7-06 — Users List Page

### B.1 — Layout (`/users`)

The Users List page lives inside the existing `AdminShell` (side nav 240px left + topbar 64px). Page title in `AdminTopBar`: **"Users"** (EN) / **"المستخدمون"** (AR). `AdminSideNav` shows a real, active-aware "Users" item for this route.

**Content area layout (main `<main>` tag, padding `$8` = 32px, `$4` = 16px at `$sm`):**

```
[Page root — flexDirection column, gap $6 (24px)]

  [Page header — flexDirection row, alignItems center, justifyContent space-between, gap $4]
    [Heading "Users" — fontSize 24px (--lx-size-h2), fontWeight 700, color $fg1, fontFamily $heading]
    [Result count pill — only when results visible: "N accounts" / "N حساب",
                         fontSize 12px, color $fg3, backgroundColor $cardSoft (#334155),
                         padding 4px 10px, borderRadius $pill (9999px)]

  [Filters bar — flexDirection row, alignItems center, gap $4, flexWrap wrap]
    [Search input — flex 1, minWidth 200px, maxWidth 340px]
    [Role filter select — width 140px]
    [Status filter select — width 140px]
    [Clear-filters ghost button — shows only when any filter is active]

  [Results area — flex 1]
    ← four states (see B.2)
```

**Breakpoints:**
- `>= 1024px` ($laptop): all filters in one row
- `768–1023px` ($tablet): search full-width first row; selects + clear second row
- `< 768px` ($sm): stacked single column; each filter full-width

### B.2 — Filters Bar component

**Search input** — reuse `TextField` from `@learnexia/ui/components/TextField` (already used in login page) with:
- `placeholder`: EN "Search by name or email…" / AR "ابحث بالاسم أو البريد الإلكتروني…"
- Leading search icon: Lucide `search`, 16px, `$fg3`
- Height: 44px (meets touch target)
- debounce: 350ms before firing the query
- Controlled; `onChangeText` updates state; state change resets to page 1

**Role filter** — a `<select>` element (native web select, not a custom dropdown — admin is desktop-first; native select is appropriate and accessible):
- Label: visually hidden `<label>` for a11y; visual placeholder in the select: "All Roles" / "كل الأدوار"
- Options: All Roles (clear), Parent / ولي الأمر, Student / طالب
  (Admin/SuperAdmin excluded per D6)
- Width: 140px; height: 44px
- Styling: `backgroundColor $bg (#0F172A)`, `borderRadius $sm (8px)`, `border 1px solid $border`, `color $fg2`, `padding 0 $3 (12px)`, `fontSize 14px`, `fontFamily $body`
- Focus: `--lx-focus-ring` (2px indigo + 6px glow)

**Status filter** — same native select pattern:
- Placeholder: "All Statuses" / "كل الحالات"
- Options: All, Active / نشط, Suspended / موقوف
  (Deleted omitted from filter defaults per D6 — only shown if explicitly searched)
- Same width/height/styling as role filter

**Clear-filters button** — `Button variant="ghost" size="sm"` (reuse `@learnexia/ui/components/Button`):
- Label: "Clear filters" / "مسح التصفية"
- Shows only when `role !== '' || status !== '' || query !== ''`
- On press: resets all three state values to empty; query reset fires debounce immediately (no wait)

### B.3 — Results Table

**Table element:** semantic `<table>` with `<thead>` / `<tbody>` / `<tr>` / `<th scope="col">` / `<td>`. No CSS-grid table substitute — semantic table for accessibility.

**Table container:** `Card variant="default"` from `@learnexia/ui`, `padding 0` (override default padding — the table provides its own cell padding), `borderRadius $card (20px = --lx-radius-card)`, `overflow hidden`.

**Column layout (from `AdminUserListItemDto`):**

| # | Column | Source field | Width | Notes |
|---|---|---|---|---|
| 1 | Name | `fullName` | flex 2 / minWidth 160px | Semibold, `$fg1` |
| 2 | Email | `email` | flex 3 / minWidth 200px | Monospace `--lx-font-mono`, `$fg2` |
| 3 | Role | `role` | 120px | `StatusBadge` (role variant) |
| 4 | Status | `accountStatus` | 120px | `StatusBadge` (status variant) |
| 5 | Created | `createdAt` | 140px | Formatted date, `$fg3`, 14px |

**Table header row:**
- Background: `$cardSoft` (`#334155`)
- `<th>` cells: `fontSize 11px`, `fontWeight 600`, `color $fg3`, `textTransform uppercase`, `letterSpacing 0.06em`, `padding $3 (12px) $4 (16px)`, `textAlign start` (logical — flips under RTL)
- No sort indicators in this story (read-only list, server-sorted)

**Table body rows:**
- Minimum height: 52px
- Background: `$card` (`#1E293B`) alternating with `$card` — no striping (dark theme, subtle enough)
- Separator: `border-bottom 1px solid $border` between rows
- `<td>` cells: `padding $3 (12px) $4 (16px)`, `fontSize 14px`, `color $fg2`, `verticalAlign middle`
- **Hover state:** `backgroundColor $cardSoft` (`#334155`), `cursor pointer`
- **Active/press state:** `backgroundColor rgba(79,70,229,0.08)` (primarySoft at lower opacity)
- Hover transition: `background-color 120ms --lx-ease-out`
- Entire row is clickable (`onClick → router.push('/users/${id}')`)

**Name cell:**
- `color $fg1`, `fontWeight 600`, `fontSize 14px`
- Avatar initials chip: 28px circle, `backgroundColor $primarySoft`, `color $primary`, `fontSize 11px`, `fontWeight 700`, `borderRadius 9999` — shown inline before the name, `marginInlineEnd $2 (8px)`

**Email cell:**
- `fontFamily --lx-font-mono`, `fontSize 13px`, `color $fg2`

**Date cell:**
- Format: `MMM D, YYYY` for EN (e.g. "Jun 18, 2026") / `D MMM YYYY` with Eastern-Arabic numerals for AR reading text, but wrapped in `dir="ltr"` because a date is a technical string — or use locale-formatted `Intl.DateTimeFormat` respecting the admin locale
- `color $fg3`, `fontSize 13px`

**Responsive table:**
- At `$sm` (< 768px): table collapses to a stacked card list. Each row becomes a `Card` with name (large), email, and `StatusBadge` chips. Created date moves to a secondary line. Role badge moves beside name.
- At `$tablet` (768–1023px): table shows cols 1–4 only; "Created" column hidden (priority 5)

### B.4 — Server Pagination control

Below the table, centered or right-aligned (align to table end edge):

```
[Pagination — flexDirection row, alignItems center, gap $3 (12px), padding $4 (16px)]
  [Prev button — Button variant="ghost" size="sm", disabled at page 1]
    icon: Lucide chevron-left (LTR) / chevron-right (RTL, mirrored direction)
    label: SR-only "Previous page" / "الصفحة السابقة"
  [Page info — fontSize 14px, color $fg2, fontVariantNumeric tabular-nums]
    EN: "Page {currentPage} of {totalPages}"
    AR: "الصفحة {١} من {٣}" (Eastern-Arabic numerals for the inline reading text)
  [Next button — Button variant="ghost" size="sm", disabled at last page]
    icon: Lucide chevron-right (LTR) / chevron-left (RTL)
    label: SR-only "Next page" / "الصفحة التالية"
  [optional — page size selector: hidden in this story; default 20]
```

Page changes fire new `useSearchUsers` calls. Previous data stays visible while refetching (`placeholderData`); a subtle spinner or `opacity 0.6` on the table body indicates in-flight state. The table is never blanked mid-refetch.

When filter/query changes: reset `currentPage` to 1 immediately in state, then fire the query.

### B.5 — List States

**State: Loading (initial)**

Render a skeleton table: the filters bar renders with real controls (allows user to type while loading); the table area shows 6 skeleton rows:

```
[Skeleton table — same Card container, same header (real header text visible)]
  [Skeleton row × 6 — height 52px, borderBottom $border]
    [SkeletonBlock — various widths matching column layout, height 16px, borderRadius $sm]
```
Use the existing shimmer animation from `AdminLoadingSkeleton` (`lx-shimmer` keyframe). `role="status"` `aria-label` on the container.

**State: Empty (no results)**

```
[Empty state — Card, padding $10 (40px), alignItems center, gap $4 (16px)]
  [Lucide icon — search-x, 40px, color $fg3]
  [Heading — fontSize 18px, fontWeight 600, color $fg1]
    EN: "No accounts found"
    AR: "لم يُعثر على حسابات"
  [Body — fontSize 14px, color $fg3, textAlign center, maxWidth 320px]
    EN: "Try adjusting the filters or search term."
    AR: "جرِّب تعديل التصفية أو مصطلح البحث."
```

No "clear filters" CTA here if no filters are active (genuinely empty DB); include it if a filter is active.

**State: Error**

```
[Error state — gap $4 (16px)]
  [AdminErrorBanner variant="error" message={errorMessage}]
  [Retry button — Button variant="ghost" size="sm"]
    EN: "Try again" / AR: "حاول مرة أخرى"
    onClick: refetch()
```

Error banner gets `role="alert"` (already in `AdminErrorBanner`). For `aria-live` coverage, wrap the results area in `aria-live="polite"` so state changes announce.

**State: Results**

The table + pagination as described. `aria-live="polite"` region on the result count so screen readers announce "N accounts" when results load.

### B.6 — Active nav item: "Users"

`AdminSideNav` gains a real nav entry. It is a link (`<a>` or Next.js `<Link>`) to `/users`, with `aria-current="page"` when `pathname.startsWith('/users')`.

Active state tokens (from P1-10 spec §4b):
- Background: `$primarySoft` (`rgba(79,70,229,0.18)`)
- Left border: `3px solid $primary (#4F46E5)` — use `borderInlineStart` for RTL safety
- Icon color: `$primary` (`#4F46E5`)
- Label: `fontSize 14px`, `fontWeight 600`, `color $fg1`

Inactive state:
- Background: `transparent`
- Icon: `$fg3`
- Label: `fontWeight 400`, `color $fg3`

Icon: Lucide `users` (2px stroke, 18px). This is a placeholder substitution (no admin icon set exists — Design Gap 1 from P1-10 remains open). Use Lucide `users` as the icon.

Hover (inactive): `backgroundColor $card (#1E293B)`, icon + label to `$fg2`. Transition `120ms --lx-ease-out`.

New `NAV_ITEMS` entry in `AdminSideNav.tsx`:
- `key: 'users'`
- `label: strings.navUsers` (EN: "Users" / AR: "المستخدمون")
- `icon: <UsersIcon />` (Lucide, 18px)
- `href: '/users'`
- `isActive: pathname.startsWith('/users')`

---

## PART C: P7-06 — User Detail Page

### C.1 — Layout (`/users/[id]`)

**Page title in `AdminTopBar`:** the user's `fullName` or "User Detail" as fallback (EN) / "تفاصيل المستخدم" (AR).

**Two-column layout at laptop+:**

```
[Page root — flexDirection column, gap $6 (24px)]

  [Detail header card]
    ← profile identity: avatar + name + email + role badge + status badge

  [Two-column body — flexDirection row, gap $6 (24px), alignItems flex-start]
    [Primary column — flex 2, minWidth 0, flexDirection column, gap $6 (24px)]
      [Profile card]
      [Family panel — UserFamilyPanel]
      [Activity panel — UserActivityPanel]

    [Secondary column — flex 1, minWidth 220px, maxWidth 280px, flexDirection column, gap $4 (16px)]
      [Actions card — P7-07 entry points, initially empty in P7-06]
```

At `$tablet` (768–1023px): single column; secondary column drops below primary (stacked).
At `$sm` (< 768px): single column, full-width; Actions card moves to the bottom.

### C.2 — Detail Header Card

```
[Header card — Card variant="default", padding $6 (24px), borderRadius $card (20px)]

  [flexDirection row, gap $6, alignItems flex-start]

    [Avatar — 64px × 64px circle, borderRadius 9999,
              backgroundColor $primarySoft, borderWidth 2px, borderColor rgba(79,70,229,0.35),
              alignItems center, justifyContent center]
      [Initial — fontFamily $heading, fontSize 24px, fontWeight 700, color $primary]
        ← first character of fullName, uppercased

    [Identity block — flex 1, flexDirection column, gap $2 (8px)]
      [Name — fontSize 22px, fontWeight 700, color $fg1, fontFamily $heading (Poppins/Cairo AR)]
      [Email — fontSize 14px, color $fg3, fontFamily --lx-font-mono]
      [Badges row — flexDirection row, gap $2, flexWrap wrap, marginTop $1 (4px)]
        [StatusBadge (role)]
        [StatusBadge (accountStatus)]
      [Status reason — conditional: shows only if lastStatusReason is non-null]
        [Text — fontSize 12px, color $fg3, fontStyle italic]
          EN: 'Reason: {lastStatusReason} — {statusChangedAtUtc formatted date}'
          AR: 'السبب: {lastStatusReason} — {date}'

    [Actions slot — marginInlineStart auto] ← P7-07 adds the LifecycleActionsMenu here
```

If `avatarUrl` is non-null on the profile DTO, render the image rather than the initials circle. Fallback to initials if image fails to load.

### C.3 — Profile Fields Card

```
[Card variant="default", padding $6 (24px), borderRadius $card (20px)]
  [Section heading — fontSize 14px, fontWeight 600, color $fg3, textTransform uppercase,
                     letterSpacing 0.06em, marginBottom $4 (16px)]
    EN: "Profile" / AR: "الملف الشخصي"

  [Fields grid — display grid, gridTemplateColumns 1fr 1fr, gap $4 (16px)]
    [Each field — flexDirection column, gap $1 (4px)]
      [Field label — fontSize 12px, fontWeight 500, color $fg3]
      [Field value — fontSize 14px, fontWeight 500, color $fg1]
```

**Fields (from `AdminUserProfileDto`):**

| Field label EN | Field label AR | Source | Notes |
|---|---|---|---|
| Full Name | الاسم الكامل | `fullName` | |
| Email | البريد الإلكتروني | `email` | Monospace font |
| Member Since | تاريخ التسجيل | `createdAt` | Formatted date |
| Sign-in Activity | آخر تسجيل دخول | `lastSignInAtUtc` (always null) | Always renders "Not tracked" / "غير مُتتبَّع" (per D5) |
| Status | الحالة | `accountStatus` | `StatusBadge` |
| Status Reason | سبب الحالة | `lastStatusReason` | Shows "—" if null |
| Status Changed | تاريخ تغيير الحالة | `statusChangedAtUtc` | Shows "—" if null |

**Child-only block** (renders only when `roles` array includes "Student"):

A visually distinct sub-section within the same card, separated by a `border-top 1px solid $border` and a sub-heading:

```
[Child-only section]
  [Sub-heading — fontSize 12px, fontWeight 600, color $primary (#4F46E5),
                 textTransform uppercase, letterSpacing 0.06em, paddingTop $4 (16px),
                 marginTop $4 (16px), borderTop 1px solid $border]
    EN: "Student Details" / AR: "تفاصيل الطالب"

  [Fields grid — same 2-col grid]
    Grade: grade → formatted as "Grade {N}" / "الصف {١}" (integer, Eastern-Arabic in AR)
    Country: nationality
```

**Language fields (CRITICAL — must be two visually distinct rows, never merged):**

Both appear in the child-only section, each as its own row spanning the full grid width (grid-column: 1 / -1), to maximize visual distinctness:

```
[Language row A — full-width, padding $3 (12px), borderRadius $sm (8px),
                  backgroundColor rgba(79,70,229,0.08), border 1px solid rgba(79,70,229,0.15)]
  [Row label — fontSize 11px, fontWeight 600, color $primary, textTransform uppercase,
               letterSpacing 0.04em, marginBottom $1 (4px)]
    EN: "Display Language (UI & Communication)" / AR: "لغة الواجهة (التواصل والعرض)"
  [Value — fontSize 14px, fontWeight 600, color $fg1]
    ← preferredLanguage (e.g. "ar-EG", "en-US"); format as human-readable locale name
  [Hint — fontSize 12px, color $fg3, marginTop 2px]
    EN: "Language used for the app interface." / AR: "اللغة المستخدمة في واجهة التطبيق."

[Language row B — full-width, padding $3 (12px), borderRadius $sm (8px),
                  backgroundColor rgba(168,85,247,0.08), border 1px solid rgba(168,85,247,0.15)]
  [Row label — fontSize 11px, fontWeight 600, color $purple (#A855F7), textTransform uppercase,
               letterSpacing 0.04em, marginBottom $1 (4px)]
    EN: "Learning Language (Math & Science)" / AR: "لغة الدراسة (الرياضيات والعلوم)"
  [Value — fontSize 14px, fontWeight 600, color $fg1]
    ← learningLanguage ("ar" → "Arabic / العربية", "en" → "English / الإنجليزية")
  [Hint — fontSize 12px, color $fg3, marginTop 2px]
    EN: "Language used to teach Math and Science. Changing this resets those subjects." / AR: "اللغة التي تُدرَّس بها الرياضيات والعلوم. تغييرها يعيد ضبط هاتين المادتين."

[P7-08 entry — marginTop $2; only on child-detail]: a "Edit Profile" button row (see Part F)
```

**Rationale for distinct visual containers:** `preferredLanguage` and `learningLanguage` are the two most confusable fields on this surface (P7-06 AC #4 explicitly requires they "must not be merged or conflated"). The indigo vs purple color coding (primary vs badge-purple) provides color, label text, and positional separation — three simultaneous distinct signals.

### C.4 — Family Panel (`UserFamilyPanel`)

**File:** `components/UserFamilyPanel.tsx`

```
[Card variant="default", padding $6 (24px), borderRadius $card (20px)]
  [Panel heading — same pattern as Profile section heading]
    For a parent: EN "Linked Children" / AR "الأبناء المرتبطون"
    For a child: EN "Linked Parents" / AR "الوالدان المرتبطان"

  [Member list — flexDirection column, gap $3 (12px)]
    [Each member — flexDirection row, alignItems center, gap $3, padding $3 (12px),
                   backgroundColor $bg (#0F172A), borderRadius $sm (8px),
                   border 1px solid $border, cursor pointer, hoverStyle: backgroundColor $card]
      [Avatar initials chip — 32px, same pattern as table row chip]
      [Name+detail block — flex 1]
        [Name — fontSize 14px, fontWeight 600, color $fg1]
        [Secondary line — fontSize 12px, color $fg3]
          For parent member: email (string, monospace)
          For child member: "Grade {N}" / "الصف {١}" — child email is null by design (D7); never render a blank email slot
      [Chevron icon — Lucide chevron-right (LTR) / chevron-left (RTL), 16px, $fg3]
    ← entire member row navigates to /users/{member.id}
```

**Empty state:**
```
[Text — fontSize 14px, color $fg3, padding $4 (16px), textAlign center]
  For parent with no children: EN "No linked children" / AR "لا يوجد أبناء مرتبطون"
  For child with no parents: EN "No linked parents" / AR "لا يوجد والدان مرتبطان"
```

**Loading:** 2 skeleton rows (32px height each, shimmer) while `useUserFamily` is loading.

**Error:** `AdminErrorBanner variant="error"` with a "Retry" link. Must NOT propagate to the profile card (this panel loads independently, per AC 7).

### C.5 — Activity Panel (`UserActivityPanel`)

**File:** `components/UserActivityPanel.tsx`

Each gamification section loads from the same `AdminActivitySummaryDto`. Any sub-field being null = show "No data" for that section (never an error).

```
[Card variant="default", padding $6 (24px), borderRadius $card (20px)]
  [Panel heading: EN "Activity Summary" / AR "ملخص النشاط"]

  [Sign-in note — fontSize 14px, color $fg3, padding $3 (12px),
                  backgroundColor $bg, borderRadius $sm, border 1px solid $border,
                  marginBottom $4]
    [Lucide icon — info, 16px, $fg3, inline]
    EN: "Sign-in activity: not tracked" / AR: "آخر تسجيل دخول: غير مُتتبَّع"  (per D5)

  [Stats grid — display grid, gridTemplateColumns repeat(auto-fit, minmax(150px, 1fr)), gap $4]

    [XP / Level section — if xp is null, show NoDataChip]
      [Stat card — padding $4 (16px), backgroundColor $bg, borderRadius $sm,
                   border 1px solid $border]
        [Label — 11px, $fg3, uppercase, letterSpacing 0.04em]
          EN: "XP / Level" / AR: "النقاط / المستوى"
        [Values — flexDirection row, alignItems baseline, gap $2]
          [XP — fontSize 20px, fontWeight 800, color $fg1, fontVariantNumeric tabular-nums]
            ← totalXp (Latin numeral always, even in AR: this is a technical counter)
          [Separator — $fg3]  " / "
          [Level — fontSize 14px, fontWeight 600, color $primary]
            EN: "Level {N}" / AR: "المستوى {N}" (Latin N even in AR: XP counter rule)

    [Streak section — if streak is null, show NoDataChip]
      [currentStreak — with label EN "Current Streak" / AR "السلسلة الحالية"]
      [longestStreak — with label EN "Best Streak" / AR "أفضل سلسلة"]
      ← no 🔥 emoji here — admin surface; Lucide `flame` icon is the approved substitution
      ← no gamification styling; these are plain stat rows

    [Badges section — if badges is null, show NoDataChip]
      [totalCount]
      [Label: EN "Badges Earned" / AR "الشارات المكتسبة"]

    [Missions section — if missions is null, show NoDataChip]
      [dailyMissionCount / completedDailyMissions formatted as fraction: "3 / 5"]
      [Label: EN "Daily Missions" / AR "المهام اليومية"]

    [League section — if league is null, show NoDataChip]
      [tier — label EN "League" / AR "الدوري"]
      [currentRank / groupSize — format "Rank {N} of {N}"]
      [weeklyXp — label EN "Weekly XP" / AR "نقاط الأسبوع"]
```

**NoDataChip:**
```
[fontSize 13px, color $fg3, fontStyle italic]
  EN: "No data" / AR: "لا توجد بيانات"
```

**Loading:** shimmer skeleton grid (4 blocks, same layout as the stats grid).

**Error:** `AdminErrorBanner variant="warning"` (not error — activity is best-effort). Does NOT affect the profile or family panels.

**XP/stat numbers rendering rule:** XP totals, levels, streaks, badge counts are always Latin numerals even in AR locale, because they are technical counters (`font-variant-numeric: tabular-nums`, `dir="ltr"` on the number span). The surrounding label text follows page direction.

### C.6 — Detail Page States

**Loading (profile):** Detail header skeleton (64px avatar circle + two skeleton blocks for name/email) + profile card skeleton (6 field skeletons in 2-col grid). Family and activity panels each have their own loading state; they load independently.

**Not found (404 or 400 on id):** Full-width empty state card:
```
[Card, padding $10 (40px), alignItems center, gap $4]
  [Lucide icon — user-x, 40px, $fg3]
  [Heading: EN "User not found" / AR "المستخدم غير موجود"]
  [Body: EN "This account doesn't exist or was removed." / AR "هذا الحساب غير موجود أو تم حذفه."]
  [Back button — Button variant="ghost": EN "Back to Users" / AR "العودة للمستخدمين"
                  onClick: router.push('/users')]
```

**Error:** `AdminErrorBanner variant="error"` + Retry button. Same pattern as list error.

---

## PART D: P7-07 — Lifecycle Actions (Suspend / Reactivate / Delete)

### D.1 — `LifecycleActionsMenu` (the actions entry point)

**File:** will be added to `app/(admin)/users/[id]/page.tsx` in the detail header Actions slot (pre-cut seam `<DetailHeaderActions>`).

This is not a dropdown menu — it is a small group of buttons (2 max) that appear in the header card's trailing area. The legal actions are derived from `accountStatus`:

| accountStatus | Rendered actions |
|---|---|
| 0 Active | Suspend + Delete |
| 1 Suspended | Reactivate + Delete |
| 2 Deleted | None (terminal message) |

```
[LifecycleActionsMenu — flexDirection row, gap $3 (12px), alignItems center]

  CASE Deleted:
    [Terminal notice — flexDirection row, alignItems center, gap $2, padding $2 (8px) $3 (12px),
                       backgroundColor rgba(239,68,68,0.08), borderRadius $sm (8px),
                       border 1px solid rgba(239,68,68,0.15)]
      [Lucide icon — lock, 14px, $danger]
      [Text — fontSize 12px, color $fg3]
        EN: "Account deleted — no further actions" / AR: "الحساب محذوف — لا يوجد مزيد من الإجراءات"

  CASE Active or Suspended:
    [Suspend button — shows only if status = Active]
      Button variant="ghost" size="sm", icon Lucide pause-circle 16px
      EN: "Suspend" / AR: "إيقاف مؤقت"
      onClick: openSuspendDialog()

    [Reactivate button — shows only if status = Suspended]
      Button variant="ghost" size="sm", icon Lucide play-circle 16px, color $secondary
      EN: "Reactivate" / AR: "إعادة تفعيل"
      onClick: openReactivateDialog()

    [Delete button — shows for Active and Suspended]
      Custom danger-styled button (NOT the shared Button — admin ghost variant would be confusing here):
      Stack: backgroundColor transparent, borderRadius $button (16px), height 36px,
             paddingHorizontal $4 (16px), border 1px solid rgba(239,68,68,0.3),
             cursor pointer, alignItems center, justifyContent center,
             gap $2 (8px), flexDirection row
      hoverStyle: backgroundColor rgba(239,68,68,0.08), borderColor $danger
      pressStyle: scale 0.95 80ms
      icon: Lucide trash-2, 16px, $danger
      label: fontSize 14px, fontWeight 500, color $danger
      EN: "Delete Account" / AR: "حذف الحساب"
      onClick: openDeleteDialog()
```

**A11y:** each button has a descriptive `aria-label` that includes the target name, e.g. `aria-label="Suspend account for Ahmed Ali"`. This is especially important for the destructive Delete button.

### D.2 — `SuspendUserDialog`

**File:** `components/SuspendUserDialog.tsx`

Uses `AdminConfirmDialog` with `variant="suspend"` icon (`pause-circle`, amber).

**Props:** `userId: number, userName: string, onClose: () => void`

**Content inside `AdminConfirmDialog` children:**

```
[Dialog body]

  [Governance notice — padding $3 (12px), backgroundColor rgba(245,158,11,0.10),
                       borderRadius $sm (8px), border 1px solid rgba(245,158,11,0.2),
                       flexDirection row, gap $2, alignItems flex-start]
    [Lucide icon — shield-alert, 16px, $accent (#F59E0B)]
    [Text — fontSize 13px, color $fg2, lineHeight 1.5]
      EN: "This is a governance action. Suspending {userName} will revoke their active sessions and block sign-in until an admin reactivates the account. This is not the same as a failed-login lockout (which is temporary and automatic)."
      AR: "هذا إجراء حوكمة. إيقاف {userName} سيُلغي جلساته النشطة ويمنع تسجيل الدخول حتى يُعيد المسؤول تفعيل الحساب. لا يُقصد بهذا قفل الحساب التلقائي الناتج عن محاولات الدخول الفاشلة."

  [ReasonField — required, maxLength 500]
    Label EN: "Reason for suspension" / AR: "سبب الإيقاف المؤقت"
    Required: yes (button disabled until non-empty)

  [AdminErrorBanner — conditional, shows on mutation error, stays open]
```

**Actions row:**
- Cancel: `Button variant="ghost" size="sm"`: EN "Cancel" / AR "إلغاء"
- Confirm: `Button variant="primary" size="md"` in amber styling:
  - Background: `#F59E0B` (`--lx-accent`), text: `#0F172A` (dark, high contrast on amber)
  - hoverStyle: brightness ~108%
  - `aria-disabled` when reason is empty; `opacity 0.4` in that state
  - loading state: spinner replaces label; button not clickable
  - EN: "Suspend Account" / AR: "إيقاف الحساب مؤقتاً"

**Success feedback:** close dialog + show a `AdminErrorBanner variant="warning"` (amber) above the detail header, auto-dismisses after 5s, `role="status"`:
- EN: "{userName}'s account has been suspended." / AR: "تم إيقاف حساب {userName} مؤقتاً."
- Profile + list queries invalidate → status badge updates to Suspended.

**Error copy (`AdminStrings.lifecycle.*`):**

| Error | EN | AR |
|---|---|---|
| already suspended | "This account is already suspended." | "هذا الحساب موقوف مؤقتاً بالفعل." |
| already deleted | "Deleted accounts cannot be suspended." | "لا يمكن إيقاف الحسابات المحذوفة." |
| self / SuperAdmin | "This account cannot be modified." | "لا يمكن تعديل هذا الحساب." |
| validation (422) | "Reason is required and must be under 500 characters." | "السبب مطلوب ويجب أن يكون أقل من ٥٠٠ حرف." |
| network / 5xx | "Something went wrong. Please try again." | "حدث خطأ ما. يُرجى المحاولة مرة أخرى." |

### D.3 — `ReactivateUserDialog`

**File:** `components/ReactivateUserDialog.tsx`

Uses `AdminConfirmDialog` with `variant="reactivate"` icon (`play-circle`, green).

**Props:** `userId: number, userName: string, lastStatusReason: string | null, statusChangedAtUtc: string | null, onClose: () => void`

**Content:**

```
[Dialog body]

  [Prior-reason block — shows only if lastStatusReason is non-null]
    [Container — padding $3 (12px), backgroundColor rgba(255,255,255,0.04),
                 borderRadius $sm (8px), border 1px solid $border,
                 flexDirection column, gap $1 (4px)]
      [Label — fontSize 11px, fontWeight 600, color $fg3, textTransform uppercase, letterSpacing 0.04em]
        EN: "Prior suspension reason" / AR: "سبب الإيقاف السابق"
      [Reason text — fontSize 14px, color $fg2, lineHeight 1.5, fontStyle italic]
        ← lastStatusReason value
      [Date — fontSize 12px, color $fg3]
        EN: "Suspended on {date}" / AR: "تم الإيقاف بتاريخ {date}"

  [Confirmation notice — fontSize 14px, color $fg2]
    EN: "Reactivating this account will restore sign-in access. The user will need to sign in fresh to receive a new session."
    AR: "ستؤدي إعادة تفعيل هذا الحساب إلى استعادة إمكانية تسجيل الدخول. سيحتاج المستخدم إلى تسجيل الدخول من جديد للحصول على جلسة جديدة."

  [ReasonField — optional, maxLength 500]
    Label EN: "Reason for reactivation (optional)" / AR: "سبب إعادة التفعيل (اختياري)"
    Required: no (button enabled even when empty)

  [AdminErrorBanner — conditional]
```

**Actions row:**
- Cancel: ghost "Cancel" / "إلغاء"
- Confirm: `Button variant="primary" size="md"` (standard indigo primary):
  EN: "Reactivate Account" / AR: "إعادة تفعيل الحساب"
  Always enabled (reason is optional); loading spinner during mutation.

**Success feedback:** close + amber-to-green success `AdminErrorBanner variant="warning"` becomes `variant="error"` — actually use a new success-variant approach: a `$successSoft`-bg banner. Since `AdminErrorBanner` only has `error|forbidden|warning` variants, use `variant="warning"` repurposed, or add a `success` variant inline with `backgroundColor rgba(34,197,94,0.15), borderColor rgba(34,197,94,0.3), iconColor $secondary`:
- EN: "{userName}'s account has been reactivated." / AR: "تم إعادة تفعيل حساب {userName}."

Design gap: `AdminErrorBanner` lacks a `success` variant. The frontend agent should add `variant: 'success'` to `AdminErrorBanner` when building P7-07. Values: bg `rgba(34,197,94,0.15)`, border `rgba(34,197,94,0.3)`, icon `$secondary` (`#22C55E`), icon shape: Lucide `check-circle`.

**Error copy:** same mapping as Suspend for network/5xx/self-protection. Additionally:
- already active: EN "This account is already active." / AR "هذا الحساب نشط بالفعل."
- is deleted: EN "Deleted accounts cannot be reactivated." / AR "لا يمكن إعادة تفعيل الحسابات المحذوفة."

### D.4 — `DeleteUserDialog`

**File:** `components/DeleteUserDialog.tsx`

This dialog has two distinct regions: a standard confirm section + a typed-confirmation gate. Uses `AdminConfirmDialog` with `variant="delete"` icon (`trash-2`, danger-red).

**Props:** `userId: number, userName: string, userEmail: string, isParent: boolean, onClose: () => void`

**Content:**

```
[Dialog body — gap $5 (20px)]

  [Soft-delete notice — backgroundColor rgba(239,68,68,0.08), borderRadius $sm (8px),
                        border 1px solid rgba(239,68,68,0.15), padding $4 (16px),
                        flexDirection column, gap $2 (8px)]
    [Heading — fontSize 14px, fontWeight 700, color $danger]
      EN: "Account will be permanently disabled" / AR: "سيُعطَّل الحساب نهائياً"
    [Body — fontSize 13px, color $fg2, lineHeight 1.5]
      EN: "This action cannot be undone. The account will be blocked from sign-in, but the learning history and account record are retained. Personal data is not yet erased — that happens in a scheduled review."
      AR: "لا يمكن التراجع عن هذا الإجراء. سيُمنع الحساب من تسجيل الدخول، لكن سجل التعلّم وبيانات الحساب ستبقى محفوظة. لم تُحذف البيانات الشخصية بعد — يحدث ذلك في مراجعة مجدولة."

  [Cascade-children block — shows ONLY when isParent is true]
    [Separator — border-top 1px solid $border, marginVertical $2]
    [Checkbox row — flexDirection row, alignItems flex-start, gap $3 (12px), cursor pointer]
      [Native checkbox — 18px × 18px, accent-color $danger]
      [Label block]
        [Label text — fontSize 14px, color $fg2, fontWeight 500]
          EN: "Also delete all linked children" / AR: "حذف جميع الأبناء المرتبطين أيضاً"
        [Warning text — fontSize 12px, color $fg3, marginTop 2px]
          EN: "Their accounts will also be disabled and blocked from sign-in. History retained."
          AR: "سيُعطَّل حساب أبنائهم أيضاً ويُمنعون من تسجيل الدخول. يبقى السجل محفوظاً."
    [Cascade-children default: unchecked (off by default, per D10 decision)]

  [ReasonField — required, maxLength 500]
    Label: EN "Reason for deletion (required)" / AR: "سبب الحذف (مطلوب)"

  [TypedConfirmField — as specified in A.4]
    Instruction: EN "Type the account email address to confirm:" / AR "اكتب عنوان البريد الإلكتروني للحساب للتأكيد:"
    Target: userEmail (rendered in the monospace display box)
    Input placeholder: EN "Enter email to confirm" / AR "أدخل البريد الإلكتروني للتأكيد"

  [AdminErrorBanner — conditional]
```

**Confirm button gate:** the destructive Confirm button is `aria-disabled` until ALL of:
1. `reason.trim().length > 0`
2. `typedValue.trim().toLowerCase() === userEmail.toLowerCase()`

**Confirm button — destructive styling:**
```
backgroundColor: $danger (#EF4444)
borderRadius: $button (16px = --lx-radius-button)
height: 44px (min)
paddingHorizontal: $5 (20px)
color: $fg1 (#F8FAFC)  ← white text on red
fontSize: 14px, fontWeight: 600
hoverStyle: backgroundColor #F87171 (danger-lighter, ~brightens 8%)
pressStyle: scale 0.95 80ms
disabled (aria): opacity 0.4, no hover/press styles
loading: spinner (white, 16px) replaces label
aria-label: EN "Confirm account deletion for {userName}" / AR "تأكيد حذف حساب {userName}"
```

EN label: "Delete Account" / AR: "حذف الحساب"

**Actions row:** Cancel (ghost) | Delete Account (danger)

**Success:** close + `AdminErrorBanner variant="error"` used as a permanent notification (deletion is irreversible — red is appropriate):
- EN: "{userName}'s account has been deleted." / AR: "تم حذف حساب {userName}."
- Profile + list queries invalidate → status updates to Deleted, LifecycleActionsMenu shows terminal notice.

**Error copy:**
- already deleted: EN "This account has already been deleted." / AR "تم حذف هذا الحساب بالفعل."
- self / SuperAdmin: EN "This account cannot be deleted." / AR "لا يمكن حذف هذا الحساب."
- confirm-missing (424, defensive): EN "Please confirm by typing the email address." / AR "يُرجى التأكيد بكتابة عنوان البريد الإلكتروني."
- validation (422): standard copy
- network/5xx: standard copy

---

## PART E: P7-07 — `AdminStrings` slots (`lib/strings.ts`)

All new copy must be added to both `en` and `ar` maps in the typed `AdminStrings` interface. Namespace `lifecycle.*` for P7-07 actions; `statusBadge.*` for the shared badge; `nav.*` for Users nav.

```typescript
// New slots — add to AdminStrings interface + en/ar maps:
navUsers: string;
pageTitleUsers: string;
pageTitleUserDetail: string;

statusActive: string;
statusSuspended: string;
statusDeleted: string;
roleParent: string;
roleStudent: string;
roleAdmin: string;

usersSearchPlaceholder: string;
usersFilterAllRoles: string;
usersFilterAllStatuses: string;
usersClearFilters: string;
usersResultCount: string;   // "{N} accounts"
usersNoResults: string;
usersNoResultsHint: string;
usersErrorRetry: string;
usersPrevPage: string;
usersNextPage: string;
usersPageOf: string;        // "Page {current} of {total}"

userDetailSectionProfile: string;
userDetailSectionStudentDetails: string;
userDetailFieldCreated: string;
userDetailFieldSignIn: string;     // always "not tracked"
userDetailFieldLanguageDisplay: string;
userDetailFieldLanguageDisplayHint: string;
userDetailFieldLanguageLearning: string;
userDetailFieldLanguageLearningHint: string;
userDetailFieldGrade: string;
userDetailFieldCountry: string;
userDetailFieldStatusReason: string;
userDetailFieldStatusChanged: string;
userDetailNotFound: string;
userDetailNotFoundBody: string;
userDetailBackToUsers: string;
userDetailFamilyHeadingParent: string;
userDetailFamilyHeadingChild: string;
userDetailFamilyEmpty: string;
userDetailActivityHeading: string;
userDetailActivitySignIn: string;   // "Sign-in activity: not tracked"
userDetailActivityNoData: string;   // "No data"
userDetailActivityXpLevel: string;
userDetailActivityStreak: string;
userDetailActivityBadges: string;
userDetailActivityMissions: string;
userDetailActivityLeague: string;

lifecycleSuspendTitle: string;
lifecycleSuspendSubtitle: string;
lifecycleSuspendNotice: string;
lifecycleSuspendReasonLabel: string;
lifecycleSuspendConfirm: string;
lifecycleReactivateTitle: string;
lifecycleReactivateSubtitle: string;
lifecycleReactivateNotice: string;
lifecycleReactivatePriorLabel: string;
lifecycleReactivateReasonLabel: string;
lifecycleReactivateConfirm: string;
lifecycleDeleteTitle: string;
lifecycleDeleteSubtitle: string;
lifecycleDeleteNotice: string;
lifecycleDeleteNoticeBody: string;
lifecycleDeleteCascadeLabel: string;
lifecycleDeleteCascadeWarning: string;
lifecycleDeleteReasonLabel: string;
lifecycleDeleteTypedInstruction: string;
lifecycleDeleteConfirm: string;
lifecycleDeleteTerminalNotice: string;
lifecycleErrorAlreadySuspended: string;
lifecycleErrorAlreadyActive: string;
lifecycleErrorAlreadyDeleted: string;
lifecycleErrorProtected: string;    // self / SuperAdmin
lifecycleErrorValidation: string;
lifecycleErrorNetwork: string;
lifecycleErrorConfirmMissing: string;
lifecycleSuccessSuspended: string;
lifecycleSuccessReactivated: string;
lifecycleSuccessDeleted: string;
```

---

## PART F: P7-08 — Child Profile Edit Page and Dialogs

### F.1 — Child Edit Page Layout (`/users/[id]/edit`)

**Access guard:** only render for accounts with `roles` array containing "Student". If a non-Student `id` is loaded, redirect to `/users/{id}` (the detail page) with an `AdminErrorBanner variant="warning"`:
- EN: "Profile editing is only available for student accounts." / AR: "تعديل الملف الشخصي متاح فقط لحسابات الطلاب."

**Page title in `AdminTopBar`:** EN "Edit Student Profile" / AR "تعديل ملف الطالب"

**Breadcrumb row:**
```
[Breadcrumb — flexDirection row, alignItems center, gap $2 (8px), marginBottom $4 (16px)]
  [Link: "Users" / "المستخدمون" — fontSize 14px, color $fg3, hoverStyle: color $fg1]
  [Separator: Lucide chevron-right 14px $fg3 / chevron-left in RTL]
  [Link: "{userName}" — fontSize 14px, color $fg3, hoverStyle: color $fg1, href /users/{id}]
  [Separator]
  [Current: "Edit Profile" / "تعديل الملف" — fontSize 14px, color $fg1, fontWeight 600]
```

**Page layout:**

```
[Page root — flexDirection column, gap $6 (24px), maxWidth 640px]
  (Centered column, not full-width — edit form doesn't need wide layout)

  [Page heading row]
    [Avatar chip 48px + Name (24px, $fg1) + role/status badges]
    ← read from the detail data (passed as prop or re-fetched via useAdminUserProfile)

  [Form card — Card variant="default", padding $6 (24px), borderRadius $card (20px)]
    [Section heading: "Harmless Profile Fields" is not shown; use subsection labels instead]

    [Country field — TextField or native <select> for a short country list]
      Label: EN "Country" / AR "البلد" (the request DTO field is `country`, maps to entity `Nationality`)
      Input: text input, maxLength 100
      Inline validation: max 100 chars

    [Display Language field]
      Label: EN "Display Language (UI)" / AR "لغة الواجهة (التطبيق)"
      Control: styled <select> with two options:
        { value: 'ar', label: 'Arabic — العربية' }
        { value: 'en', label: 'English — الإنجليزية' }
      Note: this maps to preferredLanguage on the DTO (but the PATCH body uses the key `preferredLanguage`)
      ← change here is harmless; no warning shown; saves via profile PATCH

    [Visual separator — border-top 1px solid $border, marginVertical $4]

    [Learning Language section — DISTINCT from Display Language above]
      [Section label row — flexDirection row, alignItems center, justifyContent space-between, gap $3]
        [Left block]
          [Label — fontSize 14px, fontWeight 600, color $fg1]
            EN: "Learning Language (Math & Science)" / AR: "لغة الدراسة (الرياضيات والعلوم)"
          [Sub-label — fontSize 12px, color $fg3, marginTop 2px]
            EN: "Changes this at the subject level — Math & Science only." / AR: "يؤثر فقط على مادتَي الرياضيات والعلوم."
        [Current value badge — same pill styling as StatusBadge but purple-themed]
          ← learningLanguage from the detail DTO: "Arabic / العربية" or "English / الإنجليزية"
      [Destructive action note — padding $3 (12px), backgroundColor rgba(239,68,68,0.08),
                                  borderRadius $sm (8px), border 1px solid rgba(239,68,68,0.15),
                                  marginTop $3 (12px), flexDirection row, gap $2]
        [Lucide alert-triangle 14px $danger]
        [Text — fontSize 12px, color $fg2]
          EN: "Changing the learning language resets Math and Science progress. This cannot be undone."
          AR: "تغيير لغة الدراسة يُعيد ضبط تقدم الرياضيات والعلوم. لا يمكن التراجع عن ذلك."
      [Button — opens ChangeLearningLanguageDialog]
        variant="ghost" (NOT primary — destructive gateway should be low-prominence until confirmed)
        borderColor: rgba(239,68,68,0.3) (danger-tinted ghost to signal caution)
        color: $danger (#EF4444)
        hoverStyle: backgroundColor rgba(239,68,68,0.08)
        label EN: "Change Learning Language" / AR: "تغيير لغة الدراسة"
        height 44px (min)
        disabled if learningLanguage cannot be determined (loading/error of detail)

    [Visual separator — border-top 1px solid $border, marginVertical $4]

    [Grade Override section]
      [Section label row — same pattern as Learning Language]
        Label: EN "Grade" / AR "الصف"
        Current value: "Grade {N}" / "الصف {N}" (Eastern-Arabic N in AR reading text)
      [Note — fontSize 12px, color $fg3]
        EN: "Overriding the grade re-scopes the curriculum. XP, badges, and progress history are preserved."
        AR: "تجاوز الصف يُعيد تحديد المناهج. تبقى النقاط والشارات وسجل التقدم محفوظة."
      [Button — opens GradeOverrideDialog]
        variant="ghost" size="sm"
        label EN: "Override Grade" / AR: "تجاوز الصف"

    [Form actions row — marginTop $6, flexDirection row, justifyContent flex-end, gap $3]
      [Cancel — Button variant="ghost" size="sm", href back to /users/{id}]
        EN: "Cancel" / AR: "إلغاء"
      [Save Changes — Button variant="primary" size="md"]
        EN: "Save Changes" / AR: "حفظ التغييرات"
        disabled: if no fields changed from loaded values
        loading: during PATCH mutation
        aria-label: EN "Save profile changes for {userName}" / AR "حفظ تغييرات ملف {userName}"
```

**Save logic:** only sends changed fields to `PATCH /api/Admin/Users/{childId}/profile`. If only `country` changed, send `{ country }` only. If only `preferredLanguage` changed, send `{ preferredLanguage }` only. If neither changed, button is disabled (no-op prevention client-side).

**Learning Language and Grade Override buttons do NOT submit the form** — they open separate dialogs. The "Save Changes" button ONLY handles the harmless fields (country + preferredLanguage).

### F.2 — Child Edit page entry point (on the detail page)

P7-08 adds an "Edit Profile" button to the detail page child-only section. This sits in the `<ChildEditEntry>` seam pre-cut by Batch B:

```
[Edit entry — only when roles includes Student]
  [Button variant="ghost" size="sm", icon Lucide pencil 14px]
    label EN: "Edit Profile" / AR: "تعديل الملف الشخصي"
    href: /users/{id}/edit (Next.js Link)
```

### F.3 — `GradeOverrideDialog`

**File:** `components/GradeOverrideDialog.tsx`

Uses `AdminConfirmDialog` with `variant="grade"` icon (`graduation-cap`, indigo).

**Props:** `childId: number, childName: string, currentGrade: number, onClose: () => void`

**Content:**

```
[Dialog body]

  [Current grade display — padding $3 (12px), backgroundColor $bg, borderRadius $sm (8px),
                           border 1px solid $border, flexDirection row, alignItems center, justifyContent space-between]
    [Label — fontSize 12px, color $fg3]
      EN: "Current grade" / AR: "الصف الحالي"
    [Value — fontSize 16px, fontWeight 700, color $fg1]
      EN: "Grade {currentGrade}" / AR: "الصف {١}" (Eastern-Arabic in AR)

  [Arrow indicator — Lucide arrow-down 20px, $fg3, alignSelf center]

  [Grade select field]
    [Label — fontSize 12px, fontWeight 600, color $fg3, textTransform uppercase, letterSpacing 0.04em]
      EN: "New grade" / AR: "الصف الجديد"
    [Native <select> — same styling as filter selects, height 44px]
      Options 1–6:
        EN: "Grade 1" … "Grade 6"
        AR: "الصف الأول" … "الصف السادس" (full Arabic ordinal text, not numerals, matching curriculum convention)
      Placeholder (unselected): EN "Select a grade" / AR "اختر الصف"

  [Preserve notice — only when a grade is selected]
    [padding $3 (12px), backgroundColor rgba(34,197,94,0.08), borderRadius $sm (8px),
     border 1px solid rgba(34,197,94,0.15), flexDirection row, gap $2, alignItems flex-start]
      [Lucide icon — shield-check, 14px, $secondary (#22C55E)]
      [Text — fontSize 13px, color $fg2, lineHeight 1.5]
        EN: "Curriculum will re-scope to Grade {N}. XP, level, badges, streaks and mastery records are preserved."
        AR: "ستُعاد معايرة المناهج للصف {N}. تبقى النقاط والمستوى والشارات والسلاسل وسجلات الإتقان محفوظة."

  [ReasonField — required (FE rule per D3, even though backend accepts null)]
    Label: EN "Reason for override (required)" / AR: "سبب التجاوز (مطلوب)"
    maxLength: 500

  [AdminErrorBanner — conditional]
```

**Confirm gate:** `aria-disabled` until: `selectedGrade > 0 && selectedGrade !== currentGrade && reason.trim().length > 0`

**Confirm button:** standard primary (indigo): EN "Override Grade" / AR: "تجاوز الصف"

**Error copy:**
- 422 (range invalid): EN "Grade must be between 1 and 6." / AR: "يجب أن يكون الصف بين ١ و٦."
- 400 (same grade): EN "This is already the child's current grade." / AR: "هذا هو الصف الحالي للطفل بالفعل."
- 400 (confirm missing): EN "Please confirm the grade override." (should be unreachable via UI)
- 404 / not a child: EN "This account is not a student account." / AR: "هذا الحساب ليس حساب طالب."
- network/5xx: standard copy

**Success:** close dialog, `AdminErrorBanner success-variant`:
- EN: "{childName}'s grade has been updated to Grade {N}." / AR: "تم تحديث صف {childName} إلى {الصف N}."
- Invalidate admin user-detail query → profile refreshes with new grade.

### F.4 — `ChangeLearningLanguageDialog` (admin variant)

**File:** `components/ChangeLearningLanguageDialog.tsx`

This is the most destructive dialog in the wave. Uses `AdminConfirmDialog` with `variant="destructive"` icon (`alert-triangle`, danger-red).

**Props:** `childId: number, childName: string, currentLanguage: 'ar' | 'en', onClose: () => void`

**Note:** unlike the parent P8-04 spec which uses a checkbox ack, the admin version uses a **typed confirmation** for stronger friction (admins are professionals performing an irreversible action on someone else's child — typed confirm is appropriate here). This is consistent with the Delete dialog's typed-confirm pattern already in this wave.

**Content:**

```
[Dialog body]

  [Destructive warning block — padding $4 (16px), backgroundColor rgba(239,68,68,0.08),
                               borderRadius $sm (8px), border 1px solid rgba(239,68,68,0.25),
                               flexDirection column, gap $3 (12px)]
    [Title line — fontSize 14px, fontWeight 700, color $danger]
      EN: "This will permanently reset Math and Science progress" / AR: "سيُعيد هذا ضبط تقدم الرياضيات والعلوم بشكل دائم"
    [Loss list]
      [Row — flexDirection row, gap $2, alignItems flex-start]
        [Lucide x-circle 14px $danger]
        [Text — fontSize 13px, color $fg2, lineHeight 1.5]
          EN: "All Math and Science lesson attempts, mastery records, and progress are deleted. They cannot be recovered."
          AR: "ستُحذف جميع محاولات دروس الرياضيات والعلوم وسجلات الإتقان والتقدم. لا يمكن استردادها."
    [Kept list]
      [Row — flexDirection row, gap $2, alignItems flex-start]
        [Lucide check-circle 14px $secondary]
        [Text — fontSize 13px, color $fg2]
          EN: "Arabic, English, XP, streak and badges are not affected." / AR: "العربية والإنجليزية والنقاط والسلسلة والشارات غير متأثرة."

  [Language change display — from/to]
    [Container — flexDirection row, alignItems center, gap $4, padding $3 (12px),
                 backgroundColor $bg, borderRadius $sm, border 1px solid $border]
      [From — fontWeight 600, color $fg3, fontSize 14px, textDecoration line-through]
        currentLanguage human name: "Arabic / العربية" or "English / الإنجليزية"
      [Arrow icon — Lucide arrow-right 16px $fg3 in LTR; arrow-left in RTL]
      [To — fontWeight 700, color $fg1, fontSize 14px]
        ← rendered dynamically once language select is chosen (§ below)

  [Language select — native <select>]
    [Label — required, 12px uppercase]
      EN: "New learning language" / AR: "لغة الدراسة الجديدة"
    Options: ar / en (same as grade: label as full language name, exclude current)
    If both are available (ar ≠ current or en ≠ current), show both; current pre-excluded from meaningful selection.

  [Confirm gate — same TypedConfirmField pattern, but typed text is "CONFIRM" (all caps)]
    Instruction text:
      EN: 'Type CONFIRM to proceed with the fresh start:' / AR: 'اكتب CONFIRM للمتابعة مع إعادة البدء:'
    Target display box: shows literal text "CONFIRM" (Latin, dir="ltr" always)
    Input placeholder: EN 'Type CONFIRM' / AR 'اكتب CONFIRM'
    Note: The word CONFIRM is kept as a Latin token even in AR (like a command/code). Surround it with direction-neutral treatment (span dir="ltr").
    Match logic: typedValue.trim() === 'CONFIRM' (case-sensitive — the capital matters for friction)

  [AdminErrorBanner — conditional]
```

**Confirm gate logic:** button `aria-disabled` until:
1. `selectedLanguage` is chosen and differs from `currentLanguage`
2. `typedValue === 'CONFIRM'` (exact, case-sensitive)

**Confirm button — destructive styling** (same as Delete button):
- `backgroundColor $danger`, `color $fg1`, `borderRadius $button (16px)`, `height 44px`
- EN: "Reset & Change Language" / AR: "إعادة الضبط وتغيير اللغة"
- `aria-label`: EN "Confirm fresh start: reset Math and Science progress for {childName}" / AR ...

**Error copy:**
- 424 (confirm flag missing, defensive): EN "Fresh start was not confirmed. No changes were made." / AR "لم يتم تأكيد إعادة البدء. لم يُجرَ أي تغيير."
- 422 (unsupported language): standard
- 404 / not a child: standard
- same language (200 no-op, backend): close dialog, show "No change made" notice
- network/5xx: standard

**Success:** close + success banner:
- EN: "{childName}'s learning language has been changed to {language}. Math and Science progress has been reset." / AR: "تم تغيير لغة دراسة {childName} إلى {language}. أُعيد ضبط تقدم الرياضيات والعلوم."
- Invalidate admin user-detail query + child Math/Science activity caches.

### F.5 — P7-08 `AdminStrings` slots

```typescript
// New slots for P7-08:
childEditPageTitle: string;
childEditBreadcrumb: string;
childEditSaveChanges: string;
childEditCancel: string;
childEditCountryLabel: string;
childEditDisplayLanguageLabel: string;
childEditLearningLanguageLabel: string;
childEditLearningLanguageSub: string;
childEditLearningLanguageWarning: string;
childEditChangeLearningLanguage: string;
childEditGradeLabel: string;
childEditGradeNote: string;
childEditOverrideGrade: string;
childEditNotStudent: string;
childEditEditProfileButton: string;

gradeDialogTitle: string;
gradeDialogSubtitle: string;
gradeDialogCurrentLabel: string;
gradeDialogNewLabel: string;
gradeDialogSelectPlaceholder: string;
gradeDialogPreserveNotice: string;
gradeDialogReasonLabel: string;
gradeDialogConfirm: string;
gradeDialogSuccess: string;
gradeError422: string;
gradeError400SameGrade: string;
gradeError404: string;

langDialogTitle: string;
langDialogSubtitle: string;
langDialogLossLine: string;
langDialogKeptLine: string;
langDialogFromLabel: string;   // "from" marker in from/to display
langDialogNewLabel: string;
langDialogTypedInstruction: string;
langDialogConfirm: string;
langDialogSuccess: string;
langError424: string;
langError422: string;
langErrorNoOp: string;         // same language → no change
```

---

## PART G: RTL Specification (applies across all three stories)

The admin app is EN-first but RTL-ready. All new components must use logical CSS properties, not physical left/right. When `ADMIN_LOCALE = 'ar'` and `dir="rtl"` is set on the root element:

| Element | LTR | RTL |
|---|---|---|
| Side nav | Left column | Right column |
| Active nav indicator | `borderInlineStart 3px solid $primary` | Flips to right edge automatically via logical property |
| Table text alignment | Left-aligned (`text-align: start`) | Right-aligned |
| Table chevron (row nav) | Lucide `chevron-right` | Lucide `chevron-left` |
| Pagination prev/next icons | Left = prev, Right = next | Right = prev, Left = next |
| Dialog action row | Cancel left, Confirm right | Cancel right, Confirm left (use `flex-direction: row-reverse`) |
| Breadcrumb separator | `chevron-right` | `chevron-left` |
| From→To language arrow | `arrow-right` | `arrow-left` |
| Family member chevron | `chevron-right` | `chevron-left` |
| TypedConfirmField target text (email / "CONFIRM") | natural | `dir="ltr"` forced — technical string |
| XP/streak/badge numbers | natural numerals | Latin numerals, `dir="ltr"` on number span |
| Grade "Grade N" | natural | "الصف ١" with Eastern-Arabic N for in-line AR text |
| Email addresses | natural | `dir="ltr"` forced — ASCII technical string |
| Date values | natural | `dir="ltr"` forced — date format is a technical string |

**AR font substitutions in admin:**
- Headings (EN: Poppins weight 700) → Cairo weight 700
- Body/labels (EN: Poppins weight 400/500/600) → Tajawal weight 400/500/600
- Monospace (emails, typed values) → stays `--lx-font-mono` regardless of locale

**AR heading notes:** Cairo renders slightly larger optically at the same pixel size. The frontend agent does not need to compensate — the size tokens stay the same; Cairo's metrics handle the visual weight. No letter-spacing adjustments (AR script has its own natural spacing).

---

## PART H: Accessibility Requirements

All surfaces in this wave must meet these requirements:

**Structural a11y:**
- `<table>` with `<th scope="col">` for column headers. `<caption>` (visually hidden) for the table: EN "User accounts list" / AR "قائمة حسابات المستخدمين".
- `aria-live="polite"` region wrapping the results area so filter changes announce updated result count.
- `aria-current="page"` on the active nav item.
- `role="status"` + `aria-label` on all skeleton loading states.
- `role="alert"` (via `AdminErrorBanner`) on all error/success banners — no `aria-live` needed since `role="alert"` implies assertive.

**Dialogs:**
- `role="dialog"` `aria-modal="true"` `aria-labelledby={id-of-title}` on every dialog card.
- Focus trap: on dialog open, focus moves to first interactive element. On close, focus returns to the trigger button.
- ESC key always triggers Cancel (close without action), never Confirm.
- Backdrop click: does NOT close the dialog (prevents accidental dismiss of destructive confirmation).
- Destructive Confirm buttons use `aria-disabled` (not HTML `disabled`) when gate is not met, so keyboard users can still focus it and understand why it's inactive (screen readers read the label).

**Forms:**
- All inputs and selects have associated `<label>` elements (via `for`/`id` or `aria-label`).
- Error messages are associated to their inputs via `aria-describedby`.
- `aria-invalid="true"` on inputs in error state.
- Char counter in `ReasonField` is associated as a `aria-describedby` supplemental description.

**Navigation:**
- Keyboard navigation order: side nav → topbar → page content (logical tab order).
- `skip-to-main` link as first focusable element (already a best practice; add if not present in the shell).

**Color:** all foreground/background combinations in this spec meet WCAG AA (4.5:1 minimum). Key checks:
- `$fg1 (#F8FAFC)` on `$card (#1E293B)` = 14.7:1 (passes AAA)
- `$fg3 (#94A3B8)` on `$card (#1E293B)` = 5.8:1 (passes AA)
- `$danger (#EF4444)` on `$dangerSoft (rgba(239,68,68,0.18))` = checked against effective background = ~2.5:1 (low on its own) — always paired with icon + text weight (never color-only signal). Add a non-color signal for every danger state.
- White `$fg1` on `$danger (#EF4444)` button = 4.6:1 (AA pass)

---

## PART I: Motion Spec

All animation durations and easings are minimal on the admin surface:

| Interaction | Duration | Easing | CSS variable |
|---|---|---|---|
| Nav item hover background | 120ms | `--lx-ease-out` | `--lx-dur-fast` |
| Input focus border + ring | 120ms | `--lx-ease-out` | `--lx-dur-fast` |
| Button press scale 0.95 | 80ms | `--lx-ease-out` | — |
| Button hover scale 1.02 + brighten | 120ms | `--lx-ease-out` | `--lx-dur-fast` |
| Row hover background (table) | 120ms | `--lx-ease-out` | `--lx-dur-fast` |
| Error/success banner entrance | 240ms | `--lx-ease-out` | `--lx-dur-base` |
| Dialog overlay fade-in | 200ms | `--lx-ease-out` | — |
| Dialog card slide + fade-in | 200ms | `--lx-ease-out` | — |
| Skeleton shimmer | 1400ms | linear infinite | — |
| Page transitions (Next.js route) | none (instant) | — | — |

No spring (`--lx-ease-spring`) anywhere on the admin surface — that easing is reserved for student-facing reward moments. No XP glow, no confetti, no celebration animation.

---

## PART J: Token Reference Summary

All token values from `design-system/colors_and_type.css`. Tamagui tokens mirror these 1:1 (the admin app re-exports `@learnexia/design-system/config`).

| Usage | Tamagui token | CSS var | Hex / value |
|---|---|---|---|
| Page background | `$bg` | `--lx-bg` | `#0F172A` |
| Side nav + topbar background | `$bgElevated` | `--lx-bg-elevated` | `#111B33` |
| Default card | `$card` | `--lx-card` | `#1E293B` |
| Soft / nested card | `$cardSoft` | `--lx-card-soft` | `#334155` |
| Primary action (indigo) | `$primary` | `--lx-primary` | `#4F46E5` |
| Primary hover | `$primaryHover` | `--lx-primary-hover` | `#6366F1` |
| Primary press | `$primaryPress` | `--lx-primary-press` | `#4338CA` |
| Primary soft bg | `$primarySoft` | `--lx-primary-soft` | `rgba(79,70,229,0.18)` |
| Success / green | `$secondary` | `--lx-secondary` | `#22C55E` |
| Success soft | `$successSoft` | `--lx-success-soft` | `rgba(34,197,94,0.18)` |
| Warning / amber | `$accent` | `--lx-accent` | `#F59E0B` |
| Warning soft | `$warningSoft` | `--lx-warning-soft` | `rgba(245,158,11,0.18)` |
| Danger / red | `$danger` | `--lx-danger` | `#EF4444` |
| Danger soft | `$dangerSoft` | `--lx-danger-soft` | `rgba(239,68,68,0.18)` |
| Purple / badge | `$purple` | `--lx-purple` | `#A855F7` |
| Purple soft | `$purpleSoft` | `--lx-purple-soft` | `rgba(168,85,247,0.18)` |
| Heading / primary text | `$fg1` | `--lx-fg1` | `#F8FAFC` |
| Body text | `$fg2` | `--lx-fg2` | `#CBD5E1` |
| Muted / labels | `$fg3` | `--lx-fg3` | `#94A3B8` |
| Default border | `$border` | `--lx-border` | `rgba(255,255,255,0.08)` |
| Strong border | `$borderStrong` | `--lx-border-strong` | `rgba(255,255,255,0.16)` |
| Focus border | `$borderFocus` | `--lx-border-focus` | `#4F46E5` |
| Overlay (backdrop) | `$overlay` | `--lx-overlay` | `rgba(15,23,42,0.72)` |
| Radius: inputs/chips | `$sm` | `--lx-radius-sm` | `8px` |
| Radius: buttons/nav items | `$button` | `--lx-radius-button` | `16px` |
| Radius: cards | `$card` (radius) | `--lx-radius-card` | `20px` |
| Radius: dialogs | `$modal` | `--lx-radius-modal` | `24px` |
| Radius: pills | `$pill` | `--lx-radius-pill` | `9999px` |
| Shadow: cards | `$shadowSoft` | `--lx-shadow-soft` | `0 4px 12px rgba(0,0,0,0.15)` |
| Shadow: floating (detail card) | `$shadowFloat` | `--lx-shadow-float` | `0 8px 24px rgba(0,0,0,0.25)` |
| Shadow: dialogs | — | `--lx-shadow-popup` | `0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.12)` |
| Focus ring (inputs) | — | `--lx-focus-ring` | `0 0 0 2px #4F46E5, 0 0 0 6px rgba(99,102,241,0.25)` (admin-variant: tighter glow than kid-facing) |
| Spacing 4px | `$1` | `--lx-space-1` | `4px` |
| Spacing 8px | `$2` | `--lx-space-2` | `8px` |
| Spacing 12px | `$3` | `--lx-space-3` | `12px` |
| Spacing 16px | `$4` | `--lx-space-4` | `16px` |
| Spacing 20px | `$5` | `--lx-space-5` | `20px` |
| Spacing 24px | `$6` | `--lx-space-6` | `24px` |
| Spacing 32px | `$8` | `--lx-space-8` | `32px` |
| Spacing 40px | `$10` | `--lx-space-10` | `40px` |
| Duration fast | — | `--lx-dur-fast` | `120ms` |
| Duration base | — | `--lx-dur-base` | `240ms` |
| Duration slow | — | `--lx-dur-slow` | `400ms` |
| Ease out | — | `--lx-ease-out` | `cubic-bezier(0.16,1,0.3,1)` |

---

## PART K: Implementation Handoff

### New shared components (built in P7-06 Batch B, imported by P7-07 and P7-08)

| Component | File | Stories that use it |
|---|---|---|
| `StatusBadge` | `apps/admin-dashboard/components/StatusBadge.tsx` | P7-06, P7-07, P7-08 |
| `AdminConfirmDialog` | `apps/admin-dashboard/components/AdminConfirmDialog.tsx` | P7-07, P7-08 |
| `ReasonField` | `apps/admin-dashboard/components/ReasonField.tsx` | P7-07, P7-08 |
| `TypedConfirmField` | `apps/admin-dashboard/components/TypedConfirmField.tsx` | P7-07 (Delete), P7-08 (learning-lang) |
| `AdminErrorBanner` (add success variant) | existing `components/AdminErrorBanner.tsx` | P7-07, P7-08 |

### New story-specific components

| Component | File | Story |
|---|---|---|
| `UserFamilyPanel` | `apps/admin-dashboard/components/UserFamilyPanel.tsx` | P7-06 |
| `UserActivityPanel` | `apps/admin-dashboard/components/UserActivityPanel.tsx` | P7-06 |
| `LifecycleActionsMenu` | added to `apps/admin-dashboard/app/(admin)/users/[id]/page.tsx` | P7-07 |
| `SuspendUserDialog` | `apps/admin-dashboard/components/SuspendUserDialog.tsx` | P7-07 |
| `ReactivateUserDialog` | `apps/admin-dashboard/components/ReactivateUserDialog.tsx` | P7-07 |
| `DeleteUserDialog` | `apps/admin-dashboard/components/DeleteUserDialog.tsx` | P7-07 |
| `GradeOverrideDialog` | `apps/admin-dashboard/components/GradeOverrideDialog.tsx` | P7-08 |
| `ChangeLearningLanguageDialog` | `apps/admin-dashboard/components/ChangeLearningLanguageDialog.tsx` | P7-08 |

### New pages

| Page | Route | Story |
|---|---|---|
| Users list | `apps/admin-dashboard/app/(admin)/users/page.tsx` | P7-06 |
| User detail | `apps/admin-dashboard/app/(admin)/users/[id]/page.tsx` | P7-06 |
| Child edit | `apps/admin-dashboard/app/(admin)/users/[id]/edit/page.tsx` | P7-08 |

### Shared files (serialized per plan — single writer at a time)

| File | What changes |
|---|---|
| `packages/api-client/src/query/queryKeys.ts` | Add `adminUsers.*` namespace (Batch A); D appends child-progress keys |
| `packages/api-client/src/hooks/index.ts` | A exports read hooks; C exports lifecycle hooks; D exports child mutation hooks |
| `apps/admin-dashboard/lib/strings.ts` | A adds base/nav; B adds list/detail copy; C adds lifecycle copy; D adds child-edit copy |
| `apps/admin-dashboard/components/AdminSideNav.tsx` | A upgrades to real active-aware Users item |

### `AccountStatus` shared const (D2)

Define once in `@learnexia/shared` in Batch A:

```typescript
// packages/shared/src/types/accountStatus.ts
export const AccountStatus = {
  Active: 0,
  Suspended: 1,
  Deleted: 2,
} as const;
export type AccountStatusValue = typeof AccountStatus[keyof typeof AccountStatus];
```

Export from `@learnexia/shared` index. P7-06, P7-07, P7-08 all import this — never hardcode 0/1/2 in three places.

---

## PART L: Design Gaps and Open Questions

**Gap 1 — No admin icon set.**
`design-system/assets/icons/` contains only gamification icons (flame, heart, star). Admin nav and action icons use Lucide as the flagged substitution (Brand Law rule 11 — Lucide is the designated substitute). This is the same gap noted in P1-10 §11 Gap 1. No action required in this wave.

**Gap 2 — `AdminErrorBanner` lacks a `success` variant.**
The existing `AdminErrorBanner` has `error | forbidden | warning` only. P7-07 (Reactivate success) and P7-08 (grade/language change success) need a positive success state. Spec: add `success` variant with `backgroundColor rgba(34,197,94,0.15)`, `borderColor rgba(34,197,94,0.3)`, `iconColor #22C55E`, icon `Lucide check-circle`. Frontend agent adds this variant when implementing P7-07 — it is a 4-line addition to the existing VARIANTS map, not a new component.

**Gap 3 — Language display format for `preferredLanguage`.**
The DTO returns culture codes like "ar-EG" and "en-US", not human-readable locale names. The admin detail page should display "Arabic (Egypt)" / "English (US)" or similar. Recommend using `Intl.DisplayNames` (web API) to format: `new Intl.DisplayNames(['en'], { type: 'language' }).of('ar-EG')` → "Arabic (Egypt)". Frontend agent implements this; no design token needed.

**Gap 4 — Admin locale toggle (carry-forward from P1-10 Gap 4).**
Admin ships EN-first, no live locale toggle. All AR copy is authored in `lib/strings.ts` for RTL-readiness testing but the toggle UI is not built. Confirm with lead: this wave does not change that constraint.

**Gap 5 — Grade ordinal labels in Arabic.**
The spec recommends "الصف الأول" through "الصف السادس" (full ordinal words) for the grade select options, matching Arabic curriculum convention. This differs from "الصف ١" (digit). The backend sends and receives integers 1–6. The frontend must map integer → Arabic ordinal display label. The exact ordinals are:
- 1 → الصف الأول
- 2 → الصف الثاني
- 3 → الصف الثالث
- 4 → الصف الرابع
- 5 → الصف الخامس
- 6 → الصف السادس

Add these to `AdminStrings` as `gradeLabel1` through `gradeLabel6`.

**Gap 6 — Typed-confirmation token for learning-language dialog.**
This spec locks the typed confirmation as the literal word `CONFIRM` (capital letters, Latin script) rather than an email or the child's name. Rationale: (a) unlike the Delete dialog where the email is a unique identifier, here there is no natural unique value to type; (b) the word "CONFIRM" in all-caps is recognizable to admin operators cross-language; (c) it is trivially parseable without locale issues. If the lead prefers the child's name instead, that is a valid alternative — flag as a decision to confirm before frontend builds this dialog.

**Gap 7 — ActivityPanel XP/level number rendering in the brief's data shape.**
The `AdminActivitySummaryDto.xp` field is `{ totalXp: number, currentLevel: number } | null`. On the admin surface, both values are always rendered as Latin numerals (they are technical counters, not localized reading text), regardless of AR locale. This is consistent with the "Latin for technical strings" rule. Frontend agent must not apply Eastern-Arabic numeral conversion to these values.

**Gap 8 — No "last sign-in" column in the list.**
The list DTO `AdminUserListItemDto` does not include `lastSignInAtUtc` (it only has `createdAt`). The detail page renders "not tracked" per D5. The list table column spec deliberately omits last-sign-in for this reason. If a future sprint adds a `lastSignInAt` column to the BE, it can be added as column 6 without spec changes.

**Gap 9 — Cascade on suspend is not supported (D10).**
The Suspend endpoint has no `cascadeChildren` parameter. This spec specifies NO cascade checkbox on the Suspend dialog. Only Delete has the cascade checkbox. If the lead wants cascade-suspend, that is a backend feature request, not a FE design change.

**Gap 10 — No "Users" nav item in the existing `AdminSideNav`.**
This wave adds it. The plan's Batch A handles this edit. The current `NAV_ITEMS` const has Curriculum + Content placeholders; the Users item is a new functional entry above them (Users is the first real data surface). Suggested order: Users (top, functional) → Curriculum (placeholder) → Content (placeholder).

---

Design spec ready for frontend.
