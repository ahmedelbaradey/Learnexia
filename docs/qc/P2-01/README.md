# QC Test Plan & Coverage Report — P2-01 (Model the curriculum hierarchy)

**Surface:** Backend API only (Learning module curriculum CRUD).
**Run scope:** Single story P2-01, backend HTTP surface (`api/learning/{grades,subjects,units,lessons,concepts,skills}`).
**Designer:** qc-test-designer (Opus). **Design-only** — no test code, no execution.
**Generated for:** `api-tester` to implement `backend-test-cases.md`; results recorded in `execution-report.md`.

---

## 1. Summary

P2-01 models the curriculum hierarchy **Grade → Subject → Unit → Lesson → Concept → Skill** as a new `learning`
module (schema `learning`) with full CRUD vertical slices and HTTP controllers. Since the original P2-01 ship,
two material changes landed that this pass must cover:

- **Authz tightening (PR #100 / #104):** curriculum **write** endpoints (`Create`/`Update`/`Delete`) on **all six**
  controllers are now `[Authorize(Policy = AdminOnly)]`. `GradesController` is additionally `[Authorize]` at the
  class level (its reads require any authenticated user). The other five controllers' `List`/`GetById` remain
  anonymous.
- **P8-02 bilingual key:** `Subject` gained `SubjectCode` (MATH/SCIENCE/ARABIC/ENGLISH) + `Language` (Ar/En) with a
  **UNIQUE index `(GradeId, SubjectCode, Language)`**. **Load-bearing observation:** `AddSubjectCommand`/`AddSubjectDto`
  do **not** expose `SubjectCode` or `Language`, and `SubjectsProfile` maps the command straight to the entity, so a
  subject created via `POST /api/learning/subjects/Create` always defaults to `SubjectCode=MATH(0)`,`Language=Ar(0)`.
  Consequently **two subjects created under the same grade via the public Create endpoint collide on the unique key**
  and the second write hits the DB unique violation. This is the reachable "duplicate → conflict" path for this story
  (see Risk R1 and BE-TC-30..32).

These cases **EXTEND** `backend/tests/Learnexia.IntegrationTests/P2_01_CurriculumHierarchy_Tests.cs`. Where the
existing suite already covers a behavior, the new case **cross-references** it (column "Extends/Existing") rather than
duplicating it. New cases concentrate on: the unique-constraint conflict path, full FK-integrity matrix across the
hierarchy (not just Subject→Grade), the **non-admin write → 403** axis (the existing suite only checks anonymous reads,
not the new AdminOnly write gate), status-code/envelope mapping for the write gate, and product-override negative
assertions.

### Counts

| Metric | Count |
|---|---|
| **Total cases** | **37** |
| Backend (api-tester) | 37 |
| Frontend | 0 (out of scope — backend-only story) |
| **P0** | 17 |
| **P1** | 14 |
| **P2** | 6 |
| New cases authored here | 37 |
| Of which **cross-reference** existing tests (extend, don't re-run) | 11 |

---

## 2. Coverage matrix (acceptance criterion → case IDs)

Acceptance criteria taken from the brief / plan **Definition of Done** (AC-1..AC-8) plus the lead-supplied
current-state contract (authz, unique constraint). Each criterion maps to at least one P0/P1 case.

| AC | Criterion (abridged) | Covering case IDs | Gap? |
|----|----------------------|-------------------|------|
| **AC-1** | Module exists; full CRUD round-trip per aggregate; full hierarchy creation | BE-TC-01, BE-TC-02, BE-TC-03, BE-TC-04, BE-TC-05 | No |
| **AC-2** | `BaseResponse<T>` envelope + `Successed` spelling; `PaginatedResult` shape | BE-TC-06, BE-TC-07, BE-TC-08, BE-TC-09 | No |
| **AC-3** | Relationships per SRS §6 (parent→child chain wired) | BE-TC-05, BE-TC-10, BE-TC-11 | No |
| **AC-4** | Skill mastery/time + Lesson difficulty/order/lock + nullable SkillId persist | BE-TC-12, BE-TC-13, BE-TC-14, BE-TC-15 | No |
| **AC-5 (Q7)** | Validation → **422** on every command (ICommand bodies) | BE-TC-16..BE-TC-24 | No |
| **AC-6** | Migration created six tables/FKs/indexes (incl. unique index) on PostgreSQL | BE-TC-25, BE-TC-26 | No |
| **Authz-1** | Anonymous → **401** on protected endpoints (all writes; Grades reads) | BE-TC-27, BE-TC-28 | No |
| **Authz-2** | Non-admin authenticated → **403** on every write (AdminOnly gate) | BE-TC-29 | No |
| **Authz-3** | Admin → **200** on writes; reads reachable | BE-TC-01..05, BE-TC-08 | No |
| **DataInt-1** | `(GradeId,SubjectCode,Language)` unique constraint → duplicate conflicts | BE-TC-30, BE-TC-31, BE-TC-32 | No |
| **DataInt-2** | FK integrity: child under non-existent parent fails gracefully (non-2xx) | BE-TC-33, BE-TC-34, BE-TC-35, BE-TC-36 | No |
| **Product** | 4 subjects / no Social Studies; no teacher role | BE-TC-37, BE-TC-29 (parent/basic, not teacher) | No |

**Coverage verdict: every acceptance criterion + the lead-named current-state contract has at least one P0/P1 case.
No uncovered AC.**

---

## 3. Risk notes (where cases are weighted, and why)

- **R1 — Unique-key conflict is reachable but the create path can't set the key (HIGH).** Because
  `AddSubjectCommand` does not expose `SubjectCode`/`Language`, every API-created subject defaults to `(MATH, Ar)`.
  A second subject under the same grade therefore violates `IX_Subjects_GradeId_SubjectCode_Language`. The handler
  wraps the failure in `try/catch → ServerError<string>()` (HTTP 500, `Successed=false`), **not** a clean 409.
  Weighted three cases (BE-TC-30..32) here: assert the duplicate is *rejected* (non-2xx, `Successed=false`, valid
  JSON envelope, first row still retrievable) **without** over-asserting a specific 409 — the implementer should
  record the *actual* status. **This is the top item for the lead** (see Open Question Q1): is a 500 the intended
  contract for a duplicate, or should this surface a 409/422 and/or should the create command expose code/language?

- **R2 — FK integrity across the whole chain, not just Subject→Grade (MEDIUM).** The existing suite checks only
  `Subject` under a non-existent `GradeId`. The hierarchy has four more required FK edges
  (Unit→Subject, Lesson→Unit, Concept→Subject, Skill→Concept) plus the optional Lesson→Skill (SetNull). Orphan
  creates on those edges are weighted P1 (BE-TC-33..36) — they exercise that `DeleteBehavior.Restrict` and the
  UoW rollback behave consistently and never leak a naked exception page.

- **R3 — The AdminOnly write gate is new and only partially tested (HIGH).** The existing suite verifies anonymous
  reads and that writes succeed *with* the admin token, but does **not** assert the negative axes the gate
  introduced: anonymous write → 401, and **authenticated-but-non-admin** write → 403. A regression that loosens
  any of the six controllers' write attributes would pass the current suite. Weighted P0 (BE-TC-27..29), with
  BE-TC-29 as a `[Theory]` sweeping all six controllers × Create/Update/Delete so no endpoint silently opens up.

- **R4 — Status-code mapping drift (MEDIUM).** Creates return **HTTP 200** (handlers call `Success()`, not
  `Created()`), so 201 must **not** be asserted (BE-TC-01 note). 422 is emitted by the validation middleware, not
  the 400 path. These are easy to get wrong; the envelope-shape cases (BE-TC-06..09, BE-TC-24) pin them.

- **R5 — Rollback on a multi-write is not exercised (LOW for P2-01).** P2-01 creates are single-aggregate, so there
  is no genuine multi-write transaction to roll back. BE-TC-32 (first subject survives after the duplicate fails)
  is the closest persistence-integrity assertion available and is included; a true atomic-rollback case is deferred
  (noted as not-applicable rather than dropped).

---

## 4. Open questions / assumptions (lead decision needed before/while implementing)

- **Q1 (blocking the duplicate cases' expected status):** What is the intended contract when the
  `(GradeId,SubjectCode,Language)` unique key is violated via the Create endpoint? Today it surfaces as **HTTP 500
  `ServerError`** (DB exception caught in the handler), because the create command cannot set `SubjectCode`/`Language`
  so all API subjects collide on `(MATH, Ar)`. Options: (a) accept 500 as the documented behavior for now;
  (b) expose `SubjectCode`+`Language` on `AddSubjectCommand` and add a pre-check → clean 409/422. **Assumption used
  in the cases:** assert the write is *rejected* (non-2xx, `Successed=false`, valid envelope, first row preserved)
  and have `api-tester` record the observed status code, rather than hard-coding 409. Flag the observed code in the
  execution report so the lead can decide.
- **Q2:** Should `POST /subjects/Create` validate `GradeId` *existence* (returning 404/422) rather than letting the
  FK violation reach `SaveChanges` (→ 500)? Same pattern applies to all child-create FK edges (BE-TC-33..36).
  **Assumption:** current behavior is "non-2xx, graceful JSON envelope"; cases assert that, not a specific 404.
- **Q3:** Is a non-admin authenticated user guaranteed available to the integration harness for the 403 sweep?
  **Assumption (confirmed from `P1_05_RBAC_Tests`):** `basicuser/123Pa$$word!` (role Basic) and a freshly
  `Register-Parent`ed parent token are both usable. BE-TC-29 uses `basicuser` as the primary non-admin; a parent
  token is the documented fallback.
- **Q4:** Are there other curriculum-write controllers gated by a different policy than `AdminOnly`
  (e.g. content-import)? **Assumption:** only the six core CRUD controllers are in scope for P2-01; KnowledgeGraph /
  Quizzes / Students / Dashboard controllers are out of scope for this run.

---

## 5. Handoff

| File | Goes to | Action |
|---|---|---|
| `docs/qc/P2-01/backend-test-cases.md` | **`api-tester`** | Implement each `BE-TC-*` as one integration test, **extending** `P2_01_CurriculumHierarchy_Tests.cs` (or a sibling `P2_01_CurriculumHierarchy_Extended_Tests.cs`). Cross-referenced cases: assert the existing test already covers it, do not duplicate. |
| `docs/qc/P2-01/execution-report.md` | **`api-tester`** (fills it) | After running, record pass/fail per `BE-TC-*` + defects. For BE-TC-30..36 record the **actual** status code observed (Q1/Q2). The designer leaves this file as an empty template; testers fill results — the designer never fills results. |

Run order: `api-tester` runs against the live API (per the plan's Batch 5), then `reviewer` gate 2.

---

*Designed by qc-test-designer. No executable test code, no builds/servers run, no feature code edited.*
