# P2-09 Home Dashboard — Backend Test Cases (`api-tester` implements)

> Target agent: **`api-tester`**
> Endpoint under test: **`GET /api/Learning/Dashboard`** `[Authorize]`
> Envelope: `BaseResponse<DashboardDto>` (success flag spelled **`Successed`** → JSON key `successed`, camelCase).
> Existing test file: `backend/tests/Learnexia.IntegrationTests/P2_09_HomeDashboard_Tests.cs` (11 cases, C01–C11). **Extend it** — add the new IDs below; do NOT rewrite existing passing cases unless this catalog marks one as a CONTRACT-DRIFT correction.

## Source-of-truth contract (read before implementing)

The **real** `DashboardDto` on `main` has evolved past the Phase-2 brief. It now has **13 positional fields** (`backend/src/Modules/Learning/Learnexia.Modules.Learning.Application/Features/Dashboard/Dtos/DashboardDto.cs`):

| # | Field | Type | Phase-2 brief said | Current reality (assert THIS) |
|---|---|---|---|---|
| 1 | `xp` | `int` | always 0 | **real XP** via `IStudentXpQuery`; 0 only for brand-new |
| 2 | `streak` | `int` | always 0 | **real streak** via `IStudentStreakQuery`; 0 only for brand-new |
| 3 | `leaguePreview` | `LeaguePreviewDto?` | always null | **wired** (P4-07); `null` only when `GroupSize == 0` (brand-new) |
| 4 | `continue` | `ContinueTargetDto?` | next Available lesson | unchanged — see below; `ContinueTargetDto` gained an `isBoss` bool |
| 5 | `level` | `int = 1` | (absent) | computed from XP; default 1 |
| 6 | `hearts` | `int = 5` | (absent) | real hearts via `IStudentHeartsQuery`; default 5 (Cap) |
| 7 | `inPracticeMode` | `bool = false` | (absent) | derived `Hearts == 0` |
| 8 | `badgesCount` | `int = 0` | (absent) | real count via `IStudentBadgesQuery` |
| 9 | `recentBadges` | `IReadOnlyList<BadgeSummary>?` | (absent) | top-3; `null` when none |
| 10 | `dailyMissions` | `IReadOnlyList<MissionSummary>?` | (was stub `dailyMission`, always null) | **wired** (P4-06); `null` when empty list |
| 11 | `weeklyMission` | `MissionSummary?` | (absent) | top weekly; `null` when none |
| 12 | `freezeBalance` | `int = 0` | (absent) | streak-freeze inventory (P4-11) |
| 13 | `activeTimedEvents` | `IReadOnlyList<ActiveTimedEventDto>?` | (absent) | active event banners; `null` when none |

`ContinueTargetDto` (`…/Dtos/ContinueTargetDto.cs`): `subjectId, subjectName, unitId, unitName, lessonId, lessonName, skillId?, skillName?, nodeState, isBoss`.

**Self-scoping / IDOR:** `studentId` is resolved exclusively from the JWT (`_currentUser.UserId`). There is **no** `studentId` route/query/body parameter. IDOR is structurally impossible — but we assert it actively (BE-TC-12) by appending a `studentId` query param and proving it is ignored.

**Learning-language guard (P8-03):** the continue-target subject set is resolved per `SubjectCode` via `SubjectLanguageResolver` using the JWT `learning_language` claim. ARABIC→Ar (pinned), ENGLISH→En (pinned), MATH/SCIENCE→learner language. `GetDashboardQueryHandler.cs:103,118-153`.

### Seed / auth harness (already in the existing test file — reuse verbatim)
- `CreateStudentViaParentFlowAsync(tag, learningLanguage="en")` → Register-Parent → Add-Child (Student role) → Sign-In → `(studentToken, studentId)`. **Parameterize `LearningLanguage`** so language cases can pass `"ar"`.
- `SeedCompletedAttemptAsync(studentId, lessonId)` — seeds a `Completed` `Attempt` directly.
- `GetFirstLessonInSubjectAsync(subjectId)` — first lesson by `SequenceOrder` then `Id`.
- `LearningSeeder.SeedAsync` in `InitializeAsync` — bilingual Grade-1 trees (Math/Ar, Math/En, Science/Ar, Science/En, Arabic/Ar, English/En).
- `TryProp(...)` case-insensitive JSON property lookup; `successed` / envelope helpers.

---

## Group A — Auth & self-scoping (P0)

### BE-TC-01 — Anonymous request → 401
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** none (no JWT).
- **Steps:**
  1. `GET /api/Learning/Dashboard` with no `Authorization` header.
- **Expected:** HTTP `401 Unauthorized`. Body need not be the success envelope.
- **Traces to:** Q7 auth (`[Authorize]`); story "as a student".
- **Status:** EXISTS (C01) — keep.

### BE-TC-02 — Malformed / expired bearer token → 401
- **Type:** auth-authz / negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:**
  1. `GET /api/Learning/Dashboard` with `Authorization: Bearer not-a-real-jwt`.
- **Expected:** HTTP `401`. Handler body is never reached (middleware rejects).
- **Traces to:** Q7 auth; security-auditor focus (a) 401 consistency.
- **Status:** NEW.

### BE-TC-03 — Authenticated student → 200 with success envelope
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** student via parent flow.
- **Steps:**
  1. Sign in as a fresh student; `GET /api/Learning/Dashboard` with the Student JWT.
- **Expected:** HTTP `200`; envelope has keys `successed` (== `true`), `statusCode`, `message`, `data`, `errors`. JSON literally contains `"successed":` (camelCase, lowercase `d`).
- **Traces to:** AC1; CONVENTIONS §2 envelope / rule 2 `Successed` spelling.
- **Status:** EXISTS (C06) — keep.

### BE-TC-12 — IDOR: a passed `studentId` query param is ignored (caller gets OWN data)
- **Type:** auth-authz / negative (IDOR) · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** two students A and B via parent flow. Seed a `Completed` Science attempt for **A only** (so A's most-recent-activity subject = Science, B's = Math fallback).
- **Steps:**
  1. As **B**, call `GET /api/Learning/Dashboard?studentId={A.studentId}` (deliberately inject A's id).
  2. Also call `GET /api/Learning/Dashboard?studentId=-1` and `?studentId=0` (garbage ids) as B.
- **Expected:** all three calls return **B's** dashboard — `continue.subjectId == Math G1 (B's fallback)`, NOT Science (A's). The query param has no effect (no binding exists). HTTP `200` each.
- **Traces to:** Q1/Q7 self-scoping; security-auditor focus (c) no cross-student leakage; controller header comment "IDOR is structurally impossible".
- **Status:** NEW — actively proves the structural claim the existing file only asserts "by inspection".

---

## Group B — Full `DashboardDto` shape (the current 13-field contract) (P0)

### BE-TC-04 — All 13 top-level keys are present in `data`
- **Type:** functional / regression · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** fresh student.
- **Steps:**
  1. `GET /Dashboard`; read `data`.
  2. Assert presence of every key: `xp`, `streak`, `leaguePreview`, `continue`, `level`, `hearts`, `inPracticeMode`, `badgesCount`, `recentBadges`, `dailyMissions`, `weeklyMission`, `freezeBalance`, `activeTimedEvents`.
- **Expected:** all 13 keys present (null-valued keys are serialized, not omitted). Catches accidental field drops / api-client contract drift.
- **Traces to:** AC1 dashboard shape (XP, streak, mission, continue, league + the live gamification fields).
- **Status:** NEW (existing C-tests only check a subset). **Supersedes the implicit shape coverage.**

### BE-TC-05 — Fresh student zero/default state is well-formed
- **Type:** state (empty) / boundary · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** brand-new student (no attempts, no XP profile).
- **Steps:**
  1. `GET /Dashboard`.
- **Expected:** `200` and:
  - `xp == 0`, `streak == 0`, `level == 1`, `freezeBalance == 0`, `badgesCount == 0`.
  - `hearts == 5`, `inPracticeMode == false`.
  - `leaguePreview` is JSON `null` (brand-new → `GroupSize == 0` → null).
  - `dailyMissions` is JSON `null` OR an array (sentinel maps empty → null).
  - `recentBadges` is JSON `null` OR array; `weeklyMission` JSON `null` OR object.
  - `activeTimedEvents` is JSON `null` OR array.
  - `continue` is **non-null** (Grade-1 Math fallback finds an Available lesson).
- **Traces to:** Q8 empty state (200, never 404/500); brief "fresh student → level 1".
- **Status:** EXISTS partially (C02/C04/C05) — **extend** to assert `level`, `hearts`, `inPracticeMode`, `freezeBalance`, `badgesCount` literally (the existing cases assert only `xp`/`streak`/`dailyMissions`/`leaguePreview`).

### BE-TC-06 — `hearts`/`inPracticeMode` default sentinel for a never-played student
- **Type:** functional / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** fresh student (never lost a heart).
- **Steps:**
  1. `GET /Dashboard`.
- **Expected:** `hearts == 5` (Cap sentinel), `inPracticeMode == false`. (Asserts the `IStudentHeartsQuery` never-null sentinel surfaces correctly through the Learning module.)
- **Traces to:** P4-04 contract surfaced on dashboard; brief "level/hearts/inPracticeMode".
- **Status:** NEW.

### BE-TC-07 — `leaguePreview` shape when populated
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** a student whose `IStudentLeagueQuery` snapshot has `GroupSize > 0`. **May require** earning XP first (lazy league instantiation triggers on first dashboard read once an XP profile exists) or a Gamification seed helper. If no clean fixture exists, mark **BLOCKED** with the reason and document the manual-verify path.
- **Steps:**
  1. Bring the student into a league cohort (`GroupSize > 0`).
  2. `GET /Dashboard`.
- **Expected:** `leaguePreview` is a non-null object with keys `tierName` (string, one of Bronze/Silver/Gold/Diamond), `rank` (int ≥ 1), `totalPlayers` (int ≥ 1), `xpThisWeek` (int ≥ 0).
- **Traces to:** AC1 league preview; P4-07.
- **Status:** NEW — likely BLOCKED on fixture; record blocker in `execution-report.md` rather than dropping.

---

## Group C — Continue target (next unlocked lesson) (P0)

### BE-TC-08 — Continue target shape & `nodeState == Available`
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** fresh student.
- **Steps:**
  1. `GET /Dashboard`; read `continue`.
- **Expected:** `continue` non-null with `subjectId>0`, `subjectName` non-empty, `unitId>0`, `unitName` non-empty, `lessonId>0`, `lessonName` non-empty, `nodeState == 1` (Available; accept string `"Available"` too), `isBoss == false` (fresh student lands on `SequenceOrder==1`). `skillId`/`skillName` may be null.
- **Traces to:** AC2 continue → next unlocked lesson.
- **Status:** EXISTS (C03) — keep.

### BE-TC-09 — Most-recent-activity drives the continue subject (Math)
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** student with one `Completed` attempt on the first Math G1 lesson.
- **Steps:**
  1. Seed Completed Math attempt; `GET /Dashboard`.
- **Expected:** `continue.subjectId == Math G1 id`; `continue.lessonId != completedLessonId` (a completed lesson is not Available).
- **Traces to:** AC2; Q3 Option A steps 1-4.
- **Status:** EXISTS (C07) — keep.

### BE-TC-10 — Most-recent-activity drives the continue subject (Science)
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** student with one `Completed` Science G1 attempt.
- **Steps:**
  1. Seed Completed Science attempt; `GET /Dashboard`.
- **Expected:** `continue.subjectId == Science G1 id` (most-recent-activity selects Science over the Math fallback).
- **Traces to:** AC2; Q3 Option A.
- **Status:** EXISTS (C08) — keep.

### BE-TC-11 — Cross-subject fallback when the active subject is exhausted
- **Type:** functional / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** student whose **entire** Math G1 tree is `Completed` (seed a Completed attempt for every Math G1 lesson) so no Available lesson remains in Math.
- **Steps:**
  1. Seed Completed attempts covering all Math G1 lessons.
  2. `GET /Dashboard`.
- **Expected:** `continue` is non-null and `continue.subjectId != Math G1 id` — fallback iterates SCIENCE→ARABIC→ENGLISH (deterministic `FallbackSubjectCodeOrder`) and returns the first Available lesson found. If seeding every Math lesson is too costly, mark **BLOCKED**/ManualVerify with the reason.
- **Traces to:** AC2; Q3 Option A step 5; handler `GetDashboardQueryHandler.cs:166-176`.
- **Status:** NEW (the existing file's C11 is a seeder smoke test, not this fallback).

### BE-TC-13 — Degenerate empty state → `continue == null`, still 200
- **Type:** state (empty) / boundary · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** a student for whom NO subject has any Available lesson (requires a custom seed with zero Available nodes across all Grade-1 subjects). Hard to construct against the real seeder.
- **Steps:**
  1. Construct/seed the degenerate state; `GET /Dashboard`.
- **Expected:** `200`; `continue` is JSON `null`; all other fields well-formed zero/default. **Never** 404/500.
- **Traces to:** Q8 empty state; brief success measure "`continue: null` for a student with no Available lessons".
- **Status:** NEW — likely **BLOCKED** (no clean fixture against the real seeder). Record the blocker; do not drop.

### BE-TC-14 — Engine consistency: continue lesson is Available in the SkillTree endpoint
- **Type:** functional / regression (cross-endpoint invariant) · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** fresh student.
- **Steps:**
  1. `GET /Dashboard`; capture `continue.subjectId` + `continue.lessonId`.
  2. `GET /api/Learning/Subjects/{continue.subjectId}/SkillTree` with the same JWT.
  3. Locate `continue.lessonId` in the skill-tree response.
- **Expected:** that lesson's `state == Available (1)` in the skill tree. Proves the dashboard and the P2-04 engine agree.
- **Traces to:** AC2; brief consistency invariant; api-tester handoff case 9.
- **Status:** NEW (recommended in brief, absent from current file).

---

## Group D — Learning-language guard on the continue-target (P1)

### BE-TC-15 — Arabic-medium student → MATH continue-target resolves to the Ar tree
- **Type:** functional / i18n-routing · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** student created with `LearningLanguage="ar"`; resolve Math/Ar Grade-1 subject id in `InitializeAsync`.
- **Steps:**
  1. `GET /Dashboard` for the Ar-medium fresh student.
- **Expected:** `continue.subjectId == Math/Ar G1 id` (NOT Math/En). `SubjectLanguageResolver.Resolve(MATH, Ar) == Ar`.
- **Traces to:** P8-03 learning-language guard; `GetDashboardQueryHandler.cs:118-153`.
- **Status:** NEW.

### BE-TC-16 — English-medium student → MATH continue-target resolves to the En tree
- **Type:** functional / i18n-routing · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** student with `LearningLanguage="en"` (default harness already uses "en").
- **Steps:**
  1. `GET /Dashboard`.
- **Expected:** `continue.subjectId == Math/En G1 id`.
- **Traces to:** P8-03 guard.
- **Status:** NEW (the existing default-en cases imply this but never assert the Ar vs En fork).

### BE-TC-17 — Pinned-language subjects ignore the learner language (ARABIC always Ar, ENGLISH always En)
- **Type:** functional / i18n-routing / boundary · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** an Ar-medium and an En-medium student; seed a Completed attempt on the ARABIC G1 first lesson for one of them so the continue-target lands on ARABIC.
- **Steps:**
  1. For the student whose recent activity is ARABIC, `GET /Dashboard`.
- **Expected:** `continue.subjectId` is the ARABIC/**Ar** subject regardless of the learner language (pinned). Symmetric check for ENGLISH→En if a fixture is cheap; otherwise document.
- **Traces to:** P8-03 pinned-subject rule; `SubjectLanguageResolver`.
- **Status:** NEW — mark ManualVerify if the targeted-subject seed is costly.

---

## Group E — Persistence / side-effect & isolation (P1)

### BE-TC-18 — Cross-student isolation: B's dashboard ignores A's activity
- **Type:** auth-authz / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** A with a Completed Science attempt; B with no attempts.
- **Steps:**
  1. `GET /Dashboard` as B.
- **Expected:** B's `continue.subjectId == Math G1` (B's fallback), NOT A's Science. No leakage across the `WHERE StudentId == studentId` join.
- **Traces to:** self-scoping; security focus (c).
- **Status:** EXISTS (C09) — keep.

### BE-TC-19 — Read-only / idempotent: two consecutive reads return the same shape
- **Type:** persistence / regression · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** fresh student.
- **Steps:**
  1. `GET /Dashboard` twice with the same JWT.
- **Expected:** identical `continue.lessonId` (or both null); `xp`/`streak`/`level` stable. No hidden writes from the read path (note: league/mission lazy-instantiation is internal to Gamification and must not change the dashboard's continue target or zero-state shape between two immediate reads for a brand-new student).
- **Traces to:** "No SaveChangesAsync" read-only invariant; CONVENTIONS rule 3.
- **Status:** EXISTS (C10) — keep.

---

## Group F — Seeder / environment smoke (P2)

### BE-TC-20 — Grade-1 bilingual trees seeded (smoke)
- **Type:** regression / smoke · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** `LearningSeeder.SeedAsync` ran.
- **Steps:**
  1. Query `Subjects` for `Grade.Number == 1`.
- **Expected:** ≥ 6 subject roots; all 4 `SubjectCode`s present (MATH/SCIENCE/ARABIC/ENGLISH); the 4 student-visible trees (Math/En, Science/En, Arabic/Ar, English/En) exist. **Must NOT** assert mock "Reading"/"Art" subjects (HANDOFF: subjects are Math/Science/Arabic/English only).
- **Traces to:** product decision "4 subjects, no Social Studies"; test precondition guard.
- **Status:** EXISTS (C11) — keep.

---

## Product-override negative assertions (fold into existing cases — no new endpoint)

- **No teacher role / no student self-register:** the only auth path to this endpoint is the parent→child onboarding Student JWT (already the harness). No additional negative endpoint exists to hit here; document in the test-file header that the dashboard is reachable ONLY by a Student-role JWT minted via parent onboarding.
- **4 subjects only:** BE-TC-20 already guards against extra/mock subjects; the continue-target fallback order (`MATH, SCIENCE, ARABIC, ENGLISH`) contains no Social Studies — assert the fallback set has exactly these 4 codes if cheaply inspectable.

---

## Implementation notes for `api-tester`
1. **Extend** `P2_09_HomeDashboard_Tests.cs`; keep C01–C11, add BE-TC-02, 04, 06, 07, 11, 12, 13, 14, 15, 16, 17 (the NEW ones). Re-id displays as you see fit but keep this catalog's IDs in a `// BE-TC-NN` comment per test for traceability.
2. **Correct the stale framing:** the existing file's header calls `xp`/`streak`/`dailyMissions`/`leaguePreview` "Phase-2 stubs that will always be 0/null". That is now only true for a **brand-new** student. Keep the brand-new assertions (still valid) but relabel the comments — do not assert "always 0" as a permanent contract.
3. **Blocked cases** (BE-TC-07 populated league, BE-TC-11 exhausted-Math fallback, BE-TC-13 degenerate empty, BE-TC-17 pinned-subject) — if no clean fixture, mark `[Trait("Category","ManualVerify")]` / skip and record the blocker in `execution-report.md`. Do NOT delete them.
4. Full Wave regression (P2_04, P2_07, P2_08, P2_11, P4_* dashboard-consuming suites) must stay green.
