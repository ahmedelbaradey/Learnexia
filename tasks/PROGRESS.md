# Learnexia — Build Progress Tracker

> Single source of truth for **what's done vs. not** across the whole backlog.
> Maintained automatically: the **`committer` agent updates this file on every commit** (flips the row for the story it just committed). The lead may also reconcile it after merges.
>
> Status reflects **merged to `main`** unless a row says otherwise.

## Legend
- ✅ **Done** — pipeline complete, reviewer PASS, committed, merged to `main`
- 🟡 **In progress** — pipeline running (branch exists, not yet merged)
- ✅ **Not started**
- `—` — no work in this stack for this story (single-stack story)

## Recently completed (newest first)
- **2026-06-24 — BL-03 knowledge graph (Backend + Python):** edge inference + suggestion review + query API. .NET EdgeInferenceAdvanceService (consumes infer_edges outbox → KGSuggestion{Pending}, never auto-publish — Decision E); admin approve→publish via IKnowledgeEdgeWriter seam (guards cross-language/duplicate/acyclic); IKnowledgeNodeReader seam; student-facing RelatedConcepts/RemediationPath queries; nullable-DocumentId migration. Python infer_edges lane (LightRAG mocked/devops-gated). Curriculum Intelligence pipeline COMPLETE: BL-04 ✅ → BL-01 ✅ → BL-02 ✅ → BL-05 ✅ → BL-03 ✅.
- **2026-06-24 — BL-05 curriculum ingestion (Backend + Python):** LLM-extracted hierarchy → learning pedagogical tree via the IPedagogicalTreeWriter Shared.Contracts seam. .NET IngestJobAdvanceService : BackgroundService consumes ingest outbox (PipelineJobs WHERE Status='Done' AND JobType='ingest'), orchestrates pedagogical tree writes via IParsingServiceClient, categorizes confidence-scored results (Draft-only), routes low-confidence (< 0.7 threshold) to IngestionReviewItem queue for human review (Pending→{Approved|Rejected} terminal). Parse→ingest hand-off: ParseJobAdvanceService updates doc status → Done, initializes parse→ingest transition via explicit job creation. DB migration AddIngestionReviewItems (IngestionReviewItem + IngestionStatus/ReviewStatus enums). Cross-module seam: IPedagogicalTreeWriter (Shared.Contracts/Learning/; NO project references; DTO-based integration only) + PedagogicalTreeWriterAdapter (Learning.Infrastructure; safe-default NoOp when Learning module not available). Python ingestion lane: new curriculum_intelligence/ingestion/ package (Claude semantic extraction behind NoOpIngestionServiceClient mock, fail-closed design, no DB writes — pure artifact generation), workers/ingest_poller.py claims jobs atomically (FOR UPDATE SKIP LOCKED), ingestion/pipeline.py orchestrates chunking + Claude extraction with per-language support (ar/en). Infrastructure/Config: IngestJobPollerConfiguration (enabled=false for now, confidence_threshold_draft=0.7). Shared resources: SharedResourcesKey + en-US/ar-EG resx (no localized content at MVP). Endpoints: POST /api/Curriculum/Documents/{id}/reingest (idempotent), GET /api/Curriculum/IngestionReviewItems (list paginated), PATCH /api/Curriculum/IngestionReviewItems/{id}/approve (reason ≤2000), PATCH /api/Curriculum/IngestionReviewItems/{id}/reject (reason ≤2000). Tests: Backend integration 42/42 (BL-01+BL-02 compat = 47/47 cumulative Curriculum); Python pytest 57/57 (ingestion unit + semantic extraction mock + ingest-contract; includes diacritics). Build 0 errors. Security-auditor PASS after fixing 2 blocking Highs: (H1) ResultJson untrusted input injection → bounded JSONB contract validation (fail-closed IngestionStatus.Failed sentinel on parse error), (H2) confidence-threshold code-path boundary → 0.7 enum guard (Draft-only when confidence >= 0.7; all else → review queue). Lead decisions baked: Q1 IPedagogicalTreeWriter seam (avoids Learning module reference), Q2 cross-module DTO-only, Q3 confidence threshold 0.7, Q4 embeddings deferred Q4 (vec search post-MVP), Q5 review queue + fail-closed (Draft-only policy), Q6 Python no-DB (artifact-only), Q7 mocked Claude now. Pipeline: BL-04 → BL-01 → BL-02 → BL-05 → BL-03. Deferred (non-blocking): live Claude switch + Azure provisioning + re-audit on Q4 embedding activation.
- **2026-06-23 — BL-01 curriculum document upload (Backend):** admin-only curriculum document upload + pipeline outbox. POST `api/Curriculum/Documents` streams PDF/DOCX/image files (magic-byte validated, 100 MB limit) to dedicated MinIO bucket w/ transactional DB record + `PipelineJob` outbox seam for Python parsing pipeline (BL-02/BL-05). GET `api/Curriculum/Documents` (list paginated) + `api/Curriculum/Documents/{id}` (detail). CurriculumDocument + PipelineJob + DocumentStatus entities; migrations `20260623071411_AddCurriculumDocumentTable` + `20260623071440_AddPipelineJobsTable`; UploadCurriculumDocument command + validators (MIME, size, magic-byte); CurriculumBucketEnsureService (Q1 auto-create), CurriculumCurrentUserService (admin-only policy); Shared.Kernel StorageService streaming fix (Q3 buffer→stream, resolves buffering OOM/DoS High; affects avatar uploads — signature unchanged). Module isolation preserved; extends BL-04 schema. Tests: 24/24 api-tester (Testcontainers PostgreSQL + minio) + integration schema tests. Build 0 errors. Security-auditor PASS (High fixed: 1 OOM/DoS stream fix). Reviewer PASS. Pipeline: BL-04 → BL-01 → BL-02 → BL-05 → BL-03. Lead decisions baked: Q1 bucket auto-ensure, Q2 no KGSuggestion FK, Q3 100 MB streaming, Q4 int id, Q5 string job fields, Q6 AdminOnly policy, Q7 transactional doc+job.
- **2026-06-23 — BL-02 multimodal parsing (Backend + Python):** Python `curriculum_intelligence` worker (Azure Document Intelligence OCR primary + MinerU fallback + RAG-Anything orchestration + Claude diagram captioning, both ar/en) claims `parse` jobs off the `curriculum.PipelineJobs` DB-outbox with atomic `FOR UPDATE SKIP LOCKED`, downloads file from MinIO `curriculum` bucket, parses to normalized JSON (text/images/tables/equations/layout with `source_page`/`source_region`/`parser_used` traceability), writes artifact to `curriculum` bucket, updates job ResultJson w/ `chapters[]` array + diagnostics. .NET side: `ParseJobAdvanceService : BackgroundService` polls `PipelineJobs WHERE Status IN ('Done','Failed') AND JobType='parse'`, advances `CurriculumDocument` parse status → `Done` (stores `ParsedArtifactObjectKey`/`ParsedAt`) or `Failed` (+ diagnostics), initializes provenance tree (one `ContentSource` + N `Chapter` rows); `ReparseCurriculumDocument` command + admin `POST /api/Curriculum/Documents/{id}/reparse` endpoint (idempotent, 409 if already Processing); `IParsingServiceClient` seam (NoOp mock for now), `NoOpParsingServiceClient` test double. DB migration `AddParseResultFieldsToCurriculumDocument` (ParsedArtifactObjectKey, ParseStatus enum, ParseDiagnostics, ParsedAt). First Python service in the repo (ADR-0004); DB-outbox-only integration contract (.NET owns retry via RetryCount). Tests: .NET api-tester 15/15 (BL-01 + BL-04 schema compat = 47/47 Curriculum tests vs Testcontainers); Python pytest 31/31 (contract tests incl. diacritics U+064B–U+065F Arabic OCR benchmark). Build 0 errors. Security-auditor PASS (High+Medium fixed: bounded untrusted ResultJson + no-stranding guarantee). Reviewer PASS. Pipeline: BL-04 → BL-01 → BL-02 → BL-05 → BL-03. ADR-0004 + lead decisions (build-mocked-now, Python home, .NET retry, schema reuse Q9, compose+bucket, mock-only client). DevOps follow-up: Azure DI provisioning + Arabic benchmark + mandatory live re-audit (deferred zip-bomb/XXE/VLM).
- **2026-06-22 — P5-06 parent grade-transition (Backend):** parent-initiated grade transition for linked children via `PUT api/Parent/Children/{childId}/Grade` — re-scopes curriculum to new grade 1–6, preserves history (XP/badges/streaks/mastery), IDOR-guarded, publishes `ChildGradeChangedIntegrationEvent`, audited via `AdminActionPerformedEvent(Child.GradeTransitioned)`. Feature (TransitionChildGrade command/handler/validator + Identity seam `TransitionGradeAsync` method + controller action) + tests (9/9 integration scenarios) + briefs/tasks/handoff; gates: build 0 errors, api-tester 9/9 PASS, security-auditor PASS, reviewer PASS. FE pending separate lead with the P5-06-FE contract.
- **2026-06-22 — P5-05 & P8 QC/E2E (Test):** P5-05 parent dashboard formal QC catalog (50 test cases + coverage report) + gap E2E spec (33 PASS / 4 SKIP / 0 FAIL; child-switch isolation BLOCKED by seat-limit 409 — needs unlimited-seat E2E config or pre-seeded multi-child account). P8 learning-language FE QC (42 test cases + coverage report) + new E2E spec (38/38 PASS) covering add-child learning-language, parent change-learning-language, app-shell language switch, UI-lang/learning-lang axis independence. Reviewer PASS, no feature code changes. Follow-ups (optional): stable testIDs for P5-05 20-day Export CSV, P8-04 change-LL controls (retire aria-label+force:true workarounds).
- **2026-06-22 — P5-06 parent grade-transition (Backend):** parent-initiated grade transition for linked children via `PUT api/Parent/Children/{childId}/Grade` — re-scopes curriculum to new grade 1–6, preserves history (XP/badges/streaks/mastery), IDOR-guarded, publishes `ChildGradeChangedIntegrationEvent`, audited via `AdminActionPerformedEvent(Child.GradeTransitioned)`. Feature (TransitionChildGrade command/handler/validator + Identity seam `TransitionGradeAsync` method + controller action) + tests (9/9 integration scenarios) + briefs/tasks/handoff; gates: build 0 errors, api-tester 9/9 PASS, security-auditor PASS, reviewer PASS. FE pending separate lead with the P5-06-FE contract.
- **2026-06-21 — P5-05 parent dashboard FE (PR #217)** + **P7-10/P7-11 admin dashboards FE (PR #218)** merged. Parent analytics dashboard (real /api/Parent data, hand-rolled Tamagui charts) + admin platform-KPI & AI-safety dashboards (Recharts, PII-light flagged table). reviewer PASS · E2E green · security-auditor PASS (P7-11). **Phase-7 admin console FE is now COMPLETE (P7-01..13).**
- **2026-06-19 – P7 curriculum sub-wave 2c (P7-03 FE):** Skill dependency graph — accessible list/adjacency editor (role=listbox + roving tabindex + aria-live; NO graph-viz/drag lib per lead decision); skills CRUD + KnowledgeGraph read + edge add/remove (Prerequisite only, strength 1.0 hard-coded); subject-tree scoping (concept.subjectId); cycle/cross-language/duplicate rejections mapped inline; refetch-on-update (no optimistic); NODE_TYPE/RELATIONSHIP_TYPE consts in shared; gates PASS (reviewer PASS; security-auditor not required — curriculum metadata, no PII/content sink). Wave 2 — **Curriculum admin FE COMPLETE** (P7-01..05 all shipped across 3 sub-waves: 2a #175, 2b #179, 2c this PR).
- **2026-06-19 – P7 Wave 3 Admin Dashboard — moderation queue (P7-09) + audit log viewer (P7-12) + gamification overrides (P7-13 FE):** Moderation queue list + detail + ReviewItemDialog (approve/reject with reason≤2000/flag; Pending→terminal; SafetyVerdictView reason-codes only, no raw content; verdict enums INT on wire); Audit read-only list + inline-expand detail (escaped JSON/text, no export endpoint deferred); Gamification badge/mission/timed-event catalogs (list+PATCH activate/retire, NO delete soft-retire) + league-tier + streak-freeze grant dialogs from users/[id] (student-only); shared ReasonField near-limit scaling + StatusBadge moderation variant + AdminConfirmDialog mod/override variants + AdminSideNav 3 new nav items + lib/strings 180+ keys + queryKeys adminAudit/adminGamification/adminModeration. Gates: reviewer PASS (should-fixes applied), security-auditor PASS (0 Critical/High). Known gaps: audit export endpoint, student tier+freeze-balance read, timed-event edit prefill, DG-2 lifecycle-state on DTOs. Test status: no backend e2e (FE-only); admin Playwright E2E coverage for Wave 2+3 deferred (next FE step). Pre-existing Wave-2 lint: @learnexia/api-client no-unused-vars (useAddKnowledgeEdge/useCreateSkill/useEditQuestion/useUpdateSkill) — flagged cleanup. Remaining admin: P7-11 FE (blocked backend), P7-10 FE (blocked P5-03).
- **2026-06-19 – P7 curriculum sub-wave 2b (P7-02 + P7-04 FE):** Lessons CRUD + ContentBlockEditor (Text/Image/Video/Callout) with sanitized Markdown preview via marked+DOMPurify; per-lesson questions MCQ/TrueFalse/FillInBlank/Matching with JSONB round-trip; keyboard reorder; inherited-language + difficulty + lock badges; lifecycle reuse Lesson=3/QuizQuestion=4. Gates: reviewer PASS, security-auditor PASS-with-notes (0 Critical/High; sanitization verified fail-closed SSR + URL denylist + TrueFalse lowercase). Committed on `feat/P7-curriculum-2b`.
- **P10-17 (Backend + Tests)** — Refund reconciliation: unused purchased energy (refundable = purchasedTotal − consumedPurchased − alreadyRefunded, per-original-Payment FIFO, bucket-B/shared-purchased ONLY), parent (family-scope) + admin (any family, actor-ledgered) initiate, webhook-settled (refund.succeeded decrements shared PurchasedBalance, idempotent, clamped no-negative). Security: per-payment idempotency key pack-refund:{paymentId} + Status==Refunded guard + subtract-already-refunded (distinct-event-id over-refund fix) + xmin optimistic-concurrency + bounded retry on wallet decrement (concurrency race fix) + explicit parent-ownership IDOR assertion. Refines the P10-09 refund path; no migration. Tests: Billing unit 134/134; api-tester P10_17 18/18 (incl TC-RF-S1 distinct-event-id, TC-RF-S2 repurchase-in-between, TC-RF-S3 full-refund-re-request, TC-RF-S4 IDOR); security-auditor PASS (0 Crit/High after fixing 2 real money bugs); reviewer PASS. Known-red CI: 3 daily-cap time-of-day flake (pre-existing); P3_AI build fix needed (PR #166). Stacked on #164 (base feat/P10-16-redistribution). — committed
- **P10-14 (Backend + Tests)** — Child seats model: Subscription.IncludedSeats/PurchasedExtraSeats + Plan.IncludedSeats + SeatReservation state machine (Initiated→Reserved→Active|Cancelled). Parent add-child reserve-before-create pattern (via ISubscriptionSeatContract cross-module seam); 409+no-child on no free seat. Webhook seat branch: inline seats.max ceiling guard + per-payment Status==Initiated idempotency (single-tx, no energy mint). Mid-cycle MONEY proration (SeatPrice × qty × remaining-cycle-ratio, server-side, legacy-timestamp Kind normalization). Cycle-end cancel via PendingExtraSeatRemovals marker (no grace, no energy reclaim). Regression fixes: WEBHOOK-SEAT-04 ceiling, 10 stale-seed tests, TC-GS-04 17→21 renamed. Tests: P10_14 22/22, blast-radius 191/191 (P10_14+P10_13+P1_03+P2_12+P10_01_12), Billing unit 93/93, Ai unit 287/287, build 0 errors. Security-auditor PASS (High #1 fixed; Med #2 removed; Med #3 deferred P10-15 + rationale). Reviewer PASS. Known-red CI (pre-existing, not this PR): ~19 AI-SSE tests (LLM keys), BE-TC-24 (comments), BE-TC-19b (assertion). Stacked on #158 (base feat/P10-13). — committed
- **Wave 3:** P10-05 (subscription plans — Plan + Subscription + RealBillingSubscriptionContract family tier via IParentChildQuery, upgrade/downgrade/cancel state machine IDOR-scoped endpoints) + P10-06 (pay-for-subscription — IPaymentProvider + FakePaymentProvider (config-selected) + Payment + WebhookEvent + checkout idempotency + signature-gated webhook verify-before-mutation HMAC FixedTimeEquals + ProviderEventId replay dedup + server-side amount/tier check → SubscriptionActivatedIntegrationEvent → grant; ReconcilePaymentsJob). 2 migrations BillingPlansSubscriptions + AddPaymentAndWebhookTables. Test-infra: SQLite :memory: for txn tests. Tests: Billing 67/67, W3 integration 20/20 (SubscriptionPayment), regressions green (W2 energy 21, AI E2E 24, W1 Billing 19). Reviewer PASS + mandatory security gate PASS (0 blocking; signature verify before mutation, idempotent under concurrency, forged amount no escalate). Go-live follow-ups (HANDOFF): webhook amount alerting, provider fail-fast, body-size/rate cap. — committed
- **P7-12 (Backend + Tests)** — Curriculum admin creates now produce audit rows (real entity id via ILearningRepository.FlushAsync pattern) + OccurredAtUtc UTC normalization at read boundary + LoggerManager.LogError exception logging fix. Bucket C + D defects found during verify pass now fixed. P7-12 audit 22/22, full P7 410/414 (4 pre-existing/flaky). Reviewer PASS + security-auditor PASS + completeness-critic PASS. — committed
- **P7-07 (Backend + Tests)** — Account-delete cascade 500 + post-commit side-effect ordering + refresh guard bugfix (nested txn removed; Identity-scoped post-commit domain-event buffer for session revocation + event publishing on commit; RefreshToken rejects suspended/deleted before minting). P7-07 integration 22/22, P1-02 refresh 26/26, Identity unit 10/10. Reviewer PASS + mandatory security gate PASS. — committed
- **P7-04 (Backend + Tests)** — CorrectAnswer jsonb encode/decode bugfix (MCQ/FillInBlank create 500 + edit 422 fixed) — committed
- **Wave 4:** P3-05-Backend (Hints + WhyWrong + Simplify SSE endpoints — IDOR-scoped, no-reveal guard, usage instrumentation, MVP slice) — committed
- **Wave 4:** P3-06-Backend (SimilarExample — AI tutor SSE endpoint, intent #4, no preamble, refuse-and-redirect, usage instrumentation, MVP slice) — committed
- **P3-04 (Backend)** — Explain a concept on demand (ExplainConceptCommand + validator + handler orchestrates ILearningContextProvider→IPromptBuilder→ISafetyLayer with refuse-and-redirect on empty context; SSE ExplainController with pinned wire contract event:message/redirect/error/done; ILessonContextContract cross-module seam; RedirectResponseBuilder ar/en; Help* instrumentation events; in-process rate limiter; folded into Ai module; buffer→safety→emit, never raw tokens; cache economy deferred P3-01-BE-12/13/14; live grounding dormant EmptyLearningContextProvider default; 208 unit + 13 SSE integration green; mandatory security gate PASS, error-leak + D-1 fixes landed) — committed
- **P3-04 (Backend)** — Explain a concept on demand (ExplainConceptCommand + validator + handler orchestrates ILearningContextProvider→IPromptBuilder→ISafetyLayer with refuse-and-redirect on empty context; SSE ExplainController with pinned wire contract event:message/redirect/error/done; ILessonContextContract cross-module seam; RedirectResponseBuilder ar/en; Help* instrumentation events; in-process rate limiter; folded into Ai module; buffer→safety→emit, never raw tokens; cache economy deferred P3-01-BE-12/13/14; live grounding dormant EmptyLearningContextProvider default; 208 unit + 13 SSE integration green; mandatory security gate PASS, error-leak + D-1 fixes landed) — committed
- **P3-11 (Backend)** — Adaptive quiz selection (QuizSelectionEngine pure static 70/30 weighted-mix, Attempt.ServedDifficultyMix jsonb + TargetDifficulty int migration, StartAttempt integration after guards, deterministic resume via sort-by-Id, graceful degradation on thin pools, 9 QuizSelectionEngine unit tests + 8 integration green, reviewer PASS, inline security PASS) — committed
- **P3-07 (Backend)** — RAG retrieval (Curriculum module 4 projects, CurriculumChunk+CurriculumVersion+chunk_embeddings_bge_m3 pgvector schema HNSW, InitialCurriculum migration, BgeM3EmbeddingProvider + DeterministicEmbedding placeholders, RetrieveChunksQuery handler, RagContextProvider ILearningContextProvider+ICurriculumContextQuery, CurriculumChunkSeeder, cross-module MediatR, 11/11 integration vs pgvector; placeholder embeddings, real BGE-M3 pending P3-07-BE-0 TEI provision) — committed
- **P3-03 (Backend)** — Prompt Builder (IPromptBuilder stateless facade, PromptContext value object, 4-subject 4-intent 2-language templates ar/en Math/Science/Arabic/English, TemplateSelector pure lookup, HelperIntent enum Explain/Hint/WhyWrong/SimilarExample, ToneFrame anti-injection, PII-minimal grade+age only, optional seams IStudentWeakAreasQuery/IChildLearningProfileQuery/ICurriculumContextQuery/ILearningContextProvider with safe stubs, graceful degradation, 203 Ai unit tests green 166 new, FR-AI-6 mandatory security gate PASS) — committed
- **P3-02 (Backend)** — AI Safety Layer (ISafetyLayer facade, 3 composable fail-closed checks toxicity/age/hallucination, SafetyEvents append-only table, eval harness ar+en) — committed
- **P3-13 (Backend)** — Adaptive student profile (StudentLearningProfile + StudentProfileEngine 4-rule derivations + SP-Recompute job + GET /api/Learning/Profile) — committed
- **P3-10 (Backend)** — Spaced-repetition scheduler (SpacedRepetitionEngine IsDue/ComputeNextReview, expanding ladder [1,3,7,14,30], GetDueMasteryRows/UpdateSR repo methods, SpacedRepetitionSweepJob Hangfire fixed-ID, write-path hook in CompleteAttempt, GET /Reviews/Due endpoint) — committed
- **P3-01 (Backend)** — AI Gateway (IAiGateway seam, Ai module, Claude + second provider, task-based model router) — committed
- **P3-08 (Backend)** — Adaptivity Engine (weighted-score algorithm, 4-signal model, AdaptivityService seam, inspection endpoint + admin debug endpoint) — committed
- **P3-09 (Backend)** - Student mastery engine (StudentSkillMastery table + MasteryEngine + write/read paths + IMasteryService seam) - committed (Wave 1, PR #126)
- **2026-06-13 â€” P1/2/3 carryover (branch `feat/p1-p2-p3-carryover`):** gamification FE (all screens + TabBar + celebrations), Matching full-stack, parent Reports + attempt-history (both surfaces), auth messaging + CAPTCHA, parentâ†”child attempts authz; e2e 39/3-skip/0-fail; PR pending.
- **2026-06-13 - AI-phase + Phase-10 planning breakdown (PLANNING ONLY, all ✅; PR #124 `docs/ai-phase-task-breakdown -> main`):** authored the full task breakdown for **Phase 4 - AI Tutor (P3-01..13)**, the **Curriculum-Intelligence backlog (BL-01..05)**, and the new **Phase 10 - Payment, Billing & Credits (P10-01..12)** - Pipeline Briefs (`docs/briefs/`) + Execution Plans (`docs/plans/`) + per-stack task files. **No code - build plan only.** Payments renumbered Phase 9 -> 10 (`main` owns Phase 9 = Notifications). Cross-cutting briefs: `ai-helper-mvp`, `ai-cost-routing`, `ai-eval-gate`, `curriculum-system-of-record`. Settled: new `Ai` + `Curriculum` + `Billing` modules, Claude provider w/ model routing, AI credit economy (Global Settings P10-12), Arabic stack (Azure DI + RAG-Anything). Chore PR #123 = subagent model tuning. See HANDOFF 2026-06-13 note for the full decision log.
- **2026-06-10 - Phase 2 Exit Gate (P2-HARDENING):** full QC + test pass over Phase 2. **Design:** `qc-test-designer` catalogs for all 11 backend + 7 student-app stories (~319 + ~208 cases; PR #107/#108, merged). **Backend api-tester:** P2-01 (92) + P2-02 (39) integration tests green; P2-03..P2-12 catalogs ready. **Frontend e2e** (Playwright, isolated per story): P2-09 (23) / P2-02 (19) / P2-03 (6, lock-gate P0s pass) / P2-05 (15) / P2-06 (21) / P2-07 (21) / P2-12 (37) - ~142 pass; blocked long-tail classified in `docs/qc/PHASE-2-FE-blocked-classification.md` (seed/spec/feature follow-ups, none release-blocking). **Bugs found+fixed:** BUG-001 (child-home subjects dropped by name-match -> keyed off subjectCode), DEF-P205FE-02 (lesson back broken on web deep-link), and **DEF-P205FE-01 (HIGH) - quiz grading: jsonb-encoded CorrectAnswer compared raw -> every MCQ/TF/FillInBlank graded wrong; fixed in AnswerComparator (decode), 18 unit tests, verified live**. Remaining: Matching renderer + TrueFalse/FillInBlank seed (P2-06-FE-2 / P2-06-BE-3, already-tracked yellow). **Phase 2 tagged complete.**
- **2026-06-06 â€” FE status reconciliation:** board corrected against `main` ground truth â€” Phase-1 FE (P1-01/02/03/04) and Phase-2 student FE (P2-05/06/07/09, merged via PR #70/#71/#72/#74) flipped ðŸ”²â†’âœ…; **P8-04 FE corrected âœ…â†’ðŸ”² (branch `feat/P8-04` was backend-only â€” no FE shipped)**. Open-WIP FE branches: `feat/P4-08-gamification-screens-motion` (resumable), `feat/design-system-pixel-align` (stale, holds font/RTL fixes).
- **P8-04 (BE only):** Change a child's learning language (parent-only, fresh start) â€” backend merged; **parent FE not built** (carry-forward).
- **Wave 14 (BE+FE):** P4-07 (weekly leagues ï¿½ Phase 3 Gamification 6th story: AddLeagueAndLeagueMembership migration (Leagues + LeagueMemberships + LeagueXpDeltaLogs + StudentXpProfile.CurrentTier + MembershipStatus enum) + StudentXpProfile.ApplyAward 4-arg refactor to single XP chokepoint (amount/newLevel/reason/occurredAtUtc) raising XpAwardedDomainEvent + LeagueStandings pure static (ComputeCutoffs handles tier extremes + small-cohort scaling, Apply assigns ranks/status/tierAfter) + StudentXpProfile.UpdateTier mutation + LeagueOptions config (CohortSize=30/PromoteCount=7/DemoteCount=5/cron=15 0 * * 1 UTC Monday) + IncrementLeagueXpCommand (period key derived from request.OccurredAtUtc for week-boundary correctness, dual-layer idempotency via LeagueXpDeltaLog unique index, narrowed DbUpdateException catch, no-op when no membership) + XpAwardedLeagueHandler (in own try/catch per ADR 0002 ï¿½3) + LeaguePlacementService Infrastructure (find-or-create cohort + insert membership with graph-nav AttachLeague) + IStudentLeagueQuery cross-module seam with LAZY INSTANTIATION on dashboard read + LeagueTierDto drift enum in Shared.Contracts + DashboardDto.LeaguePreview wired + GET /api/Gamification/Leagues/Me endpoint with "Student #N" anonymization + LeagueRolloverJob Hangfire Monday 00:15 UTC (after streak sweep + mission rollover); FE: LeaguePreviewRow inline component + EN/AR i18n + dashboard wire-up; lead-approved ApplyAward chokepoint refactor + Student #N anonymization + top-7/bottom-5 cutoffs + endpoint+FE bundled; 27 LeagueStandings unit + 4 enum drift unit + 23 integration tests + 85/85 P4-02..P4-06 regression = 108/108 full P4 suite; security PASS 0 blocking; reviewer-fixes-applied: periodKey derived from OccurredAtUtc + stale TODOs removed; accepted MVP risks: R1 cohort overfill / D15 XP-before-dashboard / JoinOrder collision / XpAwardedDomainEvent retry ghost; deferred to P4-08 UI motion / P4-09 nudges / P4-10 Redis / P7 admin tier override; LeaguePlacementServiceTests deferred (integration-covered)) ï¿½ open as PR on feat/P4-07-weekly-leagues
- **Wave 13 (BE):** P4-06 (daily/weekly missions ï¿½ Phase 3 Gamification 5th story: AddMissionDefinitionStudentMissionProgressLog migration (MissionDefinitions catalog + StudentMissions per-period instance + MissionProgressLogs idempotency ledger) + XpReason.MissionCompleted=6 + MissionTargetType enum + MissionPeriodCalculator pure static UTC + ISO 8601 week math + StudentXpProfile.RecordMissionCompleted domain mutation raising MissionCompletedDomainEvent + StudentMission.ApplyProgress/MarkCompleted mutations + IncrementMissionProgressCommand (row-lock after probe, dual-layer idempotency, inline completion to avoid nested-tx) + 3 notification handlers (LessonCompletedMissionHandler/AnswerSubmittedMissionHandler/StreakAdvancedMissionHandler, each in own try/catch per ADR 0002 ï¿½3) + MissionSeeder idempotent atomic seed of 8 missions at startup + IStudentMissionsQuery cross-module seam with LAZY INSTANTIATION on dashboard read + DashboardDto.DailyMissions[] + WeeklyMission (replaces old DailyMission placeholder) + GET /api/Gamification/Missions/Me endpoint + MissionStatusDto/MissionTargetTypeDto/MissionTypeDto drift enums in Shared.Contracts + MissionRolloverJob Hangfire @ 5 0 * * * daily + 10 0 * * 1 weekly bulk ExecuteUpdateAsync; lead-approved 8 missions/lazy/PM-counts/daily-list+weekly-single + graph-nav 4th instance (AttachStudentMission); 19 unit tests + 23 integration tests + 62/62 P4-02/03/04/05 regression; security PASS with F1 comment + F2 narrowed catch + F3 DTO enums + F5 lock placement + reviewer F2-cleanup applied) ï¿½ open as PR on feat/P4-06-missions

## Phase 1 â€” Foundation
> **Per-task detail added 2026-06-07:** each P1 task file now carries a Status column (âœ…/ðŸŸ¡/ðŸ”²). Story-level cells below are unchanged. **Closed in the P1/P2/P3 carryover (Batch 1):** **P1-10-FE-6** (admin account-locked message) âœ…, **P1-11-FE-15/16** (sign-in lockout msg + Register CAPTCHA) âœ…. Remaining open sub-task gaps inside otherwise-shipped stories: **P1-12-FE-*** Batch-2 wiring ðŸŸ¡ + **FE-4** forgot/reset-password screens ðŸ”². Their backend deps (P1-12-BE, P1-13-BE-1/2/4) are merged, so all are unblocked. **P1-11 FE** stays ðŸŸ¡ pending the remaining pixel-perfect sub-tasks (FE-7 edit-child save, FE-13 QA pass), but Reports (chart-less KPIs + mastery + Send-Report stub) plus FE-15/FE-16 are now done via carryover.
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| â€” | Monorepo, api-client & shared (foundation) | â€” | âœ… |
| P1-01 | Register as a parent | âœ… | âœ… |
| P1-02 | Stay signed in (token refresh & sign-out) | âœ… | âœ… |
| P1-03 | Parent onboarding & add children | âœ… | âœ… |
| P1-04 | Link a parent to a child account | âœ… | âœ… |
| P1-05 | Role-based access control | âœ… | â€” |
| P1-06 | PostgreSQL + pgvector + Redis | âœ… | â€” |
| P1-07 | Dockerized environment & CI/CD | âœ… | â€” |
| P1-08 | Design system & components (RTL) | â€” | âœ… |
| P1-09 | Auth & onboarding screens | âœ… | âœ… |
| P1-10 | Sign in to the admin dashboard | âœ… | âœ… |
| P1-11 | Web app pages (pixel-perfect, parent web) | â€” | ðŸŸ¡ |
| P1-12 | Web account backend (Batch 2) â€” profile/Me, register consent, edit-child, avatar (MinIO), Google sign-in, password reset | âœ… | ðŸŸ¡ |
| P1-12b | IUserLookup cross-module seam | âœ… | â€” |
| P1-13a | Notifications email delivery (enabler) | âœ… | â€” |
| P1-13 | Backend hardening (lockout/sign-in/admin seed/CAPTCHA) | âœ… | â€” |
| P1-13b | Backend hardening pass â€” BE-1 rate-limiting (PR #50); rest â†’ P6-06 | âœ… | â€” |

## Phase 2 â€” Learning Core
> **Per-task detail added 2026-06-07:** each P2 task file now carries a Status column (âœ…/ðŸŸ¡/ðŸ”²). Story-level cells below are unchanged. **Quiz Matching is now done on both stacks** â€” **P2-06-FE-2** âœ… (real tap-to-pair `MatchingPanel`) and **P2-06-BE-3** âœ… (order-independent comparator + all-4-types demo seed) shipped in the P1/P2/P3 carryover (payload `{pairs:[{leftId,rightId}],attemptOrder,timeMs}`). Note: P2-06's "assessment module" was deliberately folded into the **Learning** module per the no-new-module decision.
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P2-01 | Model the curriculum hierarchy | âœ… | â€” |
| P2-02 | Browse subjects and lessons | âœ… | âœ… |
| P2-03 | Navigate the skill tree | âœ… | âœ… |
| P2-04 | Unlock lessons by prerequisite/mastery | âœ… | â€” |
| P2-05 | Open and complete a lesson | âœ… | âœ… |
| P2-06 | Take a quiz (4 question types) | âœ… | âœ… |
| P2-07 | Get instant answer feedback | âœ… | âœ… |
| P2-08 | Record granular per-question answers | âœ… | â€” |
| P2-09 | See the home dashboard | âœ… | âœ… |
| P2-10 | Seed demo subjects & skill trees | âœ… | â€” |
| P2-11 | Author the skill dependency graph (relational, hand-authored) | âœ… | â€” |
| P2-12 | Account settings APIs (Parent module + Notifications prefs + Identity security) | âœ… | âœ… |

## Phase 3 â€” Gamification *(story IDs `P4-xx`)*
> Backend XP/streak/hearts/badges/missions/leagues shipped. **Gamification FE shipped** via the P1/P2/P3 carryover on branch `feat/p1-p2-p3-carryover` â€” bottom TabBar + xp/streak/hearts/events/badges/missions/league screens + celebrations. Task tree under `tasks/Frontend/student-app/Phase-3-Gamification/`.
> **Carry-over (Phase 1/2 gap closure scheduled into this wave):** `Backend/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-BE.md` (quiz Matching type) + `Frontend/student-app/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-FE.md` (Reports build, account-locked message, Register CAPTCHA, landing ar/RTL, Matching UI).
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P4-01 | Emit learning domain events | âœ… | â€” |
| P4-02 | Earn XP and level up | âœ… | âœ… |
| P4-03 | Maintain a daily streak | âœ… | âœ… |
| P4-04 | Lose hearts and enter Practice Mode | âœ… | âœ… |
| P4-05 | Earn badges | âœ… | âœ… |
| P4-06 | Complete daily/weekly missions | âœ… | âœ… |
| P4-07 | Compete in weekly leagues | âœ… | âœ… |
| P4-08 | Gamification screens & motion | â€” | âœ… |
| P4-09 | Re-engagement notifications | âœ… | ðŸ”² |
| P4-10 | Redis realtime gamification state | âœ… | â€” |
| P4-11 | Streak freeze, timed events & weekly challenges | âœ… | âœ… |

## Phase 4 â€” AI Tutor *(story IDs `P3-xx`)*
| Story | Title | Status |
|---|---|:--:|
| P3-01 | Route AI requests through an AI Gateway | ✅ |
| P3-02 | Filter AI output through a Safety Layer | ✅ |
| P3-03 | Build personalized tutor prompts | ✅ |
| P3-04 | Explain a concept on demand | ✅ |
| P3-05 | Progressive hints & simpler re-explanations | ðŸ”² |
| P3-06 | Generate curriculum-grounded questions (RAG) | ✅ |
| P3-07 | Retrieve curriculum context via vector search | ✅ |
| P3-08 | Adjust difficulty adaptively | ✅ |
| P3-09 | Track per-skill mastery | ✅ |
| P3-10 | Schedule spaced-repetition practice | ✅ |
| P3-11 | Serve adaptive quizzes | ✅ |
| P3-12 | Interact with the AI tutor UI | ðŸ”² |
| P3-13 | Build the adaptive student profile | ✅ |

## Phase 5 â€” Parent + Analytics
| Story | Title | Status |
|---|---|:--:|
| P5-01 | Generate a weekly student report | ðŸ”² |
| P5-02 | Detect and rank weak areas | ðŸ”² |
| P5-03 | Capture product analytics events | ðŸ”² |
| P5-04 | Deliver reports via notifications | ðŸ”² |
| P5-05 | View the parent dashboard | ✅ |
| P5-06 | Transition a child to a new grade | ✅ |

## Phase 6 â€” Stabilization
| Story | Title | Status |
|---|---|:--:|
| P6-01 | Meet API & AI performance targets | ðŸ”² |
| P6-02 | Validate AI safety with an eval set | ðŸ”² |
| P6-03 | Pass localization & RTL review | ðŸ”² |
| P6-04 | Regression, prompt-tuning & bug triage | ðŸ”² |
| P6-05 | Observability: logging, tracing, dashboards | ðŸ”² |
| P6-06 | Backend security hardening (timing-oracle/email-locale/secrets/Redis rate-limit) | ðŸ”² |

## Phase 7 â€” Admin Console *(post-MVP)*
> **Admin Console FE COMPLETE** — curriculum (P7-01..05, #175/#179/#182), users/accounts/child (P7-06..08, #170), moderation/audit/gamification (P7-09/12/13, #185), platform-analytics + AI-safety dashboards (P7-10/11, #218). All ✅.
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P7-01 | Manage subjects & units | ✅ | ✅ |
| P7-02 | Manage lessons & lesson content | ✅ | ✅ |
| P7-03 | Author skills & the skill dependency graph | ✅ | ✅ |
| P7-04 | Manage quizzes & questions | ✅ | ✅ |
| P7-05 | Publish, version & preview curriculum content | ✅ | ✅ |
| P7-06 | Search & inspect users | ✅ | ✅ |
| P7-07 | Suspend, reactivate & delete accounts | ✅ | ✅ |
| P7-08 | Manage child profiles & grade overrides | ✅ | ✅ |
| P7-09 | Content moderation queue & review actions | ✅ | ✅ |
| P7-10 | Platform analytics & KPI dashboard | ✅ | ✅ |
| P7-11 | AI-safety & quality monitoring dashboard | ✅ | ✅ |
| P7-12 | Admin action audit log | ✅ | ✅ |
| P7-13 | Gamification admin overrides (tier / badge & mission catalog / timed-event write / streak-freeze) | ✅ | ✅ |

## Phase 8 â€” Localization
> Learning language (medium of instruction) vs UI language; bilingual curriculum as parallel ar/en trees. Design: `docs/architecture/localization-architecture.md`.
> **App-side localization FE wave** (tasks `tasks/Frontend/student-app/Phase-8-Localization/`): **P8-99-FE** app-shell foundation (fonts + persisted UI-language switch + RTL + api-client regen, incl. a durable NSwag `/Me` operationId fix) âœ… merged (PR #93); **P8-01-FE** (add-child learning-language field) âœ… merged (PR #94); **P8-04-FE** (parent change-learning-language UI, fresh-start warning) ðŸŸ¡ PR open on `feat/P8-04-FE`. Wave feature-complete.
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P8-01 | Set a child's learning language (parent-driven; JWT claim) | ðŸ”² | ðŸ”² |
| P8-02 | Author bilingual curriculum (SubjectCode + Language; parallel trees) | ðŸ”² | â€” |
| P8-03 | Serve curriculum in the student's learning language | ðŸ”² | â€” |
| P8-04 | Change a child's learning language (parent-only, fresh start) | âœ… | ðŸŸ¡ |


## Phase 9 — Notifications *(story IDs `P9-xx`)*
> Backend shipped extensively (P9-01..P9-12: push/inbox APIs, nudge arbitration, re-engagement, SR/weekly reminders, localization, analytics sink). **Frontend NOT started** — the student-app notification surfaces are pending:
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P9-01 | Push permission + device registration | ✅ | ✅ |
| P9-02 | Notification deep-linking + foreground | ✅ | ✅ |
| P9-03 | In-app notification inbox | ✅ | ✅ |
| P9-04 | Parent per-child notification controls (toggles/quiet-hours/cap) | ✅ | ✅ |

## Phase 10 - Payment, Billing & Credits *(story IDs `P10-xx`, post-MVP)*
> Task breakdown authored 2026-06-13 (PR #124). AI credit economy ("⚡ طاقة المساعد") + monetization; **parent-driven** (web checkout, no native IAP); new `Billing` module owns the dual-pool ledger + subscriptions + payments; Global Settings (P10-12) makes the economy runtime-tunable. **Renumbered from Phase 9** (which `main` owns as **Notifications**) - files under `*/Phase-10-Payments-Billing/`. `P10-03` (spend) is hard-blocked on the AI Helper cluster (P3-01..06). **Stacked wave (PRs #157 → #158 → #159):** P10-12 intake + P10-13 (family wallet) + P10-14 (child seats & add-child) + P10-15/16/18 (enforcement, redistribution, pause).
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
> ⚠️ **FE status below is OVERSTATED.** A 2026-06-21 code audit found the P10 frontend is largely **stubs/not-built**: only a read-only `PlanPanel` (disabled "Manage") + an `EnergyWeb` display fed by `getEnergyBalanceStub()`. No checkout / energy-pack / billing-history / refund / admin-billing-config / family-wallet / seat-lifecycle screens exist. Treat P10-05..11 + P10-13 FE as **✅ (verify)** despite the ✅ marks; P10-15/16/18 FE ✅.
| P10-01 | Credit (energy) account & ledger *(enabler)* | ✅ | — |
| P10-02 | Grant monthly energy per plan | ✅ | — |
| P10-03 | Spend energy on AI help (charge-on-delivery) | ✅ | — |
| P10-04 | Daily soft cap & low-energy warning | ✅ | — |
| P10-05 | Manage subscription plan (monthly 199 / annual 1990 EGP) | ✅ | ✅ |
| P10-06 | Pay for a subscription (provider; web checkout) | ✅ | ✅ |
| P10-07 | Buy an energy pack (1000 credits / $5) | ✅ | ✅ |
| P10-08 | Billing history & receipts | ✅ | ✅ |
| P10-09 | Failed payments & refunds | ✅ | ✅ |
| P10-10 | Kid-facing energy UI (⚡ read-only) | — | ✅ |
| P10-11 | Admin: configure plans, grants & costs | ✅ | ✅ |
| P10-12 | Runtime config via Global Settings *(enabler)* | ✅ | — |
| P10-13 | Family wallet (shared budget: per-child seat reservation, cycle-cumulative spend) | ✅ | ✅ |
| P10-14 | Child seats & seat-reserved add-child (seat model, mid-cycle money proration, cycle-end cancel) | 🟡 | — |
| P10-15 | Seat enforcement, grace period & NoSeat/Locked lifecycle | 🟡 | ✅ |
| P10-16 | Family energy redistribution | 🟡 | ✅ |
| P10-17 | Refund reconciliation (unused purchased energy) | 🟡 | — |
| P10-18 | Pause child access | ✅ | ✅ |
## Backlog (Phase 2+) â€” Curriculum Intelligence
| Story | Title | Status |
|---|---|:--:|
| BL-01 | Upload curriculum documents with metadata | ✅ |
| BL-02 | Parse curriculum files (Multimodal Parsing) | ✅ |
| BL-03 | Build & query the knowledge graph | ✅ |
| BL-04 | Curriculum, KG & vector schema | ðŸ”² |
| BL-05 | Ingest parsed content into hierarchy | ðŸ”² |

---

## Deferred / follow-up debt (not blocking; track for a hardening pass)
- Anti-automation (rate-limit/CAPTCHA) on anonymous registration â€” P1-01
- `RoleHelper` legacy lowercase-constant cleanup â€” Identity
- Remove `DEMO_PgvectorProof` migration when the real embedding table lands â€” P1-06
- Container non-root image, CI action SHA-pinning, staging TLS cert â€” P1-07
- Tokenize inline glow/alpha shades in components â€” P1-08
- **Open decision:** staging deploy provider (Azure / Railway / Render) â€” see `docs/deploy/staging-decision.md`
- **Phase-2 QC follow-ups (from the P2 exit gate; non-blocking):**
  - ~~Seed **TrueFalse / FillInBlank** quiz questions + finish the **Matching** renderer (P2-06-FE-2 / P2-06-BE-3)~~ â€” **done in the P1/P2/P3 carryover**: real tap-to-pair `MatchingPanel` + order-independent comparator + all-4-types demo seed shipped (payload `{pairs:[{leftId,rightId}],attemptOrder,timeMs}`).
  - Backend defects the api-tester catalogs flagged (assert-actual, lead-decision): P2-01 duplicate-subject -> 500 (AddSubject omits SubjectCode/Language) and FK-orphan -> 500; cross-language browse silently redirects (not 403); no start-lock-guard (FE is the only lock gate); Learning IDOR -> 401 / business-state -> 424 conventions. See `docs/qc/P2-*/`.
  - Implement the remaining backend api-tester stories (P2-03/04/05/06/07/08/09/11/12) + fill P2-02's execution report.
  - Small FE testID follow-ups + spec nits per `docs/qc/PHASE-2-FE-blocked-classification.md` (categories D-I).
