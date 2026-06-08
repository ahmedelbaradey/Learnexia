# Backend Test Cases — P2-11 (Skill dependency graph API)

**Target agent:** `api-tester`
**Surface under test (shipped):**
- `GET /api/Learning/KnowledgeGraph/Prerequisites/{nodeId:int}` — `[Authorize]`
- `GET /api/Learning/KnowledgeGraph/UnlockedBy/{nodeId:int}` — `[Authorize]`

**Harness:** `backend/tests/Learnexia.IntegrationTests/` — `LearnexiaWebAppFactory` (Testcontainers PostgreSQL, Testing env, rate-limit bypass), `[Collection("IntegrationTests")]`. Existing file: `P2_11_KnowledgeGraph_Tests.cs` (T1–T6 = BE-TC-01..06). Seed via `LearningSeeder.SeedAsync(scope.ServiceProvider)` in `InitializeAsync` (Learning seeder does not auto-run in Testing).

**Envelope contract (`BaseResponse<T>` via `BaseResponseHandler`):**
- `Success` → 200, `successed=true`, `data` = list.
- `EmptyCollection` → 200, `successed=true`, `data = []`.
- `NotFound` → 404, `successed=false`, no/empty data.
- `ServerError` → 500, `successed=false` (must NOT occur on the read paths).
- Success boolean serializes as **`"successed"`** (intentional spelling). Controller path = Newtonsoft camelCase; auth/middleware path = System.Text.Json. Use case-insensitive property lookup (`TryProp` helper already in the harness).

**Seeded fixtures (resolve ids from DB by these exact names — do not hard-code ids).** English Math chain (within-subject, cross-grade):
`Count to 1000 (G1)` → `Compare and Order Numbers (G1)` → `Add Single-Digit Numbers (G1)` → `Subtract Within 100 (G2)` → `Multiply Single-Digit Factors (G3)` → `Identify Unit Fractions (G5)` → `Compare Fractions with Same Denominator (G5)` → `Convert Fractions to Decimals (G6)`. (A parallel Arabic chain exists with the `(صN)` suffix.)

**Auth helper:** sign in via `POST /api/Users/Authentication/Sign-In` (`{ UserName, Password }`), read `data.accessToken`. Superadmin: `superadmin` / `123Pa$$word!`. For non-admin cases, sign in as a seeded Parent or Student (resolve credentials from the identity seeder; if none seeded, report the missing fixture to the lead rather than skipping silently).

---

## §A — Testable now (against the running API)

### BE-TC-01 — Prerequisites of a known node returns 200 with the expected direct prereq
- **Type:** functional
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** Graph seeded. Superadmin token. Resolve node ids by name: target = `Add Single-Digit Numbers (G1)`, expected prereq = `Compare and Order Numbers (G1)`.
- **Steps:**
  1. Resolve `targetNodeId` and `expectedPrereqNodeId` from the DB by name.
  2. `GET /api/Learning/KnowledgeGraph/Prerequisites/{targetNodeId}` with bearer token.
- **Expected result:** 200; `successed=true`; `data` is a non-empty array; an item with `id == expectedPrereqNodeId` is present. Each item exposes `id, name, nodeType, subjectId, gradeId, difficulty, skillId` keys.
- **Traces to:** AC-5 ("prerequisites of X"). (Existing T1.)

### BE-TC-02 — UnlockedBy of a known node returns 200 with the expected next node
- **Type:** functional
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** source = `Compare and Order Numbers (G1)`, expected unlocked = `Add Single-Digit Numbers (G1)`. Superadmin token.
- **Steps:**
  1. Resolve `sourceNodeId` and `expectedUnlockedNodeId` by name.
  2. `GET /api/Learning/KnowledgeGraph/UnlockedBy/{sourceNodeId}` with bearer token.
- **Expected result:** 200; `successed=true`; `data` non-empty; item with `id == expectedUnlockedNodeId` present.
- **Traces to:** AC-5 ("skills unlocked by mastering X"). (Existing T2.)

### BE-TC-03 — Prerequisites of an unknown nodeId returns 404, never 500
- **Type:** negative
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** Superadmin token. `nodeId = 999999` (guaranteed absent).
- **Steps:**
  1. `GET /api/Learning/KnowledgeGraph/Prerequisites/999999` with bearer token.
- **Expected result:** Status is **404** (handler `NotFound` — `KnowledgeNodeExistsAsync` is false), `successed=false`, message is the localized `KnowledgeNodeNotFound`. **Must NOT be 500.** (Accept 200-empty only if the handler contract changes; current code returns 404 for a missing node.)
- **Traces to:** AC-5 robustness; api-tester handoff "unknown/invalid id → not 500". (Existing T3 — tighten to assert 404 specifically.)

### BE-TC-04 — UnlockedBy of an unknown nodeId returns 404, never 500
- **Type:** negative
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** Superadmin token. `nodeId = 999999`.
- **Steps:**
  1. `GET /api/Learning/KnowledgeGraph/UnlockedBy/999999` with bearer token.
- **Expected result:** 404; `successed=false`; not 500. (Mirror of BE-TC-03 on the second endpoint — the unknown-node branch was only T3-tested on Prerequisites.)
- **Traces to:** AC-5 robustness.

### BE-TC-05 — Anonymous request to Prerequisites returns 401
- **Type:** auth-authz
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** No bearer token.
- **Steps:**
  1. `GET /api/Learning/KnowledgeGraph/Prerequisites/1` with **no** Authorization header.
- **Expected result:** **401 Unauthorized**. If a JSON body is returned by `ErrorHandlerMiddleWare`, its keys are camelCase (e.g. `statusCode`, not `StatusCode`). A 200 here means `[Authorize]` is missing/not enforced — fail.
- **Traces to:** Plan Authz decision; AC-5 access control. (Existing T4.)

### BE-TC-06 — Seed smoke: graph has skill-backed nodes and Prerequisite edges (no cycle at seed time)
- **Type:** persistence / functional
- **Priority:** P1
- **Target agent:** `api-tester`
- **Preconditions / seed:** Seeder ran in `InitializeAsync`.
- **Steps:**
  1. Query DB: `KnowledgeNodes.Any(n => n.SkillId != null)`.
  2. Query DB: `KnowledgeEdges.Any(e => e.RelationshipType == Prerequisite)`.
- **Expected result:** Both true. (a) proves Step-1 node mapping ran; (b) proves Step-2 edge authoring saved — which only happens if `SkillGraphValidator.AssertAcyclic` did NOT throw. 0 edges ⇒ cycle false-positive or unresolved skill names — investigate seeder logs.
- **Traces to:** AC-3 (seedable), AC-4 (acyclic at seed time, indirectly). (Existing T6.)

### BE-TC-07 — Node DTO field contract + envelope spelling on a success response
- **Type:** validation / regression
- **Priority:** P1
- **Target agent:** `api-tester`
- **Preconditions / seed:** Superadmin token; a node with prereqs (e.g. `Add Single-Digit Numbers (G1)`).
- **Steps:**
  1. `GET /api/Learning/KnowledgeGraph/Prerequisites/{id}`.
  2. Inspect the raw JSON body and each `data` item.
- **Expected result:** Raw body contains the literal `"successed":` (camelCase, capital-S spelling preserved). Each item has exactly the DTO keys `id (int)`, `name (string)`, `nodeType (int/enum)`, `subjectId (int)`, `gradeId (int)`, `difficulty (int)`, `skillId (int|null)`. No leaked entity-only fields (no audit fields, no navigation collections). `skillId` key present even when null.
- **Traces to:** AC-1 (SRS §6 node shape surfaced), CONVENTIONS §5 (`Successed`). (Extends T5.)

### BE-TC-08 — Cross-grade prereq edge is queryable (within-subject, across grades)
- **Type:** functional
- **Priority:** P1
- **Target agent:** `api-tester`
- **Preconditions / seed:** Superadmin token. The seeded cross-grade edge `Subtract Within 100 (G2)` → `Multiply Single-Digit Factors (G3)` (and `Multiply Single-Digit Factors (G3)` → `Identify Unit Fractions (G5)`).
- **Steps:**
  1. Resolve node ids for `Multiply Single-Digit Factors (G3)` (target) and `Subtract Within 100 (G2)` (expected prereq).
  2. `GET .../Prerequisites/{multiplyG3Id}`.
- **Expected result:** 200; `data` contains the node id for `Subtract Within 100 (G2)` whose `gradeId` differs from the target's `gradeId` — proving edges span grades within a subject.
- **Traces to:** AC-2 (edges within/across lessons; cross-grade chain).

### BE-TC-09 — Authenticated non-admin (Parent) user can read Prerequisites (not 403)
- **Type:** auth-authz
- **Priority:** P1
- **Target agent:** `api-tester`
- **Preconditions / seed:** A seeded **Parent** user; obtain its bearer token. A known node id.
- **Steps:**
  1. Sign in as Parent → token.
  2. `GET .../Prerequisites/{knownNodeId}` with the Parent token.
- **Expected result:** **200** (NOT 403). Read endpoints are `[Authorize]` only — any authenticated role is permitted. Guards against an accidental `AdminOnly` over-tightening regression. *(If no non-admin user is seeded, report the missing fixture to the lead; do not silently skip.)*
- **Traces to:** Plan Authz decision (authenticated-only, no role restriction).

### BE-TC-10 — Authenticated non-admin (Student) user can read UnlockedBy (not 403)
- **Type:** auth-authz
- **Priority:** P2
- **Target agent:** `api-tester`
- **Preconditions / seed:** A seeded **Student** user token; a known node id.
- **Steps:**
  1. Sign in as Student → token.
  2. `GET .../UnlockedBy/{knownNodeId}` with the Student token.
- **Expected result:** 200 (NOT 403). Confirms the second endpoint is also open to authenticated non-admin roles. *(Skip-with-report if no Student fixture exists.)*
- **Traces to:** Plan Authz decision.

### BE-TC-11 — Multiple prerequisites (fan-in) returned for a node with >1 incoming prereq edge
- **Type:** functional / boundary
- **Priority:** P2
- **Target agent:** `api-tester`
- **Preconditions / seed:** Superadmin token. Identify a node that is the TARGET of more than one `Prerequisite` edge. *(Within the documented Math chain edges are mostly linear; if no fan-in node exists in the seed, mark this case Not Applicable and report — do NOT author extra edges.)*
- **Steps:**
  1. Find a node with ≥2 incoming prereq edges (DB query on `KnowledgeEdges`).
  2. `GET .../Prerequisites/{nodeId}`.
- **Expected result:** 200; `data.Length` equals the count of distinct source nodes for that target; all expected source ids present; no duplicates.
- **Traces to:** AC-5 (correct prereq set), AC-2.

### BE-TC-12 — Node that exists but has no prerequisites returns 200 empty (not 404)
- **Type:** boundary / state
- **Priority:** P1
- **Target agent:** `api-tester`
- **Preconditions / seed:** Superadmin token. A chain-root node that is never a `Target` of a prereq edge — e.g. `Count to 1000 (G1)` (top of the chain; nothing precedes it).
- **Steps:**
  1. Resolve the root node id.
  2. `GET .../Prerequisites/{rootNodeId}`.
- **Expected result:** **200**, `successed=true`, `data = []` (empty array — `EmptyCollection` path: node exists, zero prereqs). Distinguishes "exists-but-empty (200)" from "missing (404, BE-TC-03)".
- **Traces to:** AC-5 (empty result handling); risk note 2 (404↔200 boundary).

### BE-TC-13 — Node that exists but unlocks nothing returns 200 empty (chain leaf)
- **Type:** boundary / state
- **Priority:** P2
- **Target agent:** `api-tester`
- **Preconditions / seed:** Superadmin token. A chain-leaf node that is never a `Source` of a prereq edge — e.g. `Convert Fractions to Decimals (G6)` (bottom of the chain; unlocks nothing further).
- **Steps:**
  1. Resolve the leaf node id.
  2. `GET .../UnlockedBy/{leafNodeId}`.
- **Expected result:** 200, `successed=true`, `data = []`. (Mirror of BE-TC-12 on the UnlockedBy endpoint.)
- **Traces to:** AC-5 (empty handling).

### BE-TC-14 — Non-integer nodeId is a route miss (framework 404), not a handler envelope
- **Type:** validation / negative
- **Priority:** P2
- **Target agent:** `api-tester`
- **Preconditions / seed:** Superadmin token.
- **Steps:**
  1. `GET /api/Learning/KnowledgeGraph/Prerequisites/abc` with bearer token.
- **Expected result:** **404** from the routing layer (the `{nodeId:int}` constraint rejects `abc` → no action match). This is a framework 404, distinct from the handler's `BaseResponse` NotFound envelope — body may be empty/non-JSON. Documented so it is not mistaken for a handler bug. Must NOT be 500.
- **Traces to:** AC-5 robustness; risk note 3.

### BE-TC-15 — Negative / zero nodeId binds the int route and returns handler 404
- **Type:** boundary / negative
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** Superadmin token.
- **Steps:**
  1. `GET .../Prerequisites/0` and `GET .../Prerequisites/-1` with bearer token.
- **Expected result:** Both **404** with the **handler** `NotFound` envelope (`successed=false`) — `0`/`-1` are valid ints, bind the route, hit `KnowledgeNodeExistsAsync` (false) → `NotFound`. Must NOT be 500. Confirms no off-by-one/exists-check bypass for non-positive ids.
- **Traces to:** AC-5 robustness; risk note 2/3.

---

## §B — BLOCKED: authoring surface not built (descoped per plan Q2)

> These cases cover the **add/remove prerequisite-edge authoring** behaviour the QC request asked for (cycle rejection on add, self-loop, duplicate, non-existent skill, remove, admin-vs-non-admin gate). **No such endpoint or command exists** in the shipped build — `KnowledgeGraphController` has only the two `[Authorize]` GET read endpoints; there are no `AddPrerequisite`/`RemovePrerequisite` commands and no `AdminOnly`-gated edge POST/DELETE. The lead descoped authoring to "seam/query API only" (`docs/plans/P2-11.md` decision **Q2**). The acyclic invariant IS covered at the **unit** level by `SkillGraphValidator` + `SkillGraphValidatorTests` (BE-3) — referenced below, not re-specified as API cases.
>
> **`api-tester` action:** record each as **Blocked** in `execution-report.md` with blocker = "authoring endpoint not built (plan Q2); cycle invariant covered by `SkillGraphValidatorTests` unit tests." **Do not fabricate endpoints.** Unblock only if/when an admin authoring surface (P7-03) ships.

### BE-TC-16 — Add prerequisite edge (happy path): edge persists and appears in the graph read
- **Type:** functional / persistence — **Priority:** P0 — **Blocker:** no add-edge endpoint (plan Q2).
- **Intended:** `POST` add prereq A→B (admin) → 200/201; then `GET .../Prerequisites/{B}` includes A; `GET .../UnlockedBy/{A}` includes B.
- **Traces to:** AC-4/AC-5 (authoring + queryable). *Unbuilt.*

### BE-TC-17 — Direct cycle rejected: add A→B then B→A → business-validation error (not 500)
- **Type:** negative / business-validation — **Priority:** P0 — **Blocker:** no add-edge endpoint (plan Q2).
- **Intended:** second add (B→A) rejected with a clear domain error (`FailedDependency`/`BusinessValidation` 424 per `BaseResponseHandler`, or 422) — NOT 500, NOT persisted.
- **Existing unit cover:** `SkillGraphValidatorTests` — single cycle A→B→C→A throws `InvalidOperationException`.
- **Traces to:** AC-4 (cycle rejected with clear error). *API path unbuilt.*

### BE-TC-18 — Transitive cycle rejected: A→B, B→C, then C→A → rejected
- **Type:** negative / business-validation — **Priority:** P0 — **Blocker:** no add-edge endpoint (plan Q2).
- **Intended:** the closing edge C→A is rejected (DFS detects the back edge); graph unchanged.
- **Existing unit cover:** `SkillGraphValidatorTests` covers a 3-node cycle.
- **Traces to:** AC-4. *API path unbuilt.*

### BE-TC-19 — Self-loop rejected: skill as prerequisite of itself (A→A) → rejected
- **Type:** negative / boundary — **Priority:** P0 — **Blocker:** no add-edge endpoint (plan Q2).
- **Intended:** add A→A rejected with a clear error; not persisted.
- **Existing unit cover:** `SkillGraphValidatorTests` — self-loop throws (Gray-node revisit).
- **Traces to:** AC-4. *API path unbuilt.*

### BE-TC-20 — Duplicate edge handling: re-adding an existing A→B prereq
- **Type:** negative / persistence — **Priority:** P1 — **Blocker:** no add-edge endpoint (plan Q2).
- **Intended:** duplicate `(Source, Target, Prerequisite)` rejected (409 Conflict) or idempotent no-op — and the DB unique index `UX_KnowledgeEdges_SourceTarget_Type` prevents a second row.
- **Note:** the index exists today (verifiable by reviewer); only the *endpoint* is missing.
- **Traces to:** AC-1/AC-4 (edge uniqueness). *API path unbuilt.*

### BE-TC-21 — Prerequisite referencing a non-existent skill/node → 404/422 (not 500)
- **Type:** negative / validation — **Priority:** P0 — **Blocker:** no add-edge endpoint (plan Q2).
- **Intended:** add edge whose source or target node id does not exist → 404 NotFound or 422, never 500; nothing persisted.
- **Traces to:** AC-4 (prereq must reference a valid existing skill). *API path unbuilt.*

### BE-TC-22 — Authz on authoring: anonymous→401, non-admin→403, admin→200
- **Type:** auth-authz — **Priority:** P0 — **Blocker:** no add/remove-edge endpoint (plan Q2).
- **Intended:** the (future) authoring endpoint is `AdminOnly` — anonymous→401, Parent/Student→403, Admin/SuperAdmin→200. (The `AdminOnly` policy already gates sibling Subjects/Grades/Lessons/Concepts/Skills authoring, so this is the expected shape when P7-03 lands.)
- **Traces to:** Authoring authz (curriculum-write gating, PR #100/#104 precedent). *API path unbuilt.*
