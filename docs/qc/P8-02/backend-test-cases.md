# P8-02 — Author bilingual curriculum (parallel language trees) · Backend test cases

> Story: [user-stories/Phase-8-Localization/P8-02-bilingual-curriculum-content.md](../../../user-stories/Phase-8-Localization/P8-02-bilingual-curriculum-content.md)
> Task: [tasks/Backend/Phase-8-Localization/P8-02-BE.md](../../../tasks/Backend/Phase-8-Localization/P8-02-BE.md)
> Module: **Learning** · Implemented by **api-tester** · Results → `execution-report.md`

## Surface under test (mapped from the running code)
- **Entity:** `Subject` now carries `SubjectCode` (`MATH`/`SCIENCE`/`ARABIC`/`ENGLISH`) and `Language` (`ContentLanguage` `Ar`/`En`), both stored as int.
- **EF config:** `SubjectConfig` — UNIQUE index `IX_Subjects_GradeId_SubjectCode_Language` on `(GradeId, SubjectCode, Language)`.
- **Seeder:** `LearningSeeder.SeedAsync` authors, **per grade (1–6)**, exactly **6 subject roots**: `MATH/Ar`, `MATH/En`, `SCIENCE/Ar`, `SCIENCE/En`, `ARABIC/Ar`, `ENGLISH/En` (see `SeedMathArAsync`/`SeedMathEnAsync`/… ). `EnsureSubjectAsync` is idempotent on the `(GradeId, SubjectCode, Language)` triplet. Children (Units/Lessons/Concepts/Skills/QuizQuestions) inherit language via their owning Subject — **no language column on child entities**.
- **Knowledge graph:** edges authored per-language tree separately (`AuthorEdgesAsync(..., "MATH/Ar")` and `"MATH/En"`) — **no cross-language edges**.

## Important — what is testable at the INTEGRATION layer vs unit/DB-assert
P8-02 is largely a **seed + schema** story. There is no dedicated HTTP "author curriculum" endpoint; the seeder runs in-process. Therefore:
- **Assert via direct `LearningDbContext` queries** inside the test (the harness already exposes `_factory.Services.CreateScope()` → `GetRequiredService<LearningDbContext>()`, used throughout `P8_04`). This is the recommended approach for seed/schema assertions and is fully doable in the IntegrationTests project.
- The pure-seed/unit assertions (exact root set, uniqueness) are **also** covered by `Modules.Learning.UnitTests` per task BE-6 — if those exist, the integration cases below are a **belt-and-suspenders DB-level** verification against a real PostgreSQL/Testcontainers schema (catches migration drift the unit test cannot). Flag to lead if BE-6 unit tests are absent → these become the primary coverage.
- The `(GradeId, SubjectCode, Language)` **uniqueness** is enforced by the DB index; assert it by attempting a duplicate insert and expecting a `DbUpdateException` (see BE-TC-06).

## Preconditions (all cases)
In `InitializeAsync`: `await _factory.ApplyMigrationsAndSeedAsync();` then `using var scope = ...; await LearningSeeder.SeedAsync(scope.ServiceProvider);`. Seeder is idempotent — safe to call once per class.

---

## Test cases

### BE-TC-01 — every grade has exactly the 6 expected (SubjectCode, Language) roots
- **Type:** persistence / functional · **Priority:** P0 · **Target:** api-tester
- **Steps:** For each grade number 1–6: resolve `GradeId` (`Grades` where `Number==g`); query `Subjects` where `GradeId==gradeId`; project the set of `(SubjectCode, Language)` pairs.
- **Expected:** the set equals exactly `{ (MATH,Ar), (MATH,En), (SCIENCE,Ar), (SCIENCE,En), (ARABIC,Ar), (ENGLISH,En) }` — 6 roots, no more (no orphan/untagged), no fewer. Specifically: **no `ARABIC/En`** and **no `ENGLISH/Ar`** root exists.
- **Traces to:** AC "seeder authors per grade 6 subject roots: MATH/ar, MATH/en, SCIENCE/ar, SCIENCE/en, ARABIC/ar, ENGLISH/en".

### BE-TC-02 — Math & Science exist in BOTH languages; Arabic only Ar; English only En
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Steps:** Query distinct `Language` values per `SubjectCode` (across all grades, or a representative grade).
- **Expected:**
  - `MATH` → `{Ar, En}` (both).
  - `SCIENCE` → `{Ar, En}` (both).
  - `ARABIC` → `{Ar}` only.
  - `ENGLISH` → `{En}` only.
- **Traces to:** AC "Math/Science exist in both Arabic and English while Arabic/English subjects exist in their single language".

### BE-TC-03 — `SubjectCode` + `Language` are populated on every Subject (no NULL/untagged trees)
- **Type:** persistence / data-integrity · **Priority:** P0 · **Target:** api-tester
- **Steps:** Query `Subjects` for any row where `SubjectCode` is the enum default-but-meaningless OR `Language` not in `{Ar,En}`. (Both are non-null int columns; verify no row falls outside the four codes / two languages.)
- **Expected:** zero rows outside the valid code/language domain — no orphan single-language untagged tree remains after migration/replace.
- **Traces to:** AC "Subject carries a stable SubjectCode and a Language"; AC "existing single-language seed data is migrated/replaced so no orphan trees remain".

### BE-TC-04 — child entities (Unit/Lesson/Concept/Skill/QuizQuestion) carry NO language column; language is inherited via Subject
- **Type:** schema / functional · **Priority:** P1 · **Target:** api-tester
- **Rationale:** AC "Content language is carried only on Subject and inherited by its Units/Lessons/Concepts/Skills/QuizQuestions (no language column added to child entities)."
- **Steps (integration-feasible):** Pick a known MATH/En lesson (e.g. "Introduction to Counting (G1)") and a MATH/Ar lesson; walk `Lesson → Unit → Subject` and assert language is resolved **only** from the owning Subject's `Language`. Cross-check that the same lesson name space differs between trees (MATH/Ar lessons have Arabic names, MATH/En lessons English names) yet neither Lesson row stores a language field.
- **Expected:** a lesson's language is determined solely by its owning Subject; there is no per-lesson language attribute used in queries.
- **Schema-assert alternative:** confirm `LearningDbContext.Model` for `Lesson`/`Unit`/`Concept`/`Skill`/`QuizQuestion` has **no** property named `Language`/`ContentLanguage`. (Reflect over `entityType.GetProperties()`.) This is the precise AC assertion.
- **Traces to:** AC "no language column added to child entities".

### BE-TC-05 — UNIQUE index `IX_Subjects_GradeId_SubjectCode_Language` exists with the right key
- **Type:** schema · **Priority:** P1 · **Target:** api-tester
- **Steps:** Inspect `LearningDbContext.Model.FindEntityType(typeof(Subject))` indexes; find the unique index over `(GradeId, SubjectCode, Language)`.
- **Expected:** an index exists, `IsUnique == true`, columns = GradeId+SubjectCode+Language (name `IX_Subjects_GradeId_SubjectCode_Language`).
- **Traces to:** AC "A migration adds the new columns with an index on (GradeId, SubjectCode, Language)".

### BE-TC-06 — duplicate (GradeId, SubjectCode, Language) insert is rejected by the DB
- **Type:** persistence / negative · **Priority:** P0 · **Target:** api-tester
- **Rationale:** behavioral proof the unique constraint is enforced (catches a missing/ misconfigured index that BE-TC-05's model-inspection would miss against a drifted DB).
- **Steps:** In a scope, take an existing seeded `(GradeId, MATH, En)` root; insert a **second** `Subject` with the same `GradeId`, `SubjectCode=MATH`, `Language=En`; call `SaveChangesAsync`.
- **Expected:** `DbUpdateException` (unique violation) — duplicate tree rejected.
- **Traces to:** AC "index on (GradeId, SubjectCode, Language)" (uniqueness intent). Mirrors task BE-6 "(GradeId, SubjectCode, Language) is unique".

### BE-TC-07 — seeder is idempotent (re-running does not duplicate roots)
- **Type:** functional / idempotency · **Priority:** P1 · **Target:** api-tester
- **Steps:** Count `Subjects` per grade after the first seed; call `LearningSeeder.SeedAsync` a second time in a fresh scope; re-count.
- **Expected:** the per-grade root count is unchanged at exactly 6 — `EnsureSubjectAsync` resolves the existing triplet rather than inserting a duplicate.
- **Traces to:** task BE-4 "(idempotent)"; AC "no orphan trees" (re-run safety).

### BE-TC-08 — Math/Science parallel trees may differ structurally (per-language authoring)
- **Type:** functional · **Priority:** P2 · **Target:** api-tester
- **Rationale:** AC note "Math/Science trees may differ in structure/examples per language — parallel trees allow that." Verify the model supports independent trees (not a shared structure with translated strings).
- **Steps:** For grade 1, load `MATH/Ar` subject's Units and `MATH/En` subject's Units; assert each subject owns its **own** Unit rows (distinct UnitIds, distinct owning SubjectId) — the En tree's units are not the same rows as the Ar tree's.
- **Expected:** MATH/Ar and MATH/En have disjoint child-entity rows (independent parallel trees), each rooted at its own Subject. Display names differ by language.
- **Traces to:** AC "Replaces the earlier paired-columns idea — parallel trees".

### BE-TC-09 — no cross-language KnowledgeEdge (prereq edges stay within one language tree)
- **Type:** data-integrity / negative · **Priority:** P1 · **Target:** api-tester
- **Rationale:** task note "confirm prerequisite edges are authored per-language tree (no cross-language edges)."
- **Steps:** For each `KnowledgeEdge`, resolve the owning Subject `Language` of both endpoint skills (Skill → Concept → Subject). Assert both endpoints share the same `(GradeId, SubjectCode, Language)` tree — specifically the same `Language`.
- **Expected:** zero edges connect a skill in the `Ar` tree to a skill in the `En` tree (no cross-language prerequisite leakage).
- **Traces to:** task P8-02-BE note "no cross-language edges"; supports AC "parallel language trees".
- **Blocker note:** if `KnowledgeEdge` endpoints cannot be resolved to a Subject via the public service surface, do the resolution directly over `LearningDbContext` (KnowledgeNode→Skill→Concept→Subject). If the edge model does not link to Skills in a queryable way at the integration layer, mark **partially blocked** and defer the cross-language assertion to a Domain unit test, noting the path in the execution report.

### BE-TC-10 — a content item resolves to exactly one language tree (no cross-language leakage in a lesson lookup)
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Rationale:** AC "a content item resolves correctly per language. No cross-language leakage."
- **Steps:** Take the known MATH/En G1 lesson; walk to its Subject; assert `Subject.Language == En` and `Subject.SubjectCode == MATH`. Take the MATH/Ar G1 lesson; assert its Subject is `MATH/Ar`. Confirm the two lessons belong to **different** Subjects (no shared parent).
- **Expected:** each lesson maps to exactly one `(SubjectCode, Language)` Subject; the Ar and En lessons never share a parent Subject — content does not leak across languages.
- **Traces to:** AC "a content item resolves correctly per language. No cross-language leakage."

---

## Notes for the implementer
- All assertions here are DB-level via `LearningDbContext` (and `.Model` for schema/index/property checks) — no HTTP calls needed except where convenient. This is intentional: P8-02 has no public authoring endpoint.
- Reuse the `_factory` + `CreateScope` pattern from `P8_04`. Known seeded lesson names you can anchor on: "Introduction to Counting (G1)" (MATH/En), "What Are Living Things? (G1)" (SCIENCE/En), "مراجعة الحروف الهجائية (ص1)" (ARABIC/Ar), "Sight Words and Fluency (G1)" (ENGLISH/En).
- If `Modules.Learning.UnitTests` already covers BE-TC-01/05/06 (task BE-6), keep the integration versions as DB-level regression guards but note the overlap in the execution report so they aren't flagged as redundant.
