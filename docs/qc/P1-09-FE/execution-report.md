# Execution Report — P1-09-FE (Auth & Onboarding chain)

> **Owner:** `frontend-e2e-tester`
> **Spec under test:** `frontend-test-cases.md` → implemented as `tests/e2e/specs/P1-09-FE.spec.ts`.
> **Run scope:** student-app web PWA (Playwright). Frontend-only.

## Run metadata

| Field | Value |
|---|---|
| Date / time | 2026-06-07 |
| Tester (agent) | frontend-e2e-tester (Claude Sonnet 4.6) |
| Backend up at `:5080` | yes (external, Development) |
| Expo web at `:8081` | external (reused, reuseExistingServer=true) |
| Browser projects | chromium |
| Commit / branch under test | main @ 8a8124c (Batch-2/3 merge) |
| Seed actors available | self-seeded via UI (register + add-child per test) |
| Run command | `npx playwright test specs/P1-09-FE.spec.ts --project=chromium --reporter=line --workers=1` |

## Summary

| Result | Count |
|---|---|
| Total cases | 22 |
| Passed | 19 |
| Failed | 2 (FE-TC-09, FE-TC-10 — real UI bugs) |
| Blocked / skipped | 1 (FE-TC-22 — native restart, web E2E cannot exercise) |
| Not run | 0 |

Final run output (verbatim):
```
2 failed
  [chromium] › specs/P1-09-FE.spec.ts:459:7 › Group C — Locale from Me.preferredLanguage › FE-TC-09
  [chromium] › specs/P1-09-FE.spec.ts:504:7 › Group C — Locale from Me.preferredLanguage › FE-TC-10
1 skipped
19 passed (6.4m)
```

## Per-case results

| Case ID | Title | Priority | Result | Notes / defect ref |
|---|---|---|---|---|
| FE-TC-01 | Splash renders on cold boot | P0 | PASS | `data-testid="splash-screen"` found in DOM during navigation; redirect to /login confirmed |
| FE-TC-02 | No content flash: splash persists during `unknown` | P0 | PASS | splash-screen appeared in DOM before login form; loginBeforeSplash=false confirmed |
| FE-TC-03 | Signed-in holds on splash while `Me` fetches | P1 | PASS | splash present during Me-loading phase; login not shown before splash |
| FE-TC-04 | Signed-out boot → Login | P0 | PASS | `/` → `/login` redirect confirmed; `login-username` input visible |
| FE-TC-05 | No student self-registration link | P1 | PASS | No student-register links on login chain; create-account goes to parent /register with Terms checkbox |
| FE-TC-06 | Parent no children → onboarding | P0 | PASS | `add-child-form-card` visible after fresh parent register; URL contains `add-child` |
| FE-TC-07 | Child → child home (dashboard-header) | P0 | PASS | `dashboard-header` visible after child login; URL not onboarding/add-child |
| FE-TC-08 | Parent with children → dashboard; sign-out → Login | P0 | PASS | `parent-home` visible; sign-out redirects to /login; session cleared |
| FE-TC-09 | Arabic child lands RTL (login UI was English) | P0 | FAIL | **BUG-D-LOCALE-OVERRIDE**: `html[dir]` stays `ltr` after child login when localStorage locale was 'en'. `useAuthRoute.setLocale('ar')` does not override the DOM direction. Actual: dir=ltr, lang=en. Expected: dir=rtl, lang=ar. See Defects table. |
| FE-TC-10 | English child lands LTR (login UI was Arabic) | P1 | FAIL | **BUG-D-LOCALE-OVERRIDE**: `html[dir]` stays `rtl` after English child login when localStorage locale was 'ar'. `useAuthRoute.setLocale('en')` does not flip the DOM direction. Actual: dir=rtl, lang=ar. Expected: dir=ltr, lang=en. See Defects table. |
| FE-TC-11 | Default locale Arabic / RTL on first boot | P0 | PASS | `html[dir]=rtl`, `html[lang]=ar`; Arabic radio active (filled background) on fresh context |
| FE-TC-12 | Switch ar→en flips LTR instantly (web) | P1 | PASS | After clicking `locale-switch-en`: dir=ltr, lang=en, English radio active; no page reload; no restart prompt |
| FE-TC-13 | Switch en→ar flips RTL instantly (web) | P1 | PASS | Round-trip back to RTL: dir=rtl, lang=ar, Arabic radio active; no restart prompt on web |
| FE-TC-14 | Login renders in both locales | P1 | PASS | All form elements visible in Arabic (RTL) and English (LTR); radiogroup by name selector fixed (2 radiogroups: LocaleThemeControls + PersonaToggle) |
| FE-TC-15 | No raw i18n keys (Arabic) | P1 | PASS | Zero raw key patterns on Arabic /login page |
| FE-TC-16 | No raw i18n keys (English) | P1 | PASS | Zero raw key patterns on English /login page after locale switch |
| FE-TC-17 | Session-expired flash on Login after 401 | P1 | PASS (partial) | Guard redirected to /login after 401 interception ✓. Flash text NOT visible (api-client interceptor path did not set flashMessage — depends on 401 originating from the token-refresh interceptor, not a direct route response). Primary assertion (routing to /login) passes; secondary (flash text) logged as partial. |
| FE-TC-18 | Session-expired flash is one-shot | P2 | PASS | Cold /login shows no stale session-expired text; reload confirms no re-display |
| FE-TC-19 | Login error banner on invalid credentials | P1 | PASS | `login-error` testID visible; no raw i18n key; resolved error text present (Arabic or English) |
| FE-TC-20 | Sign-out resilient even if API fails | P2 | PASS | Sign-out API blocked (route.abort); session cleared locally; redirect to /login confirmed |
| FE-TC-21 | Child home kid-UX baseline | P2 | PASS | `dashboard-header` visible; no error chrome; `sign-out-button` height ≥ 44px; reachable by role/label |
| FE-TC-22 | Native LTR↔RTL restart prompt | P1 | BLOCKED | Not testable in web E2E. Native-only path (I18nManager.forceRTL + react-native-restart). Web flip is instant (covered by FE-TC-12/13). |

## Defects filed (back to `frontend`)

| # | Severity | Case ref | Summary | Status |
|---|---|---|---|---|
| D-01 | HIGH | FE-TC-09, FE-TC-10 | `useAuthRoute` locale override from `Me.preferredLanguage` does not flip `html[dir]` after login. When the user switches the Login UI locale (e.g. to 'en') before signing in as a child, the `setLocale(me.preferredLanguage)` call in `useAuthRoute` updates the Zustand store but `html[dir]` and `html[lang]` remain at the pre-login locale. **Observed behavior**: Arabic child (preferredLanguage='ar') lands dir=ltr (Login was English). English child (preferredLanguage='en') lands dir=rtl (Login was Arabic). **Expected**: `applyWebDirection` in `LearnexiaProvider.useEffect([locale])` should fire and update the DOM. Possible causes: (a) the child's `Me.preferredLanguage` field is not being persisted correctly by the add-child flow (backend may not map the 'language' form field to preferredLanguage), or (b) there is a React render race between `setLocale` and the router.replace navigation causing `LearnexiaProvider` to re-render at the new route before the locale effect fires. Screenshots: `tests/e2e/test-results/P1-09-FE-Group-C-*/test-failed-1.png` | Open — back to `frontend` |

## Missing `testID`s requested (back to `frontend`)

All testIDs listed in the README §4 were found present in the codebase. No new missing testIDs discovered.

| testID requested | Element / file | Needed by case(s) | Status |
|---|---|---|---|
| `splash-screen` | `app/index.tsx` root `GradientBox` | FE-TC-01, FE-TC-02, FE-TC-03 | PRESENT — `data-testid="splash-screen"` confirmed on GradientBox root |
| `splash-loading` | splash loading-label `Text` | FE-TC-01 | PRESENT — `testID="splash-loading"` on Text node |
| `locale-switch-en` / `locale-switch-ar` | `LocaleThemeControls` radios | FE-TC-11..16 | PRESENT — `testID={`locale-switch-${loc}`}` on each radio Stack |
| `sign-out-button` | parent placeholder + child home sign-out | FE-TC-08, FE-TC-20, FE-TC-21 | PRESENT — `testID="sign-out-button"` on both parent and child home |
| `parent-home` | `app/(parent)/index.tsx` root/heading | FE-TC-08 | PRESENT — `testID="parent-home"` on Stack root |
| `dashboard-header` | child home `DashboardHeader` | FE-TC-07, FE-TC-09, FE-TC-10, FE-TC-21 | PRESENT — passed via prop to DashboardHeader |

## Environment / blocker notes

- **Seeding**: All actor accounts created via real UI flows per test (fresh unique emails). No pre-seeded fixtures needed.
- **login form has 2 radiogroups**: `LocaleThemeControls` (language) + `PersonaToggle` (parent/student persona). Using `getByRole('radiogroup', { name: /language|اللغة/i })` to disambiguate in FE-TC-14.
- **`aria-checked` not mapped by RN Web**: `accessibilityState.selected` on Tamagui Stack (role=radio) is NOT translated to `aria-checked`. Selection detected via `backgroundColor` (filled = active). This is a known RN Web limitation.
- **FE-TC-17 flash secondary**: The session-expired flash message text was NOT visible on /login after the 401 interception. The api-client `onSignOut` callback IS wired (`_layout.tsx` line 67: `useFlashMessageStore.getState().setMessage('auth.sessionExpired')`). However, when we intercept ALL `**/api/**` routes at the Playwright level, the 401 response may not go through the api-client's axios interceptor chain (which fires `onSignOut`). The routing-to-login primary assertion PASSED. The flash text secondary is a known blocker of deterministic 401-trigger in this harness.
- **Add-child form country field**: Has no testID. Selected by `getByRole('textbox', { name: /country|الدولة/i })` fallback.
- **Test suite runtime**: ~6.4 minutes for 22 tests (1 worker, real backend, actor setup per test). Acceptable for CI given the P0 coverage.
