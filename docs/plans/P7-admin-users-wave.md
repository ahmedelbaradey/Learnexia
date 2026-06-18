# Execution Plan — P7 Admin Console Wave 1: User & Account Management (P7-06 / P7-07 / P7-08, FE)

> Written by `planner` (2026-06-18). Turns the three Pipeline Briefs + FE task files into a dependency-ordered, batched plan for the **admin-dashboard frontend** wave (Next.js 15, `apps/admin-dashboard`, dev port 3001). **Frontend-only** — the Identity backend (`AdminUsersController`) is built + merged to `main`. Plan only; no code.

## Source
- **Briefs:** `docs/briefs/P7-06.md` (search & inspect users — FE), `docs/briefs/P7-07-FE.md` (suspend / reactivate / delete), `docs/briefs/P7-08-FE.md` (child profiles & grade overrides).
- **Task files:** `tasks/Frontend/admin-dashboard/Phase-7-Admin-Console/P7-06-FE.md`, `P7-07-FE.md`, `P7-08-FE.md`.
- **Rules:** `CLAUDE.md` (workflow, non-negotiables), `docs/dev/PARALLELISM.md`, `docs/dev/FRONTEND_ARCHITECTURE.md`, `docs/dev/CONVENTIONS.md`.
- **Live source verified on branch `feat/P7-admin-users-fe`:**
  - `apps/admin-dashboard/app/(admin)/` has only `dashboard/` + `layout.tsx` — **no `users/` tree, no Users nav item** (confirms the P7-06-foundation dependency).
  - `packages/api-client/src/hooks/useUserList.ts` — the raw-path paginated reference (`client.getPaginated('/api/...')`, PascalCase query keys) — but it targets the **legacy** `/api/Users/UserManagement/UserList` (the "stale hook, do not reuse" the briefs warn about).
  - `packages/api-client/src/hooks/useUnlinkChild.ts` / `useUpdateChild.ts` / `useChangeLearningLanguage.ts` — mutation reference (`useMutation` + `unwrapEnvelope` + `onSuccess` invalidate; DELETE-with-body is supported).
  - `packages/api-client/src/query/queryKeys.ts` — has a legacy `users.*` namespace (wrong endpoint) → this wave **adds a fresh `adminUsers.*` namespace**.
  - `apps/admin-dashboard/components/AdminSideNav.tsx` — nav items are non-functional placeholders from a module-level `NAV_ITEMS` const (`navCurriculum`/`navContent`), no `usePathname`/active state.
  - `apps/admin-dashboard/components/AdminTopBar.tsx` — already accepts a `title` prop (the per-page-title seam exists; pages pass their title).
  - Branch `feat/P7-admin-users-fe` exists and is checked out — **single wave branch, no worktrees** (constraint 5).

## Wave shape (one-paragraph summary)
P7-06 FE is the **foundation**: it stands up the `app/(admin)/users/` list + detail screens, the shared `adminUsers.*` query keys + read hooks, the real "Users" nav item, and the status-badge primitive. P7-07 (lifecycle actions) and P7-08 (child edit/grade/language) **hang their UI off P7-06's user-detail page** and **invalidate P7-06's query keys** — so they cannot start until the foundation lands. We sequence: **Batch A** (shared foundation: read hooks + query keys + nav/title + base copy) → **Batch B** (P7-06 list + detail/family/activity) → then **Batch C** (P7-07) and **Batch D** (P7-08) which are mutually independent and **run in parallel after B**, with one serialization rule on the shared files they both touch. A `designer` stage runs before B/C/D; `security-auditor` (MANDATORY) + `frontend-e2e-tester` run after the implementer batches; `reviewer` gates before the single `committer` PR.

---

## Task inventory (mapped to acceptance criteria)

### P7-06 — Search & inspect users (foundation)
| ID | Stack | Summary | Target | Est (h) | Depends on | AC ref (brief P7-06) |
|---|---|---|---|---|---|---|
| P7-06-FE-5 | FE / api-client + shell | Read hooks (`useSearchUsers`, `useAdminUserProfile`, `useUserFamily`, `useUserActivity`) + `adminUsers.*` query keys + FE-local DTO types; real **Users** nav item (active-aware) + per-page title; EN+AR base copy | `packages/api-client/src/hooks/*`, `…/query/queryKeys.ts`, `components/AdminSideNav.tsx`, `lib/strings.ts` | 3 | P1-10-FE-3 (shell, done) | AC 8, 9, 10 |
| P7-06-FE-1 | FE | Users **list** page: server-paginated table, role + status filters, debounced free-text search, 4 states (loading / empty / error+retry / results), page-1 reset on filter change, `placeholderData` smoothing | `app/(admin)/users/page.tsx` | 5 | FE-5 | AC 1, 2 |
| P7-06-FE-2 | FE | User **detail** (read-only): name/email/role/status/dates, status reason/changed-at when present; child block = grade + country + **two distinctly-labeled language rows** (`PreferredLanguage` vs `LearningLanguage`); states loading / not-found / error | `app/(admin)/users/[id]/page.tsx` | 4 | FE-1 | AC 3, 4, 7 |
| P7-06-FE-3 | FE | **Family** panel: parent → linked children, child → linked parent(s); deep-links to each `/users/[id]`; child email omitted by design | `components/UserFamilyPanel.tsx` | 3 | FE-2 | AC 5 |
| P7-06-FE-4 | FE | **Activity** panel: XP/level, streak, badges, missions, league — each best-effort ("no data" on null); last sign-in labeled "not tracked" | `components/UserActivityPanel.tsx` | 3 | FE-2 | AC 6 |

### P7-07 — Suspend, reactivate & delete (hangs off P7-06 detail)
| ID | Stack | Summary | Target | Est (h) | Depends on | AC ref (brief P7-07-FE) |
|---|---|---|---|---|---|---|
| P7-07-FE-5 | FE / api-client | Mutation hooks `useSuspendUser` / `useReactivateUser` / `useDeleteUser` (raw-path POST/DELETE-with-body, output typed `string`/`void`), invalidate **`adminUsers.*`** profile + list keys on success | `packages/api-client/src/hooks/*` (+ `index.ts`) | 3 | P7-06-FE-5 (hooks/keys exist) | AC 7 |
| P7-07-FE-2 | FE | **Suspend** dialog: required reason (≤500), confirm; governance-vs-lockout copy; inline envelope errors; refresh → Suspended badge | `components/SuspendUserDialog.tsx` | 3 | FE-5, P7-06-FE-2 | AC 2, 5, 8 |
| P7-07-FE-3 | FE | **Reactivate** dialog: optional reason, confirm; surface prior `lastStatusReason`/`statusChangedAtUtc`; refresh → Active | `components/ReactivateUserDialog.tsx` | 2 | FE-5, P7-06-FE-2 | AC 3, 5, 8 |
| P7-07-FE-4 | FE | **Delete** dialog: two-step (required reason + typed confirmation) gating destructive button; parent-only cascade-children checkbox + child-data warning; sends `confirm:true` only at final step | `components/DeleteUserDialog.tsx` | 4 | FE-5, P7-06-FE-2 | AC 4, 5, 8 |
| P7-07-FE-1 | FE | Lifecycle **actions menu** on P7-06 detail header: renders only legal transitions per `accountStatus` (Active → {Suspend, Delete}; Suspended → {Reactivate, Delete}; Deleted → none); wires the three dialogs | `app/(admin)/users/[id]/page.tsx` (P7-06-owned — **add to it, don't fork**) | 3 | FE-2/3/4, P7-06-FE-2 | AC 1, 6 |

### P7-08 — Child profiles & grade overrides (hangs off P7-06 child detail)
| ID | Stack | Summary | Target | Est (h) | Depends on | AC ref (brief P7-08-FE) |
|---|---|---|---|---|---|---|
| P7-08-FE-4 | FE / api-client | Mutation hooks `useUpdateChildProfile` / `useOverrideChildGrade` / **`useAdminChangeChildLearningLanguage`** (distinct from the parent hook), invalidate detail (+ activity; learning-language also invalidates child Math/Science progress caches) | `packages/api-client/src/hooks/*` (+ `index.ts`), `…/query/queryKeys.ts` | 3 | P7-06-FE-5 (keys exist) | AC 9 |
| P7-08-FE-1 | FE | **Edit child profile** page: country + `PreferredLanguage` (ui locale, applied directly), and a **separate** `LearningLanguage` control that opens the fresh-start dialog; renders **only for Student-role** accounts; sends only changed fields | `app/(admin)/users/[id]/edit/page.tsx` | 5 | FE-4, P7-06-FE-2 | AC 1, 2, 3 |
| P7-08-FE-2 | FE | **Change-learning-language** dialog: fresh-start "resets Math & Science, cannot be undone" + explicit/typed confirm; sends `confirmFreshStart:true`; not shown when unchanged; 424 → gate message | `components/ChangeLearningLanguageDialog.tsx` | 4 | FE-1 | AC 4, 5 |
| P7-08-FE-3 | FE | **Grade-override** dialog: grade 1–6, required reason (FE rule, ≤500), confirm; "curriculum re-scopes, history preserved" copy; sends `confirm:true`; maps 422 (range) + 400 (same-grade/confirm) inline | `components/GradeOverrideDialog.tsx` | 4 | FE-1 | AC 6 |
| P7-08-FE-5 | FE | Inline validation + envelope-error surfacing across the page + both dialogs; EN+AR copy; RTL/a11y polish | `app/(admin)/users/[id]/edit/page.tsx`, both dialogs | 2 | FE-1/2/3 | AC 7, 10 |

**Wave totals:** P7-06 = 18h · P7-07 = 15h · P7-08 = 18h → ~51h FE (excludes designer / test / review stages).

> **Backend = 0 batches.** All three backends are merged; this wave consumes the live `api/Admin/Users` contract documented in the briefs. The only "backend-shaped" item that surfaced is the **codegen decision** (hand-write vs NSwag regen) — resolved as a decision (D1), not a batch.

---

## Dependency order

```
P1-10 admin shell (DONE, on main)
        │
        ▼
[A] P7-06-FE-5  ── adminUsers.* query keys + read hooks + Users nav + base copy   ◄── shared foundation
        │
        ▼
[B] P7-06-FE-1 (list) ─► P7-06-FE-2 (detail) ─┬─► P7-06-FE-3 (family panel)   ┐
                                              └─► P7-06-FE-4 (activity panel)  ┘  (3 & 4 ∥ once 2's shell lands)
        │
        ├─────────────────────────────┬───────────────────────────────────────┐
        ▼                             ▼                                         │
[C] P7-07-FE-5 (mut hooks)          [D] P7-08-FE-4 (mut hooks)                  │
        ▼                             ▼                                         │
   FE-2/3/4 dialogs (∥)            FE-1 edit page                               │  C and D run in PARALLEL
        ▼                             ▼                                         │  (independent files except
   FE-1 actions menu               FE-2/3 dialogs (∥) ─► FE-5 polish            │   the serialized shared set)
        └─────────────┬───────────────┘                                        │
                      ▼                                                         │
            security-auditor (MANDATORY) ─► frontend-e2e-tester ─► reviewer ─► committer (single PR) ◄┘
```

**Hard dependencies (the spine):**
1. **P7-06-FE-5 before everything** — the `adminUsers.*` query keys + read hooks are the shared data layer P7-07/08 mutations must invalidate. Nothing renders real data without them.
2. **P7-06-FE-2 (user detail) before P7-07-FE-1 and all of P7-08** — the actions menu, the child edit entry point, and all dialogs are reached **from the detail page**. P7-07/08 must **add to** `app/(admin)/users/[id]/page.tsx`, never fork a second detail page (briefs P7-07 Q-1, P7-08 blocker).
3. **Within P7-06:** list (FE-1) before detail (FE-2) — the detail is reached by deep-link from the list and shares the badge/data hooks. Family (FE-3) + activity (FE-4) panels are parallel once FE-2's detail shell exists (they load independently per AC 7).
4. **Within P7-07/08:** mutation hooks (FE-5 / FE-4) land first; dialogs depend on hooks + design; the actions-menu (P7-07-FE-1) / edit-page wiring depends on dialogs + the P7-06 detail.

---

## Execution batches

All batches run **on the single existing branch `feat/P7-admin-users-fe`** (constraint 5 — no worktrees; the heavy shared-file coupling makes parallel checkouts unsafe). "Parallel" below means *parallel agents on the same branch in one dispatch*, allowed only because the file sets are disjoint (Mode A, `docs/dev/PARALLELISM.md`).

### Stage 0 — designer (before B/C/D; can overlap Batch A)
- **Agent:** `designer` ×3 (or one designer over all three surfaces — recommend **one designer pass** for visual consistency of the shared badge + dialog system).
- **Outputs:**
  - `design-system/ui_kits/admin-users/P7-06.md` — list (filters bar, table + column priority, role/status **badge styles**, pagination, 4 states), detail (profile card, the **two distinct language rows**, family panel, activity panel with per-section "no data"), responsive table→stacked, RTL, dark tokens, active-nav treatment.
  - `design-system/ui_kits/admin-dashboard/P7-07.md` — actions menu (legal-transition matrix), the **shared status badge** states (Active/Suspended/Deleted — define once here or reference P7-06), three dialogs (Suspend / Reactivate / Delete), the **typed-confirmation** pattern + **cascade-children** block, EN+AR copy, a11y/RTL.
  - `design-system/ui_kits/admin-dashboard/P7-08-FE.md` — child edit page, grade-override dialog, fresh-start learning-language dialog (mirror parent `P8-04-FE.md` confirm gesture), EN+AR copy, a11y/RTL.
- **Why first:** every implementer batch (B/C/D) consumes a Design Spec; the **badge** and **dialog/confirm** primitives must be specified once so P7-06 builds them and P7-07/08 reuse them (no duplicate primitives — CLAUDE.md "mirror existing shapes; ask before new abstractions").
- **Parallel with:** Batch A (the api-client data layer has no visual dependency on the spec).

### Batch A (sequential, first) — shared data + shell foundation → `P7-06-FE-5`
- **Agent:** `frontend`.
- **Work:** add `adminUsers.*` namespace to `packages/api-client/src/query/queryKeys.ts`; hand-write read hooks `useSearchUsers` / `useAdminUserProfile` / `useUserFamily` / `useUserActivity` (raw-path, mirroring `useUserList.ts`; PascalCase query keys `Role/Status/Q/PageNumber/PageSize/OrderBy`) with FE-local DTO types; export from `hooks/index.ts`. Upgrade `AdminSideNav.tsx` to a real, active-aware **Users** link (`usePathname` → `aria-current`); add page-title slots + EN+AR base copy to `lib/strings.ts` (the `title` prop seam already exists on `AdminTopBar`).
- **Why first + sequential:** it is the foundation B/C/D all build on, and it **touches three of the four shared files** (`queryKeys.ts`, `AdminSideNav.tsx`, `lib/strings.ts`). Landing it alone first removes the biggest shared-file collision risk for the rest of the wave.
- **Gate after A:** lightweight `reviewer` check that hooks compile + keys are well-formed (optional intermediate gate; the mandatory gates are after B/C/D). No PII rendered yet → no security-auditor needed for A alone.

### Batch B (sequential, after A + after design) — P7-06 screens → `FE-1 → FE-2 → (FE-3 ∥ FE-4)`
- **Agent:** `frontend`.
- **Work:** Users list page (FE-1, consumes `useSearchUsers` + design); user detail page (FE-2, consumes `useAdminUserProfile`, renders the two language rows + status badge); then `UserFamilyPanel` (FE-3) and `UserActivityPanel` (FE-4) **in parallel** (independent files, independent hooks, load independently per AC 7).
- **Sub-parallelism:** FE-1 → FE-2 are sequential (deep-link + shared shell); FE-3 ∥ FE-4 once FE-2's detail shell exists.
- **Shared-file touch:** none beyond what Batch A already changed (B creates **new** files under `app/(admin)/users/`). If B needs a copy string Batch A didn't add, it appends to `lib/strings.ts` — see serialization rule.
- **Builds the shared status badge primitive** (used by C). Land it here so P7-07 reuses it.

### Batch C (parallel with D, after B) — P7-07 lifecycle actions → `FE-5 → FE-2/3/4 → FE-1`
- **Agent:** `frontend` (own dispatch).
- **Work:** mutation hooks `useSuspendUser`/`useReactivateUser`/`useDeleteUser` (FE-5) → Suspend/Reactivate/Delete dialogs (FE-2/3/4, parallel among themselves) → actions menu integrated into the P7-06 detail (FE-1).
- **Hooks invalidate `adminUsers.profile(id)` + `adminUsers.list()`** (the Batch-A keys, **not** the legacy `users.*`).
- **Independent of D** — different new component files (`SuspendUserDialog`/`ReactivateUserDialog`/`DeleteUserDialog` vs `GradeOverrideDialog`/`ChangeLearningLanguageDialog`/edit page) and different hooks.

### Batch D (parallel with C, after B) — P7-08 child edit/grade/language → `FE-4 → FE-1 → FE-2/3 → FE-5`
- **Agent:** `frontend` (own dispatch).
- **Work:** mutation hooks (FE-4, incl. the distinctly-named `useAdminChangeChildLearningLanguage`) → child edit page (FE-1, Student-role-gated) → grade + fresh-start dialogs (FE-2/3, parallel) → validation/error/i18n polish (FE-5).
- **Hooks invalidate** the Batch-A detail key (+ activity; learning-language also invalidates child Math/Science progress caches).
- **Independent of C** — see above.

> **Why C ∥ D is safe:** their owned files are disjoint, both depend only on B (the detail page + read hooks + badge), and neither depends on the other. The **only** overlap is the shared file set (below) — serialized, not parallelized.

### Conflict point — P7-07-FE-1 and P7-08 both edit the P7-06 detail page
Both C (actions menu) and D (child-edit entry button) add to `app/(admin)/users/[id]/page.tsx`. This single file is the one place C and D **collide**. Mitigation:
- **Recommended:** have **Batch B** add explicit, empty insertion seams to the detail page header — a `<DetailHeaderActions>` slot for C's actions menu and a `<ChildEditEntry>` slot for D's edit button — so C and D each touch a **different region** of the file. With named seams, the edits are non-overlapping and the parallel dispatch is safe.
- **Fallback (if seams aren't pre-cut):** serialize the *final wiring* — run C's FE-1 and D's edit-entry as a short sequential tail after both batches' dialogs/hooks land. The dialogs + hooks (the bulk of C and D) still run in parallel; only the ~1-file menu/entry wiring serializes.

---

## Shared-file serialization rule (per `docs/dev/PARALLELISM.md` §3)
Because the whole wave is **one branch, one working tree**, concurrent edits to the same file clobber each other. These files are touched by multiple batches and **must be edited by one batch at a time** (never two agents in the same dispatch):

| Shared file | Touched by | Rule |
|---|---|---|
| `packages/api-client/src/query/queryKeys.ts` | A (adds `adminUsers.*`), D (may add child-progress invalidation keys) | **A owns the namespace.** D only *appends* leaf keys after A merges; never two writers at once. |
| `packages/api-client/src/hooks/index.ts` | A, C, D (each export their hooks) | Each batch appends its own export lines; serialize the dispatches (A → then C and D each add their block — if C and D run together, give them **disjoint export regions** or land C's index edit, then D's). |
| `apps/admin-dashboard/lib/strings.ts` (`AdminStrings` + en/ar) | A (base + nav/title), B (list/detail copy), C (dialog copy), D (edit/grade/language copy) | **A seeds the interface; B/C/D append their own slots.** The biggest collision risk in the wave. Run the batches in order; if C ∥ D, give each a **distinct, non-adjacent block** of `AdminStrings` (e.g. C = `lifecycle.*`, D = `childEdit.*`) so appends don't overlap. |
| `apps/admin-dashboard/app/(admin)/users/[id]/page.tsx` | B (creates it), C (actions menu), D (edit entry) | See "Conflict point" above — **pre-cut named seams in B**, or serialize the final wiring. |
| `apps/admin-dashboard/components/AdminSideNav.tsx` | A only | A owns it; no other batch touches it. |
| `@learnexia/shared` `AccountStatus` const (if added — D2) | A (recommended to add it here) | Define the `0/1/2` enum **once** in Batch A; C and D import it. Do not hardcode integers in three places. |

**Operating rule for the lead:** dispatch **A alone**, then **B alone**, then **C ∥ D in one message** *only after* the strings.ts blocks and the detail-page seams are partitioned; otherwise dispatch C, then D. Re-run the build after each batch lands so the next batch starts from a clean tree.

---

## Review / security / e2e gates

| Gate | When | Scope |
|---|---|---|
| `reviewer` (light, optional) | after **Batch A** | hooks compile, `adminUsers.*` keys well-formed, nav active-state correct. No PII yet. |
| `reviewer` | after **Batch B** | P7-06 AC 1–10: list states + filters + pagination, read-only detail, **two distinct language rows**, family/activity independence, admin gate, nav, i18n/a11y. |
| `security-auditor` **(MANDATORY)** | after **B**, and again after **C+D** | **User + child PII** (B) and **destructive child/account actions** (C+D). Audit: AdminOnly is the real gate (FE guard is UX only); no PII persisted beyond session (TanStack cache + sessionStorage only, cleared on sign-out); no new unaudited read path; for C+D — typed-confirm genuinely gates delete/fresh-start, `confirm`/`confirmFreshStart:true` sent only at final step, no optimistic cache-wipe before success, cascade warning accurate (soft-delete, history retained, PII not yet scrubbed), revocation copy not over-promising instant logout (residual JWT window). **Critical/High block the gate.** |
| `frontend-e2e-tester` | after **C+D** (covers B's screens too) | Playwright vs running admin PWA + seeded admin session: admin-gate redirect (anon/non-admin → `/login`); list search/filter/pagination + empty/error; detail incl. both language fields distinct + family deep-links + activity "no data"/"sign-in not tracked"; suspend/reactivate/delete happy + error (already-suspended 400, etc.) + parent cascade; profile edit; grade override (confirm + reason + 422 range + 400 same-grade); learning-language fresh-start (typed confirm required, 424 unconfirmed, destructive copy present); **RTL ar + en** on lists + dialogs. |
| `reviewer` (final) | after security + e2e | Gate the whole wave against all three briefs' AC + `CONVENTIONS.md`; confirm security-auditor + e2e results attached and clean; confirm hooks invalidate **`adminUsers.*`** not the legacy keys; no duplicated badge/dialog primitives; no backend changes. |
| `committer` | after final reviewer **PASS** | **One PR** off `feat/P7-admin-users-fe` covering the whole wave (constraint 5 — single branch). Conventional message; push + open PR; update `docs/dev/HANDOFF.md` (Phase 7 FE section) **in the same PR**. Never on `main`. |

> Note: this wave deliberately uses **one branch + one PR for all three stories** (constraint 5), unlike the per-story branches the individual briefs each suggested. The briefs' "branch `feat/P7-06-…`/`feat/P7-07-…`/`feat/P7-08-…`" lines are **superseded** by the wave-level single-branch instruction.

---

## Open decisions to resolve (recommended default for each — wave is NOT blocked)
Each has a recommended default so the lead can proceed; flag only if the lead disagrees.

| # | Decision | Recommended default (proceed unless overruled) | Source |
|---|---|---|---|
| **D1** | api-client: regen NSwag client vs hand-write hooks | **Hand-write** raw-path hooks (mirror `useUserList.ts`/`useUnlinkChild.ts`) for the whole wave. A full regen has blast radius across the student app and isn't needed for 7 endpoints. Revisit regen as a separate chore. **Not a new abstraction** — same shape as existing hooks. | P7-06 Q3, P7-07 handoff, P7-08 Q4 |
| **D2** | `AccountStatus` enum (0 Active / 1 Suspended / 2 Deleted) source | Define **once** as a shared const/type in `@learnexia/shared`, added in **Batch A**; C and D import it. Don't hardcode ints in three files. | P7-07 Q-5 |
| **D3** | Grade override: required reason vs backend-optional | **FE enforces required** (support-traceability), even though the API accepts null. Pure FE rule. Note: reason is **not** persisted in the immutable audit `Details` (ids/values only) — a durable reason trail would be a backend follow-up, out of scope. | P7-08 Q6 |
| **D4** | Delete copy semantics (soft-delete vs anonymize) | Copy must say the live behaviour: **account disabled/blocked + terminal, learning history retained, cannot sign in; PII not yet erased** (anonymization deferred to a later GDPR/COPPA sweep). **Do NOT claim PII is anonymized.** The task file's "anonymize" wording is superseded by the live contract. | P7-07 Q-4 |
| **D5** | Last sign-in | Label **"Sign-in activity: not tracked"** (backend `lastSignInAtUtc` is always null; no column). Don't block the AC; don't invent tracking. | P7-06 Q1 |
| **D6** | Role filter scope | List filter offers **Parent / Student only** (support targets). **Exclude Admin/SuperAdmin** from the role filter UI (not support subjects). Status filter = Active / Suspended (Deleted hidden unless explicitly requested). | P7-06 Q5 |
| **D7** | Child email in family panel | **Hidden by design** (family DTO returns `email:null` for children). Show name + grade + deep-link; the child's own profile is the path to their email. | P7-06 Q2 |
| **D8** | Typed-confirmation token for Delete / fresh-start | Delete: type the **account email** (uniquely identifies target), fallback `DELETE` if RTL-awkward — **designer locks**. Fresh-start (learning-language): mirror the **parent P8-04** gesture for consistency. | P7-07 Q-6, P7-08 Q3 |
| **D9** | Suspend response is a message string, not a DTO | **Refetch, not optimistic.** Hooks type output `string`/`void` and rely on query invalidation to refresh the badge (the task files' `BaseResponse<AdminUserProfileDto>`/`<bool>` are wrong — live handlers return `BaseResponse<string>`). | P7-07 contract correction |
| **D10** | No suspend-cascade in the live BE | **Drop suspend-cascade from FE scope** (the endpoint has no cascade param). Only **Delete** offers the parent cascade checkbox. Don't build UI the API can't honor. | P7-07 Q-8 |
| **D11** | Pre-existing middleware pass-through (client-side-only route guard) | **Out of scope** — known P1-10 debt (sessionStorage can't be read at the edge). Note to security-auditor so it isn't re-raised as new; true server-side enforcement needs HttpOnly-cookie auth (separate story). | P7-06 Q6 |
| **D12** | Brief filenames (`P7-07-FE.md`, `P7-08-FE.md` split from BE briefs) | **Keep split** — the FE briefs deliberately don't clobber the BE briefs. No action. | P7-07 Q-9 |

> **Genuine backend gap (not a batch):** none requiring a handler change. The only backend-shaped follow-ups are documented + deferred (PII anonymization sweep D4; durable audit-reason trail D3; residual access-JWT window on suspend). Surface these to the lead as **known/accepted**, not wave blockers.

---

## Blockers / prerequisites
1. **Resolved by sequencing:** the P7-06-FE-not-on-disk blocker (P7-07 Q-1, P7-08 blocker) is handled by making **Batch A + B (P7-06) land first**; C and D build on the detail page + hooks it produces. No external blocker remains.
2. **Designer must run before B/C/D** — the shared badge + dialog/confirm primitives need specifying once (so they're built in P7-06 and reused, not duplicated). Designer may overlap Batch A.
3. **No design pattern without asking** (CLAUDE.md rule 8 / memory): the shared dialog wrapper and actions menu must **mirror existing shapes**, not introduce a compound-component/provider pattern. If a batch believes a pattern is genuinely warranted, it **stops and asks the lead** before implementing.
4. **Decisions D1–D12** above should be acknowledged by the lead before dispatch, but each has a safe default — the wave is **not blocked** waiting on them.

---

## Designer handoff (runs next)
**`designer` runs next**, before the frontend batches, on these surfaces:
- **P7-06** → `design-system/ui_kits/admin-users/P7-06.md`: Users list (filters bar, table + column priority, **role/status badge styles**, pagination, 4 states) + read-only detail (profile card, the **two distinct language rows** labeled so they're never read as one value, family panel, activity panel with per-section "no data") + responsive table→stacked + RTL + dark tokens + active-nav treatment.
- **P7-07** → `design-system/ui_kits/admin-dashboard/P7-07.md`: actions menu (legal-transition matrix), the **shared status badge** (Active/Suspended/Deleted — specify once, reused by 06/07/08), the three dialogs, the **typed-confirmation** gesture (lock D8), the parent-only **cascade-children** warning block, destructive-button states, inline error placement, EN+AR copy, a11y/RTL.
- **P7-08** → `design-system/ui_kits/admin-dashboard/P7-08-FE.md`: child edit page (two distinct language controls), grade-override dialog, fresh-start learning-language dialog (mirror parent `design-system/ui_kits/parent-settings/P8-04-FE.md` confirm gesture), EN+AR copy, a11y/RTL.
- Recommend **one designer pass** across all three for a consistent badge + dialog system; ground everything in `design-system/` tokens + the existing P1-10 admin components. No admin icon set exists yet (keep the emoji placeholder approach or propose an icon source as an open question).

---

## Definition of done

**Per batch:**
- **A:** `adminUsers.*` keys + four read hooks + the shared `AccountStatus` const exist and compile; `AdminSideNav` has a real active-aware Users item (`aria-current`); base + nav/title EN+AR copy present; exported from `hooks/index.ts`. (P7-06 AC 8, 9, 10 partial.)
- **B:** P7-06 AC 1–10 met — list (4 states, filters, debounced search, server pagination, page-1 reset, `placeholderData`), read-only detail (both language rows distinct, status reason when present), family panel (deep-links, child email omitted), activity panel (per-section "no data", sign-in "not tracked"), all behind `useAdminGuard`. Shared status badge built. `reviewer` + `security-auditor` (PII) pass.
- **C:** P7-07 AC 1–8 met — legal-transition actions menu, Suspend (required reason + governance copy), Reactivate (prior history), Delete (two-step typed confirm + parent-only cascade + accurate soft-delete copy), inline envelope errors (400/422/424/5xx, dialog stays open), hooks invalidate `adminUsers.*`, EN+AR + RTL + a11y. No duplicate badge/dialog primitives.
- **D:** P7-08 AC 1–10 met — child-only surface, harmless profile edit (changed-fields-only PATCH), `LearningLanguage` as a separate confirm-gated control, fresh-start dialog (typed confirm, 424 handled, destructive copy), grade override (1–6, required reason FE-rule, re-scope/preserve copy, 422/400 inline), cache coherence (detail + Math/Science on language change), EN+AR + RTL + a11y.

**Overall (wave):**
- All three stories' acceptance criteria pass against their briefs.
- `security-auditor` MANDATORY pass (no Critical/High) on the PII + destructive surfaces; `frontend-e2e-tester` green on the admin PWA flows (incl. RTL ar+en); final `reviewer` PASS against the briefs + `CONVENTIONS.md`.
- Hooks invalidate the **admin** `adminUsers.*` keys (never the stale legacy `users.*`); no backend changes; no new design patterns introduced unilaterally.
- `committer` opens **one PR** off `feat/P7-admin-users-fe` with `docs/dev/HANDOFF.md` (Phase 7 FE section) updated in the same PR; never on `main`.

---

Plan ready — dispatch Batch 1.
(Order: **designer** (3 surfaces, may overlap A) → **Batch A** `frontend` (P7-06-FE-5 foundation) → **Batch B** `frontend` (P7-06 list+detail+family+activity) → **Batch C ∥ Batch D** `frontend` (P7-07 lifecycle ∥ P7-08 child edit, per the serialization rule) → **security-auditor** + **frontend-e2e-tester** → final **reviewer** → **committer** single PR.)
