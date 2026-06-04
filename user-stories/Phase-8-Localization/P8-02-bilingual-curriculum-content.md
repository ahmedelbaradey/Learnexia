# Author bilingual curriculum (parallel language trees)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 8 — Localization
- **Epic:** Localization
- **Issue type:** Technical Enabler
- **Story Points:** 5 — schema change on `Subject` + bilingual seed of medium-dependent subjects.
- **Labels:** `localization`, `backend`, `learning`, `data`
- **Requirements:** NFR-5, FR-LR-1

## Description
As a backend engineer, I want curriculum content stored as **parallel language trees keyed on the subject**, so that Math/Science exist in both Arabic and English while Arabic/English subjects exist in their single language — without per-row translation columns.

## Acceptance Criteria
- `Subject` carries a stable **`SubjectCode`** (`MATH` | `SCIENCE` | `ARABIC` | `ENGLISH`) and a **`Language`** (`ar` | `en`).
- Content language is carried **only** on `Subject` and inherited by its Units/Lessons/Concepts/Skills/QuizQuestions (no language column added to child entities).
- The seeder authors, per grade, **6 subject roots**: `MATH/ar`, `MATH/en`, `SCIENCE/ar`, `SCIENCE/en`, `ARABIC/ar`, `ENGLISH/en`.
- A migration adds the new columns with an index on `(GradeId, SubjectCode, Language)`.
- Existing single-language seed data is migrated/replaced so no orphan trees remain.

## Notes
- Replaces the earlier "paired ar/en columns on every entity" idea — see [docs/architecture/localization-architecture.md](../../docs/architecture/localization-architecture.md) §3.
- Math/Science trees may differ in structure/examples per language — parallel trees allow that.
- Blocked by: P2-01 (curriculum entities), P2-10 (existing seed).
