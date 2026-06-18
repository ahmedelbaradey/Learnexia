# Execution Report — P7 Admin Console Batch 1 (P7-06 + P7-07 + P7-08)

> **Template scaffolded by `qc-test-designer` (results EMPTY).** Filled by **`frontend-e2e-tester`** AFTER implementing + running `frontend-test-cases.md`. `qc-test-designer` never fills results.
> One row per `FE-TC-*` case. Status ∈ `PASS` / `FAIL` / `BLOCKED` / `SKIPPED`. For FAIL/BLOCKED, link a defect and a one-line note.

## Run metadata (fill in)

| Field | Value |
|---|---|
| Run date | _yyyy-mm-dd_ |
| Tester (agent) | frontend-e2e-tester |
| Admin app build | _commit / branch_ |
| Admin app URL | http://localhost:3001 |
| Backend URL | http://localhost:5080 |
| Locale(s) run | _en (default) / + ar build?_ |
| Admin auth path used | _seeded creds / role grant / token injection_ (Q-B) |
| Playwright admin project added? | _yes/no_ (handoff §6a) |
| testIDs added by frontend? | _yes/no_ (handoff §6b) — if no, note selector fallback |
| Spec file(s) | _e.g. tests/e2e/specs/P7-admin-batch1.spec.ts_ |

## Result summary (fill in)

| Metric | Count |
|---|---|
| Total cases | 77 |
| PASS | _ |
| FAIL | _ |
| BLOCKED | _ |
| SKIPPED | _ |
| P0 failures | _ |

## Frontend results (`frontend-e2e-tester`)

### Auth & routing
| Case | Title (abbrev.) | Status | Defect / note |
|---|---|---|---|
| FE-TC-01 | Anon → /login | | |
| FE-TC-02 | Non-admin → /login | | |
| FE-TC-03 | Admin reaches /users | | |
| FE-TC-04 | Users nav active (aria-current) | | |
| FE-TC-05 | Single AdminShell per route | | |
| FE-TC-06 | Topbar title per page | | |
| FE-TC-07 | Anon deep-link /edit → /login | | |

### P7-06 Users list
| Case | Title (abbrev.) | Status | Defect / note |
|---|---|---|---|
| FE-TC-08 | Results table + 5 cols + aria-live count | | |
| FE-TC-09 | Loading skeleton (role=status) | | |
| FE-TC-10 | Empty state | | |
| FE-TC-11 | Error + retry | | |
| FE-TC-12 | Debounced search (single request) | | |
| FE-TC-13 | Role filter (Parent/Student only) | | |
| FE-TC-14 | Status filter (Active=0/Suspended=1) | | |
| FE-TC-15 | Filter change resets to page 1 | | |
| FE-TC-16 | Pagination next/prev + bounds | | |
| FE-TC-17 | In-flight refetch keeps rows | | |
| FE-TC-18 | Clear-filters visibility + reset | | |
| FE-TC-19 | Row click + keyboard nav to detail | | |
| FE-TC-20 | Email/date LTR; lean row (no PII) | | |

### P7-06 User detail
| Case | Title (abbrev.) | Status | Defect / note |
|---|---|---|---|
| FE-TC-21 | Header name/email/role+status badges | | |
| FE-TC-22 | Profile fields incl. sign-in not tracked | | |
| FE-TC-23 | TWO distinct language rows | | |
| FE-TC-24 | Parent hides student block | | |
| FE-TC-25 | Family (parent→children, no email, deep-link) | | |
| FE-TC-26 | Family (child→parents, email, deep-link) | | |
| FE-TC-27 | Family empty state | | |
| FE-TC-28 | Activity best-effort + No data | | |
| FE-TC-29 | Sign-in not tracked note | | |
| FE-TC-30 | Panels fail independently | | |
| FE-TC-31 | Detail loading skeleton | | |
| FE-TC-32 | Not-found (404) friendly | | |
| FE-TC-33 | Status reason/changed only when present | | |

### P7-07 Lifecycle actions
| Case | Title (abbrev.) | Status | Defect / note |
|---|---|---|---|
| FE-TC-34 | Active → {Suspend, Delete} | | |
| FE-TC-35 | Suspended → {Reactivate, Delete} | | |
| FE-TC-36 | Deleted terminal (no actions) | | |
| FE-TC-37 | Suspend required-reason gate + governance copy | | |
| FE-TC-38 | Suspend success → refetch Suspended | | |
| FE-TC-39 | Suspend already-suspended (400) inline | | |
| FE-TC-40 | Suspend 422 on reason field | | |
| FE-TC-41 | Reactivate optional reason + prior history | | |
| FE-TC-42 | Reactivate success → refetch Active | | |
| FE-TC-43 | Reactivate already-active/deleted (400) inline | | |
| FE-TC-44 | Delete two-gate (reason + typed email) | | |
| FE-TC-45 | Delete typed-email case-insensitive | | |
| FE-TC-46 | Delete cascade parent-only, default off | | |
| FE-TC-47 | Delete confirm:true only at final step → Deleted | | |
| FE-TC-48 | Delete already-deleted (400) inline | | |
| FE-TC-49 | Delete defensive 424 mapping | | |
| FE-TC-50 | Self/SuperAdmin protection (400) | | |

### P7-08 Child edit
| Case | Title (abbrev.) | Status | Defect / note |
|---|---|---|---|
| FE-TC-51 | Child-only gate (non-student blocked) | | |
| FE-TC-52 | Edit renders 3 distinct sections | | |
| FE-TC-53 | Save disabled until change; changed-fields-only PATCH | | |
| FE-TC-54 | Profile PATCH success refetch [copy bug flag] | | |
| FE-TC-55 | Profile PATCH 422 inline [mis-map flag] | | |
| FE-TC-56 | Learning-language opens dialog (no inline save) | | |
| FE-TC-57 | Grade dialog 1–6 + required reason + gate | | |
| FE-TC-58 | Grade same-grade warning + disabled | | |
| FE-TC-59 | Grade success refetch | | |
| FE-TC-60 | Grade 422 range inline | | |
| FE-TC-61 | Lang dialog destructive copy + CONFIRM (case-sensitive) | | |
| FE-TC-62 | Lang confirmFreshStart:true only at final step → refetch | | |
| FE-TC-63 | Lang defensive 424 mapping | | |
| FE-TC-64 | Lang 422 unsupported inline | | |
| FE-TC-65 | Cancel/ESC closes + clears form; no mutation | | |
| FE-TC-66 | Backdrop does NOT dismiss | | |
| FE-TC-67 | Dialog focus trap + return focus | | |
| FE-TC-68 | role=dialog + aria-modal + aria-labelledby | | |

### Cross-cutting
| Case | Title (abbrev.) | Status | Defect / note |
|---|---|---|---|
| FE-TC-69 | EN default — no raw keys | | |
| FE-TC-70 | RTL/ar build — dir/lang + Arabic copy | | |
| FE-TC-71 | RTL — technical strings stay LTR | | |
| FE-TC-72 | RTL — dialogs (reversed row, mirrored arrows) | | |
| FE-TC-73 | a11y — table/aria-live/active-nav/skeleton | | |
| FE-TC-74 | a11y — role=alert + aria-disabled gate | | |
| FE-TC-75 | No PII in console/toasts/URL | | |
| FE-TC-76 | No optimistic mutation | | |
| FE-TC-77 | Sign-out clears auth + no persisted PII | | |

## Backend results (pre-existing — not re-run unless lead requests)

> No `backend-test-cases.md` authored for this batch (BE coverage pre-existing). If the lead requested a re-verify of the `coverage-report.md` §4 contract-smoke set, record it here; otherwise leave as N/A.

| Check | Status | Note |
|---|---|---|
| BE-SMOKE-01..13 | N/A (pre-existing) | _only if re-verified_ |

## Defects found (fill in)

| # | Severity | Case(s) | Summary | File / location |
|---|---|---|---|---|
| | | | | |

## Notes / blockers encountered (fill in)
- _e.g. RTL cases BLOCKED — no `ar` build available (Q-A); selector fragility where testIDs were not added (handoff §6b); admin auth path used (Q-B); etc._
