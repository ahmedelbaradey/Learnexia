# P7-06 — Admin User Search & Inspect — Frontend (web E2E) test cases (reference)

**Target agent:** `frontend-e2e-tester` (owned by the FE lead).
**Status:** REFERENCE deliverable. The admin console is a **Next.js `admin-dashboard`** app (per the story Notes), separate from the Expo student app this backend lead's `frontend-e2e-tester` normally drives. If the admin dashboard UI is not yet built, mark all cases **Blocked (UI not implemented)** and keep this doc as the spec.

**Note (Arabic-default RTL):** the admin console copy direction follows the same ar/en convention as the rest of the product; assert RTL when locale = ar.

| ID | Title | Type | Pri | Steps | Expected |
|----|-------|------|-----|-------|----------|
| FE-TC-06-01 | Signed-out user cannot reach /admin/users | auth | P0 | Visit `/admin/users` with no session | Redirected to admin sign-in (no PII rendered) |
| FE-TC-06-02 | Non-admin (parent) blocked from admin console | authz | P0 | Sign in as parent, visit `/admin/users` | 403 / redirect away; list never renders |
| FE-TC-06-03 | Admin sees paginated user list | functional | P0 | Sign in admin → Users | Table renders rows; pagination control visible; server-paged |
| FE-TC-06-04 | Filter by role (parent/child) | functional | P1 | Select role=Parent | Only parent rows shown |
| FE-TC-06-05 | Filter by status (active/suspended) | functional | P1 | Select status=Suspended | Only suspended rows; suspended badge shown |
| FE-TC-06-06 | Free-text search by name/email | functional | P0 | Type an email in search | Matching user appears; debounced server call |
| FE-TC-06-07 | Empty search → friendly empty state | state | P1 | Search a no-match string | "No results" message (i18n), not an error |
| FE-TC-06-08 | Open profile → read-only detail with both language fields labelled | functional | P0 | Click a child row | Profile shows PreferredLanguage AND LearningLanguage, distinctly labelled |
| FE-TC-06-09 | Profile shows grade + country for a child | functional | P1 | Open a child | Grade + country rendered |
| FE-TC-06-10 | Family panel: parent → children, child → parents | functional | P1 | Open a parent then a child | Linked family rendered both directions |
| FE-TC-06-11 | Activity summary renders / degrades gracefully | state | P1 | Open activity tab | Activity shown or "no recent activity"; last sign-in shown as not-tracked, never a crash |
| FE-TC-06-12 | List does not render sensitive child PII | PII | P1 | Inspect list columns | No grade/nationality/learning-language column in the list view |
| FE-TC-06-13 | RTL layout when locale = ar | RTL-i18n | P1 | Switch to Arabic | Table + filters mirror to RTL; copy in Arabic, not raw keys |
| FE-TC-06-14 | Loading + error states on the list | state | P2 | Throttle network / force 500 | Skeleton on load; localized error banner on failure |
