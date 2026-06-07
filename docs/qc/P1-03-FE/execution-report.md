# P1-03-FE — Execution report (filled by `frontend-e2e-tester`)

> Scaffolded empty by QC. **`frontend-e2e-tester` fills this after running** the Playwright specs derived from `frontend-test-cases.md`. QC does NOT fill results. Results feed the `reviewer` gate.

## Run metadata
- Date / time (UTC):
- Branch / commit:
- Expo web base URL: `http://localhost:8081`
- Backend base URL: `http://localhost:5080`
- Browser projects run: chromium / mobile (Pixel 7)
- Spec file(s): `tests/e2e/specs/P1-03-FE.spec.ts`
- Seed accounts used (parent w/ no children, etc.):

## Result summary
| Metric | Count |
|---|---|
| Total cases | 21 |
| Passed | |
| Failed | |
| Blocked | |
| Skipped | |

## Per-case results
| Case ID | Title | Priority | Result (Pass/Fail/Blocked/Skipped) | Notes / defect ref |
|---|---|---|---|---|
| FE-TC-01 | Parent adds one child → appears in My Children | P0 | | |
| FE-TC-02 | Add multiple children in one pass | P0 | | |
| FE-TC-03 | Remove a draft before submit | P1 | | |
| FE-TC-04 | Edit a draft before submit (in-memory) | P1 | | |
| FE-TC-05 | Required-field validation blocks add | P0 | | |
| FE-TC-06 | Learning-language required despite app-language default | P0 | | |
| FE-TC-07 | Duplicate login email → specific i18n msg, no account | P0 | | |
| FE-TC-08 | Generic BaseResponse error fallback as i18n text | P1 | | |
| FE-TC-09 | Learning language auto-fills app language (untouched) | P0 | | |
| FE-TC-10 | Manual app-language edit stops auto-fill | P1 | | |
| FE-TC-11 | Two language fields fenced + labelled distinctly | P2 | | |
| FE-TC-12 | Arabic default → RTL + Arabic copy | P0 | | |
| FE-TC-13 | English locale → LTR + English copy | P1 | | |
| FE-TC-14 | Locale switch preserves draft list | P2 | | |
| FE-TC-15 | My Children empty state | P1 | | (see OQ-1 — may be BLOCKED) |
| FE-TC-16 | My Children loading skeletons → loaded | P1 | | |
| FE-TC-17 | My Children error state + retry | P1 | | |
| FE-TC-18 | Added child persists in My Children after reload | P0 | | |
| FE-TC-19 | No student self-register / self-onboard | P0 | | |
| FE-TC-20 | Grade selector bounded 1–6 | P1 | | |
| FE-TC-21 | Only ar/en languages; no teacher role | P2 | | |

## Defects filed (back to `frontend`)
| Defect ID | Case ID(s) | Severity | Summary | Status |
|---|---|---|---|---|
| | | | | |

## Missing testIDs requested from `frontend` (per README OQ-2)
| Surface / control | Suggested `testID` | Blocking which case(s) |
|---|---|---|
| | | |

## Blocked / not-yet-testable cases
| Case ID | Reason (cite OQ) |
|---|---|
| | |

## Notes for `reviewer`
-
