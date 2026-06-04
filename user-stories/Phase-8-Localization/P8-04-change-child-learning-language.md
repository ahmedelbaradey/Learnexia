# Change a child's learning language (parent-only, fresh start)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 8 — Localization
- **Epic:** Localization
- **Issue type:** Story
- **Story Points:** 5 — parent-only mutation + fresh-start reset via integration event.
- **Labels:** `localization`, `backend`, `identity`, `learning`
- **Requirements:** NFR-5, child-data protection

## Description
As a parent, I want to change my child's learning language (rare — typically at the start of a school year), with a clear warning that it **resets the child's Math/Science progress**, so that a mis-set medium can be corrected deliberately.

## Acceptance Criteria
- Only a **parent** (family-scoped to their own child) can change `LearningLanguage`; the student cannot.
- The request requires an **explicit confirmation flag** acknowledging the fresh start; without it the API returns a business-validation error (not a silent change).
- On change: `User.LearningLanguage` is updated and a `LearningLanguageChangedIntegrationEvent` is published **after commit**.
- The Learning module consumes the event and **resets the child's Math/Science attempts + mastery**; **Arabic/English progress is untouched**.
- Global gamification (XP, streak, badges, level) is **retained** (engagement, not curriculum-bound).
- On the child's next sign-in, the JWT carries the new `learning_language` and the curriculum switches trees.

## Notes
- Cross-module via `Shared.Contracts` integration event — no cross-module FK; dispatched post-commit (ADR 0002). See [docs/architecture/localization-architecture.md](../../docs/architecture/localization-architecture.md) §5.
- The "very clear warning" copy is a FE concern (parent app); the backend enforces the confirm flag.
- Blocked by: P8-01, P8-02, P8-03.
