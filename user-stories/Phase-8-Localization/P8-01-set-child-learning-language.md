# Set a child's learning language

- **Project:** Learnexia
- **Sprint / Phase:** Phase 8 — Localization
- **Epic:** Localization
- **Issue type:** Story
- **Story Points:** 3 — new student attribute + onboarding wiring + JWT claim.
- **Labels:** `localization`, `backend`, `identity`
- **Requirements:** NFR-5 (localization), product decision "Arabic-first, bilingual"

## Description
As a parent, I want to choose the **language my child learns in** (Arabic-medium or English-medium) when I add the child, so that Math and Science content is delivered in the right language from day one.

## Acceptance Criteria
- A child carries a **`LearningLanguage`** (`ar` | `en`) attribute, separate from the UI `PreferredLanguage`.
- The parent sets `LearningLanguage` when adding/onboarding a child; it is **required**.
- `LearningLanguage` is **immutable by the student** — there is no student-facing way to change it.
- The value is surfaced on the authenticated student's **JWT** (claim `learning_language`) so the Learning module can resolve content language without a cross-module call.
- `GET /Me` (or the child profile) returns `learningLanguage`.
- The UI language (`PreferredLanguage`) defaults to match the chosen `LearningLanguage` at onboarding but remains independently editable.

## Notes
- `LearningLanguage` ≠ `PreferredLanguage` (medium of instruction vs app language) — see [docs/architecture/localization-architecture.md](../../docs/architecture/localization-architecture.md) §1.
- Parent-driven onboarding (students never self-register). Changing it later is parent-only — see **P8-04**.
- Blocked by: P1-03/P1-04 (add/link child), P1-09 (`/Me`).
