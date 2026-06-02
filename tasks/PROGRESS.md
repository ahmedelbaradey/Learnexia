# Learnexia — Build Progress Tracker

> Single source of truth for **what's done vs. not** across the whole backlog.
> Maintained automatically: the **`committer` agent updates this file on every commit** (flips the row for the story it just committed). The lead may also reconcile it after merges.
>
> Status reflects **merged to `main`** unless a row says otherwise.

## Legend
- ✅ **Done** — pipeline complete, reviewer PASS, committed, merged to `main`
- 🟡 **In progress** — pipeline running (branch exists, not yet merged)
- 🔲 **Not started**
- `—` — no work in this stack for this story (single-stack story)

## Recently completed (newest first)
- **Wave 14 (BE+FE):** P4-07 (weekly leagues � Phase 3 Gamification 6th story: AddLeagueAndLeagueMembership migration (Leagues + LeagueMemberships + LeagueXpDeltaLogs + StudentXpProfile.CurrentTier + MembershipStatus enum) + StudentXpProfile.ApplyAward 4-arg refactor to single XP chokepoint (amount/newLevel/reason/occurredAtUtc) raising XpAwardedDomainEvent + LeagueStandings pure static (ComputeCutoffs handles tier extremes + small-cohort scaling, Apply assigns ranks/status/tierAfter) + StudentXpProfile.UpdateTier mutation + LeagueOptions config (CohortSize=30/PromoteCount=7/DemoteCount=5/cron=15 0 * * 1 UTC Monday) + IncrementLeagueXpCommand (period key derived from request.OccurredAtUtc for week-boundary correctness, dual-layer idempotency via LeagueXpDeltaLog unique index, narrowed DbUpdateException catch, no-op when no membership) + XpAwardedLeagueHandler (in own try/catch per ADR 0002 �3) + LeaguePlacementService Infrastructure (find-or-create cohort + insert membership with graph-nav AttachLeague) + IStudentLeagueQuery cross-module seam with LAZY INSTANTIATION on dashboard read + LeagueTierDto drift enum in Shared.Contracts + DashboardDto.LeaguePreview wired + GET /api/Gamification/Leagues/Me endpoint with "Student #N" anonymization + LeagueRolloverJob Hangfire Monday 00:15 UTC (after streak sweep + mission rollover); FE: LeaguePreviewRow inline component + EN/AR i18n + dashboard wire-up; lead-approved ApplyAward chokepoint refactor + Student #N anonymization + top-7/bottom-5 cutoffs + endpoint+FE bundled; 27 LeagueStandings unit + 4 enum drift unit + 23 integration tests + 85/85 P4-02..P4-06 regression = 108/108 full P4 suite; security PASS 0 blocking; reviewer-fixes-applied: periodKey derived from OccurredAtUtc + stale TODOs removed; accepted MVP risks: R1 cohort overfill / D15 XP-before-dashboard / JoinOrder collision / XpAwardedDomainEvent retry ghost; deferred to P4-08 UI motion / P4-09 nudges / P4-10 Redis / P7 admin tier override; LeaguePlacementServiceTests deferred (integration-covered)) � open as PR on feat/P4-07-weekly-leagues
- **Wave 13 (BE):** P4-06 (daily/weekly missions � Phase 3 Gamification 5th story: AddMissionDefinitionStudentMissionProgressLog migration (MissionDefinitions catalog + StudentMissions per-period instance + MissionProgressLogs idempotency ledger) + XpReason.MissionCompleted=6 + MissionTargetType enum + MissionPeriodCalculator pure static UTC + ISO 8601 week math + StudentXpProfile.RecordMissionCompleted domain mutation raising MissionCompletedDomainEvent + StudentMission.ApplyProgress/MarkCompleted mutations + IncrementMissionProgressCommand (row-lock after probe, dual-layer idempotency, inline completion to avoid nested-tx) + 3 notification handlers (LessonCompletedMissionHandler/AnswerSubmittedMissionHandler/StreakAdvancedMissionHandler, each in own try/catch per ADR 0002 �3) + MissionSeeder idempotent atomic seed of 8 missions at startup + IStudentMissionsQuery cross-module seam with LAZY INSTANTIATION on dashboard read + DashboardDto.DailyMissions[] + WeeklyMission (replaces old DailyMission placeholder) + GET /api/Gamification/Missions/Me endpoint + MissionStatusDto/MissionTargetTypeDto/MissionTypeDto drift enums in Shared.Contracts + MissionRolloverJob Hangfire @ 5 0 * * * daily + 10 0 * * 1 weekly bulk ExecuteUpdateAsync; lead-approved 8 missions/lazy/PM-counts/daily-list+weekly-single + graph-nav 4th instance (AttachStudentMission); 19 unit tests + 23 integration tests + 62/62 P4-02/03/04/05 regression; security PASS with F1 comment + F2 narrowed catch + F3 DTO enums + F5 lock placement + reviewer F2-cleanup applied) � open as PR on feat/P4-06-missions

## Phase 1 — Foundation
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| — | Monorepo, api-client & shared (foundation) | — | ✅ |
| P1-01 | Register as a parent | ✅ | 🔲 |
| P1-02 | Stay signed in (token refresh & sign-out) | ✅ | 🔲 |
| P1-03 | Parent onboarding & add children | ✅ | 🔲 |
| P1-04 | Link a parent to a child account | ✅ | 🔲 |
| P1-05 | Role-based access control | ✅ | — |
| P1-06 | PostgreSQL + pgvector + Redis | ✅ | — |
| P1-07 | Dockerized environment & CI/CD | ✅ | — |
| P1-08 | Design system & components (RTL) | — | ✅ |
| P1-09 | Auth & onboarding screens | ✅ | ✅ |
| P1-10 | Sign in to the admin dashboard | ✅ | ✅ |
| P1-11 | Web app pages (pixel-perfect, parent web) | — | 🟡 |
| P1-12 | Web account backend (Batch 2) — profile/Me, register consent, edit-child, avatar (MinIO), Google sign-in, password reset | ✅ | 🔲 |
| P1-12b | IUserLookup cross-module seam | ✅ | — |
| P1-13a | Notifications email delivery (enabler) | ✅ | — |
| P1-13 | Backend hardening (lockout/sign-in/admin seed/CAPTCHA) | ✅ | — |
| P1-13b | Backend hardening pass — BE-1 rate-limiting (PR #50); rest → P6-06 | ✅ | — |

## Phase 2 — Learning Core
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P2-01 | Model the curriculum hierarchy | ✅ | — |
| P2-02 | Browse subjects and lessons | 🟡 | ✅ |
| P2-03 | Navigate the skill tree | 🟡 | ✅ |
| P2-04 | Unlock lessons by prerequisite/mastery | 🟡 | — |
| P2-05 | Open and complete a lesson | ✅ | 🔲 |
| P2-06 | Take a quiz (4 question types) | ✅ | 🔲 |
| P2-07 | Get instant answer feedback | 🟡 | 🔲 |
| P2-08 | Record granular per-question answers | 🟡 | — |
| P2-09 | See the home dashboard | ✅ | 🔲 |
| P2-10 | Seed demo subjects & skill trees | ✅ | — |
| P2-11 | Author the skill dependency graph (relational, hand-authored) | 🟡 | — |
| P2-12 | Account settings APIs (Parent module + Notifications prefs + Identity security) | ✅ | ✅ |

## Phase 3 — Gamification *(story IDs `P4-xx`)*
> Backend XP/streak/hearts/badges shipped; **all gamification FE is not started** (task tree added under `tasks/Frontend/student-app/Phase-3-Gamification/`).
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P4-01 | Emit learning domain events | ✅ | — |
| P4-02 | Earn XP and level up | ✅ | 🔲 |
| P4-03 | Maintain a daily streak | ✅ | 🔲 |
| P4-04 | Lose hearts and enter Practice Mode | ✅ | 🔲 |
| P4-05 | Earn badges | ✅ | 🔲 |
| P4-06 | Complete daily/weekly missions | 🟡 | 🔲 |
| P4-07 | Compete in weekly leagues | 🟡 | 🟡 |
| P4-08 | Gamification screens & motion | — | 🔲 |
| P4-09 | Re-engagement notifications | 🔲 | 🔲 |
| P4-10 | Redis realtime gamification state | 🔲 | — |
| P4-11 | Streak freeze, timed events & weekly challenges | 🔲 | 🔲 |

## Phase 4 — AI Tutor *(story IDs `P3-xx`)*
| Story | Title | Status |
|---|---|:--:|
| P3-01 | Route AI requests through an AI Gateway | 🔲 |
| P3-02 | Filter AI output through a Safety Layer | 🔲 |
| P3-03 | Build personalized tutor prompts | 🔲 |
| P3-04 | Explain a concept on demand | 🔲 |
| P3-05 | Progressive hints & simpler re-explanations | 🔲 |
| P3-06 | Generate curriculum-grounded questions (RAG) | 🔲 |
| P3-07 | Retrieve curriculum context via vector search | 🔲 |
| P3-08 | Adjust difficulty adaptively | 🔲 |
| P3-09 | Track per-skill mastery | 🔲 |
| P3-10 | Schedule spaced-repetition practice | 🔲 |
| P3-11 | Serve adaptive quizzes | 🔲 |
| P3-12 | Interact with the AI tutor UI | 🔲 |
| P3-13 | Build the adaptive student profile | 🔲 |

## Phase 5 — Parent + Analytics
| Story | Title | Status |
|---|---|:--:|
| P5-01 | Generate a weekly student report | 🔲 |
| P5-02 | Detect and rank weak areas | 🔲 |
| P5-03 | Capture product analytics events | 🔲 |
| P5-04 | Deliver reports via notifications | 🔲 |
| P5-05 | View the parent dashboard | 🔲 |
| P5-06 | Transition a child to a new grade | 🔲 |

## Phase 6 — Stabilization
| Story | Title | Status |
|---|---|:--:|
| P6-01 | Meet API & AI performance targets | 🔲 |
| P6-02 | Validate AI safety with an eval set | 🔲 |
| P6-03 | Pass localization & RTL review | 🔲 |
| P6-04 | Regression, prompt-tuning & bug triage | 🔲 |
| P6-05 | Observability: logging, tracing, dashboards | 🔲 |
| P6-06 | Backend security hardening (timing-oracle/email-locale/secrets/Redis rate-limit) | 🔲 |

## Phase 7 — Admin Console *(post-MVP)*
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P7-01 | Manage subjects & units | 🔲 | 🔲 |
| P7-02 | Manage lessons & lesson content | 🔲 | 🔲 |
| P7-03 | Author skills & the skill dependency graph | 🔲 | 🔲 |
| P7-04 | Manage quizzes & questions | 🔲 | 🔲 |
| P7-05 | Publish, version & preview curriculum content | 🔲 | 🔲 |
| P7-06 | Search & inspect users | 🔲 | 🔲 |
| P7-07 | Suspend, reactivate & delete accounts | 🔲 | 🔲 |
| P7-08 | Manage child profiles & grade overrides | 🔲 | 🔲 |
| P7-09 | Content moderation queue & review actions | 🔲 | 🔲 |
| P7-10 | Platform analytics & KPI dashboard | 🔲 | 🔲 |
| P7-11 | AI-safety & quality monitoring dashboard | 🔲 | 🔲 |
| P7-12 | Admin action audit log | 🔲 | 🔲 |

## Backlog (Phase 2+) — Curriculum Intelligence
| Story | Title | Status |
|---|---|:--:|
| BL-01 | Upload curriculum documents with metadata | 🔲 |
| BL-02 | Parse curriculum files (Multimodal Parsing) | 🔲 |
| BL-03 | Build & query the knowledge graph | 🔲 |
| BL-04 | Curriculum, KG & vector schema | 🔲 |
| BL-05 | Ingest parsed content into hierarchy | 🔲 |

---

## Deferred / follow-up debt (not blocking; track for a hardening pass)
- Anti-automation (rate-limit/CAPTCHA) on anonymous registration — P1-01
- `RoleHelper` legacy lowercase-constant cleanup — Identity
- Remove `DEMO_PgvectorProof` migration when the real embedding table lands — P1-06
- Container non-root image, CI action SHA-pinning, staging TLS cert — P1-07
- Tokenize inline glow/alpha shades in components — P1-08
- **Open decision:** staging deploy provider (Azure / Railway / Render) — see `docs/deploy/staging-decision.md`
