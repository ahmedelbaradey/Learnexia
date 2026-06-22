# P5-02 — Weak-Area Detection — Backend Test Cases

> Story: [P5-02 Detect and rank weak areas](../../../user-stories/Phase-5-Parent-Analytics/P5-02-weak-area-detection.md)
> Task: [P5-02-BE](../../../tasks/Backend/Phase-5-Parent-Analytics/P5-02-BE.md)
> Surface under test: **DETECTION ACCURACY end-to-end** — seed real answer/mastery signals → `WeakAreaDetectorService` (via the `IStudentAllSubjectsWeakAreasQuery` / `IStudentWeakAreasQuery` seams as wired in real DI) returns the right weak skills with the right severity bands and ranking. The Parent READ endpoint `GET /Parent/Children/{id}/WeakAreas` is **already covered** by `P5_08_ParentReadApi_IntegrationTests` (E5-HAPPY, E5-IDOR).
> Target agent: **api-tester**
> File type: integration tests in `backend/tests/Learnexia.IntegrationTests/` (new file `P5_02_WeakAreaDetection_Tests.cs`), Testcontainers Postgres, `[Collection("IntegrationTests")]`.

## Scope and de-duplication (read before implementing)

`WeakAreaDetectorServiceTests` (Learning.UnitTests, EF InMemory) **already covers** the algorithm thoroughly: empty/first-week, High (<30%), Medium (30–50%), Low (≥50% + recent accuracy <60%), excluded (good accuracy), High>Medium>Low ranking, maxResults cap, tie-break by deficit, NotStarted excluded, maxResults=0 unlimited, SubjectCode propagation, SuggestedNextAction non-empty. **Do NOT re-implement those as integration tests.**

The detector logic is **best covered by unit tests** (pure ranking/threshold logic over a DbContext, no HTTP/auth involved) — this is already done. The **gap** these integration cases close is *detection accuracy against the real PostgreSQL provider and the real DI-wired seams end-to-end*, where the InMemory provider's LINQ translation can differ from Npgsql (group-by, `OrderByDescending(...).FirstOrDefault()`, navigation joins `Skill → Concept → Subject`). Keep the integration set **small and targeted** — accuracy/severity/ranking sanity over Postgres, not a re-run of every unit case.

IDOR on the parent-facing endpoint is already covered (P5-08 E5-IDOR) — **noted here for traceability, not re-implemented**.

## Seeding notes (binding for the implementer)

- Severity is derived from `StudentSkillMastery.MasteryPercentage` + recent `Attempt.AccuracyPercentage` joined via `StudentAnswer → QuizQuestion.SkillId`. Seed the Learning graph (`Grade → Subject → Concept → Skill → StudentSkillMastery`, plus `Attempt`/`QuizQuestion`/`StudentAnswer` for the Low tier) directly in `LearningDbContext` within a DI scope, mirroring `WeakAreaDetectorServiceTests` seed helpers but against the real container.
- Resolve `IStudentAllSubjectsWeakAreasQuery` (the all-subjects seam) from DI for the detection assertions — this exercises the real `StudentAllSubjectsWeakAreasQueryAdapter → WeakAreaDetectorService` chain that the unit test bypasses.
- `WeakAreaEntry` shape: `SkillId, SkillName, SubjectCode (int 0=MATH/1=SCIENCE/2=ARABIC/3=ENGLISH), MasteryPercent, Severity (Low=1/Medium=2/High=3), SuggestedNextAction`.

---

## Test cases

### WA-INT-01 — Mixed-severity child: detector identifies the right weak skills with correct severity bands (over Postgres)
- **Type:** functional / boundary (severity thresholds)
- **Priority:** P0
- **Traces to:** AC1 (weak areas from mastery <50% + recent accuracy); AC2 (each weak area carries a severity)
- **Preconditions / seed:** One student S. Seed:
  - Skill A — mastery 20% (NeedsReview) → expect **High**.
  - Skill B — mastery 40% (NeedsReview) → expect **Medium**.
  - Skill C — mastery 55% (InProgress) + most-recent completed attempt accuracy 50% on a question with `SkillId == C` → expect **Low**.
  - Skill D — mastery 85% (Mastered) + most-recent attempt accuracy 90% → expect **excluded**.
  - Skill E — NotStarted → expect **excluded**.
- **Steps:**
  1. Resolve `IStudentAllSubjectsWeakAreasQuery`; call `GetWeakAreasAsync(S, maxResults: 0)`.
- **Expected result:** Result contains exactly A (High), B (Medium), C (Low). D and E absent. Each entry's `Severity` matches the expected band and `MasteryPercent` matches the seeded value. Confirms threshold mapping and the `StudentAnswer → Attempt.AccuracyPercentage` join translate correctly under Npgsql.

### WA-INT-02 — Ranking order High → Medium → Low, then by mastery deficit descending
- **Type:** functional (ranking)
- **Priority:** P0
- **Traces to:** AC2 (severity indicator drives ordering); P5-02-BE-1 ranking rule
- **Preconditions / seed:** Student S with: two High skills (mastery 10% and 25%), one Medium (40%), one Low (60% + recent accuracy 40%).
- **Steps:**
  1. `GetWeakAreasAsync(S, maxResults: 10)`.
- **Expected result:** Order is `[High(10%), High(25%), Medium(40%), Low]`. Within High, the 10% skill (deficit 90) precedes the 25% skill (deficit 75). Confirms `OrderByDescending(severity).ThenByDescending(deficit)` is stable over Postgres.

### WA-INT-03 — Child with strong skills only → empty list (not error)
- **Type:** negative / empty-state
- **Priority:** P0
- **Traces to:** AC4 (resolved/improved areas drop off automatically); P5-02 empty-state safety
- **Preconditions / seed:** Student S with all skills mastered ≥80% and recent accuracy ≥60% (or all `NotStarted`).
- **Steps:**
  1. `GetWeakAreasAsync(S, maxResults: 5)`.
- **Expected result:** Empty list — **never null, never throws**. Confirms the sentinel contract end-to-end.

### WA-INT-04 — Resolved area drops off after mastery rises above threshold
- **Type:** functional / regression (auto-drop-off)
- **Priority:** P1
- **Traces to:** AC4 (resolved/improved areas drop off the list automatically)
- **Preconditions / seed:** Student S with Skill A at mastery 25% (High weak area).
- **Steps:**
  1. `GetWeakAreasAsync(S)` → assert A present as High.
  2. Update `StudentSkillMastery` for A to mastery 85% (Mastered) and seed a recent attempt with accuracy ≥60% on A.
  3. `GetWeakAreasAsync(S)` again.
- **Expected result:** First call includes A (High); second call **excludes** A. Confirms the list is recomputed from current mastery each call (no stale persistence) — the explicit AC4 behavior the unit tests imply but do not sequence.

### WA-INT-05 — maxResults cap enforced over the real query
- **Type:** boundary
- **Priority:** P1
- **Traces to:** P5-02-BE-1 (maxResults), P5-01 consumes `maxResults: 5`
- **Preconditions / seed:** Student S with ≥7 High-severity weak skills.
- **Steps:**
  1. `GetWeakAreasAsync(S, maxResults: 5)`.
- **Expected result:** Exactly 5 entries returned, all the highest-deficit High skills (the cap is applied **after** ranking, not before). Confirms `Take(maxResults)` ordering semantics over Postgres.

### WA-INT-06 — Cross-subject detection: weak skills span multiple of the 4 subjects with correct SubjectCode
- **Type:** functional / persistence (join correctness)
- **Priority:** P1
- **Traces to:** AC3 (weak areas surface in report + dashboard, across subjects); product override (4 subjects, no Social Studies)
- **Preconditions / seed:** Student S with weak skills in MATH (code 0), SCIENCE (1), and ENGLISH (3) — never seed a Social-Studies subject (does not exist).
- **Steps:**
  1. `GetWeakAreasAsync(S, maxResults: 0)`.
- **Expected result:** Entries carry the correct `SubjectCode` ints (0/1/3 present). No entry carries any code outside `{0,1,2,3}`. Confirms the `Skill → Concept → Subject.SubjectCode` navigation join projects correctly under Npgsql and respects the 4-subject product rule.

### WA-INT-07 — Parent endpoint surfaces detected weak areas for own child (smoke; IDOR already covered)
- **Type:** functional / auth-authz (smoke)
- **Priority:** P2
- **Traces to:** AC3 (weak areas surface to the parent); P5-08 AC (parent-scoped read)
- **Preconditions / seed:** Parent P + own child C seeded with ≥1 known weak skill.
- **Steps:**
  1. `GET /api/Parent/Children/{C}/WeakAreas` with parent JWT.
- **Expected result:** 200 + `Successed == true`; `data.areas` is a non-empty array carrying the seeded weak skill with its severity.
- **De-dup note:** P5-08 E5-HAPPY asserts the envelope/array shape against a *fresh* (empty) child. This case differs only by seeding a child with **actual** weak areas to assert the detector output actually flows through the endpoint. If considered redundant with E5-HAPPY once E5 is re-run against seeded data, the api-tester may fold it into E5-HAPPY and mark WA-INT-07 as covered-by-E5.

---

## Priority summary
- **P0:** WA-INT-01, WA-INT-02, WA-INT-03
- **P1:** WA-INT-04, WA-INT-05, WA-INT-06
- **P2:** WA-INT-07

Total new integration cases: **7**. The 12 `WeakAreaDetectorServiceTests` unit cases remain the primary algorithm coverage and are NOT duplicated.

## Better-as-unit-test note
The threshold/ranking/tie-break logic is pure and already lives in fast InMemory unit tests — that is the correct home for exhaustive band/edge coverage. The integration cases above deliberately cover only what the InMemory provider cannot guarantee: Npgsql LINQ translation of the group-by + `OrderByDescending(...).FirstOrDefault()` recent-accuracy lookup and the multi-hop navigation join, plus the real DI-wired seam chain and the auto-drop-off sequence.
