# Execution Plan — Recommendation Engine + Lexi Narration (P5-09 + P3-14)

> Backend-only wave. **Two stacked PRs.** PR1 = deterministic engine (P5-09), PR2 = Lexi narration (P3-14, stacked on PR1).
> Branch: `feat/P5-09-recommendation-engine`, **stacked on `feat/P5-parent-read-api` (PR #173, unmerged)** — P5-09's parent endpoint extends the `ParentAnalyticsController` that #173 created.
> Lead decisions locked 2026-06-18 (do not re-litigate): engine first / Lexi fast-follow; Lexi cost = 5 (`ai_cost.recommendation`), charge-per-delivery; `HelperIntent.Recommendation` rule-#8 approved; engine deterministic + FREE; Lexi narrates persisted content only; no new module; FE is the other lead.

## Source
- Brief: `E:\Wrokspace\Learnexia\docs\briefs\recommendations-engine.md`
- Stories: `E:\Wrokspace\Learnexia\user-stories\Phase-5-Parent-Analytics\P5-09-recommendation-engine.md`, `E:\Wrokspace\Learnexia\user-stories\Phase-4-AI-Tutor\P3-14-lexi-recommendation-narration.md`
- Tasks: `E:\Wrokspace\Learnexia\tasks\Backend\Phase-5-Parent-Analytics\P5-09-BE.md`, `E:\Wrokspace\Learnexia\tasks\Backend\Phase-4-AI-Tutor\P3-14-BE.md`
- Verified in code: `LearningModule.cs` (job registration), `StudentProfileRecomputeJob.cs` (job template), `HelperIntent.cs`, `TemplateSelector.cs`, `CreditCostResolver.cs`, `GlobalSettingKeys.cs`, `GlobalSettingsSeeder.cs`, `CreditReasonCode.cs`, `Shared.Contracts/Identity/*` (no grade-read query exists).

## Task inventory

### PR1 — P5-09 deterministic engine (Learning + Parent)
| ID | Stack/agent | Summary | Est | Depends-on |
|---|---|---|---|---|
| P5-09-BE-1 | db-migration | `StudentRecommendation` entity (schema `learning`) + EF config + migration. Cols: `Id`, `StudentId int` (indexed), `RecommendationDate date`, `ItemsJson jsonb` (`RecommendationItem[]`), `GeneratedAtUtc timestamptz`, `FullAuditedEntity` audit cols. **UNIQUE index `(StudentId, RecommendationDate)`**. `dotnet ef migrations add` (Designer + snapshot). Mirror `StudentLearningProfileConfig` + `20260613180132_AddStudentLearningProfileTable`. | 3h | — |
| P5-09-BE-2 | backend-feature | `RecommendationItem` model + pure Domain `RecommendationEngine` (no I/O). Inputs: `WeakAreaEntry[]` + per-skill `AdaptivityDecision` + `DerivedProfile` + grade. Output ranked `RecommendationItem[]` cap 3–5: `{SkillId, SubjectCode, TitleKey, BodyKey, CtaKey, Severity, ActionType, TargetDifficulty}`. Deterministic ranking (severity → mastery deficit); i18n KEYS only. Mirror `AdaptivityEngine`/`StudentProfileEngine` shape. | 6h | P5-02, P3-08 (built) |
| P5-09-BE-3 | backend-feature | `IRecommendationService` (Application) + `RecommendationService` (Infrastructure, Option C — EF only here). Resolves child grade (**via the new Identity grade seam — see BE-3a**), runs the engine in-module (`IWeakAreaDetectorService` + `IAdaptivityService` + `IStudentProfileService`), **upserts** the day's row (idempotent per `(child, date)`). Cold-start/empty → well-formed encouraging set. DI in `Learning.Infrastructure/DependencyInjection.cs`. | 5h | BE-1, BE-2, BE-3a |
| **P5-09-BE-3a** | backend-feature | **NEW (surfaced — not in task file): minimal grade-read seam** `Shared.Contracts/Identity/IChildGradeQuery.GetGradeAsync(int studentId, ct) → int?` + Identity-module impl (sentinel: never throws, null if unknown). Required because the daily job has **no JWT/HttpContext** to read grade from a claim. **Shared-file batch — touches the Identity module.** | 2h | — |
| P5-09-BE-4 | backend-feature | `RecommendationRecomputeJob` (Hangfire). Mirror `StudentProfileRecomputeJob` **exactly**: `[DisableConcurrentExecution(300)]`, `_scopeFactory.CreateAsyncScope()` per child, `SaveChangesAsync(userId:0)`, `Cron.Daily()`, fixed ID `"Rec-Recompute"`, `TimeZone=Utc`, fail-soft per child. Register in `LearningModule.InitializeAsync` **after the `SP-Recompute` AddOrUpdate** (line 74) — recommendations read the P3-13 profile. Sweep active children (mirror P3-13's stale/active-sweep). | 3h | BE-3 |
| P5-09-BE-5 | backend-feature | `IStudentRecommendationsQuery` seam (`Shared.Contracts/Learning`) → `IReadOnlyList<RecommendationItem>`; loose `int studentId`; **never null / never throws** (sentinel, mirror `StudentAllSubjectsWeakAreasQueryAdapter`). Reads the latest persisted row. Learning.Infrastructure adapter + DI. Consumed by Parent endpoint (BE-6) AND P3-14 Lexi grounding. **`RecommendationItem` record lives here in `Shared.Contracts`** (shared by BE-2 engine output + the seam — single definition). | 3h | BE-1 |
| P5-09-BE-6 | backend-feature | Parent read endpoint `GET /Parent/Children/{id}/Recommendations` on `ParentAnalyticsController` (the #173 controller). IDOR via `IParentChildQuery.IsParentOfChildAsync` **before** the `IStudentRecommendationsQuery` fan-out; `BaseResponse<RecommendationsDto>` via `NewResult`; EF-free handler; no-data → well-formed empty. Mirror E5 (`GetChildWeakAreas`). | 3h | BE-5 |
| P5-09-BE-7 | api-tester + security-auditor | Endpoint status/envelope/auth + **cross-family IDOR**; engine determinism + cap; job idempotency per day; empty/cold-start state. | 5h | BE-1..6 |

### PR2 — P3-14 Lexi narration (Ai + Billing), stacked on PR1
| ID | Stack/agent | Summary | Est | Depends-on |
|---|---|---|---|---|
| P3-14-BE-1 | backend-feature | Energy cost key + reason. Add `ai_cost.recommendation = 5` to `GlobalSettingKeys` const + **`ManagedKeys` allowlist** + `GlobalSettingsSeeder` defaults; add `CreditReasonCode.AiRecommendation` (next free in the 10–19 AI range, e.g. `=14`). **Billing-module shared files.** | 2h | — |
| P3-14-BE-2 | backend-feature | `HelperIntent.Recommendation = 5` (Shared.Contracts) + exhaustive `TemplateSelector` case + **all 4 subject template classes** (`Math/Science/Arabic/English`) get the Recommendation branch + a kid-style, grade-tuned, grounded prompt template. Mirror the const + default in `CreditCostResolver.ResolveCost` + `ResolveReasonCode`. **Shared/exhaustive-enum files — serialize against any other Ai work.** | 4h | — |
| P3-14-BE-3 | backend-feature | Recommendation narration command + validator + handler. Mirror `ExplainConceptCommandHandler` exactly: rate-limit → `IChildAccessStateQuery` gate → `ICreditSpendService` pre-auth → cache-first → `ISafetyLayer` (NEVER `IAiGateway`) → `TryDebitAsync` on delivery → events. Inject `IStudentRecommendationsQuery` (PR1 seam) for grounding — **never recompute/invent**. childId from JWT only (validator), never body. Cache key includes the recommendation date/content hash (OQ-5). | 6h | P5-09-BE-5, BE-1, BE-2 |
| P3-14-BE-4 | backend-feature | Endpoint — "Ask Lexi recommendation" route mirroring the Explain SSE controller action (auth, child scoping) in `Ai.Api`. | 2h | BE-3 |
| P3-14-BE-5 | api-tester + security-auditor | Pre-auth/debit (charge-per-delivery incl. cache hit; no-delivery=no-debit); insufficient-energy + paused/locked child blocked; safety-block path; **grounding (no hallucinated skills beyond the persisted set)**; IDOR/child-scope; EN+AR. | 5h | BE-1..4 |

## Dependency order

**PR1 critical path:** `BE-1 (migration)` → `BE-2 (engine)` + `BE-3a (grade seam)` → `BE-3 (service)` → `BE-4 (job)`; `BE-5 (seam)` parallel after `BE-1`; `BE-6 (endpoint)` after `BE-5`; `BE-7 (api-tester + security-auditor)` after `BE-1..6`.

**PR2 critical path (after PR1 merges/stacks):** `BE-1 (Billing config)` ‖ `BE-2 (intent + templates)` → `BE-3 (handler, needs PR1's BE-5 seam)` → `BE-4 (endpoint)` → `BE-5 (api-tester + security-auditor)`.

Hard ordering rules honored: **schema before features** (BE-1 first); **`RecommendationItem` defined once in `Shared.Contracts`** before both the engine output (BE-2) and the seam (BE-5) consume it; **job registers after `SP-Recompute`** (reads the profile); **no UoW** — the job commits explicitly per child (ADR-0001); Lexi handler depends on PR1's persisted seam (BE-5).

## Execution batches

### PR1 — `feat/P5-09-recommendation-engine` (stacked on PR #173)
- **Batch 1 (sequential): db-migration** → `P5-09-BE-1`. Schema must land before any feature code. Verify the unique index `(StudentId, RecommendationDate)` and jsonb column; Designer + snapshot regenerated; `MigrateAsync` idempotent.
- **Batch 2 (parallel): backend-feature** →
  - 2a: `P5-09-BE-2` (pure engine + `RecommendationItem` model — note: the **record type** is authored in `Shared.Contracts` here so BE-5 reuses it)
  - 2b: `P5-09-BE-3a` (Identity grade-read seam — **shared file, Identity module**; independent of the engine)
  These two are independent (different files/projects) → run together.
- **Batch 3 (sequential, after Batch 2): backend-feature** → `P5-09-BE-3` (service composes engine + grade seam + in-module inputs + upsert), then `P5-09-BE-4` (daily job + `LearningModule` registration after `SP-Recompute`). Same module, dependent → sequential.
- **Batch 4 (parallel with Batch 3 once Batch 1 is done): backend-feature** → `P5-09-BE-5` (`IStudentRecommendationsQuery` seam + adapter + DI). Depends only on BE-1 (the persisted row shape), independent of the job → can run alongside Batch 3.
- **Batch 5 (sequential, after Batch 4): backend-feature** → `P5-09-BE-6` (Parent endpoint on the #173 `ParentAnalyticsController`, behind IDOR guard).
- **Batch 6 (sequential, after Batches 3+5): api-tester** → `P5-09-BE-7` API portion (endpoint status/envelope/422/auth/IDOR; determinism; job idempotency; empty-state) against WebApplicationFactory + Testcontainers Postgres.
- **Batch 7 (sequential, after Batch 6): security-auditor [MANDATORY]** → child-data exposure + **IDOR on the parent endpoint** + data-minimisation/purpose-limitation on the persisted behavioral-derived recommendations + the determinism invariant (no LLM in the engine core). Critical/High block.
- **Gate: reviewer** → PR1 batch against acceptance criteria + CONVENTIONS + api-tester + security-auditor results.
- **committer** (only after reviewer PASS) → commit on `feat/P5-09-recommendation-engine`, push, open PR **with base `feat/P5-parent-read-api`** (stacked PR1, not `main`).

### PR2 — Lexi narration (stacked on PR1's branch)
- **Batch 8 (parallel): backend-feature** →
  - 8a: `P3-14-BE-1` (Billing cost key + reason code — **Billing shared files**: `GlobalSettingKeys` + `ManagedKeys` + `GlobalSettingsSeeder` + `CreditReasonCode`)
  - 8b: `P3-14-BE-2` (`HelperIntent.Recommendation` + `TemplateSelector` + 4 subject templates + `CreditCostResolver` mirror — **Ai shared/exhaustive-enum files**)
  Independent modules → run together. **Both touch shared files — serialize against any *other* concurrent Ai/Billing pipeline** (none in this wave).
- **Batch 9 (sequential, after Batch 8): backend-feature** → `P3-14-BE-3` (narration command/validator/handler reusing the Explain orchestration + `IStudentRecommendationsQuery` grounding), then `P3-14-BE-4` (SSE endpoint). Dependent → sequential.
- **Batch 10 (sequential, after Batch 9): api-tester** → `P3-14-BE-5` API portion (pre-auth/debit charge-per-delivery incl. cache hit; no-delivery=no-debit; insufficient-energy/paused/locked blocked; safety-block; grounding; IDOR/child-scope; EN+AR).
- **Batch 11 (sequential, after Batch 10): security-auditor [MANDATORY]** → **AI-prompt safety** (PII-minimised prompt: grade/age proxies only, never name/email), child-data, **energy/money correctness** (charge-on-delivery, no double-debit, no charge on refuse/safety-block/error), and the **grounding invariant** (narrates only persisted items — no hallucinated skills). Critical/High block.
- **Gate: reviewer** → PR2 batch.
- **committer** (only after reviewer PASS) → commit on the Lexi branch, push, open PR **stacked on PR1's branch**.

## Review gates
- **PR1:** reviewer after Batch 7. **security-auditor MANDATORY** (Batch 7) — child data + IDOR + data-minimisation + determinism invariant. api-tester (Batch 6) feeds the gate.
- **PR2:** reviewer after Batch 11. **security-auditor MANDATORY** (Batch 11) — AI-prompt + child-data + energy/money + grounding invariant. api-tester (Batch 10) feeds the gate.
- Both PRs: reviewer checks CONVENTIONS (`BaseResponse`/`Successed`, `ILoggerManager`, no UoW / explicit job commit, no cross-module FK, Option-C — no EF in Application, response envelope, `[Authorize]` on the parent route) and that **no new design pattern** beyond the established Engine/Service/Job shapes (PR1) and the lead-approved 5th intent (PR2) was introduced.

## Invariants the reviewer + security-auditor must explicitly verify
- **Determinism (PR1):** the engine core is rule-based with NO LLM call; same inputs ⇒ same output. A live LLM in the core is a blocking violation (burns energy, non-deterministic).
- **Grounding (PR2):** Lexi narrates ONLY the persisted `RecommendationItem[]` from `IStudentRecommendationsQuery`; it never recomputes or invents recommendations/skills.
- **Three un-conflated signals:** Grade (Identity) → scope/tone; mastery% weak areas + AdaptivityEngine → which areas + difficulty; gamification level → motivational framing only (never which area).
- **Energy model (PR2):** charge-per-delivery, cache-hit charges the same, no charge on refuse/safety-block/error; cost server-resolved + client-blind.
- **Child privacy (both):** per-child only, no cross-child leak; loose `int studentId`, no cross-module FK; IDOR guard before fan-out; no PII in the Lexi prompt body.

## Blockers / prerequisites
1. **Stacked-branch hygiene (PR #173 unmerged).** PR1 extends the `ParentAnalyticsController` created by `feat/P5-parent-read-api` (PR #173). Branch `feat/P5-09-recommendation-engine` MUST be cut from `feat/P5-parent-read-api`, and **PR1 must target base `feat/P5-parent-read-api`** (not `main`). If #173 merges first, rebase PR1 onto `main`. The committer must set the PR base explicitly. **Recommendation:** proceed stacked per the lead's locked decision; flag to the lead that PR1 cannot merge to `main` until #173 merges.
2. **Grade-read seam is NOT present (confirmed in code).** No `IChildGradeQuery`/grade-read query exists in `Shared.Contracts/Identity` — only `ChildGradeChangedIntegrationEvent` (an event, not a query) and `IChildAccountService.GetChildrenAsync` (roster, not single-student-by-id). The daily job has no JWT, so **BE-3a (new grade seam) is a required prerequisite for BE-3**, and it **touches the Identity module** (a shared boundary). Surfaced as a new task (`P5-09-BE-3a`) — confirm the lead accepts adding this minimal Identity seam (recommended) vs reading grade via the existing `IChildLearningProfileQuery`/`ChildLearningProfile.Grade` Ai bridge (couples Learning's job to an Ai-facing seam — not recommended).
3. **Engine inputs confirmed in-module (no seam needed).** `IAdaptivityService`, `IStudentProfileService`, `IWeakAreaDetectorService` all live in `Learning.Application` — the engine/service calls them in-module. **No `Shared.Contracts` seam** for these (only the grade seam in BE-3a and the outbound `IStudentRecommendationsQuery` in BE-5 cross a module boundary). This resolves the brief/task open question.
4. **`RecommendationItem` single-definition decision.** Define the `RecommendationItem` record in `Shared.Contracts/Learning` (BE-5) and have the engine (BE-2) output that same type — avoid a duplicate Domain copy + a mapper. Author BE-2 to depend on the `Shared.Contracts` record (so it must be created at/by BE-2 time, finalized by BE-5). Confirm with the lead; alternative is a Domain record + mapping to a contract record (more code, no benefit here).
5. **Cache activation for the Lexi narration (OQ-5/OQ-7).** `AiResponseCache` serving is currently dormant (operationalize-phase-4). PR2's narration follows whatever the existing intents do; the cache key MUST include the recommendation date/content hash so a new day yields a fresh narration. Not a blocker — accept live-each-time for v1 if cache serving stays dormant; flag in reviewer.
6. **Daily-job scope/timing (OQ-6).** `Rec-Recompute` registered after `SP-Recompute`, `Cron.Daily()`, UTC. Confirm sweep scope (all active children vs active-in-last-N-days). Default: mirror P3-13's sweep. Not blocking; note for BE-4.

## Definition of done

### Per batch (PR1)
- **Batch 1 (migration):** `StudentRecommendation` table in schema `learning` with the unique `(StudentId, RecommendationDate)` index + jsonb `ItemsJson`; Designer + snapshot regenerated; migration applies cleanly (idempotent `MigrateAsync`); builds.
- **Batch 2 (engine + grade seam):** pure `RecommendationEngine` produces a ranked, capped (3–5), deterministic `RecommendationItem[]` from the four inputs (unit-testable, no I/O); `IChildGradeQuery` seam returns a sentinel-safe grade; builds + unit tests green.
- **Batch 3 (service + job):** `RecommendationService` upserts idempotently per `(child, date)`, cold-start safe; `RecommendationRecomputeJob` registered after `SP-Recompute`, `[DisableConcurrentExecution]`, per-child scope + commit; builds.
- **Batch 4 (seam):** `IStudentRecommendationsQuery` returns the persisted set, never null/throws; DI wired; builds.
- **Batch 5 (endpoint):** `GET /Parent/Children/{id}/Recommendations` returns `BaseResponse<RecommendationsDto>`, IDOR-guarded, empty-safe; builds.
- **Batch 6 (api-tester):** integration tests green — 200 + envelope, 401/403 unauth, cross-family IDOR blocked, empty-state well-formed, determinism asserted.
- **Batch 7 (security-auditor):** no Critical/High; child-data/IDOR/determinism invariants confirmed.

### Per batch (PR2)
- **Batch 8 (config + intent):** `ai_cost.recommendation=5` seeded + in `ManagedKeys`; `CreditReasonCode.AiRecommendation` added; `HelperIntent.Recommendation` + exhaustive `TemplateSelector` + 4 subject templates + `CreditCostResolver` mirror compile (exhaustiveness satisfied); builds.
- **Batch 9 (handler + endpoint):** narration reuses the Explain orchestration, grounds on the seam, debits per delivery; SSE endpoint auth + child-scoped; builds.
- **Batch 10 (api-tester):** integration tests green — charge-per-delivery incl. cache hit, no-delivery=no-debit, insufficient-energy/paused/locked blocked, safety-block, grounding (no skills beyond persisted), IDOR.
- **Batch 11 (security-auditor):** no Critical/High; AI-prompt/PII, energy/money, grounding invariants confirmed.

### Overall (tied to story acceptance criteria)
- **P5-09:** parent dashboard reads a real, persisted, deterministic 3–5 item recommendation set per child (no stubs); recomputed daily after the profile job; IDOR-safe; cold-start friendly; reproducible; no LLM in the core; `IStudentRecommendationsQuery` seam live for both Parent and Ai consumers.
- **P3-14:** `HelperIntent.Recommendation` narrates the persisted set kid-style (EN+AR), grade-tuned, grounded-only, costing 5 energy charge-per-delivery via the existing Explain orchestration; blocked/no-debit paths correct; safety-passed; security-audited.

Plan ready — dispatch Batch 1.
