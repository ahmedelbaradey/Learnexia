# QC Test Plan + Coverage Report — P1-01-FE (Register screen, student-app web PWA)

> Story: [user-stories/Phase-1-Foundation/P1-01-register-student-or-parent.md](../../../user-stories/Phase-1-Foundation/P1-01-register-student-or-parent.md)
> FE tasks: [tasks/Frontend/student-app/Phase-1-Foundation/P1-01-FE.md](../../../tasks/Frontend/student-app/Phase-1-Foundation/P1-01-FE.md)
> Surface under test: `apps/student-app/app/(auth)/register.tsx` + `app/(auth)/_components/RegisterForm.tsx` (web PWA, Playwright)
> Scope: **frontend web E2E only** — no backend test cases this run.
> Run recipe: backend at `:5080`/Postgres prerequisite (NOT auto-started) → `pnpm --filter @learnexia/e2e test` (Playwright owns Expo web at `:8081`). See [docs/dev/HANDOFF.md](../../dev/HANDOFF.md) "Testing — E2E (Playwright)".

## 1. Summary

P1-01-FE is the **parent Register** screen — the only registration path in the product (students never self-register; the parent provisions children later in P1-03). The canonical screen + form were actually built under **P1-11-FE** (login/register screen set) and wired/extended under **P1-12-FE-7** (which added the `country` Select and the required `acceptedTerms` consent gate). This QC pass therefore **does not duplicate** generic P1-11 login coverage — it concentrates on the register flow proper: the happy path → onboarding, zod + `BaseResponse` error surfacing as i18n text, the `acceptedTerms` consent gate, `country` selection, duplicate-email and weak-password mapping, RTL/LTR, loading state, and the product override (no student self-register route).

**Counts**

| Metric | Count |
|---|---|
| Total FE cases | 20 |
| By surface | Frontend (web E2E) 20 / Backend 0 |
| P0 | 8 |
| P1 | 8 |
| P2 | 4 |
| Blocked (not testable as written) | 3 (FE-TC-04 success-route, FE-TC-13 duplicate-email, FE-TC-14 weak-password from server) — all depend on a live/seedable backend at `:5080`; see Risk + Open Questions |

All cases target **`frontend-e2e-tester`** (Playwright, chromium + mobile projects).

## 2. Coverage matrix (every acceptance criterion → case IDs)

Acceptance criteria are from the story. The two backend-only criteria are noted as out-of-scope for this FE run (covered by P1-01-BE / `api-tester`), not gaps.

| # | Acceptance criterion | FE case(s) | Verdict |
|---|---|---|---|
| AC-1 | Valid email + password → parent account created + JWT returned, user advances | FE-TC-01 (happy path form), FE-TC-04 (success → tokens persisted → route to onboarding) | Covered (FE-TC-04 BLOCKED on live backend) |
| AC-2 | Child account is **not** self-registered — parent-provisioned later | FE-TC-18 (no student self-register route), FE-TC-19 (parent-only consent banner + role copy), FE-TC-20 (login is the only other auth route) | Covered |
| AC-3 | Already-registered email → clear error, no duplicate created | FE-TC-13 (duplicate-email server error → i18n banner) | Covered (BLOCKED on live backend / seeded duplicate) |
| AC-4 | Password failing strength rules → registration blocked with a specific message | FE-TC-08 (client zod < 6 chars inline error), FE-TC-14 (backend `PASSWORD_REGEX` 422 → `weakPassword` banner for a client-valid-but-weak password) | Covered (FE-TC-14 BLOCKED on live backend) |
| AC-5 | Passwords hashed, never returned | — | **Out of scope for FE** — backend concern (P1-01-BE / `api-tester`). Not a FE gap. FE-TC-12 only asserts the password field is masked (`type=password`) as a UI hardening check. |

Supporting / cross-cutting cases (not 1:1 to an AC but required by the FE task + QC spec): FE-TC-02 (consent gate blocks submit), FE-TC-03 (consent check enables submit), FE-TC-05/06 (Arabic RTL / English LTR), FE-TC-07 (invalid email), FE-TC-09 (country required), FE-TC-10 (country selectable), FE-TC-11 (loading/pending state), FE-TC-15 (network error → generic banner), FE-TC-16 (back-to-sign-in nav), FE-TC-17 (email value stays LTR in RTL).

**Coverage verdict:** Every FE-relevant acceptance criterion (AC-1, AC-2, AC-3, AC-4) has at least one P0/P1 case. AC-5 is a backend assertion and is explicitly out of scope for this frontend-only run (no FE gap). **No uncovered FE criterion.**

## 3. Risk notes (where cases are weighted, and why)

- **Consent gate + product override (highest weight — AC-2 + a real money/compliance + COPPA surface).** `acceptedTerms` must default `false` and only become `true` by explicit user action; the comment in `RegisterForm.tsx` calls this a "security requirement — never auto-set". The whole product decision (parent-driven onboarding, no student self-register) hangs on this screen. Cases FE-TC-02/03/18/19/20 cover the gate and the absence of any self-register path.
- **Weak-password split brain (AC-4).** Client zod only enforces `min(6)` (`registerPasswordField`), but the backend enforces the full `PASSWORD_REGEX` (lower+upper+digit+special). So `"abcdef"` passes the client and is rejected by the server → must surface as the `weakPassword` i18n banner via `byStatus[422]`. This dual path is easy to regress; FE-TC-08 (client) and FE-TC-14 (server) split it deliberately.
- **Error-mapping fragility.** `useServerError` matches the duplicate-email hint on the broad substring list `['exists','duplicate','taken','email']` — the bare `'email'` token is very loose and could mis-map an unrelated email-mentioning server message to "duplicate email". FE-TC-13 asserts the user-visible banner is the duplicate-email i18n string (not a raw key, not a generic server error).
- **RTL is the default locale.** Arabic is the app default, so copy-based selectors are forbidden — cases must use `getByRole`/`getByLabel` (aria-label = the i18n label) or testIDs. FE-TC-05/06/17 assert direction wiring (heading/field `writingDirection`, email value forced LTR).
- **i18n surfacing, not raw keys.** zod messages are i18n KEYS resolved by the form via `t()`; a missing key would render the literal key string. FE-TC-07/08/09 assert localized human text appears, never a `auth.register.errors.*` key.

## 4. Open questions / assumptions (resolve before implementation)

1. **Missing `testID`s (primary blocker for stable selectors).** The register screen and `RegisterForm` pass **no** `testID`s — every UI primitive (`TextField`, `Select`, `CheckboxField`, `Button`, `ServerErrorBanner`) accepts an optional `testID` but the form does not set one. The tester must currently rely on `getByRole`/`getByLabel` against the i18n `accessibilityLabel`s. **Requested hooks for `frontend` to add** (would make every case below far more robust):
   - `testID="register-form"` on the form root, and field-level: `register-fullname`, `register-country`, `register-email`, `register-password`, `register-terms` (checkbox), `register-submit` (Button), `register-error` (ServerErrorBanner).
   Until added, cases use the documented role/label fallbacks; flag any that prove flaky back to `frontend`.
2. **Backend availability for the 3 server-error cases.** FE-TC-04 (success route), FE-TC-13 (duplicate email), FE-TC-14 (server weak-password) need a **running backend + Postgres at `:5080`** plus a **pre-seeded already-registered email** for the duplicate case. The e2e harness does NOT auto-start the backend. Lead must confirm: (a) the backend will be up for this run, (b) the seed email to use for the duplicate test (or that the tester registers it once then re-registers). Marked BLOCKED until confirmed.
3. **Success destination assumption.** On success the form routes to `/(onboarding)/add-child`. FE-TC-04 asserts the URL/onboarding header advances. If onboarding step-1 is a placeholder in the current build, the assertion should be the URL change only — confirm with lead.
4. **Country list source.** `country` options come from `COUNTRIES` (shared constants) localized by current locale. FE-TC-10 picks the first option generically (no copy assertion) to stay locale-agnostic.
5. **Terms/Privacy links are inline emphasis, not navigations.** `TermsLabel` renders "Terms"/"Privacy" as styled text spans with no `onPress`/href. No FE case asserts navigation from them (none exists). Confirm that is intended for P1-01 (likely a later story).

## 5. Handoff

- `frontend-test-cases.md` → **`frontend-e2e-tester`**: implement each FE-TC-* 1:1 as a Playwright test in `tests/e2e/specs/P1-01-FE.spec.ts`, preferring `getByTestId` (once hooks land) then `getByRole`/`getByLabel`. Do **not** assert on Arabic/English copy strings.
- `backend-test-cases.md` → **not produced** (frontend-only run).
- `execution-report.md` → the empty template in this folder. After running, **`frontend-e2e-tester`** fills pass/fail per case + defects (do not edit case definitions; raise selector/testID gaps back to `frontend`). The QC architect never fills results.
