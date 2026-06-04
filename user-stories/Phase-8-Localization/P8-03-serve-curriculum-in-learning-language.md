# Serve curriculum in the student's learning language

- **Project:** Learnexia
- **Sprint / Phase:** Phase 8 — Localization
- **Epic:** Localization
- **Issue type:** Story
- **Story Points:** 5 — read-path language resolution across all curriculum queries.
- **Labels:** `localization`, `backend`, `learning`
- **Requirements:** NFR-5, FR-LR-1

## Description
As a student, I want every subject's content shown in the correct language — Math/Science in my learning language, the Arabic subject in Arabic and the English subject in English — so that the curriculum matches my school.

## Acceptance Criteria
- A single **resolver** computes effective content language per subject: `ARABIC → ar`, `ENGLISH → en`, `MATH`/`SCIENCE → student's LearningLanguage`.
- Curriculum read queries (subjects-for-grade, skill tree, lessons-in-unit, lesson, quiz, dashboard) return the tree whose `Subject.Language` matches the resolved language.
- An Arabic-medium and an English-medium student in the same grade both see **all four** subjects; only Math/Science differ in language; Arabic/English subjects are identical for both.
- Missing-tree fallback: serve the other language tree and log a warning (should not occur once seeded).
- The student's learning language comes from the **JWT claim** (`learning_language`), not a query parameter.

## Acceptance Criteria — edge case
- Arabic-medium student: Math(ar), Science(ar), Arabic(ar), English(en).
- English-medium student: Math(en), Science(en), Arabic(ar), English(en).

## Notes
- Resolution + read path detailed in [docs/architecture/localization-architecture.md](../../docs/architecture/localization-architecture.md) §2 & §4.
- Blocked by: P8-01 (LearningLanguage + claim), P8-02 (SubjectCode/Language + seed), P2-02/03/04/09.
