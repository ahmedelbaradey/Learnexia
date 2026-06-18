# QC Test Plan — P7 Admin Console Batch 1 (P7-06 + P7-07 + P7-08)

> Per-run QC folder produced by `qc-test-designer` (2026-06-18). **Design only** — no executable test code, nothing run. The `frontend-e2e-tester` implements the cases next.

## Files in this folder

| File | Owner | Purpose |
|---|---|---|
| `README.md` (this) | qc-test-designer | Test plan + coverage summary + handoff. |
| `frontend-test-cases.md` | qc-test-designer | **77** Web-E2E cases for `frontend-e2e-tester` (admin screens, port 3001). |
| `coverage-report.md` | qc-test-designer | AC→case matrix, gaps/deviations, BE contract-smoke (pre-existing), test-data/seeding, the **missing-testID + harness handoff**, open questions. |
| `execution-report.md` | the testers | Filled **after** the E2E run — pass/fail/blocked per case + defects. Scaffolded empty here; qc-test-designer never fills results. |

> **No `backend-test-cases.md`.** Per the request, the backend admin endpoints (`AdminUsersController`, all `[Authorize(AdminOnly)]`) are already built, merged, and tested in earlier phases — **BE coverage is pre-existing**. A short BE contract-smoke set is listed in `coverage-report.md` §4 (informational; only if the lead wants a re-verify). There is no net-new HTTP surface to design tests for.

## 1. Summary

- **In scope:** frontend E2E of the admin-dashboard User & Account management wave shipped to `main` — P7-06 (search + inspect), P7-07 (suspend / reactivate / delete), P7-08 (child edit / grade / learning-language). App: `apps/admin-dashboard` (Next.js 15, port 3001).
- **Out of scope:** new backend tests (pre-existing); the P7-12 audit store; true token-revocation timing (backend concern).
- **Counts:** **77 cases, all frontend** (`frontend-e2e-tester`). **P0 = 38 · P1 = 27 · P2 = 12.** Sections: auth/routing (7), P7-06 list (13), P7-06 detail (13), P7-07 lifecycle (17), P7-08 child edit incl. shared dialog a11y (15), cross-cutting RTL/a11y/PII/no-optimistic (9).
- **Grounding:** every case is written against the **actually-shipped code** (`app/(admin)/users/**`, `components/*`, `lib/strings.ts`, `packages/api-client/src/hooks/*`, `AdminUsersController.cs`), not the imagined design. Where the code deviates from the Design Spec, the Expected result describes **reality** and the deviation is flagged `[DEVIATION]`.

## 2. Coverage verdict

**Every acceptance criterion across P7-06 (AC 1–10), P7-07 (AC 1–8), and P7-08 (AC 1–10) is covered by at least one P0/P1 case.** Full AC→case-ID matrices are in `coverage-report.md` §2. No acceptance criterion is left uncovered.

The only "soft" areas are **limitations/deviations of the shipped surface**, not holes in the test design — each is encoded into a case with `[DEVIATION]`/BLOCKED guidance:
- Runtime RTL is unreachable (no locale toggle) → RTL cases require an `ar` build or are BLOCKED.
- The list is not responsive (fixed 5-col table vs the spec's stacked layout).
- Two shipped copy/mapping bugs on the child-edit page (profile-save success string; profile-PATCH 422 message) — flagged for fix-or-file.
- Detail not-found renders the error branch (still user-friendly).
- Audit (P7-12) and access-JWT revocation timing are out of E2E scope.

## 3. Risk notes (where coverage is weighted)

1. **Destructive child actions** (Delete + cascade, learning-language fresh-start that hard-deletes Math/Science) — heaviest P0 weighting: two-gate Delete, `confirm`/`confirmFreshStart:true` only at the final step, case-sensitive `CONFIRM`, no optimistic wipe, backdrop-no-dismiss, no PII leak.
2. **The two-language confusion** — P7-06 AC 4 ("never merged"): conflating `preferredLanguage` with `learningLanguage` is both a correctness and a safety bug. FE-TC-23 is P0.
3. **Auth boundary** — anon + non-admin both redirect, never render PII; sign-out clears the cache. FE guard is UX-only (backend is the real gate); known client-side-only middleware is accepted debt.
4. **Status-machine correctness** — the legal-action matrix per `accountStatus` (FE-TC-34/35/36).
5. **Refetch-not-optimistic** — mutations return only a message string, so the UI must refetch; a regression to optimistic would show wrong state on error (FE-TC-76).

## 4. Open questions / assumptions (need a lead decision before implementation)

(Full list in `coverage-report.md` §8.)
- **Q-A — RTL reachability:** no runtime locale toggle (`ADMIN_LOCALE='en'`). Run an `ar` build for RTL (FE-TC-70/71/72), or accept English-only runtime + strings-file verification? Recommend the `ar` build if feasible; else BLOCK those cases.
- **Q-B — admin credentials:** no admin self-register. What admin/SuperAdmin login (or token-mint path) is available for the test environment? Hard prerequisite for every case.
- **Q-C — shipped copy bugs:** fix the profile-save success message + profile-PATCH 422 mapping in `edit/page.tsx` before the run (so specs assert correct copy), or assert current behaviour + file defects? Recommend fix-first.
- **Q-D — testIDs + harness:** confirm `frontend` will add the `coverage-report.md` §6 testIDs + an admin Playwright project (the harness today only targets student-app :8081 / marketing :3002) before the run.
- **Assumption:** no net-new backend tests (BE pre-existing); `backend-test-cases.md` intentionally omitted.

## 5. Handoff

- The two prerequisites in `coverage-report.md` §6 (**add an admin-dashboard Playwright project on :3001** + **add the listed `testID`s**) and the auth/seed fixtures in §7 must be in place before the run.
- `frontend-e2e-tester` implements **all 77** `FE-TC-*` cases from `frontend-test-cases.md`, English/LTR by default; RTL cases per Q-A.
- Results (pass/fail/blocked + defects) go into `execution-report.md`. `qc-test-designer` does not fill results.

Test cases ready — `api-tester` has no `backend-test-cases.md` to implement (backend coverage is pre-existing; see `coverage-report.md` §4); `frontend-e2e-tester` to implement `frontend-test-cases.md`; results go into `execution-report.md`.
