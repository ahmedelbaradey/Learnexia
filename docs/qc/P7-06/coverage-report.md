# P7-06 — Coverage Report (Search & Inspect Users)

**Story:** `user-stories/Phase-7-Admin-Console/P7-06-search-and-inspect-users.md`
**Controller:** `backend/src/Modules/Identity/Learnexia.Modules.Identity.Api/Controllers/AdminUsersController.cs`
**Baseline tests:** `backend/tests/Learnexia.IntegrationTests/P7_06_07_08_UserAccountAdmin_Tests.cs` + `P7_12_AuditLog_Tests.cs`

## Acceptance-criterion → test-case matrix

| AC | Criterion (abridged) | Covering cases | Verdict |
|----|----------------------|----------------|---------|
| AC-1 | Paginated list, filterable by role / status / free-text; server-paged | 06-20, 06-21, 06-22, 06-30, 06-32, 06-36 (covered) + **06-31, 06-33, 06-34, 06-35, 06-37, 06-38, 06-23/24/25 (GAP)** | Covered for role+q+pagination; **status filter + name-q are gaps** |
| AC-2 | Read-only profile incl. both language fields, grade, country, status, dates | 06-50, 06-51, 06-53 (covered) + **06-52 (status fields GAP)** | Mostly covered; status-governance fields gap |
| AC-3 | Parent→children, child→parents linkage | 06-60, 06-61 (covered) + **06-62, 06-63, 06-64 (GAP)** | Core covered; empty/not-found/PII gaps |
| AC-4 | Recent activity summary via integration contracts | 06-70, 06-71 (covered) + **06-72, 06-73 (GAP)** | Core covered; real-child + not-found gaps |
| AC-5 | Only admin can reach; non-admin → 403 | 06-01..06-12 (mostly covered) + **06-06/09/12 basic-role + 06-13 invalid-token (GAP)** | Covered for anon+parent; basic-role on 3 endpoints + invalid-token gap |
| AC-6 | Read-only inspection is **audited** (P7-12) | **06-80, 06-81, 06-82 (GAP — none covered)** | **NOT COVERED — highest-value gap** |
| (PII brief Q-A1) | List DTO minimal child PII | 06-40 (covered) + 06-41 (allow-list GAP) | Covered |

## Prioritized backend gap list for `api-tester`

**P0 (do first):**
1. **BE-TC-06-80 / 06-81 / 06-82 — audit-trail emission for `User.Viewed` + `User.Searched`** (AC-6). The handlers emit these events but nothing verifies them; 06-81 also asserts the **PII-safe Details** (q-length only, never the raw search term/child name). Highest value.
2. **BE-TC-06-34 / 06-35 — `status` (AccountStatus int) filter** — entirely untested filter dimension.
3. **BE-TC-06-06 / 06-09 / 06-12 — basic-role 403** on profile-detail, family, and activity (only the parent-role variant exists for those three).

**P1:**
4. BE-TC-06-31 — search by FullName (name-q path; only email-q tested).
5. BE-TC-06-33 — role=Student filter.
6. BE-TC-06-52 — profile status/reason/timestamp governance fields.
7. BE-TC-06-55 — id ≤ 0 guard (BadRequest/NotFound, not 500).
8. BE-TC-06-63 / 06-73 — graceful not-found on family + activity.
9. BE-TC-06-62 — parent with no children → empty children[].
10. BE-TC-06-25 — empty result → EmptyCollection 200 successed=true.
11. BE-TC-06-13 — malformed/expired JWT → 401.
12. BE-TC-06-64 — family DTO minimal-PII shape.
13. BE-TC-06-72 — activity for a real child with no learning data.

**P2:** 06-23, 06-24, 06-26 (page coercion + OrderBy whitelist/injection), 06-37 (combined filters), 06-38 (unknown role → empty, asserts "no teacher role"), 06-41 (positive allow-list on list DTO).

## Headline counts
- Total backend cases: **38** — Covered **16**, GAP **22** (PARTIAL counted as GAP).
- By priority: P0 = 12, P1 = 18, P2 = 8.
- Frontend reference cases: **14** (admin-dashboard; likely Blocked if UI not built).
