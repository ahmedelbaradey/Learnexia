# Pipeline Brief — Parent web + auth RTL alignment / rounded-corner polish + functional fixes

## Summary & traceability

- **One-line task:** A QA-driven UI/UX correction pass on the student-app **parent web dashboard + auth surfaces**, dominated by **Arabic/RTL alignment** and **rounded-corner-not-matching-design** defects, plus a handful of functional bugs (region Save error, plus items already coded but reported still-broken live).
- **Source of truth:** lead/user **QA findings** (this brief), NOT a user story. There is **no `user-stories/` file** — do not invent one.
- **Design source of truth (in-repo):** `design-system/ui_kits/parent-dashboard/index-ar.html` (+ `index.html`), `design-system/screenshots/web-ar/{01-landing,02-login,03-register,04-my-children,05-dashboard,06-reports,07-settings}.png`, `design-system/preview/ar-web-*.html` + `web-add-child-modal.html`/`ar-web-add-child-modal.html`, and `design-system/SKILL.md` (10 core rules — radii bucket + RTL rules). The user is viewing `design-system/ui_kits/student-mobile/index-ar.html` as the AR reference.
- **Product-decision context (CLAUDE.md, authoritative):** parent-driven onboarding (no student self-register); 4 subjects; no teacher role; grade transition preserves history. The **"children have no country"** rule is already settled (see §Critical context).
- **Phase/epic:** post-Phase-1/2 web FE polish. Relates to the **parent-dashboard-uiux** workstream (brief `docs/briefs/parent-dashboard-uiux.md`, spec `design-system/ui_kits/parent-dashboard/parent-dashboard-uiux.md`) and FE auth P1-11/P1-12.

---

## CRITICAL CONTEXT — read before planning (this changes the whole shape of the work)

**Almost every finding in this list maps to code that is ALREADY WRITTEN on the current `main` working tree.** Two prior efforts shipped these exact fixes, by their own commit comments:

1. **PR #128** (`fix/frontend-rtl-alignment-polish`) — the *same* QC findings list across Login, My-Children, Overview, Settings, layout/Sidebar, Register. HANDOFF (2026-06-13) documents it shipping: `TextField` direction-aware align + `forceValueLtr` + `autoComplete`; `AddChildModal` **edit mode** + `autoComplete="off"` + the Arabic "Add Child" `flexWrap` fix; sidebar **ChildSwitcher made functional** + duplicate top-nav switcher **removed**; **Logout added** to sidebar; **register 2-step wizard**; the new **`PUT /api/Users/Account/Language`** endpoint for the language-Save 403.
2. **`feat/onboarding-add-child-modal`** — onboarding add-child → modal flow; `AddChildModal` moved to `apps/student-app/src/components/AddChildModal.tsx`.
3. A follow-up commit `5e3a057` (per HANDOFF) **removed the Country field from child add/edit entirely** (product rule: children have no country).

**The working tree on `main` contains all of this** — verified by reading the files (the code comments literally say "bug #3 / #6 fix", "bug #4 fix", "TWO-STEP WIZARD", "ChildSwitcher removed", etc.), and the committed `packages/api-client/swagger.json` already carries the `language()` method.

**So why is the user still reporting these as broken?** The single load-bearing reason is in HANDOFF for PR #128:

> **"NOT verified live: no Expo-web visual RTL smoke this pass (typecheck/lint/review only)."**

The fixes passed type-check / lint / reviewer **but were never run in the browser**. The user re-tested the live web PWA and the fixes do **not** actually render correctly. The dominant cause is a **systemic RTL double-flip anti-pattern** (see §Systemic root causes) that is invisible to type-check/lint/review and only shows up in a real RTL browser render.

**This means the task is NOT "implement these fixes from scratch." It is: (a) verify each already-coded fix against the live AR/EN render, (b) repair the ones that don't actually work (mostly the double-flip), (c) implement the genuinely-new items (region Save endpoint decision), and (d) finally do the live RTL smoke that #128 skipped.** The `planner` should structure the plan around *verify → repair → re-verify*, not greenfield implementation. Treat this as **partly revising PR #128 + the onboarding-modal work**, with the register-wizard being the biggest live decision (see §Register wizard decision).

> **Branch note:** a branch `feat/parent-web-rtl-polish` already exists (matches this task slug). `fix/frontend-rtl-alignment-polish` and `feat/onboarding-add-child-modal` also exist as separate branches; their content is reflected on `main`. The committer should confirm the working branch with the lead before committing.

---

## Business context & value

- **Who benefits:** the **parent** (primary user of the entire web dashboard + auth) and, transitively, the **child** (a parent who can't navigate or trust the dashboard won't sustain the habit loop). Arabic-first families are the core market — a broken RTL render is a first-impression failure.
- **Value:** (1) Arabic users get a correctly-mirrored, polished dashboard that matches the design (credibility + usability); (2) functional bugs (region Save error, any still-broken Add-Child / switcher / edit) stop blocking real parent workflows; (3) the register/onboarding flow becomes a coherent single wizard.
- **Success measure:** every finding below renders **pixel-faithful to its AR screenshot reference in a live web PWA, in both `ar` (RTL) and `en` (LTR)**, with no functional error, confirmed by `frontend-e2e-tester` (the step #128 skipped).

---

## Systemic root causes (FIX THESE FIRST — a few shared changes resolve most findings)

### SYSTEMIC-1 — RTL "double-flip": `flexDirection="row-reverse"` ON TOP OF `<html dir="rtl">` (THE DOMINANT BUG)

This is the root cause of essentially **every "icons / content not aligned in Arabic"** finding (sidebar nav icons, KPI card icons, Recommendations icons, FocusAreas icons, settings-tab icons, overview header, child card, login/register rows, photo-upload avatar).

**Mechanism:**
- Web direction is applied by `applyWebDirection()` in `packages/shared/src/i18n/rtl.ts:71-77`, which sets `document.documentElement.dir = 'rtl'`. With `<html dir="rtl">`, the browser **already reverses the visual order of a flex `row`** to right-to-left.
- The CORRECT pattern (used by `app/(child)/_components/ChildTabBar.tsx:19,285`, `app/(parent)/_layout.tsx:26-28`, and `_components/SettingsWeb.tsx:95-97`) is to keep `flexDirection="row"` and let the document `dir` flip it once. Those files even carry explicit warnings: *"do NOT add `row-reverse` (would double-flip)."*
- But the MAJORITY of parent + auth components do `const rowDir = isRtl ? 'row-reverse' : 'row'` and apply `flexDirection={rowDir}`. On web that **flips a second time**, landing the row back at **LTR order** — so the icon/label/control end up on the wrong side, contradicting the AR screenshots.

**Components confirmed using the `rowDir`/`row-reverse` double-flip pattern (web):**
- `app/(parent)/_components/Sidebar.tsx` (nav rows, brand row, child-selector rows, logout row — `rowDir` throughout)
- `app/(parent)/_components/ChildSwitcher.tsx` (pill + dropdown rows)
- `app/(parent)/_components/OverviewWeb.tsx` (header row, KPI region, KPI label/chip row)
- `app/(parent)/_components/FocusAreasCard.tsx` (header + each focus row + chip)
- `app/(parent)/_components/RecommendationsCard.tsx` (each recommendation row + chip)
- `app/(parent)/_components/ChildDashboardCard.tsx` (header, status, grade/lang, stat tiles, footer)
- `app/(parent)/_components/MyChildrenWeb.tsx` (header row, pick-a-child row)
- `src/components/AddChildModal.tsx` (`rowDir` on header, photo row, tiles, footer)
- `packages/ui/src/components/Tabs/index.tsx:53,70` (settings-tab rows — root cause of "icon in settings tabs nav not aligned")
- `packages/ui/src/components/ChildCard/index.tsx` (`rowDir`)
- `app/(auth)/_components/LoginForm.tsx` (remember-me row, social row), `loginParts.tsx` (Checkbox, OrDivider, SocialButton, SocialRow), `RegisterForm.tsx`, `RegisterFeaturePanel.tsx`, `PersonaToggle.tsx`, `LoginBrandPanel.tsx`.

**Why review/lint missed it:** type-check and lint only see a valid `flexDirection` value; the visual double-flip is only observable in a real RTL browser render. #128's reviewer PASS was over typecheck/lint/code-read — exactly the blind spot.

**`designer` + `planner` MUST resolve the rule first (this is a design-pattern / convention decision per CLAUDE.md rule 8 — ASK before standardizing):** pick ONE canonical RTL row strategy and apply it uniformly. Two viable options:
- **(A) "Logical row, single dir flip" (recommended, matches the components that already work):** components keep `flexDirection="row"` and rely on `<html dir>` (web) / `I18nManager` (native) to flip once. Remove all `row-reverse`/`rowDir` from web row containers. Caveat: native (Expo) does NOT get a DOM `dir`; native RTL comes from `I18nManager.forceRTL` (`app/_layout.tsx:89-101`) which auto-flips `row` — so plain `row` is also correct on native. This makes (A) consistent cross-platform.
- **(B) "Manual flip, no dir":** stop setting `document.dir` and flip everything manually with `row-reverse`. **Not recommended** — it breaks native, breaks text bidi/`writingDirection`, and is a larger blast radius.

Recommend **(A)**. This single normalization fixes the bulk of the alignment findings at once. The `designer` should specify, per component, the expected visual side of icon vs label/control in BOTH locales, grounded in the AR screenshots (e.g. dashboard `05-dashboard.png`: sidebar nav **icon on the right** in AR; KPI card **icon chip on the LEFT, label on the right** in AR; focus rows **chip on the LEFT** in AR).

> Verify the double-flip empirically during design/e2e: load the app with `dir=rtl` and compare to `screenshots/web-ar/*`. The fix is mechanical but must be applied consistently and then **visually confirmed**.

### SYSTEMIC-2 — "Rounded corners not matching design" — NOT a token-value bug; diagnose the render

The radius tokens are **correct** (`packages/design-system/src/tokens/index.ts:60-72`): `sm:8`, `nav:12`, `cardInner:14`, `button:16`, `card:20`, `modal:24`, `pill:9999` — these match SKILL.md rule 4 (8 chips/inputs, 16 buttons, 20 cards, 24 modals, pill). The cards DO use the right tokens (`OverviewWeb` KPI `$card`=20, `ChildDashboardCard`/`AddChildCard` `$modal`=24, panels `$modal`=24, inputs local `14`, buttons `$button`=16).

So "all cards not rounded as design (AR + EN)" is almost certainly a **render/compile issue, not a wrong number.** Candidate root causes to diagnose (designer + frontend):
- **Tamagui token → CSS not applied on RN-Web.** Some surfaces set radius via the **Tamagui `borderRadius="$card"` prop**, others via **raw inline `style={{ borderRadius: 24 }}`** (e.g. `AddChildModal` line 326 uses inline `borderRadius: 24`, KPI cards use the token). If the token form isn't compiling to CSS in the web build (e.g. a `@tamagui/core` `Stack` vs the `packages/ui` `primitives` `YStack/XStack` mismatch, or missing radius in the web `tamagui.config` token set), card corners render square while the modal (inline) renders rounded — which matches the "cards look unrounded but the modal is fine" symptom.
- **`overflow` clipping interplay** — a parent without `overflow:hidden` plus a child gradient/image can visually defeat the corner; check the cards that layer `GradientBox`/`Image`.

**Action:** the designer confirms the *intended* radius per surface from SKILL.md + screenshots (no token change expected); the frontend agent diagnoses *why the applied token doesn't render* on web and fixes the render path (likely: ensure radius is set in a way Tamagui compiles on RN-Web, or normalize all card surfaces to the same prop form that already works on the modal). **Do not bump token values** — that would regress the (correct) bucket.

### SYSTEMIC-3 — Latin/technical strings + numerals

Already handled in most places (`forceValueLtr` on email, `writingDirection="ltr"` on brand wordmark, Eastern-Arabic numerals via `Intl`). Note the known minor (HANDOFF): subject-mastery percentages render Latin digits in AR while KPIs use Eastern-Arabic — flag as a consistency nit, not a blocker.

---

## Acceptance criteria (testable; the reviewer checks against these)

**Global (both `ar`/RTL and `en`/LTR, live web PWA):**
1. The chosen canonical RTL row strategy (SYSTEMIC-1) is applied uniformly to all listed parent + auth components; no surface double-flips. Every "icons not aligned in Arabic" finding renders on the side shown in the matching `screenshots/web-ar/*` reference.
2. All cards (KPI, child cards, panels, recommendations, focus areas, add-child) render with the correct design radius **visibly** in the browser (SYSTEMIC-2), in both themes.
3. `frontend-e2e-tester` runs the live AR(RTL)+EN smoke that PR #128 skipped, covering every surface below, and it passes.

**Login (`web-ar/02-login.png`):**
4. Email field text is right-aligned/LTR-correct in Arabic; brand panel logo + text align per design; email & password inputs render with the design radius.

**Children (`web-ar/04-my-children.png` + add-child modal refs):**
5. Add/Edit Child modal has the correct field set (name, email+password add-only, grade tiles, app-language flag tiles, learning-language) and **no country field** (children have no country); email + password have `autoComplete="off"`.
6. The "Add Child" button above the list is functional in **Arabic** (and EN); pressing **Edit** on a child opens the **same modal** as Add (edit mode), in both AR + EN.
7. Photo-upload avatar badge sits on the logical-end corner in AR; the "Add Child" button + "اختر طفلاً لعرض تقدّمه" text and the child card (name, active dot, grade, language, edit) align per the AR reference.

**Overview (`web-ar/05-dashboard.png`):**
8. KPI card icons, Recommendations icons, FocusAreas icons, and the overview header all align per the AR reference; cards rounded per design.

**Settings (`web-ar/07-settings.png`):**
9. Settings tab-nav icons align in Arabic.
10. Region tab **Save** no longer throws "No internet connection…"; the resolved region behavior (per §Functional bug REGION decision) works without error.

**Layout / Sidebar:**
11. Sidebar menu icons align in Arabic; there is **exactly one** working child-switcher in the **sidebar** (the top-nav duplicate is gone on wide); the sidebar **Logout** works (clears session → routes to login).

**Register (`web-ar/03-register.png`):**
12. The "ولي أمر / وصي قانوني فقط…" banner aligns + has the correct border; the page has no dead/forced scroller; email + password inputs render the design radius; `RegisterFeaturePanel` aligns in Arabic with correct margin.
13. The add-child step is **step 2 of an inline wizard** (per §Register wizard decision) reusing the Add-Child modal field set at the parent-form width — OR the lead's chosen alternative.

---

## Per-finding table — finding → current file(s):line → AR design ref → root-cause → proposed fix → severity

> Severity legend: **FUNC** = functional bug · **ALIGN** = RTL alignment · **RADIUS** = rounded-corner · **DEC** = needs a decision.
> "Already coded" = the fix exists on `main` but was never live-verified; the work is verify/repair, not greenfield.

### LOGIN — `screenshots/web-ar/02-login.png`

| # | Finding | Current file(s) | AR ref | Root cause | Proposed fix | Severity |
|---|---|---|---|---|---|---|
| L1 | Email text not right-aligned in Arabic | `packages/ui/src/components/TextField/index.tsx:124-126,226-228`; used in `app/(auth)/_components/LoginForm.tsx:311-329` | `02-login.png`, `preview/components-input.html` | Email is intentionally LTR (`forceValueLtr` on `keyboardType='email-address'`), so value sits left. The *label* is direction-aware. If the user means the **label/placeholder** isn't right-aligned, verify `valueAlign`/`textAlign` in RTL; if they mean the email **value**, that's by design (Latin/LTR per SKILL.md rule 4.5). | Confirm intent with designer against `02-login.png`. Likely **no change to the value** (LTR is correct); ensure label + error are right-aligned in AR (already coded). | ALIGN |
| L2 | Brand text + logo not aligned as design | `app/(auth)/_components/LoginBrandPanel.tsx:24-41,44-80`; `src/components/FormScaffold.tsx:123-187` (SplitFormScaffold `brandSide`) | `02-login.png` | Logo row is hard-`row` (mark→wordmark, correct, not mirrored); tagline block uses `alignItems` by direction. Possible mismatch is the **panel side** (`brandSide='start'` for login) vs the AR reference, or vertical spacing. | Designer compares panel layout/spacing to `02-login.png`; adjust alignment/margins; keep brand mark LTR. | ALIGN |
| L3 | Email & password not rounded as design | `TextField/index.tsx:32` (`inputRadius = 14`) | `02-login.png`, `components-input.html` | Input radius is a local `14` (between `$sm`=8 and `$card`=20). Either the design wants 8/`$sm`, or the `14` isn't rendering on web (SYSTEMIC-2). | Designer confirms the exact input radius from the AR reference; frontend ensures it renders on web. Likely keep 14 (matches spec gap-9) but verify visually. | RADIUS |
| L4 | Review whole page vs design | login.tsx + all of the above | `02-login.png` | Composite. | Designer does a full page diff vs screenshot; enumerate deltas in the Design Spec. | ALIGN |

### CHILDREN — `screenshots/web-ar/04-my-children.png` + `web-add-child-modal.html` / `ar-web-add-child-modal.html`

| # | Finding | Current file(s) | AR ref | Root cause | Proposed fix | Severity |
|---|---|---|---|---|---|---|
| C1 | Add/Edit popup has NO country field (should it?) | `src/components/AddChildModal.tsx` (no country field present) | `ar-web-add-child-modal.html` | **Resolved product rule: children have no country** (HANDOFF commit `5e3a057` removed it deliberately; backend still accepts optional `country` but client stops sending for children). The finding's premise ("it should") **conflicts with the settled rule.** | **Do NOT add country to child add/edit.** Confirm with lead this is intended (it is, per HANDOFF). Treat finding as already-resolved. | DEC (resolved) |
| C2 | email + password autocomplete must be OFF | `AddChildModal.tsx:505-506,524` (`autoComplete="off"`, `autoCorrect={false}`) | — | Already coded. | Verify live that the browser doesn't autofill. | FUNC (already coded) |
| C3 | "Add Child" button above list not functional in Arabic | `_components/MyChildrenWeb.tsx:178-205` (`flexWrap="nowrap"`, button `onPress={() => openAddChild()}`) | `04-my-children.png` | Originally `flexWrap:"wrap"` + `row-reverse` pushed the button to an un-clickable wrapped position; HANDOFF says fixed to `nowrap`. **If still broken live**, the residual cause is the SYSTEMIC-1 double-flip in the same row (`flexDirection={rowDir}`) placing the button under another element / off-screen, or a z-index/overlay from the shell. | Apply SYSTEMIC-1 (drop `row-reverse`); verify the button is hit-testable in AR live; check no overlay covers it. | FUNC + ALIGN |
| C4 | Edit child must open the SAME popup as Add (both AR+EN) | `MyChildrenWeb.tsx:85,248-258,277-282` (edit → `AddChildModal` edit mode); `AddChildModal.tsx:112-159` (edit mode) | `ar-web-add-child-modal.html` | Already coded — edit opens `AddChildModal` with `childId`+`initialValues`. (Note: `(onboarding)/_components/EditChildSheet.tsx` is orphaned dead code per HANDOFF.) | Verify edit opens the centered modal (not a sheet) in both locales live. | FUNC (already coded) |
| C5 | Upload-photo avatar not aligned in Arabic | `AddChildModal.tsx:420-447` (photo row `rowDir`; badge `{...(isRtl ? {left:-2} : {right:-2})}`) | `ar-web-add-child-modal.html` | Badge corner uses logical-end correctly, BUT the photo **row** uses `rowDir` (SYSTEMIC-1) → the avatar+dropzone order may double-flip. | Apply SYSTEMIC-1 to the photo row; keep badge logical-end. Verify live. | ALIGN |
| C6 | "Add Child" button + "اختر طفلاً…" text not aligned as design | `MyChildrenWeb.tsx:178-205` (pick-a-child row) | `04-my-children.png` | SYSTEMIC-1 double-flip in the pick-a-child row. | Apply SYSTEMIC-1; verify text on logical start, button on logical end per AR ref. | ALIGN |
| C7 | Child Card (name, active dot, grade, language, edit) alignment differs | `_components/ChildDashboardCard.tsx:104-182` (`rowDir` throughout) and/or `packages/ui/.../ChildCard/index.tsx` | `04-my-children.png` | SYSTEMIC-1 double-flip across the card header/meta/status rows. Note: which card is on screen — the dashboard grid uses `ChildDashboardCard`; the onboarding/list uses `packages/ui` `ChildCard`. | Apply SYSTEMIC-1 to whichever renders here; align to the AR card reference. | ALIGN |

### OVERVIEW — `screenshots/web-ar/05-dashboard.png`

| # | Finding | Current file(s) | AR ref | Root cause | Proposed fix | Severity |
|---|---|---|---|---|---|---|
| O1 | KPI card icons not aligned in Arabic | `_components/OverviewWeb.tsx:245-289` (KPI region + label/chip row `flexDirection={rowDir}`) | `05-dashboard.png` (chip on LEFT, label on RIGHT in AR) | SYSTEMIC-1 double-flip — code intends chip on logical-end but `row-reverse` + `dir` puts it back on the right. | Apply SYSTEMIC-1 (plain `row`); verify chip lands on the visual LEFT in AR per ref. | ALIGN |
| O2 | RecommendationsCard icons not aligned | `_components/RecommendationsCard.tsx:113-136` (`rowDir`) | `05-dashboard.png`, `ar-web-recommendations.html` | SYSTEMIC-1. | Apply SYSTEMIC-1. | ALIGN |
| O3 | FocusAreasCard icons not aligned | `_components/FocusAreasCard.tsx:98,157-187` (`rowDir`) | `05-dashboard.png`, `ar-web-weak-areas.html` | SYSTEMIC-1. | Apply SYSTEMIC-1; keep confidence bar `direction:ltr` (already correct). | ALIGN |
| O4 | ALL cards not rounded as design (AR+EN) | KPI `OverviewWeb.tsx:251` `$card`; `ChildDashboardCard.tsx:86` `$modal`; panels `$modal`; `FocusAreasCard`/`RecommendationsCard` `$card` | `05-dashboard.png` | **SYSTEMIC-2** — tokens are correct (card=20, modal=24); the radius likely isn't rendering on RN-Web for token-prop surfaces (modal uses inline `borderRadius:24` and looks fine). | Diagnose the Tamagui token→CSS web render; normalize card surfaces to a form that renders; **do not change token values**. | RADIUS |
| O5 | Overview header content not aligned in Arabic | `OverviewWeb.tsx:146-194` (`overview-header` `flexDirection={rowDir}`) | `05-dashboard.png` | SYSTEMIC-1. | Apply SYSTEMIC-1; title block on logical start, controls on logical end. | ALIGN |

### SETTINGS — `screenshots/web-ar/07-settings.png`

| # | Finding | Current file(s) | AR ref | Root cause | Proposed fix | Severity |
|---|---|---|---|---|---|---|
| S1 | Settings tab-nav icon not aligned in Arabic | `packages/ui/src/components/Tabs/index.tsx:53,70` (`rowDir`) | `07-settings.png` | SYSTEMIC-1 double-flip in the shared `Tabs`. | Apply SYSTEMIC-1 to `Tabs`; verify the tab icon sits on the logical-start side in AR. | ALIGN |
| S2 | Region Save → "No internet connection…" | `_components/settings/LanguagePanel.tsx:164-177` (Save → `useUpdateUserLanguage`); region is **UI-only** (`LanguagePanel.tsx:113,228-239,242-251`) | `07-settings.png` | **See §Functional bug REGION below.** The *language* Save 403 was fixed by the new `PUT /api/Users/Account/Language` endpoint (#128). **Region has NO backend endpoint** — it's UI-only and the Save button only persists language. If the user expects region to persist, there is no endpoint → needs a decision. The "No internet" text is the generic fallback when the client throws a non-`ApiError` ProblemDetails (the FE already mitigates the 403 case at `LanguagePanel.tsx:133-148`). | **DECISION (lead):** (a) region stays UI-only + Save shows success for language only (current intent — verify the error is gone now that the endpoint exists), or (b) add a real region/preferences endpoint, or (c) fold region into the language Save payload. Most likely the live error was the pre-#128 403; **re-verify live** that Save now succeeds. | FUNC + DEC |

### LAYOUT / SIDEBAR

| # | Finding | Current file(s) | AR ref | Root cause | Proposed fix | Severity |
|---|---|---|---|---|---|---|
| LS1 | Sidebar menu icons not aligned in Arabic | `_components/Sidebar.tsx:128,154,305-351,357-391` (`rowDir` on brand, child-selector, nav rows, logout) | `05-dashboard.png` sidebar, `ar-web-sidebar.html` | SYSTEMIC-1 double-flip across sidebar rows. | Apply SYSTEMIC-1; per `ar-web-sidebar.html` the nav **icon sits on the right** (logical start) in AR. | ALIGN |
| LS2 | Sidebar ChildSwitcher not working + TWO switchers | `_components/Sidebar.tsx:93-104,135-301` (sidebar selector wired to `useActiveChildStore`); `_layout.tsx:153-198` (top-nav switcher removed on wide; kept narrow `:204-229`) | `05-dashboard.png` | Already coded: the duplicate top-nav switcher was removed from the wide shell; the sidebar selector is wired to the same `useActiveChildStore`. **If still "not working" live**, candidate causes: the dropdown `position:fixed` backdrop + `position:absolute` menu z-index under the sticky shell header (`_layout.tsx:171` `zIndex:40`) vs dropdown `zIndex:100` — should be fine, but verify the dropdown isn't clipped by the sidebar's container or the content `ScrollView`; or the SYSTEMIC-1 flip makes the chevron/rows mis-hit. | Verify live; if broken, check z-index/clipping of the dropdown relative to the shell header + content scroll container; apply SYSTEMIC-1. Keep exactly ONE switcher (sidebar on wide, narrow header on mobile). | FUNC + ALIGN |
| LS3 | Add a Logout to the sidebar | `_components/Sidebar.tsx:106-107,356-391` (Logout row via `useSignOutAction`); hook `src/hooks/useSignOutAction.ts` | — | Already coded: sidebar Logout row calls `useSignOutAction` (best-effort server sign-out → `authStore.signOut()` → `router.replace('/(auth)/login')`), i18n `parent.nav.logout`. | Verify the Logout row renders + works live in both locales; confirm it's visible (it sits after the XP widget — check it's not pushed off-screen by `marginTop:auto` on the widget). | FUNC (already coded) |

### REGISTER — `screenshots/web-ar/03-register.png`

| # | Finding | Current file(s) | AR ref | Root cause | Proposed fix | Severity |
|---|---|---|---|---|---|---|
| R1 | "ولي أمر / وصي قانوني فقط…" banner alignment + border | `app/(auth)/register.tsx:170-216` (`ParentOnlyBanner`, `$purpleBorder` + `$purpleSoft`, `rowDir`) | `03-register.png` | Border uses `$purpleBorder` (#128 added the token). Alignment uses `rowDir` (SYSTEMIC-1). | Apply SYSTEMIC-1 to the banner row; designer confirms border color/width vs ref. | ALIGN |
| R2 | No need for a scroller on this page | `src/components/FormScaffold.tsx:196-211` (SplitFormScaffold ScrollView `showsVerticalScrollIndicator={false}`) | `03-register.png` | The split scaffold's own ScrollView already hides the indicator; if a visible/forced scroller remains it's likely the **content exceeding viewport height** (the 2-step wizard adds the add-child list/modal), or a nested scroll. Note register uses `variant="split"` — separate from the parent shell scroll. | Designer/frontend confirm the page fits without a dead scroll at standard heights; if content overflows, adjust spacing or allow natural single scroll without a forced indicator. | ALIGN |
| R3 | Email + password not rounded as design (AR+EN) | `RegisterForm.tsx:219-281` → `TextField` (`inputRadius=14`) | `03-register.png` | Same as L3 (SYSTEMIC-2 / input radius confirmation). | Same fix as L3. | RADIUS |
| R4 | **BIG ONE:** add-child should be a STEP OF A WIZARD, not a separate page | `register.tsx:11-16,47,155-163,218-324` (already a 2-step inline wizard reusing `AddChildModal`); separate `(onboarding)/add-child.tsx` page still exists | `03-register.png`, `ar-web-add-child-modal.html` | **See §Register wizard decision.** Already coded as an inline wizard, but this is the biggest decision and **partially revises PR #127** (which made onboarding add-child a dashed-tile→modal page). | Lead decision: confirm the inline-wizard approach (step 1 parent form, step 2 = add-child reusing the modal field set at parent-form width), and decide the fate of `(onboarding)/add-child.tsx` + the dashed-tile page. | DEC (biggest) |
| R5 | RegisterFeaturePanel not aligned in Arabic + margin issue | `app/(auth)/_components/RegisterFeaturePanel.tsx:24-94` (`rowDir` on bullets; `alignSelf="flex-start"` on icon) | `03-register.png` | SYSTEMIC-1 in bullet rows; the icon `alignSelf="flex-start"` is physical-start (should be logical) → margin/side wrong in AR. Panel side is `brandSide="end"` (right) via SplitFormScaffold. | Apply SYSTEMIC-1; change `alignSelf` to logical (`flex-start` is fine if the container dir flips, else gate by direction); designer confirms margins vs ref. | ALIGN |

---

## Functional-bug diagnoses (root cause + fix)

1. **Region Save → "No internet connection"** (S2). Root cause: the *language* write was 403 pre-#128 (the only language-write endpoint was AdminOnly); the generic transport error rendered as "No internet". #128 added a self-scoped `PUT /api/Users/Account/Language` and a FE mitigation (`LanguagePanel.tsx:133-148`). **Region itself has no endpoint** — it is UI-only by design. **Most likely the live error the user saw was the pre-#128 403; re-verify Save now succeeds.** If region must persist, that's a NEW backend endpoint decision (db-migration + backend-feature). Recommend: **re-verify first**, then lead decides region UI-only (keep) vs persisted (new endpoint).
2. **"Add Child" button not working in Arabic** (C3). Root cause: `flexWrap:"wrap"` + `row-reverse` wrapped the button to an un-clickable spot; HANDOFF says fixed to `nowrap`. If still broken live, residual = SYSTEMIC-1 double-flip mispositioning, or an overlay/z-index. Fix: apply SYSTEMIC-1 + verify hit-testability live.
3. **Sidebar ChildSwitcher broken + dual switchers** (LS2). Root cause: was a static card that only `router.push`'d; #128 wired it to `useActiveChildStore` and removed the top-nav duplicate on wide. If still broken live, check dropdown z-index/clipping vs the sticky shell header + content `ScrollView`, and the SYSTEMIC-1 flip. Fix: verify live; repair clipping/flip; keep one switcher.
4. **Edit-child popup** (C4). Already opens `AddChildModal` in edit mode in both locales. Verify it's the centered modal (not the orphaned `EditChildSheet`).

---

## Register wizard decision (FLAG — the biggest decision)

**State today (`main`):** `app/(auth)/register.tsx` is **already an inline 2-step wizard** (state `step: 1|2`): step 1 = parent form (`RegisterForm` with `onSuccess` → `setStep(2)`), step 2 = `AddChildStep` rendering a My-Children-style list + dashed `AddChildCard` + `AddChildModal` + Continue → `/(onboarding)/complete`. The separate `app/(onboarding)/add-child.tsx` page **still exists** (used as a `LinkedChildrenPanel` fallback) and is itself a dashed-tile→modal page (the **PR #127** shape).

**The decision the lead/user must confirm:**
- **Does register/onboarding add-child become the inline wizard step (step 2) reusing the modal's field set, replacing the separate `(onboarding)/add-child` page + dashed-tile+modal?** This is the user's stated desired end-state ("step 2 uses the SAME design + fields as the Add-Child popup and the SAME WIDTH as the parent form").
- **PR #127 interaction:** #127 made onboarding add-child a dashed-tile→modal **page**. The inline-wizard approach **partially revises #127.** Options:
  - **(A) Inline wizard is canonical (recommended, matches current code + user intent):** keep `register.tsx` 2-step wizard; in step 2, render the **Add-Child modal's field set inline** (not a separate dashed-tile that opens a modal) at the **parent-form width**, so step 2 visually equals the parent form. Decide whether `(onboarding)/add-child.tsx` is (i) deleted, (ii) kept only as the post-login "add more children" entry, or (iii) redirected into the wizard. Today the wizard still uses a dashed-tile→modal *inside* step 2 (`AddChildStep`), which the user may want replaced by inline fields.
  - **(B) Keep the separate page (revert wizard):** contradicts the user's ask — not recommended.
- **Width parity:** the split form column is `maxWidth: 500` (`FormScaffold.tsx:205`); the add-child modal is `width: 480`. If step 2 reuses the modal's field set inline, ensure it fills the parent-form column width, not the modal's fixed 480.

**Recommendation:** confirm **(A)** with a clarification: *does step 2 inline the modal's fields directly (no nested dashed-tile/modal), or keep the dashed-tile→modal inside step 2?* The user's wording ("step 2 uses the SAME design + fields as the Add-Child popup") suggests **inline the fields**. This is a **design-pattern-adjacent decision** — designer proposes the step-2 layout; lead approves before frontend builds.

---

## Affected modules & data (new vs existing)

- **Frontend only** for all alignment/radius findings — no new entities, no DB. Touch `apps/student-app/app/(auth)/`, `app/(parent)/`, `app/(onboarding)/`, `src/components/AddChildModal.tsx`, `FormScaffold.tsx`, and shared `packages/ui` (`TextField`, `ChildCard`, `Tabs`, `Button`, `Avatar`, `Select`) + `packages/design-system` (tokens — **no value change expected**).
- **Backend — only if** the lead chooses to persist **region** (S2 option b/c). That would be a new self-scoped preferences endpoint in the **Identity** module, mirroring the existing `PUT /api/Users/Account/Language` (`UpdateMyPreferredLanguageCommand` pattern). **Existing entity:** `User` (would add a region/locale-region preference field). **No new module.** Default position: **region stays UI-only → no backend.**
- **Existing backend already in place (no work):** `PUT /api/Users/Account/Language` (`UpdateMyPreferredLanguageCommand`, self-scoped, `[Authorize]`); `Add-Child`/`Update-Child` (country optional, client omits for children); profile/avatar endpoints.

---

## Handoff → db-migration

**Likely NO migration.** Only triggered if the lead chooses **region persistence (S2 option b/c)**:
- Entity: `User` (Identity module) — add an optional region/locale-region preference column (nullable, backfill null). Mirror the `PreferredLanguage` precedent.
- No cross-module FK; no new aggregate. If region is UI-only (recommended/default), **skip this handoff entirely.**

## Handoff → backend-feature

**Likely NO backend work.** Only if region persistence is chosen:
- Command: self-scoped `UpdateMyRegion`/extend `UpdateMyPreferredLanguageCommand` — user resolved from JWT (no IDOR/mass-assignment), `[Authorize]`, mirrors `AccountController.UpdateMyPreferredLanguage` (HANDOFF). Returns `BaseResponse<T>` via `NewResult`. Add to the committed `swagger.json` + regen api-client (`gen:api` works off the snapshot; `refresh:swagger` needs a live backend).
- **Otherwise:** the only backend touch is re-verifying the existing language Save endpoint resolves the S2 error live (api-tester can cover `PUT /api/Users/Account/Language`, noted as not-yet-covered in HANDOFF).

## Handoff → designer (REQUIRED — UI surface)

Produce a Design Spec at `design-system/ui_kits/parent-dashboard/parent-web-rtl-polish.md` that:
1. **Resolves SYSTEMIC-1** — pick the canonical RTL row strategy (recommend option A: logical `row` + single dir flip) and specify, per listed component, the expected visual side of icon vs label/control in **both** `ar` and `en`, grounded in `screenshots/web-ar/{02,03,04,05,07}.png` + `preview/ar-web-*.html`.
2. **Resolves SYSTEMIC-2** — confirm the intended radius per surface from SKILL.md rule 4 + screenshots (expect: inputs 8 or 14 — decide L3/R3; buttons 16; cards 20; modals 24). **No token-value change unless the screenshots prove the bucket is wrong.** Flag that the bug is render, not value.
3. Specifies the **register step-2 layout** (inline modal field set vs dashed-tile) at parent-form width (R4).
4. Per-surface annotated deltas for Login (L1-L4), Children (C5-C7), Overview (O1-O5), Settings (S1), Sidebar (LS1), Register (R1,R2,R5).
5. Calls out the resolved **no-country-for-children** rule (C1) and the **region UI-only** default (S2) so they aren't re-litigated.

## Handoff → frontend (batched by surface)

Consume the Design Spec. Batches (independent surfaces → can parallelize; shared-package edits serialized):
- **FE-Batch-SHARED (serialize first):** `packages/ui` `Tabs`, `ChildCard`, `TextField` (radius confirm), `Button`, `Avatar`, `Select` — apply the SYSTEMIC-1 strategy + SYSTEMIC-2 render fix. These are consumed by all surfaces, so land first. (Per [docs/dev/PARALLELISM.md], shared-package edits are serialized.)
- **FE-Batch-AUTH:** `LoginForm`, `LoginBrandPanel`, `loginParts`, `RegisterForm`, `RegisterFeaturePanel`, `PersonaToggle`, `register.tsx` (+ wizard step-2 layout R4), `FormScaffold` scroll (R2).
- **FE-Batch-CHILDREN:** `MyChildrenWeb`, `ChildDashboardCard`, `AddChildCard`, `AddChildModal` (photo row C5), `(onboarding)/add-child.tsx` disposition per R4.
- **FE-Batch-OVERVIEW:** `OverviewWeb`, `FocusAreasCard`, `RecommendationsCard` (+ card radius O4).
- **FE-Batch-SETTINGS-LAYOUT:** `LanguagePanel` (S2 verify), `SettingsWeb`, `Sidebar` (LS1/LS2/LS3 verify), `_layout.tsx` (confirm single switcher).
- **Approach:** mirror existing shapes; **no new design patterns** without lead approval (the RTL-strategy normalization IS a convention decision — get designer/lead sign-off, CLAUDE.md rule 8). Tokens only; `Successed` envelope unaffected (FE). Keep brand/Latin strings LTR; numerals via `Intl`.

## Handoff → frontend-e2e-tester (REQUIRED — the step PR #128 skipped)

Drive the live web PWA with Playwright in **both `ar`(RTL) and `en`(LTR)**:
- For each surface, assert the icon/label/control lands on the side matching the AR screenshot (this is the regression that typecheck/lint missed). Visual/position assertions, not just presence.
- Functional: Add-Child button clickable in AR (C3); Edit opens the modal in AR+EN (C4); sidebar ChildSwitcher selects + only one switcher on wide (LS2); sidebar Logout clears session → login (LS3); **region/language Save succeeds with no "No internet" error** (S2); register 2-step wizard advances + step 2 width parity (R4).
- Card radius visible (O4/L3/R3) — capture screenshots for the reviewer to diff against `screenshots/web-ar/*`.
- Needs the live stack (backend :5080 + Expo web :8081 per HANDOFF "Testing — E2E (Playwright)").

## Handoff → reviewer

Gate against the §Acceptance criteria + the per-finding table + the e2e visual results. **Hard gate:** the e2e RTL/LTR visual smoke must pass (do not accept typecheck/lint-only — that's exactly how #128 shipped a non-working fix). Confirm no token-value regressions in `packages/design-system`. Confirm the no-country rule and single-switcher invariant hold.

---

## Open questions / assumptions / risks

**Open questions (lead → user before/early in planning):**
1. **[BLOCKING the framing]** Confirm these findings are against the **current `main`** (post-#128 + onboarding-modal merge), i.e. the user re-tested the merged result. If they were captured **before** #128 merged, several items are already fixed and the task collapses to "live-verify + repair the double-flip." (Strong evidence says post-merge re-test — the code already contains every described fix.)
2. **L1:** For the login email field — does the user want the **value** right-aligned (conflicts with the LTR-for-email SKILL.md rule) or the **label/placeholder**? Likely label.
3. **L3/R3:** Exact input radius from the AR reference — keep `14`, or move to `$sm`=8?
4. **S2 (region):** Re-verify the "No internet" error is gone now that the language endpoint exists. If region must **persist**, approve a new self-scoped region endpoint (Identity) — else region stays UI-only.
5. **R4 (register wizard — biggest):** Confirm the inline 2-step wizard is canonical; decide whether step 2 **inlines the modal's fields** (vs dashed-tile→modal) and the fate of `(onboarding)/add-child.tsx`. Acknowledge this revises **PR #127**.
6. **C1:** Confirm children stay **country-less** (the finding's "it should have country" contradicts the settled rule — assume no country).
7. **RTL strategy (SYSTEMIC-1):** Approve the canonical row strategy (recommend option A) — this is a convention decision (CLAUDE.md rule 8).

**Assumptions:**
- Findings are against post-merge `main`; the work is verify/repair + the few decisions, not greenfield.
- No backend work unless region persistence is chosen (default: none).
- The dominant fix is SYSTEMIC-1 (double-flip) + SYSTEMIC-2 (radius render), which together resolve most findings.

**Risks:**
- **Double-flip normalization has wide blast radius** — touching `packages/ui` shared components affects child-app screens too. Land shared edits first, and the e2e must cover both parent web AND a spot-check of child screens that consume `Tabs`/`ChildCard` to avoid regressing RTL elsewhere.
- **SYSTEMIC-2 render diagnosis is uncertain** — if Tamagui token→CSS is the cause, the fix may need a `tamagui.config`/primitives change with broad effect; treat carefully and re-verify all radii after.
- **Repeating #128's mistake** — if this pass again skips the live RTL smoke, it ships non-working fixes a third time. The e2e gate is non-negotiable.
- **`(onboarding)/_components/EditChildSheet.tsx` is orphaned dead code** (HANDOFF) — decide delete vs keep during the children batch.

---

## Recommended pipeline order (first cut — `planner` finalizes)

1. **analyzer** (this brief) → **lead clarifies the 7 open questions** (esp. Q1 framing, Q5 wizard, Q7 RTL strategy) BEFORE planning the build.
2. **planner** — structure as **verify → repair → re-verify**, not greenfield. Sequence: shared-package batch first (serialized), then parallel surface batches, then mandatory live e2e gate.
3. **designer** (REQUIRED) — Design Spec resolving SYSTEMIC-1 + SYSTEMIC-2 + register step-2 + per-surface deltas. Runs before any frontend batch.
4. **frontend** — FE-Batch-SHARED (first, serialized) → then FE-Batch-AUTH ‖ FE-Batch-CHILDREN ‖ FE-Batch-OVERVIEW ‖ FE-Batch-SETTINGS-LAYOUT (parallel where independent).
5. **(db-migration → backend-feature → api-tester)** — ONLY if region persistence is chosen; otherwise skipped. If chosen, this is security-sensitive (user data) → **security-auditor** before the gate.
6. **frontend-e2e-tester** (REQUIRED) — live AR(RTL)+EN visual + functional smoke across all surfaces (the #128 gap).
7. **reviewer** — gate on acceptance criteria + e2e visual results (hard gate; no typecheck/lint-only pass).
8. **committer** — after reviewer PASS only; confirm working branch (`feat/parent-web-rtl-polish` exists) with the lead; conventional message; push + open PR; never on `main`.
