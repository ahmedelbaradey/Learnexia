# P7-09 Moderation Queue — Backend Test Cases (api-tester)

> Surface: `ModerationController` @ `api/Admin/Moderation` (AdminOnly). Ingest seam: `AiOutputFlaggedIntegrationEvent` (Shared.Contracts) → `AiOutputFlaggedEventHandler` → idempotent `ModerationItem` writer. Review: `POST {id}/Review` (`ICommand` → ValidationBehavior).
>
> Existing suite: `backend/tests/Learnexia.IntegrationTests/P7_09_Moderation_Tests.cs` — read for context before implementing. Every case below is marked **Covered** (cite the existing `[Fact]` method) or **GAP** (new test to add).
>
> Enum wire facts (no global `JsonStringEnumConverter`): `ModerationStatus` is serialized/deserialized **by int** — `Pending=0, Approved=1, Rejected=2, Flagged=3`. `ModerationSource`: `AiOutput`, `CurriculumUpload`. Queue/detail DTOs serialize Status/Source **as strings** ("Pending"/"AiOutput"). Review request body: `{ Decision:int, Reason:string? }`.
> Query filter param names (from `GetModerationQueueQuery`): `Status`, `Source`, `SubjectCode`, `Grade`, `DateFrom`, `DateTo`, `Search`, `PageNumber`, `PageSize`.

## Auth / authz matrix

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-09-01 | Queue anonymous → 401 | auth | P0 | none | GET `Queue` no bearer | 401 | **Covered** — `Auth1_Queue_Anonymous_Returns401` |
| BE-TC-09-02 | Queue parent → 403 | auth | P0 | parent JWT | GET `Queue` parent | 403 | **Covered** — `Auth2_Queue_Parent_Returns403` |
| BE-TC-09-03 | Queue basicuser → 403 | auth | P0 | basicuser JWT | GET `Queue` basicuser | 403 | **Covered** — `Auth3_Queue_BasicUser_Returns403` |
| BE-TC-09-04 | Queue admin → 200 | auth | P0 | admin JWT | GET `Queue` admin | 200 | **Covered** — `Auth4_Queue_Admin_Returns200` |
| BE-TC-09-05 | Detail anonymous → 401 | auth | P0 | none | GET `{id}` no bearer | 401 | **Covered** — `Auth5_Detail_Anonymous_Returns401` |
| BE-TC-09-06 | Detail parent → 403 | auth | P0 | parent JWT | GET `{id}` parent | 403 | **Covered** — `Auth6_Detail_Parent_Returns403` |
| BE-TC-09-07 | Review anonymous → 401 | auth | P0 | none | POST `{id}/Review` no bearer | 401 | **Covered** — `Auth7_Review_Anonymous_Returns401` |
| BE-TC-09-08 | Review parent → 403 | auth | P0 | parent JWT | POST `{id}/Review` parent | 403 | **Covered** — `Auth8_Review_Parent_Returns403` |
| BE-TC-09-09 | Detail basicuser → 403 | auth | P1 | basicuser JWT | GET `{id}` basicuser | 403 | **GAP** — only parent is tested for detail; add basicuser (non-admin role ≠ unregistered parent) |
| BE-TC-09-10 | Review basicuser → 403 | auth | P1 | basicuser JWT | POST `{id}/Review` basicuser | 403 | **GAP** — basicuser not asserted on Review route |

## Ingest / idempotency

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-09-11 | Ai-flag event → Pending/AiOutput item | functional | P0 | publish `AiOutputFlaggedIntegrationEvent` | publish event, poll Queue | item present, `status="Pending"`, `source="AiOutput"` | **Covered** — `Ingest1_DispatchEvent_ItemAppearsInQueue` |
| BE-TC-09-12 | Duplicate SourceEventId → 1 row | persistence/idempotency | P0 | same SourceEventId published twice | publish ×2, count rows | exactly 1 row (unique index) | **Covered** — `Idem1_DuplicateSourceEventId_OnlyOneRow` |
| BE-TC-09-13 | SafetyVerdict.failedChecks is real JSON array (not double-escaped) | persistence | P1 | publish event w/ `["ToxicityCheck"]` | publish, read item, parse verdict | `failedChecks` is JSON array, no `\"` | **Covered** — `Verdict1_SafetyVerdict_FailedChecks_IsRealArray` |
| BE-TC-09-14 | Redelivery after item already **reviewed** → no resurrection | persistence/idempotency | P1 | item enqueued, then Approved | publish the **same** SourceEventId again, re-read item | item count still 1; its status stays `Approved` (redelivery must not reset to Pending nor create a dup) | **GAP** — idempotency is proven for un-reviewed dup only; redelivery after a terminal review is the riskier ordering case and is untested |
| BE-TC-09-15 | Event with empty/whitespace ContentReference | boundary | P2 | publish event, `ContentReference=""` | publish, poll Queue | handler does not 500; item enqueued (or fail-soft skip — assert no exception leaks, queue still returns 200) | **GAP** — malformed event payload (empty content ref) untested; verifies fail-soft handler |
| BE-TC-09-16 | Event with malformed FailedChecks (non-JSON literal) | boundary | P2 | publish event, `FailedChecks="not-json"` | publish, read item verdict | handler stores literal safely (ParseJsonOrLiteral fallback), no double-escape corruption, no 500 | **GAP** — only the well-formed-array path is tested; the literal-fallback branch of `ParseJsonOrLiteral` is unverified |

## Detail

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-09-17 | Detail of seeded item → 200 + full fields | functional | P0 | seeded item | GET `{id}` | 200; `successed=true`; fields id/sourceEventId/contentReference/safetyVerdict/source/status/detectedAt/reviewedByUserId/reviewedAtUtc | **Covered** — `Detail1_GetItem_Returns200WithDetailFields` |
| BE-TC-09-18 | Detail unknown id → 404 (not 500) | negative | P0 | none | GET `999999999` | 404; `successed=false` | **Covered** — `Detail2_UnknownId_Returns404` |
| BE-TC-09-19 | Detail id=0 / negative id | boundary | P2 | none | GET `0` and `-1` | not 500 (404 or route-miss 400/404) | **GAP** — zero/negative route id not asserted |

## Review transitions + validation

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-09-20 | Approve Pending → 200, status=Approved | functional | P0 | Pending item | POST Review `{Decision:1}` | 200; data.status="Approved" | **Covered** — `Review1_ApprovePendingItem_FlipsToApproved` |
| BE-TC-09-21 | Reject without reason → 422 | validation | P0 | Pending item | POST Review `{Decision:2, Reason:null}` | 422 | **Covered** — `Review2_RejectWithoutReason_Returns422` |
| BE-TC-09-22 | Re-review terminal (Approved) item → 400 | negative | P0 | item Approved | POST Review `{Decision:2, Reason:"x"}` | 400; `successed=false` | **Covered** — `Review3_ReReviewTerminalItem_Returns400` |
| BE-TC-09-23 | Approved item drops from Pending filter | state | P1 | item Approved | GET `Queue?Status=Pending` | reviewed item not present | **Covered** — `Review4_AfterApprove_ItemDropsFromPendingFilter` |
| BE-TC-09-24 | **Reject with reason → 200, status=Rejected, reviewer/time recorded** | functional | P0 | Pending item | POST Review `{Decision:2, Reason:"unsafe"}` | 200; status="Rejected"; (via DB read) `ReviewedByUserId`=admin id, `ReviewedAtUtc` set, `ReviewReason` persisted | **GAP** — the **happy Reject path** is never asserted; only the 422-no-reason and the terminal-400 are. AC-4 reject success is uncovered |
| BE-TC-09-25 | **Flag Pending → 200, status=Flagged** | functional | P0 | Pending item | POST Review `{Decision:3}` | 200; status="Flagged" | **GAP** — the **Flag decision is entirely untested**; AC-2/AC-4 name Flag as a first-class action |
| BE-TC-09-26 | **Flagged → Approve is allowed (Flagged not terminal)** | state | P1 | item Flagged (via 09-25) | POST Review `{Decision:1}` on the Flagged item | 200; status="Approved" (enum doc: Flagged→Approved/Rejected allowed) | **GAP** — the Flagged→terminal transition path documented on `ModerationStatus` is untested |
| BE-TC-09-27 | **Flagged → Reject (reason) is allowed** | state | P1 | item Flagged | POST Review `{Decision:2, Reason:"x"}` | 200; status="Rejected" | **GAP** — second leg of the Flagged transition, untested |
| BE-TC-09-28 | Review unknown id → 404 (not 500) | negative | P1 | none | POST `999999999/Review {Decision:1}` | 404 (or BaseResponse failure, not 500); `successed=false` | **GAP** — review-on-missing-item only exercised indirectly via 401/403; a 404-on-missing for an **authenticated admin** is not asserted |
| BE-TC-09-29 | Review with invalid Decision enum (Decision=99) | validation/boundary | P1 | Pending item | POST Review `{Decision:99}` | 422 or 400 (invalid enum); not 500 | **GAP** — out-of-range decision enum untested |
| BE-TC-09-30 | Approve with a reason supplied → 200 (reason optional for non-reject) | validation | P2 | Pending item | POST Review `{Decision:1, Reason:"note"}` | 200; reason accepted/ignored, no validation error | **GAP** — confirms reason is only *required* for Reject, allowed otherwise |
| BE-TC-09-31 | Reject reason over max length (>2000) → 422 | boundary | P2 | Pending item | POST Review `{Decision:2, Reason: 2001-char}` | 422 (maxlen rule) | **GAP** — the documented ~2000 char cap is unverified |
| BE-TC-09-32 | **Reviewer identity comes from JWT, not body (no IDOR)** | auth-authz | P1 | Pending item | POST Review as admin; inspect DB `ReviewedByUserId` | equals the acting admin's JWT sub (body cannot inject a different reviewer — there is no reviewer field in the body) | **GAP** — the brief's explicit IDOR guard (actor from JWT) is asserted nowhere |

## Audit emission (cross-module relay)

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-09-33 | **Approve emits AdminActionPerformedEvent → audit row** | functional/persistence | P0 | Pending item | POST Review Approve; poll `api/Admin/Audit/Log?actionType=...&adminUserId=...` | one audit row appears w/ correct action (e.g. `Moderation.ItemApproved` / confirm exact string from handler), `targetEntityType` for ModerationItem, `adminUserId`=actor | **GAP** — P7-09 AC-5 (review audits via relay) has **no test**; this is the key cross-module assertion mirroring P7-12/P7-13 audit E2E |
| BE-TC-09-34 | Reject emits audit row (before→after status in Details, PII-safe) | persistence | P1 | Pending item | POST Review Reject; read audit row Details | Details carries before→after status + ids only; **no free-text reason**, no prompt/response text (PII-light invariant) | **GAP** — uncovered; also guards the brief's "do not persist free-text reason into immutable Details" rule |
| BE-TC-09-35 | Flag emits audit row | persistence | P2 | Pending item | POST Review Flag; poll audit log | audit row with the Flag action string | **GAP** — uncovered (Flag path overall is untested) |

## Queue envelope / pagination / filters

| ID | Title | Type | Pri | Precondition | Steps | Expected | Covered / GAP |
|---|---|---|---|---|---|---|---|
| BE-TC-09-36 | Empty queue → 200, empty paged result | state | P0 | far-future date filter | GET `Queue?DateFrom=2099-01-01&DateTo=2099-12-31` | 200; `successed=true`; totalCount=0; paging keys present | **Covered** — `Empty1_EmptyQueue_Returns200WithEmptyPage` |
| BE-TC-09-37 | Envelope has BaseResponse keys | functional | P1 | admin | GET `Queue` | statusCode/successed/message/data/errors present | **Covered** — `Filter1_QueueEnvelope_HasBaseResponseKeys` |
| BE-TC-09-38 | Data has pagination keys | functional | P1 | admin | GET `Queue` | currentPage/totalCount/totalPages/pageSize present | **Covered** — `Filter2_QueueData_HasPaginationKeys` |
| BE-TC-09-39 | PageSize=9999 clamped ≤100 | boundary | P1 | seeded item | GET `Queue?PageSize=9999` | pageSize ≤ 100 | **Covered** — `Filter3_PageSize9999_IsClamped` |
| BE-TC-09-40 | Status=Approved filter returns only Approved | functional | P1 | Approved item present | GET `Queue?Status=Approved` | all rows status="Approved" | **Covered** — `Filter4_StatusFilter_ReturnsOnlyMatchingRows` |
| BE-TC-09-41 | Source=AiOutput filter returns only AiOutput | functional | P1 | AiOutput item present | GET `Queue?Source=AiOutput` | all rows source="AiOutput" | **Covered** — `Filter5_SourceFilter_ReturnsOnlyMatchingRows` |
| BE-TC-09-42 | **SubjectCode filter returns only matching subject** | functional | P1 | publish event w/ subjectCode="Math" | GET `Queue?SubjectCode=Math` | all rows subjectCode="Math" | **GAP** — AC-4 subject facet filter is untested (only Status + Source are) |
| BE-TC-09-43 | **Grade filter returns only matching grade** | functional | P1 | publish event w/ grade=5 | GET `Queue?Grade=5` | all rows grade=5 | **GAP** — AC-4 grade facet filter untested |
| BE-TC-09-44 | **Search by ContentReference (partial match)** | functional | P1 | publish event w/ unique contentRef | GET `Queue?Search={fragment}` | matching item returned; non-matching excluded | **GAP** — AC-2 "searchable by content reference" untested |
| BE-TC-09-45 | **DateFrom/DateTo windows DetectedAt** | functional | P1 | publish two items at different DetectedAt | GET `Queue?DateFrom=&DateTo=` narrow window | only in-window items returned | **GAP** — date-range filter only used to *force empty*; positive windowing not asserted |
| BE-TC-09-46 | Default order newest-first (by DetectedAt) | functional | P2 | two items, distinct DetectedAt | GET `Queue` | data ordered DetectedAt desc | **GAP** — AC-1 newest-first ordering unverified |
| BE-TC-09-47 | Combined filters (Status + Source + Grade) AND-compose | functional | P2 | mixed items | GET `Queue?Status=Pending&Source=AiOutput&Grade=5` | rows satisfy all three | **GAP** — multi-filter composition untested |
| BE-TC-09-48 | No mutation routes beyond Review (PUT/DELETE on item → 404/405) | negative | P2 | admin | PUT/DELETE `api/Admin/Moderation/{id}` | 404 or 405 | **GAP** — AC-7 "no update/delete beyond review" is asserted for Audit (P7-12) but not for Moderation |
