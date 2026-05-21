# Design system & core component library (RTL/Arabic)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** Frontend Foundation
- **Issue type:** Story
- **Story Points:** 5 — tokens, a kid-friendly component set, fonts, and full RTL/Arabic layout support.
- **Labels:** `frontend`, `design-system`, `localization`
- **Requirements:** NFR-5, NFR-6, NFR-7

## Description
As a frontend engineer, I want a design system with reusable, kid-friendly components and Arabic-first RTL support, so that every screen is consistent, accessible to young learners, and works in both languages.

## Acceptance Criteria
- Design tokens (colors, typography, radius, spacing) are defined per the UI docs and consumed by components.
- Core components exist and are documented: Button, Card, XP bar, Hearts, Streak, Badge, AI Tutor bubble, Reward popup.
- Switching locale to Arabic flips the layout to RTL and uses Cairo/Tajawal fonts; English uses Poppins.
- Components meet kid-accessibility rules: large touch targets, one primary action per screen, high contrast, visual feedback on interaction.

## Notes
- Covers F1.1–F1.3. Auth screens themselves are a separate story (P1-09).
