# Implementation Brief — Parent Web (responsive header, nav, KPIs)

**Source of truth:** `ui_kits/parent-dashboard/index.html` (English, React) and `ui_kits/parent-dashboard/index-ar.html` (Arabic RTL, static HTML). Shared React components in `DashboardComponents.jsx`, `PagesApp.jsx`, `PagesPublic.jsx`. This is a hi-fi visual recreation over stubbed data — match the visuals/behavior, not the (mock) data layer.

## Auth entry flow (all parent surfaces, EN + AR)
`Splash → Role Select → Login`. Role is chosen on Role Select (Parent / Student); Login shows a read-only "Signing in as Parent · Change" badge (no inline role toggle). Children never self-register. Logout returns to Splash/Login.

## Responsive system (container-query based)
The app frame is a container: `.frame { container-type: inline-size; container-name: pdframe; max-width: calc(100vw - 32px) }`. Three tiers, driven by `@container pdframe`:
- **Desktop ≥1025px** — 240px left sidebar + full multi-column grids.
- **Tablet 769–1024px** — multi-column grids collapse to 2 columns; sidebar stays.
- **Mobile ≤768px** — sidebar hidden, replaced by a glass **bottom tab bar** (`.pd-bottombar`: Children · Overview/Dashboard · Reports · Energy · Settings); most grids stack to 1 column.

Inline grids reflow via an attribute selector — `@container … { .pd-main [style*="grid-template-columns"] { … !important } }` — so no per-element tagging is needed except the exceptions below.

## Header (`.pd-header`) — must be on EVERY post-login page
Every parent page (My Children, Dashboard/Overview, Reports, Energy, Settings) carries `.pd-header` with a right-side `.pd-header-actions` containing:
- **`.pd-header-switcher`** — compact child pill (avatar + name). **Hidden on desktop** (`display:none`; the sidebar carries the real child switcher), **shown only ≤768px** (`display:flex`).
- **Account avatar (`.pd-acct-btn`)** — opens a popover account menu: **Language** (EN/AR — AR switches to `index.html` / EN to `index-ar.html`), **Theme** (Night `#0F172A` / Black OLED via `data-theme` on `<html>`), **Log out** (→ login). Present and functional on all pages, both languages.
- Desktop-only extras tagged `.pd-hide-sm` (period `<select>`, Send Report) hide ≤768px.
- Mobile tightens header padding to `16px 18px`.

EN: `PDHeader` in `DashboardComponents.jsx` renders this (account menu = `PDAccountMenu`, switcher = `PDChildSwitcher compact`). AR: headers are static HTML normalized at runtime by `arWireAccountMenus()` in `index-ar.html` — it rebuilds each `.pd-header`'s actions so all five pages are identical and the menu works.

## KPI strip — 2-per-row on small screens
Dashboard/Overview and Reports KPI rows use class **`.pd-kpi-grid`** (4 columns desktop). Override: `@container pdframe (max-width:768px){ .pd-kpi-grid { grid-template-columns: repeat(2,1fr) !important } }` — they stay 2-up on mobile instead of stacking to 1.

## Family Hero — responsive banner
`.pd-family-hero` is `1.4fr repeat(4,1fr)` on desktop. ≤768px it becomes a banner: title block (`.pd-fh-title`) spans full width on top, then the 4 stats in a 2×2 grid with dividers; the oversized background emoji (`.pd-fh-emoji`) hides.

## Settings — labeled tab rail (NOT icon-only)
`.pd-settings-grid` = `200px 1fr` (rail + content). Tabs are icon **+ label**. Rail stays beside content on tablet; stacks (`1fr`) ≤768px. (An earlier icon-only rail was reverted.)

## Scrollbars
Brand-styled indigo scrollbars (`::-webkit-scrollbar*`, pill thumb) on both axes; reuse rather than default OS scrollbars.

## RTL rules (Arabic)
Native `dir="rtl"` + ambient direction; do NOT use row-reverse. Numerals are Eastern-Arabic (٠١٢…) in prose, Latin for technical strings (emails, `820/1000`). Fonts: Cairo (headings) + Tajawal (body); Poppins for EN.

## Parity checklist (EN ⇔ AR)
Header + account menu on all 5 pages · child pill hidden desktop / shown mobile · KPI 2-up on mobile · family hero banner reflow · settings labeled rail · bottom tab bar ≤768px. All verified in both languages.

## Design System tab
252 component/screen cards in `preview/*.html` (each is standalone HTML loading `_base.css` / `_base-ar.css`). New responsive cards: `web-kpi-grid-responsive.html`, `ar-web-kpi-grid-responsive.html`, `web-page-header-responsive.html`, `ar-web-page-header-responsive.html`. Tokens in `colors_and_type.css`; full guidance in `README.md` + `SKILL.md`.
