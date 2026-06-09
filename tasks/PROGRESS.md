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
- **2026-06-06 â€” FE status reconciliation:** board corrected against `main` ground truth â€” Phase-1 FE (P1-01/02/03/04) and Phase-2 student FE (P2-05/06/07/09, merged via PR #70/#71/#72/#74) flipped ðŸ”²â†’âœ…; **P8-04 FE corrected âœ…â†’ðŸ”² (branch `feat/P8-04` was backend-only â€” no FE shipped)**. Open-WIP FE branches: `feat/P4-08-gamification-screens-motion` (resumable), `feat/design-system-pixel-align` (stale, holds font/RTL fixes).
- **P8-04 (BE only):** Change a child's learning language (parent-only, fresh start) â€” backend merged; **parent FE not built** (carry-forward).
- **Wave 14 (BE+FE):** P4-07 (weekly leagues ï¿½ Phase 3 Gamification 6th story: AddLeagueAndLeagueMembership migration (Leagues + LeagueMemberships + LeagueXpDeltaLogs + StudentXpProfile.CurrentTier + MembershipStatus enum) + StudentXpProfile.ApplyAward 4-arg refactor to single XP chokepoint (amount/newLevel/reason/occurredAtUtc) raising XpAwardedDomainEvent + LeagueStandings pure static (ComputeCutoffs handles tier extremes + small-cohort scaling, Apply assigns ranks/status/tierAfter) + StudentXpProfile.UpdateTier mutation + LeagueOptions config (CohortSize=30/PromoteCount=7/DemoteCount=5/cron=15 0 * * 1 UTC Monday) + IncrementLeagueXpCommand (period key derived from request.OccurredAtUtc for week-boundary correctness, dual-layer idempotency via LeagueXpDeltaLog unique index, narrowed DbUpdateException catch, no-op when no membership) + XpAwardedLeagueHandler (in own try/catch per ADR 0002 ï¿½3) + LeaguePlacementService Infrastructure (find-or-create cohort + insert membership with graph-nav AttachLeague) + IStudentLeagueQuery cross-module seam with LAZY INSTANTIATION on dashboard read + LeagueTierDto drift enum in Shared.Contracts + DashboardDto.LeaguePreview wired + GET /api/Gamification/Leagues/Me endpoint with "Student #N" anonymization + LeagueRolloverJob Hangfire Monday 00:15 UTC (after streak sweep + mission rollover); FE: LeaguePreviewRow inline component + EN/AR i18n + dashboard wire-up; lead-approved ApplyAward chokepoint refactor + Student #N anonymization + top-7/bottom-5 cutoffs + endpoint+FE bundled; 27 LeagueStandings unit + 4 enum drift unit + 23 integration tests + 85/85 P4-02..P4-06 regression = 108/108 full P4 suite; security PASS 0 blocking; reviewer-fixes-applied: periodKey derived from OccurredAtUtc + stale TODOs removed; accepted MVP risks: R1 cohort overfill / D15 XP-before-dashboard / JoinOrder collision / XpAwardedDomainEvent retry ghost; deferred to P4-08 UI motion / P4-09 nudges / P4-10 Redis / P7 admin tier override; LeaguePlacementServiceTests deferred (integration-covered)) ï¿½ open as PR on feat/P4-07-weekly-leagues
- **Wave 13 (BE):** P4-06 (daily/weekly missions ï¿½ Phase 3 Gamification 5th story: AddMissionDefinitionStudentMissionProgressLog migration (MissionDefinitions catalog + StudentMissions per-period instance + MissionProgressLogs idempotency ledger) + XpReason.MissionCompleted=6 + MissionTargetType enum + MissionPeriodCalculator pure static UTC + ISO 8601 week math + StudentXpProfile.RecordMissionCompleted domain mutation raising MissionCompletedDomainEvent + StudentMission.ApplyProgress/MarkCompleted mutations + IncrementMissionProgressCommand (row-lock after probe, dual-layer idempotency, inline completion to avoid nested-tx) + 3 notification handlers (LessonCompletedMissionHandler/AnswerSubmittedMissionHandler/StreakAdvancedMissionHandler, each in own try/catch per ADR 0002 ï¿½3) + MissionSeeder idempotent atomic seed of 8 missions at startup + IStudentMissionsQuery cross-module seam with LAZY INSTANTIATION on dashboard read + DashboardDto.DailyMissions[] + WeeklyMission (replaces old DailyMission placeholder) + GET /api/Gamification/Missions/Me endpoint + MissionStatusDto/MissionTargetTypeDto/MissionTypeDto drift enums in Shared.Contracts + MissionRolloverJob Hangfire @ 5 0 * * * daily + 10 0 * * 1 weekly bulk ExecuteUpdateAsync; lead-approved 8 missions/lazy/PM-counts/daily-list+weekly-single + graph-nav 4th instance (AttachStudentMission); 19 unit tests + 23 integration tests + 62/62 P4-02/03/04/05 regression; security PASS with F1 comment + F2 narrowed catch + F3 DTO enums + F5 lock placement + reviewer F2-cleanup applied) ï¿½ open as PR on feat/P4-06-missions

## Phase 1 â€” Foundation
> **Per-task detail added 2026-06-07:** each P1 task file now carries a Status column (âœ…/ðŸŸ¡/ðŸ”²). Story-level cells below are unchanged. Open sub-task gaps inside otherwise-shipped stories: **P1-10-FE-6** (admin account-locked message) ðŸ”², **P1-11-FE-15/16** (sign-in lockout msg + Register CAPTCHA) ðŸ”², **P1-12-FE-*** Batch-2 wiring ðŸŸ¡ + **FE-4** forgot/reset-password screens ðŸ”². Their backend deps (P1-12-BE, P1-13-BE-1/2/4) are merged, so all are unblocked.
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
> **Per-task detail added 2026-06-07:** each P2 task file now carries a Status column (âœ…/ðŸŸ¡/ðŸ”²). Story-level cells below are unchanged. One open sub-task gap inside an otherwise-shipped story: **quiz Matching** is incomplete on both stacks â€” **P2-06-FE-2** ðŸŸ¡ (UI stub) and **P2-06-BE-3** ðŸŸ¡ (answer-shape TODO, unseeded). Note: P2-06's "assessment module" was deliberately folded into the **Learning** module per the no-new-module decision.
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
> Backend XP/streak/hearts/badges/missions/leagues shipped. **Gamification FE is mostly not started** â€” only the P4-07 dashboard LeaguePreview flip is merged (ðŸŸ¡), and P4-08 motion/screens is unmerged WIP on `feat/P4-08-gamification-screens-motion`. Task tree under `tasks/Frontend/student-app/Phase-3-Gamification/`.
> **Carry-over (Phase 1/2 gap closure scheduled into this wave):** `Backend/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-BE.md` (quiz Matching type) + `Frontend/student-app/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-FE.md` (Reports build, account-locked message, Register CAPTCHA, landing ar/RTL, Matching UI).
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P4-01 | Emit learning domain events | âœ… | â€” |
| P4-02 | Earn XP and level up | âœ… | ðŸ”² |
| P4-03 | Maintain a daily streak | âœ… | ðŸ”² |
| P4-04 | Lose hearts and enter Practice Mode | âœ… | ðŸ”² |
| P4-05 | Earn badges | âœ… | ðŸ”² |
| P4-06 | Complete daily/weekly missions | âœ… | ðŸ”² |
| P4-07 | Compete in weekly leagues | âœ… | ðŸŸ¡ |
| P4-08 | Gamification screens & motion | â€” | ðŸ”² |
| P4-09 | Re-engagement notifications | âœ… | ðŸ”² |
| P4-10 | Redis realtime gamification state | âœ… | â€” |
| P4-11 | Streak freeze, timed events & weekly challenges | âœ… | ðŸ”² |

## Phase 4 â€” AI Tutor *(story IDs `P3-xx`)*
| Story | Title | Status |
|---|---|:--:|
| P3-01 | Route AI requests through an AI Gateway | ðŸ”² |
| P3-02 | Filter AI output through a Safety Layer | ðŸ”² |
| P3-03 | Build personalized tutor prompts | ðŸ”² |
| P3-04 | Explain a concept on demand | ðŸ”² |
| P3-05 | Progressive hints & simpler re-explanations | ðŸ”² |
| P3-06 | Generate curriculum-grounded questions (RAG) | ðŸ”² |
| P3-07 | Retrieve curriculum context via vector search | ðŸ”² |
| P3-08 | Adjust difficulty adaptively | ðŸ”² |
| P3-09 | Track per-skill mastery | ðŸ”² |
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

