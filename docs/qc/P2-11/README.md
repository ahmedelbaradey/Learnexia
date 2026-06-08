# QC Test Plan & Coverage Report — P2-11 (Author the skill dependency graph)

**Scope:** Backend API surface only. No frontend (`P2-11` has no UI surface).
**Run owner:** qc-test-designer (Opus design pass).
**Date:** 2026-06-08
**Branch context:** `qc/phase-1-frontend` (design artefacts only; no code changes).

---

## 1. Summary

P2-11 adds a relational, hand-authored, acyclic **skill dependency graph** to the **Learning** module (`KnowledgeNode` + `KnowledgeEdge`, schema `learning`). The story's status is **Done** (audited 2026-06-07). This QC pass designs a traceable backend test catalog over the **actually shipped HTTP surface** and explicitly tracks the requested-but-not-shipped authoring surface as blocked.

### What is actually shipped (the real surface under test)

| Endpoint | Method | Auth | Handler behaviour |
|---|---|---|---|
| `/api/Learning/KnowledgeGraph/Prerequisites/{nodeId:int}` | GET | `[Authorize]` (any authenticated user) | node missing → 404 `NotFound`; node found, 0 prereqs → 200 `EmptyCollection`; found + prereqs → 200 `Success` |
| `/api/Learning/KnowledgeGraph/UnlockedBy/{nodeId:int}` | GET | `[Authorize]` (any authenticated user) | same NotFound / Empty / Success shape (mirror direction) |

Files: `KnowledgeGraphController.cs`, `GetPrerequisitesQueryHandler.cs`, `GetUnlockedByQueryHandler.cs`, `KnowledgeNodeDto.cs`, `LearningRepository.GetPrerequisiteNodesAsync/GetUnlockedByNodeAsync/KnowledgeNodeExistsAsync`.

### CRITICAL scope correction (read before implementing)

The QC request asks for **admin-gated add/remove prerequisite-edge authoring** cases (cycle rejection on add, self-loop on add, duplicate-edge handling, prereq referencing non-existent skill, remove-edge, anonymous→401 / non-admin→403 / admin→200). **That authoring surface does not exist in the codebase.** It was deliberately **descoped** by the lead decision recorded in `docs/plans/P2-11.md` (decision **Q2**): *"BE-6 scope: seam/query API only. Do NOT build speculative wiring... The two query endpoints ARE the integration seam."* There are **no** `AddPrerequisite` / `RemovePrerequisite` commands, no POST/DELETE edge endpoints, and the `KnowledgeGraphController` carries only the two `[Authorize]` GET endpoints above (no `AdminOnly` policy on it).

Consequently:
- **Cycle detection / self-loop / duplicate-edge / non-existent-skill-prereq / remove-edge / admin-vs-non-admin authoring** are **NOT testable via the API** in the shipped build. They are captured in `backend-test-cases.md` under **§B (BLOCKED — authoring surface not built)** with the blocker noted, so they are traceable and not silently dropped. If/when P7-03 (admin authoring) ships, unblock them.
- **Cycle detection IS already covered at the unit level** by `SkillGraphValidator` + `SkillGraphValidatorTests` (BE-3) — acyclic, single cycle, self-loop, Related-edge exclusion. The validator is a domain service called by the seeder at startup; it has **no HTTP reach**. This QC catalog references those unit tests as the existing coverage for AC-4 rather than re-specifying them as API cases.

### Counts

| | P0 | P1 | P2 | Total |
|---|---|---|---|---|
| Backend — testable now (§A) | 7 | 5 | 3 | **15** |
| Backend — blocked / authoring not built (§B) | 6 | 1 | 0 | **7** |
| **Total** | **13** | **6** | **3** | **22** |

All 22 cases target `api-tester`. 0 frontend cases (no `frontend-test-cases.md` produced, per backend-only scope).

There is an existing integration file `backend/tests/Learnexia.IntegrationTests/P2_11_KnowledgeGraph_Tests.cs` (6 tests, T1–T6). The §A catalog **supersedes and extends** it: BE-TC-01..06 map 1:1 to T1–T6 (already implemented — `api-tester` verifies/keeps them); BE-TC-07..15 are the new gaps this Opus pass found.

---

## 2. Coverage matrix (acceptance criterion → case IDs)

Acceptance criteria from `docs/briefs/P2-11.md §"Acceptance criteria (testable)"`. Only **AC-4** and **AC-5** have an HTTP surface; the others are entity/seed/docs concerns verified by unit tests, seed-smoke checks, or reviewer inspection (noted, not gaps).

| AC | Statement (abridged) | Backend API case(s) | Other coverage | Verdict |
|---|---|---|---|---|
| AC-1 | `KnowledgeNode` + `KnowledgeEdge` entities, SRS §6 shape | — | DTO field assertions BE-TC-07; migration/reviewer inspection | Covered (indirect via DTO shape) |
| AC-2 | Edges within/across lessons (and across grades) | BE-TC-08 (cross-grade chain), BE-TC-11 (fan-in) | seed data | Covered |
| AC-3 | Hand-authored / seedable, no OCR | BE-TC-06 (seed smoke) | reviewer (no Azure DI dependency) | Covered |
| AC-4 | Graph validated acyclic; cycle rejected with clear error; unit-tested | **No API surface** (authoring descoped) → BE-TC-16..22 BLOCKED | **`SkillGraphValidatorTests` (BE-3) — existing unit cover: acyclic / single cycle / self-loop / Related-excluded** | Covered at unit level; API authoring blocked — see §B |
| AC-5 | Queryable "prerequisites of X" / "unlocked by X" | BE-TC-01, 02, 03, 04, 05, 07, 08, 09, 10, 11, 12, 13, 14, 15 | — | **Fully covered** |
| AC-6 | P2-04/P3-08/P3-10 read from graph (seam only, per Q2) | — | seam = the two endpoints; consumed by `LearningPathEngine` (P2-04) per task file | Out of P2-11 API scope (deferred) |
| AC-7 | Deferral recorded in `user-stories/README.md` | — | reviewer / docs | Out of API scope |

**Coverage verdict:** Every acceptance criterion with an HTTP surface (**AC-5**) is fully covered. **AC-4's** cycle-rejection requirement is covered at the **unit** level (existing `SkillGraphValidatorTests`) but has **no API surface** because edge authoring is descoped — this is a known, lead-approved gap, not a defect. No uncovered AC that is testable via the shipped API.

---

## 3. Risk notes (where cases are weighted, and why)

1. **Highest weight — cycle detection has no API surface.** The QC ask centres on cycle rejection via an add-edge endpoint, but that endpoint does not exist. The real risk is a *false sense of coverage*: a tester could "confirm cycle handling" against an endpoint that isn't there. Mitigation: §B documents every authoring case as BLOCKED with the exact blocker, and the matrix points AC-4 at the existing unit tests. **`api-tester` must NOT invent authoring endpoints** — if asked to cover cycles end-to-end, escalate to the lead (needs P7-03).
2. **`KnowledgeNodeExistsAsync` gate vs. EmptyCollection.** The two handlers branch on node existence first (→404) then on empty result (→200 empty). The riskiest confusion is the difference between *"node id doesn't exist"* (404, `successed=false`) and *"node exists but has no prereqs / unlocks nothing"* (200, `successed=true`, empty array). Cases BE-TC-03, 04, 12, 13 pin both branches on both endpoints so the 404↔200 boundary can't silently drift.
3. **Route constraint `{nodeId:int}`.** A non-integer segment (`/Prerequisites/abc`) won't bind the action → framework 404 (route miss), *not* the handler's `NotFound` envelope. BE-TC-14 documents the expected framework behaviour so it isn't mistaken for a handler bug. Negative ids (`-1`, `0`) DO bind the int route and flow to the handler → 404 NotFound; BE-TC-15 pins that.
4. **Authz is `[Authorize]`, not `AdminOnly`.** Read endpoints are open to any authenticated user (the lead's Authz decision in the plan). A regression that accidentally tightened these to AdminOnly, or loosened them to anonymous, both matter. BE-TC-05 (anonymous→401) is P0; BE-TC-09/10 assert a **non-admin authenticated** user (Parent/Student) still gets 200 (i.e. NOT 403) — guarding against an over-tightening regression.
5. **Envelope spelling + dual serializer.** `Successed` (intentional misspelling) must serialize as `"successed"`. The harness already copes with the Newtonsoft-camelCase (controller) vs System.Text.Json (middleware/401) split; BE-TC-05 and BE-TC-07 keep the spelling and the camelCase-on-error contracts pinned.
6. **Related-edge exclusion at query level.** The seeder only authors `Prerequisite` edges, but the repository query explicitly filters `RelationshipType == Prerequisite`. If a future `Related` edge is ever seeded, prereq/unlocked-by must still ignore it. BE-TC-13 is a P2 forward-guard (data-dependent; mark not-applicable if no `Related` edge exists in the seed).

---

## 4. Open questions / assumptions (lead decisions needed before/with implementation)

1. **(Decision needed) Cycle-detection via API.** The QC request explicitly asks for end-to-end cycle/self-loop/duplicate-edge rejection tests. These require an **add-prerequisite-edge endpoint that does not exist** (descoped in plan Q2). **Confirm:** keep them BLOCKED in §B and rely on the existing `SkillGraphValidator` unit tests for AC-4, **or** treat "wire up an admin authoring endpoint" as new scope (a separate story, likely P7-03)? Default assumption: **keep blocked; do not build endpoints in a QC pass.**
2. **(Assumption) Authz target role.** Plan Authz decision = read endpoints are **authenticated-only, no role restriction**. The QC ask says "admin→200, non-admin→403" — that applies to the *authoring* surface (which isn't built). For the shipped **read** endpoints the correct expectation is **any authenticated role → 200** (BE-TC-09/10). Confirm the read endpoints should remain non-admin-gated (assumed yes).
3. **(Assumption) Seed fixtures are stable lookup keys.** Cases resolve node ids by exact seeded skill names (e.g. `"Add Single-Digit Numbers (G1)"`, `"Compare and Order Numbers (G1)"`, `"Convert Fractions to Decimals (G6)"`). These come straight from `LearningSeeder.SeedSkillGraphAsync` candidate arrays and must not be renamed. `api-tester` resolves ids from the DB by name at runtime (as the existing harness does) rather than hard-coding ids.
4. **(Assumption) Test environment seeding.** The Learning seeder runs only in Development, but the integration harness calls `LearningSeeder.SeedAsync` directly in `InitializeAsync`. All §A cases assume that seeding path. If the harness changes, the seed-dependent cases need the same direct-seed step.
5. **(Note) `Difficulty` is a flat int (default 3) on seeded nodes**, not the `DifficultyLevel` enum. DTO assertions check the key is present and an int — not a specific value.

---

## 5. Handoff

- **`backend-test-cases.md` → `api-tester`.** §A (BE-TC-01..15) are implementable now against the running API + real PostgreSQL (Testcontainers harness, `[Collection("IntegrationTests")]`). BE-TC-01..06 already exist as T1–T6 in `P2_11_KnowledgeGraph_Tests.cs` — verify/keep them and rename/annotate to the BE-TC ids; BE-TC-07..15 are new. §B (BE-TC-16..22) are **blocked** — `api-tester` records them as Blocked in `execution-report.md` with the "authoring surface not built (plan Q2)" reason and does **not** fabricate endpoints.
- **No `frontend-test-cases.md`** — backend-only story, no UI surface.
- **`execution-report.md`** — scaffolded empty by this pass (one row per BE-TC id, Status = Not Run / Blocked). `api-tester` fills the result columns after running; it never edits the case definitions.

---

Test cases ready — `api-tester` to implement `backend-test-cases.md` (§A now, §B blocked) and write results into `execution-report.md`. No frontend surface, so no `frontend-test-cases.md` / `frontend-e2e-tester` stage for this story.
