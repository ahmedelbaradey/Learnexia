# Execution Report — P2-12-FE (Parent Settings tabs, web E2E)

> **Filled by:** `frontend-e2e-tester` **after** running `tests/e2e/specs/P2-12-FE.spec.ts`. qc leaves this empty (template only).
> **Spec source:** `docs/qc/P2-12-FE/frontend-test-cases.md` (1 test per `FE-TC-*`).
> Mark each row **PASS / FAIL / BLOCKED / SKIPPED**. For FAIL/BLOCKED, link the defect in §3.

## 1. Run metadata

| Field | Value |
|---|---|
| Date / time (UTC) | _TBD_ |
| Branch / commit | _TBD_ |
| Playwright project(s) (chromium / mobile) | _TBD_ |
| Web server (WEB_URL) | http://localhost:8081 |
| Backend (API_URL) | http://localhost:5080 |
| Backend reachable? | _TBD_ |
| Seed used (parent/child) | _TBD_ |
| Locale(s) exercised | _TBD_ |
| Total / Pass / Fail / Blocked / Skipped | _TBD_ |

## 2. Results per case

| Case ID | Title (short) | Priority | Result | Notes / evidence |
|---|---|---|---|---|
| FE-TC-01 | Six tabs render, four P2-12 panels reachable | P0 | _TBD_ | |
| FE-TC-02 | Each tab swaps in correct panel (no ComingSoon) | P0 | _TBD_ | |
| FE-TC-03 | Default Profile; switch away/back preserves shell | P2 | _TBD_ | |
| FE-TC-04 | Notifications 4×2 switch grid renders | P0 | _TBD_ | |
| FE-TC-05 | Toggles reflect saved server state | P0 | _TBD_ | |
| FE-TC-06 | Toggle persists via full-array PUT (optimistic) | P0 | _TBD_ | |
| FE-TC-07 | Failed PUT rolls back + shows error | P0 | _TBD_ | |
| FE-TC-08 | Switches disabled while PUT in flight | P1 | _TBD_ | |
| FE-TC-09 | Notifications loading → content | P1 | _TBD_ | |
| FE-TC-10 | Notifications i18n: no raw keys (en + ar) | P1 | _TBD_ | |
| FE-TC-11 | Notifications RTL row + switch-pair flip | P1 | _TBD_ | |
| FE-TC-12 | Switch a11y: role=switch + aria-checked + label | P1 | _TBD_ | |
| FE-TC-13 | Linked-children lists parent's own children | P0 | _TBD_ | |
| FE-TC-14 | Empty state (no children) | P1 | _TBD_ | |
| FE-TC-15 | Add-child CTA → /add-child | P1 | _TBD_ | |
| FE-TC-16 | Inline Edit form opens + validates | P1 | _TBD_ | |
| FE-TC-17 | Edit submit persists (PUT) + per-row success | P1 | _TBD_ | |
| FE-TC-18 | Inline Unlink confirm strip → unlink call | P0 | _TBD_ | |
| FE-TC-19 | Unlink Cancel dismisses, no API call | P1 | _TBD_ | |
| FE-TC-20 | Unlink last-parent 400 keeps strip + error | P1 | _TBD_ | |
| FE-TC-21 | Linked-children loading + load-error | P1 | _TBD_ | |
| FE-TC-22 | Per-child learning-language row (P8-04 xref) | P2 | _TBD_ | |
| FE-TC-23 | Linked-children RTL: names AR, email LTR | P2 | _TBD_ | |
| FE-TC-24 | IDOR-ish: parent sees only own children | P0 | _TBD_ | |
| FE-TC-25 | Security renders password form + sessions | P1 | _TBD_ | |
| FE-TC-26 | Client validation: mismatch + same-as-current | P1 | _TBD_ | |
| FE-TC-27 | Password fields forceLtr in Arabic | P2 | _TBD_ | |
| FE-TC-28 | Change-password success clears + invalidates sessions | P1 | _TBD_ | |
| FE-TC-29 | Wrong-current → localized error | P1 | _TBD_ | |
| FE-TC-30 | Active sessions: truncated id + status pill | P2 | _TBD_ | |
| FE-TC-31 | Sessions empty state | P2 | _TBD_ | |
| FE-TC-32 | Plan: name + status pill + disabled Manage | P1 | _TBD_ | |
| FE-TC-33 | Manage CTA non-interactive | P2 | _TBD_ | |
| FE-TC-34 | Plan loading + error states | P2 | _TBD_ | |
| FE-TC-35 | Plan-name localization AR (Free → مجاني) | P2 | _TBD_ | |
| FE-TC-36 | Settings RTL across four new tabs | P1 | _TBD_ | |
| FE-TC-37 | Signed-out /settings → Login | P0 | _TBD_ | |
| FE-TC-38 | Theme carries into panels (xref P1-11, thin) | P2 | _TBD_ | |
| FE-TC-39 | Sign-out-others count message | P1 | **BLOCKED** | needs ≥2 real sessions seed |
| FE-TC-40 | Notification rollback-shake motion | P2 | **BLOCKED** | motion not assertable in Playwright |
| FE-TC-41 | AR fonts Cairo/Tajawal on panels | P2 | **BLOCKED** | DG-W10-01 (font resolution unresolved) |

## 3. Defects found

> One row per defect. Reference the case ID(s) that surfaced it. File a `frontend` ticket for any FAIL.

| Defect ID | Case(s) | Severity | Summary | Repro | Suspected area |
|---|---|---|---|---|---|
| _none yet_ | | | | | |

## 4. Notes / deviations

- _Selectors used where no testID exists (record any role/aria-label fallbacks here, and the `frontend` testID ticket if filed):_ _TBD_
- _Route-interception used vs live backend per case:_ _TBD_
- _Locale-switch approach for EN assertions:_ _TBD_
- _Any case re-scoped or merged during implementation (note why):_ _TBD_

---
## Isolated run result (P2-HARDENING exit gate, fresh Expo) — 37 pass / 1 fail / 3 skip
Run: `npx playwright test specs/P2-12-FE.spec.ts --project=chromium --workers=1` on a freshly-restarted
Expo. **37 pass / 1 fail / 3 skip** (the combined batch-2 run's 0/38 was a Metro-OOM artifact, not real).
- **1 FAIL — FE-TC-23:** in an RTL (ar) settings card, the email field's *computed* text direction is
  expected `ltr` but resolved otherwise. Minor bidi/`unicode-bidi` styling assertion — file to `frontend`
  to set the email field `direction: ltr` (or relax the assertion); not release-blocking.
- 3 skips: route-mock-only / multi-session-seed cases (see classification doc categories F/I).
- DEF-01 (aria-checked on the notification switch) noted in FE-TC-12 — see P2-09 retrofit follow-ups.
