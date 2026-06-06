# Execution Report — P1-13a (Notifications email delivery + preferences)

> **TEMPLATE — to be filled by `api-tester` AFTER running the tests.** The QC architect leaves this
> empty. Record pass/fail per case + defects. Do NOT edit `backend-test-cases.md` or `README.md`.
>
> - Source cases: `docs/qc/P1-13a/backend-test-cases.md` (25 cases, BE-TC-01..25).
> - Status legend: **PASS** / **FAIL** / **BLOCKED** (record the blocker) / **SKIPPED** (record why).
> - For any **FAIL** or **BLOCKED**, file a defect row in §3 and reference its ID.

## 1. Run metadata

| Field | Value |
|-------|-------|
| Run date / time (UTC) | _<fill>_ |
| Tester (agent) | api-tester |
| Branch / commit | _<fill>_ |
| Backend host + port | _<fill, e.g. http://localhost:5080>_ |
| DB (Postgres) | _<fill>_ |
| Email provider in test env | _<fill — expected `None` / LogEmailSender>_ |
| `IUserLookup` registered? | _<fill — affects BE-TC-21/22; see README Q5>_ |
| Failing-sender mechanism available? | _<fill — affects BE-TC-17/18/22; see README Q4>_ |
| Admin seed/token available? | _<fill — affects BE-TC-14..18, 20, 21..23, 25; see README Q6>_ |

## 2. Results per case

| ID | Title (short) | Priority | Status | Notes / observed |
|----|---------------|----------|--------|------------------|
| BE-TC-01 | GET first-read returns 4 defaults | P0 | _<PASS/FAIL/BLOCKED>_ | |
| BE-TC-02 | GET default read persists nothing | P1 | | |
| BE-TC-03 | GET never 404 for no-rows user | P1 | | |
| BE-TC-04 | GET unauthenticated → 401 | P0 | | |
| BE-TC-05 | PUT happy path all 4 → 200 | P0 | | |
| BE-TC-06 | PUT empty list → 422 | P0 | | |
| BE-TC-07 | PUT undefined category → 422 | P0 | | |
| BE-TC-08 | PUT duplicate categories → 422 | P0 | | |
| BE-TC-09 | PUT partial (1 category) → 200 (record-of-behaviour) | P1 | | |
| BE-TC-10 | PUT values round-trip on GET | P0 | | |
| BE-TC-11 | Upsert update-in-place, partial leaves others | P1 | | |
| BE-TC-12 | Prefs self-scoped, B never sees A | P0 | | |
| BE-TC-13 | PUT unauthenticated → 401 | P0 | | |
| BE-TC-14 | Send with email → 202 | P0 | | |
| BE-TC-15 | Send null email → skipped, 202 | P1 | | |
| BE-TC-16 | Send success bare 202, not enveloped | P1 | | |
| BE-TC-17 | Send failure → 400 generic Error | P1 | | |
| BE-TC-18 | Failure leaks no provider internals | P1 | | |
| BE-TC-19 | Config ships no secret + dev log sink | P2 | | |
| BE-TC-20 | Malformed send body NOT auto-validated | P2 | | |
| BE-TC-21 | Registration writes welcome row + succeeds | P0 | | |
| BE-TC-22 | Welcome email failure isolated | P0 | | |
| BE-TC-23 | Welcome row idempotent on redelivery | P1 | | |
| BE-TC-24 | Prefs return BaseResponse `Successed` envelope | P2 | | |
| BE-TC-25 | Anonymous / non-admin send → 401/403 | P0 | | |

### Summary tally
| Status | Count |
|--------|-------|
| PASS | _<n>_ |
| FAIL | _<n>_ |
| BLOCKED | _<n>_ |
| SKIPPED | _<n>_ |
| **Total** | **25** |

## 3. Defects filed

| Defect ID | Case ref | Severity | Summary | Repro / expected vs actual | Owner |
|-----------|----------|----------|---------|----------------------------|-------|
| _<DEF-01>_ | _<BE-TC-..>_ | _<Crit/High/Med/Low>_ | _<one line>_ | _<expected … / actual …>_ | frontend / backend-feature |

> **Pre-flagged findings to confirm or refute (from README §5):**
> - README **Q1** — PUT validator does not require all 4 categories (BE-TC-09). File a defect ONLY if the
>   lead confirms product intent is "all 4 required."
> - README **Q2** — `SendNotificationCommand` validator never fires (BE-TC-20). File a defect ONLY if the
>   lead confirms `POST /api/notifications` should 422 on a malformed body.

## 4. Verdict

- **Overall:** _<PASS / FAIL / PASS-WITH-DEFECTS / BLOCKED>_
- **Coverage actually executed:** _<n>_ / 25 (blocked: _<list IDs + blocker>_).
- **Acceptance criteria status:** AC1 _<>_, AC2 _<>_, AC3 _<>_, AC4 _<>_, AC5 _<>_ (per the README §2
  matrix).
- **Handoff:** results feed the `reviewer` gate; defects routed to `backend-feature` (or `frontend` if a
  contract change is needed). Blocked cases must be resolved (test-infra) or accepted by the lead before
  the gate closes.
