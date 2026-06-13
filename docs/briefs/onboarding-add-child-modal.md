# Pipeline Brief — Onboarding Add-Child Modal (replace inline form with the design-system modal)

> **No formal user story.** There is no `user-stories/<phase>/*.md` for this work. **This brief's spec section IS the source of truth**, per the lead's instruction. Traceability below maps to the originating stories (P1-03-FE onboarding add-child, P1-04/P1-12-FE My-Children) for context only — this is a frontend-only refactor of an already-shipped flow, not a new feature.

## Summary & traceability
- **Task (1 line):** Replace the onboarding add-child screen's always-visible inline form + in-memory draft list + batch "submit all at end" flow with a **My-Children-style screen** (heading + list of already-added children + a dashed "Add a child" tile) that opens the **existing `AddChildModal`** popup; each modal confirm creates the child immediately, and a primary **Continue** button (enabled once ≥1 child exists) routes to `/(onboarding)/complete`.
- **Originating context (not a contract):** onboarding add-child = P1-03-FE; the modal + My-Children pattern it reuses = P1-04-FE / P1-12-FE (parent dashboard). Design Spec Screen 5 (current onboarding form) is being superseded by the My-Children pattern (`design-system/ui_kits/parent-dashboard/align-my-children.md` Section E/F).
- **FR / BRD:** No new FR. Same capability as **SRS parent-driven onboarding / add-child** (product decision: parents register + add children; students don't self-register). BRD goal **G1** (activation/onboarding) — reduce friction by reusing the polished modal. No backend change; `POST /api/Parent/Add-Child` is unchanged.
- **Scope:** Frontend only (`apps/student-app` + `packages/shared` i18n). No `db-migration`, no `backend-feature`, no `api-tester`. Pipeline = **designer (light) → frontend → frontend-e2e-tester → reviewer**.

## Business context & value
- **Who benefits:** the registering **parent**. The dashboard already has a beautiful, Skill-8-compliant Add-Child modal (plant-emoji grade tiles, flag tiles, photo upload, password strength, personalized CTA). The onboarding screen currently uses a separate, plainer inline form (`AddChildForm` + `GradePicker` dropdown + text country field). This creates **two divergent add-child UIs** for the same action.
- **Value:** one consistent, on-brand add-child experience across onboarding and dashboard; less code to maintain (one modal vs. modal + inline form + draft-list machinery); the onboarding "add several children" goal is preserved while removing the confusing two-step "add to a list, then submit the list" model.
- **Success measure:** the onboarding add-child step looks and behaves like the design/ui-kit modal; a parent can add one or more children during onboarding and reach Complete; no regression in RTL/ar+en, validation, or auth.

## Acceptance criteria (testable)

### AC group 1 — Screen layout (My-Children style)
- **AC-1.1** The onboarding `add-child` screen renders: a **heading + subtitle** (onboarding-appropriate copy), a **list of children already added** rendered as cards, and a **dashed "Add a child" tile/CTA**. There is **no longer an always-visible inline add-child form** on the screen.
- **AC-1.2** When **0 children** exist, the screen shows the dashed "Add a child" tile (empty state) and the **Continue button is disabled**.
- **AC-1.3** The added-children list is driven by **`useMyChildren()`** (TanStack Query) — not a local `drafts` array. Children appear/refresh because `useAddChild` already invalidates `queryKeys.family.myChildren()` on success.
- **AC-1.4** Tapping the dashed tile (or an "Add child" CTA) opens the **`AddChildModal`** popup over a scrim. Tapping the scrim / ✕ / Cancel closes it without creating a child.

### AC group 2 — Immediate create + multiple children
- **AC-2.1** Confirming the modal calls **`useAddChild` once** (the modal already does this) → `POST /api/Parent/Add-Child`. On success the modal **closes**, the query invalidates, and the new child **appears in the list immediately**.
- **AC-2.2** The parent can **re-open the tile and add more children** before Continue; each is created immediately and appended to the list. There is **no batch "submit all" step** and **no draft "discard the list" semantics** — created children persist server-side the moment the modal confirms.
- **AC-2.3** Modal validation/error behavior is unchanged from the dashboard: required-field errors block submit; server errors (e.g. duplicate email) surface inside the modal via its `ServerErrorBanner`; on error the modal stays open and no child is added.

### AC group 3 — Continue / Finish
- **AC-3.1** A primary **Continue/Finish** button is **disabled while `useMyChildren().data.length === 0`** and **enabled once ≥1 child exists**.
- **AC-3.2** Pressing Continue routes to **`/(onboarding)/complete`** (unchanged destination). The existing Complete screen → "Go to Dashboard" → `/(parent)` flow is untouched.
- **AC-3.3** (Guard nuance — see OQ-1) Navigating to Complete and onward must not bounce the parent back to onboarding. `useAuthRoute` routes on `me.hasChildren`; confirm the chosen approach keeps the parent moving forward (recommended: refetch/invalidate `queryKeys.auth.me()` after the first successful add, OR rely on Complete→`/(parent)` where the guard re-resolves).

### AC group 4 — Reuse, RTL, tokens (constraints)
- **AC-4.1** The screen reuses the **existing `AddChildModal`** component (NOT a new modal, NOT a re-skinned `AddChildForm`). No new modal/overlay pattern is introduced.
- **AC-4.2** Tamagui + design-system tokens only; RTL via logical props (`flexDirection` rowDir, `writingDirection`, logical start/end). ar + en both render correctly with no raw i18n keys visible.
- **AC-4.3** The onboarding wizard chrome (`_layout.tsx`: step label "Step 1 of 2" + `ProgressSteps`, back hidden on step 1) is preserved; the modal renders **above** the chrome.
- **AC-4.4** Dead code is removed/contained: anything no longer referenced **after** this change is deleted; anything still shared (see Affected modules) is **kept** and must still compile/work for the dashboard.

## Affected modules & data
**Surface:** `apps/student-app/app/(onboarding)/` + shared component placement + `packages/shared` i18n. No entities, no DB, no API. `useAddChild` / `useMyChildren` / `AddChildModal` already exist and are unchanged in signature.

**Files to CHANGE:**
| File | Change |
|---|---|
| `apps/student-app/app/(onboarding)/add-child.tsx` | Rewrite: remove `drafts`/`submitAll`/`partialFailure`/`EditChildSheet`/`AddChildForm`/`childListTypes` usage; render heading + `useMyChildren`-driven list + dashed add tile + modal (visibility via local `useState`) + a Continue button gated on child count. |
| `packages/shared/src/i18n/resources.ts` | Add the onboarding-modal-context keys listed in the i18n section (en + ar). |

**Files to MOVE or REUSE (component-placement decision — see Handoff → frontend, item 1):**
| File | Decision |
|---|---|
| `apps/student-app/app/(parent)/_components/AddChildModal.tsx` | **Reuse.** Recommended: **move to a shared location** so both `(parent)` and `(onboarding)` import one copy. See recommendation + OQ-2. |

**Files possibly REMOVED — verify references first (do NOT blind-delete):**
| File | Status |
|---|---|
| `apps/student-app/app/(onboarding)/_components/AddChildForm.tsx` | **Still imported by `EditChildSheet.tsx`** (onboarding in-memory edit mode). After this change the onboarding screen no longer uses `EditChildSheet`'s in-memory mode, BUT `EditChildSheet` itself is still used by the dashboard (`MyChildrenWeb.tsx`) in **backend mode** (which renders `EditChildFields`, not `AddChildForm`). **→ `AddChildForm` becomes referenced ONLY by `EditChildSheet`'s in-memory branch.** Decide: keep `AddChildForm` + `EditChildSheet` as-is (lowest risk), OR if you also prune `EditChildSheet`'s dead in-memory branch, then `AddChildForm` + `childListTypes.ts` can be deleted. **Recommendation: leave `AddChildForm`/`EditChildSheet`/`childListTypes.ts` in place this PR** (they are not on the onboarding screen anymore and `EditChildSheet` is load-bearing for the dashboard); only remove the onboarding screen's imports of them. Flag the now-orphaned in-memory `EditChildSheet` branch as cleanup backlog. |
| `apps/student-app/app/(onboarding)/_components/EditChildSheet.tsx` | **KEEP — do NOT delete.** Imported by `apps/student-app/app/(parent)/_components/MyChildrenWeb.tsx` (line 27) for backend edit mode. Only the onboarding screen stops importing it. |
| `apps/student-app/app/(onboarding)/_components/childListTypes.ts` | Referenced only by the onboarding `add-child.tsx` and `EditChildSheet`'s type import surface. Safe to delete **only if** nothing else imports `ChildDraft`/`nextLocalId` after the rewrite — verify with grep before deleting. |

> **Load-bearing reconciliation:** `EditChildSheet` lives under `(onboarding)/_components/` but is **cross-imported by `(parent)`**. That is an existing coupling, not introduced here. Do not break it. This also means "remove the inline-form components" is NOT a clean delete — the brief's safe stance is *de-reference from onboarding, keep the files*.

**Cross-cutting observation (raise as OQ-3):** `MyChildrenWeb.tsx` (dashboard) currently routes its "+ Add Child" button (line 151) and dashed `AddChildCard` (line 211) to `router.push('/(onboarding)/add-child')` — i.e. the dashboard "add child" sends the parent **into the onboarding screen**. Meanwhile the dashboard `_layout.tsx` already mounts `AddChildModal` and opens it via `activeChildStore.openAddChild()` from the header `ChildSwitcher`. So the dashboard has **two add-child entry points with different behaviors** (modal vs. navigate-to-onboarding). This brief does not require fixing that, but the planner/lead should decide whether, once onboarding uses the modal, the dashboard's `MyChildrenWeb` add-child buttons should also switch to `openAddChild()` for consistency. Flagged, not assumed.

## Handoff → db-migration
**None.** No schema change. Skip this stage.

## Handoff → backend-feature
**None.** No commands/queries/endpoints/DTOs change. `POST /api/Parent/Add-Child` and `GET /api/Users/Parent/My-Children` are consumed as-is via existing hooks. Skip this stage. (No `api-tester` either.)

## Handoff → frontend

### 1. Component reuse + placement — RECOMMENDATION (pattern decision, CLAUDE.md rule 8)
`AddChildModal` currently lives at `apps/student-app/app/(parent)/_components/AddChildModal.tsx`. Both options below **reuse the same component** (no new modal):

- **Option A (recommended): move it to a shared, group-agnostic location** so `(parent)` and `(onboarding)` both import one copy — e.g. `apps/student-app/src/components/AddChildModal.tsx` (alongside `ServerErrorBanner`, which the modal already imports from `../../../src/components/`). Update the two existing import sites in `(parent)/_layout.tsx` (lines ~47, 192, 223) and the new `(onboarding)/add-child.tsx`. Fix the modal's relative imports (`../../../src/...` → `../...`).
  - **Why:** the modal is no longer parent-dashboard-specific; co-locating it under `src/components` matches the existing convention for cross-group UI and avoids `(onboarding)` reaching into `(parent)/_components` (route-group internals). This is a **file move + import update, not a new abstraction** — but per rule 8 it is still a structural decision, so it is called out here for the lead to approve rather than executed silently.
- **Option B (lower-churn fallback): leave the modal in `(parent)/_components/` and import it from onboarding** via a relative path. Works, but `(onboarding)` then depends on `(parent)` route-group internals, which is the kind of coupling that already bit us with `EditChildSheet` (see above).

**Frontend agent must NOT pick unilaterally** — the planner should surface "Option A vs B" as the one pattern decision in this story and get lead/user sign-off (see OQ-2). Everything else (rendering a list, a Stack-based dashed tile, a Continue button) mirrors existing shapes and needs no approval.

### 2. Modal-open state in onboarding
- The dashboard opens the modal via the `activeChildStore` Zustand signal (because Expo Router `<Slot>` can't forward props). The onboarding screen is a **single screen, not a `<Slot>` parent**, so it can hold the modal directly with **local `useState`** (`const [addOpen, setAddOpen] = useState(false)`), pass `visible={addOpen} onClose={() => setAddOpen(false)}`. **Do not reuse `activeChildStore`** for onboarding — that store is dashboard-scoped (persists active child id). Keep onboarding's modal state local.

### 3. Screen composition (new `add-child.tsx`)
Mirror `MyChildrenWeb.tsx`'s list+add-card pattern, simplified for onboarding (no hero, no period select, no Send Report, no per-child stats):
- **Header:** `Text` heading + subtitle (onboarding copy — see i18n). Keep the existing `accessibilityRole="header"`, `writingDirection={direction}`.
- **List of added children:** map `useMyChildren().data` → child cards.
  - **Card choice (decide — OQ-4):** the simplest reuse is the design-system **`ChildCard` from `@learnexia/ui`** in `variant="status"`/`"success"` (already imported by the old screen) showing name + a grade/language meta line built from `LinkedChildResponse` (`fullName`, `grade`, `language`). The dashboard's `ChildDashboardCard` is heavier (stats/mastery stubs, edit/view actions) and is overkill for onboarding. **Recommendation: use `ChildCard` (status variant) or a thin local card** — confirm with the designer which matches the "child-cards-as-added" pattern in `align-my-children.md`/`index.html` best. No edit/remove affordances are required on these cards in onboarding (children are real now; editing lives on the dashboard) — but if the designer wants an edit affordance, that's an explicit scope add.
  - **Loading/error:** `useMyChildren` exposes `isLoading`/`isError`. On first paint after register the list is empty (expected). Handle `isError` minimally (the children were still created; a transient list-fetch error shouldn't block — show a retry or just the add tile).
- **Dashed "Add a child" tile:** reuse **`AddChildCard` from `(parent)/_components`** (props `{ onPress }`) OR a local equivalent. Same placement consideration as the modal (it's a `(parent)/_components` file). For onboarding, `onPress={() => setAddOpen(true)}` (open modal) instead of the dashboard's `router.push('/(onboarding)/add-child')`. **Note `AddChildCard`'s subtitle copy** ("Set their grade, language, and login email") is generic enough to reuse; confirm with designer.
- **Modal:** `<AddChildModal visible={addOpen} onClose={() => setAddOpen(false)} />` at the end of the screen tree.
- **Continue button:** `@learnexia/ui` `Button variant="primary" size="full"`, `disabled={children.length === 0}`, `onPress={() => router.replace('/(onboarding)/complete')}`. Give it a stable **testID** (e.g. `onboarding-continue`).

### 4. testIDs (load-bearing for e2e — the old ones are being removed)
The rewrite **removes** these onboarding testIDs: `add-child-form-card`, `add-child-to-list`, `add-child-submit` (the batch button), the onboarding `add-child-name/email/password/grade/learning-language/app-language/country` (those now live inside the modal with the modal's own testIDs), `child-card-{localId}` / `child-card-edit-*` / `child-card-remove-*`, `edit-child-sheet`/`edit-child-save` (onboarding usage). Provide a clear, documented new set, e.g.:
- `onboarding-add-child-tile` (the dashed tile / open-modal CTA)
- `onboarding-children-list` (the added-children list container)
- `onboarding-child-card-{id}` (each added child card; key off `LinkedChildResponse.id`)
- `onboarding-continue` (the Continue/Finish button)
- The modal already exposes: `add-child-modal`, `add-child-name`, `add-child-email`, `add-child-password`, `grade-tile-{1..6}`, `app-lang-tile-{ar|en}`, `add-child-learning-language`, `add-child-country`, `add-child-submit` (modal CTA), plus Cancel/✕ via `aria-label`. **Heads-up:** the modal's CTA testID is also `add-child-submit` — the same string the old onboarding batch button used. E2E must now resolve `add-child-submit` **inside the modal** (it means "confirm this one child"), not as the old "submit all" button. Document this in the spec.

### 5. Auth token confirmation
- The parent is **signed in throughout onboarding** (registration returns the access token; `useAuthRoute` routes the just-registered 0-child parent to `/(onboarding)/add-child`; the typed client attaches the JWT). `useAddChild` resolves the acting parent **server-side from the JWT** (never in the body). The modal's `POST /api/Parent/Add-Child` therefore works in onboarding exactly as on the dashboard. **Confirmed via the P1-03-FE e2e flow** (register → add-child, then `POST /api/Parent/Add-Child` with the bearer token succeeds). No token work needed.

## i18n keys needed (en + ar)
The modal already has its full key set under **`parent.addChildModal.*`** (en `resources.ts` ~638; ar ~1815) — title "Add a child"/"أضف طفلاً", subtitle "They'll log in with the email you set"/"سيسجّل الدخول بالبريد الذي تحدّده", CTA "Add {{name}} →", etc. **Decision (OQ-5):** reuse `parent.addChildModal.*` **as-is** in onboarding (the copy is context-neutral and correct), OR add onboarding-specific overrides. Recommendation: **reuse as-is** — the modal copy reads fine during onboarding; do not fork strings unless the designer insists on onboarding-flavored wording.

New keys required **for the onboarding screen chrome** (the modal copy is NOT changed):

| Key | EN | AR |
|---|---|---|
| `onboarding.addChild.title` (REUSE existing, line 214) | `Add your child` | `أضف طفلك` — *consider rewording to plural "Add your children" since multiple are now added inline; see OQ-6* |
| `onboarding.addChild.subtitle` (REUSE existing, line 215) | `Fill in your child's details. You can add more children after.` | (ar ~1391) — *reword: the "fill in details" wording assumed an inline form; update to e.g. "Add each of your children. You can add more than one."* |
| `onboarding.addChild.continue` (**NEW**) | `Continue` | `متابعة` |
| `onboarding.addChild.addTile` (**NEW**, or reuse `parent.myChildren.addCardTitle`) | `Add a child` | `أضف طفلاً` |
| `onboarding.addChild.addTileSub` (**NEW**, or reuse `parent.myChildren.addCardSubtitle`) | `Set their grade, language, and login email` | `حدّد صفه ولغته وبريد دخوله` |
| `onboarding.addChild.listLabel` (REUSE existing, line 235; semantics shift from "to add" → "added") | `Children to add ({{count}})` → reword to `Children added ({{count}})` | `الأطفال المراد إضافتهم ({{count}})` → reword to `الأطفال المُضافون ({{count}})` |
| `onboarding.addChild.emptyHint` (**NEW**, optional) | `Add your first child to continue` | `أضف طفلك الأول للمتابعة` |

**Keys that become unused** after the rewrite (do NOT delete unless confirmed unused elsewhere — `addChildSchema`/`EditChildSheet` may still reference some): `onboarding.addChild.addToListButton`, `onboarding.addChild.submitButton`, `onboarding.addChild.partialFailureBanner`, and possibly the per-keystroke field labels if `AddChildForm` is later removed. **Recommendation: leave them** this PR (they're still referenced by the retained `AddChildForm`/`EditChildSheet`); prune in the `AddChildForm` cleanup backlog. Reuse-vs-add for the tile/list keys is the designer's call (OQ-5/OQ-6).

> All AR numerals (e.g. the `{{count}}` in `listLabel`) must render via `Intl.NumberFormat('ar-EG')` per existing convention — `ChildCard`/the list label should use the locale formatter already used elsewhere.

## E2E specs to update (MUST change for the modal flow)
The inline-form testIDs and the batch-submit flow are going away, so these break and must be rewritten to the modal flow:
- **`tests/e2e/specs/P1-03-FE.spec.ts`** — **primary impact.** Nearly every test drives the old flow: `fillChildFormAndAddToList()` helper (uses `add-child-name/email/password/grade/learning-language/app-language`, `add-child-to-list`, `add-child-form-card`), `submitDraftList()` (uses the old `add-child-submit` batch button + waits for nav off `add-child`), and assertions on `child-card-{localId}` drafts, the draft `listLabel`, the `submitButton` count label, in-memory edit (`edit-child-sheet`), draft remove, and partial-failure banner (FE-TC-01..21). Rewrite to: open the modal via `onboarding-add-child-tile` → fill the **modal** fields (`add-child-name`, `grade-tile-N`, `app-lang-tile-*`, `add-child-learning-language`, `add-child-country`) → confirm via the modal's `add-child-submit` → assert the child appears in `onboarding-children-list` immediately → add a second child the same way → assert `onboarding-continue` enables → press it → land on `/complete`. The validation/duplicate-email cases (FE-TC-05/06/07) move **inside the modal** (errors shown in the modal, modal stays open). The in-memory edit/remove draft cases (FE-TC-03/04) are **removed** (no drafts anymore) — note in the spec why. FE-TC-15..18 (My-Children states) are largely unaffected but verify they still pass.
- **`tests/e2e/specs/P1-04-FE.spec.ts`** and **`tests/e2e/specs/P1-12-FE.spec.ts`** — touch My-Children add/edit; verify they still pass. If the planner/lead also switches the dashboard's `MyChildrenWeb` add buttons to `openAddChild()` (OQ-3), these need updates too; if not, they should be unaffected (dashboard still navigates to onboarding, which now shows the modal screen).
- **`tests/e2e/specs/parent-final-capture.spec.ts`, `tests/e2e/specs/p12-screenshots.spec.ts`, `tests/e2e/specs/carryover-d1.spec.ts`** — grep hits for `add-child`/`onboarding`/`AddChild`; review for any onboarding-add-child assertions/screenshots and update if they exercise the inline form.
- **QC docs:** `docs/qc/P1-03-FE/frontend-test-cases.md` is the case source the spec implements; if `qc-test-designer` is run, it should re-author these cases for the modal flow. Otherwise the e2e tester updates the spec directly and notes deltas.

## Open questions / assumptions / risks

**Open questions (recommend the lead resolve before the frontend batch):**
- **OQ-1 (guard / forward-progress):** After the **first** successful add via the modal, `useAddChild` invalidates `myChildren` but **not** `queryKeys.auth.me()`, so `useAuthRoute`'s `me.hasChildren` stays stale (still `false`). Does that matter? The parent is *on* `/(onboarding)`, and `useAuthRoute` only redirects 0-child parents *to* onboarding (it won't push them off it). Continue → `/(onboarding)/complete` → "Go to Dashboard" → `/(parent)` is where the guard re-resolves. **Risk:** if `me` is refetched mid-onboarding and `hasChildren` is still cached false while the parent is already past add-child, no harm (they're moving forward). **Recommendation:** to be safe and correct, **invalidate/refetch `queryKeys.auth.me()` after the first successful add** (small addition in the screen's modal `onClose`/success path, or extend `useAddChild`'s `onSuccess`). Confirm with lead whether to touch `useAddChild` (shared) or keep it screen-local.
- **OQ-2 (component placement — pattern decision, rule 8):** Approve **Option A (move `AddChildModal` to `src/components/`)** vs **Option B (import from `(parent)/_components`)**? Recommended: A. Needs lead/user sign-off because it's a structural move touching the dashboard's import sites.
- **OQ-3 (dashboard consistency — scope boundary):** `MyChildrenWeb`'s "+ Add Child" button and dashed `AddChildCard` currently `router.push('/(onboarding)/add-child')` instead of opening the already-mounted modal. Once onboarding becomes a modal screen this is functionally OK but inconsistent (dashboard has both modal-via-ChildSwitcher and navigate-to-onboarding). **In scope to also switch those to `openAddChild()`, or leave for a follow-up?** Default assumption: **out of scope** (leave as-is) unless the lead says otherwise.
- **OQ-4 (added-child card component):** Use `@learnexia/ui` `ChildCard` (status variant) for the added list, a thin local card, or the heavier `ChildDashboardCard`? Recommended: `ChildCard`/thin local (no stats, no edit/remove). Designer to confirm against `align-my-children.md` / `index.html` "My Children" cards.
- **OQ-5 (modal copy):** Reuse `parent.addChildModal.*` strings as-is in onboarding (recommended), or add onboarding-specific overrides? Designer to confirm.
- **OQ-6 (screen heading copy):** The existing `onboarding.addChild.title`="Add your child" / subtitle assume a single inline form. Reword to plural / "add each child" semantics? Recommended yes (minor copy update, en+ar).

**Assumptions:**
- Frontend-only; no backend/DTO change (the modal already calls the existing endpoint).
- `useAddChild`'s existing `myChildren` invalidation is sufficient to make added children appear in the onboarding list without extra wiring.
- The onboarding screen keeps using `/(onboarding)/complete` as the next step and the existing `_layout.tsx` chrome.
- `AddChildForm` / `EditChildSheet` / `childListTypes.ts` are **retained** (de-referenced from onboarding, not deleted) because `EditChildSheet` is still used by the dashboard.

**Risks:**
- **R-1 (testID churn):** P1-03-FE is large and almost entirely coupled to the old inline-form/draft testIDs and the batch-submit nav pattern. Budget real time for the e2e rewrite; treat it as part of this story's done-criteria (frontend-e2e-tester gate).
- **R-2 (shared-file edits — serialize per PARALLELISM.md):** `packages/shared/src/i18n/resources.ts` is a high-traffic shared file; if other stories run in parallel, serialize the i18n edit. The `AddChildModal` move also edits `(parent)/_layout.tsx` (a shared layout) — coordinate.
- **R-3 (modal CTA testID collision):** modal CTA and old batch button both used `add-child-submit`. After removal there's only one meaning, but be explicit in the e2e so the selector resolves within the modal.
- **R-4 (dead-code over-deletion):** deleting `AddChildForm`/`EditChildSheet`/`childListTypes` without grepping references will break the dashboard build (`MyChildrenWeb` imports `EditChildSheet`). The reviewer must confirm a clean `tsc`/build.
- **R-5 (RTL/native parity):** the modal uses several web-only constructs (hidden `<input type="file">`, `backdropFilter`, `vh`/`vw`, `boxShadow` strings). It already guards `Platform.OS === 'web'` for the file input and constrains width; onboarding currently targets web PWA, so this is fine, but the frontend agent should not regress native (the modal already handles `Platform.OS !== 'web'` fallbacks).

## Recommended pipeline order (first cut — the planner finalizes)
1. **designer (light)** — produce/append a short Design Spec for the onboarding add-child *screen* (heading/subtitle, dashed tile, added-children list/card choice, Continue placement, empty/loading states, RTL). The **modal itself is already designed/built** (`align-my-children.md` Section E/F + `AddChildModal.jsx` + `web-add-child-modal.html`) — reference it, don't redesign. Resolve OQ-4/OQ-5/OQ-6 here. *Cannot start the frontend batch until OQ-1 + OQ-2 are answered by the lead.*
2. **frontend** — single batch (no DB/BE): (a) component placement per approved OQ-2; (b) rewrite `add-child.tsx`; (c) add i18n keys; (d) optional `me` invalidation per OQ-1. De-reference (don't delete) `AddChildForm`/`EditChildSheet`/`childListTypes` from onboarding.
3. **frontend-e2e-tester** — rewrite `P1-03-FE.spec.ts` to the modal flow; re-verify P1-04/P1-12 + screenshot/capture specs; drive ar+en, validation, multi-child, Continue gating, auth/role routing.
4. **reviewer** — gate against the AC groups above + CONVENTIONS.md; confirm clean build (no orphaned imports), no new pattern beyond the approved modal move, RTL/i18n complete, e2e green.
5. **committer** — only after PASS: branch `feat/onboarding-add-child-modal`, conventional commit, push, open PR.

*No `db-migration`, `backend-feature`, `api-tester`, or `security-auditor` stage (frontend-only; no auth/authz/data-model/file-upload/AI/secrets change — the modal's existing client-side avatar guard is unchanged).*
