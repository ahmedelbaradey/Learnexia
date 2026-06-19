# P7-05 — Content lifecycle (publish/version/preview) — Frontend (web E2E) reference

> Lighter reference for the frontend admin lead. The lifecycle UI spans the whole curriculum hierarchy.

| ID | Title | Type | Pri | Preconditions | Steps | Expected |
|----|-------|------|-----|---------------|-------|----------|
| FE-TC-01 | Edits accumulate in Draft, not served to students | functional | P0 | admin; a published item | Edit it (don't publish); check student view | Student sees old content; admin sees pending draft |
| FE-TC-02 | Publish records a new version (timestamp + author) | functional | P0 | a draft | Click Publish | New version listed with timestamp + author; students now receive it |
| FE-TC-03 | Per-language publication-coverage view flags one-sided coverage | functional/i18n | P0 | ar Math published, en Math draft | Open coverage view | en Math flagged as not-published-in-parallel |
| FE-TC-04 | Live vs pending-draft are distinguishable | state | P1 | published item with newer draft edits | Open item | Both live + pending-draft states shown distinctly |
| FE-TC-05 | Preview renders draft as a student would, in tree language (RTL ar / LTR en) | functional/RTL-i18n | P1 | a draft in ar tree, one in en tree | Open Preview for each | ar preview RTL; en preview LTR; not published |
| FE-TC-06 | Rollback restores previous published version for that tree only | functional | P1 | item with ≥2 versions | Roll back | Previous version restored; sibling-language tree untouched |
| FE-TC-07 | Publish acts on one `(SubjectCode,Language)` tree only | state/i18n | P1 | ar + en trees | Publish ar tree | en tree remains draft |
| FE-TC-08 | Student / non-admin cannot open Preview (no Draft leak) | auth-routing/security | P0 | non-admin / signed out | Hit Preview URL | Redirect / 403; no draft content |
| FE-TC-09 | Illegal transition surfaces friendly error | error-state | P2 | a Draft item | Attempt Draft→Archived | Localized "illegal transition" message |
