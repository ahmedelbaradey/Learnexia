# Pipeline Brief — Phase 8 Localization (Frontend / student-app wave)

> Analyzer deliverable. Source of truth: the user stories + `docs/architecture/localization-architecture.md`.
> This brief scopes the **app-side localization wave** only. Backend P8-01/02/03/04 are **merged to main**
> (PR #90/#91); the curriculum read-path (axis C) is fully server-side and needs **no FE work**.

## Summary & traceability

| | |
|---|---|
| **Task (one line)** | Close the frontend localization gaps: collect a child's **learning language** at add-child (P8-01 FE), give parents a **confirm-gated change-learning-language** flow (P8-04 FE), and complete the **app-shell language foundation** — brand-font runtime loading, persisted UI-language switch in an authenticated location, and an RTL/i18n key-completeness pass (axis A + P6-03 FE). |
| **User stories** | `user-stories/Phase-8-Localization/P8-01-set-child-learning-language.md`, `P8-04-change-child-learning-language.md`; RTL pass `user-stories/Phase-6-Stabilization/P6-03-localization-rtl-pass.md`. |
| **Design of record** | `docs/architecture/localization-architecture.md` (three-axis model — §1 governs the axes; §5 the change flow). |
| **FE architecture** | `docs/dev/FRONTEND_ARCHITECTURE.md` §6 (RTL/i18n, fonts, token storage), §1. |
| **Requirements** | NFR-5 (localization); child-data protection (P8-04 is destructive). |
| **BRD goal** | G-differentiator: **Arabic-first** experience (BRD §1) — "feels native, not bolted-on" (P6-03). |
| **Epic / phase** | Localization · Phase 8 (FE wave runs after the merged BE). |

**Three axes (do not conflate — `localization-architecture.md` §1):**
- **Axis A — UI language** (`User.PreferredLanguage`, ar/en): drives react-i18next + RTL + fonts (Cairo/Tajawal ar, Poppins en). **Partially wired**; this wave finishes it.
- **Axis B — Learning language** (`User.LearningLanguage`, ar/en): medium of instruction for Math/Science. Parent-set at add-child, **immutable by the student**, parent-changed via a fresh-start flow. **Entirely absent on FE today** — this wave builds it.
- **Axis C — Subject content language**: server-side, resolved from the `learning_language` JWT claim. FE just renders what the API returns; use `subjectCode` (MATH/SCIENCE/ARABIC/ENGLISH) for ordering/icons. **No FE work** beyond using `subjectCode`.

## Business context & value

- **Who benefits:** the **parent** (sets the correct medium of instruction for each child; can correct a mis-set medium deliberately) and the **student** (Math/Science delivered in the right language from day one; UI in their preferred language; native-feeling Arabic RTL).
- **Value:** Arabic-first is a core product differentiator. Getting the medium of instruction right at onboarding is load-bearing for the entire learning experience (it selects which curriculum trees the child ever sees). The UI-language axis must feel first-class, not bolted-on.
- **Success measures:** add-child cannot be submitted without a learning language; the change-learning-language flow never fires without an explicit fresh-start acknowledgement; brand fonts actually render on web + native; the UI-language choice survives sign-out/sign-in (persisted to the backend); no hardcoded strings remain on the built Phase-1/Phase-2 screens; every screen reads correctly in Arabic RTL.

## Acceptance criteria (testable)

### P8-01-FE — add-child learning language
- The add-child form collects a **required** learning-language choice (ar | en), phrased as the **medium of instruction** ("which language will this child study Math & Science in?"), visually/semantically **distinct** from the existing account/UI **language** field.
- Submit is **blocked** (zod validation) when learning language is empty; error copy is i18n'd (ar + en).
- On selecting a learning language, the child's **UI `PreferredLanguage` defaults to match** the chosen learning language, but stays **independently editable** (changing one does not lock the other).
- The chosen learning language is sent to the backend on the add-child mutation (new `learningLanguage` field on the add-child command — **requires the api-client regeneration**, see blocker).
- No student-facing surface can set or change learning language (parent-only).

### P8-04-FE — parent change-learning-language (fresh start)
- A **parent-only** entry point (in parent settings / manage-child) lets a parent change a linked child's learning language.
- Selecting a **different** language opens an **explicit fresh-start warning modal** stating it **resets the child's Math/Science progress** (Arabic/English progress and all gamification — XP/streak/badges — are **retained**), and that this is rare (start-of-year).
- The change is only submitted after the parent **explicitly confirms** in the modal; the request carries `confirmFreshStart: true`. There is no silent change.
- Selecting the **same** language is a no-op (the flow either disables confirm or shows "no change").
- Success and error states are surfaced (success refreshes the child's data; the destructive nature is acknowledged before the call). **Requires the new `Change-Learning-Language` endpoint in the api-client** — see blocker.

### P8-99-FE — app-shell language foundation (axis A + RTL pass)
*(Justification for a new ID below.)*
- **Brand fonts render**: Cairo/Tajawal (ar) + Poppins (en) are registered at app startup (expo-font `useFonts`/`loadAsync` + the font plugin in `app.config.ts`) so Tamagui's `$heading`/`$body` faces actually resolve on **both web and native** (today `expo-font` is not even a dependency; the face-loaders live only in stale `dist/`).
- **UI-language switch is promoted** from the Login-only control to a **persistent authenticated location** (account/settings) and **persists to the backend** `User.PreferredLanguage`, so the choice survives sign-out/sign-in (today it only writes local Zustand).
- **RTL/native-restart UX** is correct: web flips direction instantly; native shows the existing restart-prompt UX before `react-native-restart` (`react-native-restart` flip on RTL toggle), and the switch never leaves the app in a half-flipped state.
- **i18n key-completeness**: a sweep of the already-built Phase-1/Phase-2 screens finds **no hardcoded user-facing strings**; any found are moved to `packages/shared` resources (ar + en).
- **RTL review (P6-03 FE-relevant)**: all built screens reviewed in Arabic (RTL) and English — logical/direction props correct, no clipping/overlap, no wrong-side alignment; mixed-direction (numbers/dates/Latin-in-Arabic) renders correctly. Issues logged with severity and fixed or scheduled.

## Affected modules & data (new vs existing)

| Area | New vs existing | Notes |
|---|---|---|
| `apps/student-app/app/(onboarding)/_components/AddChildForm.tsx` | **extend** | add a learning-language field, distinct from the existing `language` (account/UI) field. |
| `packages/shared` add-child zod schema (`addChildSchema` / `AddChildFormValues`) | **extend** | add required `learningLanguage`; keep `language` as UI default. |
| `apps/student-app/app/(parent)/...` (settings or a manage-child surface) | **new flow** | parent change-learning-language entry + fresh-start warning modal. **Not** the onboarding `EditChildSheet` (that edits in-memory drafts pre-creation, no backend call). |
| `packages/api-client` generated client + hooks | **regenerate + new hook** | add-child `learningLanguage`, `/Me` `learningLanguage`, and a `useChangeLearningLanguage` hook — **all blocked on regeneration** (see verdict). |
| `apps/student-app` font registration + `app.config.ts` | **new** | expo-font dependency + `useFonts`/plugin; the brand-font faces. |
| UI-language switch persistence | **new wiring** | a `useUpdateUserLanguage` hook wrapping the **existing** `updateUserLanguage` / `EditUserPreferredLanguageCommand` (already in the generated client — no regen needed for axis A persistence). |
| Curriculum rendering (axis C) | **none** | server-side; FE only uses `subjectCode` for ordering/icons (already exposed). |

No new persisted FE state stores beyond the existing locale store. No server data in Zustand (TanStack Query owns it).

## Handoff → db-migration
**None.** This is a frontend-only wave; all schema (User.LearningLanguage, Subject.Language/SubjectCode) shipped with the merged backend.

## Handoff → backend-feature
**No new backend feature code.** The only backend-touching prerequisite is a **mechanical api-client regeneration** against a backend running the merged P8 code (`refresh:swagger` → `gen:api`) — owned by whoever runs the regen task, not a feature change. See the blocker.

## Handoff → designer
A **Design Spec** is needed before the frontend batch (this wave has real UI surfaces):
- **Add-child learning-language field**: how to present "medium of instruction" so a parent does not confuse it with the app/UI language — label, helper text, the two-field relationship, the "UI defaults to match" affordance. ar + en, RTL.
- **Fresh-start warning modal**: high-severity destructive-confirmation pattern (clear loss statement: Math/Science progress reset; XP/streak/badges kept), explicit confirm affordance, rare/start-of-year framing. Mirror existing modal/destructive shapes (e.g. unlink-child confirm) — **do not invent a new pattern**; if a new pattern seems required, ask the lead first (CLAUDE.md rule 8).
- **UI-language switch in settings**: promote the existing `LocaleThemeControls` segmented control into the account/settings surface; native-restart prompt UX.

## Handoff → frontend
- **Screens/components:** extend `AddChildForm` (+ `addChildSchema`); add the parent change-learning-language entry + warning modal in the parent settings/manage-child surface; add the settings UI-language control; register fonts at app start.
- **API shapes (post-regen):**
  - add-child command gains `learningLanguage: 'ar' | 'en'` (required).
  - `/Me` (`MeResponse`) gains `learningLanguage`.
  - new `PUT api/Parent/Change-Learning-Language`, body `{ childId, learningLanguage, confirmFreshStart: true }` (verify exact field names from the regenerated client).
  - **existing (no regen):** `updateUserLanguage(EditUserPreferredLanguageCommand { userPreferredLanguage })` for axis-A persistence — needs a new TanStack hook + i18n/locale-store sync.
- **Rules:** no API calls in components → all through `api-client` hooks; no server data in Zustand; use `subjectCode` not display names for ordering/icons; mirror existing component/hook shapes; ask before any new design pattern.
- **api-tester:** this wave exposes no **new** backend endpoints from FE work; the change-learning-language + add-child endpoints are already covered by the merged BE integration tests. No new api-tester batch required for the FE wave (regen validation is a build/typecheck concern).
- **security-auditor:** the change-learning-language flow is **destructive + child-data sensitive**, but the FE only gathers the confirm flag and calls a parent-only, family-scoped, already-audited endpoint. Flag for a light FE-side check (confirm flag truly required in UI; no student-facing path; family scope respected by the called endpoint) — not a full audit gate unless the lead wants one.

## Handoff → reviewer
Gate each batch against the acceptance criteria above + CONVENTIONS.md. Key checks: learning-language required & distinct from UI language; UI-PreferredLanguage default-to-match still editable; fresh-start modal blocks the call until confirmed and sends `confirmFreshStart`; fonts actually load; UI-language persists to backend and survives re-login; no hardcoded strings; RTL correct across built screens.

---

## ⚠️ CRITICAL — api-client / Swagger dependency verdict (BLOCKER)

The committed Swagger snapshot + generated NSwag client (`packages/api-client/src/generated/nswag-client.ts`, `swagger.json`) **predate the merged P8 backend.** Verified on disk:

| Needed by | In the generated client today? | Verdict |
|---|---|---|
| `AddChildCommand.learningLanguage` | **NO** — `AddChildCommand` has only `fullName, email, password, grade, language, country` (`language` = account/UI lang, not learning lang). | **MISSING → regen required** |
| `MeResponse.learningLanguage` | **NO** — `MeResponse` has `preferredLanguage` but no `learningLanguage`. | **MISSING → regen required** |
| `PUT api/Parent/Change-Learning-Language` (`confirmFreshStart`) | **NO** — zero `ChangeLearning` / `confirmFreshStart` / `FreshStart` anywhere in the generated client or `swagger.json`. | **MISSING → regen required** |
| Axis-A persistence: `updateUserLanguage(EditUserPreferredLanguageCommand { userPreferredLanguage })` | **YES** — method + DTO already generated. **No hook wraps it yet** (no `useUpdateUserLanguage` in `packages/api-client/src/hooks`). | **PRESENT** — just needs a FE hook + wiring. |

**Conclusion:** the **P8-01-FE and P8-04-FE batches are BLOCKED** until the api-client is regenerated (`refresh:swagger` → `gen:api`) against a backend running the merged P8 code. This is the **first** task in the plan (it gates P8-01-FE and P8-04-FE). **The axis-A app-shell work (P8-99-FE) is NOT blocked** — fonts, RTL, key-completeness, and UI-language persistence all use already-generated/already-present surfaces — so it can run in parallel with the regen.

---

## Open questions / assumptions / risks (for the lead)

1. **Exact PreferredLanguage-update endpoint (axis A persistence).** The generated client exposes `updateUserLanguage(EditUserPreferredLanguageCommand { userPreferredLanguage })` and a `profilePUT(UpdateMyProfileCommand)` that does **not** carry language. **Assumption:** persist UI language via `updateUserLanguage`. Lead to confirm this is the intended endpoint for an authenticated user updating their own `PreferredLanguage` (vs a profile-update variant), and whether it applies to child accounts too.
2. **Font loader source.** A fix may exist on the unmerged branch `feat/design-system-pixel-align` (expo-font registration + face-loaders). **Question:** cherry-pick from that branch, or build the font registration fresh in this wave? (Do not check the branch out.) Recommend the lead decide before the P8-99-FE font task is dispatched.
3. **Parent change-learning-language home.** P8-04 backend is `PUT api/Parent/Change-Learning-Language`. **Question:** does this flow live in `(parent)/settings.tsx` (a manage-child/family panel) or a dedicated per-child management screen? The onboarding `EditChildSheet` is **not** the right home (it edits in-memory drafts, no backend). Recommend the designer + lead pick the surface.
4. **Add-child two-language-field UX.** With both an account/UI **language** and a **learning language** on one form, there's real confusion risk. **Assumption:** keep both, default UI to match learning language, label clearly. Lead/designer to confirm copy and whether the UI-language field should be de-emphasized at add-child.
5. **P6-03 scope for this wave.** P6-03 is a stabilization story covering *all* screens. **Assumption:** fold only the FE-relevant RTL/key-completeness pass over the **already-built** Phase-1/Phase-2 screens into P8-99-FE; the full cross-phase pass stays with P6-03 when Phase 6 runs. Lead to confirm.
6. **Where the learning-language picker reads its options.** ar/en only (per the model). Assuming it reuses the locale constants in `packages/shared`; confirm we don't need a separate learning-language option set.

## Recommended pipeline order (first cut — the `planner` finalizes)

1. **Prereq / blocker (must run first):** `regen:api-client` — `refresh:swagger` → `gen:api` against a backend on merged P8 code. Gates P8-01-FE and P8-04-FE. *(Backend-running task; not a FE component.)*
2. **designer** — Design Spec for: add-child learning-language field, fresh-start warning modal, settings UI-language control. *(Can start immediately, parallel with step 1.)*
3. **Parallel batches once 1 + 2 land:**
   - **P8-99-FE** (app-shell foundation: fonts + UI-language persistence + RTL/key-completeness) — **not blocked on regen**, can start with step 2; only the UI-language-persistence sub-task depends on the designer's settings placement.
   - **P8-01-FE** (add-child learning language) — after regen + design.
   - **P8-04-FE** (parent change-learning-language + warning modal) — after regen + design; lightly depends on P8-01-FE's shared learning-language picker if extracted.
4. **reviewer** — gate each batch against the acceptance criteria above (+ light security-auditor note on P8-04-FE).
5. **committer** — per-story branches after PASS.
