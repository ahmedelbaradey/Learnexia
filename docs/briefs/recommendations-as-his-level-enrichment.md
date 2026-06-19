# Pipeline Brief — "At his level" enrichment of the recommendation engine + Lexi narration

> **Status:** read-only scoping pass. This brief proposes an enrichment of the **already-merged** P5-09
> (deterministic recommendation engine) and P3-14 (Lexi narration). It does **NOT** author user stories or
> task files — those require lead sign-off (CLAUDE.md "Work intake" / rule #9). The "Story/task breakdown"
> and "Open questions" sections below are the proposal the lead reviews before any stories are generated.

## Summary & traceability
- **One-line task:** Make the recommendation engine and Lexi's narration genuinely fit each child's *level* by feeding the **P3-13 behavioral profile** into the deterministic engine's selection/action/difficulty/quantity/ordering, and feeding **grade + gamification level + profile** into Lexi's tone/framing — while keeping the three "at his level" signals **un-conflated**.
- **Refines (not new features):**
  - **P5-09** — `user-stories/Phase-5-Parent-Analytics/P5-09-recommendation-engine.md` (engine; FR-PA-1, FR-PA-2).
  - **P3-14** — `user-stories/Phase-4-AI-Tutor/P3-14-lexi-recommendation-narration.md` (Lexi narration; FR-AI-*, FR-PA-2).
  - **P3-13** — `user-stories/Phase-4-AI-Tutor/P3-13-adaptive-student-profile.md` (the profile being consumed; FR-AD-5). P3-13 AC explicitly names P5-02 weak areas as a consumer of the profile — this enrichment realizes that promise.
- **BRD goal:** G1 (personalised, adaptive learning / "learns *how* I learn"); supports the "Adaptive Learning Profile" moat (BE2/BE7).
- **Epic / phase:** Adaptive Guidance (P5) + AI Tutor / Lexi (P4). Locked design source: `docs/briefs/recommendations-engine.md` and `MEMORY.md → recommendations-engine-feature`.

## Business context & value
- **Who benefits:** the **student** (recommendations + Lexi's voice are tuned to how they actually learn, not just to raw mastery) and the **parent** (the "Areas to focus" / "Recommendations from Lexi" cards feel personal and trustworthy).
- **Value:** P5-09 today is correct but *shallow* in personalization — it uses mastery + adaptivity well, but treats the rich P3-13 behavioral profile as a single cold/rich boolean and ignores gamification level entirely. P3-14's prompt template only tunes on **grade** even though its own AC promised motivational **gamification-level** framing. This enrichment turns the dormant P3-13 signals into live behaviour and closes the P3-14 AC gap.
- **Success measure:** profile dimensions demonstrably change recommendation **action-type / difficulty / quantity / ordering** for distinct learner shapes (low-persistence vs fast-pace) deterministically and explainably; Lexi's tone visibly shifts with grade + level + encouragement style; the 3 signals remain un-conflated; cold-start still degrades to grade + mastery only.

## The locked "at his level" model (do not violate — design rule)
Three **un-conflated** signals (`docs/briefs/recommendations-engine.md` line 32):
1. **School Grade** (Identity / JWT `Grade` claim) → Lexi **language/tone + curriculum scope**. Never picks *which* area.
2. **Per-skill mastery %** (P5-02 weak areas) + **AdaptivityEngine** (`IAdaptivityService.GetTargetDifficulty`) → **which** areas appear + **practice difficulty**.
3. **Gamification level** (`IStudentXpQuery.CurrentLevel`) → **motivational framing ONLY**. Never picks which area, never sets difficulty.

Plus the **P3-13 behavioral profile** (`DerivedProfile`) as the deeper personalization input — it refines selection/sequencing/quantity within signal #2's mastery-chosen set and shapes Lexi's encouragement style (within signal #3's framing role). The profile is **not** a fourth "level" axis; it modulates *how* the already-chosen areas are presented and paced.

---

## Q1 — What `DerivedProfile` / `IStudentProfileService` actually exposes (the raw material)
Source of truth: `backend/src/Modules/Learning/Learnexia.Modules.Learning.Domain/Services/DerivedProfile.cs` (the record) and `StudentProfileEngine.cs` (how each is derived). `IStudentProfileService.GetProfile(studentId)` returns this record; cold-start returns neutral defaults with `DataPointCount = 0`.

`DerivedProfile` is a **5-field** record (NOT a broad pace/grit/time-of-day model — see the gap note):

| # | Field | Type | Meaning | Cold-start value | Derivation (StudentProfileEngine) |
|---|-------|------|---------|------------------|------------------------------------|
| 1 | `QuestionTypeAffinity` | `IReadOnlyDictionary<string,double>` | Per-`QuestionType` success rate **above the student's overall accuracy**, min-sample-guarded (`MinSamplePerQuestionType`, default 5). Keys = `QuestionType` enum names (MCQ/TrueFalse/Matching/FillInBlank…), values [0..1]. | empty `{}` | `DeriveQuestionTypeAffinity` — type included only if total ≥ min-sample AND successRate > overall accuracy |
| 2 | `RecurringErrorSkillIds` | `IReadOnlyList<int>` | SkillIds with repeated wrong-answer patterns (`RecurringErrorThreshold`, default 3 wrong). | empty `[]` | `DeriveRecurringErrorClusters` |
| 3 | `AttentionSpanMinutes` | `int?` | Minute-bucket at which accuracy drops ≥ `AccuracyDropThreshold` (default 0.15) below session-start. **v1 proxy** from Attempt/StudentAnswer timestamps; truer signal awaits P5-03. | `null` | `DeriveAttentionSpan` (needs ≥2 buckets) |
| 4 | `PreferredExplanationStyle` | `ExplanationStyle` enum (Standard / Simplified / Visual / StepByStep) | Conservative heuristic over affinity + hint-rate. **Provisional** — flagged "treat as a hint, not a directive" pending P3-03. | `Standard` (0) | `DeriveExplanationStyle` |
| 5 | `DataPointCount` | `int` | # answer data points that fed the derivation; **the confidence indicator**. `< ColdStartDataPointThreshold` (default 10) ⇒ low confidence; `0` ⇒ cold-start. | `0` | passthrough of `StudentSignals.TotalAnswers` |

**Critical reality-check for the lead:** the prompt's "rich profile" wording (pace, persistence/grit, preferred time-of-day, motivation style, mastery trajectory) is the *aspiration* in the P3-13 story Description, **not** what the entity exposes today. The entity (`StudentLearningProfile.cs`) and `DerivedProfile` expose **only the 5 fields above**. There is **no** pace, grit/persistence, time-of-day, motivation-style, or mastery-trajectory field. Raw `StudentSignals` adds `OverallAccuracy`, `AnswersByType`, `SkillErrorCounts`, `SessionAccuracyBuckets`, `HintAnswerCountByType` — but those are pre-aggregation inputs assembled inside `StudentProfileService`, not exposed via the seam. **So "use the profile more deeply" = use these 5 dimensions; anything beyond them is a NEW P3-13 derivation (bigger scope) and must be called out as such.** This is OQ-1 below.

## Q2 — What the engine uses TODAY vs the gap
Source: `RecommendationEngine.Compute(weakAreas, adaptivityDecisions, profile, grade)` + `RecommendationService.cs` (the orchestrator that pre-fetches all four inputs).

| Signal | Fetched? | Used in the engine TODAY | Gap |
|--------|----------|--------------------------|-----|
| **Mastery / weak areas** (signal #2a) | Yes (`IWeakAreaDetectorService.DetectAsync`) | **Deeply** — drives WHICH areas + the ranking (`OrderByDescending(Severity).ThenByDescending(100 - MasteryPercent)`) | None — working as designed |
| **AdaptivityEngine difficulty** (signal #2b) | Yes (per-skill `GetTargetDifficulty`) | **Deeply** — sets `RecommendationItem.TargetDifficulty` per item | None — working as designed |
| **Grade** (signal #1) | Yes (`IChildGradeQuery.GetGradeAsync`) | **NOT used in ranking.** Engine docstring lines 21-23: *"Currently unused in ranking; stored for the Lexi narration tier (P3-14 tone)."* Param is even nullable/"not a hard dependency" | Intentional (grade = scope/tone, not selection). The real grade-scope guard is upstream in P5-02 weak-area detection. **Probably fine to leave**, but confirm (OQ-4) |
| **Behavioral profile** (P3-13) | Yes (`IStudentProfileService.GetProfile`) | **Lightly — ONE boolean only.** `ResolveActionType` reads `profile.DataPointCount == 0` to decide Low-severity → Celebrate (cold) vs Practice (rich). `QuestionTypeAffinity`, `RecurringErrorSkillIds`, `AttentionSpanMinutes`, `PreferredExplanationStyle` are **completely ignored** | **This is the primary enrichment target** |
| **Gamification level** (signal #3) | **No** — not fetched, not a param | **Not used at all.** Engine docstring line 26: *"Gamification level → NOT used here (explicitly excluded per spec)."* Correct per the un-conflation rule (level = framing only, applied by narration) | Correct by design — **must stay out of the engine.** Level enrichment belongs to Lexi (Q3), not here |

**Net:** the engine consumes the full `DerivedProfile` object but extracts a single cold/rich bit. Four of five profile dimensions are dead inputs. The enrichment is almost entirely about **making `ResolveActionType` / ranking / quantity use dimensions 1-4 deterministically**, plus a confidence gate on `DataPointCount`.

## Q3 — What Lexi's narration uses TODAY vs the gap
Sources: `RecommendationNarrationCommandHandler.cs`, `PromptContext.cs`, `PromptBuilder.cs`, the 4 subject templates' `…Recommendation` constants (e.g. `MathTemplate.cs` `EnRecommendation`/`ArRecommendation`).

- **Grade / Age / Language:** resolved from **JWT claims** (`Grade`, `Age`, `Language`) via `TryResolveProfile` (defaults grade 4, age 10, Ar). Injected into the prompt by `PromptBuilder.AppendGradeAge` → *"The student is in Grade {grade}, approximately {age} years old."* Template line: *"Adjust your tone and depth to the student's grade level."* → **grade/tone tuning works.**
- **Grounding:** built from the persisted `RecommendationItem[]` (`BuildRecommendationGrounding`) — subject + title key + action label only, **no PII, no skill invention.** Correct and must be preserved.
- **Gamification level:** **NOT used.** Not in `PromptContext` (which has only `StudentId, Intent, Subject, Grade, Age, Language, WeakAreas, Context`), not fetched by the handler, not in any template. **Gap — and it contradicts the P3-14 AC**, which says narration should use *"motivational gamification level framing."* The AC was written but never implemented; the template has no level hook.
- **Behavioral profile:** **NOT used** in the narration. The handler never calls `IStudentProfileService` (and couldn't directly — it's a different module; see Q5 seam note). No encouragement-style adaptation. Gap.

**Net for Lexi:** grade/tone = done; **gamification-level motivational framing = missing (closes an open P3-14 AC); profile-driven encouragement style = missing.**

---

## Q4 — Proposed enrichment (concrete, per signal)

### A. Engine (deterministic, FREE, explainable) — `RecommendationEngine.Compute`
Keep the contract: pure static, no I/O, deterministic, one class, no new pattern (rule #8). Feed dimensions 1-4 of `DerivedProfile`, **gated by `DataPointCount`** (confidence). The mastery signal still **chooses which** areas; the profile only modulates action-type, quantity, difficulty-nudge, and ordering *within* that chosen set.

Proposed deterministic rules (the lead picks the exact ones — OQ-2/OQ-3):
1. **Quantity (cap within 3–5) from confidence + fatigue:**
   - `DataPointCount == 0` → keep current cold-start single Celebrate item.
   - Low `AttentionSpanMinutes` (e.g. ≤ a configured threshold) → cap nearer **3** (smaller, less-overwhelming set) for fatigue-prone learners.
   - Rich profile + no fatigue signal → allow up to **5**.
2. **Action-type from profile, not just severity:**
   - `RecurringErrorSkillIds` contains the area's `SkillId` → force `Review` (deep, repeated errors) even at Medium severity — practice alone hasn't worked.
   - Otherwise keep the current severity map (High→Review, Medium→Practice, Low→Practice/Celebrate).
3. **Difficulty nudge (bounded, never overrides AdaptivityEngine's band):** keep `TargetDifficulty` from `GetTargetDifficulty` as the source of truth. Optional: for a low-persistence/fatigue-prone shape, prefer the *lower* edge of the adaptivity band on `Review` items (smaller, more-encouraging steps). **This must stay a nudge, not a recompute** — adaptivity owns difficulty (OQ-3).
4. **Ordering refinement:** keep severity → mastery-deficit primary sort; optionally surface a `RecurringError` item first within equal severity (most-stuck-and-repeating first).
5. **Explanation-style passthrough (optional):** carry `PreferredExplanationStyle` onto the persisted item (new optional field) so Lexi *and* the practice surface can honour it — **without** the engine acting on it. Flagged as optional because it widens the `RecommendationItem` contract (OQ-5).
6. **Confidence gate:** when `DataPointCount < ColdStartDataPointThreshold` (default 10) but `> 0`, apply **only** the most conservative rules (e.g. RecurringError→Review) and fall back to grade+mastery for everything else — "low confidence, adapt minimally." This is the cold-start / low-data fallback the brief requires.

Notes: `gamification level` stays **out** of this method (un-conflation). All thresholds become `RecommendationOptions` (mirroring `StudentProfileOptions`/`AdaptivityOptions`), bound from `appsettings.json`, so product can tune without a deploy. Every rule remains traceable to a profile dimension (explainability AC of P5-09).

### B. Lexi narration (prompt-template enrichment, still grounded on persisted content only)
No new energy cost — Lexi still costs the **same 5** (Practice tier). This is template + a couple of read-seam injections, not a new orchestration. The grounding stays strictly the persisted `RecommendationItem[]` (no skill invention — preserve `BuildRecommendationGrounding`).
1. **Grade → vocabulary/scope:** already done; keep `AppendGradeAge` + the template's grade line.
2. **Gamification level → motivational hooks (closes the P3-14 AC):** add `CurrentLevel` to `PromptContext`; the handler fetches it via the existing `IStudentXpQuery` seam (see Q5); `PromptBuilder` injects a short, anonymous motivational line (e.g. *"The student is Level {n} — celebrate their progress and frame the next step as levelling up."*). **Framing only** — the model must not change which areas or difficulty (template already forbids assessing level / unlocking lessons; keep those guardrails).
3. **Profile → encouragement style:** pass a *coarse, anonymous* encouragement hint derived from the profile (e.g. fatigue-prone → "keep it short and very encouraging"; `PreferredExplanationStyle` → tone of the explanation). Pass the **derived hint**, not raw signals, to keep PII-minimisation (the builder already forbids `StudentId` in prompt text). Source either the new persisted `RecommendationItem` style field (B.5 above, no cross-module call) or a profile read seam (OQ-6).
4. **Un-conflation in the prompt:** grade governs vocabulary, level governs motivational hooks, profile governs encouragement intensity/style — three distinct prompt fragments, never merged into one "level" notion.

---

## Affected modules & data
- **Learning module** (engine + orchestrator): `RecommendationEngine.cs` (enrich `Compute`/`ResolveActionType` + add quantity/ordering rules), `RecommendationService.cs` (fetch gamification level **only if** the engine needs it — it should NOT, per un-conflation; level is Lexi's), new `RecommendationOptions` (Domain, mirroring `StudentProfileOptions`). **No new entity** unless `RecommendationItem` gains an optional `PreferredExplanationStyle`/style field (B.5/OQ-5) — that is a **contract change** in `Shared.Contracts/Learning/IStudentRecommendationsQuery.cs` and a JSON-shape change in the persisted `ItemsJson` (additive, nullable, backward-compatible; the daily job rewrites rows anyway).
- **Ai module** (Lexi): `PromptContext.cs` (+`CurrentLevel`, +optional encouragement/style hint), `PromptBuilder.cs` (+motivational-line + encouragement-line helpers), all four subject templates' `Recommendation` constants (EN+AR), `RecommendationNarrationCommandHandler.cs` (fetch level via `IStudentXpQuery`; populate the new `PromptContext` fields). New i18n keys if any framing text is localized server-side.
- **Shared.Contracts:** **no new gamification seam needed for the engine** — `IStudentXpQuery` (→ `StudentXpSnapshot(StudentId, TotalXp, CurrentLevel)`) already exists and Gamification already implements it. **For Ai:** the seam exists in `Shared.Contracts` but the **Ai module has never injected it** — Ai would add a DI registration/consumption of the *existing* seam (no new contract). Only a *new* contract is needed if we choose the cross-module **profile** read for Lexi (OQ-6) rather than persisting the style hint on the item (B.5).
- **Entities new vs existing:** all **existing** (`StudentLearningProfile`, `StudentRecommendation`). No migration unless `RecommendationItem`/`ItemsJson` gains the optional style field (additive JSON — no schema migration required for a jsonb column).

## Handoff → db-migration
- **Likely none.** `ItemsJson` is a jsonb column holding `RecommendationItem[]`; adding an optional nullable field to the record is an additive serialization change, and the daily `RecommendationRecomputeJob` rewrites every active child's row, so no backfill migration is required. **Only** if the lead wants the style field promoted to a real column (not recommended) would a migration be needed. Confirm with reviewer per OQ-5.
- Grade-transition rule still holds: `StudentRecommendation` is per-(child, date); not wiped on grade change; stale-grade rows roll off next day (already the case).

## Handoff → backend-feature
- **Engine (Learning):** enrich `RecommendationEngine.Compute` private helpers (`ResolveActionType`, new `ResolveQuantity`/ordering) to consume `DerivedProfile` dimensions 1-4 with a `DataPointCount` confidence gate; add `RecommendationOptions` (Domain) + bind in DI + `appsettings.json`. Keep it pure/static/deterministic/one-class — **do not** introduce Strategy/pipeline (rule #8). Update `RecommendationEngineTests.cs` for each new rule (determinism + per-dimension behaviour + cold-start fallback). **Do not** add gamification level to the engine.
- **Lexi (Ai):** extend `PromptContext` (+`CurrentLevel`, +encouragement/style hint); inject motivational + encouragement fragments in `PromptBuilder`; update all four templates' Recommendation constants (EN+AR) with the framing guardrails intact; in `RecommendationNarrationCommandHandler` fetch `IStudentXpQuery.GetByStudentIdAsync(childId)` (default `CurrentLevel=1` on null) and populate the new context fields. **No new energy cost** — cost stays `ai_cost.recommendation = 5`, charge-per-delivery unchanged. Grounding stays persisted-only.
- **Commands/queries/endpoints:** **no new endpoints, no new commands.** Reuses the existing `GET /Parent/Children/{id}/Recommendations` (engine output shape unchanged except the optional field) and the existing Lexi recommendation SSE route.
- **Security:** child behavioral data + AI prompt → re-run `security-auditor` (child-privacy, data-minimisation, purpose-limitation; ensure only *derived, anonymous* hints reach the prompt, never raw per-skill error lists or `StudentId`).

## Handoff → frontend
- **No FE work in scope for this lead** (backend-only lead per MEMORY → `backend-only-scope`; FE is the other lead's). The engine output contract is backward-compatible (additive optional field); the Parent `RecommendationsCard.tsx`/`FocusAreasCard.tsx` and the "Ask Lexi" CTA need no change to keep working. If the lead later wants the FE to render the new style/level framing, that is a separate FE story to raise with the other lead — flag, do not build.

## Open questions / assumptions / risks (for the lead → user)
- **OQ-1 (scope-defining):** "Use the profile more deeply" today = the **5 existing `DerivedProfile` fields only**. The richer dimensions named in the prompt (pace, persistence/**grit**, preferred **time-of-day**, **motivation style**, **mastery trajectory**) **do not exist** in P3-13. Decision: (a) enrich using only the 5 existing dimensions (smaller, fits "refine P5-09 + P3-14"), or (b) **also** extend P3-13 to derive new dimensions (e.g. a persistence/grit proxy from hint-rate + retry behaviour, a time-of-day signal) — which is a **separate, larger P3-13 story** with new derivation + tests + security review. **Recommend (a) now, (b) as a follow-up story.**
- **OQ-2 (which dimensions + exact rules):** confirm the deterministic rule per dimension (the Q4.A list is a proposal). E.g. is "RecurringError → force Review" desired? Is the fatigue→smaller-set mapping desired, and what's the `AttentionSpanMinutes` threshold? Product owns the thresholds (they become `RecommendationOptions`).
- **OQ-3 (difficulty aggressiveness):** may the engine *nudge* difficulty toward the low edge of the adaptivity band for low-persistence/fatigue-prone learners, or must `TargetDifficulty` be left exactly as `GetTargetDifficulty` returns it? (Recommend: nudge only on `Review` items, bounded, never crossing the band — adaptivity stays the source of truth.) Also: max quantity swing (3 vs 5) — confirm the cap rule.
- **OQ-4 (grade in the engine):** keep grade out of engine ranking (current state, since P5-02 already scopes by grade and grade is the *tone/scope* signal), or add a grade-scope assertion in the engine? Recommend leaving as-is.
- **OQ-5 (`RecommendationItem` contract change):** OK to add an optional nullable style/explanation-style field to `RecommendationItem` (in `Shared.Contracts/Learning`) and to `ItemsJson` (additive jsonb, no migration), so Lexi and the practice surface can honour `PreferredExplanationStyle` without a cross-module profile call? Or keep the item unchanged and source the style via a seam (OQ-6)?
- **OQ-6 (how Lexi gets the profile signal):** two options — (a) **persist** a derived, anonymous style/encouragement hint on `RecommendationItem` so the Ai handler reads it from the grounding it already fetches (**no new cross-module seam, no module-isolation concern** — recommended); or (b) add a **new `Shared.Contracts` profile read seam** so Ai reads `DerivedProfile` directly (more flexible, but a new cross-module contract + a security-review surface for raw behavioral data crossing into Ai). Recommend (a).
- **OQ-7 (economy):** confirm this is **purely engine + template enrichment with NO economy change** — Lexi still costs **5**, charge-per-delivery, cache-hit-charges, no new intent. (Assumption: yes. The enrichment adds *quality* to the same call, not a new billable action.) Flag: the narration cache key (`AiCacheKeyBuilder.ForRecommendation`) already includes the recommendation content hash + grade; if level/profile now affect the *prompt*, the cache key MUST also include `CurrentLevel` (and any style hint) so a level-up yields a fresh narration — otherwise a stale cached narration is served. **This is a required correctness item, not optional.**
- **OQ-8 (gamification level seam for Ai):** confirm Ai may consume the existing `IStudentXpQuery` seam (Gamification already implements it; Learning already consumes it). This is the cleanest way to give Lexi the motivational level. No new contract; just a DI registration + handler injection in Ai. (Assumption: yes.)
- **OQ-9 (cold-start / confidence):** confirm the fallback: `DataPointCount == 0` → grade + mastery only (current behaviour preserved); `0 < DataPointCount < ColdStartDataPointThreshold` → apply only the most conservative profile rule(s), else grade + mastery. (Assumption per the locked design — confirm the exact "low-confidence" rule subset.)
- **Risk — `PreferredExplanationStyle` is provisional:** P3-13 itself flags this enum as speculative pending P3-03 ("treat as a hint, not a directive"). If the engine *acts* on it (vs. passing it to Lexi as a tone hint), that hardens a provisional signal. Recommend: use it only as a Lexi tone hint and/or a soft ordering tiebreaker, not as a hard action/difficulty driver, until P3-03 confirms the enum.
- **Risk — over-personalisation / explainability:** every new rule must stay traceable to a named dimension (P5-09 AC requires explainable, reproducible). Keep rules few and documented; resist a scoring blend that becomes a black box (and would invite a pattern → rule #8).
- **Risk — security (child data → AI prompt):** the Ai handler must inject only **derived, anonymous** hints (level number, coarse encouragement style), never raw `RecurringErrorSkillIds`/per-skill error counts or `StudentId`. Mandatory `security-auditor` on the Ai batch.

## Story/task breakdown (PROPOSAL — needs lead sign-off before authoring)
**Recommendation: enrich the existing stories, do NOT create a brand-new feature story.** This is a refinement of merged P5-09 + P3-14 behaviour, not a new capability. Two clean options for the lead:

- **Option A (recommended) — two small enrichment stories, BE-only:**
  - **P5-09a — "Profile-aware recommendation selection"** (Learning, engine). BE tasks: (BE-1) `RecommendationEngine` consumes `DerivedProfile` dims 1-4 + confidence gate + `RecommendationOptions`; (BE-2) optional `RecommendationItem` style field + `ItemsJson` additive change (per OQ-5); (BE-3) enrich `RecommendationEngineTests` (per-dimension + cold-start fallback determinism); (BE-4) reviewer + (child-data) security-auditor. No FE.
  - **P3-14a — "Level- and profile-aware Lexi framing"** (Ai, narration). BE tasks: (BE-1) `PromptContext` + `PromptBuilder` motivational/encouragement fragments; (BE-2) all four templates' Recommendation constants (EN+AR) with guardrails; (BE-3) handler injects `IStudentXpQuery` + populates context + **cache-key includes level/style** (OQ-7); (BE-4) api-tester + mandatory security-auditor (AI prompt + child data). No FE. **Stacked on P5-09a** if it consumes the persisted style field (OQ-6a).
- **Option B — single combined story** "At-his-level enrichment" spanning Learning + Ai. Simpler to track but mixes two modules/phases in one branch; the parallelism rules prefer per-module/per-story branches.

**FE-vs-BE split:** **BE-only for this lead.** No FE tasks (contracts stay backward-compatible). Any FE rendering of the new framing is the other lead's separate story — flag, don't author.

**`Shared.Contracts` seam verification (asked in the task):** the gamification-level seam **already exists** (`IStudentXpQuery` → `StudentXpSnapshot.CurrentLevel`), Gamification implements it, and **Learning already consumes it** (`DashboardQueryService`). So **no new seam is required** — the engine doesn't need level at all (un-conflation), and Ai consumes the *existing* seam (just a new DI registration in Ai). A new contract is only needed if the lead picks OQ-6 option (b) (a direct cross-module profile read for Ai) instead of persisting the style hint on the item.

## Recommended pipeline order (first cut — the `planner` finalizes)
1. **`analyzer`** (this brief) → lead resolves OQ-1..OQ-9 (especially OQ-1 scope, OQ-2/3 rules, OQ-5/6 contract choice, OQ-7 cache key). Then **lead approves the story/task breakdown** (rule #9) before any stories/tasks are written.
2. **`planner`** → Execution Plan once stories exist. No `designer` stage (no UI surface — backend-only).
3. **Batch 1 (Learning, P5-09a):** `db-migration` only if OQ-5 promotes a column (likely **skip** — additive jsonb) → `backend-feature` (engine + options + tests) → `security-auditor` (child data) → `reviewer`.
4. **Batch 2 (Ai, P3-14a):** `backend-feature` (PromptContext/builder/templates/handler + cache-key fix) → `api-tester` (Lexi SSE still correct, no-delivery=no-debit, cache freshness on level change) → **mandatory** `security-auditor` (AI prompt + child data) → `reviewer`. **Sequential after Batch 1** if P3-14a consumes the persisted style field (OQ-6a); otherwise the two batches are independent and may run in parallel (separate modules, separate branches per PARALLELISM.md).
5. **`committer`** per story branch after each reviewer PASS; update `docs/dev/HANDOFF.md` (new `RecommendationOptions` config + cache-key change are load-bearing).
