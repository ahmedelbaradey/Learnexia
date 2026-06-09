# P2-12-FE — Frontend (web E2E) test cases — Parent Settings tabs

> Implementer: **`frontend-e2e-tester`** (Playwright, `tests/e2e/specs/P2-12-FE.spec.ts`).
> One `FE-TC-*` → one Playwright test (1:1). Honor **BLOCKED** markers as `test.skip` with the blocker in the title.
> Results go in this folder's `execution-report.md` — qc does **not** fill results.

## Harness facts (load-bearing)
- **Web app:** Playwright owns the Expo web server at `http://localhost:8081` (`WEB_URL`). Backend must be live at `http://localhost:5080` (`API_URL`).
- **Settings route:** `/settings` (Expo Router `(parent)/settings`). Reach it via `loginAsParent(...)` then `page.goto('/settings')` — reuse the seed/login/`uniqueEmail` helpers already in `tests/e2e/specs/P1-11-FE.spec.ts` (`seedParentWithChild`, `loginAsParent`, `registerParent`).
- **Default locale = Arabic (RTL).** Select by `getByTestId` / `getByRole` / `getByLabel` — **never** by visible Arabic copy. EN assertions only after switching to EN (Profile-tab has no locale switch; use the Language tab `settings-language-switch`, or seed an EN-preferred user, or set locale on Login before navigating).
- **Tab order (0-indexed):** Profile(0), Notifications(1), Linked children(2), Security(3), Plan & billing(4), Language & region(5). Tab nav is `settings-tabs-nav`; tabs are `role="tab"` and also carry `settings-tab-{key}` testIDs where key ∈ `profile|notifications|linkedChildren|security|billing|language`.

## testIDs that EXIST (Phase-1 hardening + P2-12 build) — prefer these
- Shell: `settings-root`, `settings-tabs-nav`, `settings-tab-profile|notifications|linkedChildren|security|billing|language`, `settings-language-switch`, `sign-out-button` (sidebar), `theme-toggle` (Login only).
- Profile (P1-12, cross-ref only): `avatar-upload-button`, `avatar-remove-button`, `avatar-file-input`, `profile-fullname`, `profile-phone`, `profile-save`, `profile-cancel`.
- **Notifications (net-new):** `notification-weeklyReport-email`, `notification-weeklyReport-push`, `notification-streakAtRisk-email`, `notification-streakAtRisk-push`, `notification-productAnnouncement-email`, `notification-productAnnouncement-push`, `notification-achievement-email`, `notification-achievement-push` (these wrap the `Switch`; the track inside carries `role="switch"` + `aria-checked`).

## Selector GAPS (no testID exists — use role/label, and file a `frontend` ticket)
- **Notification category rows** have `accessibilityLabel={categoryLabel}` (a `group`) but **no testID** → assert switches by their 8 testIDs; assert `aria-checked` on the inner `[role="switch"]`.
- **Linked-children `ChildCard`s** render with **no `testID` / `editTestID` / `removeTestID`** — only `accessibilityLabel={child.fullName}`. The edit/remove icon buttons are `role="button"` with hard-coded EN `aria-label` `"Edit child"` / `"Remove child"` (NOT i18n-keyed — locale-stable, usable as selectors). → select cards by `getByRole('button', { name: <fullName> })` or by the `"Edit child"`/`"Remove child"` buttons.
- **Inline edit form / unlink strip / learning-language row** have no testIDs — select by their localized button labels (switch to EN first) or by `role`.
- **Session rows / plan badge / Manage CTA** have no testIDs — assert by structure/role + EN copy.

> **Backend reality (corrects some Design-Spec assumptions):** Notification prefs GET/PUT = `**/api/Notifications/Preferences**`; children list = `**/api/Parent/My-Children**`; update = `**/api/Parent/Update-Child**`; unlink = `**/api/Parent/Unlink-Child**`; sessions = `**/api/Users/Account/Sessions**`; sign-out-others = `**/api/Users/Account/Sessions/SignOutOthers**`; change-password = `**/api/Users/Account/ChangePassword**`; plan = `**/api/Users/Account/Plan**`. Use these globs for `page.route(...)` interception.

---

## A. Tab structure & switching (net-new structural surface for P2-12)

### FE-TC-01 — All six settings tabs render with the four P2-12 panels reachable
- **Type:** functional · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** seeded parent (with ≥1 child), logged in, on `/settings`.
- **Steps:**
  1. Assert `settings-root` + `settings-tabs-nav` visible.
  2. Assert `settings-tabs-nav` contains exactly 6 `role="tab"` items.
  3. For each key in `[profile, notifications, linkedChildren, security, billing, language]`: assert `settings-tab-{key}` is visible.
- **Expected:** 6 tabs present; all four new tab testIDs (`notifications`, `linkedChildren`, `security`, `billing`) exist alongside `profile` + `language`.
- **Traces to:** P2-12 epic — four remaining tabs built into the P1-11 shell. (Extends P1-11 FE-TC-51, which only counted 6 tabs.)

### FE-TC-02 — Switching to each tab swaps in the correct panel (no ComingSoon)
- **Type:** functional/regression · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** logged-in parent on `/settings`, locale = EN (switch via Language tab first for stable copy).
- **Steps:**
  1. Click `settings-tab-notifications` → panel shows a Notifications header + the email/push switch grid (assert `notification-weeklyReport-email` visible).
  2. Click `settings-tab-linkedChildren` → panel shows an Add-child CTA + child list/empty state (assert a `role="button"` with the Add-child label).
  3. Click `settings-tab-security` → panel shows the password fields + Active sessions sub-header.
  4. Click `settings-tab-billing` → panel shows a PLAN eyebrow + disabled Manage CTA.
  5. After each, assert the page does NOT contain the construction emoji `🚧` / `comingSoon` copy.
- **Expected:** each of the four tabs renders its real panel; the `ComingSoonPanel` (P1-11 FE-TC-39 fallback) is no longer reachable for these four keys.
- **Traces to:** P2-12a/b/c/d — each tab matches its capture area, not a placeholder.

### FE-TC-03 — Default tab is Profile; switching away and back preserves shell
- **Type:** state · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Steps:** Land on `/settings` (Profile default — assert `profile-save` visible) → click `settings-tab-security` → click `settings-tab-profile` → assert `profile-save` visible again, `settings-root` intact.
- **Expected:** tab state is local; switching does not unmount the shell or crash.
- **Traces to:** P1-11 shell reuse (cross-ref P1-11 FE-TC-39; net-new = asserting return-to-Profile after a P2-12 tab).

---

## B. Notifications preferences (P2-12a) — net-new, highest-value

### FE-TC-04 — Notifications panel renders the 4×2 switch grid
- **Type:** functional · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** logged-in parent on `/settings`, Notifications tab open.
- **Steps:**
  1. Click `settings-tab-notifications`; wait for the grid (loading "…" resolves).
  2. Assert all 8 switch testIDs visible: `notification-{weeklyReport|streakAtRisk|productAnnouncement|achievement}-{email|push}`.
- **Expected:** exactly 4 category rows × 2 channels = 8 switches; no missing category. Even if the BE returns empty, 4 default rows render (all OFF).
- **Traces to:** P2-12a AC — toggles per type × channel (email/push); N1 (4 rows always shown).

### FE-TC-05 — Toggles reflect saved server state on load
- **Type:** functional/persistence · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** intercept `GET **/api/Notifications/Preferences` (before navigating) to return a known mix, e.g. `weeklyReport {email:true, push:false}`, `achievement {email:false, push:true}`, others false. Envelope: `{ successed: true, data: { preferences: [...] } }`.
- **Steps:** open Notifications tab; read `aria-checked` on the inner `[role="switch"]` for each of the 8 switches.
- **Expected:** `notification-weeklyReport-email` checked=true, `notification-weeklyReport-push` false, `notification-achievement-push` true, rest false — i.e. the UI mirrors the GET payload exactly.
- **Traces to:** P2-12a AC — toggles reflect persisted prefs; N2 (load via query).

### FE-TC-06 — Toggling a switch persists via full-array PUT (optimistic)
- **Type:** functional/persistence · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** logged-in parent, Notifications tab, all switches known OFF (seed/intercept GET). Spy on `PUT **/api/Notifications/Preferences` capturing the request body (let it pass through OR fulfill 200 `{successed:true}`).
- **Steps:**
  1. Click `notification-streakAtRisk-push`.
  2. Assert the switch shows checked=true **immediately** (optimistic — before/independent of the PUT resolving).
  3. Assert a PUT to `/api/Notifications/Preferences` fired, and its body `preferences` is a **full 4-item array** (one per category 0..3), with `streakAtRisk.pushEnabled === true` and the other 3 categories present.
  4. On 200, assert a success strip appears (`role`/live-region or success copy in EN).
- **Expected:** optimistic flip + a single full-array PUT (partial-array PUTs are rejected by BE — the FE must send all 4); success strip shows then auto-dismisses (~3 s).
- **Traces to:** P2-12a AC — changes save with success feedback; N3 (optimistic, full PUT).

### FE-TC-07 — Failed PUT rolls back the optimistic toggle + shows error
- **Type:** negative/state · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** logged-in parent, Notifications tab, a known-OFF switch. Intercept `PUT **/api/Notifications/Preferences` → `400` `{ successed:false, errors:['fail'] }`.
- **Steps:**
  1. Click `notification-weeklyReport-email` → assert it flips ON optimistically.
  2. After the 400 resolves, assert the switch snaps **back** to OFF (`aria-checked=false`).
  3. Assert a `ServerErrorBanner` renders localized save-error copy (NOT a raw key like `parent.settings.notifications.saveError`).
- **Expected:** rollback to previous state + localized error banner; no success strip.
- **Traces to:** P2-12a AC — error feedback; N3 (rollback on error).

### FE-TC-08 — Switches disabled while a PUT is in flight (no double-fire)
- **Type:** boundary/state · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** intercept `PUT **/api/Notifications/Preferences` with a deliberate delay (e.g. 1500 ms before fulfilling 200).
- **Steps:** toggle one switch; while pending, assert the Switch wrappers are non-interactive (`updateMutation.isPending` sets `disabled` → wrapper `pointer-events:none` / opacity 0.4); attempt a second click on another switch and assert no second PUT fires until the first resolves.
- **Expected:** in-flight PUT disables the grid; exactly one PUT per settled toggle.
- **Traces to:** P2-12a — optimistic mutation safety (derived edge case).

### FE-TC-09 — Notifications loading state then content
- **Type:** state (loading) · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** intercept `GET **/api/Notifications/Preferences` with a ~1 s delay.
- **Steps:** open the tab; assert the centered loading text (`common.loading`, localized — not a raw key) appears, then the 8 switches materialise after the GET resolves.
- **Expected:** "Loading…" text (no spinner primitive), then the grid.
- **Traces to:** P2-12a — N2 loading state.

### FE-TC-10 — Notifications i18n: no raw keys in EN and AR
- **Type:** RTL-i18n · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Steps:**
  1. EN: open the tab, capture the panel text; assert it contains human copy (e.g. "Weekly report", "Email", "Push") and matches none of `^parent\.settings\.notifications\.` raw keys.
  2. AR (default): assert the channel eyebrows are localized full words (البريد / الإشعارات الفورية), NOT uppercased Latin "EMAIL"/"PUSH", and no raw keys appear.
- **Expected:** all labels/helpers/eyebrows localized in both locales; AR drops `textTransform:uppercase`.
- **Traces to:** P2-12a AC — render en (LTR) + ar (RTL); N4.

### FE-TC-11 — Notifications RTL: row + switch-pair flip in Arabic
- **Type:** RTL-i18n · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** locale AR (default), Notifications tab.
- **Steps:** assert `document.documentElement.dir === 'rtl'`; assert the category-label block sits on the logical-start (right) and the switch column-pair on the logical-end (left) — e.g. compare bounding-box `x` of the label text vs the `notification-weeklyReport-email` switch.
- **Expected:** in AR the switch pair is physically on the left of its label (logical-end), mirroring LTR.
- **Traces to:** P2-12a AC — RTL render; Design Spec §1 RTL.

### FE-TC-12 — Notification switch a11y: role=switch + aria-checked + composed label
- **Type:** a11y · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Steps:** for `notification-weeklyReport-email`, locate the inner `[role="switch"]`; assert it has `aria-checked` (true/false) and an `aria-label` composed as `"{category} — {channel}"` (EN: contains "Weekly report" and "Email"). Toggle and assert `aria-checked` updates.
- **Expected:** switch semantics present and reactive; label is the composed category+channel string, not a raw key.
- **Traces to:** Design Spec §0/§1 a11y.

---

## C. Linked children (P2-12b) — net-new

### FE-TC-13 — Linked-children panel lists the parent's own children
- **Type:** functional/auth-authz · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** seed a parent with a known child (`seedParentWithChild`), logged in. Linked children tab open.
- **Steps:**
  1. Click `settings-tab-linkedChildren`; wait for loading to resolve.
  2. Assert a `ChildCard` for the seeded child renders — locate by `getByRole('button', { name: <child fullName> })` OR by the card's `accessibilityLabel`.
  3. Assert the child's email shows as the card meta line (forced-LTR technical string).
- **Expected:** the panel lists exactly the parent's linked children; the seeded child appears with name + email meta.
- **Traces to:** P2-12b AC — linked-children tab lists children with status; L1/L2.

### FE-TC-14 — Empty state when the parent has no linked children
- **Type:** state (empty) · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** register a fresh parent with NO child (`registerParent`, skip add-child), logged in. OR intercept `GET **/api/Parent/My-Children` → `{successed:true, data:[]}`.
- **Steps:** open Linked children tab; assert the empty illustration + empty title/body + an Add-child primary CTA (EN copy "No children yet" / "Add Child").
- **Expected:** friendly empty state with Add-child CTA; no `ChildCard`s.
- **Traces to:** P2-12b — L6 (empty state).

### FE-TC-15 — Add-child CTA routes to the add-child flow
- **Type:** functional · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Steps:** open Linked children tab; click the Add-child CTA (header button, or empty-state button); assert the URL changes to `/add-child` and `add-child-name` is visible.
- **Expected:** navigates to `/(onboarding)/add-child` (same target as `MyChildrenWeb`).
- **Traces to:** P2-12b — L5; story "link (by email)" entry point.

### FE-TC-16 — Inline Edit form opens under the child row and validates
- **Type:** functional/validation · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** parent with ≥1 child, Linked children tab (EN locale for button copy).
- **Steps:**
  1. On the child's `ChildCard`, click the "Edit child" icon button (`aria-label="Edit child"`).
  2. Assert an inline form expands with full-name (pre-filled), grade, language, country fields.
  3. Without picking grade/language/country, assert the Save/Done primary button is **disabled** (form invalid — `isValid` requires all four).
  4. Pick grade, language, country → assert Save becomes enabled.
- **Expected:** inline form (NOT a modal) expands; Save gated on full-name + grade + language + country (grade/lang/country default empty per Q L-1).
- **Traces to:** P2-12b — L3 (inline Edit → useUpdateChild); Design Spec §2.1.

### FE-TC-17 — Edit submit persists (PUT) and shows per-row success
- **Type:** persistence · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** as FE-TC-16; spy/fulfill `PUT **/api/Parent/Update-Child` → 200 `{successed:true}`.
- **Steps:** open Edit, fill all fields, click Save; assert a PUT to `/api/Parent/Update-Child` fires with the childId + new fullName/grade/language/country; on success assert the form collapses and a per-row success strip appears (auto-dismiss ~3 s).
- **Expected:** PUT fired with correct body; form collapses; success strip shown.
- **Traces to:** P2-12b — L3.

### FE-TC-18 — Inline Unlink confirm strip; confirm calls DELETE/unlink
- **Type:** functional · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** parent with ≥1 child, Linked children tab (EN). Spy/fulfill `POST **/api/Parent/Unlink-Child` → 200 `{successed:true, data:true}`.
- **Steps:**
  1. Click the "Remove child" icon button (`aria-label="Remove child"`).
  2. Assert an inline confirm strip expands (danger-tinted) with Cancel + Unlink actions — **no modal/dialog appears**.
  3. Click Unlink; assert the unlink request fires with `{ childId }`; on success assert an unlink-success notification appears and the child block is removed/faded.
- **Expected:** confirm-before-unlink via inline strip; success removes the row.
- **Traces to:** P2-12b AC — link/unlink with confirm-before-unlink; L4.

### FE-TC-19 — Unlink "Cancel" dismisses the strip without calling the API
- **Type:** negative · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Steps:** open the unlink strip; spy on `POST **/api/Parent/Unlink-Child`; click Cancel; assert the strip closes, the child remains, and **no** unlink request fired.
- **Expected:** Cancel is a pure UI dismiss; child stays linked.
- **Traces to:** P2-12b — confirm-before-unlink safety.

### FE-TC-20 — Unlink last-parent guard: 400 keeps strip open with localized error
- **Type:** negative/auth-authz · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** intercept `POST **/api/Parent/Unlink-Child` → `400` `{successed:false, errors:['last parent']}`.
- **Steps:** open the unlink strip, click Unlink; assert the strip **stays open** and shows the localized last-parent error (`unlinkLastParentError`, not a raw key) in danger color; child not removed.
- **Expected:** 400 surfaces the last-parent guard message inline; row preserved.
- **Traces to:** P2-12b AC — family-scope/guard; Design Spec §2.2.

### FE-TC-21 — Linked-children loading + load-error states
- **Type:** state (loading/error) · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Steps:**
  1. Delay `GET **/api/Parent/My-Children` ~1 s → assert centered loading text appears, then content.
  2. (separate run) `GET **/api/Parent/My-Children` → 500 → assert the panel does not crash; `settings-root` stays mounted. (Note: the panel has no explicit error banner for the list query — assert graceful no-crash + document if children silently render empty.)
- **Expected:** loading text then content; 500 does not break the shell.
- **Traces to:** P2-12b — robustness (derived).

### FE-TC-22 — Per-child learning-language row renders current language + Change (P8-04 cross-ref)
- **Type:** functional · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions:** child seeded with `learningLanguage` (e.g. `ar`). Linked children tab.
- **Steps:** under the child's card, assert a learning-language row shows the row label + current language label (e.g. "Arabic"/"العربية") and a "Change" button.
- **Expected:** the P8-04 learning-language row appears per child with the current language + Change affordance.
- **Traces to:** P8-04-FE (learning language) — cross-referenced; net-new only insofar as it lives in this panel. Deep change-language flow is P8-04's own QC scope — assert presence only here.

### FE-TC-23 — Linked-children RTL: child names Arabic, email stays LTR
- **Type:** RTL-i18n · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Steps:** AR locale; assert `dir=rtl`; assert the email meta line on a card renders LTR (computed `direction: ltr` on the email text) even inside the RTL card; card actions on logical-end.
- **Expected:** RTL layout with forced-LTR email (technical string).
- **Traces to:** Design Spec §2 RTL.

### FE-TC-24 — IDOR-ish: parent sees ONLY their own children
- **Type:** auth-authz · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** seed two distinct parents A and B, each with a distinct child (A→childA, B→childB). Log in as parent A.
- **Steps:** open Linked children tab; assert childA appears and childB's name does **not** appear anywhere in the panel.
- **Expected:** family-scoped — `GET My-Children` returns only the acting parent's children; cross-parent child is never shown.
- **Traces to:** P2-12b AC — family-scope enforced (a parent only manages their own children); product: parent-driven.

---

## D. Security (P2-12c) — thinner here; deeper auth flows are P2-12c's own QC scope

### FE-TC-25 — Security panel renders change-password form + sessions section
- **Type:** functional · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Steps:** open `settings-tab-security`; assert 3 password fields (current/new/confirm — `secureTextEntry`, type=password), a strength meter, an Update-Password button, and an Active-sessions sub-header with a Sign-out-others button.
- **Expected:** both sub-sections render inside one panel separated by a divider.
- **Traces to:** P2-12c AC — change password + sessions; S1/S3.

### FE-TC-26 — Client-side validation: mismatch + same-as-current disable submit
- **Type:** validation · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Steps (EN):** type current `Str0ng!Pass1`; new `NewPass1!`; confirm `Different1!` → assert a localized mismatch hint shows and Update-Password is disabled. Then set confirm = new but new = current → assert same-as-current hint shows and Save disabled.
- **Expected:** localized inline hints (not raw keys); Save gated until valid.
- **Traces to:** P2-12c AC — strength rules / validation; Design Spec §3 states.

### FE-TC-27 — Password fields are forceLtr in Arabic
- **Type:** RTL-i18n · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Steps:** AR locale, Security tab; assert each password field's computed `direction` is `ltr` even with `dir=rtl`.
- **Expected:** passwords LTR regardless of locale.
- **Traces to:** P2-12c — S5; SKILL.md.

### FE-TC-28 — Change-password success clears form + invalidates sessions
- **Type:** functional/persistence · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** fulfill `POST **/api/Users/Account/ChangePassword` → 200 `{successed:true}`; spy on `GET **/api/Users/Account/Sessions` (expect a refetch after success).
- **Steps (EN):** fill valid current/new/confirm, submit; assert the 3 fields clear, a success strip shows, and a sessions GET re-fires (cache invalidated).
- **Expected:** form cleared, success strip, sessions refetched.
- **Traces to:** P2-12c AC — on password change other sessions invalidated; S2.

### FE-TC-29 — Change-password wrong-current maps to localized error
- **Type:** negative · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** intercept `POST **/api/Users/Account/ChangePassword` → `400` `{successed:false, errors:['current password incorrect']}`.
- **Steps (EN):** fill valid-looking values, submit; assert a `ServerErrorBanner` with the localized wrong-current message (not a raw key); fields stay populated.
- **Expected:** localized error; no form clear.
- **Traces to:** P2-12c — S2 error path.

### FE-TC-30 — Active sessions list renders truncated id + status pill
- **Type:** functional · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions:** intercept `GET **/api/Users/Account/Sessions` → `{successed:true, data:[{sessionId:'a3f1e0c2deadbeef', isActive:true, expiresAt:'2026-12-01T00:00:00Z'}, {sessionId:'ffffffff00000000', isActive:false, expiresAt:'2025-01-01T00:00:00Z'}]}`.
- **Steps:** open Security tab; assert two session rows; first shows `a3f1e0c2…` + Active pill, second shows Expired pill; assert the id text renders LTR.
- **Expected:** truncated-8 id + Active/Expired pills; ids forced LTR.
- **Traces to:** P2-12c — S3; Design Spec §3.

### FE-TC-31 — Sessions empty state (defensive)
- **Type:** state (empty) · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions:** intercept `GET **/api/Users/Account/Sessions` → `{successed:true, data:[]}`.
- **Steps:** open Security tab; assert the "No active sessions" centered text (localized, not a raw key).
- **Expected:** empty-sessions copy; no crash.
- **Traces to:** Design Spec §3 states.

---

## E. Plan & billing (P2-12d) — net-new, read-only stub

### FE-TC-32 — Plan panel renders plan name + status pill + disabled Manage CTA
- **Type:** functional · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Preconditions:** intercept `GET **/api/Users/Account/Plan` → `{successed:true, data:{planName:'Free', status:'Active'}}`.
- **Steps (EN):** open `settings-tab-billing`; assert a PLAN eyebrow, plan name "Free", an "Active" success-styled pill, body copy, and a Manage-Subscription button that is **disabled** (`aria-disabled=true` / no press effect).
- **Expected:** read-only plan view with disabled Manage CTA (TODO(P2-12-PAYMENTS)).
- **Traces to:** P2-12d AC — current plan + status + manage CTA (read-only).

### FE-TC-33 — Manage CTA is non-interactive (no navigation/no crash)
- **Type:** negative · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Steps:** force-click the disabled Manage button; assert no navigation occurs and `settings-root` stays mounted.
- **Expected:** disabled CTA is a no-op.
- **Traces to:** P2-12d — payments out of scope.

### FE-TC-34 — Plan loading + error states
- **Type:** state (loading/error) · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Steps:** (a) delay `GET **/api/Users/Account/Plan` → assert centered loading text then content. (b) `GET ...Plan` → 500 → assert a `ServerErrorBanner` renders (localized) and the panel does not crash.
- **Expected:** loading text then plan; 500 → localized banner.
- **Traces to:** Design Spec §4 states.

### FE-TC-35 — Plan-name localization in Arabic (Free → مجاني)
- **Type:** RTL-i18n · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions:** `GET ...Plan` → `{planName:'Free', status:'Active'}`; AR locale.
- **Steps:** open Billing tab; assert the plan name renders the AR display label (`مجاني`), the PLAN eyebrow is the Arabic word (no Latin uppercase), and no raw `parent.settings.billing.*` keys appear.
- **Expected:** FE localizes the Latin wire value; eyebrow drops uppercase in AR.
- **Traces to:** P2-12d AC — render en/ar; Design Spec §4 / DG-W10-04.

---

## F. Cross-cutting

### FE-TC-36 — Settings RTL default holds across the four new tabs
- **Type:** RTL-i18n · **Priority:** P1 · **Target:** frontend-e2e-tester
- **Steps:** AR (default); for each of `notifications, linkedChildren, security, billing`, click the tab and assert `document.documentElement.dir === 'rtl'` and the panel header text aligns right (no LTR leakage / no layout overflow at desktop width).
- **Expected:** all four panels render RTL with no horizontal overflow.
- **Traces to:** P2-12 epic — render en/ar across the four tabs (extends P1-11 FE-TC-52).

### FE-TC-37 — Signed-out access to /settings redirects to Login
- **Type:** auth-authz · **Priority:** P0 · **Target:** frontend-e2e-tester
- **Preconditions:** no auth (fresh context / clear storage).
- **Steps:** `page.goto('/settings')`; assert the routing guard redirects to `/login` (`login-username` visible) and no settings panel renders.
- **Expected:** unauthenticated users cannot reach the parent settings surface.
- **Traces to:** Cross-cutting auth routing (cross-ref P1-11 FE-TC-01/17; net-new = the `/settings` deep-link).

### FE-TC-38 — Theme switch reflects on a P2-12 panel (cross-ref P1-11, thin)
- **Type:** state · **Priority:** P2 · **Target:** frontend-e2e-tester
- **Preconditions:** `theme-toggle` only exists on Login — toggle theme there, then login and open a P2-12 tab.
- **Steps:** set theme on Login → login → open Notifications tab → assert the panel renders under the chosen theme (dark default; if light was chosen, assert a light-surface attribute/class differs). If the toggle is a known E2E no-op (see P1-11 FE-TC-06), assert presence/no-crash and document.
- **Expected:** theme choice carries into settings panels; otherwise documented as the existing P1-11 limitation.
- **Traces to:** P1-11 shell theme (cross-ref; not re-verifying the toggle mechanics).

---

## BLOCKED / not-yet-testable (scaffold as `test.skip` with reason)

### FE-TC-39 — Sign-out-others count message (BLOCKED — needs ≥2 real sessions)
- **Reason:** asserting the "Signed out {count} other sessions" copy with a correct count requires seeding multiple real backend sessions for one user (a second login/device), which the harness does not currently create deterministically. Intercepting only fakes the list, not the count semantics. Scaffold skipped; revisit when a multi-session seed helper exists.
- **Traces to:** P2-12c — S4.

### FE-TC-40 — Notification row shake-on-rollback motion (BLOCKED — motion not assertable)
- **Reason:** the 60 ms ±6 px error shake (Design Spec §1 motion) is a transient animation with no stable DOM signal in Playwright; the rollback itself is covered by FE-TC-07. Scaffold skipped.
- **Traces to:** Design Spec §1 motion.

### FE-TC-41 — Locale-aware AR fonts (Cairo/Tajawal) on the four panels (BLOCKED — DG-W10-01)
- **Reason:** Design gap DG-W10-01 flags that `$heading`/`$body` may still resolve to Poppins for AR until locale-aware font config ships. Asserting the rendered font-family for AR is unreliable until that gap is resolved; track as a design defect, not an E2E pass/fail. Scaffold skipped with the DG-W10-01 reference.
- **Traces to:** Design Spec §6 DG-W10-01.
