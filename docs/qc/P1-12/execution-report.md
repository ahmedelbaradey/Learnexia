# P1-12 — Backend execution report (filled by `api-tester`)

> **Template scaffolded by the QC architect — results are filled in by `api-tester` AFTER running the suite.**
> One row per BE-TC-* case from `backend-test-cases.md`. Status: PASS / FAIL / BLOCKED / SKIPPED.
> For FAIL, file a defect (ID + summary) in §3 and link it. For BLOCKED, restate the blocker (README §4 Q1–Q5 / infra).

## 0. Run metadata (fill on execution)
- **Date run:** _TBD_
- **Branch / commit:** _TBD_
- **API base URL:** _TBD_ (e.g. http://localhost:5080)
- **Backend up?** _TBD_ · **Postgres up?** _TBD_ · **MinIO up (avatar)?** _TBD_ · **GoogleAuth__ClientId set?** _TBD_ · **Email sink for reset token?** _TBD_
- **Tester:** `api-tester`

## 1. Summary (fill on execution)
- **Total:** 58 · **Passed:** _ · **Failed:** _ · **Blocked:** _ · **Skipped:** _
- **P0 result:** _ / 31 passed · **Verdict:** _PASS / FAIL / PARTIAL_

## 2. Per-case results

| Case ID | Title | Priority | Status | Notes / defect link |
|---|---|---|---|---|
| BE-TC-01 | GET own profile returns enriched shape | P0 | | |
| BE-TC-02 | GET profile without JWT → 401 | P0 | | |
| BE-TC-03 | PUT profile updates + persists | P0 | | |
| BE-TC-04 | PUT profile without JWT → 401 | P0 | | |
| BE-TC-05 | GET /Me without JWT → 401 | P0 | | |
| BE-TC-06 | /Me reflects profile fields after update | P0 | | |
| BE-TC-07 | /Me role/grade shape for a parent | P1 | | |
| BE-TC-08 | PUT profile empty fullName → 422 | P0 | | |
| BE-TC-09 | PUT profile malformed phone / oversize → 422 | P1 | | |
| BE-TC-10 | Upload valid PNG → 200 + presigned URL | P0 | | BLOCKED-infra if MinIO down |
| BE-TC-11 | Uploaded avatar in profile + /Me | P0 | | BLOCKED-infra if MinIO down |
| BE-TC-12 | Valid JPEG and WEBP accepted | P1 | | BLOCKED-infra if MinIO down |
| BE-TC-13 | Storage failure no raw error leak | P1 | | BLOCKED-infra (fault injection) |
| BE-TC-14 | Reject SVG → 422 | P0 | | |
| BE-TC-15 | Spoofed content-type non-image → 422 | P0 | | |
| BE-TC-16 | Valid bytes, disallowed MIME → 422 | P1 | | |
| BE-TC-17 | GIF/BMP/TIFF → 422 | P2 | | |
| BE-TC-18 | Oversize (>2 MB) → 422 | P0 | | |
| BE-TC-19 | Empty / missing file → 422 | P1 | | |
| BE-TC-20 | Upload without JWT → 401 | P0 | | |
| BE-TC-21 | One user can't affect another's avatar | P0 | | BLOCKED-infra if MinIO down |
| BE-TC-22 | Remove avatar clears AvatarUrl | P0 | | BLOCKED-infra if MinIO down |
| BE-TC-23 | Remove avatar without JWT → 401 | P1 | | |
| BE-TC-24 | Valid Google token creates parent + JWT | P0 | | BLOCKED (Q1) |
| BE-TC-25 | Valid Google token links existing email | P1 | | BLOCKED (Q1) |
| BE-TC-26 | Google account can't password-login | P1 | | BLOCKED (Q1) |
| BE-TC-27 | Garbage idToken → 401 fail-closed | P0 | | |
| BE-TC-28 | Wrong-audience idToken → 401 | P0 | | partial BLOCKED |
| BE-TC-29 | Empty / absent idToken → 422 | P0 | | |
| BE-TC-30 | Empty ClientId fails every token closed | P2 | | BLOCKED (config override) |
| BE-TC-31 | Google sign-in can't inject role | P1 | | BLOCKED (Q1) |
| BE-TC-32 | Forgot-Password known email → generic 200 | P0 | | |
| BE-TC-33 | Forgot-Password response has no token | P0 | | |
| BE-TC-34 | Forgot-Password unknown → identical 200 | P0 | | |
| BE-TC-35 | Forgot-Password timing parity | P2 | | observational |
| BE-TC-36 | Reset unknown email → generic failure | P0 | | |
| BE-TC-37 | Reset known email + bad token → same failure | P0 | | |
| BE-TC-38 | Reset weak password → same generic failure | P1 | | partial BLOCKED (Q2) |
| BE-TC-39 | Reset success sets usable password | P0 | | BLOCKED (Q2) |
| BE-TC-40 | Reset token single-use | P1 | | BLOCKED (Q2) |
| BE-TC-41 | Reset invalidates other sessions | P0 | | BLOCKED (Q2) |
| BE-TC-42 | Malformed email forgot/reset → 422 | P1 | | |
| BE-TC-43 | Reset token absent from client surfaces | P1 | | partial (log dim out of scope) |
| BE-TC-44 | Parent edits own child → 200 + updated | P0 | | |
| BE-TC-45 | Edit-child without JWT → 401 | P0 | | |
| BE-TC-47 | Edit-child invalid grade → 422 | P0 | | |
| BE-TC-48 | Edit-child invalid language → 422 | P1 | | |
| BE-TC-49 | Edit-child empty/missing fields → 422 | P1 | | |
| BE-TC-50 | Cross-family edit → 403, no write | P0 | | |
| BE-TC-51 | Edit non-existent child → identical 403 | P0 | | |
| BE-TC-52 | Register stores country, reflected in /Me | P0 | | |
| BE-TC-53 | Register country omitted → 200 | P1 | | |
| BE-TC-54 | Register oversize country → 422 | P2 | | |
| BE-TC-55 | Register acceptedTerms=false → 422 | P0 | | |
| BE-TC-56 | Register acceptedTerms absent → 422 | P0 | | |
| BE-TC-57 | Register can't inject role | P0 | | |
| BE-TC-58 | Register duplicate email → 422 | P1 | | |

> Note: BE-TC-46 intentionally not issued (IDs are stable/non-reused; the edit-child block runs 44,45,47–51).

## 3. Defects found (fill on execution)

| Defect ID | Case(s) | Severity | Summary | Owner | Status |
|---|---|---|---|---|---|
| _none yet_ | | | | | |

## 4. Blocked-case ledger (fill on execution)

| Case(s) | Blocker | Resolution needed |
|---|---|---|
| BE-TC-24/25/26/31 | No test idToken / fake Google validator (README Q1) | Lead: provide fake `IGoogleTokenValidator` or seeded token + matching ClientId |
| BE-TC-38(partial)/39/40/41 | No reset-token capture (README Q2) | Lead: test email sink or internal test-only token mint |
| BE-TC-10/11/12/13/21/22 | MinIO not provisioned in CI (README Q3) | Confirm docker-compose `minio` up in api-tester env |
| BE-TC-28/30 | Wrong-aud token / config override per-test | Token signed for other aud, or test host config override |

## 5. Sign-off (fill on execution)
- **api-tester verdict:** _PASS / FAIL / PARTIAL_
- **Hand to reviewer:** _yes / no_ — feeds the reviewer gate per CLAUDE.md pipeline.
