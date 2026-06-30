# P3-13a (backend) — Behavioral profile depth (new derivations) — BACKLOG

> Story: [../../../user-stories/Phase-4-AI-Tutor/P3-13a-behavioral-profile-depth.md](../../../user-stories/Phase-4-AI-Tutor/P3-13a-behavioral-profile-depth.md)
> Brief: [../../../docs/briefs/recommendations-as-his-level-enrichment.md](../../../docs/briefs/recommendations-as-his-level-enrichment.md) (OQ-1)
> Phase 4 · Module: **Learning** (`StudentProfileEngine` / `DerivedProfile`).

## Status: BUILT (2026-06-30) — backend-feature implementation complete; pending reviewer + security-auditor.

## What was built
Two new `DerivedProfile` dimensions added to `StudentProfileEngine` (P3-13a):

### 1. GritScore (persistence / grit proxy)
- **Formula:** `0.5 * RetryAfterWrongRate + 0.5 * (1.0 − overallHintRate)`, clamped [0,1], rounded 4 d.p.
- **RetryAfterWrongRate** = fraction of skills with any wrong answer where the student ALSO had at least one correct answer (across any attempt). Derived from `StudentAnswer.IsCorrect` grouped by `QuizQuestion.SkillId`.
- **overallHintRate** = `totalHints / TotalAnswers` from existing `HintAnswerCountByType` signal.
- **Guard:** returns null when `TotalAnswers < options.MinSampleForGrit` (default 5).
- **Cold-start:** null when `TotalAnswers < ColdStartDataPointThreshold`.

### 2. MasteryVelocity (mastery trajectory / rate-of-improvement)
- **Formula:** `avgAccuracy(recentWindow) − avgAccuracy(olderWindow)`, clamped [-1,1], rounded 4 d.p.
- **Window:** `TrajectoryRecentWindowFraction` (default 0.4) of the attempt list from each end; ordered by `AttemptId` (chronological proxy).
- **Guard:** returns null when `AttemptAccuraciesChronological.Count < options.MinAttemptsForTrajectory` (default 3).
- **Cold-start:** null when `TotalAnswers < ColdStartDataPointThreshold`.

## Dimensions DEFERRED and why

| Dimension | Reason for deferral |
|---|---|
| **Time-of-day signal** | Requires richer session-boundary events from **P5-03** (analytics event capture). Current `AttentionSpanMinutes` is the v1 proxy. Left as-is. |
| **Motivation style** | Would require a NEW cross-module event producer from the Gamification module (streak/badge engagement). No existing `Shared.Contracts` seam exists for this signal. Violates module isolation if added now. |

## Files changed
- `Domain/Services/StudentSignals.cs` — +2 fields: `RetryAfterWrongRate`, `AttemptAccuraciesChronological`
- `Domain/Services/DerivedProfile.cs` — +2 fields: `GritScore`, `MasteryVelocity`
- `Domain/Services/StudentProfileOptions.cs` — +3 thresholds: `MinSampleForGrit`, `MinAttemptsForTrajectory`, `TrajectoryRecentWindowFraction`
- `Domain/Services/StudentProfileEngine.cs` — +2 private derivation methods; `Derive()` updated
- `Domain/Entities/StudentLearningProfile.cs` — +2 columns: `GritScore`, `MasteryVelocity`
- `Infrastructure/Persistence/Configurations/StudentLearningProfileConfig.cs` — +2 column configs
- `Infrastructure/Repository/LearningRepository.cs` — `GetStudentAnswerSignalsAsync` +2 new signals (Steps 7-8)
- `Infrastructure/Service/StudentProfileService.cs` — maps new dims to/from entity in `RecomputeProfile` + `GetProfile`
- `Application/Features/Profile/Dtos/StudentLearningProfileDto.cs` — +2 DTO properties
- `Application/Features/Profile/Queries/GetStudentProfile/GetStudentProfileQueryHandler.cs` — maps new dims to DTO
- `tests/Modules.Learning.UnitTests/StudentProfileEngineTests.cs` — +13 new test cases (P1–P13)
- `tests/Modules.Learning.UnitTests/RecommendationEngineTests.cs` — updated 6 `DerivedProfile` constructions to include new params

## Migration needed
YES — `StudentLearningProfile` entity gained 2 new nullable `double precision` columns (`GritScore`, `MasteryVelocity`). Schema: `learning.StudentLearningProfiles`. Hand to **db-migration** agent.

## Notes
- Mandatory security-auditor review (child behavioral data).
