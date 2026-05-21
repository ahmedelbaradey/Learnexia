# UI Kit — Learnexia Parent Dashboard

Web dashboard for parents to track their child's progress.

## What's here

- `index.html` — full dashboard at 1280px wide, framed in a browser window.
- `Components.jsx` — `PDSidebar`, `PDHeader`, `PDStatCard`, `PDActivityChart`, `PDWeakAreas`, `PDPanel`, `PDRecommendation`.
- `browser-window.jsx` — browser chrome (not currently used; simple inline chrome is in `index.html`).

## Sections

- **Sidebar** — Logo, child switcher, nav (Overview, Reports, Activity, Subjects, Settings), weekly XP brag block.
- **KPI row** — Time learning, XP, lessons completed, streak (with week-over-week deltas).
- **Daily activity** — Bar chart of XP per day, today highlighted in indigo gradient.
- **Subject mastery** — Per-subject progress bars (Math, Reading, Science, Art).
- **Areas to focus on** — Ranked list of weak topics with accuracy %.
- **Recommendations** — Three Lexi-flavoured next-step cards (practice, schedule, celebrate).

## Caveats

- Static data — no real backend.
- Uses Poppins for everything; Arabic-RTL view not implemented in this kit.

