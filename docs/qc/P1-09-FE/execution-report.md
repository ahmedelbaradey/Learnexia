# Execution Report — P1-09-FE (Auth & Onboarding chain)

> **Owner:** `frontend-e2e-tester` (fill AFTER running). The QC architect scaffolds this template; it does NOT fill results.
> **Spec under test:** `frontend-test-cases.md` → implemented as `tests/e2e/specs/P1-09.spec.ts`.
> **Run scope:** student-app web PWA (Playwright). Frontend-only.

## Run metadata

| Field | Value |
|---|---|
| Date / time | _to fill_ |
| Tester (agent) | frontend-e2e-tester |
| Backend up at `:5080` | _yes / no_ |
| Expo web at `:8081` | _Playwright-owned / external_ |
| Browser projects | chromium / mobile (Pixel 7) |
| Commit / branch under test | _to fill_ |
| Seed actors available (parent-no-children / parent-with-children / ar-child / en-child) | _to fill_ |

## Summary

| Result | Count |
|---|---|
| Total cases | 22 |
| Passed | _to fill_ |
| Failed | _to fill_ |
| Blocked / skipped | _to fill_ (FE-TC-22 expected BLOCKED) |
| Not run | _to fill_ |

## Per-case results

| Case ID | Title | Priority | Result (pass/fail/blocked/skip) | Notes / defect ref |
|---|---|---|---|---|
| FE-TC-01 | Splash renders on cold boot | P0 | _ | _ |
| FE-TC-02 | No content flash: splash persists during `unknown` | P0 | _ | _ |
| FE-TC-03 | Signed-in holds on splash while `Me` fetches | P1 | _ | _ |
| FE-TC-04 | Signed-out boot → Login | P0 | _ | _ |
| FE-TC-05 | No student self-registration link | P1 | _ | _ |
| FE-TC-06 | Parent no children → onboarding | P0 | _ | _ |
| FE-TC-07 | Child → child home in child's language | P0 | _ | _ |
| FE-TC-08 | Parent with children → dashboard; sign-out → Login | P0 | _ | _ |
| FE-TC-09 | Arabic child lands RTL (UI was English) | P0 | _ | _ |
| FE-TC-10 | English child lands LTR (UI was Arabic) | P1 | _ | _ |
| FE-TC-11 | Default locale Arabic / RTL on first boot | P0 | _ | _ |
| FE-TC-12 | Switch ar→en flips to LTR instantly (web) | P1 | _ | _ |
| FE-TC-13 | Switch en→ar flips to RTL instantly (web) | P1 | _ | _ |
| FE-TC-14 | Login renders in both locales | P1 | _ | _ |
| FE-TC-15 | No raw i18n keys (Arabic) | P1 | _ | _ |
| FE-TC-16 | No raw i18n keys (English) | P1 | _ | _ |
| FE-TC-17 | Session-expired flash on Login | P1 | _ | _ |
| FE-TC-18 | Session-expired flash is one-shot | P2 | _ | _ |
| FE-TC-19 | Login error banner on invalid creds | P1 | _ | _ |
| FE-TC-20 | Sign-out resilient even if API fails | P2 | _ | _ |
| FE-TC-21 | Child home kid-UX baseline | P2 | _ | _ |
| FE-TC-22 | Native LTR↔RTL restart prompt | P1 | BLOCKED (web E2E cannot exercise native restart) | _ |

## Defects filed (back to `frontend`)

| # | Severity | Case ref | Summary | Status |
|---|---|---|---|---|
| _ | _ | _ | _ | _ |

## Missing `testID`s requested (back to `frontend`)

> Per README §4 — file these so future runs don't depend on copy/CSS.

| testID requested | Element / file | Needed by case(s) | Status |
|---|---|---|---|
| `splash-screen` | `app/index.tsx` root `GradientBox` | FE-TC-01, FE-TC-02, FE-TC-03 | _ |
| `splash-loading` | splash loading-label `Text` | FE-TC-01 | _ |
| `locale-switch-en` / `locale-switch-ar` | `LocaleThemeControls` radios | FE-TC-09..16 | _ |
| `sign-out-button` | parent placeholder + child home sign-out | FE-TC-08, FE-TC-20, FE-TC-21 | _ |
| `parent-home` | `app/(parent)/index.tsx` root/heading | FE-TC-08 | _ |
| `child-home` | `app/(child)/index.tsx` landing anchor | FE-TC-07 | _ |

## Environment / blocker notes

- _e.g. seed-data gaps, 401-trigger availability for FE-TC-17/18, route-URL observability for group B — to fill_
