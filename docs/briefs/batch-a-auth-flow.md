# Pipeline Brief — Batch A · Auth flow redesign (Splash → Role Select → Login)

## Summary & traceability
- **Task (1 line):** Replace the student-app's inline parent/student login toggle with a 3-step pre-login flow — **Splash → Role Select → Login** — where `role` is chosen on a new Role Select screen and carried into a restyled Login that shows it as a read-only "Signing in as … · Change" badge.
- **Epic:** 4-batch design-driven `student-app` auth/parent epic. **This is Batch A (auth flow only).** Batch B = web-responsive layout, Batch C = parent-mobile native shell, Batch D = (later). Keep Role Select + role-badge components reusable for both form factors, but do NOT build the parent shell or responsive dashboard here.
- **Design source of truth (handoff Update 2):** `design-system/design_handoff_parent_app_and_auth/README.md` §"Update 2 — CHANGED: Auth flow". This is a **design-system handoff**, not a numbered user story; there is no `user-stories/<phase>/` file or `tasks/` file for it. **OPEN QUESTION Q1** records this gap. The closest prior story is **P1-11** (shared Login pixel pass) / **P1-12** (Google OAuth) — this batch *amends* their output.
- **Product rule (authoritative, CLAUDE.md):** Parent-driven onboarding — parents register, **students never self-register**. The Role Select copy and the student-login state must reflect this.
- **Build contract:** RTL via native `dir` + natural source order (NO `flexDirection:'row-reverse'`); LTR islands only for technical strings; build from JSX/HTML kits + `preview/*.html` (PNGs are outputs); acceptance split into a **visual gate** and a **functional gate**; no new design pattern (state machine / provider) without lead sign-off.

### Canonical design references (read these — not screenshots)
| Screen | Native JSX | Web JSX | Preview HTML (EN / AR) |
|---|---|---|---|
| Splash | `PMSplashScreen` (PMScreens.jsx) | `SplashWebPage` (PagesPublic.jsx) | `screen-pmen-00-splash.html` / `screen-pmar-00-splash.html`, `screen-wen-00-splash.html` / `screen-war-00-splash.html` |
| Role Select | `PMRoleSelectScreen({onPick})` | `RoleSelectWebPage({onPick})` | `screen-pmen-00b-role.html` / `screen-pmar-00b-role.html`, `screen-wen-00b-role.html` / `screen-war-00b-role.html` |
| Login | `PMLoginScreen({role,onBack,onLogin,onRegister})` | `LoginWebPage({role,onBack,...})` | `screen-pmen-07-login.html` / `screen-pmar-*`, `screen-wen-…` |
| Register (unchanged behavior) | `PMRegisterScreen` | `RegisterWebPage` | `screen-pmen-08-register.html` |

Tokens: `design-system/design_handoff_parent_app_and_auth/colors_and_type.css` (Primary `#4F46E5`, bg `#0F172A`, card `#1E293B`, warning `#F59E0B`, fg `#F8FAFC`, muted `#94A3B8`, faint `#64748B`).

---

## Business context & value
- **Who benefits:** Parents and students entering the app; the platform's COPPA posture (no child self-registration).
- **Value:** (1) Removes ambiguity at login — the user declares *who they are* on a dedicated screen instead of a cramped inline toggle. (2) Makes the "kids can't self-register" rule visible and unmissable (Role Select footnote + student-login amber notice) rather than buried. (3) Aligns the student-app login visuals with the rest of the design system (purple glow, 🌟 brand mark) for a consistent first impression across web + native.
- **Success measure:** A user can pick a role and reach a login styled for that role; the student path never exposes a register affordance; the flow works identically in EN and AR, on web and native, without the native "navigate before mounting" crash.

---

## Acceptance criteria

### Visual gate (matches the design — checked against `preview/*.html` + JSX kits, both EN + AR)
- **V1. Splash** unchanged in intent but is the entry point: 🌟 brand mark, purple radial glow, wordmark "Learnexia" (always Latin/LTR), subtitle, loader. (Current `app/index.tsx` already matches `01-splash`; no visual change required beyond it remaining the boot screen.)
- **V2. Role Select** matches `PMRoleSelectScreen` / `RoleSelectWebPage`: 🎮 header emoji, "Welcome to Learnexia" (H ~26 native / ~30 web), "Who's signing in?" subtitle, **two cards** stacked — 👨‍👩‍👦 Parent ("Track your child's progress") and 🎓 Student ("Learn, play, level up") — each a rounded `#1E293B` card with a 56–64px icon tile (`rgba(79,70,229,0.18)`), label, sub, and a trailing chevron (`›`, flips in RTL). Footnote (faint `#64748B`): "Children log in with the email a parent assigned — they never create their own account."
- **V3. Login header** restyled to the student-mobile look: 72px purple-gradient brand tile with 🌟, "Welcome back", and **role-conditional subtitle** — student: "Log in to keep your streak alive 🔥"; parent: "Sign in to follow your children's progress". Purple top-glow radial behind the header.
- **V4. Role badge** replaces the toggle: a centered pill (`#15161D`/`#1E293B`, hairline border) showing the role emoji + "Signing in as Parent"/"Signing in as Student" + a **"Change"** text link. The inline `PersonaToggle` is gone from the login screen.
- **V5. Student login** shows **no register link**; instead an amber notice (`rgba(245,158,11,0.08)` bg, `#F59E0B` accent): "Need an account? Ask a parent to add you."
- **V6. Parent login** keeps the register link ("New to Learnexia? Create parent account") exactly as today.
- **V7. Email/password fields, Remember me, Forgot password, OR divider, Google/Apple/Microsoft social row** all preserved (they already match P1-11/P1-12).
- **V8. RTL (AR):** every screen mirrors via `dir`/natural order — chevrons flip, badge + cards mirror, the brand wordmark stays Latin/LTR, the email placeholder stays an LTR island. No double-flip.

### Functional gate (routing/auth works against real endpoints)
- **F1.** Signed-out boot → Splash → guard redirects to **Role Select** (NOT directly to login). The `useRootNavigationState()?.key` mount guard in `useAuthRoute.ts` is preserved (no native "navigate before mounting" crash).
- **F2.** Picking **Parent** routes to Login with `role=parent`; picking **Student** routes to Login with `role=student`. `role` survives the navigation (see role-state model below).
- **F3.** Login "Change" returns to Role Select (and the previously-chosen role no longer leaks into the next selection).
- **F4.** Submitting valid credentials authenticates against the real sign-in endpoint and hands off to `router.replace('/')` → `useAuthRoute` resolves `Me` and routes by **server role** (student→`(child)`, parent→onboarding/`(parent)`). The chosen UI `role` is a *hint only* — the backend role from `Me` is authoritative; a mismatch (e.g. a student account picking "Parent") must still route per the real role and must NOT grant parent surfaces.
- **F5.** Google OAuth still works on both role paths (same `useGoogleSignIn` → `setTokens` → `/` path).
- **F6.** No student self-registration path exists or is reachable from any screen.
- **F7.** Locale (en/ar) and theme controls still function on Login; deep-link / refresh on `/(auth)/login` without a chosen role degrades gracefully (see Q3).

---

## Affected modules & data
Frontend-only (`apps/student-app` + `packages/shared`, `packages/ui`). **No backend, no DB, no new entities.** The auth endpoints (sign-in, Google sign-in, `Me`) are unchanged. `role` is **ephemeral client UI state**, never persisted to the server and never sent in the auth request.

### Role-state model (how `role` flows Splash → Role Select → Login → useAuthRoute)
- `role` is chosen on **Role Select** and must reach **Login**. Recommended mechanism: **expo-router route param** (`router.push({ pathname:'/(auth)/login', params:{ role }})`, read via `useLocalSearchParams`). This is the lightest mechanism, mirrors existing `router.push`/`useRouter` usage, survives the web URL, and needs no new store. **Default when absent:** `parent` (matches today's `useState(LOGIN_PERSONAS.Parent)` default and the kit's `role='parent'` default).
- `role` is a **UI hint only** — it drives copy/badge/register-link visibility. It must NOT be conflated with the authenticated role from `Me`; `useAuthRoute` already routes by `Me.roles` and that stays the source of truth (F4).
- The existing `LOGIN_PERSONAS` const + `LoginPersona` type already model exactly `parent | student` — **reuse them**; do not introduce a new role enum.
- **Avoid** adding a Zustand store or a state machine for this 2-value hint unless the param approach proves insufficient (flag as Q2 / pattern-approval if so).

---

## Handoff → db-migration
**None.** Batch A is frontend-only; no schema, no migration, no seed. (Skip this agent in the plan.)

## Handoff → backend-feature
**None.** No new commands/queries/endpoints/DTOs. Existing `useSignIn`, `useGoogleSignIn`, `useMe` are consumed as-is. (Skip this agent; therefore **no `api-tester` stage** for Batch A.)

## Handoff → frontend (the core of this batch)

### Files to CREATE
| File | Purpose |
|---|---|
| `apps/student-app/app/(auth)/role-select.tsx` | New Role Select screen (the new pre-login default for signed-out). Renders the two role cards + footnote; on pick → `router.push({pathname:'/(auth)/login', params:{role}})`. |
| `apps/student-app/app/(auth)/_components/RoleCard.tsx` | Reusable role card (icon tile + label + sub + chevron). Built form-factor-agnostic so Batches B/C reuse it. |
| `apps/student-app/app/(auth)/_components/RoleBadge.tsx` | Reusable "Signing in as … · Change" pill (emoji + label + Change link). Reused by login on web + native. |

(If the designer prefers, `RoleCard`/`RoleBadge` may live in `packages/ui` instead of `(auth)/_components` for cross-surface reuse — **designer/lead to decide**, Q4.)

### Files to CHANGE
| File | Change |
|---|---|
| `apps/student-app/src/hooks/useAuthRoute.ts` | Signed-out target `'/(auth)/login'` → **`'/(auth)/role-select'`**. **Preserve** the `navReady = Boolean(rootNavState?.key)` guard and the `current !== '(auth)'` group check (role-select is in the same `(auth)` group, so the existing group guard still holds — verify the redirect target string and the doc comment block at top). Update the docstring "signed-out → /(auth)/login" to role-select. |
| `apps/student-app/app/(auth)/_layout.tsx` | Add `<Stack.Screen name="role-select" />`. Keep `headerShown:false`, `animation:'fade'`. Update the docstring (it currently asserts "Only two routes exist here"). Still NO student self-register route. |
| `apps/student-app/app/(auth)/login.tsx` | Read `role` from `useLocalSearchParams` (default `parent`). Pass `role` + an `onBack` (→ `router.replace('/(auth)/role-select')`) into `LoginForm` (or render the badge here above `<LoginForm/>`). Render `RoleBadge`. Make the register-link block **conditional on role==='parent'**; for student render the amber "Ask a parent to add you" notice. Adjust subtitle to role-conditional copy. |
| `apps/student-app/app/(auth)/_components/LoginForm.tsx` | **Remove** `PersonaToggle` usage + the `persona`/`setPersona` `useState` + the import. Accept `role` (and optionally `onBack`) as a prop instead of internal toggle state. Everything else (RHF, zod, Google OAuth, error banners, social row, remember-me, forgot-password) stays. The student vs parent register affordance lives in `login.tsx` (V5/V6). |
| `apps/student-app/app/(auth)/_components/PersonaToggle.tsx` | **DELETE** (no longer used). Confirm no other importer via grep before deleting. |
| `apps/student-app/app/index.tsx` | No structural change required (splash already correct). Verify the splash still mounts `useAuthRoute` and that the new role-select redirect lands correctly. |
| `packages/shared/src/i18n/resources.ts` | Add the NEW keys below under `auth` (both `en` and `ar` blocks). Decide whether to retire the now-unused `auth.login.personaParent/personaStudent/personaToggleLabel` keys (Q5). |

### NEW i18n keys (add to BOTH en + ar)
Proposed namespace `auth.roleSelect` + additions to `auth.login`. Existing reusable keys: `auth.login.title` ("Welcome back"), `auth.login.createAccount`, `auth.login.newParent`, all field/social keys. NEW:
- `auth.roleSelect.title` — EN "Welcome to Learnexia" / AR "مرحبًا بك في Learnexia"
- `auth.roleSelect.subtitle` — EN "Who's signing in?" / AR "من يقوم بتسجيل الدخول؟"
- `auth.roleSelect.parentLabel` / `auth.roleSelect.parentSub` — "Parent" / "Track your child's progress"
- `auth.roleSelect.studentLabel` / `auth.roleSelect.studentSub` — "Student" / "Learn, play, level up"
- `auth.roleSelect.footnote` — "Children log in with the email a parent assigned — they never create their own account."
- `auth.login.signingInAs` — "Signing in as {{role}}" (or two literal keys `signingInAsParent`/`signingInAsStudent` to avoid interpolated-role gender/RTL issues — **designer to pick**)
- `auth.login.roleParent` / `auth.login.roleStudent` — "Parent" / "Student" (badge nouns)
- `auth.login.change` — "Change"
- `auth.login.subtitleParent` — "Sign in to follow your children's progress" (keep existing `subtitle` for the student variant, or add `auth.login.subtitleStudent`)
- `auth.login.studentNoAccountTitle` — "Need an account?"; `auth.login.studentNoAccountBody` — "Ask a parent to add you."
- `auth.roleSelect.parentA11y` / `auth.roleSelect.studentA11y` — accessibility labels for the role cards.

(AR strings: Cairo headings / Tajawal body per handoff; Eastern-Arabic numerals in prose, Latin for technical strings; brand "Learnexia" stays Latin.)

### Existing reusable UI primitives
`packages/ui` exports used in auth today: `Button`, `TextField`, `Card`, `GradientBox`; `loginParts.tsx` (`Checkbox`, `OrDivider`, `SocialButton`, `SocialRow`); `SocialIcons.tsx`; `FormScaffold`/`LoginBrandPanel`/`LocaleThemeControls`. Reuse these; do not re-implement.

---

## Open questions / assumptions / risks

### Open questions for the lead (resolve before/early in planning)
- **Q1 (traceability gap):** There is no `user-stories/` or `tasks/` file for this auth-flow change — it lives only in the design handoff. Confirm Batch A should proceed off the handoff as source-of-truth, or whether a story/task should be authored first. *(Assumption: proceed off handoff + this brief.)*
- **Q2 (role transport / pattern):** Recommended mechanism is an expo-router **route param** (no new store, no state machine). Confirm acceptable. If the lead wants a store/state-machine instead, that is a **new design pattern → requires explicit approval** (CLAUDE.md rule 8).
- **Q3 (deep-link / no-role):** If a user lands on `/(auth)/login` directly (refresh, bookmarked web URL, logout returning mid-flow) with no `role` param — default to `parent`, or redirect back to role-select? *(Assumption: default `parent`, matching today's default and the kit.)*
- **Q4 (component home):** Should `RoleCard`/`RoleBadge` live in `apps/student-app/app/(auth)/_components` (story-local) or `packages/ui` (cross-surface, since Batches B/C reuse them)? *(Assumption: start in `(auth)/_components`, promote to `packages/ui` if Batch B/C need it.)*
- **Q5 (dead i18n keys):** Retire `auth.login.personaParent/personaStudent/personaToggleLabel` now (toggle is gone) or leave them? *(Assumption: remove with the PersonaToggle deletion to avoid orphan keys.)*
- **Q6 (logout target):** Handoff says "Logout returns to splash." Today logout → signed-out → guard. With F1 the guard now sends signed-out users to **role-select** (after the splash boot). Confirm logout should land on role-select (via splash), not login. *(Assumption: yes — splash → role-select.)*

### Assumptions
- Splash visuals need no change; only the *next* destination changes (login → role-select).
- The chosen `role` never affects which credentials are accepted — the backend authoritatively assigns the post-login surface via `Me.roles` (F4). The UI role is presentation-only.
- Email/password validation, anti-enumeration error handling, lockout/deactivated messaging, and the Google OAuth flow are all carried over unchanged from P1-11/P1-12.

### Risks
- **R1 (native mount crash — highest):** `useAuthRoute.ts` was just fixed to gate redirects on `useRootNavigationState()?.key`. Adding a new pre-login screen and changing the redirect target must NOT regress this. The new `role-select` is inside the existing `(auth)` group, so the `current !== '(auth)'` guard keeps redirects idempotent — verify this holds and don't add navigation outside the gated effect.
- **R2 (expo-router group routing):** `role-select` must be registered in `(auth)/_layout.tsx` and be the redirect target; ensure `app/(auth)/role-select.tsx` exists before changing the guard or the redirect 404s on native. Web URL becomes `/role-select`.
- **R3 (web vs native parity):** Web uses a split-panel `FormScaffold`; native uses a single column. Role Select in the web kit is a single centered 460px column (NOT split-panel) over `#0F172A` with a top glow — don't force it into the login split-panel scaffold. RoleCard/RoleBadge must render correctly in both. (Full web-responsive polish is Batch B — keep Batch A's web rendering functional, not pixel-final for responsive tiers.)
- **R4 (RTL double-flip):** Established bug — use `dir`/natural order only; LTR islands for email + brand wordmark. The role badge and cards must not use `flexDirection:'row-reverse'`.
- **R5 (role leak on "Change"):** Returning from login to role-select then re-picking must produce a clean param; use `router.replace` for the back path so the param updates rather than stacking.
- **R6 (orphaned imports):** Deleting `PersonaToggle` — grep for all importers (currently only `LoginForm.tsx`) before removal; also check `LOGIN_PERSONAS`/`ALL_LOGIN_PERSONAS` usages so the const isn't left dangling unintentionally.

---

## Recommended pipeline order (first cut — `planner` finalizes)
Frontend-only, single story. No db-migration / backend-feature / api-tester / security-auditor batches needed (no data, no auth-contract change — though logout/role-routing touches auth UX, the *contract* is unchanged; lead may still opt a light security pass on F4 to confirm the UI role can't escalate surfaces).

1. **designer** — Design Spec for Splash (carryover), **Role Select**, restyled **Login + RoleBadge**, EN + AR, from the JSX/HTML kits + tokens. Decide component home (Q4), the `signingInAs` key shape, and the no-role default (Q3). → `design-system/ui_kits/<surface>/batch-a-auth-flow.md`.
2. **frontend** (after design spec) — implement in dependency order within the batch:
   - 2a. i18n keys (en+ar) + `RoleCard`/`RoleBadge` components (no dependents block them).
   - 2b. `role-select.tsx` + `_layout.tsx` registration.
   - 2c. `login.tsx` + `LoginForm.tsx` edits + `PersonaToggle` deletion.
   - 2d. `useAuthRoute.ts` redirect change (last, so the new screen exists first — mitigates R2).
3. **frontend-e2e-tester** (after frontend) — Playwright on the running web PWA: full **splash → role-select → login → app** flow for **both roles** in **en + ar**; assert: parent shows register link, student shows the amber notice + no register link, "Change" returns to role-select, valid login routes by real role, RTL has no double-flip, no native-style mount errors on web.
4. **reviewer** — gate against the visual + functional ACs above and CONVENTIONS.md (incl. RTL rule, no-new-pattern rule, e2e results). Then **committer** on `feat/<StoryID-or-slug>-auth-flow` with a PR.

Blockers: Q1/Q2 should be answered before frontend starts (they decide whether the param approach stands and whether a story file is authored).
