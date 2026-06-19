# P7-05 — Content lifecycle — Coverage report

## Summary
- Story: P7-05 Publish, version & preview curriculum content (draft→published→archived state machine + versions + rollback + preview + per-language coverage).
- Backend cases catalogued: **35** (BE-TC-01..35). Covered by existing tests: **27**. **GAP (to implement): 8.**
- Frontend reference cases: 9 (FE lead).
- Existing test source: `P7_05_ContentLifecycle_Tests.cs` (~50 facts).

## Acceptance criteria → test cases → status

| Acceptance criterion (story) | Test case(s) | Status |
|------------------------------|--------------|--------|
| Edits accumulate as Draft, not served to students | BE-TC-30, BE-TC-31, BE-TC-33, BE-TC-34 | Covered + **GAP** (QuizQuestion leak, child-published/ancestor-draft) |
| Publish records versioned snapshot (timestamp + author) | BE-TC-15, BE-TC-16, BE-TC-21 | Covered + **GAP** (author == acting admin) |
| Live vs pending-draft distinguishable | BE-TC-24, BE-TC-27 | Covered (state) + **GAP** (draft-edit-vs-live-content distinction) |
| Preview draft as student without publishing | BE-TC-24, BE-TC-25, BE-TC-26, BE-TC-27 | Covered + **GAP** (preview shows pending edit) |
| Revert/rollback restores previous published version | BE-TC-17, BE-TC-18, BE-TC-19, BE-TC-22, BE-TC-23 | Covered + **GAP** (content actually reverts; per-language scope) |
| Publish/version/preview/rollback per `(SubjectCode,Language)` tree | BE-TC-23, BE-TC-28 | Covered (publish coverage) + **GAP** (rollback scope) |
| Per-language publication-coverage view | BE-TC-28, BE-TC-29 | Covered |
| Admin-only access; non-admin → 403 (esp. Preview) | BE-TC-25, BE-TC-35 | Covered |
| (state machine) legal + illegal transitions enforced | BE-TC-01..10, BE-TC-14 | Covered (Lesson) + **GAP** (Subject/Unit/Question illegal; self-transition; type/id mismatch) |

**No acceptance criterion is fully uncovered.** Every criterion has Covered cases; GAPs harden the state-machine
uniformity across entity types, the QuizQuestion leak guard, and the *semantic* rollback/preview assertions (content
actually reverts / preview shows the pending edit, not just the state label).

## Prioritized GAP list for `api-tester`

**P0:**
1. BE-TC-33 ★ Draft QuizQuestion NOT in student StartAttempt; after Publish IS (question-level leak guard)

**P1:**
2. BE-TC-10 Illegal transition enforced for Subject/Unit/QuizQuestion (not just Lesson)
3. BE-TC-21 ContentVersion `publishedBy` == acting admin
4. BE-TC-22 Rollback actually reverts entity content to the chosen version
5. BE-TC-23 Rollback per-`(SubjectCode,Language)` tree only
6. BE-TC-27 Preview shows pending draft edit while student read shows old published content
7. BE-TC-34 Child Published but ancestor Draft → no partial student leak
8. BE-TC-09 Published→Published re-publish version behavior (deterministic)

**P2:**
9. BE-TC-08 Self-transition (Draft→Draft etc.) → deterministic, no 500
10. BE-TC-14 EntityType/EntityId mismatch → graceful, no wrong-entity flip, no 500

## Risk notes
- **Strongest residual risk = the QuizQuestion leak guard (BE-TC-33).** Subject + Lesson Draft leaks are covered; the
  question-level leak is not, yet questions are independently publishable (AC-TC-7). A Draft question surfacing in a
  student attempt is a direct content-safety leak — P0.
- The existing suite proves transitions **succeed/fail by state label**; it under-tests the **semantic** outcomes
  (rollback content actually reverts, preview shows the pending edit). A handler can pass all current tests while
  rolling back the version pointer but not the editorial fields — BE-TC-22/27 close that.
- State-machine enforcement is proven for **Lesson only**; BE-TC-10 confirms it's uniform across entity types (a
  per-type handler could legalize an illegal transition for Subject without any current test catching it).
- The hierarchy interaction (BE-TC-34: publish a child while its ancestor is Draft) is a plausible admin workflow that
  could leak a lesson under an unpublished subject — worth a deliberate guard test.
