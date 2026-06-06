# Execution Plan — P8-localization-FE: Phase 8 Frontend Localization Wave

## Source

| Input | File |
|---|---|
| Pipeline Brief | `docs/briefs/P8-localization-FE.md` |
| Task file — P8-99 | `tasks/Frontend/student-app/Phase-8-Localization/P8-99-FE.md` |
| Task file — P8-01 | `tasks/Frontend/student-app/Phase-8-Localization/P8-01-FE.md` |
| Task file — P8-04 | `tasks/Frontend/student-app/Phase-8-Localization/P8-04-FE.md` |
| Design of record | `docs/architecture/localization-architecture.md` |
| FE architecture | `docs/dev/FRONTEND_ARCHITECTURE.md` |
| Workflow rules | `CLAUDE.md` |

---

## Lead decisions (already made — do not relitigate)

1. **P8-99-FE-0 is DONE** — commit `db97d88` on `feat/P8-localization-FE` regenerated the api-client against merged P8 backend. `changeLearningLanguage`, `ChangeLearningLanguageCommand`, `confirmFreshStart`, `AddChildCommand.learningLanguage`, and `MeResponse.learningLanguage` are all now in the generated client. The regen blocker is cleared.
2. **Strictly sequential**: one story at a time; full `reviewer` PASS gate between each story.
3. **Build order**: P8-99-FE first, then P8-01-FE, then P8-04-FE.
4. **One PR per story**, stacked bottom-up (P8-99 → P8-01 → P8-04); merge bottom-up.
5. **Font cherry-pick approach**: cherry-pick `328ef06` (font loader) then `bf749f5` (RTL fix) from `feat/design-system-pixel-align` into the P8 wave branch; handle conflicts manually (see cherry-pick plan below).
6. **Axis-A persistence endpoint**: `updateUserLanguage(EditUserPreferredLanguageCommand { userPreferredLanguage })` — the already-generated client method. No new regen needed.

---

## Task inventory

### P8-99-FE — App-shell language foundation

| ID | Summary | Target path | Deps | Est |
|---|---|---|---|---|
| P8-99-FE-0 | ~~PREREQ: api-client regen~~ **DONE** (commit db97d88) | `packages/api-client` (generated) | — | Done |
| P8-99-FE-1 | Brand-font runtime loading via cherry-pick + conflict resolution + render verification | `packages/design-system/src/fonts/*`, `apps/student-app/app/_layout.tsx`, `apps/student-app/package.json`, `apps/student-app/types/assets.d.ts`, `pnpm-lock.yaml` | cherry-pick plan (see §below) | 5h |
| P8-99-FE-2 | `useUpdateUserLanguage` hook wrapping `updateUserLanguage` (EditUserPreferredLanguageCommand) | `packages/api-client/src/hooks/useUpdateUserLanguage.ts` + `hooks/index.ts` | — | 2h |
| P8-99-FE-3 | Promote + persist UI-language switch in authenticated settings surface; backend-persist via FE-2; locale-store + i18n sync | `apps/student-app/app/(parent)/_components/settings/` (new `LanguagePanel.tsx`), `apps/student-app/app/(parent)/_components/SettingsWeb.tsx` (add panel tab) | P8-99-FE-2, Design Spec | 4h |
| P8-99-FE-4 | RTL / native-restart UX: web flips instantly; native shows restart-prompt before `react-native-restart`; no half-flipped state | `apps/student-app` locale-switch path, `RestartPrompt.tsx` (new or existing) | P8-99-FE-3 | 3h |
| P8-99-FE-5 | i18n key-completeness sweep: remove hardcoded strings on Phase-1/Phase-2 screens; move to `packages/shared` resources (ar + en) | `apps/student-app` screens + `packages/shared/src/i18n/` | — | 4h |
| P8-99-FE-6 | RTL review pass (P6-03 FE-relevant): all built screens in ar (RTL) + en; log + fix severity-tagged issues | QA across built screens | P8-99-FE-1, P8-99-FE-5 | 5h |

**P8-99-FE design decision: no designer stage.** The only new UI surface is promoting the existing `LocaleThemeControls` segmented control into the authenticated settings panel — a new `LanguagePanel.tsx` that follows the identical pattern of the four existing panels in `apps/student-app/app/(parent)/_components/settings/` (NotificationsPanel, LinkedChildrenPanel, SecurityPanel, PlanPanel). This reuses established patterns without inventing new ones; the brief confirms "mirror existing settings patterns." No design spec is warranted. If the implementer finds that the settings placement requires any new visual pattern, they must stop and ask per CLAUDE.md rule 8.

### P8-01-FE — Add-child learning language

| ID | Summary | Target path | Deps | Est |
|---|---|---|---|---|
| P8-01-FE-1 | Extend `addChildSchema` / `AddChildFormValues` with required `learningLanguage: 'ar' | 'en'`, i18n'd validation error | `packages/shared/src/schemas/addChildSchema.ts` (+ types) | P8-99-FE-0 (Done) | 2h |
| P8-01-FE-2 | Add learning-language field to `AddChildForm`, phrased as medium of instruction, visually/semantically distinct from the existing UI language field; RTL-aware, token-driven | `apps/student-app/app/(onboarding)/_components/AddChildForm.tsx` | P8-01-FE-1, Design Spec | 4h |
| P8-01-FE-3 | On learning-language select, default the child's UI `language` (PreferredLanguage) to match; both fields stay independently editable | `AddChildForm.tsx` form logic | P8-01-FE-2 | 2h |
| P8-01-FE-4 | Send `learningLanguage` on the add-child mutation; surface `learningLanguage` from `/Me` / child data | `useAddChild` hook wiring + add-child screen | P8-01-FE-1 | 3h |
| P8-01-FE-5 | i18n keys (ar + en) for field label, helper text, placeholder, error | `packages/shared/src/i18n/` (ar + en resources) | P8-01-FE-2 | 1h |
| P8-01-FE-6 | en/ar + RTL + dark/light QA of the extended add-child form | QA | P8-01-FE-2…FE-5 | 2h |

**P8-01-FE needs a designer stage.** There is a real confusion risk with two language fields on one form (UI language vs medium of instruction). The Design Spec must define: label + helper text distinguishing the two fields, the "UI defaults to match" affordance, the field order and visual grouping, and the ar + en copy — RTL-aware layout. The designer should mirror the existing `LanguageSelect` / `GradePicker` shapes; if a new pattern is needed, ask first.

**P8-01-FE security-auditor: yes (light pass).** The form collects child data (learning language for a minor). Auditor check: `learningLanguage` is validated client-side (required, enum); no student-facing path can access this form; the value is submitted to a parent-only endpoint (family-scoped on backend). This is a light gate — no new auth surface, the backend enforcement is already audited.

### P8-04-FE — Parent change-learning-language (fresh-start)

| ID | Summary | Target path | Deps | Est |
|---|---|---|---|---|
| P8-04-FE-1 | `useChangeLearningLanguage` TanStack hook wrapping the regenerated `PUT api/Parent/Change-Learning-Language` (`{ childId, learningLanguage, confirmFreshStart }`); invalidate child/family queries on success | `packages/api-client/src/hooks/useChangeLearningLanguage.ts` + `hooks/index.ts` | P8-99-FE-0 (Done) | 3h |
| P8-04-FE-2 | Parent-only entry point for change-learning-language in parent settings / manage-child surface (not the onboarding `EditChildSheet`) | `apps/student-app/app/(parent)/_components/settings/LinkedChildrenPanel.tsx` (extend) or designer-designated manage-child surface | P8-04-FE-1, Design Spec | 4h |
| P8-04-FE-3 | Fresh-start warning modal: destructive confirmation stating Math/Science progress reset, Arabic/English + XP/streak/badges retained, rare/start-of-year framing; mutation fires only after explicit confirm (`confirmFreshStart: true`) | new `ChangeLearningLanguageModal.tsx` (component location per Design Spec) | P8-04-FE-2, Design Spec | 5h |
| P8-04-FE-4 | Same-language selection is a no-op (disable confirm / show "no change"); success refreshes child data; error states surfaced | flow logic in FE-2 / FE-3 | P8-04-FE-3 | 2h |
| P8-04-FE-5 | i18n keys (ar + en) for warning copy, confirm/cancel labels, success/error messages | `packages/shared/src/i18n/` (ar + en resources) | P8-04-FE-3 | 2h |
| P8-04-FE-6 | en/ar + RTL + dark/light QA; verify no student-facing path can reach this flow | QA | P8-04-FE-2…FE-5 | 2h |

**P8-04-FE needs a designer stage.** The fresh-start warning modal must follow the existing destructive-confirmation pattern (e.g. the unlink-child confirm modal). The Design Spec must define: the modal's warning copy (loss statement + retained items), the confirm/cancel affordance, the same-language "no change" state, and the entry-point location in the parent settings/manage-child surface. Mirror existing modal shapes; do not invent a new pattern (CLAUDE.md rule 8).

**P8-04-FE security-auditor: YES (required gate).** This flow is destructive (hard-deletes child's Math/Science progress), child-data sensitive, and parent-only. Auditor checks: `confirmFreshStart` is truly gated in the UI (cannot call the endpoint without it); no student-facing path exists; family scope is respected (the UI always sends the correct `childId` from the authenticated parent's context); no client-side state leaks the confirm flag across sessions. Critical or High findings block the reviewer gate.

---

## Cherry-pick plan for brand fonts

### What to cherry-pick

Two commits from `feat/design-system-pixel-align` (which is 98 commits behind `main` and 6 ahead):

| Step | Commit | What it does | Key files |
|---|---|---|---|
| 1 | `328ef06` | Font loader: adds `expo-font` dep; registers Cairo/Tajawal/Poppins via `useFonts`/`loadAsync` at app start + Tamagui `$heading`/`$body` face mapping; web `@font-face` injection; font assets under `packages/design-system/assets/fonts/` | `apps/student-app/app/_layout.tsx`, `apps/student-app/package.json`, `apps/student-app/types/assets.d.ts`, `packages/design-system/src/fonts/*` (new files), `packages/design-system/src/index.ts`, `pnpm-lock.yaml` |
| 2 | `bf749f5` | RTL fix: Arabic text Cairo/Tajawal font stack; sidebar RTL flip correction; SettingsWeb full-width panels | `apps/student-app/app/(parent)/_components/SettingsWeb.tsx`, `Sidebar.tsx`, `MyChildrenWeb.tsx`, `OverviewWeb.tsx`, `overview.tsx`, `packages/design-system/src/fonts/index.ts` |

### Conflict risk assessment

**Commit `328ef06` — LOW conflict risk.** The only P8 branch overlap is `pnpm-lock.yaml` (auto-resolved by re-running `pnpm install` post-cherry-pick). The design-system font files (`packages/design-system/src/fonts/*`) and `apps/student-app/app/_layout.tsx` are untouched on the P8 branch since the merge-base.

**Commit `bf749f5` — MEDIUM conflict risk on one file.** `SettingsWeb.tsx` was substantially rewritten by the P2-12-FE commit (`8f7ed20`) on the P8 branch — it now has a full four-tab panel architecture (Notifications, Linked Children, Security, Plan). The `bf749f5` RTL fix to `SettingsWeb.tsx` made minor alignment changes to the older single-panel version. This is a **guaranteed conflict**. The other files (`Sidebar.tsx`, `MyChildrenWeb.tsx`, `OverviewWeb.tsx`, `overview.tsx`) may also conflict if P2-12 or later commits modified them.

### Cherry-pick procedure (part of P8-99-FE-1)

```
# Step 1: cherry-pick the font loader
git cherry-pick 328ef06 --no-commit
# -- resolve pnpm-lock.yaml by running: pnpm install
# -- verify no other conflicts, then:
git add -A && git cherry-pick --continue

# Step 2: cherry-pick the RTL fix
git cherry-pick bf749f5 --no-commit
# -- SettingsWeb.tsx WILL conflict: manually apply the RTL alignment intent
#    (drop row-reverse double-flip; add explicit content-facing border direction;
#     make panels fill full width) into the current P2-12 panel-tab architecture.
# -- Check Sidebar.tsx, MyChildrenWeb.tsx, OverviewWeb.tsx, overview.tsx for conflicts.
# -- Run: pnpm install (if pnpm-lock conflicts again)
git add -A && git cherry-pick --continue
```

**Fallback strategy (if cherry-pick is too conflicted to be clean):** skip `git cherry-pick` entirely and do a manual port. Read the diff of each commit (`git show 328ef06`, `git show bf749f5`) and apply the intent by hand to the current files on the P8 branch. This is safer than a messy cherry-pick resolution and produces the same result with a cleaner commit.

### Render-verification step (required before reviewer gate on P8-99)

After the font cherry-pick lands, the implementer must confirm:
1. `pnpm typecheck` (or `tsc --noEmit`) passes — the `ttf-modules.d.ts` ambient declaration and `types/assets.d.ts` fix were specifically added to eliminate tsc build breaks.
2. On web (`expo start --web`): verify Cairo renders for Arabic text and Poppins for English text (open DevTools → Network → confirm `.ttf` files are served with `200 font/ttf`).
3. On native simulator/device: verify `expo-font` loads without hanging the splash (no unhandled `useFonts` promise rejection in the Metro log).
4. RTL toggle: with Arabic locale, confirm Arabic text uses Cairo/Tajawal (not system fallback); no sidebar double-flip; panels full-width.

---

## Dependency order (wave-wide)

```
P8-99-FE-0  [DONE]
    |
    +-- P8-99-FE-1 (font cherry-pick) \
    +-- P8-99-FE-2 (useUpdateUserLanguage hook)  } independent, run in order within P8-99
    +-- P8-99-FE-5 (i18n sweep)       /
    |
P8-99-FE-3 (settings UI-language switch) — depends on FE-2 + Design Spec
    |
P8-99-FE-4 (RTL restart UX) — depends on FE-3
    |
P8-99-FE-6 (RTL review pass) — depends on FE-1 + FE-5
    |
[reviewer PASS on P8-99-FE]
    |
designer Design Spec (P8-01 + P8-04 surfaces) — can be drafted in parallel with P8-99 impl
    |
P8-01-FE-1 (extend addChildSchema)
    |
P8-01-FE-2 (AddChildForm field) — depends on FE-1 + Design Spec
P8-01-FE-4 (mutation wiring)    — depends on FE-1
    |
P8-01-FE-3 (default-to-match logic)
P8-01-FE-5 (i18n keys)
    |
P8-01-FE-6 (QA)
    |
[security-auditor light pass — P8-01]
[reviewer PASS on P8-01-FE]
    |
P8-04-FE-1 (useChangeLearningLanguage hook)
    |
P8-04-FE-2 (entry point in settings) — depends on FE-1 + Design Spec
    |
P8-04-FE-3 (warning modal) — depends on FE-2 + Design Spec
    |
P8-04-FE-4 (no-op + success/error)
P8-04-FE-5 (i18n keys)
    |
P8-04-FE-6 (QA)
    |
[security-auditor REQUIRED gate — P8-04]
[reviewer PASS on P8-04-FE]
    |
[committer — per-story PRs, stacked]
```

---

## Execution batches

### Batch 1 — P8-99-FE (sequential within batch; implementer runs in order)

**Agent:** `frontend`
**Branch:** `feat/P8-localization-FE` (already exists; continue on this branch)
**PR base:** current `feat/P8-localization-FE` tip (stacked on top of `db97d88`)

| Step | Task | Notes |
|---|---|---|
| 1a | P8-99-FE-1 | Cherry-pick `328ef06` then `bf749f5`; resolve `pnpm-lock.yaml` with `pnpm install`; manually port `SettingsWeb.tsx` RTL intent into P2-12 panel architecture; run render-verification (typecheck + web font load + native font load) |
| 1b | P8-99-FE-2 | `useUpdateUserLanguage` hook — no regen needed; follow existing hook shapes |
| 1c | P8-99-FE-5 | i18n key-completeness sweep (can run alongside 1a/1b, but commit after) |
| 1d | P8-99-FE-3 | New `LanguagePanel.tsx` in `settings/`; extend `SettingsWeb.tsx` to add it as a tab; wire FE-2 hook; sync locale store + i18n on change |
| 1e | P8-99-FE-4 | RTL restart UX; web instant-flip; native restart-prompt; no half-flipped state |
| 1f | P8-99-FE-6 | RTL review pass across all built Phase-1/Phase-2 screens; log + fix severity-tagged issues |

**Review gate after Batch 1:** `reviewer` checks against P8-99-FE acceptance criteria (fonts render, UI-language persists, RTL correct, no hardcoded strings). Then `security-auditor` is NOT required for P8-99 (no child data, no authz surface, no destructive action).

**Committer after PASS:** create PR for P8-99-FE on `feat/P8-localization-FE`. Base = `main` (or the branch's current base — this is the bottom of the stack).

---

### Interlude — designer stage (can run in parallel with Batch 1 implementation)

**Agent:** `designer`
**Deliverable:** `design-system/ui_kits/localization/P8-01-P8-04-FE.md`

Design Spec must cover:
1. **Add-child learning-language field (P8-01):** label + helper text clearly distinguishing "medium of instruction for Math & Science" from "app display language"; field order on the form; the "UI defaults to match" affordance (visual hint or helper text); ar + en copy; RTL layout; mirror `LanguageSelect` / `GradePicker` shape.
2. **Fresh-start warning modal (P8-04):** destructive-confirmation pattern; clear loss statement (Math/Science progress reset); retained items (Arabic, English, XP, streak, badges); rare/start-of-year framing; explicit confirm vs cancel; same-language "no change" state. Mirror the existing unlink-child confirm modal — do not invent a new pattern (CLAUDE.md rule 8); if a new pattern seems necessary, stop and ask.
3. **Change-learning-language entry point (P8-04):** where in the parent settings the trigger lives (recommended: extend `LinkedChildrenPanel.tsx` with a per-child action row, since the panel already lists linked children; designer confirms or proposes alternative).
4. **RTL + dark/light** for all three surfaces.

The designer stage blocks Batch 2 (P8-01 needs the spec for FE-2) and Batch 3 (P8-04 needs it for FE-2/FE-3).

---

### Batch 2 — P8-01-FE (sequential within batch)

**Agent:** `frontend`
**Branch:** create `feat/P8-01-FE` branching off `feat/P8-localization-FE` after Batch 1 PASS (stacked PR)
**Gate:** Batch 1 reviewer PASS + designer spec in hand

| Step | Task | Notes |
|---|---|---|
| 2a | P8-01-FE-1 | Extend `addChildSchema` / `AddChildFormValues`; required `learningLanguage`; i18n'd error |
| 2b | P8-01-FE-4 | Wire `learningLanguage` on the `useAddChild` mutation (can run alongside 2a) |
| 2c | P8-01-FE-2 | Add the field to `AddChildForm` per Design Spec; RTL-aware, token-driven |
| 2d | P8-01-FE-3 | Default-to-match logic (learning → UI language preselect, stays independently editable) |
| 2e | P8-01-FE-5 | i18n keys ar + en |
| 2f | P8-01-FE-6 | en/ar + RTL + dark/light QA |

**Security-auditor gate (light) after implementation:** confirm `learningLanguage` is required at the form level (no bypass); no student-facing path leads to this form; the value targets the parent-only `AddChildCommand`.

**Review gate after Batch 2:** `reviewer` checks against P8-01-FE acceptance criteria (required field, distinct from UI language, default-to-match, sent on mutation, no student-facing path). Unblocks Batch 3.

**Committer after PASS:** create PR for P8-01-FE stacked on `feat/P8-localization-FE`.

---

### Batch 3 — P8-04-FE (sequential within batch)

**Agent:** `frontend`
**Branch:** create `feat/P8-04-FE` branching off `feat/P8-01-FE` after Batch 2 PASS (stacked PR)
**Gate:** Batch 2 reviewer PASS + designer spec in hand

| Step | Task | Notes |
|---|---|---|
| 3a | P8-04-FE-1 | `useChangeLearningLanguage` hook; invalidate child/family queries on success |
| 3b | P8-04-FE-2 | Entry point in parent settings surface per Design Spec (likely `LinkedChildrenPanel.tsx` extension) |
| 3c | P8-04-FE-3 | Fresh-start warning modal; `confirmFreshStart: true` gates the mutation |
| 3d | P8-04-FE-4 | Same-language no-op; success + error states; success refreshes child data |
| 3e | P8-04-FE-5 | i18n keys ar + en (warning copy, confirm/cancel, success/error) |
| 3f | P8-04-FE-6 | en/ar + RTL + dark/light QA; student-path exclusion check |

**Security-auditor gate (REQUIRED, blocks reviewer) after implementation:**
- `confirmFreshStart` is truly gated: the mutation cannot be called from the UI without it.
- No student-facing navigation path reaches this flow (route guard or parent-only layout enforces it).
- `childId` is always sourced from the authenticated parent's family context (never from a URL param that a student could manipulate).
- No client-side state leaks the confirm flag across sessions or child selections.
- Critical or High findings block the reviewer gate and must be fixed before proceeding.

**Review gate after Batch 3:** `reviewer` checks against P8-04-FE acceptance criteria (explicit confirm gates the call; same-language no-op; success refreshes; error surfaced; no student path; RTL correct).

**Committer after PASS:** create PR for P8-04-FE stacked on `feat/P8-01-FE`.

---

## Branch and PR stacking map

```
main
  └── feat/P8-localization-FE     ← Batch 1 (P8-99-FE)  PR base: main
        └── feat/P8-01-FE          ← Batch 2 (P8-01-FE)  PR base: feat/P8-localization-FE
              └── feat/P8-04-FE    ← Batch 3 (P8-04-FE)  PR base: feat/P8-01-FE
```

**Merge order (bottom-up, squash or merge):**
1. P8-99-FE PR → `main`
2. Rebase `feat/P8-01-FE` onto new `main`, then merge P8-01-FE PR → `main`
3. Rebase `feat/P8-04-FE` onto new `main`, then merge P8-04-FE PR → `main`

**Note:** `feat/P8-localization-FE` already exists and already has commit `db97d88` (P8-99-FE-0 regen). Batch 1 implementation continues directly on this branch — no new branch needed for P8-99.

---

## Review gates (summary)

| Gate | After | Agent | Criteria |
|---|---|---|---|
| Batch 1 reviewer PASS | P8-99-FE implementation | `reviewer` | Fonts render (web + native); UI-language persists to backend + survives re-login; RTL + restart-UX correct; no hardcoded strings on Phase-1/2 screens; RTL review issues logged/fixed |
| P8-01 security-auditor (light) | Batch 2 implementation | `security-auditor` | Required field, no student path, parent-only endpoint targeted |
| Batch 2 reviewer PASS | P8-01-FE implementation + security-auditor note | `reviewer` | Required `learningLanguage`; distinct from UI language; default-to-match editable; sent on mutation; no student path |
| P8-04 security-auditor (REQUIRED, blocking) | Batch 3 implementation | `security-auditor` | `confirmFreshStart` gated; no student path; `childId` from family context only; no cross-session state leak |
| Batch 3 reviewer PASS | P8-04-FE implementation + security-auditor PASS | `reviewer` | Explicit confirm gates call; no-op for same language; success refreshes; error surfaced; RTL correct; no student path |

---

## Blockers / prerequisites

| # | Blocker | Status | Action |
|---|---|---|---|
| 1 | api-client regen (P8-99-FE-0) | **CLEARED** (commit db97d88) | No action needed |
| 2 | Font cherry-pick conflict on `SettingsWeb.tsx` | **ACTIVE RISK** (guaranteed conflict) | Batch 1 implementer manually ports the RTL alignment intent from `bf749f5` into the P2-12 panel-tab architecture; fallback: full manual port from `git show` diff |
| 3 | Designer spec for P8-01 + P8-04 | **MUST RUN BEFORE Batch 2** | Dispatch `designer` concurrently with Batch 1; spec must land before Batch 2 starts |
| 4 | P8-04 entry-point surface decision | **Soft open** | The plan recommends extending `LinkedChildrenPanel.tsx` (it already lists linked children, making it the natural per-child action surface); designer stage confirms or overrides |
| 5 | `react-native-restart` presence in `package.json` | **Unverified** | Batch 1 implementer (P8-99-FE-4) must verify `react-native-restart` is already a dependency; if not, add it before wiring the restart-prompt UX |
| 6 | Native font render test | **Requires real device or simulator** | Web verification is straightforward (DevTools Network tab); native requires Expo Go or a simulator — if web-only dev is the constraint, log the native test as a known limitation and schedule it before the store release |
| 7 | P8-01's `learningLanguage` picker shared into P8-04 | **Soft dependency** | If the Batch 2 implementer extracts a reusable `LearningLanguagePicker` component to `packages/ui` or `packages/shared`, Batch 3 reuses it; if not extracted, Batch 3 re-implements inline. Decision belongs to Batch 2 implementer (mirror existing shapes; ask before extracting to a new shared component). |

---

## Risks

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| `bf749f5` cherry-pick produces unresolvable `SettingsWeb.tsx` conflict | High (P2-12 rewrote the file substantially) | Medium (manual port needed) | Use manual port fallback; `git show bf749f5` gives the diff to apply by hand |
| `328ef06` font commit touches `pnpm-lock.yaml` which may not apply cleanly | Medium | Low | Run `pnpm install` after cherry-pick to regenerate lock; commit the result |
| Native font loading not testable in web-only dev environment | Medium | Low | Log as known limitation; add native verification to PR checklist; block store release on it |
| i18n key-completeness sweep (P8-99-FE-5) finds more hardcoded strings than estimated | Medium | Medium (scope creep on P8-99) | Cap at Phase-1/Phase-2 screens in this wave per brief; file remaining screens as P6-03 backlog |
| RTL review pass (P8-99-FE-6) finds issues needing design changes | Medium | Medium | Log severity; fix Critical/High in this wave; schedule Medium/Low as P6-03 follow-up |
| `confirmFreshStart` UI gate bypassed by developer tooling (security-auditor finding) | Low | High | Security-auditor must verify there is no way to trigger the mutation without the modal confirm; this is the primary audit target for P8-04 |

---

## Definition of done

### Per batch

**Batch 1 (P8-99-FE) done when:**
- Cairo/Tajawal/Poppins render on web (DevTools confirmed) and on native (or native gap documented)
- `useUpdateUserLanguage` hook exported from `packages/api-client/src/hooks`
- Language switch in `(parent)/settings` (new `LanguagePanel.tsx`) persists via `updateUserLanguage` backend call and syncs locale store + i18n
- RTL toggle: web instant-flip; native restart-prompt before `react-native-restart`; no half-flipped state
- No hardcoded user-facing strings on Phase-1/Phase-2 screens (all moved to `packages/shared` i18n)
- RTL review issues logged with severity; Critical/High issues fixed
- `reviewer` PASS

**Batch 2 (P8-01-FE) done when:**
- `addChildSchema` / `AddChildFormValues` has required `learningLanguage: 'ar' | 'en'`
- `AddChildForm` shows a learning-language field, visually/semantically distinct from UI language field
- Selecting learning language defaults the UI language to match; both remain independently editable
- `learningLanguage` is sent on the add-child mutation and surfaced from `/Me`
- i18n keys present ar + en; RTL layout correct; dark/light QA passed
- No student-facing path can access learning-language selection
- `security-auditor` light pass note (no Critical/High)
- `reviewer` PASS

**Batch 3 (P8-04-FE) done when:**
- `useChangeLearningLanguage` hook exported; invalidates child/family queries on success
- Parent-only entry point in settings surface (parent authz enforced by layout)
- Fresh-start warning modal blocks the mutation until parent explicitly confirms; sends `confirmFreshStart: true`
- Same-language selection is a no-op (confirm disabled or "no change" shown)
- Success refreshes child data; error states surfaced
- i18n keys ar + en; RTL correct; dark/light QA passed
- `security-auditor` PASS (no Critical/High findings — gate is blocking)
- `reviewer` PASS

### Overall wave done when:
- All three story PRs merged to `main` (bottom-up: P8-99 → P8-01 → P8-04)
- All acceptance criteria in `docs/briefs/P8-localization-FE.md` satisfied
- `docs/dev/HANDOFF.md` updated with: font cherry-pick outcome, any remaining RTL/i18n debt scheduled as P6-03, P8-04 entry-point surface decision, native font test status

---

*Plan ready — dispatch Batch 1.*
