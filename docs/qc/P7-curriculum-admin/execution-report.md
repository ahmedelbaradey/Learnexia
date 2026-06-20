# P7 Curriculum (Admin) — E2E Execution Report

> Filled by the frontend lead after implementing + running `frontend-test-cases.md` on the live stack.
> One row per case ID. Status = PASS / FAIL / BLOCKED / SKIPPED.

## Run metadata

| Field | Value |
|-------|-------|
| Date / time | 2026-06-19 |
| Runner | frontend lead (live stack) |
| Admin app URL | http://localhost:3001 |
| Backend URL | http://localhost:5080 |
| Admin commit / branch | `test/P7-admin-qc-e2e` (off `main` @ 4270111) |
| Playwright project | `admin` (via `playwright.admin.config.ts` — admin-only, isolates :3001) |
| Auth fixture | per-spec login (testID form, `superadmin`/`123Pa$$word!`) |
| Seed state | LearningSeeder (36 subjects, 278 skills); throwaway subjects created into free (grade,code,lang) slots via Coverage |

## Result summary

| Status | Count |
|--------|------|
| PASS | 68 |
| FAIL | 1 |
| SKIPPED | 16 |
| **Executed** | **85** |
| (Designed) | 94 |

The 9 designed-but-not-executed cases were folded into adjacent cases or not authored as standalone tests (P2 edge cases). The 16 SKIPPED are conditional runtime skips (no seed rows for the chosen subject/concept, RTL runtime-unreachable, or a precondition not met) — not failures.

## CUR-TC-66 — Skills filters work — **FIXED by backend PR #193** (was FAIL)

- Selecting a subject + a non-matching search term should empty the skills table → `skills-empty-state`.
- Original root cause: `GET /api/learning/Skills/List` ignored the `Search` param entirely (`totalCount` = 278 for any term). FE sent `Search` correctly (`useSkillList` → `?Search=`).
- **Backend fixed it in PR #193 (`fix(learning): CUR-TC-66 Skills/List applies the Search filter`), merged to main, with its own regression test.** This case will PASS once the running backend is rebuilt from current main; the last local run here was against a pre-#193 backend build. No FE change required.
- (Separate, lower-priority backend note: the endpoint still has no `SubjectId` filter — only `ConceptId`/`Search`/paging — so a selected subject narrows the concept dropdown + graph but not the table directly. Not part of CUR-TC-66.)

### CUR-TC-71 — Graph keyboard navigation — FIXED ✅ (was FAIL)
- ArrowDown now sets the first node `aria-selected="true"` and advances correctly on subsequent presses; no regression to row↔graph sync (CUR-TC-70 PASS).
- Two compounding root causes fixed in FE (see FIX-6/FIX-7 below).

## Defects FIXED during this run (so the suite could pass)

| # | Type | Fix |
|---|------|-----|
| FIX-1 | **FE product defect** | `packages/api-client/src/client/apiClient.ts` `requestPaginated` now normalizes BOTH paginated wire shapes. Backend is inconsistent: Identity (`/api/Admin/Users`) returns a flattened `PaginatedResult`; Learning/Moderation/Audit return `BaseResponse<PaginatedResult>` (inner under `.data`). Previously the client returned the outer envelope for double-wrapped endpoints → pages did `data?.data` → got the inner object → `.filter`/`.map` crashed every curriculum + audit list/detail page ("Application error"). This single bug had blocked ~66 cases. |
| FIX-2 | **FE product defect** | `app/(admin)/curriculum/subjects/[id]/page.tsx` resolved the subject from `useSubjectList({pageNumber:1})` (default pageSize 12) + `.find` → any subject outside the first 12 showed "not found" and its units never rendered. Now `pageSize:100` (hard 48-subject cap guarantees coverage). Avoids `GET Subjects?id=` because that endpoint returns 500 — not 404 — on unknown id (backend bug), which would break the clean not-found UX. |
| FIX-3 | spec helpers | create URL `/api/learning/Subjects/Create`; delete `?id=` query param; id resolved via `Subjects/List?Search=`; throwaway subjects target free `(grade,code,lang)` slots via `Subjects/Coverage` (unique-tree constraint). |
| FIX-4 | spec bug | CUR-TC-69 now selects the required **Concept** before saving → PASS. |
| FIX-5 | spec bug | CUR-TC-85 uses `waitUntil:'domcontentloaded'` (invalid preview type leaves a retrying request → `networkidle` never settles) → PASS. |
| FIX-6 | **FE product defect** | `app/(admin)/curriculum/skills/page.tsx` passed an inline arrow for `onSelectSkillId`. `SkillGraph`'s subject-change `useEffect` lists `onSelectSkillId` in its deps, so the new reference every render made that effect fire on every render and `setSelectedNodeId(null)` — wiping the graph node selection on every keyboard/click selection (the dominant CUR-TC-71 cause). Now a stable `useCallback`. |
| FIX-7 | **FE product defect** | `components/SkillGraph.tsx` selection-sync effect cleared the node selection whenever `externalSelectedSkillId` was null — including when the graph itself selected a **concept node** (no `skillId` → reports null upward). Now it keeps the selection when the currently-selected node is a non-skill node. |
| INFRA | harness | added `tests/e2e/playwright.admin.config.ts` (admin-only project + reuse :3001) so a stale marketing :3002 webServer no longer aborts every admin run with EADDRINUSE. |

## Must-have
- **CUR-TC-53 "Create Matching + grades correctly" — PASS.**

## Defects to hand to the backend lead (also in HANDOFF.md)
1. `Skills/List` ignores `Search` (and has no `SubjectId` filter) — CUR-TC-66.
2. `GET /api/learning/Subjects?id={unknown}` returns **500**, not 404.
3. Paginated envelope inconsistency: Identity flattened vs Learning/Moderation/Audit double-wrapped. FE now tolerates both (FIX-1); normalizing the backend would let FIX-1 be simplified later.
