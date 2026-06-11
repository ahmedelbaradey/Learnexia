# Pipeline Brief — Parent Dashboard (web) UI/UX fix + redesign

## Summary & traceability

**One-line task:** A six-workstream UI/UX hardening + redesign of the parent web dashboard in `apps/student-app/app/(parent)/` — a persistent locale/theme switcher + brand scrollbar + RTL fixes in the parent shell, post-login redirect to Overview, pixel passes on Overview and Settings, an Add-Child modal, and a reproducible two-child verification plan.

**Source of truth:** There is **no formal user-story file** for this work. **This spec + the in-repo `design-system/` folder (current on `main`) are the source of truth.** Where the older align docs (`align-overview.md`, `align-settings.md`) conflict with the current `design-system/ui_kits/parent-dashboard/index.html` / `index-ar.html` / preview HTML, **the index/preview HTML wins** (it is the latest Claude Design export; the align docs predate the side-by-side Overview redesign — see workstream C).

**Related history (not authoritative for scope):** P1-11 (parent dashboard shell), P1-12 (account profile + avatar upload), P2-12 (settings tabs), P8-04 (learning-language change). The `api.anthropic.com` design handoff URL is byte-identical to the in-repo `design-system/` — do **not** fetch it; read the repo.

**Product-decision alignment (CLAUDE.md):** parent-driven onboarding (no student self-register); 4 subjects (Math/Science/Arabic/English, no Social Studies); no teacher role; grade transition preserves history. All six workstreams respect these.

**Pipeline:** analyzer → designer → frontend → frontend-e2e-tester → reviewer → committer. (No db-migration / backend-feature batch — this is FE-only. See workstream D + F for the one backend touch-point that is *investigated, not changed*.)

---

## Business context & value

The parent web dashboard is the parent's primary surface (Math/Science/Arabic/English progress, settings, child management). Today it has concrete UX breaks that erode trust in a paid product: the EN/AR + dark/light switcher exists only on `/login` (a signed-in Arabic-first parent is stranded in whatever locale they landed on), pages can clip/dead-scroll, the RTL sidebar order is wrong, post-login lands on a child-list instead of the at-a-glance Overview, the profile photo upload is broken, and Add-Child is a full-screen form instead of the designed modal. Fixing these makes the dashboard match the shipped design system and feel like one coherent, bilingual product. Beneficiary: **parent** (admin treated as parent for this app). Success = every parent page matches the `design-system/` reference in EN/LTR and AR/RTL, scrolls cleanly, and the locale/theme choice persists everywhere.

---

## Acceptance criteria (testable, per workstream)

### A. Global parent shell — persistent locale/theme switcher + brand scrollbar + RTL order
- A persistent **EN/AR language + dark/light theme switcher** is visible and functional on **every** parent web page (Overview, My Children, Reports, Settings, and the `index.tsx` landing) — not only `/login`. It mirrors the marketing/login switcher behavior: persists the choice, flips `dir`/RTL app-wide, swaps Cairo/Tajawal fonts in AR.
- Switching locale or theme from any parent page updates the whole app immediately on web (no reload), persists across hard reload and route changes (locale already persists via `localStorage` `lx_locale`; **theme does not persist today — see Open Questions Q-A2**).
- The app-wide **brand scrollbar** (SKILL.md Skill 9: indigo-gradient pill thumb, 10px, vertical + horizontal, lighter on hover, `rgba(255,255,255,0.03)` track) is applied to the web app, matching the `index.html` `*::-webkit-scrollbar*` block.
- **Every** parent page actually scrolls — no clipped content, no dead scroll region. The page content scrolls within the content column next to the fixed-height sidebar.
- **RTL sidebar/menu order:** in Arabic the nav order puts **"نظرة عامة" (Overview) BEFORE "أطفالي" (My Children)**. Menu icon positions/alignment are correct in RTL (icon on the logical start, label reads right-to-left, no double-flip).
- Tokens only; RTL via logical props / `dir`; no raw hex.

### B. Routing — post-login lands on Overview
- After a parent with children signs in (or resolves on app start), they land on **`/(parent)/overview`**, not `/(parent)` (which renders the `index.tsx` placeholder) and not `/(parent)/children`.
- Parent-with-no-children still routes to `/(onboarding)/add-child` (unchanged). Student → `/(child)` (unchanged). Signed-out → `/(auth)/login` (unchanged).
- The exact change is in **`useAuthRoute.ts`** (and mirrored in **`useGroupGuard.ts`** if it hard-codes a target). See handoff.

### C. Overview page — hard UI/UX pass + side-by-side panels
- Overview matches `align-overview.md` (the 58 deltas) **and** the current `index.html` dashboard layout.
- Bucketed rounded corners throughout (8 chips/inputs, 16 buttons, 20 cards, 24 modals, pill HUD) — no raw 90° corners.
- All sections present per `index.html` — including the **"Recommendations from Lexi"** panel, which the live `OverviewWeb.tsx` is currently **missing** (it renders only `FocusAreasCard`). This supersedes `align-overview.md` "Intentional Deviation #4," which said not to add it — the current `index.html` (lines 136-150) now renders it.
- **"Areas to focus on" + "Recommendations from Lexi" sit SIDE-BY-SIDE in ONE row** — a 2-column grid (`gridTemplateColumns: '1fr 1fr', gap: 20, alignItems: 'start'` per `index.html` line 136); stacks to one column on narrow widths.
- RTL/AR correct (sidebar on right, bars stay LTR, Eastern-Arabic numerals, `٪` not `%`, Cairo/Tajawal fonts).

### D. Settings page — hard UI/UX pass
- Settings matches `align-settings.md`.
- **Linked-children section** (the Linked Children tab) has correct rounded corners and no missing parts per the spec.
- The **parent profile-image upload is fixed**: clicking "Upload a photo" opens a file picker, the chosen image previews on the avatar, and (web) uploads via the existing `useUploadAvatar` hook; remove works via `useRemoveAvatar`. (See Open Questions Q-D1 — the upload code already exists; the brief calls for confirming/fixing why it appears broken, e.g. avatar URL not rendering or BE endpoint state.)
- **Email stays READ-ONLY** with a **clear disabled-state reason** shown to the user (helper/tooltip text), and **no edit flow** is added. The reason: email is the login identity and the account-profile contract has no email field and no change-email endpoint (investigation result below).
- The **"اللغة والمنطقة" (Language & Region)** section gains a **Save button**. It persists the UI language to the backend (`User.PreferredLanguage` via `useUpdateUserLanguage` — already wired on *change* today; the redesign moves persistence behind an explicit Save) and the region (region persistence has no endpoint yet → UI-only or flagged — see Q-D2).
- Tokens only; RTL/AR correct.

### E. Add Child — modal/popup
- Adding a child opens a **centered modal over a blurred scrim on web** (bottom sheet on mobile — out of scope here unless trivially shared), per `AddChildModal.jsx` + `web-add-child-modal.html`.
- The modal contains: **photo upload** (circular avatar + 📷 badge + dashed "Upload a photo" drop-zone, live preview via `URL.createObjectURL`, color-initial fallback), child name, login email, **grade as 6 plant-emoji TILES** (🌱🌿🌳🌲🍃🌴 grades 1-6; selected tile gets the Level-Up gradient; **never a `<select>`**), **language as two FLAG tiles** (🇪🇬 AR / 🇺🇸 EN; selected gets indigo border + tint). Footer: Cancel (ghost) + personalized primary CTA ("Add Layla →" / "أضف ليلى ←").
- Submits via the existing `useAddChild` hook to `POST /api/Parent/Add-Child` (fields: fullName, email, password, grade, language, learningLanguage, country). **Note:** the current page form also collects **password, learningLanguage (axis B), and country** — the modal design HTML omits these. The modal must still satisfy the backend contract (which requires all of them). See Q-E1.
- It **replaces** the relevant current add-child entry surface(s). Entry-point inventory + which become the modal are below.
- Bucketed radii, tokens only, RTL/AR (modal `dir="rtl"`, Cairo/Tajawal, flag tiles, CTA arrow flips to ←).

### F. Verification plan
- After the build, **two children with full, distinct data** are added to `parent@demo.com` via the API (backend on `:5080`), then parent **child-to-child switching** and **per-child data preview** are verified in the browser.
- The brief documents the exact endpoints/payloads (Register-Parent already done; Add-Child; how child progress data is represented) so the step is reproducible. **Critical caveat:** real per-child XP/streak/mastery/activity is **Phase-5 and not yet seedable via any endpoint** — the dashboard renders **deterministic per-child stubs** (`parentDashboardStubs.ts`) keyed on child id, so two children with different ids already show distinct XP/streak/mastery. "FULL data" = two children created with *different grades/subjects/learning-languages* so their stub-derived cards differ. See workstream F section.

---

## Affected modules & data

**Frontend only.** No new entities, no migrations, no backend code changes. Files by surface:

**Parent shell / shared (changes propagate to all parent pages):**
- `apps/student-app/app/(parent)/_components/Sidebar.tsx` — nav order (RTL), icon alignment, the align-overview/align-settings SHARED deltas (active label `$primaryLight`, remove left-border accent, radii, font sizes), and **likely the new home for the locale/theme switcher** (or a new shell header — see Q-A1).
- `apps/student-app/app/(parent)/_layout.tsx` — currently a bare `Stack`; candidate location for a shared shell wrapper if one is introduced (Q-A1; flag a pattern decision per CLAUDE.md rule 8).
- `apps/student-app/app/(parent)/overview.tsx`, `children.tsx`, `reports.tsx`, `settings.tsx` — each independently composes `<Stack flexDirection="row"><Sidebar/><ScrollView/></Stack>`. There is **no shared web shell today**; the switcher + scroll fixes touch each, OR a shared shell is added (Q-A1).
- `apps/student-app/app/(parent)/index.tsx` — the post-login placeholder; once B redirects to Overview, this page is bypassed in the happy path but still reachable via `/(parent)`. Decide whether it gets the switcher too (Q-B1).

**Existing entities (read-only context, no change):** `AccountProfileResponse` (no email field — by design), `UpdateMyProfileCommand` (no email — by design), `AddChildCommand`, `LinkedChildResponse`, `User.PreferredLanguage`.

**Design tokens (may need additions per align docs):** `$cardInner` (14px radius), `$xpSoft`, `$streakSoft` — GAP-01/GAP-02 in `align-overview.md`. Several already exist in code (`$cardInner`, `$dangerSoft`, `$warningSoft`, `$successSoft`, `$primarySoft`, `$xpSoft`, `$streakSoft` are referenced in current `OverviewWeb.tsx`/`FocusAreasCard.tsx`), so confirm which are still missing before adding any.

---

## Current-file → design-system-reference mapping

All design-system reference files below were **verified to exist** on disk.

| Workstream | Current code file(s) | design-system reference (verified) |
|---|---|---|
| A (shell switcher) | `app/(auth)/_components/LocaleThemeControls.tsx` (the existing switcher to reuse), `app/(parent)/_components/Sidebar.tsx`, `_layout.tsx`, all 4 page wrappers | `SKILL.md` Skill 9 (scrollbar), `ui_kits/parent-dashboard/index.html` (scrollbar block L65-75, `web-sidebar.html`), `index-ar.html` (RTL sidebar/order) |
| A (scrollbar) | the 4 page `ScrollView`s + web root | `design-system/ui_kits/parent-dashboard/index.html` L65-75 `*::-webkit-scrollbar*` |
| A (RTL order) | `Sidebar.tsx` `NAV` array (L50-58) | `ui_kits/parent-dashboard/index-ar.html`; `align-overview.md` §RTL (M-23, N-08) |
| B (routing) | `src/hooks/useAuthRoute.ts` (L108), `src/hooks/useGroupGuard.ts` | n/a (behavior, not visual) |
| C (Overview) | `app/(parent)/overview.tsx`, `_components/OverviewWeb.tsx`, `OverviewWeb`→`FocusAreasCard.tsx`/`DailyActivityCard.tsx`/`SubjectMasteryCard.tsx`, `parentDashboardStubs.ts` | `align-overview.md`, `ui_kits/parent-dashboard/index.html` (L136-150 side-by-side), `DashboardComponents.jsx`/`PagesApp.jsx` (`PDPanel`, `PDWeakAreas`, `PDRecommendation`), preview `web-weak-areas-list.html` / `web-recommendations.html` / `ar-web-recommendations.html` |
| D (Settings) | `app/(parent)/settings.tsx`, `_components/SettingsWeb.tsx`, `_components/settings/LanguagePanel.tsx`, `LinkedChildrenPanel.tsx` | `align-settings.md`, `ui_kits/parent-dashboard/PagesApp.jsx` (`SettingsWebPage`/`SettingsProfile`/`SettingsLanguage`), `index-ar.html`, preview `web-linked-rows.html` / `web-plan-card.html` / `components-input.html` |
| E (Add Child) | `app/(onboarding)/add-child.tsx`, `_components/AddChildForm.tsx`, `EditChildSheet.tsx`; `app/(parent)/_components/settings/LinkedChildrenPanel.tsx` (Add-child CTA); `app/(parent)/link-child.tsx` (separate *link existing* flow) | `ui_kits/parent-dashboard/AddChildModal.jsx`, preview `web-add-child-modal.html` + `ar-web-add-child-modal.html`; mobile `mobile-add-child-sheet.html` + `ar-mobile-add-child-sheet.html`; `SKILL.md` Skill 8 |
| F (verify) | `packages/api-client/src/hooks/useAddChild.ts`, `useRegisterParent.ts`, `useMyChildren.ts` | n/a (API) |

`align-my-children.md` and `README.md` in the kit also exist and give the My-Children + overall conventions context.

---

## FLAG — Global locale/theme switcher: new shared state vs reuse existing store

**Reuse the existing stores. No new shared state is needed.**
- The switcher already exists as `LocaleThemeControls` and is driven by two Zustand stores: `src/providers/localeStore.ts` (`useLocaleStore`, web-persisted to `localStorage` `lx_locale`) and `src/providers/themeStore.ts` (`useThemeStore`). Both are app-global already; the login screen is the only place that *renders the control*.
- The correct fix is **placement, not new state**: render `<LocaleThemeControls />` (or a parent-shell-styled equivalent) inside the parent shell (sidebar footer or a new shell header) so it appears on every parent page. The Language & Region settings panel already calls `useUpdateUserLanguage` + `setLocale`, confirming the store is the single source of truth.
- **One real gap (flag to designer + reviewer):** `themeStore` does **NOT** persist (no `localStorage`, unlike `localeStore`). For the switcher to "persist choice" everywhere per AC-A, theme persistence must be added (mirror `localeStore`'s `localStorage` pattern). This is a small, in-pattern change — not a new abstraction — but call it out. See Q-A2.

---

## Email-read-only investigation — RESULT

**Email is read-only because it is the account's login identity and there is no change-email path in the contract or API. This is by design, not a bug.** Evidence:
- `backend/.../Account/Dtos/AccountProfileResponse.cs` has only `FullName`, `Phone`, `Country`, `AvatarUrl` — **no `Email`** (the doc comment explicitly states "No id/email/role is accepted on update — the user is always resolved from the JWT ... no mass-assignment, no IDOR").
- `backend/.../Account/Commands/UpdateMyProfile/UpdateMyProfileCommand.cs` has `FullName`, `Phone`, `Country` — **no Email** (comment: "there is intentionally NO id/email/role field").
- The FE (`SettingsWeb.tsx` L310) reads `(profile as { email?: string }).email ?? ''` defensively, so the email field **always renders empty today** (the contract never returns it). `align-settings.md` P-24 wrongly assumes the profile returns email — it does not.

**Brief direction for D:** keep the email field **disabled/read-only** with a clear, localized helper explaining it cannot be changed (e.g. EN "Your email is your sign-in and can't be changed here" / AR equivalent). Do **not** add an edit flow. **Decide the value source (Q-D3):** since `AccountProfileResponse` has no email, either (a) source it from `useMe` (which projects the identity — confirm it exposes email), or (b) leave the field out / show a static "—". This is a real product/contract gap to surface, not silently paper over.

---

## Add-Child entry-point inventory

There are **three** distinct add/link surfaces today; only the *add-new-child* ones become the modal:

1. **`app/(onboarding)/add-child.tsx`** + `_components/AddChildForm.tsx` — the onboarding multi-child add screen (full form: name, email, password, grade picker, learning-language + app-language group, country; multi-draft list; submit loop). This is the **primary add-child form** and the main thing the modal replaces *for the parent dashboard context*. Onboarding (first-run, no children yet) may keep its own full-screen flow — **decide whether the modal replaces onboarding too or only the in-dashboard CTA (Q-E2).**
2. **`app/(parent)/_components/settings/LinkedChildrenPanel.tsx`** — the Settings → Linked children "Add child" CTA (L639-651, `handleAddChild`) currently `router.push('/(onboarding)/add-child')`. This CTA should **open the modal** instead of navigating away.
3. **`app/(parent)/link-child.tsx`** + `_components/LinkChildForm.tsx` — this is **LINK an EXISTING child**, a different feature (not create). **Out of scope** for the Add-Child modal; leave it as-is.

The `OverviewWeb.tsx` empty-state "Add child" button (L190-196) also routes to `/(onboarding)/add-child` and should open the modal once it exists.

**Net:** the new modal becomes the entry point from (a) Settings → Linked children CTA, (b) Overview empty-state, and (optionally) (c) onboarding. `link-child.tsx` is untouched.

---

## Handoff → designer

Produce a **Design Spec** at `design-system/ui_kits/parent-dashboard/parent-dashboard-uiux.md` covering, grounded in the verified reference files:

1. **Parent shell** — where the locale/theme switcher lives (sidebar footer vs a new shell header bar), its EN/LTR + AR/RTL appearance, and whether a shared shell wrapper component is introduced (if a new compound/shell pattern is proposed, it must be **flagged for lead approval per CLAUDE.md rule 8** before frontend builds it). Specify the brand scrollbar styling and the RTL nav order (Overview before My Children) with icon alignment.
2. **Overview** — the side-by-side "Areas to focus on" + "Recommendations from Lexi" 2-col grid (stack on narrow), the new Recommendations panel content/tokens (mirror `PDRecommendation`), and resolve the align-overview deltas. Call out that index.html supersedes the older "do not add Recommendations" note.
3. **Settings** — linked-children section corners/missing parts, the avatar upload affordance, the read-only email disabled state + helper copy (EN+AR), and the Language & Region Save button placement.
4. **Add-Child modal** — full web modal spec from `AddChildModal.jsx` + `web-add-child-modal.html` (photo upload, grade tiles, flag tiles, footer CTA), EN + AR, plus how the extra required backend fields (password, learning-language, country) are incorporated without breaking the designed layout (Q-E1).
5. Tokens only; bucketed radii; logical-prop RTL; name every color/spacing/radius token.

## Handoff → frontend

Consume the Pipeline Brief + Design Spec. Implementation notes:
- **A:** render `LocaleThemeControls` (or shell-styled variant) in the parent shell so it shows on every parent page; reuse `useLocaleStore` + `useThemeStore` (no new store). Add `localStorage` persistence to `themeStore` (mirror `localeStore`). Add the brand scrollbar CSS to the web app globally. Ensure each page's content column scrolls (audit the `ScrollView`/`flex` chain in `overview/children/reports/settings`). Reorder the `NAV` array / handle RTL so Overview precedes My Children in AR; fix RTL icon alignment. If a shared shell component is introduced, get lead sign-off first (rule 8).
- **B:** in `useAuthRoute.ts` change `router.replace('/(parent)')` (L108, parent-with-children branch) to `router.replace('/(parent)/overview')`; check `useGroupGuard.ts` for any matching hard-coded `'/(parent)'` target and keep them consistent. Verify deep-link/back behavior.
- **C:** restructure `OverviewWeb.tsx`'s `OverviewBody` so `FocusAreasCard` and a new `RecommendationsCard` (mirror `PDRecommendation`) sit in a 2-col `flexDirection={rowDir}` row with `flexWrap` (stack on narrow), `alignItems="flex-start"`; apply the align-overview deltas (radii, font sizes, RTL). No new design pattern — mirror existing card shapes.
- **D:** wire/repair avatar upload preview (the `useUploadAvatar` path exists in `SettingsWeb.tsx`); keep email disabled with helper copy; add the Language & Region Save button calling `useUpdateUserLanguage`; align the Linked Children panel corners. Resolve Q-D1/Q-D2/Q-D3 with the lead first if they block.
- **E:** build the Add-Child modal (Tamagui, scrim + centered card, `borderRadius="$modal"`), grade tiles + flag tiles (no `<select>`), photo preview via `URL.createObjectURL`. Submit via `useAddChild`. Repoint the LinkedChildren CTA + Overview empty-state CTA to open it. **Do not** invent a Modal/Dialog abstraction if one already exists in `@learnexia/ui` — reuse it; if none exists and one is needed, flag the pattern to the lead (rule 8).
- Constraints (all workstreams): Tamagui + design-system tokens only (no raw hex/rgba except the few sanctioned hairline `rgba(255,255,255,0.06/0.04)` already used in code); reuse `$primary/$card/$button` radii and the `*Soft/*SoftStrong` tokens; RTL via logical props; no new dependency without asking.

## Handoff → frontend-e2e-tester

Drive the running web PWA (Playwright) in both `en`/LTR and `ar`/RTL:
- **A:** switcher present + working on Overview, My Children, Reports, Settings (and landing) — toggling locale flips `dir`/fonts app-wide and persists across reload + route change; theme toggle persists across reload. Brand scrollbar present. Every page scrolls to its content end (no clipping). In AR, sidebar nav order shows Overview ("نظرة عامة") above My Children ("أطفالي"); icons aligned.
- **B:** sign in as a parent with children → lands on `/overview`. Parent with no children → `/add-child`. Student → `/(child)`. Signed-out → `/login`.
- **C:** Overview shows all sections incl. Recommendations; "Areas to focus on" and "Recommendations from Lexi" are in one row on wide, stacked on narrow; RTL correct (sidebar right, bars LTR, Eastern-Arabic numerals).
- **D:** avatar upload opens picker + previews; email field disabled with visible reason and no edit affordance; Language & Region Save persists (reflects after reload); linked-children section renders correctly.
- **E:** Add-child CTA (Settings → Linked children, and Overview empty-state) opens a centered modal with scrim; grade tiles + flag tiles (no native select); photo preview; Cancel closes; submit calls Add-Child and the new child appears in My-Children. AR modal RTL + CTA arrow flips.
- **F:** execute the verification plan below as part of the run.

## Handoff → reviewer

Gate against: this brief's per-workstream ACs + the `design-system/` reference (index.html/preview wins over older align docs where they conflict) + CONVENTIONS.md. Specifically verify: (1) switcher reuses existing stores and theme now persists; (2) no new design pattern / dependency introduced without an explicit lead-approved flag (rule 8); (3) email read-only path unchanged on backend and clearly explained in UI; (4) tokens-only, bucketed radii, RTL via logical props; (5) `link-child.tsx` (link-existing) left intact; (6) routing change is the single `/(parent)` → `/(parent)/overview` edit and its guard mirror.

---

## Workstream F — Verification plan (reproducible)

Backend runs on **`http://localhost:5080`** (per HANDOFF/dev env). All parent endpoints resolve the acting parent from the JWT — never the body.

**Pre-req (already done per task):** `parent@demo.com` registered via
`POST /api/Users/Authentication/Register-Parent` (RegisterParentCommand). Sign in via `POST /api/Users/Authentication/Sign-In` to get the JWT; send it as `Authorization: Bearer <token>` on the calls below.

**Add two children** (twice, distinct data) — `POST /api/Parent/Add-Child` (`AddChildCommand`), all fields required:
```
// Child 1
{ "fullName": "Layla",  "email": "layla@demo.com",  "password": "<Strong#1>",
  "grade": 2, "language": "ar", "learningLanguage": "ar", "country": "EG" }
// Child 2
{ "fullName": "Omar",   "email": "omar@demo.com",   "password": "<Strong#2>",
  "grade": 5, "language": "en", "learningLanguage": "en", "country": "SA" }
```
Use **different grades + learning-languages** so the dashboard's per-child cards differ.

**Verify children are linked:** `GET /api/Parent/My-Children` (`ListMyChildrenQuery`) → returns both as `LinkedChildResponse` (fullName, email, grade?, learningLanguage). This is what `useMyChildren` feeds the sidebar + Overview.

**Browser verification:**
1. Sign in as `parent@demo.com` → confirm landing on `/overview` (workstream B).
2. Confirm the sidebar child-selector shows a child and the Overview header reads that child's name; KPIs/mastery/focus/recommendations render.
3. **Child switching — IMPORTANT LIMITATION (flag):** there is **no real child-switcher** today. The sidebar child-selector card just `router.push('/(parent)/children')`; Overview always uses `children[0]` from `useMyChildren`. So "child-to-child switching + per-child preview" is **not currently implementable** without new FE state (an active-child selector). **This is a scope gap — see Q-F1.** What *is* verifiable: each child id yields distinct stub XP/streak/mastery via `getChildStatsStub`, so the two children would render different cards *if* the UI let you select each one.
4. **Data caveat:** real XP/streak/mastery/activity is **Phase-5, not seedable** — the dashboard uses deterministic stubs from `parentDashboardStubs.ts`. No endpoint seeds progress. Document this so the verifier doesn't chase non-existent "real data."

---

## Open questions / assumptions / risks

- **Q-A1 (pattern, lead decision):** Should a **shared parent shell** component (header + switcher + sidebar wrapper) be introduced in `_layout.tsx`, or should the switcher be added to the existing `Sidebar.tsx` footer and each page keep composing its own row? A shared shell is cleaner but is arguably a new structural pattern → **flag for lead approval per CLAUDE.md rule 8** before building.
- **Q-A2 (assumption → confirm):** `themeStore` does **not** persist today. Assuming AC-A "persist choice" includes theme, frontend will add `localStorage` persistence mirroring `localeStore`. Confirm desired (and whether theme should also persist server-side, or web-only like locale).
- **Q-A3 (risk):** SKILL.md caveat #4 says **light theme is not implemented** (only the `--lx-bg-light` token exists). A working dark/light *toggle* on every page may surface an unfinished light theme. Confirm scope: ship the toggle as-is (may look incomplete in light), or is full light-theme support in-scope? Likely **out of scope** — flag.
- **Q-B1:** After B redirects to `/overview`, does the `/(parent)/index.tsx` placeholder still need the switcher/treatment, or can it be retired/left as a rarely-hit fallback?
- **Q-C1 (resolved direction, confirm):** The Recommendations panel content is Phase-5 stub copy in the design (`PDRecommendation`). Confirm it ships as a styled stub (like FocusAreas) rather than wired to a non-existent endpoint.
- **Q-D1:** What exactly is "broken" about the profile upload? The pick/preview/upload code exists in `SettingsWeb.tsx` (web `<input type=file>` + `useUploadAvatar`). Likely causes to check: avatar URL not rendered back (BE-4 avatar endpoint readiness), preview not shown pre-upload, or a CORS/asset path issue. Needs a quick repro in the running app before the frontend agent "fixes" it.
- **Q-D2:** Region in Language & Region has **no backend persistence endpoint**. With a Save button added, does Save persist only the language (real) and treat region as UI-only, or is a region endpoint expected? Recommend language-only persist + region UI-only with a note.
- **Q-D3:** Email value source — `AccountProfileResponse` has no email. Confirm whether `useMe` exposes the email to display (read-only), or the field shows a static placeholder. Surface this contract gap.
- **Q-E1 (important):** The Add-Child modal design (`web-add-child-modal.html`) shows name, email, grade, language, photo, avatar-color — but the backend `AddChildCommand` **requires password, learningLanguage (axis B), and country**, and the current form collects all of them. The modal must collect these too (or the brief must define defaults). Confirm: extend the modal to include password + learning-language + country (recommended, to not break the contract), or change the backend (out of scope). Recommend extend the modal.
- **Q-E2:** Does the modal replace the **onboarding** add-child screen too, or only the in-dashboard CTAs? Onboarding is first-run (no sidebar context) and multi-child; recommend keeping onboarding's full-screen flow and using the modal only inside the dashboard. Confirm.
- **Q-E3:** Is there an existing Modal/Dialog/overlay primitive in `@learnexia/ui` to reuse (e.g. as used by `ChangeLearningLanguageModal`)? If yes, reuse it; if not and one is needed, flag the pattern (rule 8). `ChangeLearningLanguageModal.tsx` exists — check whether it's a reusable shell.
- **Q-F1 (scope gap):** There is **no active-child selector** in the parent dashboard — Overview/Sidebar always use the first child. Verifying "child-to-child switching" requires adding a child-switcher (new FE state, likely a Zustand `activeChildId` or URL param). Is building the switcher in scope for this work, or is verification limited to "two children exist and each renders distinct stub data when selected as first"? This materially affects workstream F and possibly A/C. **Recommend lead confirm before planning.**
- **Risk — SHARED component blast radius:** `Sidebar.tsx`, `Tabs`, `MasteryBar`, `Select`, `TextField`, `Button` changes propagate across My Children / Overview / Reports / Settings / auth forms. Reviewer must regression-check all parent pages + auth, per the align docs' "SHARED" callouts.
- **Risk — align docs vs index.html drift:** `align-overview.md` says "do NOT add Recommendations"; the current `index.html` adds it. This brief resolves in favor of index.html. Any other align/index conflicts should resolve the same way (index/preview = latest).

---

## Recommended pipeline order (first cut — planner finalizes)

1. **designer** (one Design Spec covering all six workstreams; resolves the visual decisions; flags any new shell/modal pattern for lead approval before frontend).
2. **frontend** — batchable:
   - Batch 1 (independent, fast): **B** routing edit (`useAuthRoute.ts`) — tiny, can land first.
   - Batch 2: **A** shell switcher + scrollbar + RTL nav order + scroll fixes (touches shared `Sidebar`/`_layout` + 4 pages) — do before/with C & D since it changes shared shell.
   - Batch 3 (parallel after A's shared changes settle): **C** Overview pass and **D** Settings pass (independent content, but both depend on the shared Sidebar deltas from A).
   - Batch 4: **E** Add-Child modal (depends on a Modal primitive decision from designer; repoints CTAs touched in D).
3. **frontend-e2e-tester** — full EN/AR run across A-E + execute **F** verification.
4. **reviewer** — gate against ACs + design-system + CONVENTIONS, with the SHARED-component regression sweep.
5. **committer** — only after PASS; per-story branch `feat/parent-dashboard-uiux`, PR, no merge.

(No db-migration / backend-feature / security-auditor batch — FE-only, no auth/data-model change. The email read-only and Add-Child paths are existing, audited backend contracts left untouched.)
