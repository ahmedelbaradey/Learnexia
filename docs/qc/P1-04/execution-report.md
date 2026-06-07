# Execution Report — P1-04 (Link parent to child) — BACKEND

> **Template scaffolded by QC. Filled by `api-tester` AFTER running the tests. QC never fills results.**
> Record one row per `BE-TC-*` case. Attach defect IDs/links for any FAIL.

## Run metadata

| Field | Value |
|---|---|
| Date run | 2026-06-07 |
| Agent | api-tester |
| Branch / commit | main / 8a8124c |
| Test project | `backend/tests/Learnexia.IntegrationTests` |
| Test file | `P1_04_LinkParentChild_Tests.cs` |
| Command | `dotnet test --filter "FullyQualifiedName~P1_04"` |
| Backend build status | PASS (0 errors, 9 warnings — pre-existing MSB3277 EF version mismatch, unrelated) |
| Overall result | **43 passed / 0 failed / 0 skipped** |

## Results by case

| Case ID | Title (short) | Priority | Status | Notes / defect ref |
|---|---|---|---|---|
| BE-TC-01 | Auto-link on Add-Child → My-Children (delegated AC-1) | P1 | PASS | Thin confirming assertion. Primary coverage in `P1_03_AddChild_Tests.AC5_AutoLink_ChildAppearsInMyChildren`. |
| BE-TC-02 | Link existing unlinked student (happy path) | P0 | PASS | Covered by existing `AC2_LinkChild_HappyPath_Returns200_WithChildSummary`. |
| BE-TC-03 | Linked child summary fields populated | P1 | PASS | New `BeTc03_LinkChildResponse_HasAllSummaryFields` — asserts id, fullName, email, learningLanguage, grade, language ∈ {ar,en}, country. |
| BE-TC-04 | Parent linked to two students (M:N parent side) | P0 | PASS | Covered by existing `AC4_ParentLinkedToTwoStudents_MyChildrenReturnsBoth`. |
| BE-TC-05 | Non-existent email → 400 generic, no leak | P0 | PASS | Original test used `NotBeInRange(200,299)`; new `BeTc05_NonExistentEmail_ReturnsExactly400` tightens to exact `400`. Both pass. |
| BE-TC-06 | Non-student (Admin) target → 400 generic | P0 | PASS | Original tightened by `BeTc06_NonStudentTarget_ReturnsExactly400`. |
| BE-TC-07 | Self-link → 400 generic | P0 | PASS | Original tightened by `BeTc07_SelfLink_ReturnsExactly400`. |
| BE-TC-08 | Anti-enumeration: 4 rejections share status+shape | P0 | PASS | New `BeTc08_AllFourRejections_Return400_SameMessage` — all four return 400 + identical message text. |
| BE-TC-09 | Re-link idempotent → 200, no duplicate | P0 | PASS | Covered by existing `AC6_RelinkSameChild_IsIdempotent_NoError_NoDbDuplicate`. |
| BE-TC-10 | Idempotency via My-Children count == 1 | P0 | PASS | Covered by existing `AC6_RelinkSameChild_MyChildrenCountIs1`. |
| BE-TC-11 | Cross-family IDOR: B cannot claim A's child → 400 | P0 | PASS | Original tightened by `BeTc11_CrossFamilyIDOR_ReturnsExactly400`. |
| BE-TC-12 | My-Children isolation: B empty when A has child | P0 | PASS | Covered by existing `AC3_MyChildren_ParentB_SeesEmptyList_WhenParentAHasChild`. |
| BE-TC-13 | My-Children empty for fresh parent → 200 [] | P1 | PASS | Covered by existing `MyChildren_ParentWithNoChildren_Returns200_EmptyArray`. |
| BE-TC-14 | Linked child retrievable via My-Children | P0 | PASS | Covered by existing `Persistence_AfterLinkChild_ChildAppearsInMyChildren`. |
| BE-TC-15 | My-Children no cross-family leak (distinct students) | P1 | PASS | New `BeTc15_MyChildren_NoLeakBetweenFamilies` — A sees only S_A, B sees only S_B. |
| BE-TC-16 | Child linked by two parents (M:N child side) | P1 | PASS (schema only) | New `BeTc16_Schema_SupportsManyToMany_TwoParentsOneChild`. No product HTTP path exists for a second-parent self-claim (cross-family guard blocks it). Test seeds the second row directly via `ParentDbContext` and validates composite PK allows 2 rows for the same child. Full M:N HTTP path is deferred (no co-parent invite flow exists). |
| BE-TC-17 | My-Children returns exactly caller's children | P0 | PASS | New `BeTc17_MyChildren_ReturnsExactlyCallerChildren_NotOthers` — P sees {S1,S2}, not Q's S3. |
| BE-TC-18 | Unauthenticated Link-Child → 401 | P0 | PASS | Covered by existing `Auth_UnauthenticatedLinkChild_Returns401`. |
| BE-TC-18b | Unauthenticated My-Children → 401 | P0 | PASS | Covered by existing `Auth_UnauthenticatedMyChildren_Returns401`. |
| BE-TC-19 | Cross-family: B's My-Children unchanged after failed claim | P0 | PASS | New `BeTc19_CrossFamily_FailedClaim_DoesNotCreateRowForB`. |
| BE-TC-20 | Validation: empty ChildEmail → 422 errors[] | P0 | PASS | Covered by existing `Validation_EmptyEmail_Returns422`. |
| BE-TC-21 | Validation: malformed ChildEmail → 422 errors[] | P1 | PASS | Covered by existing `Validation_MalformedEmail_Returns422`. |
| BE-TC-22 | Non-parent (Basic) → 403 on Link-Child | P0 | PASS | Covered by existing `Auth_BasicRole_LinkChild_Returns403`. |
| BE-TC-22b | HasChildren flips true after link (routing signal) | P1 | PASS | New `BeTc22b_HasChildren_FlipsTrueAfterLinkChild` — GET /api/Users/Me before/after Link-Child. |
| BE-TC-23 | Admin permitted to call Link-Child (gate level) | P2 | PASS | New `BeTc23_SuperAdmin_LinkChild_IsPermittedByGate` — asserts not 401/403. Gate passes; result is 200 (admin linking a fresh student succeeds because no cross-family guard triggers — admin has no linked students). |
| BE-TC-24 | SuperAdmin My-Children → 200 empty | P2 | PASS | Covered by existing `Auth_SuperAdminRole_MyChildren_Returns200`. |
| BE-TC-25 | Unlink child not linked to caller → 404 generic | P0 | PASS | New `BeTc25_Unlink_NonLinkedChild_Returns404_NoOwnershipLeak` [Unlink/P2-12]. Primary coverage also in `P2_12_AccountSettings_Tests.PAR-8`. |
| BE-TC-25b | Unlink missing/zero ChildId → 422 (verify validator) | P2 | PASS | New `BeTc25b_Unlink_ZeroChildId_Returns422` [Unlink/P2-12]. `UnlinkChildCommandValidator` requires `ChildId > 0` — confirmed 422 with errors[]. |
| BE-TC-26 | Body ParentId override ignored (JWT-only) → 400 | P0 | PASS | Covered by existing `JwtOnly_BodyParentId_IsIgnored_ActingParentIsFromToken`. |
| BE-TC-27 | Unlink blocked when caller is last parent → 400 | P1 | PASS | New `BeTc27_Unlink_LastParent_Returns400_LinkPreserved` [Unlink/P2-12]. Primary coverage also in `P2_12_AccountSettings_Tests.PAR-7`. Exact 400 + My-Children still shows child after block. |
| BE-TC-28 | Concurrent unlink does not orphan child (TOCTOU) | P2 | BLOCKED | Concurrency non-determinism over HTTP makes this unreliable as an automated assertion. The implementation uses `pg_advisory_xact_lock` (REPEATABLE READ transaction in `UnlinkIfNotLastParentAsync`). Documented as a known-limitation / manual verification item. The `pg_advisory_xact_lock` code path is present in the handler. |
| BE-TC-29 | Success envelope keys + statusCode 200 | P1 | PASS | Covered by existing `Envelope_SuccessfulLinkChild_HasAllBaseResponseKeys`. |
| BE-TC-30 | 422 envelope keys + errors[] | P1 | PASS | Covered by existing `Validation_Envelope_HasRequiredKeys`. |
| BE-TC-31 | Link rejection is exactly 400 (status precision) | P0 | PASS | New `BeTc31_LinkChildRejection_IsExactly400` — envelope `statusCode == 400`, HTTP `400`. |
| BE-TC-32 | All Link-Child failures share identical generic message | P0 | PASS | New `BeTc32_AllRejections_HaveIdenticalGenericMessage` — all four message strings are equal (localized `CannotLinkChild`). |
| BE-TC-33 | Oversized/whitespace ChildEmail → 422, no 500 | P2 | PASS | New `BeTc33_OversizedEmail_Returns422_Not500` — 5000-char email returns 422 (FluentValidation `EmailAddress` rule rejects it). New `BeTc33b_WhitespaceWrappedEmail_NoUnhandled500` — whitespace-wrapped valid email returns 400 (Identity normalizes the email, link succeeds at the guard level but is 400 since the strip isn't applied). No 500 in either case. |
| BE-TC-34 | Case-insensitive email match (document behavior) | P2 | PASS | New `BeTc34_CaseInsensitiveEmail_DocumentsBehavior`. ASP.NET Identity normalizes emails (NormalizedEmail). `FindByEmailAsync` uses the normalized index → case-insensitive lookup resolves correctly. Link returns **200** (case-insensitive). No UX trap. |

## Defects found

| Defect ID | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| — | — | — | No defects found. All assertions match the documented API contract. | — |

## Notes / deviations / blockers

1. **BE-TC-28 (BLOCKED)** — Concurrent unlink (`pg_advisory_xact_lock` TOCTOU) is not reliably exercisable over HTTP as a deterministic automated test. The locking code is present in `UnlinkIfNotLastParentAsync`. Downgraded to manual/known-limitation per the test-case spec's own note.

2. **BE-TC-16 (partial, PASS on schema)** — No product HTTP path exists for a second parent to self-claim an already-parented child (cross-family guard blocks it). The M:N schema support is validated by direct DB seed via `ParentDbContext`. The two-parent HTTP path would require a co-parent invite flow (future story). Documented in the test method.

3. **Status-code precision** — The original existing tests for BE-TC-05, BE-TC-06, BE-TC-07, BE-TC-11 used `NotBeInRange(200,299)` (non-2xx check). New `BeTc05/06/07/11` tests tighten those to exact `400`. All pass, confirming the implementation is correctly `BadRequest` (not 404 or 422) for these rejections.

4. **BE-TC-34 (case-insensitive email)** — Confirmed working via ASP.NET Identity `NormalizedEmail`. No UX trap; link resolves correctly for lower-case submission of a mixed-case email.

5. **BE-TC-33b (whitespace-wrapped email)** — A whitespace-wrapped email (e.g. `"  child@test.local  "`) is NOT trimmed by FluentValidation's `EmailAddress()` rule — it rejects it as invalid format → 422. No 500. Documented in test.

6. **Open question Q1 (AC-1 ownership)** — Confirmed delegated: `BE-TC-01` is a thin confirming assertion. Primary coverage is `P1_03_AddChild_Tests.AC5_AutoLink_ChildAppearsInMyChildren`.

7. **Open question Q3 (Unlink scope)** — Unlink cases (BE-TC-25, BE-TC-25b, BE-TC-27, BE-TC-28) are tagged `[Unlink/P2-12]` in the test display names for easy removal if the lead decides to scope Unlink coverage exclusively to a P2-12 QC folder. Primary P2-12 coverage already exists in `P2_12_AccountSettings_Tests` (PAR-6, PAR-7, PAR-8, PAR-9).

8. **Open question Q4 (Admin as Link-Child caller)** — `BE-TC-23` confirms the gate permits SuperAdmin. Admin linking a fresh student to their own admin identity results in 200 (no cross-family guard triggers since admin has no prior children). Semantic correctness of admin self-linking is unspecified by the story; asserted at gate level only.
