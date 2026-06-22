# P8-03 — Serve curriculum in the student's learning language · Backend test cases

> Story: [user-stories/Phase-8-Localization/P8-03-serve-curriculum-in-learning-language.md](../../../user-stories/Phase-8-Localization/P8-03-serve-curriculum-in-learning-language.md)
> Task: [tasks/Backend/Phase-8-Localization/P8-03-BE.md](../../../tasks/Backend/Phase-8-Localization/P8-03-BE.md)
> Module: **Learning** · Implemented by **api-tester** · Results → `execution-report.md`

## Surface under test (mapped from the running code)
| Endpoint | Handler | Language behavior (verified in code) |
|---|---|---|
| `GET api/learning/Subjects/ForGrade?grade={n}` `[Authorize]` | `GetSubjectsForGradeQueryHandler` | Loops the 4 SubjectCodes; resolves each via `SubjectLanguageResolver.Resolve(code, learnerLang)`; picks the matching-`Language` tree; **falls back to the other language + logs warn** if absent. Returns one Subject per code. |
| `GET api/learning/Subjects/{id}/Lessons` `[Authorize]` | `GetSubjectLessonsQueryHandler` | If requested Subject's `Language != resolved`, **silently redirects** to the correct-language tree (same Grade+SubjectCode); if that tree is absent, **falls back** to the requested tree + logs warn. |
| `GET api/learning/Subjects/{id}/SkillTree` `[Authorize]` | `GetSubjectSkillTreeQueryHandler` | Same silent-redirect-then-fallback as Lessons. |
| `GET api/learning/Lessons/{id}` `[Authorize]` | `GetLessonQueryHandler` | **Different**: if owning Subject's `Language != resolved` → **403 Forbidden** (`LessonLanguageMismatch`). No redirect. (Direct cross-language lesson access is blocked, not silently served.) |
| `GET` Dashboard (`DashboardController`) | `GetDashboardQueryHandler` | Resolves the Grade-1 fallback / primary subject set per SubjectCode via `SubjectLanguageResolver` using the JWT claim. |

- **Resolver (`SubjectLanguageResolver.Resolve`)**: `ARABIC→Ar`, `ENGLISH→En`, `MATH→learnerLang`, `SCIENCE→learnerLang`.
- **Claim accessor (`LearningLanguageClaimAccessor.GetLearningLanguage`)**: reads `learning_language`; **absent/unrecognised → defaults to `Ar`** + logs a warn (never throws).
- Language is taken from the **JWT claim only** — there is no query param to override it.

## Critical behavioral asymmetry to assert (non-obvious)
- **List endpoints (Subjects/Lessons, Subjects/SkillTree)** → wrong-language `SubjectId` is **silently redirected** to the right tree (200).
- **Single-lesson endpoint (Lessons/{id})** → wrong-language `LessonId` is **403 Forbidden**, not redirected.
This asymmetry is intentional in code and must each be asserted; do not assume uniform behavior.

## Seed matrix the story mandates (the core assertion)
| Subject | Arabic-medium student sees | English-medium student sees |
|---|---|---|
| MATH | Ar tree | En tree |
| SCIENCE | Ar tree | En tree |
| ARABIC | Ar tree (pinned) | Ar tree (pinned) — identical |
| ENGLISH | En tree (pinned) | En tree (pinned) — identical |

## Preconditions (all cases)
`InitializeAsync`: `ApplyMigrationsAndSeedAsync()` + `LearningSeeder.SeedAsync(scope)`. Create two children in the **same grade (1)** via `Add-Child`: one `LearningLanguage="ar"` (sign in → `arToken`), one `LearningLanguage="en"` (sign in → `enToken`). Reuse `P8_04` helpers.

To assert "which tree", map the returned Subject id back to `Subject.Language`/`Subject.SubjectCode` via `LearningDbContext` (most robust), or assert by the language-specific display name.

---

## Test cases

### BE-TC-01 — `ForGrade` returns 4 subjects, one per SubjectCode, at the resolved language (English-medium)
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Steps:** `GET api/learning/Subjects/ForGrade?grade=1` with `enToken`. For each returned subject, resolve `(SubjectCode, Language)` from DB.
- **Expected:** 200; exactly 4 subjects; resolved set = `MATH/En, SCIENCE/En, ARABIC/Ar, ENGLISH/En`. (Math/Science follow the learner = En; Arabic pinned Ar; English pinned En.)
- **Traces to:** AC matrix "English-medium student: Math(en), Science(en), Arabic(ar), English(en)"; AC "curriculum read queries return the tree whose Subject.Language matches the resolved language".

### BE-TC-02 — `ForGrade` returns the resolved language (Arabic-medium)
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Steps:** `GET api/learning/Subjects/ForGrade?grade=1` with `arToken`; resolve each subject from DB.
- **Expected:** 200; 4 subjects; resolved set = `MATH/Ar, SCIENCE/Ar, ARABIC/Ar, ENGLISH/En`.
- **Traces to:** AC matrix "Arabic-medium student: Math(ar), Science(ar), Arabic(ar), English(en)".

### BE-TC-03 — same-grade Ar vs En students: Math/Science DIFFER, Arabic/English IDENTICAL
- **Type:** functional / regression · **Priority:** P0 · **Target:** api-tester
- **Steps:** Call `ForGrade?grade=1` with both `arToken` and `enToken`; compare returned subject ids by SubjectCode.
- **Expected:**
  - MATH subject id (ar) ≠ MATH subject id (en).
  - SCIENCE subject id (ar) ≠ SCIENCE subject id (en).
  - ARABIC subject id (ar) == ARABIC subject id (en) (same row — pinned).
  - ENGLISH subject id (ar) == ENGLISH subject id (en) (same row — pinned).
  - Both see all four subject codes.
- **Traces to:** AC "An Arabic-medium and an English-medium student in the same grade both see all four subjects; only Math/Science differ; Arabic/English are identical for both".

### BE-TC-04 — `Lessons` for the En-MATH subject id serves the En tree to an En student
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Steps:** From BE-TC-01 get the En student's MATH subject id; `GET api/learning/Subjects/{mathEnId}/Lessons` with `enToken`.
- **Expected:** 200; units/lessons are the English MATH tree (English display names; lessons belong to a Subject with `Language==En`).
- **Traces to:** AC "lessons-in-unit return the resolved-language tree".

### BE-TC-05 — `SkillTree` for the En-MATH subject id serves the En tree to an En student
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Steps:** `GET api/learning/Subjects/{mathEnId}/SkillTree` with `enToken`.
- **Expected:** 200; concepts/skills are the English MATH tree.
- **Traces to:** AC "skill tree returns the resolved-language tree".

### BE-TC-06 — wrong-language SubjectId on `Lessons` SILENTLY REDIRECTS to the resolved tree
- **Type:** functional / security · **Priority:** P0 · **Target:** api-tester
- **Rationale:** an En student passing the **Arabic** MATH subject id must not get Arabic content; the handler redirects to MATH/En.
- **Steps:** Resolve the **MATH/Ar** subject id for grade 1 (from DB). `GET api/learning/Subjects/{mathArId}/Lessons` with `enToken` (En student).
- **Expected:** 200; the served units/lessons are the **English** MATH tree (redirected) — NOT the Arabic content the id pointed at. Confirm by mapping a returned lesson back to a Subject with `Language==En`.
- **Traces to:** AC "return the tree whose Subject.Language matches the resolved language"; prevents wrong-language access via direct id.

### BE-TC-07 — wrong-language SubjectId on `SkillTree` SILENTLY REDIRECTS
- **Type:** functional / security · **Priority:** P0 · **Target:** api-tester
- **Steps:** `GET api/learning/Subjects/{mathArId}/SkillTree` with `enToken`.
- **Expected:** 200; concepts/skills resolve to the En MATH tree (redirected), not the Ar id passed.
- **Traces to:** AC "skill tree returns resolved-language tree".

### BE-TC-08 — wrong-language LessonId on `Lessons/{id}` → 403 Forbidden (asymmetry)
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Rationale:** unlike the list endpoints, the single-lesson endpoint blocks cross-language access (`LessonLanguageMismatch`).
- **Steps:** Resolve a **MATH/Ar** lesson id (e.g. the Arabic counterpart of "Introduction to Counting"). `GET api/learning/Lessons/{mathArLessonId}` with `enToken` (En student, resolves MATH→En).
- **Expected:** **403 Forbidden**; `successed=false`; message = LessonLanguageMismatch (localized). The wrong-language lesson is NOT served and NOT redirected.
- **Traces to:** AC "lesson … returns the tree whose Subject.Language matches the resolved language" (enforced here as a hard block). Documents the deliberate list-vs-single asymmetry.

### BE-TC-09 — correct-language LessonId on `Lessons/{id}` → 200
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Steps:** En student requests the **MATH/En** lesson id ("Introduction to Counting (G1)"); `GET api/learning/Lessons/{mathEnLessonId}` with `enToken`.
- **Expected:** 200; lesson served (the language matches the resolved En).
- **Traces to:** AC "lesson returns the resolved-language tree" (positive path).

### BE-TC-10 — ARABIC subject is identical for both media (pinned Ar)
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Steps:** En student requests the ARABIC subject's Lessons; Ar student requests the same; compare the served subject (resolve to DB).
- **Expected:** both resolve to `ARABIC/Ar` (same Subject id) — the En student's ARABIC content is in Arabic (pinned), identical to the Ar student's. Math/Science differ but Arabic does not.
- **Traces to:** AC "Arabic/English subjects are identical for both"; "ARABIC → ar".

### BE-TC-11 — ENGLISH subject is identical for both media (pinned En)
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Steps:** Both students request the ENGLISH subject's Lessons; compare.
- **Expected:** both resolve to `ENGLISH/En` (same Subject id) — Ar student's ENGLISH content is in English (pinned).
- **Traces to:** AC "ENGLISH → en"; "identical for both".

### BE-TC-12 — switching learning language flips the served curriculum (full round-trip)
- **Type:** functional / state · **Priority:** P0 · **Target:** api-tester
- **Rationale:** AC "switching language flips served content." Combines P8-04 change + P8-03 serve.
- **Steps:**
  1. Child starts `LearningLanguage="en"`; sign in; `ForGrade?grade=1` → MATH resolves to En tree (record id).
  2. Parent: `PUT api/Parent/Change-Learning-Language` { childId, newLearningLanguage="ar", confirmFreshStart=true } → 200.
  3. Child **re-signs-in** (new JWT carries `learning_language="ar"`); `ForGrade?grade=1` → MATH resolves to **Ar** tree.
- **Expected:** the MATH (and SCIENCE) subject served flips En→Ar after the change + re-sign-in; ARABIC/ENGLISH unchanged.
- **Traces to:** AC "switching language flips served content"; AC "learning language comes from the JWT claim".

### BE-TC-13 — content language comes from the JWT claim, NOT a query parameter
- **Type:** security / negative · **Priority:** P0 · **Target:** api-tester
- **Rationale:** AC "the student's learning language comes from the JWT claim, not a query parameter."
- **Steps:** En student calls `ForGrade?grade=1&learningLanguage=ar` (and/or `&lang=ar`, `&language=ar`) — i.e. attempt to spoof Arabic via query.
- **Expected:** 200; MATH/SCIENCE still resolve to **En** (the claim wins; the bogus query param is ignored). No way to override the medium via the request.
- **Traces to:** AC "comes from the JWT claim, not a query parameter".

### BE-TC-14 — missing/absent `learning_language` claim falls back to Arabic (legacy-token fallback)
- **Type:** boundary / fallback · **Priority:** P1 · **Target:** api-tester
- **Rationale:** `LearningLanguageClaimAccessor` defaults to `Ar` + logs a warn when the claim is absent/unrecognised.
- **Steps:** Obtain a token without the `learning_language` claim. Practical options, in order of preference:
  - (a) Use a **non-student** authenticated account (parent/admin) that has a grade-resolvable path, if reachable; OR
  - (b) document this as covered by a **Domain unit test** of `LearningLanguageClaimAccessor` (absent claim → `Ar`) since minting a claimless student token at the integration layer is awkward.
- **Expected:** with no claim, MATH/SCIENCE resolve to the **Arabic** tree (product Arabic-first default); a warning is logged (not asserted at integration layer).
- **Traces to:** AC "Missing-tree fallback … should not occur once seeded" (claim-absence variant). 
- **Blocker note:** if neither (a) nor (b) is feasible at the HTTP layer, mark **blocked-by-harness** and assert the fallback via the pure accessor unit test; record the path used.

### BE-TC-15 — missing-tree fallback: resolved tree absent → serve the other language + warn (no error)
- **Type:** fallback / resilience · **Priority:** P1 · **Target:** api-tester
- **Rationale:** AC "Missing-tree fallback: serve the other language tree and log a warning (should not occur once seeded)."
- **Steps:** In a scope, **deactivate or remove** the MATH/En subject for grade 1 (e.g. set `IsActive=false` / `LifecycleState!=Published`, since the read path filters on active+published). En student calls `ForGrade?grade=1`.
- **Expected:** 200; MATH is still returned but served from the **Ar** tree (fallback); response is not 404 and not a 500. (Restore state after the test.)
- **Traces to:** AC "Missing-tree fallback: serve the other language tree and log a warning".
- **Note:** prefer toggling `IsActive`/lifecycle over hard-delete to avoid FK churn; both exercise the same `FirstOrDefault` fallback branch. Keep this test isolated (own parent/child + restore) so it does not poison shared seed for sibling tests.

### BE-TC-16 — empty-state friendliness: unknown but in-range scenario returns 200 + empty, not 404/500
- **Type:** state (empty) / boundary · **Priority:** P2 · **Target:** api-tester
- **Rationale:** AC "empty-state friendliness." `ForGrade` returns `EmptyCollection` (200 + empty list) when no subjects resolve; an unknown subject id on Lessons/SkillTree returns 404 (existing P2 behavior).
- **Steps:**
  1. `GET api/learning/Subjects/{id}/Lessons` for a subject with no published units (if such a seeded state exists) → 200 + empty collection.
  2. `GET api/learning/Subjects/ForGrade?grade=99` (out of range) → 400 (not 500). `grade=` missing → 400.
- **Expected:** empty/edge inputs yield friendly 200-empty or 400 — never an unhandled 500; envelope `successed` consistent with the status.
- **Traces to:** AC "empty-state friendliness"; supporting NFR robustness.

### BE-TC-17 — Dashboard resolves subjects in the student's learning language
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Rationale:** task BE-5 — dashboard aggregation shows correct-language subjects (`GetDashboardQueryHandler` uses `SubjectLanguageResolver` + the JWT claim).
- **Steps:** Call the dashboard endpoint (resolve route from `DashboardController`) with `enToken` and `arToken`; inspect the surfaced primary/continue subject.
- **Expected:** En student's dashboard surfaces the En MATH tree; Ar student's surfaces the Ar MATH tree (the resolved primary subject differs by medium); ARABIC/ENGLISH pinned.
- **Traces to:** AC "dashboard returns resolved-language content"; task BE-5.
- **Blocker note:** if the dashboard requires prior activity to surface a subject, seed a minimal attempt first (reuse `StartAttemptAsync`). If the dashboard contract does not expose the subject id/language observably, assert via the included subject name's language and note the limitation.

### BE-TC-18 — `quiz / start-attempt` respects the learning language (no wrong-language attempt)
- **Type:** functional / security · **Priority:** P1 · **Target:** api-tester
- **Rationale:** task BE-4 lists quiz/start-attempt among the language-filtered reads.
- **Steps:** En student attempts `POST api/Learning/Quizzes/{mathArLessonId}/Attempt` (an **Arabic** MATH lesson — wrong language for an En student).
- **Expected:** the attempt does not start against wrong-language content. Determine the actual enforced behavior (the single-lesson read returns 403; start-attempt may mirror that or 404). Assert whatever the implementation enforces and flag if start-attempt is **not** language-guarded (a gap). For the correct-language lesson (MATH/En) the attempt succeeds (200).
- **Traces to:** task BE-4 "quiz/start-attempt"; AC "every subject's content shown in the correct language".
- **Open question (lead):** confirm whether `StartAttempt` enforces the language guard (the story lists "quiz" among guarded reads, but the handler set verified covers Subjects/Lessons/SkillTree/Lesson/Dashboard — StartAttempt guard not confirmed in this pass). See coverage-report gap G-03.

---

## Notes for the implementer
- To assert "which language tree" robustly, map returned Subject/Lesson ids back to `Subject.SubjectCode`/`Subject.Language` via `LearningDbContext` rather than relying on display-name string matching alone.
- Reuse `P8_04` helpers (`CreateParentAndChildAsync(tag, learningLanguage)`, `SignInAndGetTokenAsync`, `SendAsync`, `TryProp`, `ChangeLearningLanguageAsync`) — they already create children at a chosen learning language and resolve the seeded lesson/subject ids you need.
- The two students must be in the **same grade** for BE-TC-03's "same grade, all four subjects" comparison to be valid.
- BE-TC-15 (and BE-TC-18 negative) mutate seed state — isolate them (own child accounts, restore toggled rows) so they don't break sibling tests sharing the `IntegrationTests` collection.
