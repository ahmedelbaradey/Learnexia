# Handoff / Update Notice → Claude Code

**Project:** Learnexia Design System (gamified AI learning platform — kids 7–12, parents, EN + Arabic RTL)
**This package covers two recent updates** to implement in the real codebase:

1. **NEW — Parent Mobile app** (a full parent companion app for phone, 10 screens, EN + AR)
2. **CHANGED — Auth flow** on every parent surface: now **Splash → Role Select → Login**, and the login no longer has an inline parent/student toggle.
3. **CHANGED — Responsive web + header controls** on the Parent Web dashboard (container-query tiers, bottom tab bar, compact child switcher, account menu).

---

## ⚠️ Read first — what these files are

The HTML/JSX files in this bundle are **design references** — prototypes that show the intended look and behavior. They are **not production code to copy verbatim**. Your task is to **recreate these designs in the target codebase's existing environment** (React Native / Flutter / SwiftUI for the mobile app; React/Next for web) using its established components, navigation, and state patterns. If no environment exists yet, pick the most appropriate framework and implement there.

**Fidelity: HIGH (hifi).** Colors, typography, spacing, and copy are final — match them pixel-for-pixel. All values use the shared design tokens in `colors_and_type.css` (also summarized below).

> **Note on file extensions:** the bundled source files carry a trailing `.txt` (e.g. `PMComponents.jsx.txt`, `index.html.txt`). Strip the `.txt` to get the real `.jsx` / `.html` and open in a browser. (The suffix only prevents the design-system tooling from re-compiling these reference copies.) To run a prototype live instead, open the originals in the project under `ui_kits/parent-mobile/` and `ui_kits/parent-dashboard/`.

---

## Update 1 — NEW: Parent Mobile app

A parent-facing companion app (separate from the existing **Student Mobile** app and **Parent Web** dashboard). Phone frame **402×874** (iPhone), dark navy theme, Poppins (Latin) / Cairo + Tajawal (Arabic).

### Navigation
- **5-tab bottom bar:** Children · Reports · Energy · Activity · Settings (glass bar, blur, indigo active state).
- **Auth flow precedes the app:** Splash → Role Select → Login/Register → app.

### Screens (10 + Add-Child sheet)
| Screen | Purpose | Key elements |
|---|---|---|
| **Splash** | Brand load | 🌟 Learnexia mark, radial purple bg, loading bar, "Parent Companion" |
| **Role Select** | Choose who's signing in | Two cards: 👨‍👩‍👦 Parent / 🎓 Student; note "children log in with parent-assigned email" |
| **Login** | Parent auth | 🌟 brand, "Welcome back", **role badge** ("Signing in as Parent · Change"), email/password, social, register link. **No inline role toggle.** |
| **Register** | Create parent account | name/email/password/country, consent checkbox |
| **My Children** | Family overview + child list | Gradient family-summary banner (Total XP / Lessons / Active), child rows (avatar, Lv/XP/streak/⚡energy mini-stats, mastery bar, weakest topic, ✏️ edit), Add-Child CTA |
| **Child Overview** | One child's progress | Back btn + avatar header, 3 KPI tiles (Time/XP/Lessons), daily-activity **bar chart**, subject-mastery bars, "Full report" / "Energy" buttons |
| **Reports** | Weekly report | Green summary banner, "Areas to focus" rows (icon+progress+%), "Recommendations from Lexi" cards, "Send report" |
| **Helper Energy** | Parent energy mgmt | Teal **battery** balance (180/300), hearts-vs-energy reassurance, weekly usage tiles, +500 top-up pack, "Buy credits" |
| **Activity** | Notification timeline | Filter chips (All/Badges/Energy/Alerts), event rows (who + action + time) |
| **Settings** | Account | Profile card, Preferences (language/theme/notifications/plan), Children & Safety, Log out |
| **Add Child sheet** | Bottom sheet | Photo upload (live preview), 6 grade **tiles** (🌱→🌴), 2 language **flag tiles** (🇪🇬 AR / 🇺🇸 EN), name/email |

### Reusable components (see `PMComponents.jsx`)
`PMTabBar`, `PMTopBar`, `PMCard`, `PMSectionTitle`, `PMStat`, `PMChildRow`, `PMMiniStat`, `PMBarChart`, `PMMasteryRow`, `PMFocusRow`, `PMButton` (variants: primary/energy/ghost/danger), `PMField`, `pmInput`. Child data array `PM_CHILDREN` (Sami/Layla/Yusuf).

### Files (strip the `.txt` suffix)
- `parent-mobile/index.html.txt` (EN React shell), `parent-mobile/index-ar.html.txt` (AR RTL, plain HTML)
- `parent-mobile/PMComponents.jsx.txt`, `PMScreens.jsx.txt`, `PMScreensExtra.jsx.txt`, `ios-frame.jsx.txt`
- Screenshots live in the project at `screenshots/parent-mobile/` and `screenshots/parent-mobile-ar/`

---

## Update 2 — CHANGED: Auth flow (all parent surfaces)

Applies to **Parent Mobile (EN+AR)** and **Parent Web dashboard (EN+AR)**.

**Before:** Login screen had an inline segmented toggle ("I'm a Parent / I'm a Student").
**After:**
1. **Splash** screen first (branded, tap/click to continue).
2. **Role Select** screen — pick Parent or Student.
3. **Login** — the chosen role is shown as a **read-only badge** ("Signing in as Parent") with a **"Change"** link back to Role Select. The inline toggle is **removed**.

The login visual was also restyled to match the student-mobile login (purple top glow, 🌟 brand logo, fields, social buttons, forgot-password).

### Behavior / state
- `role` state set on Role Select, passed into Login; Login renders the badge + role-appropriate copy/placeholder.
- Login "Change" → back to Role Select. Student role shows "Ask a parent to add you" instead of a register link.
- Web flow: `splash → role → login → app`. Mobile flow: same. Logout returns to `splash`.

### Files (strip the `.txt` suffix)
- Web EN: `parent-dashboard/index.html.txt` + `parent-dashboard/PagesPublic.jsx.txt` (new `SplashWebPage`, `RoleSelectWebPage`; `LoginWebPage` now takes `role`/`onBack`, toggle removed)
- Web AR: `parent-dashboard/index-ar.html.txt` (splash + role pages, login role badge)
- Web shared: `parent-dashboard/DashboardComponents.jsx.txt` (`PDSidebar`, `PDHeader`, `PDChildSwitcher`, `PDAccountMenu`, panels) + `parent-dashboard/PagesApp.jsx.txt` (`AppShell` + in-app pages)
- Mobile: covered by `parent-mobile/` files above
- Updated login screenshots in the project: `screenshots/web/02-login.png`, `screenshots/web-ar/02-login.png` (now show the badge, not the toggle)

---

## Update 3 — CHANGED: Responsive web dashboard + header controls

Applies to **Parent Web dashboard (EN + AR)**. The dashboard is no longer fixed-1280 desktop-only — it now reflows across three tiers and exposes the sidebar's controls in the header so they survive on mobile.

### Responsive tiers (driven by **container queries on `.frame`**, not viewport)
`.frame` is `container-type: inline-size; container-name: pdframe`, so panels respond to the **frame width** (correct for a fixed-frame kit + its preview cards), via `@container pdframe (max-width: …) { .pd-main [style*="grid-template-columns"] { … !important } }`:
- **Desktop (≥1025):** 240px sidebar + full multi-column grids.
- **Tablet (769–1024):** every multi-column grid → **2 columns**; sidebar stays.
- **Mobile (≤768):** sidebar **hidden** → glass **bottom tab bar** (Children · Overview · Reports · Energy · Settings); grids → **1 column**; auth split-screens stack; header tightens (`.pd-hide-sm` hides the period selector + Send Report).

### Header controls (work at every tier, since the sidebar hides on mobile)
- **Compact child switcher** — `<PDChildSwitcher compact/>` renders a circle-avatar + first-name pill + chevron (the full variant with grade subtitle stays in the sidebar on desktop). Dropdown to switch child or add one.
- **Account menu** — `PDAccountMenu`, the orange "A" avatar at the far right → dropdown with **Language** (🇺🇸 EN / 🇪🇬 AR), **Theme** (🌙 Night / ⬛ Black), and **Log out**. (Also in the sidebar footer on desktop.)

### Two implementation gotchas (cost us real bugs)
1. **The bottom tab bar must be a descendant of `.frame`** — container queries only match descendants of the container. In the AR plain-HTML file it's the last child of `.frame`, `position:absolute`.
2. **Bounded shell + inner scroll:** `AppShell` sets `.pd-shell { height: 820 }` so each page's content area (`flex:1; overflow:auto`) scrolls internally with the brand scrollbar. That makes the content a bounded flex column, so pin its direct children with `.pd-main > div[style*="overflow"] > * { flex-shrink: 0 }` — **without this the Family hero (which uses `overflow:hidden`) squishes to a thin strip and clips its title + stats.**

### Other web detail
- Auth panels use `padding: clamp(22px, 5vw, 56px)` so inputs never touch the edge on narrow frames.

### Files (strip the `.txt` suffix)
- `parent-dashboard/index.html.txt`, `parent-dashboard/index-ar.html.txt`, `parent-dashboard/DashboardComponents.jsx.txt`, `parent-dashboard/PagesApp.jsx.txt`
- Tablet/mobile reference screenshots in the project: `screenshots/web/10-tablet-dashboard.png`, `11-mobile-dashboard.png` (+ `web-ar/` twins)

---

## Design tokens (from `colors_and_type.css`)

**Core:** Primary `#4F46E5` · Success `#22C55E` · Warning `#F59E0B` · Danger `#EF4444`
**Surfaces:** Main bg `#0F172A` · Card `#1E293B` · Soft `#334155` · Deepest `#0B0C12`
**Text:** fg `#F8FAFC` · muted `#94A3B8` · subtle `#CBD5E1` · faint `#64748B`
**Gamification:** XP `#FACC15` · Streak `#FB923C` · Hearts `#FB7185` · Gem `#38BDF8` · Indigo-accent `#A5B4FC`
**Helper Energy (separate from Hearts):** teal `#2DD4BF`, deep `#14B8A6`
**Radii:** sm 8 · md 16 · lg 20 · card 24 · pill 9999
**Type:** H1 32 · H2 24 · H3 18 · body 14–16 · small 12. Display weight 900 (Poppins/Cairo).
**Fonts:** Poppins (Latin UI/display) · Cairo (Arabic headings) · Tajawal (Arabic body).

### Hearts vs Energy — never merge these two meters
Hearts = lives/mistakes (rose ❤️, top-left, shatter on loss). Energy = AI-helper fuel (teal ⚡ battery, top-right, smooth drain). They differ on color, icon, motion, position, and wording. Keep them visually distinct.

---

## RTL (Arabic)
Every screen has an Arabic twin: `dir="rtl"`, Cairo headings + Tajawal body, **Eastern-Arabic numerals in prose** (١٢٣) but Latin kept for technical strings (emails, `820 / 1000`). Mirror layout, flip chevrons, keep progress-bar fills LTR. Language toggle uses 🇪🇬 (AR) / 🇺🇸 (EN) flags.

## Assets
Logo + mascot in `assets/` (`logo-mark.svg`, `mascot-owl.svg`). Icons are **emoji** (intentional, per brand) + a few inline Lucide-style SVGs. No icon font.

## Where to look
Open the HTML files in a browser to interact with the prototypes. `README.md` and `SKILL.md` at the project root document the full system; `preview/index-master.html` is the site-map of every screen/link.
