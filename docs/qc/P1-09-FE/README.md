# QC Test Plan & Coverage Report — P1-09-FE (Auth & Onboarding screens)

**Story:** `user-stories/Phase-1-Foundation/P1-09-auth-onboarding-screens.md`
**Tasks:** `tasks/Frontend/student-app/Phase-1-Foundation/P1-09-FE.md`
**Design Spec:** `design-system/ui_kits/student-app/P1-09.md`
**Run scope:** **Frontend-only** — student-app web PWA E2E (Playwright, `tests/e2e/`). No `backend-test-cases.md` produced.
**Target agent:** `frontend-e2e-tester`
**Date:** 2026-06-07

---

## 1. Summary

P1-09-FE is the **auth/onboarding wiring story**: the splash/boot screen, the `Me`-driven routing guard that lands each actor on the correct surface, and the Arabic-default RTL + locale chain across the auth screens. The story's own added value is the **splash → routing → locale chain**; the individual auth/onboarding *screens* (register, login, add-child, child home, parent placeholder) are owned and detailed by **P1-01..04-FE, P1-11-FE, P1-12-FE** and are already merged to `main`.

**This QC pass therefore scopes its cases to:**
- The **splash/boot** screen and its no-content-flash contract (`app/index.tsx`).
- The **`useAuthRoute` routing guard** — signed-out, parent-no-children, parent-with-children, student → correct landing (Design Spec §3 + Screens 11/12).
- The **Arabic-default RTL** baseline and the **language switch** (`LocaleThemeControls` on Login) — including the web-instant flip and the native-restart boundary (web cannot exercise native restart → BLOCKED).
- **i18n integrity** — no raw keys leak in either locale across the chain.
- Screen-level **loading / error / flash (session-expired)** states the design spec defines for this chain.
- **Kid-UX (NFR-6)** touch-target / one-primary-action sanity on the child landing.
- **Product-override negatives** — no student self-register link anywhere on the auth chain.

It **does not** re-test field-level form validation, password toggles, social OAuth, add-child multi-child submit, or per-screen pixel chrome — those belong to the owning stories' specs (cross-referenced, not duplicated).

### Counts

| Metric | Count |
|---|---|
| **Total FE cases** | **22** |
| Backend cases | 0 (frontend-only run) |
| P0 | 9 |
| P1 | 9 |
| P2 | 4 |
| BLOCKED (not testable in web E2E) | 3 (FE-TC-12, FE-TC-13, FE-TC-22) |

### By surface / theme

| Theme | Case IDs |
|---|---|
| Splash / boot + no-flash | FE-TC-01, FE-TC-02, FE-TC-03 |
| Routing by `Me` (signed-out / parent / child) | FE-TC-04, FE-TC-05, FE-TC-06, FE-TC-07, FE-TC-08 |
| Locale apply from `Me.preferredLanguage` | FE-TC-09, FE-TC-10 |
| Arabic-default RTL + language switch | FE-TC-11, FE-TC-12, FE-TC-13, FE-TC-14 |
| i18n integrity (no raw keys) | FE-TC-15, FE-TC-16 |
| Session-expired flash | FE-TC-17, FE-TC-18 |
| Error / loading states | FE-TC-19, FE-TC-20 |
| Kid-UX (NFR-6) | FE-TC-21 |
| Native RTL restart (boundary) | FE-TC-22 (BLOCKED) |

---

## 2. Coverage matrix (acceptance criterion → case IDs)

| # | Acceptance criterion (story) | Covered by | Verdict |
|---|---|---|---|
| AC1 | Parent flow: Splash → Login/Register → Add Child → per-child setup, navigable end to end | FE-TC-01, FE-TC-04, FE-TC-06 (splash→login→onboarding chain). **Full add-child wizard owned by P1-03-FE / P8-01-FE.** | Covered (chain) |
| AC2 | Add-Child lets parent add multiple children + assign login email | *(P1-03-FE / P8-01-FE own this — out of P1-09 scope; cross-referenced, not duplicated.)* | Delegated |
| AC3 | Register & login call auth API + handle success/error (invalid creds, duplicate email) | FE-TC-19 (login error banner surfacing on the chain). **Field-level validation owned by P1-01-FE / P1-11-FE.** | Covered (chain) |
| AC4 | Child login by parent-assigned email → own home dashboard in chosen language (RTL for Arabic) | FE-TC-07, FE-TC-09, FE-TC-10, FE-TC-11 | Covered |
| AC5 | All screens render correctly in Arabic (RTL) and English | FE-TC-11, FE-TC-12, FE-TC-13, FE-TC-14, FE-TC-15, FE-TC-16 | Covered (web); native FE-TC-22 BLOCKED |
| DS-§3 | Routing-guard visual states (no content flash) | FE-TC-01, FE-TC-02, FE-TC-03, FE-TC-04, FE-TC-05, FE-TC-08 | Covered |
| DS-S1 | Splash brand + loader + session-expired flash | FE-TC-01, FE-TC-17, FE-TC-18 | Covered |
| DS-S10 | Language-switch UX (native restart prompt) | FE-TC-12, FE-TC-13 (web flip), FE-TC-22 (native restart — BLOCKED) | Partial (native blocked) |
| DS-S11 | Parent dashboard placeholder + sign-out → back to login | FE-TC-06, FE-TC-08, FE-TC-20 | Covered |
| DS-S12 | Child home placeholder in child's language + name | FE-TC-07, FE-TC-09, FE-TC-21 | Covered |
| Product | No student self-registration screen | FE-TC-05 (negative — no self-register link on login/auth chain) | Covered |

**Gap verdict:** Every P1-09 acceptance criterion is covered by at least one case, OR explicitly delegated to its owning story (AC2 add-child, and the field-level portions of AC3) with a cross-reference rather than a duplicate. The only *partial* is the **native RTL-restart leg of AC5/DS-S10**, which is structurally untestable in web E2E (FE-TC-22 BLOCKED, reason recorded).

---

## 3. Risk notes (where cases are weighted, and why)

1. **Routing guard (`useAuthRoute`) is the highest-risk surface** — it is the one piece of net-new logic this story owns. A wrong branch sends a child to the parent dashboard (or vice-versa), or strands a no-children parent on the dashboard instead of onboarding. Five P0/P1 cases (FE-TC-04..08) cover each `status × role × hasChildren` branch independently, because the bug class here is a single mis-evaluated condition.
2. **No-content-flash contract (DS §3)** — the guard must keep the splash visible during `status === 'unknown'` and during signed-in `Me` fetch; a regression flashes login or a blank screen. FE-TC-02/FE-TC-03 assert the splash *persists* (not just that it eventually navigates).
3. **Arabic is the DEFAULT locale** — every selector must be `getByTestId`/role/label, never copy. A test that asserts English copy passes only by accident. FE-TC-15/16 explicitly assert the *absence of raw i18n keys* in both locales (the failure mode when a namespace isn't loaded). The smoke spec already models the locale-agnostic discipline.
4. **Locale-from-`Me` for children (FE-TC-09/10)** — the guard sets the app locale from `Me.preferredLanguage` *after* login. A child whose language is Arabic but who logged in on an English UI must land RTL. This is a quiet cross-store side-effect (`useLocaleStore.setLocale`) that is easy to break and invisible without a targeted assertion.
5. **Native RTL flip needs a restart (web does not)** — design Screen 10. The risk is a tester writing a web case that *expects* a restart prompt (there is none on web) or, conversely, missing that native is out of reach. FE-TC-12/13 pin the **web-instant** behaviour; FE-TC-22 records the native leg as BLOCKED so it is not silently dropped.

---

## 4. Open questions / assumptions (lead to resolve before implementation)

**Selector / testID gaps (the load-bearing blocker for a robust web E2E run).** The auth chain leans on `accessibilityLabel` (i18n-keyed) + `accessibilityRole`; **few stable `testID`s exist on this story's own surfaces.** The tester should request these from `frontend` rather than fall back to copy (Arabic-default makes copy selectors fragile) or CSS:

1. **Splash root container** — `app/index.tsx` has **no `testID`** on its root, wordmark, or loader. Request `testID="splash-screen"` on the `GradientBox` root and `testID="splash-loading"` on the loading-label `Text`. Today the only stable web hook is the brand wordmark text "Learnexia" (forced Latin/LTR in both locales — usable but not ideal) and the loader's i18n label.
2. **Language switch control** — `LocaleThemeControls` exposes a `radiogroup` (`aria-label` = `common.prefs.language`) with two `radio`s labelled `common.prefs.switchToEnglish` / `common.prefs.switchToArabic`. These are role/label-addressable, but request `testID="locale-switch-en"` / `testID="locale-switch-ar"` for stability across copy changes.
3. **Sign-out affordance** — parent placeholder (`auth.signOut`) and child home (`child.subjects.signOut`) are label-only. Request `testID="sign-out-button"` on both so the round-trip-to-login cases (FE-TC-08, FE-TC-20) don't depend on locale-specific labels.
4. **Child-home greeting / parent-placeholder heading** — child uses `child.home.greeting`; parent uses `parent.dashboard.title`. The child surface has `testID="dashboard-header"`; the parent placeholder has none. Request `testID="parent-home"` and `testID="child-home"` as landing-confirmation anchors so the routing cases assert on a stable element, not on copy.

**Assumptions made (flag if wrong):**
- **A1 — Seeding is via the real API.** The tester signs in real seeded users (parent-no-children, parent-with-children, child) against the running backend at `:5080`. No fixture/mocked `Me`. If seed users for all three actor states don't exist, several routing cases are blocked on data — confirm the seed set or have the tester create them via register + add-child as a precondition.
- **A2 — Web E2E only.** Native RTL-restart (FE-TC-22) and the restart-prompt dialog are out of Playwright-web reach; recorded BLOCKED, not dropped.
- **A3 — URL/route is observable.** Expo Router on web yields navigable paths (`/login`, `/add-child`, `/(parent)`-equivalent, `/(child)`-equivalent). Routing assertions use `page.url()` plus a landing-element anchor. If the web router rewrites group segments (`(parent)`/`(child)` are non-URL groups), the tester must anchor on the landing element's testID instead of the path — noted per case.
- **A4 — `?flash=` / session-expired** is reproduced by triggering `onSignOut` (e.g. an expired/invalid token interceptor path) rather than by deep-linking a search param, since the flash is stored in `flashMessageStore`, not the URL. If a deterministic trigger isn't available, FE-TC-17/18 may downgrade to P2 — confirm the trigger.

---

## 5. Handoff

- **`frontend-e2e-tester`** implements **`docs/qc/P1-09-FE/frontend-test-cases.md`** as `tests/e2e/specs/P1-09.spec.ts` (per the harness convention), `getByTestId` → role → label, Arabic-default-safe.
- Any case marked **BLOCKED** stays in the spec as a `test.fixme`/skip with the recorded reason; missing-`testID` requests in §4 are filed back to `frontend`.
- After running, **`frontend-e2e-tester`** fills **`docs/qc/P1-09-FE/execution-report.md`** (pass/fail per case + defects). The QC architect never fills results.
- Results feed the **`reviewer`** gate for the P1-09-FE batch.

Test cases ready — `frontend-e2e-tester` to implement `frontend-test-cases.md`; results into `execution-report.md`. (No `backend-test-cases.md` — frontend-only run.)
