# UI Kit — Learnexia Parent Mobile

The parent-facing **mobile companion app** — monitor children, read reports, and manage Helper Energy from a phone. iPhone **402×874**, dark navy theme, Poppins (EN) / Cairo + Tajawal (AR). It mirrors the Parent Web dashboard, sized for a phone with a bottom tab bar.

## What's here

- `index.html` — English click-thru demo. Crumb bar at the top jumps between screens; a 5-tab glass **bottom tab bar** drives in-app navigation.
- `index-ar.html` — full **Arabic RTL** twin (plain HTML, mirrors the EN React app screen-for-screen).
- `PMComponents.jsx` — shared primitives (see below).
- `PMScreens.jsx` — `PMSplashScreen`, `PMRoleSelectScreen`, `PMLoginScreen`, `PMRegisterScreen`, `PMChildrenScreen`, `PMChildOverview`, `PMReportsScreen`.
- `PMScreensExtra.jsx` — `PMEnergyScreen`, `PMActivityScreen`, `PMSettingsScreen`, `PMAddChildSheet`.
- `ios-frame.jsx` — iOS status bar + device frame (shared with student-mobile).

## Auth flow (Splash → Role Select → Login)

Every parent surface (mobile + web, EN + AR) opens with the same gate:

1. **Splash** (`PMSplashScreen`) — branded 🌟 Learnexia screen + loading bar; tap to continue. "Parent Companion" subtitle.
2. **Role Select** (`PMRoleSelectScreen`) — "Who's signing in?" with two cards: **👨‍👩‍👦 Parent** and **🎓 Student**. Children log in with a parent-assigned email — they never self-register (stated on the screen).
3. **Login** (`PMLoginScreen`, takes `role` + `onBack`) — styled like the student-mobile login (purple top glow, 🌟 mark, email/password, social, forgot). **No inline parent/student toggle** — instead a read-only **"Signing in as Parent · Change"** badge (Change → back to Role Select). Logout returns to Splash.

## Screens (10)

| # | Screen | Notes |
|---|--------|-------|
| 1 | **Splash** | Brand + loading bar |
| 2 | **Role Select** | Parent / Student cards |
| 3 | **Login** | Role badge (not toggle), social, forgot |
| 4 | **Register** | Parent-only; name/email/password/country + COPPA-style consent |
| 5 | **My Children** | Family summary banner (gradient) + child rows (avatar, grade pill, language flag, Lv/XP/streak/⚡energy mini-stats, mastery bar, weakest topic, ✏️ edit) + dashed "Add a child" tile |
| 6 | **Child Overview** | KPI tiles (time/XP/lessons), daily-activity bar chart, subject-mastery bars, quick links to Reports / Energy |
| 7 | **Reports** | Weekly summary banner, areas to focus, Lexi recommendations, send-report |
| 8 | **Helper Energy** | Teal balance battery (180/300), hearts-vs-energy reassurance, weekly usage breakdown, 500-credit top-up |
| 9 | **Activity** | Notification timeline with filter chips (badges / energy / alerts) |
| 10 | **Settings** | Profile card, preferences (language / theme / notifications / plan), children & safety, **Log out** |

Plus the **Add Child bottom sheet** (`PMAddChildSheet`): photo upload with live preview, name + login email, **6 plant-emoji grade tiles** (🌱→🌴), **two flag language tiles** (🇪🇬 AR / 🇺🇸 EN). Slides up over a scrim.

## Components (`PMComponents.jsx`)

`PMScreen` (scrollable body inside the phone), `PMTopBar` (greeting + avatar), `PMTabBar` (5-tab glass bottom bar: Children · Reports · Energy · Activity · Settings), `PMCard`, `PMSectionTitle`, `PMStat` (KPI tile), `PMChildRow`, `PMMiniStat`, `PMBarChart`, `PMMasteryRow`, `PMFocusRow`, `PMButton` (primary / energy / ghost / danger), `PMField` + `pmInput`. Shared family data: `PM_CHILDREN` (Sami / Layla / Yusuf).

## Conventions

- **Tab bar** is the primary nav; the crumb bar is only a demo affordance.
- **Child rows** carry a ✏️ edit button that opens the Add/Edit sheet (`stopPropagation` so it doesn't also open the child).
- **Energy ≠ Hearts** — teal battery + lightning for AI-help fuel, kept visually distinct from the rose hearts meter (see root `Helper Energy` docs).
- **Bilingual parity** — EN and AR are screen-for-screen identical; Eastern-Arabic numerals in AR prose, Latin for technical strings.

## Caveats

- Static data — no real backend.
- AR build is plain HTML (not the EN React components) — edit both when changing a screen.
- Emoji used as icons (per brand); mascot owl is a placeholder.
- Dark theme only.
