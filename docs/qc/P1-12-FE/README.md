# QC Test Plan + Coverage Report — P1-12-FE (Web account features)

> Story: `user-stories/Phase-1-Foundation/P1-12-web-account-backend-batch2.md`
> FE tasks: `tasks/Frontend/student-app/Phase-1-Foundation/P1-12-FE.md`
> Design spec: `design-system/ui_kits/student-app/P1-12-FE.md`
> Surface under test: **student-app web PWA only** (Expo web, `apps/student-app/app/`). Default locale **ar (RTL)**, default theme **dark**.
> Run owner for results: **`frontend-e2e-tester`** (Playwright, `tests/e2e/`). This run is **frontend-only** — no `backend-test-cases.md` produced.

---

## 1. Summary

This pass designs the web-E2E test catalogue for the six P1-11-deferred account affordances that P1-12-FE lights up, all **merged/feature-complete** on `main` per HANDOFF (Batches 1+2 merged PR #96/#97; Batch 3 — Google OAuth — PR open):

| # | Surface | File(s) | Story criterion |
|---|---------|---------|-----------------|
| 1 | Profile save | `(parent)/_components/SettingsWeb.tsx` → `ProfilePanel` | P1-12a / FE-1 |
| 2 | Avatar upload / remove | `SettingsWeb.tsx` `ProfilePanel` (web `<input type=file>`) | P1-12b / FE-2 |
| 3 | Google sign-in button states | `(auth)/_components/LoginForm.tsx` + `loginParts.tsx` `SocialButton` | P1-12c / FE-3 |
| 4 | Forgot-password screen | `(auth)/forgot-password.tsx` | P1-12d / FE-4 |
| 5 | Reset-password screen | `(auth)/reset-password.tsx` | P1-12d / FE-4 |
| 6 | Register consent + country | `(auth)/_components/RegisterForm.tsx` | P1-12f / FE-7 |
| 7 | Edit child (slim edit) | `(onboarding)/_components/EditChildSheet.tsx`, `ChildDashboardCard.tsx`, `MyChildrenWeb.tsx` | P1-12e / FE-5 |
| — | RTL/en + dark/light QA across the above | all of the above | FE-6 |

**Counts**

- **Total cases: 41** (all frontend; `frontend-e2e-tester`).
- **By surface:** Profile save 6 · Avatar 7 · Google sign-in 5 · Forgot-password 5 · Reset-password 7 · Register consent 4 · Edit-child 5 · cross-cutting RTL/i18n/a11y 2.
- **By priority:** P0 = 16 · P1 = 18 · P2 = 7.
- **By status:** Live-testable now = 33 · **BLOCKED = 8** (see §5).

---

## 2. Coverage matrix (acceptance criterion → case IDs)

| Acceptance criterion (story / FE task) | Covered by | Verdict |
|---|---|---|
| **FE-1 / P1-12a** — Profile read (enriched `/Me`) populates the form | FE-TC-01, FE-TC-06 | Covered |
| **FE-1 / P1-12a** — Profile update (fullName/phone/country) persists, success state | FE-TC-02 | Covered |
| **FE-1** — Profile validation + error surfacing (400/422 → i18n) | FE-TC-03, FE-TC-04 | Covered |
| **FE-1** — Cancel resets to loaded values; email display-only | FE-TC-05 | Covered |
| **FE-2 / P1-12b** — Avatar upload happy path (pick → upload → avatarUrl shows) | FE-TC-07 | Covered |
| **FE-2** — Client type validation (non-PNG/JPG rejected, no network call) | FE-TC-08 | Covered |
| **FE-2** — Client size validation (>5 MB rejected) | FE-TC-09 | Covered |
| **FE-2** — Upload pending overlay + buttons disabled | FE-TC-10 | Covered |
| **FE-2** — Server upload error surfaced inline | FE-TC-11 | Covered |
| **FE-2** — Remove happy path (Remove hidden on initials-only) | FE-TC-12, FE-TC-13 | Covered |
| **FE-3 / P1-12c** — Google button disabled / graceful-degrades when env unset | FE-TC-14 | Covered |
| **FE-3** — Google live OAuth happy path | FE-TC-15 | **BLOCKED (unset env)** |
| **FE-3** — Google in-flight one-action-at-a-time lock | FE-TC-16 | Covered (state-only) |
| **FE-3** — Google error → shared banner, no enumeration; user-cancel silent | FE-TC-17 | Covered (state-only) |
| **FE-3** — Apple/Microsoft dimmed disabled placeholders (no-op) | FE-TC-18 | Covered |
| **FE-4 / P1-12d** — Forgot link from Login routes to forgot-password | FE-TC-19 | Covered |
| **FE-4** — Forgot email validation | FE-TC-20 | Covered |
| **FE-4** — Anti-enumeration generic success on any 2xx (identical copy) | FE-TC-21 | Covered |
| **FE-4** — Forgot server/network error (generic) + Back-to-Sign-in | FE-TC-22, FE-TC-23 | Covered |
| **FE-4** — Reset: missing/empty token param → token-invalid block, no form | FE-TC-24 | Covered |
| **FE-4** — Reset: email param prefilled read-only; password policy + match | FE-TC-25, FE-TC-26 | Covered |
| **FE-4** — Reset: server token-invalid (400/410/422) → dedicated block + new-link | FE-TC-27 | **BLOCKED (token)** |
| **FE-4** — Reset: happy path → success panel → route to login | FE-TC-28 | **BLOCKED (token)** |
| **FE-4** — Reset: token never echoed in DOM/aria/URL-visible string | FE-TC-29 | Covered (partial; see §5) |
| **FE-4** — Reset: other server error → generic banner | FE-TC-30 | **BLOCKED (token)** |
| **FE-7 / P1-12f** — Consent unchecked → submit blocked (termsRequired), not pre-checked | FE-TC-31, FE-TC-32 | Covered |
| **FE-7** — Country empty → submit blocked (countryRequired) | FE-TC-33 | Covered |
| **FE-7** — Valid register posts country + acceptedTerms | FE-TC-34 | Covered |
| **FE-5 / P1-12e** — Edit affordance opens sheet pre-filled from child | FE-TC-35, FE-TC-36 | Covered |
| **FE-5** — Slim field set only (no password/learningLanguage); email read-only | FE-TC-37 | Covered |
| **FE-5** — Edit validation (name required, grade 1..6) | FE-TC-38 | Covered |
| **FE-5** — Save success closes sheet + refetches list | FE-TC-39 | Covered |
| **FE-5** — Save error → banner inside sheet | FE-TC-40 | Covered |
| **FE-6** — RTL (ar) vs LTR (en) + i18n text (no raw keys) across surfaces | FE-TC-41 + RTL clauses in each case | Covered |
| **Product override** — no student self-register; Login routes to parent register only | FE-TC-19 precondition + register flow | Covered (negative implicit) |

**Gaps:** No acceptance criterion is left without a case. **4 criteria are covered only as design/state assertions (BLOCKED for full live verification)** — the Google live-OAuth happy path and the three reset-password server-token paths — because they require provisioned secrets / a real reset token captured from email. They are written so the tester verifies everything reachable (disabled/missing-token/missing-env behaviour, structure, i18n) and marks the live leg BLOCKED with the reason rather than dropping it.

---

## 3. Risk notes (where cases are weighted, and why)

1. **Security-flavoured invariants carry P0 even though they are "negative" cases.** The highest-value bugs here are leaks/regressions, not happy-path breaks:
   - **Anti-enumeration** on forgot-password — the success copy must be **identical and unconditional on any 2xx** (FE-TC-21). A branch on "user exists" would be a privacy defect.
   - **Reset token never surfaced** — token must travel only in the mutation body, never in the DOM text, an `aria-label`, a visible URL-echo string, or a component key (FE-TC-29).
   - **Consent never auto-checked** — `acceptedTerms` default `false`, submit gated (FE-TC-31/32). Auto-true would be a COPPA/legal defect.
   - **Google error must not enumerate** — generic `socialFailed`, no "account not found" reveal (FE-TC-17).
   - **Edit-child slim set** — no password/email/learningLanguage editable; password re-entry would be a security smell + the four-field contract must be matched exactly (FE-TC-37).
2. **Client-side avatar validation is the real gate** (FE-TC-08/09): the `<input accept>` is advisory; the JS allowlist + 5 MB cap is what actually blocks, and must reject **before** any network call. Easy to regress to "accept anything, let server 4xx".
3. **One-action-at-a-time lock** on the Login screen (FE-TC-16): while Google is in flight, the email submit + Apple + Microsoft must all disable. A regression here lets a user fire two auth flows at once.
4. **RTL is the default, not the exception** — Arabic is the app default. Every surface ships `writingDirection`/`forceLtr` handling; the cross-cutting RTL case (FE-TC-41) plus per-case RTL clauses guard the Latin-technical fields (email/phone/token) staying LTR and the Google brand label staying Latin even in `ar`.
5. **Selector fragility (highest *test-implementation* risk):** these surfaces ship **no `testID`s** — only `accessibilityLabel` (i18n-keyed) + `accessibilityRole`. With Arabic as default, copy-based selectors are forbidden. See §4 Q1 — the tester must select by `getByRole` + i18n-resolved `aria-label`, and report any flow that lacks a stable hook back to `frontend` rather than reaching into CSS.

---

## 4. Open questions / assumptions (lead must resolve before/with implementation)

**Q1 — Missing `testID`s (BLOCKER for stable selectors).** None of the P1-12-FE surfaces expose `testID`. RN Web maps `accessibilityLabel`→`aria-label` and `accessibilityRole`→`role`, so the tester *can* select by role+aria-label resolved through the i18n resources — but several controls share the same role with no distinguishing label (e.g. the two password fields on reset use distinct labels, OK; but the avatar Upload/Remove buttons, profile Save/Cancel, and the social buttons rely entirely on i18n-keyed `aria-label`). **Recommend `frontend` add stable `testID`s** to at least: profile Save/Cancel/Upload/Remove buttons + the hidden file input; the forgot/reset submit buttons + email/password fields; each `SocialButton`; the edit-sheet container + Save button + the card Edit affordance; the register consent checkbox + country select + submit. Until then the tester selects by role+aria-label and flags any ambiguous control.

**Q2 — Google live-OAuth env (`EXPO_PUBLIC_GOOGLE_CLIENT_ID`).** Per HANDOFF this is gitignored and **likely unset in the test env**; the button gracefully disables (`disabled={!isGoogleConfigured || !request}`). **Assumption: env is UNSET in CI/test** → FE-TC-15 (live happy path) is BLOCKED and the *disabled-state* case FE-TC-14 is the real coverage. If the lead provisions the web client ID in the test env, FE-TC-14 inverts (button becomes enabled) and FE-TC-15 becomes runnable against Google's real dialog (still hard to automate headlessly).

**Q3 — Reset-password token capture.** The reset deep link is `…/reset-password?email=&token=`; a valid token can only be obtained from the delivered reset email (Notifications module, English-only per HANDOFF P6-06). **Assumption: no email pipeline in the E2E env** → the three server-token paths (FE-TC-27/28/30) are BLOCKED; everything reachable by crafting the URL directly (missing-token block FE-TC-24, prefilled-email + client validation FE-TC-25/26, token-not-echoed FE-TC-29) is live-testable by navigating to `reset-password?email=test@x.com&token=...` with a fabricated/garbage token. A **garbage token** will exercise the server-rejection path against a running backend — if the backend is up, FE-TC-27 (400/410/422 → token-invalid block) becomes partially runnable; mark per env.

**Q4 — Backend availability for the E2E run.** Per `tests/e2e/README.md`, the backend at `:5080` is a prerequisite and is **not** auto-started (needs the Postgres stack). Cases that assert *success* states (profile save, avatar upload, edit-child save, forgot 2xx) need a live backend + a seeded authenticated parent + at least one linked child. **Assumption: the tester seeds a parent (register or known creds) and one child via the API before the parent-surface cases.** If the backend is down, the success-leg cases are BLOCKED (env) and only validation/disabled/state legs run.

**Q5 — Reset success transition timing.** `reset-password.tsx` shows a success panel then `router.replace('/(auth)/login')` after ~1800 ms (`setTimeout`). The tester should assert the success panel first, then the login route — not race the redirect.

**Q6 — Avatar file fixtures.** The tester needs fixtures: a valid small PNG/JPG (<5 MB), a disallowed type (e.g. `.gif`/`.svg`/`.pdf`), and an oversized (>5 MB) image. Confirm these can be added under `tests/e2e/` fixtures.

---

## 5. What is marked BLOCKED (and why)

| Case | Surface | Blocker | What the tester still verifies |
|---|---|---|---|
| FE-TC-15 | Google live OAuth happy path | **Unset env** `EXPO_PUBLIC_GOOGLE_CLIENT_ID` (+ unautomatable Google dialog) | The disabled/graceful-degrade path (FE-TC-14) instead |
| FE-TC-27 | Reset server token-invalid/expired | **Token** — needs a real/garbage token + running backend | Partially runnable with a garbage token if backend is up; else BLOCKED |
| FE-TC-28 | Reset happy path → login | **Token** — needs a valid token from the reset email | Client validation (FE-TC-25/26) + missing-token block (FE-TC-24) |
| FE-TC-30 | Reset other server error (generic) | **Token + backend error injection** | The generic-banner mapping is asserted at design level |
| FE-TC-07 (success leg) | Avatar upload success | **Backend/env** — needs live avatar endpoint + auth | The pick/validation/pending legs run client-side regardless |
| FE-TC-02 / 39 (success leg) | Profile save / edit-child save success | **Backend/env** — needs live endpoints + seeded auth/child | Validation + form-state legs run regardless |
| FE-TC-21 (2xx leg) | Forgot anti-enumeration success | **Backend/env** — needs a 2xx from the forgot endpoint | Validation + structure run regardless |
| FE-TC-29 (full) | Reset token-not-echoed | **Token** to assert the body-only path end-to-end | DOM/aria/URL inspection with a crafted token runs regardless |

> All BLOCKED cases stay in the catalogue with a written blocker. The tester records them as **Blocked** in `execution-report.md` (not Fail) and runs every reachable leg.

---

## 6. Handoff

- **`frontend-test-cases.md`** → **`frontend-e2e-tester`**: implement FE-TC-01 … FE-TC-41 as Playwright specs under `tests/e2e/specs/P1-12-FE.spec.ts`, following the selector convention (`getByTestId` → `getByRole`/`getByLabel`, never copy — Arabic is default). Where a stable hook is missing (Q1), report the needed `testID` back to `frontend`; do not reach into CSS.
- **`execution-report.md`**: the tester fills pass/fail/blocked per case **after** running, plus any defects (filed back to `frontend`). This QC pass scaffolds the empty template only — it never fills results.
- **No `backend-test-cases.md`** — this is a frontend-only run by scope.
- Backend prerequisite + run recipe: `tests/e2e/README.md` (backend at `:5080`, Postgres stack, then `pnpm --filter @learnexia/e2e test`).

Test cases ready — `frontend-e2e-tester` to implement `frontend-test-cases.md`; results into `execution-report.md`.
