# Pipeline Brief — P6-02 Validate AI safety with an eval set

## Summary & traceability
- **Task (one line):** Curate an AI-safety eval set (age-appropriateness + hallucination spot-checks across the 4 subjects × ar/en), run the Safety Layer against it with recorded pass/fail thresholds, triage failures (safety-critical blocks launch), and make the harness re-runnable for future prompt/model changes.
- **User story (source of truth):** `user-stories/Phase-6-Stabilization/P6-02-ai-safety-eval-set.md`
- **FR-ID:** FR-AI-4 (mandatory Safety Layer). Validates Q1.3.
- **BRD goal:** G2 (trust & safety — child-safe AI). Epic: QA & Stabilization (Phase 6, Week 9). SP 5.
- **Unblocks:** the **last unbuilt P7-11 facet** — `GetEvalResultsQuery` / `GET /api/Admin/AiSafety/evals` (see §E). `AdminAiSafetyController` literally states *"Eval results endpoint is omitted (deferred — blocked on P6-02)."*
- **No FE in this story.** Backend/QA + AI only. (The P7-11 admin FE that renders eval results is a separate story.)

## Business context & value
- **Who benefits:** parents (trust child-safety is proven, not best-effort — the story's "as a parent" framing); admins (P7-11 dashboard surfaces eval pass/fail + threshold breach); the platform (a launch gate + a regression artifact that catches safety drift on prompt/model changes).
- **Value:** converts the Safety Layer from "implemented" to "evidenced". The eval set is the regression net every future prompt/model swap runs against. Per `docs/dev/HANDOFF.md` this is a standing launch-gate item ("Eval harness must pass with live keys on ar+en before P3-04 integration. LAUNCH-GATE requirement").
- **Success measure:** a committed eval set with ar/en × 4-subject coverage; a CI-runnable harness that asserts pass/fail thresholds; a recorded run result the P7-11 eval endpoint can read; safety-critical failures triaged (any safe-sample-blocked or unsafe-sample-passed on the deterministic path fails the build).

## Pre-existing groundwork (do NOT rebuild — this is a v1→v2 expansion, not greenfield)
P3-02-BE-9 already shipped a **v0 harness**. The job here is to *fix its core gap and expand it*, not start over:
- **Project exists & is in the solution:** `backend/tests/Ai.EvalTests/Ai.EvalTests.csproj` (registered in `backend/Learnexia.Modular.sln`, project GUID `C0000006-…`). References the real Ai.Application + Ai.Infrastructure + Shared.Contracts + Shared.Resources.
- **Harness exists:** `backend/tests/Ai.EvalTests/SafetyEvalHarnessTests.cs` — xUnit `[Theory]` + `[MemberData]` driven from JSON, every test tagged `[Trait("Category","Eval")]` so the default CI run filters them out.
- **Eval set exists:** `backend/tests/Ai.EvalTests/Data/safety-eval-set.json` — 20 curated samples (ar+en, safe/toxic/age/hallucination) with `{id, language, check, content, expectedOutcome, description}`.
- **THE GAP (this is the crux to fix):** the v0 harness wires a `MakeNoKeyGateway()` mock that **always returns `AiResult.Fail(Unavailable)`**. Because the two LLM-backed checks fail-closed to `Block` on gateway failure, the **safe samples for ToxicityCheck / AgeAppropriatenessCheck can never pass in CI** — they're tagged Eval/CI-excluded precisely because they only validate against *live keys*. That makes the harness non-CI-runnable as a true pass/fail gate (see §A/§B).

## A) Are the checks LLM-backed or deterministic? (THE pivotal answer)
**Mixed — and this is the load-bearing fact for the whole design:**

| Check | Implementation | LLM-backed? | Cite |
|---|---|---|---|
| **ToxicityCheck** | cheap-tier **LLM-as-judge via `IAiGateway.CompleteAsync`** (sentinel-wrapped prompt → parses `{"toxic":…}` JSON) | **YES** — needs the AI gateway/provider | `Ai.Infrastructure/Safety/ToxicityCheck.cs` |
| **AgeAppropriatenessCheck** | cheap-tier **LLM-as-judge via `IAiGateway.CompleteAsync`** (parses `{"inappropriate":…}` JSON) | **YES** — needs the AI gateway/provider | `Ai.Infrastructure/Safety/AgeAppropriatenessCheck.cs` |
| **HallucinationCheck** | **deterministic heuristics** — keyword/cue lists (ar+en uncertainty cues, fabricated-claim markers), threshold of 2 cues, optional RAG token-overlap. *"No external calls are made… no API keys are needed."* | **NO** — pure, offline, deterministic | `Ai.Infrastructure/Safety/HallucinationCheck.cs` |

- Composition entry point: `Ai.Application/Safety/SafetyLayer.cs` (`ISafetyLayer.GenerateSafeAsync`) — the 8-step facade: input toxicity screen → `IAiGateway.CompleteAsync` → run enabled checks concurrently (`Task.WhenAll`) → block / bounded-regen (`SafetyOptions.MaxRegenerationAttempts`) / allow; writes `SafetyEvent` (reason codes only, PII-light) + fail-soft publishes `AiOutputFlaggedIntegrationEvent`. **Fail-closed on every error path.**
- Verdict model: `Ai.Domain/Safety/CheckVerdict.cs` (`CheckOutcome` Pass/Block/NeedsRegeneration + stable `ReasonCodes` in `Ai.Domain/Safety/ReasonCodes.cs`).
- **Severity mapping nuance the eval MUST respect:** Toxicity high→Block, medium/low→NeedsRegeneration; Age clear→Block, borderline→NeedsRegeneration; Hallucination strong-marker→NeedsRegeneration(!), ≥2 uncertainty cues→NeedsRegeneration, low RAG overlap→NeedsRegeneration. So "flagged-as-unsafe" legitimately spans **both** Block and NeedsRegeneration — the v0 harness already relaxes Toxicity-Block samples to accept either; v2 must apply the same logic per check and not assert a single outcome where two are valid.

## B) Offline-capable v1 plan / is there a live-keys blocker?
**Recommendation: an OFFLINE, CI-runnable v1 is both achievable and the right call.** There is a real keys blocker for *live* validation, but it does NOT block a deterministic eval.

- **The blocker (real but scoped):** Toxicity + Age call `IAiGateway`, whose provider keys are **devops-gated / deferred** (per memory: AI keys/TEI/RAG flip-to-live deferred; HANDOFF "flip-to-live = devops"). A harness that calls real providers can't run in CI and isn't deterministic.
- **The resolution — fake the gateway, don't call a model.** `IAiGateway` is a clean `Shared.Contracts` seam (`Shared.Contracts/Ai/IAiGateway.cs`) and the checks take it via constructor. v1 supplies a **deterministic fake `IAiGateway`** that returns a **canned judge JSON per eval case** (the exact `{"toxic":…}` / `{"inappropriate":…}` shape the parsers expect). This exercises the **real check parsing/mapping logic + real fail-closed paths** without a model or a key. This is the existing test pattern — `SafetyLayerTests` already mocks `IAiGateway` with Moq to return controlled `AiResult`s (`tests/Modules.Ai.UnitTests/SafetyLayerTests.cs`). No new pattern; mirrors rule 8.
  - **Two ways to drive the fake (pick one — flag for lead/planner):**
    1. **Per-case canned verdict (recommended):** each LLM-backed eval case carries an `expectedJudgeVerdict` (e.g. `toxic:true/severity:high`) that the fake returns. The harness then asserts the **real check + SafetyLayer maps that verdict to the expected `CheckOutcome`**. Validates our parsing/mapping/fail-closed logic deterministically — which is the part *we* own and can regress. (It does NOT validate the model's judgment — that's the live-only run.)
    2. **Keyword fake judge:** the fake inspects the content for the same toxic/age markers and synthesizes a verdict. More "realistic" but reintroduces nondeterminism risk and duplicates heuristic logic — **not recommended.**
- **Hallucination needs no fake at all** — it's deterministic; the harness calls `new HallucinationCheck(logger)` directly (v0 already does). These cases are the truest offline coverage.
- **Keep the live path as an opt-in tier.** Retain a `[Trait("Category","EvalLive")]` (or keep the existing `Eval` tag) variant that uses a *real* gateway when keys are present, for the launch-gate Arabic-quality validation (Gate B, `docs/briefs/ai-eval-gate.md`). CI runs the offline tier (`Category=EvalOffline`); devops runs the live tier before prod. **Document both filters.**
- **Net:** v1 = deterministic, CI-runnable, no keys; live validation = a documented opt-in tier gated by devops. This satisfies AC4 (re-runnable) honestly without waiting on the keys flip.

## C) Harness form + where results persist
**Two coupled decisions — recommend (a) for the harness, plus a thin persistence seam for (E):**

**Harness form → (a) xUnit eval-suite in the existing `Ai.EvalTests` project.** Re-run = run the tests; thresholds = assertions; CI-native; zero new infra; matches AC4 ("re-runnable") and rule 8 (mirror existing shapes). Do NOT build an admin endpoint/job that runs the eval at runtime — that would require live keys in prod and couples a QA artifact to the request path.

**Where eval RESULTS live (needed by P7-11) — this is the one genuinely-open design choice.** The harness asserting in CI proves correctness, but P7-11-BE-3 needs a **queryable run record** (`{runId, passRate, failRate, threshold, breached, ranAt}` per the P7-11 FE contract). There is **no existing eval-result entity or table** (confirmed: only `SafetyEvents`, `AiResponseCache`, `AiUsageLogs` migrations exist). Three options, in recommended order:

1. **Recommended for v1 — a committed results artifact (JSON) + a CI publish step, NO DB table yet.** The harness writes a structured run summary (per-check pass/fail counts, total pass rate, threshold, breached flag, ranAt, git SHA) to a committed/known path (e.g. `backend/tests/Ai.EvalTests/Results/latest-eval-run.json` or a CI artifact). P7-11-BE-3 reads "latest eval run" from this artifact via a small read-seam. **Pros:** no migration, no runtime coupling, dovetails with "harness is the regression artifact" framing. **Cons:** P7-11 trend-across-runs needs a history of these files (or a small ingest).
2. **A new `ai.SafetyEvalRuns` table + a CLI/admin ingest.** The harness (or a small ingest command run in CI) appends a row per run; P7-11-BE-3 queries it for the trend. **Pros:** clean trend-across-runs, queryable. **Cons:** needs a **db-migration** (additive, `ai` schema, no cross-module FK) + an ingest path; more surface for a QA artifact.
3. **Reuse `SafetyEvent`.** Reject — `SafetyEvent` is per-block runtime telemetry, PII-light, append-only; eval runs are a different shape (aggregate per-run pass-rate vs per-event reason codes). Overloading it muddies both P7-09 and P7-11.

**Recommendation:** ship v1 with **option 1 (artifact + read-seam)** to unblock P7-11-BE-3's "latest run + breach indicator" without a migration; **flag option 2 to the lead** as the clean answer if P7-11 needs real *trend-across-runs* history (the P7-11 story does ask for "trend across runs"). The planner should put this as a **decision gate before coding** — it determines whether a `db-migration` batch is needed (see §F). **Cross-module rule:** P7-11 must read eval results via a `Shared.Contracts` seam (e.g. an `IAiSafetyEvalResultsQuery` in `Shared.Contracts/Ai/`, implemented in `Ai.Infrastructure` — exactly like `IPlatformAiSafetyStatsQuery` / `IAiSafetyDashboardService`), **never** a cross-module reference or cross-schema join.

## D) Eval-set storage + format + coverage
- **Storage/format — keep the existing shape, expand it.** `backend/tests/Ai.EvalTests/Data/safety-eval-set.json`, copied to output (`CopyToOutputDirectory=PreserveNewest`, already configured), deserialized to the `EvalSample` record. **Add two fields** to each case for AC + P7-11 traceability: `subject` (Math/Science/Arabic/English) and, for LLM-backed cases, `expectedJudgeVerdict` (the canned verdict the fake gateway returns — see §B). Keep `{id, language, check, content, expectedOutcome, description}`.
- **Coverage shape (representative, not hundreds).** Current v0 = 20 cases, ar/en × 3 checks, but **subject-blind** (photosynthesis/Pythagoras only — no Arabic/English-subject or per-subject spread). The story AC requires **"across subjects and both languages."** Target a curated matrix:
  - **4 subjects × 2 languages (ar/en) × {age-appropriateness, hallucination}** as the AC-mandated core, plus the existing toxicity cases retained. Suggested ~32–48 cases: for each subject (Math, Science, Arabic, English) and each language, at least one safe + one age-inappropriate (age check) and one safe + one hallucination-suspect (hallucination check); keep the toxicity safe/toxic ar/en cases.
  - Each case = `{id, subject, language, check, content, expectedOutcome (+ expectedJudgeVerdict for LLM checks), description}`.
  - **Arabic-quality emphasis (launch-gate risk):** ensure Arabic toxic/age/hallucination samples are genuinely Arabic (not transliterated), because Arabic moderation quality is the documented weak spot (P3-02-BE notes + HANDOFF Gate B). The offline tier validates our *mapping*; the live tier validates the *model's Arabic judgment*.
- **No new module, no DB for the eval SET itself** — it's a committed test data file (rule: ask-before-new-modules respected; this is test data, not a module).

## E) The P7-11 facet this unblocks + how it's fed
- **The exact "last facet":** **P7-11-BE-3 — `GetEvalResultsQuery` → `GET /api/Admin/AiSafety/evals`**, returning `EvalResultsDto { runs[]: { runId, passRate, failRate, threshold, breached, ranAt } }` (per `tasks/Backend/Phase-7-Admin-Console/P7-11-BE.md` §"Contract for Frontend" and the P7-11 story AC: *"surfaces the latest AI-safety eval results: pass/fail rate per run … trend across runs and a clear indicator when a run breaches the safety threshold"*). Confirmed deferred by the live code comment in `Ai.Api/Controllers/AdminAiSafetyController.cs` ("Eval results endpoint is omitted (deferred — blocked on P6-02)") and by HANDOFF ("P6-02 eval for the last P7-11 facet").
- **How it's fed:** P6-02 produces the **run result record** (§C). P7-11-BE-3 (a *separate* story/batch — NOT built in P6-02) will read it via a `Shared.Contracts` seam mirroring `IPlatformAiSafetyStatsQuery` (`Ai.Infrastructure` implements; the admin controller/handler injects from `Shared.Contracts`). **P6-02's job is to (1) produce eval results in a stable, readable form and (2) define/ship the read-seam contract so P7-11-BE-3 is a thin consumer.** Scope question for the lead: does P6-02 also *build* the P7-11-BE-3 endpoint, or only produce the result + seam? (See open questions.)
- **DI/registration precedent for the seam:** `Ai.Infrastructure/DependencyInjection.cs` already registers `IAiSafetyDashboardService` and `IPlatformAiSafetyStatsQuery` as scoped — a new `IAiSafetyEvalResultsQuery` registers the same way.

## Acceptance criteria (testable)
1. **Eval set covers age-appropriateness + hallucination across the 4 subjects (Math, Science, Arabic, English) and both languages (ar, en).** Verified by: the JSON contains ≥1 safe + ≥1 unsafe case per (subject × language × {age, hallucination}); a structural test asserts coverage (counts per subject/language/check are non-zero).
2. **The Safety Layer / checks run against every eval case with recorded pass/fail vs threshold, OFFLINE in CI (no live keys).** Verified by: the xUnit eval suite runs in CI (offline tier), each case asserts its expected `CheckOutcome` (respecting the Block-or-NeedsRegeneration severity nuance per check), and a run summary records pass/fail counts + pass rate + threshold + breached flag.
3. **Pass/fail threshold is defined (config-driven) and a breach is a hard failure.** Verified by: a configurable threshold (e.g. `EvalOptions.MinPassRate`, default 1.0 for the deterministic tier); a run below threshold fails the build; the run record carries `threshold` + `breached`.
4. **Failures are triaged; safety-critical failures block launch.** Verified by: any **safe sample blocked** (false-positive over-block) or **unsafe sample passed** (false-negative leak — the dangerous one) on the deterministic tier fails the suite; a short triage note documents known false-negatives/false-positives and their disposition.
5. **Re-runnable for future prompt/model changes.** Verified by: re-running `dotnet test` re-executes the harness; the live tier (`--filter Category=<live>`) is documented for devops to validate Arabic/model quality with real keys before prod.
6. **Eval results are persisted/exposed in a form the P7-11 eval facet can read** (per §C/§E decision), via a `Shared.Contracts` seam — no cross-module reference, no cross-schema join.

## Affected modules & data (new vs existing)
- **Module:** `Ai` only (plus the `Ai.EvalTests` test project). No new product module.
- **Existing, reused (no change):** `SafetyLayer`, `ToxicityCheck`, `AgeAppropriatenessCheck`, `HallucinationCheck`, `CheckVerdict`/`CheckOutcome`/`ReasonCodes`, `IAiGateway`, `SafetyEvent`, `SafetyOptions`, `Ai.EvalTests` project + JSON + harness.
- **New (this story):**
  - Expanded `safety-eval-set.json` (+ `subject`, `expectedJudgeVerdict`) — test data, no DB.
  - A deterministic **fake `IAiGateway`** in `Ai.EvalTests` returning canned judge JSON per case (mirrors `SafetyLayerTests` Moq pattern).
  - A rewritten/expanded `SafetyEvalHarnessTests` (offline tier asserts; live tier opt-in) + a coverage/structural test + a run-summary writer.
  - **Eval-result persistence/seam (§C decision):** either a committed results artifact + `IAiSafetyEvalResultsQuery` read-seam (option 1, no migration) **or** a new `ai.SafetyEvalRuns` entity + migration + ingest (option 2 — **only if** the lead wants trend history). **Flag to planner before coding.**
- **New entities/fields IF option 2 chosen:** `SafetyEvalRun { Id, RunAtUtc, PassRate, FailRate, ThresholdPct, Breached, GitSha?, TotalCases, PassedCases }` in `ai` schema, append-only, plain ints, no cross-module FK; additive migration.

## Handoff → db-migration
- **Default (option 1): NO migration.** Results live as a committed/CI artifact read via a contract seam.
- **Only if lead picks option 2 (trend history in DB):** additive migration `AddSafetyEvalRunsTable` in the `ai` schema — `SafetyEvalRun` entity above, append-only, no cross-module FK, no raw content. Index `RunAtUtc`. Apply manually per CONVENTIONS §13 before integration tests (mirrors the `AddSafetyEventsTable` precedent). **Do not build this batch unless the lead confirms option 2.**

## Handoff → backend-feature
- **Most of P6-02 is a test/QA artifact, not a feature handler.** The only "backend-feature" surface is the **eval-result read-seam** (and possibly its persistence):
  - Define `IAiSafetyEvalResultsQuery` in `Shared.Contracts/Ai/` returning a `SafetyEvalRunResult` record (`{ RunId, RanAtUtc, PassRate, FailRate, ThresholdPct, Breached }` + a list for trend), mirroring `IPlatformAiSafetyStatsQuery` exactly (sentinel-safe: empty → zeroed/empty, never null/throw).
  - Implement it in `Ai.Infrastructure` (reads the artifact or `SafetyEvalRuns` per §C), register Scoped in `Ai.Infrastructure/DependencyInjection.cs` next to `IPlatformAiSafetyStatsQuery`.
  - Use `ILoggerManager` (rule 5), not `ILogger<T>`. No new design pattern (rule 8) — mirror the existing query-adapter shape; if anything novel is needed, stop and ask.
  - **Scope flag:** whether P6-02 also builds the P7-11-BE-3 endpoint/handler or leaves it for the P7-11 story is a lead decision (see open questions). If in-scope, the handler is a thin `IQuery` reading the seam + `[Authorize(AdminOnly)]` on the existing `AdminAiSafetyController` (uncomment the omitted endpoint).
- **`EvalOptions`** (threshold config): a small options/settings entry for `MinPassRate` (default 1.0 deterministic tier). Could live as test config in `Ai.EvalTests` (preferred — keeps QA config out of the app) rather than `SafetyOptions`. Flag.

## Handoff → frontend
- **None.** No FE surface in P6-02. The P7-11 admin dashboard that renders eval results is a separate story (`tasks/Frontend/admin-dashboard/Phase-7-Admin-Console/P7-11-FE.md`).

## Open questions / assumptions / risks
**Open questions for the lead (resolve before planner finalizes):**
1. **Eval-result persistence (§C):** option 1 (committed/CI artifact + read-seam, **no migration**) vs option 2 (new `ai.SafetyEvalRuns` table + ingest, **needs migration**). P7-11's "trend across runs" leans toward option 2; v1-speed leans option 1. **This decides whether a db-migration batch exists.**
2. **Scope boundary:** does P6-02 also build the P7-11-BE-3 endpoint (`GET /api/Admin/AiSafety/evals`) + its read-seam, or only *produce eval results + the seam contract* and leave the endpoint to the P7-11 story? (Brief assumes P6-02 ships the result + seam; endpoint optional.)
3. **Fake-gateway strategy (§B):** confirm option 1 (per-case canned verdict — recommended) over option 2 (keyword fake judge).
4. **Live tier:** keep a key-gated `EvalLive` tier in this story for the Arabic launch-gate, or defer the live run entirely to devops? (Recommend: keep it as an opt-in trait, documented; CI runs offline only.)
5. **Threshold:** deterministic tier MinPassRate = 1.0 (any deviation is a real bug in our mapping)? And a separate, looser threshold for the live tier (model judgment is fuzzier)?

**Assumptions:**
- The v0 `Ai.EvalTests` project + JSON + harness are the baseline to expand, not replace wholesale (HANDOFF + P3-02-BE-9 confirm).
- "4 subjects" = Math, Science, Arabic, English (product decision; no Social Studies).
- The offline deterministic tier is the AC4/CI artifact; live validation is a devops launch-gate, not a CI blocker.

**Risks (biggest first):**
- **BIGGEST RISK — the offline eval validates OUR mapping, not the MODEL'S judgment.** Faking `IAiGateway` means the deterministic CI tier proves the checks parse/map a verdict correctly and fail closed — it does **not** prove the real model correctly classifies toxic/age-inappropriate Arabic content. The genuine child-safety assurance (especially weak Arabic moderation) still depends on the **live-keys tier**, which is devops-gated and may not run before launch. **Mitigation:** make this distinction explicit in the brief/triage doc, keep the live tier first-class + documented, and treat "live ar+en eval passed" as a separate, named launch gate (Gate B) the lead/devops must sign off — do not let "CI green" be mistaken for "AI safety proven."
- **Severity-mapping false expectations:** asserting a single `CheckOutcome` where Block *and* NeedsRegeneration are both valid (per §A) will produce flaky/incorrect failures. Mitigation: per-check accepted-outcome sets (v0 already does this for toxicity-block).
- **Coverage theater:** a "4-subject" set that's really generic facts relabeled by subject. Mitigation: genuinely subject-specific content (esp. Arabic-language Arabic-subject and English-subject samples) + a structural coverage assertion.
- **Child-safety sensitivity:** this is defensive-security work touching child safety + AI → **security-auditor is mandatory** (no fail-open in the eval logic, no raw unsafe content leaking into committed artifacts/results, no PII in run records).

## Recommended pipeline order (first cut — planner finalizes)
1. **Decision gate (lead):** resolve open questions 1–5 (esp. persistence option → migration y/n, and scope boundary). *No code before this.*
2. **db-migration** — *only if option 2* (new `ai.SafetyEvalRuns` table). Else skip.
3. **backend-feature** — eval-set expansion (JSON + `subject`/`expectedJudgeVerdict`), deterministic fake `IAiGateway`, rewritten offline harness + coverage test + run-summary writer + `EvalOptions` threshold; `IAiSafetyEvalResultsQuery` seam + impl + DI; (optionally) the P7-11-BE-3 endpoint if in scope. *(Mostly a test-project + thin-seam batch; mirror existing shapes — rule 8.)*
4. **api-tester / test-run gate** — run the offline eval suite (`Category=EvalOffline`) as the gate; if the P7-11-BE-3 endpoint is in scope, integration-test `GET /api/Admin/AiSafety/evals` (admin-only → 401/403). The eval suite *is* the primary gate here, not a live API.
5. **security-auditor (MANDATORY)** — child-safety + AI: assert no fail-open in eval logic, no unsafe content / PII in committed results or run records, fake-gateway can't mask a real fail-open path.
6. **reviewer** — gate against ACs 1–6 + CONVENTIONS; confirm the offline-vs-live distinction is documented and the P7-11 read-seam is contract-only (no cross-module ref).
7. **committer** — `feat/P6-02-ai-safety-eval-set`, PR, update `docs/dev/HANDOFF.md` (flip the standing "P6-02 eval for the last P7-11 facet" item; record the persistence decision + live-tier gate).
