# UI Kit — Learnexia Web

A complete, navigable web app for parents (and the public marketing surface). 1280px wide.

## What's here

- `index.html` — multi-page demo with a crumb bar at the top to jump between pages.
- `Components.jsx` — shared building blocks: `PDSidebar`, `PDHeader`, `PDStatCard`, `PDActivityChart`, `PDWeakAreas`, `PDPanel`, `PDRecommendation`.
- `PagesPublic.jsx` — pre-auth surfaces: `LandingPage`, `LoginWebPage`, `RegisterWebPage`.
- `PagesApp.jsx` — in-app surfaces: `MyChildrenWebPage`, `ReportsWebPage`, `SettingsWebPage`, plus reusable `AppShell` (sidebar + content) and the Settings tab views.
- `browser-window.jsx` — browser chrome (not currently used; inline chrome lives in `index.html`).

## Pages

### Public (pre-auth)
1. **Landing** — Marketing site. Sticky nav, hero with phone mock + floating reward chips, feature grid, subjects band, gradient CTA, footer.
2. **Login** — Split layout. Left: purple visual panel with brand + tagline. Right: form with role toggle (Parent / Student), email + password, remember me, forgot, Google / Apple / Microsoft social buttons.
3. **Register** — Parent-only. Two-column form + benefits panel. Full name, country, email, password, COPPA-style consent. Step 1 of 2 progress bar.

### App (post-auth, with sidebar)
4. **My Children** — Family hero with combined weekly stats, child cards with avatar / level / XP / streak / mastery / weakest topic, dashed "Add a child" tile, security strip.
5. **Dashboard (Overview)** — Per-child KPI strip, weekly XP chart, subject mastery, weak areas, Lexi recommendations.
6. **Reports** — Detailed monthly report: 4 KPIs, 20-day XP chart, skills mastery breakdown, time-of-day chart with peak-focus insight, weak areas detail.
7. **Settings** — Tabbed account settings: Profile, Notifications (with toggles), Linked children, Security (password + 2FA), Plan & billing, Language & region.

## Caveats

- Static data — no real backend.
- Lucide icons substituted with emoji.
- Mascot owl is a placeholder.
- Light theme not implemented (the spec only defines dark surfaces for parent surfaces too).

