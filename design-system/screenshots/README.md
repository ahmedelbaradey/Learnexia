# Learnexia Screenshots

Captures of every screen + every atomic component in the design system, English and Arabic.

## How to use this folder

- **`mobile/`** — 18 English mobile captures
- **`web/`** — 7 English web captures
- **`mobile-ar/`** — 18 Arabic (RTL) mobile captures (same screens as English)
- **`web-ar/`** — 7 Arabic (RTL) web captures (same pages as English)
- **`../preview/`** — atomic component cards. English (no prefix), Arabic prefixed `ar-`.

> The Design System tab inside this project also indexes every preview card with a one-line subtitle.

## Mobile (`mobile/`) — 18 screens

| # | Screen |
|---|---|
| 01 | splash |
| 02 | login |
| 03 | register |
| 04 | role-select |
| 05 | grade-select |
| 06 | subject-select |
| 07 | my-children |
| 08 | home |
| 09 | skill-tree |
| 10 | profile |
| 11 | lesson |
| 12 | quiz |
| 13 | reward |
| 14 | mission-completed |
| 15 | daily-mission |
| 16 | league |
| 17 | badges |
| 18 | hearts |

## Web (`web/`) — 7 pages

| # | Page |
|---|---|
| 01 | landing |
| 02 | login |
| 03 | register |
| 04 | my-children |
| 05 | dashboard |
| 06 | reports |
| 07 | settings |

## Atomic component cards (`../preview/`)

Anything an AI coder might want to pull as a single piece. Naming convention: `<surface>-<component>.html` where `<surface>` is `mobile-` or `web-`. Tokens (colors / type / radii / etc) are unprefixed.

### Tokens
- `colors-primary.html` · `colors-surfaces.html` · `colors-gamification.html` · `colors-text.html`
- `gradients.html` · `radii.html` · `elevation.html` · `borders-focus.html` · `spacing-scale.html`

### Type
- `type-display.html` · `type-body.html` · `type-numbers.html` · `type-arabic.html`

### Brand
- `logo.html` · `logo-mark.html` · `mascot.html`

### Shared components (gamification + UI)
- `components-buttons.html` · `components-hud.html` · `components-xp-bar.html`
- `components-badges.html` · `components-hearts-streak.html`
- `components-lesson-card.html` · `components-quiz.html` · `components-tutor.html`
- `components-missions.html` · `components-reward.html` · `components-input.html`
- `components-skill-node.html`

### Mobile-specific atoms
- `mobile-splash-anatomy.html` · `mobile-role-toggle.html` · `mobile-password-meter.html` · `mobile-consent.html`
- `mobile-social-buttons.html` · `mobile-country-select.html`
- `mobile-role-cards.html` · `mobile-grade-tiles.html` · `mobile-subject-rows.html` · `mobile-child-card.html`
- `mobile-home-topbar.html` · `mobile-continue-hero.html` · `mobile-continue-subjects.html` · `mobile-tabbar.html`
- `mobile-league-header.html` · `mobile-player-rows.html`
- `mobile-confetti-trophy.html` · `mobile-reward-stats.html`
- `mobile-mission-hero.html` · `mobile-mission-checklist.html` · `mobile-mission-reward-card.html` · `mobile-level-progress.html`
- `mobile-profile-hero.html` · `mobile-stat-tiles.html`
- `mobile-hearts-row.html` · `mobile-hearts-warning.html`
- `mobile-badge-stats-strip.html` · `mobile-badge-tiles.html` · `mobile-skill-path.html`

### Web-specific atoms
- `web-nav.html` · `web-hero-phonemock.html` · `web-feature-card.html` · `web-subject-band.html` · `web-cta-banner.html` · `web-footer.html`
- `web-auth-split.html` · `web-benefits-panel.html`
- `web-sidebar.html` · `web-page-header.html` · `web-browser-chrome.html`
- `web-family-hero.html` · `web-child-card.html` · `web-security-strip.html`
- `web-kpi-row.html` · `web-activity-chart.html` · `web-weak-areas-list.html` · `web-recommendations.html`
- `web-skills-mastery.html` · `web-time-of-day.html`
- `web-settings-tabs.html` · `web-toggle.html` · `web-linked-rows.html` · `web-plan-card.html` · `web-2fa-card.html`

## Regenerating

If you change a screen, re-run the capture flow against the live UI kits at `ui_kits/student-mobile/index.html` and `ui_kits/parent-dashboard/index.html`.