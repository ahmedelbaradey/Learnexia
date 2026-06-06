# Frontend Gap Analysis — Phase 1 (Foundation) + Phase 2 (Learning Core)

> **Date:** 2026-06-07 · **Author:** FE lead (Claude) · **Method:** diff of the committed backend contract
> (`packages/api-client/swagger.json`, 90+ endpoints) against the FE api-client **hooks** *and* the **screens**
> that actually consume them. Endpoints belonging to later phases were excluded from "gap" status (see §4).
>
> **Headline:** Phase-1/2 FE coverage is **near-complete**. Every core auth/account/parent endpoint and every
> student learning endpoint is both wrapped in a hook **and** surfaced in a screen. The genuine gaps are few
> and small; the only actionable one is **attempt history** (G5), which may legitimately be Phase-5 scope.

---

## 1. Phase 1 (Foundation) — gaps

| # | Gap | Backend | FE state | Severity |
|---|---|---|---|:--:|
| **G1** | **"Remember me" is a no-op.** The login checkbox is UI-only (per the `LoginForm` code comment) — it does not change token persistence or session duration. Stay-signed-in works via the Refresh-Token single-flight, but the explicit toggle does nothing. (P1-02) | `Refresh-Token` ✓ | Cosmetic checkbox | Low |
| **G2** | **CAPTCHA config-gating not wired.** The FE never reads `GET /api/system/configuration`, so it can't conditionally send the `captchaToken` the backend `Register-Parent` accepts. (P1-01) | `system/configuration` advertises it; `Register-Parent` accepts `captchaToken` | Not consumed — already tracked as deferred **P1-11-FE-16** | Low (known) |
| **G3** | **Parent web `index` + `reports` are blank placeholders.** `(parent)/index.tsx` + `(parent)/reports.tsx` are "coming soon" (reports = Phase 5). | — | Intentional 🟡 placeholders | Low (known) |
| **G4** | `POST /api/Users/Authentication/Validate-Token` is unused by the FE. Transport uses Refresh-Token single-flight instead — fine, not a real gap. | ✓ | Unused | Info |
| — | **Google OAuth (P1-12)** is built but **in open PR #98** (not yet merged). Once merged this closes. Also requires the backend `GoogleAuth__ClientId` to match the FE's `EXPO_PUBLIC_GOOGLE_CLIENT_ID`. | ✓ | In flight (#98) | — |

**Complete & wired in Phase 1:** Sign-In, Register-Parent, Refresh-Token, Forgot/Reset-Password, Sign-Out, `/Me`,
Account Profile (GET/PUT), Avatar (POST/DELETE), ChangePassword, **Plan**, **Sessions + SignOutOthers**,
Parent Add/Link/Update/Unlink-Child, Change-Learning-Language, and notification Preferences (GET/PUT).

---

## 2. Phase 2 (Learning Core) — gaps

| # | Gap | Backend | FE state | Severity |
|---|---|---|---|:--:|
| **G5** | **Attempt history — no FE.** No hook and no screen consume this; there is no way to review past quiz attempts. The endpoint lives in the Learning module, but the natural consuming UI (per-child attempt review) may be **Phase-5 parent-analytics** scope — confirm placement before building. | `GET /api/Learning/Students/{studentId}/Attempts` (typed) | **No hook, no screen** | **Medium** |
| **G6** | KnowledgeGraph `Prerequisites/{nodeId}` + `UnlockedBy/{nodeId}` have no hook — **but this is NOT a real student gap.** `SkillNodeDto` already carries `state` + **`missingPrerequisites`**, so the skill tree (`useSubjectSkillTree`) already renders lock state and what's missing (P2-04). These granular endpoints are for a deeper graph explorer (admin/analytics), intentionally unused on the student tree. | ✓ | Covered by `SkillTree` DTO | Info |

**Complete & wired in Phase 2:** browse subjects (`Subjects/ForGrade`, `Subjects/{id}/Lessons`), skill tree
(`Subjects/{id}/SkillTree`), lesson player (`Lessons/{id}`), the full quiz lifecycle (`Quizzes/{lessonId}/Attempt`,
`/Answers`, `/Complete`, `/Abandon`), and the home dashboard (`Learning/Dashboard` → Continue / streak / XP).
The gamification numbers on the dashboard (hearts/streak/XP/league) are **Phase-3 stubs by design** — the BE
returns zero/null in Phase 2; not a Phase-1/2 gap.

---

## 3. Endpoint → FE coverage (Phase-1/2 relevant subset)

| Backend endpoint | FE hook | Surfaced in |
|---|---|---|
| `POST Authentication/Sign-In` | `useSignIn` | `(auth)/login` |
| `POST Authentication/Register-Parent` | `useRegisterParent` | `(auth)/register` |
| `POST Authentication/Refresh-Token` | (transport) | api-client interceptor |
| `POST Authentication/Google-SignIn` | `useGoogleSignIn` | `(auth)/login` *(PR #98)* |
| `POST Authentication/Forgot-Password` | `useForgotPassword` | `(auth)/forgot-password` |
| `POST Authentication/Reset-Password` | `useResetPassword` | `(auth)/reset-password` |
| `POST Authentication/Sign-Out` | `useSignOut` | settings / guard |
| `POST Authentication/Validate-Token` | — | **unused (G4)** |
| `GET Users/Me` | `useMe` | routing guard / shell |
| `GET/PUT Account/Profile` | `useMyProfile` / `useUpdateProfile` | settings → ProfilePanel |
| `POST/DELETE Account/Avatar` | `useUploadAvatar` / `useRemoveAvatar` | settings → ProfilePanel |
| `POST Account/ChangePassword` | `useChangePassword` | settings → SecurityPanel |
| `GET Account/Plan` | `useMyPlan` | settings → PlanPanel |
| `GET Account/Sessions` + `SignOutOthers` | `useMySessions` / `useSignOutOtherSessions` | settings → SecurityPanel |
| `POST Parent/Add-Child` | `useAddChild` | `(onboarding)/add-child` |
| `POST Parent/Link-Child` | `useLinkChild` | `(parent)/link-child` |
| `GET Parent/My-Children` | `useMyChildren` | `(parent)/children` |
| `PUT Parent/Update-Child` | `useUpdateChild` | MyChildrenWeb → EditChildSheet |
| `DELETE Parent/Unlink-Child` | `useUnlinkChild` | settings → LinkedChildrenPanel |
| `PUT Parent/Change-Learning-Language` | `useChangeLearningLanguage` | settings → LinkedChildrenPanel |
| `GET/PUT Notifications/Preferences` | `useNotificationPreferences` / `useUpdateNotificationPreferences` | settings → NotificationsPanel |
| `GET Subjects/ForGrade` | `useSubjectsForGrade` | `(child)/index`, subjects |
| `GET Subjects/{id}/Lessons` | `useSubjectLessons` | `(child)/subjects/[id]` |
| `GET Subjects/{id}/SkillTree` | `useSubjectSkillTree` | `(child)/subjects/[id]/tree` |
| `GET Lessons/{id}` | `useLesson` | `(child)/lessons/[lessonId]` |
| `POST Quizzes/{lessonId}/Attempt` | `useStartAttempt` | lesson player |
| `POST Quizzes/{attemptId}/Answers` | `useSubmitAnswer` | lesson player |
| `POST Quizzes/{attemptId}/Complete` | `useCompleteAttempt` | lesson player |
| `POST Quizzes/{attemptId}/Abandon` | `useAbandonAttempt` | lesson player |
| `GET Learning/Dashboard` | `useDashboard` | `(child)/index` |
| `GET Learning/Students/{studentId}/Attempts` | — | **none (G5)** |
| `GET KnowledgeGraph/Prerequisites|UnlockedBy` | — | covered by SkillTree DTO (G6) |

---

## 4. Explicitly out of Phase-1/2 scope (not counted as gaps)

These backend endpoints exist but belong to later phases; their FE is correctly absent here:

- **Phase 3 — Gamification (`P4-*`):** `Gamification/Badges|Leagues|Missions/Me`, `Gamification/Profile`, `admin/timed-events`. (Gamification FE not started except the P4-07 dashboard league-preview flip; `feat/P4-08` is resumable WIP.)
- **Phase 4 — Re-engagement / push (`P4-09`):** `Notifications/Devices/Register|{tokenId}`, `Notifications/Inbox/*`, `Notifications/Notifications/List`, `Notifications/Preferences/Children/{childId}/Reengagement`, `POST /api/notifications`.
- **Phase 7 — Admin Console:** all `learning/{Subjects|Units|Lessons|Concepts|Grades|Skills}/{Create|Update|Delete|List}` CRUD, `Skills/{id}/Stats`, and the entire `Users/UserManagement/*` + `Users/Authorzation/*` surface. The admin app currently has only the P1-10 sign-in shell.
- **Phase 5 — Parent analytics:** likely the home of the **attempt-history** consumer (G5) and richer reporting (`(parent)/reports`).

> ⚠️ Several `UserManagement/*` endpoints (`GetBoardSecretaryUsers`, `GetFundManagerUsers`, `GetLegalCouncilUsers`, …)
> are **template leftovers from the scaffold origin** ("Jadwa"-style roles) and are not part of the Learnexia
> product (no teacher role; roles are Parent/Student/Admin). Flag for backend cleanup, but not a FE gap.

---

## 5. Recommendations

1. **G5 (attempt history)** — the only actionable Phase-1/2 gap. **Decide scope first:** student-facing "my past attempts" (Phase 2) vs. parent "child attempt review" (Phase 5). Then build a `useStudentAttempts(studentId)` hook + a review screen. Medium priority.
2. **G1 (remember-me)** — small: either wire the toggle to token-persistence behavior (e.g. session- vs persistent-storage selection) or remove the cosmetic checkbox to avoid implying a behavior that doesn't exist.
3. **G2 (CAPTCHA gating)** — leave as the tracked **P1-11-FE-16** deferral; revisit with the anti-automation hardening pass.
4. **G3 / reports** — Phase-5 work; keep the placeholders.
5. **Backend hygiene (not FE):** prune the `Jadwa`-template `UserManagement` role endpoints; consider whether `Validate-Token` (G4) is still needed.

**Net:** no blocking Phase-1/2 FE gaps. The wave is functionally complete for parents + students; G5 is the single feature worth scheduling, pending a scope decision.
