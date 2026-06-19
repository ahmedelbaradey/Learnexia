# P7-04 — Question authoring admin — Frontend (web E2E) reference

> Lighter reference for the frontend admin lead. Note the impl reality: authoring is per-lesson questions, not a
> separate Quiz aggregate (see backend-test-cases.md Group C). The FE editor should reflect that.

| ID | Title | Type | Pri | Preconditions | Steps | Expected |
|----|-------|------|-----|---------------|-------|----------|
| FE-TC-01 | Author a question of each of the 4 types with type-specific editor | functional | P0 | admin; a lesson | Add MCQ / TrueFalse / Matching / FillInBlank | Each renders its type's option/correct-answer editor |
| FE-TC-02 | Invalid per-type shape shows friendly validation error | error-state/i18n | P0 | question editor | Submit MCQ with no correct option | Localized "must have a correct option" error; not saved |
| FE-TC-03 | Edit/remove question updates ordered list | state | P1 | lesson with ≥2 questions | Edit one, delete another; reorder | List reflects edits + new order |
| FE-TC-04 | correctAnswer never shown to a student preview | security | P0 | a question | Open student preview | No correct answer visible |
| FE-TC-05 | Cross-language attach blocked with friendly copy | error-state/i18n | P1 | ar lesson + en skill | Try to attach | Localized "same language tree" error |
| FE-TC-06 | Question language shown for context, not editable | i18n | P2 | a question | Open editor | Language read-only label |
| FE-TC-07 | Non-admin blocked / redirected | auth-routing | P0 | non-admin / signed out | Open question editor URL | Redirect / 403 screen |
| FE-TC-08 | RTL (ar) vs LTR (en) for the question editor | RTL-i18n | P2 | locale ar then en | Open editor | Mirrored RTL for ar |
