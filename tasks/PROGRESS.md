# Learnexia â€” Build Progress Tracker

> Single source of truth for **what's done vs. not** across the whole backlog.
> Maintained automatically: the **`committer` agent updates this file on every commit** (flips the row for the story it just committed). The lead may also reconcile it after merges.
>
> Status reflects **merged to `main`** unless a row says otherwise.

## Legend
- âœ… **Done** â€” pipeline complete, reviewer PASS, committed, merged to `main`
- ðŸŸ¡ **In progress** â€” pipeline running (branch exists, not yet merged)
- ðŸ”² **Not started**
- `â€”` â€” no work in this stack for this story (single-stack story)

## Recently completed (newest first)
- **P3-10 (Backend)** — Spaced-repetition scheduler (SpacedRepetitionEngine IsDue/ComputeNextReview, expanding ladder [1,3,7,14,30], GetDueMasteryRows/UpdateSR repo methods, SpacedRepetitionSweepJob Hangfire fixed-ID, write-path hook in CompleteAttempt, GET /Reviews/Due endpoint) — committed
- **P3-01 (Backend)** — AI Gateway (IAiGateway seam, Ai module, Claude + second provider, task-based model router) — committed
- **P3-08 (Backend)** — Adaptivity Engine (weighted-score algorithm, 4-signal model, AdaptivityService seam, inspection endpoint + admin debug endpoint) — committed
- **P3-09 (Backend)** - Student mastery engine (StudentSkillMastery table + MasteryEngine + write/read paths + IMasteryService seam) - committed (Wave 1, PR #126)
- **2026-06-13 â€” P1/2/3 carryover (branch `feat/p1-p2-p3-carryover`):** gamification FE (all screens + TabBar + celebrations), Matching full-stack, parent Reports + attempt-history (both surfaces), auth messaging + CAPTCHA, parentâ†”child attempts authz; e2e 39/3-skip/0-fail; PR pending.
- **2026-06-13 - AI-phase + Phase-10 planning breakdown (PLANNING ONLY, all 🔲; PR #124 `docs/ai-phase-task-breakdown -> main`):** authored the full task breakdown for **Phase 4 - AI Tutor (P3-01..13)**, the **Curriculum-Intelligence backlog (BL-01..05)**, and the new **Phase 10 - Payment, Billing & Credits (P10-01..12)** - Pipeline Briefs (`docs/briefs/`) + Execution Plans (`docs/plans/`) + per-stack task files. **No code - build plan only.** Payments renumbered Phase 9 -> 10 (`main` owns Phase 9 = Notifications). Cross-cutting briefs: `ai-helper-mvp`, `ai-cost-routing`, `ai-eval-gate`, `curriculum-system-of-record`. Settled: new `Ai` + `Curriculum` + `Billing` modules, Claude provider w/ model routing, AI credit economy (Global Settings P10-12), Arabic stack (Azure DI + RAG-Anything). Chore PR #123 = subagent model tuning. See HANDOFF 2026-06-13 note for the full decision log.
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
| P3-02 | Filter AI output through a Safety Layer | ðŸ”² |
| P3-03 | Build personalized tutor prompts | ðŸ”² |
| P3-04 | Explain a concept on demand | ðŸ”² |
| P3-05 | Progressive hints & simpler re-explanations | ðŸ”² |
| P3-06 | Generate curriculum-grounded questions (RAG) | ðŸ”² |
| P3-07 | Retrieve curriculum context via vector search | ðŸ”² |
| P3-08 | Adjust difficulty adaptively | ✅ |
| P3-09 | Track per-skill mastery | ✅ |
| P3-10 | Schedule spaced-repetition practice | ðŸ”² |
| P3-11 | Serve adaptive quizzes | ðŸ”² |
| P3-12 | Interact with the AI tutor UI | ðŸ”² |
| P3-13 | Build the adaptive student profile | ðŸ”² |

## Phase 5 â€” Parent + Analytics
| Story | Title | Status |
|---|---|:--:|
| P5-01 | Generate a weekly student report | ðŸ”² |
| P5-02 | Detect and rank weak areas | ðŸ”² |
| P5-03 | Capture product analytics events | ðŸ”² |
| P5-04 | Deliver reports via notifications | ðŸ”² |
| P5-05 | View the parent dashboard | ðŸ”² |
| P5-06 | Transition a child to a new grade | ðŸ”² |

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
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P7-01 | Manage subjects & units | âœ… | ðŸ”² |
| P7-02 | Manage lessons & lesson content | âœ… | ðŸ”² |
| P7-03 | Author skills & the skill dependency graph | âœ… | ðŸ”² |
| P7-04 | Manage quizzes & questions | âœ… | ðŸ”² |
| P7-05 | Publish, version & preview curriculum content | âœ… | ðŸ”² |
| P7-06 | Search & inspect users | âœ… | ðŸ”² |
| P7-07 | Suspend, reactivate & delete accounts | âœ… | ðŸ”² |
| P7-08 | Manage child profiles & grade overrides | âœ… | ðŸ”² |
| P7-09 | Content moderation queue & review actions | ðŸ”² | ðŸ”² |
| P7-10 | Platform analytics & KPI dashboard | ðŸ”² | ðŸ”² |
| P7-11 | AI-safety & quality monitoring dashboard | ðŸ”² | ðŸ”² |
| P7-12 | Admin action audit log | âœ… | ðŸ”² |
| P7-13 | Gamification admin overrides (tier / badge & mission catalog / timed-event write / streak-freeze) | âœ… | ðŸ”² |

## Phase 8 â€” Localization
> Learning language (medium of instruction) vs UI language; bilingual curriculum as parallel ar/en trees. Design: `docs/architecture/localization-architecture.md`.
> **App-side localization FE wave** (tasks `tasks/Frontend/student-app/Phase-8-Localization/`): **P8-99-FE** app-shell foundation (fonts + persisted UI-language switch + RTL + api-client regen, incl. a durable NSwag `/Me` operationId fix) âœ… merged (PR #93); **P8-01-FE** (add-child learning-language field) âœ… merged (PR #94); **P8-04-FE** (parent change-learning-language UI, fresh-start warning) ðŸŸ¡ PR open on `feat/P8-04-FE`. Wave feature-complete.
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P8-01 | Set a child's learning language (parent-driven; JWT claim) | ðŸ”² | ðŸ”² |
| P8-02 | Author bilingual curriculum (SubjectCode + Language; parallel trees) | ðŸ”² | â€” |
| P8-03 | Serve curriculum in the student's learning language | ðŸ”² | â€” |
| P8-04 | Change a child's learning language (parent-only, fresh start) | âœ… | ðŸŸ¡ |

## Phase 10 - Payment, Billing & Credits *(story IDs `P10-xx`, post-MVP)*
> Task breakdown authored 2026-06-13 (PR #124) - **all ✅ not started; planning only.** AI credit economy ("⚡ طاقة المساعد") + monetization; **parent-driven** (web checkout, no native IAP); new `Billing` module owns the dual-pool ledger + subscriptions + payments; Global Settings (P10-12) makes the economy runtime-tunable. **Renumbered from Phase 9** (which `main` owns as **Notifications**) - files under `*/Phase-10-Payments-Billing/`. `P10-03` (spend) is hard-blocked on the AI Helper cluster (P3-01..06).
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
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

## Backlog (Phase 2+) â€” Curriculum Intelligence
| Story | Title | Status |
|---|---|:--:|
| BL-01 | Upload curriculum documents with metadata | ðŸ”² |
| BL-02 | Parse curriculum files (Multimodal Parsing) | ðŸ”² |
| BL-03 | Build & query the knowledge graph | ðŸ”² |
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

