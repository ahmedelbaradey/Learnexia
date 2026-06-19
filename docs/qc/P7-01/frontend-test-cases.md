# P7-01 — Subjects & Units admin — Frontend (web E2E) reference

> Lighter reference deliverable for the **frontend admin lead** (Next.js `admin-dashboard`). The backend lead does NOT
> implement these. Target agent (when the FE lead schedules it): a Playwright runner against the admin dashboard.
> Surface is **admin-only** — Arabic-default RTL + English LTR both apply to admin chrome.

| ID | Title | Type | Pri | Preconditions | Steps | Expected |
|----|-------|------|-----|---------------|-------|----------|
| FE-TC-01 | Subjects list shows the 6 roots per grade with code/language/order/active | functional | P0 | admin signed in; grade with all 6 trees | Open Curriculum → Subjects, pick grade | 6 roots listed, each distinguishable by SubjectCode + Language badge; active state visible |
| FE-TC-02 | Language-coverage view flags gaps | functional | P0 | grade with `ar` Science but no `en` Science | Open coverage view | Missing `(SCIENCE, en)` slot flagged as a gap |
| FE-TC-03 | Create unit under a tree persists scoped to `(code,language)` | functional | P1 | admin; a subject tree | Create unit with title/desc/order; reload | Unit appears only under that tree; not in the sibling language tree |
| FE-TC-04 | Drag-reorder persists within one language tree only | state/functional | P1 | tree with ≥2 subjects | Drag to reorder; reload | New order persisted; sibling tree order unchanged |
| FE-TC-05 | Toggle active/inactive hides from student view but preserves the row | state | P1 | a subject/unit | Toggle inactive; reload admin list | Item still in admin list flagged inactive |
| FE-TC-06 | Duplicate tree create surfaces a friendly error (i18n, not raw key) | error-state/i18n | P1 | existing MATH/Ar in grade | Try to create another MATH/Ar | Inline error shown in localized copy; no row added |
| FE-TC-07 | Delete non-empty unit shows "unit not empty" error | error-state | P1 | unit with a lesson | Attempt delete | Localized "unit not empty" message; unit retained |
| FE-TC-08 | Non-admin is redirected / blocked | auth-routing | P0 | signed in as non-admin (or signed out) | Navigate to Curriculum admin URL | Redirect / 403 screen; no admin data rendered |
| FE-TC-09 | Server 404/400 on edit (PR #183) surfaces friendly copy, no raw stack | error-state | P2 | edit a stale (deleted) subject | Submit edit | Friendly "not found"/"cannot save" copy; no stack trace shown |
| FE-TC-10 | Arabic-default RTL layout for admin curriculum screens | RTL-i18n | P2 | locale = ar | Open Subjects screen | Layout mirrors RTL; English LTR when locale=en |
