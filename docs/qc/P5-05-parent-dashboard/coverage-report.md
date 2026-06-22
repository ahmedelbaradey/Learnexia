# P5-05 Parent Dashboard — QC Coverage Report (Frontend / Web E2E)

**Story:** P5-05 — Parent Dashboard · **Surface:** `apps/student-app` parent area (web PWA)
**Companion:** [`frontend-test-cases.md`](./frontend-test-cases.md) · [`execution-report.md`](./execution-report.md)
**Scope this cycle (per plan lead decisions):** P5-05 charts + real-data wiring only.
P5-06 grade-transition and P5-04 send-report are **DEFERRED / OUT** — no FE surface exists, so the
corresponding acceptance criteria are explicitly **not testable** and are flagged below.

---

## 1. Summary

| Metric | Count |
|---|---|
| Total cases | 50 |
| `[EXISTING]` (already in `P5-05-parent-dashboard.spec.ts`) | 14 |
| `[ENHANCE]` (exists but soft/weak — tighten) | 7 (subset of EXISTING) |
| `[NEW]` (gaps to implement) | 29 |
| P0 | 13 |
| P1 | 22 |
| P2 | 15 |

**Verdict:** Every **in-scope (P5-05)** acceptance criterion has at least one P0/P1 case. The deferred
**P5-06** and **P5-04** criteria are uncovered **by design** (no FE surface — documented as blocked, not gaps).
The biggest real gaps the existing spec leaves are: **child-switch data isolation** (no test switches children),
**populated-data assertions** (skipped), **per-panel error/retry breadth**, **chart-direction RTL**, and the
**absolute-`xpDelta` (GAP-8) regression**.

---

## 2. Acceptance-criteria → test-case traceability

### 2.1 P5-05 story acceptance criteria (source: user story)

| # | Acceptance criterion (story) | Covered by | Status |
|---|---|---|---|
| AC-1 | Dashboard shows latest weekly report, weak areas (severity), progress charts, recommendations | TC-05, TC-07, TC-08, TC-09, TC-10, TC-12, TC-13, TC-22 | Covered |
| AC-2 | Parent with multiple children can switch; each shows only that child's data | TC-27, TC-28, TC-29, TC-30 | Covered (TC-29 is the critical isolation case — **NEW**) |
| AC-3 | Per-child grade-transition control + confirmation step (calls P5-06) | — | **NOT TESTABLE — deferred (no FE surface; P5-06-BE missing)** |
| AC-4 | After grade transition, tree reflects new grade next load; history retained | — | **NOT TESTABLE — deferred** |
| AC-5 | Dashboard renders in Arabic (RTL) and English | TC-43, TC-44, TC-45, TC-46, TC-47 | Covered |
| AC-6 | Empty/first-week states handled gracefully (no charts-with-no-data errors) | TC-05, TC-08, TC-09, TC-10, TC-16, TC-19, TC-20, TC-21 | Covered |

### 2.2 P5-05 brief acceptance criteria (testable, FE in-scope)

| Brief AC | Covered by | Status |
|---|---|---|
| Overview KPIs from `WeeklyKpis` (not stub) | TC-05, TC-06 | Covered |
| `xpDelta` is ABSOLUTE, copy not a `%` (GAP-8) | TC-06, TC-47 | Covered (**NEW** — not in existing spec) |
| Subject mastery: 4 subjects + overall; zero not hidden | TC-08, TC-13, TC-48 | Covered |
| Weak areas with severity; empty → graceful empty state | TC-09, TC-38 | Covered |
| Recommendations resolve EN/AR i18n keys; empty → empty state | TC-10, TC-39, TC-43/44 | Covered |
| Daily-activity bar chart from `Reports.dailyXpSeries`; Export CSV real | TC-07, TC-22, TC-23, TC-24 | Covered (CSV download = **NEW**) |
| Reports: 20-day trend (exactly 20) + time-of-day (4 buckets) | TC-12, TC-15, TC-16, TC-17, TC-18, TC-19, TC-25 | Covered (20-count + 4-bucket asserts = **NEW**) |
| Multi-child switch re-fetches per active child only | TC-29, TC-30 | Covered (**NEW**) |
| RTL/ar + en for every panel + chart; direction-aware charts | TC-43–TC-47 | Covered (chart direction = **NEW**) |
| Empty/first-week zero-state, never 404, no chart errors | TC-16, TC-19, TC-20, TC-21 | Covered |
| Error per panel → generic strip + retry; 403 leaks no oracle | TC-31–TC-40 | Covered (per-panel breadth + UI-403 = **NEW**) |
| `BaseResponse` envelope honored (`successed`); pagination metadata | TC-49, TC-50 | Covered (**NEW**) |

### 2.3 Product-decision invariants (CLAUDE)

| Invariant | Covered by |
|---|---|
| 4 subjects, no Social Studies | TC-08, TC-48 |
| No teacher role / no student self-register | TC-04 |
| Parent linkage scopes access (IDOR) | TC-31, TC-32, TC-33 |

---

## 3. Gap analysis vs the existing spec

**What the existing `P5-05-parent-dashboard.spec.ts` already covers (14):** TC-01, 02, 05, 07, 08, 12, 13, 14,
16, 20, 23, 27, 31, 43, 44, 45, 48 (legacy TC-01..24 minus the two SKIPs and the now-removed stale-bundle fallbacks).

**Material NEW gaps this catalog adds (29), grouped by risk:**

1. **Child-switch & cross-child isolation (highest risk):** TC-29 (re-fetch per active child, no A-data linger),
   TC-28 (dropdown with ≥2 children — resolves SKIP TC-10), TC-30 (active child persists across nav).
   *The existing suite never switches children — AC-2's core promise is effectively untested today.*
2. **Populated-data assertions (closes SKIPs):** TC-22 (daily bars), TC-15 (exactly 20 bars), TC-17 (4 named
   buckets), TC-18 (peak insight — resolves SKIP TC-21), TC-13 (mastery > 0). Use interception to remove the
   "fresh child = all-zero" blocker that forced the SKIPs.
3. **Export CSV actually works:** TC-24, TC-25 (download event + header + row count) — current spec only checks
   the button exists.
4. **GAP-8 regression:** TC-06, TC-47 (absolute `xpDelta`, no `%`, Eastern-Arabic numerals) — the single highest-
   value correctness regression and entirely absent today.
5. **Per-panel error breadth + recovery:** TC-35 (retry recovers), TC-36 (Reports 500 → both charts), TC-37/38/39
   (mastery/weak/recommendations 500), TC-40 (children-list 500). Existing spec only tested `WeeklyKpis` + a
   stale-bundle-conditioned `Reports`.
6. **Chart RTL behavior:** TC-46 (bars stay LTR, Latin axis, LTR-safe value labels) — not asserted today.
7. **Loading states:** TC-41, TC-42 — no skeleton assertion exists.
8. **Empty-family / no-child:** TC-21 (`reports-add-child-band`).
9. **IDOR breadth + UI surface:** TC-32 (all endpoints 403, never 404), TC-33 (UI shows generic error, no oracle).
10. **Envelope edge:** TC-49 (`successed:false` on a 200), TC-50 (pagination dual-shape normalization).
11. **DEF-P5-01 regression:** TC-26 (assert `daily-activity-card` testID actually emits — turns the fallback into
    a real assertion).
12. **Auth breadth:** TC-03 (`/reports` guard), TC-04 (no teacher / no self-register).

---

## 4. Implementation-vs-spec divergences QC must enforce

These are documented in detail in `frontend-test-cases.md §0`. Tests MUST assert the **real** shapes:
single `/Reports` endpoint (not 3); `{day,xp}` not `{date,xpEarned}`; **4 named TOD buckets** not 8 hours;
`bySubject[].subjectCode`(0-indexed)/`percent` not `subjects[].subjectId`/`masteryPercent`; **no `lessonsCount`**
on mastery rows; lowercase `successed` on the wire. Any test written to the design-spec shapes will be wrong.

---

## 5. Risk notes

- **Cross-child data leakage** is the riskiest untested area: query keys are childId-scoped, but nothing proves
  a switch doesn't show stale child-A data. TC-29 is P0.
- **OQ-3.5 silent-zero degrade:** a missing backend seam returns zeros, not an error. Real-attempt seeding can
  therefore make a *broken* panel look like a *correct* empty state. Prefer interception (TC §12) for populated
  cases so a rendering bug isn't masked as "no data".
- **GAP-8 (`xpDelta` absolute):** a regression here is silent (a wrong but plausible number). TC-06 guards it.
- **Locale propagation (DEF-P5-03):** EN only applies if set before app init. Make TC-45 deterministic or its
  Arabic-vs-English coverage stays soft.

---

## 6. Open questions / assumptions for the lead

1. **Interception vs real seeding** for populated cases — assumed interception is acceptable for FE-render
   correctness (TC §12). Confirm; if the lead wants real end-to-end data, an attempt-seeding helper is needed.
2. **FamilySummary wiring status (TC-11):** the hooks file marks Progress/Energy/Family partially deferred; if
   `FamilySummaryStrip` still consumes `FamilyTotalsStub`, TC-11 documents a wiring gap rather than a pass.
3. **AC-3 / AC-4 (grade transition) and P5-04 send-report** are deferred with no FE surface — confirmed not gaps.
   They become testable only after a P5-06-BE story + FE wiring; re-run QC then.
4. **TC-50 dual pagination shape** may not be exercisable against a single live backend; if so, cover the live
   shape and treat the normalization as documented (not asserted) — confirm acceptable.

---

## 7. Handoff

- **`frontend-e2e-tester`** implements the `[NEW]` and `[ENHANCE]` cases in
  `tests/e2e/specs/P5-05-parent-dashboard.spec.ts` (reusing the existing seed helpers + login flow), prioritizing
  P0 → P1, and records pass/fail per `P5-05-TC-NN` into `execution-report.md` (append a new run section; do not
  overwrite the 2026-06-21 run).
- This QC stage **designs only** — no executable code was written here.
