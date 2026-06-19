# P7-08 — Coverage Report (Child Profiles & Grade Overrides)

**Story:** `user-stories/Phase-7-Admin-Console/P7-08-manage-child-profiles-and-grade-overrides.md`
**Controller:** `AdminUsersController` (profile / grade / learning-language) — `AdminOnly`.
**Baseline:** `P7_06_07_08_UserAccountAdmin_Tests.cs` + `P7_12_AuditLog_Tests.cs`.

## Acceptance-criterion → test-case matrix

| AC | Criterion (abridged) | Covering cases | Verdict |
|----|----------------------|----------------|---------|
| AC-1 | Edit PreferredLanguage / country = plain field write, **no progress affected** | 08-20 (covered) + **08-21/22/23 (GAP)** | Saved-value covered; "no progress affected" + partial-update gaps |
| AC-2 | LearningLanguage change + confirm → `IChildAccountService.ChangeLearningLanguageAsync`, **hard-deletes Math/Science**, emits event; confirm-gated like P8-04 | 08-50, 08-51, 08-54 (covered) + **08-52, 08-55, 08-56, 08-57 (GAP)** | Gate + field-update covered; **the destructive fresh-start (the heart of AC-2) is uncovered** |
| AC-3 | LearningLanguage change NOT confirmed → nothing deleted, field unchanged | 08-50, 08-51 (covered) + **08-52 (attempts survive) (GAP)** | Field unchanged covered; "nothing deleted" gap |
| AC-4 | Override grade + confirm → grade updates, curriculum re-scopes | 08-33, 08-35 (covered) + **08-36 (same-grade), 08-40 (event) (GAP)** | Core covered; no-op + re-scope event gaps |
| AC-5 | **History preserved** (XP/level/badges/streaks/mastery), re-scope via integration event | 08-37 (covered) | Covered (attempts + XP-awards preserved) |
| AC-6 | Invalid grade (1–6) / unsupported language / country → clear validation | 08-30, 08-31, 08-53 (covered) + **08-25 (preferredLanguage), 08-32 (boundary accept) (GAP)** | Grade + learning-lang covered; **preferredLanguage value validation gap** |
| AC-7 | Every override/edit records actor/timestamp/old→new/reason, **audited (P7-12)** | **08-80..08-84 (GAP — none covered)** | **NOT COVERED — top gap** |
| AC-8 | Only admin; non-admin → 403 | 08-01, 08-02, 08-04, 08-07, 08-08 (covered) + **08-03/05/06/09/10 (GAP)** | Anonymous + some parent covered; **grade parent-403 + basic-role gaps** |

## Prioritized backend gap list for `api-tester`

**P0 (do first):**
1. **BE-TC-08-55 + 08-52 — destructive fresh-start verification** (AC-2/3): confirmed change hard-deletes Math/Science attempts while **retaining Arabic/English + gamification**, and the not-confirmed path leaves all attempts intact. This is the single most important uncovered behaviour in P7-08.
2. **BE-TC-08-80 / 81 / 82 / 84 — audit rows** for `Child.GradeOverridden` (with `oldGrade=…;newGrade=…` Details), `Child.LearningLanguageChanged`, `Child.ProfileUpdated`, and gate-before-audit (AC-7).
3. **BE-TC-08-36 — same-grade no-op → 400** (no phantom event); the handler guard is untested.
4. **BE-TC-08-05 — grade-override parent-403** (AC-8); only anonymous is tested for grade.

**P1:**
5. BE-TC-08-25 — unsupported preferredLanguage → 422 (AC-6).
6. BE-TC-08-34 — grade confirm=false leaves grade unchanged (no mutation).
7. BE-TC-08-57 — same learning-language → no-op success (no event, no reset).
8. BE-TC-08-83 — audit Details PII-safety for P7-08 rows.
9. BE-TC-08-22 — profile edit does not touch XP/attempts.
10. BE-TC-08-21 — preferredLanguage value reflected on profile.
11. BE-TC-08-26 / 39 / 59 — not-found (99999999) on profile / grade / learning-language.
12. BE-TC-08-40 / 56 — integration events emitted (ChildGradeChanged / LearningLanguageChanged).
13. BE-TC-08-03 / 06 / 09 — basic-role 403 on all three endpoints.
14. BE-TC-08-10 — expired admin JWT → 401.

**P2:** 08-32 (grade 1 and 6 inclusive boundary accept), 08-23 (null = no change partial update), 08-60 (learning-language claim staleness Q-C3).

## Headline counts
- Total backend cases: **41** — Covered **14**, GAP **27** (PARTIAL counted as GAP).
- By priority: P0 = 14, P1 = 21, P2 = 6.
- Frontend reference cases: **11** (admin-dashboard; likely Blocked if UI not built).
