# P7-09 Moderation Queue — Coverage Report

**Story:** `user-stories/Phase-7-Admin-Console/P7-09-content-moderation-queue.md`
**Brief:** `docs/briefs/P7-09-moderation-queue.md`
**Controller:** `backend/src/Modules/Moderation/.../Controllers/ModerationController.cs` (`api/Admin/Moderation`, AdminOnly)
**Existing suite:** `backend/tests/Learnexia.IntegrationTests/P7_09_Moderation_Tests.cs`

## Counts

| Bucket | Total | Covered | GAP |
|---|---|---|---|
| Backend | 48 | 22 | **26** |
| Frontend (reference) | 16 | n/a | n/a |

## Acceptance-criteria → coverage matrix

| AC (story) | Backend case IDs | Verdict |
|---|---|---|
| AC-1 Paginated queue w/ source/submitter/ref/status, newest-first | 09-04, 09-17, 09-36..39; **09-46 (ordering GAP)** | Partial — ordering uncovered |
| AC-2 Open item + signal, then Approve/Reject(reason)/Flag; searchable | 09-17, 09-20, 09-21, 09-24, **09-25 Flag GAP**, **09-44 search GAP** | Partial — Flag + search uncovered |
| AC-3 Review transitions status, records admin/time/reason, emits audit event | 09-20, 09-22, 09-23; **09-24/09-32 record GAP**, **09-33..35 audit-emit GAP** | **Gap — audit emission + reviewer recording untested** |
| AC-4 Filter by status/source/subject/grade/date + search | 09-40, 09-41; **09-42 subject**, **09-43 grade**, **09-45 date**, **09-44 search** (all GAP) | **Gap — only status + source filters covered** |
| AC-5 Admin-only; non-admin → 403 | 09-01..08; **09-09/09-10 basicuser GAP** | Covered (minor role-matrix gap) |
| AC-6 Items via Shared.Contracts (no FK); record from FullAuditedEntity | 09-11, 09-12, 09-13 | Covered |
| (Brief AC-8) Non-existent / terminal item → BaseResponse failure, not 500; idempotency defined | 09-18, 09-22; **09-14 redelivery-after-review**, **09-28 review-404 GAP** | Partial |

## Risk notes (where the gaps cluster)

1. **Audit emission is completely untested (highest risk).** AC-3/AC-5 require every review to emit `AdminActionPerformedEvent` so P7-12 writes an audit row. The Moderation suite asserts the review *response* but never that the cross-module relay fires. The prior P7-backend execution report already found a real defect where some producers fail to raise the domain event (curriculum **create** wasn't audited) — Moderation is a *new producer* of this event and is exactly the kind of path that historically broke. **Cases 09-33/09-34/09-35 are P0/P1 and must run.**
2. **The Flag decision path is entirely unimplemented in tests.** Approve and Reject-without-reason are tested; **Flag (Decision=3) and the Flagged→terminal transitions documented on `ModerationStatus` have zero coverage** (09-25/09-26/09-27). Flag is a named first-class action in the AC.
3. **The happy Reject path is missing.** Only the 422 (no reason) and the 400 (terminal) are tested — the actual successful reject + reviewer/timestamp/reason persistence (09-24, 09-32) is uncovered, leaving AC-3's "records reviewing admin + timestamp + reason" unverified.
4. **Three of five queue filter facets are untested** — subject, grade, date-range positive windowing, and content-ref search (09-42..45). The AC explicitly lists all of these as DB-side filters.
5. **Idempotency on redelivery-after-review** (09-14) is the subtle ordering case: the existing idempotency test only proves "two un-reviewed publishes → one row." A redelivery arriving *after* an admin already approved must not resurrect/reset the item.

## Prioritized backend GAP list for api-tester

**P0 (must run before sign-off):**
- 09-24 Reject-with-reason happy path (status=Rejected + reviewer/time/reason persisted)
- 09-25 Flag Pending → Flagged
- 09-33 Approve emits audit row (cross-module relay)

**P1:**
- 09-26, 09-27 Flagged→Approve / Flagged→Reject transitions
- 09-28 Review unknown id (authenticated admin) → 404/failure not 500
- 09-29 Invalid Decision enum (99) → 422/400 not 500
- 09-32 Reviewer identity from JWT (IDOR guard)
- 09-34 Reject emits audit row, Details PII-safe (no free-text reason in immutable log)
- 09-42, 09-43, 09-44, 09-45 subject / grade / search / date-range filters
- 09-09, 09-10 basicuser → 403 on detail + review
- 09-14 redelivery-after-review idempotency

**P2:**
- 09-15, 09-16 malformed event payloads (empty content ref / non-JSON FailedChecks) fail-soft
- 09-19 zero/negative route id
- 09-30 Approve with reason allowed; 09-31 reason > maxlen → 422
- 09-35 Flag emits audit row
- 09-46 newest-first ordering; 09-47 combined filters; 09-48 no PUT/DELETE mutation routes

## Open questions / assumptions for the lead

- **Exact audit action strings** for Moderation review (e.g. `Moderation.ItemApproved/ItemRejected/ItemFlagged`, and the `targetEntityType` value) must be read from the review command handler / `AdminActionPerformedDomainEvent` raise site before 09-33..35 can assert literals. If the handler does **not** yet raise the domain event, 09-33 will fail and surfaces a real AC-5 defect (matching the historical curriculum-create-audit gap).
- Confirm `ReviewModerationItemCommandValidator` enforces a max length on `Reason` (assumed ~2000 per brief) — drives 09-31.
- Confirm whether an out-of-range `Decision` enum is rejected by FluentValidation (`IsInEnum`) → 422, vs model-binding → 400. Drives the expected status in 09-29.
