# Phase 8 — Localization · Backend coverage report

> Scope: **backend only** (this lead owns backend; frontend E2E is the FE lead's). Stories P8-01, P8-02, P8-03 (new design) + audit of P8-04 (already covered).
> Author: qc-test-designer (design only — no executable test code, nothing run).
> Per-story case docs: [`P8-01/backend-test-cases.md`](../P8-01/backend-test-cases.md) · [`P8-02/backend-test-cases.md`](../P8-02/backend-test-cases.md) · [`P8-03/backend-test-cases.md`](../P8-03/backend-test-cases.md)
> Implementation owner: **api-tester** → results into each story folder's `execution-report.md`.

## 1. Summary & counts

| Story | New BE cases | P0 | P1 | P2 | Coverage verdict |
|---|---|---|---|---|---|
| P8-01 set child learning language | 15 (BE-TC-01..15) | 7 | 4 | 4 | All ACs covered (1 sub-clause = FE/flag — G-01) |
| P8-02 bilingual curriculum (seed/schema) | 10 (BE-TC-01..10) | 4 | 4 | 2 | All ACs covered (DB-level asserts) |
| P8-03 serve in learning language | 18 (BE-TC-01..18) | 8 | 7 | 3 | All ACs covered (2 flags — G-02, G-03) |
| P8-04 change learning language | 0 new | — | — | — | **Already covered** by `P8_04_ChangeLearningLanguage_Tests.cs` (T1–T10 + 2 extras) — see §4 |
| **Total NEW** | **43** | **19** | **15** | **10** | Every story AC mapped to ≥1 case |

Existing relevant coverage reused/extended (not re-authored): `P8_04_ChangeLearningLanguage_Tests.cs`, `P2_02_BrowseSubjectsAndLessons_Tests.cs` (+ `_Extended`), `P5_08_ParentReadApi_Tests.cs` (parent/child + IDOR helper patterns).

## 2. What is genuinely NEW vs already-covered

- **NEW (no prior integration coverage):** P8-01 (entire initial-set path — Add-Child `LearningLanguage`, validation, persistence, `/Me`, claim emission/refresh, student-immutability, IDOR-by-construction), P8-02 (seed roots / schema / index / no-cross-language-edge — DB-level), P8-03 (per-medium serve matrix, redirect-vs-403 asymmetry, claim-not-query, fallback, dashboard/quiz language guard).
- **ALREADY COVERED — do NOT re-author:** P8-04 change path (role gate, confirm-gate 424, IDOR cross-family 403, happy-path 200 + DB + `/Me`, Math/Science reset, Arabic/English intact, gamification retained, idempotency, same-language no-op, invalid code 422, anonymous 401, childId=0 422). The **admin** change path (`POST api/Admin/Users/{childId}/learning-language`, `AdminChangeLearningLanguageCommand`) belongs to **P7-08/P8-04**, not P8-01 — excluded here to avoid overlap.
- **Indirectly already exercised:** `P2_02` already proves `ForGrade` returns exactly 4 subjects for an `learning_language="en"` student post-bilingual-seed (its header documents the resolver). P8-03 BE-TC-01..03 deepen this into the full Ar-vs-En matrix; keep both, note the overlap so reviewers don't flag duplication.

## 3. Coverage matrix (AC → coverage)

### P8-01 — Set a child's learning language
| Acceptance criterion | Covered by | Status |
|---|---|---|
| Child carries `LearningLanguage` (ar\|en), separate from `PreferredLanguage` | BE-TC-01, 02, 03 | NEW |
| Parent sets it at add/onboarding; **required** | BE-TC-01, 04 (omitted→422), 05 (empty→422) | NEW |
| Valid value enforcement (ar\|en) | BE-TC-06 (fr→422), 07 (AR→422, case) | NEW |
| Immutable by the student (no student-facing change) | BE-TC-12 | NEW |
| Surfaced on JWT (`learning_language`) so Learning resolves without cross-module call | BE-TC-09 (behavioral via ForGrade), 10 (refresh) | NEW |
| `GET /Me` returns `learningLanguage` | BE-TC-08 | NEW |
| UI `PreferredLanguage` defaults to match LearningLanguage at onboarding, independently editable | BE-TC-11 (+ G-01 flag) | **Partial — see G-01** |
| (supporting) parent-driven, IDOR-safe; anonymous blocked | BE-TC-13 (401), 14 (IDOR by construction), 15 (duplicate-email integrity) | NEW |

### P8-02 — Bilingual curriculum (parallel trees)
| Acceptance criterion | Covered by | Status |
|---|---|---|
| `Subject` carries stable `SubjectCode` + `Language` | BE-TC-01, 03 | NEW (DB-level) |
| Language **only** on Subject; children inherit (no child-entity column) | BE-TC-04 | NEW (model-inspection) |
| Seeder authors 6 roots per grade (MATH/ar,MATH/en,SCIENCE/ar,SCIENCE/en,ARABIC/ar,ENGLISH/en) | BE-TC-01, 02 | NEW (DB-level) |
| Migration adds columns + index on (GradeId, SubjectCode, Language) | BE-TC-05 (index exists), 06 (uniqueness enforced) | NEW |
| Existing single-language seed migrated/replaced — no orphan trees | BE-TC-01 (exact set), 03 (no untagged), 07 (idempotent re-run) | NEW |
| (note) parallel trees may differ structurally | BE-TC-08 | NEW |
| (note) no cross-language KnowledgeEdge | BE-TC-09 | NEW (+ blocker note if edge→subject not queryable) |
| Content item resolves correctly per language; no cross-language leakage | BE-TC-10 | NEW |

### P8-03 — Serve curriculum in learning language
| Acceptance criterion | Covered by | Status |
|---|---|---|
| Single resolver: ARABIC→ar, ENGLISH→en, MATH/SCIENCE→learnerLang | BE-TC-01, 02 (matrix), 10, 11 (pinned) | NEW |
| All curriculum reads return resolved tree (subjects-for-grade, skill tree, lessons-in-unit, lesson, quiz, dashboard) | ForGrade BE-TC-01/02; Lessons BE-TC-04/06; SkillTree BE-TC-05/07; Lesson BE-TC-08/09; Dashboard BE-TC-17; quiz BE-TC-18 | NEW |
| Ar vs En student same grade: see all 4; Math/Science differ; Arabic/English identical | BE-TC-03, 10, 11 | NEW |
| Edge-case matrix (Ar: Math ar/Sci ar/Ar ar/Eng en; En: Math en/Sci en/Ar ar/Eng en) | BE-TC-01, 02 | NEW |
| Missing-tree fallback: serve other language + warn | BE-TC-15 | NEW |
| Learning language from JWT claim, not query param | BE-TC-13 (spoof ignored), 09 (claim behavioral) | NEW |
| (claim-absent legacy token → Arabic default) | BE-TC-14 (+ unit-test fallback note) | NEW (+ G-02 harness flag) |
| (robustness) switching flips content; empty-state friendly | BE-TC-12 (round-trip), 16 (empty/edge) | NEW |

### P8-04 — Change learning language (AUDIT — already covered)
| Acceptance criterion | Covered by existing test | 
|---|---|
| Parent-only, family-scoped; student cannot | `P8_04` T1 (Student→403), T3 (non-linked→403), Extra (anon→401) |
| Explicit confirm flag or business-validation error | `P8_04` T2 (confirm=false→424, no state change) |
| Update + post-commit `LearningLanguageChangedIntegrationEvent` | `P8_04` T4 (200 + DB + /Me), T8 (event re-delivery) |
| Reset Math/Science only; Arabic/English untouched | `P8_04` T5 (Math/Science→0), T6 (Arabic/English intact) |
| Gamification retained | `P8_04` T7 (XP/streak/badges unchanged) |
| New `learning_language` on next sign-in | `P8_04` T4 (re-sign-in /Me reflects new lang) |
| Idempotent consumer | `P8_04` T8 |
| Same-language no-op | `P8_04` T9 |
| Invalid code / childId=0 validation | `P8_04` T10 (fr→422), Extra (childId=0→422) |

Verdict: **P8-04 fully covered — no new cases needed.** One cross-reference only: P8-03 BE-TC-12 reuses the P8-04 change endpoint to prove the *serve-side* flip (different concern, not duplication).

## 4. What is NOT testable at the integration layer (and how to assert instead)

| Concern | Why not pure-HTTP | Assert via |
|---|---|---|
| P8-02 seed root set / uniqueness / no-orphan | no public "author curriculum" endpoint | direct `LearningDbContext` queries inside the IntegrationTests project (recommended) + `Modules.Learning.UnitTests` (task BE-6) |
| P8-02 "no language column on child entities" | schema fact, not a runtime response | `LearningDbContext.Model` property inspection (BE-TC-04) |
| P8-02 unique index presence | schema fact | `Model` index inspection (BE-TC-05) + behavioral duplicate-insert (BE-TC-06) |
| P8-02 no cross-language edges | depends on edge→Subject queryability | DB join KnowledgeNode→Skill→Concept→Subject; if not queryable, Domain unit test (BE-TC-09 blocker note) |
| P8-03 claim-absent fallback → Ar | hard to mint a claimless student token over HTTP | pure unit test of `LearningLanguageClaimAccessor` (BE-TC-14 / G-02) |
| P8-03 "log a warning" on fallback | log assertion is brittle at integration layer | assert the **served tree** is correct (behavioral); leave the log to code review |
| P8-01 exact JWT claim string | claim is internal, format may change | behavioral assert via curriculum resolution (BE-TC-09); optional direct decode if desired |
| P8-04 "very clear warning" copy | FE concern | out of backend scope (FE lead) |

## 5. Gaps / flags for the lead (decisions before implementation)

- **G-01 (P8-01 AC sub-clause):** "UI `PreferredLanguage` defaults to match LearningLanguage at onboarding." The Add-Child **validator currently requires `Language` (UI) as its own NotEmpty ar|en field** — so the "auto-default from LearningLanguage when omitted" behavior is **not enforced server-side**; the FE supplies both. BE-TC-11 asserts the stored values; the *default-match-when-omitted* clause is effectively a FE/onboarding-flow concern. **Decision needed:** is this AC satisfied by FE passing matched values (then BE-11 P2 is enough), or should the backend default `PreferredLanguage` from `LearningLanguage` when `Language` is absent? If the latter, it's a small BE change + a stronger BE-11.

- **G-02 (P8-03 claim-absent fallback):** minting a claimless **student** token at the HTTP layer is awkward (all student tokens now carry `learning_language`). Recommend asserting the `Ar` default via a `LearningLanguageClaimAccessor` **unit test** rather than integration. BE-TC-14 documents both options. **Decision:** accept unit-test coverage for this AC variant?

- **G-03 (P8-03 quiz/start-attempt language guard):** the story + task BE-4 list **quiz/start-attempt** among language-filtered reads, but this pass only *confirmed* the guard in Subjects-for-grade / Lessons / SkillTree / single-Lesson / Dashboard. `StartAttemptCommandHandler` references LearningLanguage (it's in the grep hit list) but the **enforcement shape** (403? 404? silent?) was not verified line-by-line. BE-TC-18 instructs the tester to assert the *actual* enforced behavior and flag if start-attempt is **unguarded**. **Decision:** confirm the intended start-attempt behavior so the tester asserts the right status, not just observes it.

- **Non-blocking observation (asymmetry is by design):** single-lesson read returns **403** on wrong language while list reads **silently redirect**. This is intentional in code (P8-03 BE-TC-06/07 vs BE-TC-08). Flagged so the reviewer does not treat the 403 as a bug — but worth a one-line product confirm that a hard 403 (vs redirect) is the desired UX contract for direct lesson links.

## 6. Risk notes (where coverage is weighted)

1. **Cross-language access via direct id (highest risk):** a student passing another language's SubjectId/LessonId must never receive wrong-language content. Weighted heavily — BE-TC-06/07 (redirect) + BE-TC-08 (403) + BE-TC-13 (no query-param override). This is the security-sensitive core of P8-03.
2. **Claim is the single source of truth:** the whole serve path trusts `learning_language` from the JWT. BE-TC-09/10/13 guard emission, refresh re-issuance, and non-overridability.
3. **Seed integrity (orphans/duplicates):** a missing or duplicated tree silently corrupts the matrix. BE-TC-01/03/06/07 (P8-02) guard the exact root set + uniqueness + idempotency.
4. **Initial-set required-ness:** an unset LearningLanguage would silently default students to Arabic content. BE-TC-04/05/06 (P8-01) enforce required + valid.
5. **Fallback resilience:** a seed gap must degrade gracefully (serve other tree, not 500/404). BE-TC-15 (P8-03).

## 7. Handoff

- `docs/qc/P8-01/backend-test-cases.md` → **api-tester** (Identity/Parent integration tests; mirror `P8_04` helpers).
- `docs/qc/P8-02/backend-test-cases.md` → **api-tester** (DB-level + model-inspection in IntegrationTests; coordinate with `Modules.Learning.UnitTests` for BE-6 overlap).
- `docs/qc/P8-03/backend-test-cases.md` → **api-tester** (Learning serve-path integration tests; same-grade Ar/En children).
- Each folder's `execution-report.md` is a stub the **api-tester fills** (pass/fail per case + defects). qc-test-designer does not fill results.
- **Frontend E2E (RTL/ar+en surfacing, parent-app onboarding/change UX)** is the FE lead's `frontend-e2e-tester` scope — out of this backend deliverable.
