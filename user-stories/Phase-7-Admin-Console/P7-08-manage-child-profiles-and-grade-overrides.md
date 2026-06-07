# Manage child profiles & grade overrides

- **Project:** Learnexia
- **Sprint / Phase:** Phase 7 — Admin Console (post-MVP / ongoing)
- **Epic:** Admin — User & Account Management
- **Issue type:** Story
- **Story Points:** 5 — admin edits to a child's grade/languages/country; grade override re-scopes curriculum while preserving history (per P5-06) via integration events, and a learning-language change triggers the same confirm-gated fresh-start hard-delete as P8-04.
- **Labels:** `admin`, `identity`, `backend`, `frontend`
- **Requirements:** SRS §3 (Admin role), FR-ADM-7

## Description
As an admin, I want to edit a child's profile and override their grade, so that I can correct mistakes and help with support cases without forcing the parent to redo onboarding.

## Acceptance Criteria
- Given a child account, when I edit **`PreferredLanguage`** (UI language) or **country**, then the change is a plain field update that is saved and reflected on the child's profile — **no progress is affected**.
- Given a child account, when I change **`LearningLanguage`** (medium of instruction, ar/en) and **confirm**, then the change goes through **`IChildAccountService.ChangeLearningLanguageAsync`** (not a naive field write): it **hard-deletes the child's Math/Science attempts/progress** (a fresh start in the new-language Math/Science trees) and emits the **`LearningLanguageChangedIntegrationEvent`** so other modules clean up. This is **confirm-gated** with the exact same warning + typed confirmation as the parent flow (**P8-04**); Arabic/English subjects are pinned by subject and unaffected.
- Given a `LearningLanguage` change that is **not** confirmed, then nothing is deleted and the field is unchanged (the destructive path never runs without confirmation).
- Given a child account, when I **override the grade** and confirm, then the grade updates and the curriculum/skill tree **re-scopes to the new grade** (same behavior as the parent-driven transition, P5-06).
- **History is preserved:** XP, level, badges, streaks, and past mastery records carry over and are not deleted; re-scoping is signaled to learning/gamification via integration events (no cross-module FK).
- Given an invalid grade (outside 1–6) or an unsupported language (`PreferredLanguage`/`LearningLanguage` outside ar/en) or country, then the edit is rejected with a clear validation message.
- Every override/edit — including a `LearningLanguage` change — records **actor, timestamp, old → new values, and reason**, and is **audited** (P7-12).
- Only an admin can perform these edits; non-admin → 403/redirect.

## Notes
- Surface: **Next.js `admin-dashboard`** app, built on the P1-10 admin shell.
- Depends on: P1-10 (admin shell), P1-05 (Admin policy), P1-01/P1-03/P1-04 (Identity, parent/child), P7-06 (inspect/search), P7-12 (audit log). **P5-06 has not been built yet** — P7-08 introduces the `ChildGradeChanged` integration event in `Shared.Contracts` so P5-06 can consume the same seam when it lands.
- Reuses the **Identity** module. A child has **two** language fields: `PreferredLanguage` (UI — a harmless field write) and `LearningLanguage` (medium of instruction). Changing `LearningLanguage` is **destructive**: it must go through `IChildAccountService.ChangeLearningLanguageAsync` + emit `LearningLanguageChangedIntegrationEvent`, hard-deleting the child's Math/Science progress for a fresh start in the new-language trees — the **same confirm-gated flow as P8-04** (the parent-driven change), never a naive field assignment. Grade override **preserves XP/badges/streaks/mastery history** and re-scopes curriculum via the `ChildGradeChanged` integration event (no cross-module FK). No teacher role.
