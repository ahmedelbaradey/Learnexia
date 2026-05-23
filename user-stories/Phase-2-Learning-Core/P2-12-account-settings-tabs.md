# Parent account settings — notifications, linked children, security, plan & billing

- **Project:** Learnexia
- **Sprint / Phase:** Phase 2 — Learning Core (per product decision; account settings slotted into the P2 sprint)
- **Epic:** Parent web app (extends P1-11)
- **Issue type:** Epic
- **Story Points:** 21 (sum of children) — split below; reslice during planning.
- **Labels:** `frontend`, `backend`, `web`, `settings`, `account`
- **Design source of truth:** `design-system/screenshots/web/07-settings.png` (pixel-perfect, per the designer rule).

## Description
As a parent, I want the remaining settings tabs — notification preferences, linked-children management, security, and plan & billing — built **back and front**, so that I can fully manage my account from the web settings page.

> Carved out of **P1-11** (which ships the Settings shell + Profile + Language & region). This story completes the other four tabs. Each tab matches its area of `screenshots/web/07-settings.png` and reuses the P1-11 tab shell + design tokens.

## Child stories

### P2-12a — Notification preferences (BE + FE)
**Issue type:** Story · **Points:** 5
**Description:** As a parent, I want to manage how/when Learnexia notifies me so that I control my alerts.
**Acceptance Criteria:**
- FE: Notifications tab matches the capture — toggles per channel (e.g. weekly report, streak/at-risk, product) and channel (email/push).
- BE: endpoints to read + update notification preferences for the parent; persisted per user.
- Changes save with success/error feedback; render en (LTR) + ar (RTL).
**Labels:** `frontend`, `backend`, `settings`

### P2-12b — Linked children management (BE + FE)
**Issue type:** Story · **Points:** 5
**Description:** As a parent, I want to view and manage my linked children from settings so that I can unlink or re-link accounts.
**Acceptance Criteria:**
- FE: Linked children tab lists children with link status; link (by email) + unlink actions.
- BE: list linked children, link/unlink endpoints with family-scope authz (reuses P1-04 link semantics).
- Confirm-before-unlink; family-scope enforced (a parent only manages their own children).
**Labels:** `frontend`, `backend`, `settings`, `family`
**Notes:** reuses/extends P1-04 (parent↔child link) + P1-05 (RBAC/family scope).

### P2-12c — Security (BE + FE)
**Issue type:** Story · **Points:** 5
**Description:** As a parent, I want to change my password and manage sessions so that my account stays secure.
**Acceptance Criteria:**
- FE: Security tab — change password (current + new + confirm, strength rules), and active-sessions/sign-out-others if supported.
- BE: change-password endpoint (validates current, enforces password policy); session management reuses existing Identity session services.
- On password change, other sessions are invalidated; clear success/error states.
**Labels:** `frontend`, `backend`, `settings`, `security`
**Notes:** reuses Identity `ChangePassword` + session services already in the backend.

### P2-12d — Plan & billing (BE + FE)
**Issue type:** Story · **Points:** 5
**Description:** As a parent, I want to see my plan and billing so that I understand my subscription.
**Acceptance Criteria:**
- FE: Plan & billing tab — current plan, status, and (if in scope) upgrade/manage CTA matching the capture.
- BE: read current plan/subscription for the parent (billing-provider integration is a separate concern — read-only/stub acceptable until a payments story exists).
- Renders en (LTR) + ar (RTL).
**Labels:** `frontend`, `backend`, `settings`, `billing`
**Notes:** full payment-provider integration is out of scope here — flag a follow-up payments story if upgrade/checkout is required.

## Notes
- **Blocked by P1-11** (Settings shell + tab bar + Profile/Language) and P1-08 (design system). Pixel-perfect to `screenshots/web/07-settings.png` per the designer rule.
- Thematically an account/settings story; placed in the Phase-2 sprint per the product owner's call.
- Product overrides apply: parent-driven, no teacher role, 4 subjects.
