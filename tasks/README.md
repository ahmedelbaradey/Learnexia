# Learnexia — Engineering Task Breakdown (Phase 1 & 2)

Task decomposition for every **Phase 1 (Foundation)** and **Phase 2 (Learning Core)** user story, **split by stack into separate files** so a Frontend agent and a Backend agent can each own one tree independently.

Frontend follows the **Turborepo monorepo** decision (see [../docs/dev/FRONTEND_ARCHITECTURE.md](../docs/dev/FRONTEND_ARCHITECTURE.md)): **Expo universal** for the student app, **Next.js** for admin/marketing (later phases), with shared `packages/`. All Phase 1–2 FE work lands in **student-app** + **packages** (admin/marketing are out of scope until their phases).

```
tasks/
├── Frontend/
│   ├── packages/              PKG-FOUNDATION-FE (monorepo, api-client, shared), P1-08-FE (design-system + ui)
│   ├── student-app/           Expo universal screens
│   │   ├── Phase-1-Foundation/    P1-xx-FE.md
│   │   ├── Phase-2-Learning-Core/ P2-xx-FE.md
│   │   ├── Phase-3-Gamification/  P4-xx-FE.md   (XP/streak/hearts/badges/missions/leagues/motion)
│   │   ├── Phase-4-AI-Tutor/      P3-12-FE.md   (AI tutor UI — chat/explain/hints surface, streaming, RTL)
│   │   ├── Phase-5-Parent-Analytics/ P5-xx-FE.md
│   │   ├── Phase-8-Localization/  P8-xx-FE.md   (add-child learning lang, parent change flow, app-shell fonts/RTL)
│   │   ├── Phase-9-Notifications/ P9-0x-FE.md   (expo push, deep links, in-app inbox, parent per-child controls)
│   │   └── Phase-10-Payments-Billing/ P10-xx-FE.md (parent: plan/checkout/packs/billing-history; child: ⚡ energy meter)
│   └── admin-dashboard/       Next.js admin screens
│       ├── Phase-1-Foundation/    P1-10-FE.md   (admin sign-in & shell)
│       ├── Phase-7-Admin-Console/ P7-xx-FE.md   (admin feature screens)
│       └── Phase-10-Payments-Billing/ P10-11-FE.md (billing config: plans/grants/action-costs)
└── Backend/
    ├── Phase-1-Foundation/   P1-xx-BE.md
    ├── Phase-2-Learning-Core/ P2-xx-BE.md
    ├── Phase-3-Gamification/ P4-xx-BE.md
    ├── Phase-4-AI-Tutor/     P3-xx-BE.md   (AI gateway/safety/prompt, RAG retrieval, explain/hints, adaptivity/mastery/SR/profile, Lexi recommendation narration P3-14 + P3-14a framing; P3-13a profile-depth = BACKLOG)
    ├── Phase-5-Parent-Analytics/ P5-xx-BE.md  (parent-scoped read API P5-08, weak-area detection P5-02, weekly report P5-01, recommendation engine P5-09 + P5-09a profile-aware selection, analytics event-capture backbone P5-03 [NEW Analytics module])
    ├── Phase-6-Stabilization/ P6-06-BE.md
    ├── Phase-7-Admin-Console/ P7-xx-BE.md
    ├── Phase-8-Localization/  P8-xx-BE.md
    ├── Phase-9-Notifications/ P9-0x-BE.md   (wire emitted events, new habit categories, arbitration, comeback ladder)
    ├── Phase-10-Payments-Billing/ P10-xx-BE.md  (credit ledger/grant/spend, subscriptions, payment provider, dunning/refunds, admin config)
    └── Backlog-Phase-2-Plus/ BL-xx-BE.md   (Curriculum Intelligence: schema/upload/parsing/ingestion/knowledge-graph — .NET + Python pipeline)
```

> **Scope:** this tree covers **Phase 1 & 2** stories, the **Phase 3 — Gamification** breakdown (`P4-xx`, both stacks), the **Phase 4 — AI Tutor** breakdown (`P3-xx`, all 13 stories), the **Phase 7 — Admin Console** feature breakdown (`P7-xx`), the **Phase 6 — `P6-06`** backend security-hardening pass (relocated from P1-13b), the **Phase 10 — Payment, Billing & Credits** breakdown (`P10-01..11`, both stacks — AI credit economy + monetization), plus the **Backlog (Phase 2+) — Curriculum Intelligence** breakdown (`BL-01..05`, .NET + Python pipeline). The barrier-to-entry stories **P4-09** (re-engagement notifications), **P4-10** (Redis realtime gamification) and **P4-11** (streak freeze / timed events) are decomposed in the Phase 3 tree below; **P3-13** (adaptive student profile, barrier-to-entry BE2) is now decomposed in the Phase 4 tree. The remaining barrier-to-entry story **P5-07** (data feedback / calibration) is **pending task breakdown** and will be decomposed when its phase tree is built. The Phase-2 story **P2-11** (skill dependency graph) is broken down here. See [../docs/briefs/barrier-to-entry-gap-analysis.md](../docs/briefs/barrier-to-entry-gap-analysis.md).
>
> **Phase order (resequenced):** Phase 3 = **Gamification** (`P4-xx`), Phase 4 = **AI Tutor** (`P3-xx`) — Gamification builds before AI Tutor; story IDs were kept stable so the prefix no longer equals the phase number. See [../user-stories/README.md](../user-stories/README.md).

- **Frontend agent** → work under [Frontend/](Frontend/): build `packages/` first (foundation + design-system/ui), then `student-app/` screens.
- **Backend agent** → work only under [Backend/](Backend/).
- Cross-stack dependencies are referenced by task ID across files (e.g. a FE task "blocked by P1-01-BE-1"); each FE screen task carries a **Target** column naming its monorepo location (`packages/*` or `apps/student-app`).

## Sources

- **Primary (source of truth):** the story `.md` files in [../user-stories/Phase-1-Foundation/](../user-stories/Phase-1-Foundation/) and [../user-stories/Phase-2-Learning-Core/](../user-stories/Phase-2-Learning-Core/). Scope, IDs, acceptance criteria, story points, and the **parent-driven onboarding** / **4-subjects** product decisions are preserved.
- **Detail:** [../docs/architecture.md](../docs/architecture.md) (modules, layers, routes, CQRS, Npgsql), [../docs/SRS.md](../docs/SRS.md) (FR-/NFR-, data model), [../docs/TASK_BREAKDOWN.md](../docs/TASK_BREAKDOWN.md), and [../info/](../info/) UI docs.

## Conventions

- **One file per story per stack:** `Frontend/<phase>/<story-id>-FE.md`, `Backend/<phase>/<story-id>-BE.md`.
- **Task IDs:** `<StoryID>-FE-n` / `<StoryID>-BE-n` — stable, referenceable across files.
- **Estimate unit:** **hours** (rough; not a re-estimate of the parent story's points).
- **Coverage:** each file maps the story's acceptance criteria to its tasks; criteria owned by the other stack are marked "(other stack)".
- Single-stack stories have **no file** in the other stack's tree (see coverage table).

## Stack (fixed — do not introduce other tech)

- **Backend:** .NET 10 modular monolith, ASP.NET Core, CQRS via MediatR, FluentValidation, AutoMapper, EF Core 10 on **Npgsql/PostgreSQL** (+ pgvector), Redis (`IDistributedCache`), JWT via ASP.NET Core Identity (int keys), `BaseResponse<T>` envelope. New domain modules: `learning`, `assessment` (replacing the Catalog demo).
- **Frontend:** **Turborepo monorepo**. Student app = **Expo universal** (SDK 53, RN 0.76+ New Arch, Expo Router, RN Web) → web PWA (390/768/1024) + iOS/Android from one codebase. Admin + marketing = **Next.js 15** (later phases). Shared UI via **Tamagui** (`packages/design-system` + `packages/ui`); data **TanStack Query v5**; client state **Zustand v5**; animation **Reanimated 3 + Moti + Skia**; i18n **react-i18next** with **RTL/Arabic** (Cairo/Tajawal) + English (Poppins). Typed API client + stores in `packages/api-client` + `packages/shared`. See [../docs/dev/FRONTEND_ARCHITECTURE.md](../docs/dev/FRONTEND_ARCHITECTURE.md).

> **Wireframe caveat:** the kids-UI wireframes are used for layout/screen structure only. They still show a *Teacher role*, *Social Studies*, and *student-driven role/grade selection* — all superseded by the story decisions (no teacher role, 4 subjects, parent-driven onboarding). Tasks follow the **stories**.

## Stack coverage per story

| Story | Title | Frontend | Backend |
|---|---|---|---|
| — | Monorepo, api-client & shared (foundation) | [FE](Frontend/packages/PKG-FOUNDATION-FE.md) | — |
| P1-01 | Register as a parent | [FE](Frontend/student-app/Phase-1-Foundation/P1-01-FE.md) | [BE](Backend/Phase-1-Foundation/P1-01-BE.md) |
| P1-02 | Stay signed in | [FE](Frontend/student-app/Phase-1-Foundation/P1-02-FE.md) | [BE](Backend/Phase-1-Foundation/P1-02-BE.md) |
| P1-03 | Parent onboarding & add children | [FE](Frontend/student-app/Phase-1-Foundation/P1-03-FE.md) | [BE](Backend/Phase-1-Foundation/P1-03-BE.md) |
| P1-04 | Link parent to child | [FE](Frontend/student-app/Phase-1-Foundation/P1-04-FE.md) | [BE](Backend/Phase-1-Foundation/P1-04-BE.md) |
| P1-05 | Role-based access control | — | [BE](Backend/Phase-1-Foundation/P1-05-BE.md) |
| P1-06 | PostgreSQL + pgvector + Redis | — | [BE](Backend/Phase-1-Foundation/P1-06-BE.md) |
| P1-07 | Docker & CI/CD | — | [BE](Backend/Phase-1-Foundation/P1-07-BE.md) |
| P1-08 | Design system & components | [FE](Frontend/packages/P1-08-FE.md) | — |
| P1-09 | Auth & onboarding screens | [FE](Frontend/student-app/Phase-1-Foundation/P1-09-FE.md) | [BE](Backend/Phase-1-Foundation/P1-09-BE.md) |
| P1-10 | Sign in to the admin dashboard | [FE](Frontend/admin-dashboard/Phase-1-Foundation/P1-10-FE.md) *(Next.js)* | [BE](Backend/Phase-1-Foundation/P1-10-BE.md) |
| P1-11 | Web app pages — pixel-perfect from screenshots | [FE](Frontend/student-app/Phase-1-Foundation/P1-11-FE.md) *(epic; Landing in marketing-site)* | — |
| P1-12 | Web account backend — profile/avatar/OAuth/reset/edit-child *(**Batch 2**, deferred)* | [FE](Frontend/student-app/Phase-1-Foundation/P1-12-FE.md) | [BE](Backend/Phase-1-Foundation/P1-12-BE.md) |
| P1-13a | Notifications email delivery *(enabler — built first; unblocks P1-12d & P5-04)* | — | [BE](Backend/Phase-1-Foundation/P1-13a-BE.md) |
| P1-13 | Backend hardening — lockout/sign-in safety/admin seed/CAPTCHA *(post-Batch-2 gap analysis)* | — | [BE](Backend/Phase-1-Foundation/P1-13-BE.md) |
| P1-13b | Backend hardening pass — BE-1 rate-limiting done (PR #50); rest → P6-06 | — | [BE](Backend/Phase-1-Foundation/P1-13b-BE.md) |
| P6-02 | Validate AI safety with an eval set *(✅ BE built — offline CI-native eval harness; closed the last P7-11 facet)* | — | [BE](Backend/Phase-6-Stabilization/P6-02-BE.md) |
| P6-06 | Backend security hardening — timing-oracle/email-locale/secrets/Redis rate-limit store *(Phase 6; relocated from P1-13b)* | — | [BE](Backend/Phase-6-Stabilization/P6-06-BE.md) |
| P2-01 | Model curriculum hierarchy | — | [BE](Backend/Phase-2-Learning-Core/P2-01-BE.md) |
| P2-02 | Browse subjects & lessons | [FE](Frontend/student-app/Phase-2-Learning-Core/P2-02-FE.md) | [BE](Backend/Phase-2-Learning-Core/P2-02-BE.md) |
| P2-03 | Navigate the skill tree | [FE](Frontend/student-app/Phase-2-Learning-Core/P2-03-FE.md) | [BE](Backend/Phase-2-Learning-Core/P2-03-BE.md) |
| P2-04 | Learning Path unlock rules | — | [BE](Backend/Phase-2-Learning-Core/P2-04-BE.md) |
| P2-05 | Open & complete a lesson | [FE](Frontend/student-app/Phase-2-Learning-Core/P2-05-FE.md) | [BE](Backend/Phase-2-Learning-Core/P2-05-BE.md) |
| P2-06 | Take a quiz (4 types) | [FE](Frontend/student-app/Phase-2-Learning-Core/P2-06-FE.md) | [BE](Backend/Phase-2-Learning-Core/P2-06-BE.md) |
| P2-07 | Instant answer feedback | [FE](Frontend/student-app/Phase-2-Learning-Core/P2-07-FE.md) | [BE](Backend/Phase-2-Learning-Core/P2-07-BE.md) |
| P2-08 | Record granular answers | — | [BE](Backend/Phase-2-Learning-Core/P2-08-BE.md) |
| P2-09 | Home dashboard | [FE](Frontend/student-app/Phase-2-Learning-Core/P2-09-FE.md) | [BE](Backend/Phase-2-Learning-Core/P2-09-BE.md) |
| P2-10 | Seed demo data | — | [BE](Backend/Phase-2-Learning-Core/P2-10-BE.md) |
| P2-11 | Author the skill dependency graph *(barrier-to-entry BE1)* | — | [BE](Backend/Phase-2-Learning-Core/P2-11-BE.md) |
| P2-12 | Parent account settings tabs *(carved from P1-11)* | [FE](Frontend/student-app/Phase-2-Learning-Core/P2-12-FE.md) | [BE](Backend/Phase-2-Learning-Core/P2-12-BE.md) |
| P5-05 | Parent dashboard charts + wire real analytics *(charts carved from P1-11)* | [FE](Frontend/student-app/Phase-5-Parent-Analytics/P5-05-FE.md) | — |

### Phase 3 — Gamification *(story IDs `P4-xx`)*

The Gamification module (`gamification` schema) reacting to the P4-01 learning domain events. **Backend XP/streak/hearts/badges are merged to `main`; missions (P4-06) are in progress; the rest of BE and all gamification FE are not started.** FE lands in `apps/student-app` reusing the shared `packages/`; **P4-08** is the dedicated screens-&-motion epic that the per-story FE widgets feed into.

> **Carry-over into this wave (Phase 1/2 gap closure):** [Backend/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-BE.md](Backend/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-BE.md) (quiz Matching type) + [Frontend/student-app/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-FE.md](Frontend/student-app/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-FE.md) (Reports, account-locked message, Register CAPTCHA, landing ar/RTL, Matching UI).

| Story | Title | Frontend | Backend | Status |
|---|---|---|---|---|
| P4-01 | Emit learning domain events *(technical enabler)* | — | [BE](Backend/Phase-3-Gamification/P4-01-BE.md) | BE ✅ |
| P4-02 | Earn XP and level up | [FE](Frontend/student-app/Phase-3-Gamification/P4-02-FE.md) | [BE](Backend/Phase-3-Gamification/P4-02-BE.md) | BE ✅ · FE 🔲 |
| P4-03 | Maintain a daily streak | [FE](Frontend/student-app/Phase-3-Gamification/P4-03-FE.md) | [BE](Backend/Phase-3-Gamification/P4-03-BE.md) | BE ✅ · FE 🔲 |
| P4-04 | Lose hearts and enter Practice Mode | [FE](Frontend/student-app/Phase-3-Gamification/P4-04-FE.md) | [BE](Backend/Phase-3-Gamification/P4-04-BE.md) | BE ✅ · FE 🔲 |
| P4-05 | Earn badges | [FE](Frontend/student-app/Phase-3-Gamification/P4-05-FE.md) | [BE](Backend/Phase-3-Gamification/P4-05-BE.md) | BE ✅ · FE 🔲 |
| P4-06 | Complete daily/weekly missions | [FE](Frontend/student-app/Phase-3-Gamification/P4-06-FE.md) | [BE](Backend/Phase-3-Gamification/P4-06-BE.md) | BE 🟡 · FE 🔲 |
| P4-07 | Compete in weekly leagues | [FE](Frontend/student-app/Phase-3-Gamification/P4-07-FE.md) | [BE](Backend/Phase-3-Gamification/P4-07-BE.md) | 🔲 |
| P4-08 | Gamification screens & motion *(FE-only epic)* | [FE](Frontend/student-app/Phase-3-Gamification/P4-08-FE.md) | — | FE 🔲 |
| P4-09 | Re-engagement notifications *(barrier-to-entry BE4; child-data sensitive)* | [FE](Frontend/student-app/Phase-3-Gamification/P4-09-FE.md) | [BE](Backend/Phase-3-Gamification/P4-09-BE.md) | 🔲 |
| P4-10 | Redis realtime gamification state *(barrier-to-entry BE3; enabler)* | — | [BE](Backend/Phase-3-Gamification/P4-10-BE.md) | 🔲 |
| P4-11 | Streak freeze, timed events & weekly challenges *(barrier-to-entry)* | [FE](Frontend/student-app/Phase-3-Gamification/P4-11-FE.md) | [BE](Backend/Phase-3-Gamification/P4-11-BE.md) | 🔲 |
| P4-12 | Timed-event participation *(per-child progress/completion + eligibility queries; unblocks P9-12)* | [FE](Frontend/student-app/Phase-3-Gamification/P4-12-FE.md) | [BE](Backend/Phase-3-Gamification/P4-12-BE.md) | BE ✅ · FE 🔲 |

### Phase 4 — AI Tutor *(story IDs `P3-xx`)*

The AI layer over the learning core. Three new seams: a new **`Ai`** module (gateway/safety/prompt — contracts in `Shared.Contracts/Ai/`), a new **`Curriculum`** module (chunks + pgvector + RAG retrieval), and **`learning`** extended for adaptivity/mastery/spaced-repetition/profile. **Nothing here is built — all tasks 🔲.** Build order: infra (P3-01→02→03) → AI Helper on the **seeded corpus** (P3-04‖05‖06) → UI (P3-12); RAG (P3-07→06 full) and the adaptivity chain (P3-09→08→11, with P3-10/P3-13 parallel) run alongside.

> **Three cross-cutting briefs govern this phase — read them first:**
> - [`docs/briefs/ai-helper-mvp.md`](../docs/briefs/ai-helper-mvp.md) — **"AI Helper, not AI Teacher":** four allowed intents (explain / hint / why-my-answer-is-wrong / similar-example), general use blocked, refuse-and-redirect when off-curriculum, the closed-loop completion metric, and the `ILearningContextProvider` seam (`SeededCorpusContextProvider` ships **now** → `RagContextProvider` is the later config-swap). **The Helper ships on the seeded verified-skills corpus in parallel with the BL pipeline — it is NOT gated behind ingestion.**
> - [`docs/briefs/ai-cost-routing.md`](../docs/briefs/ai-cost-routing.md) — offline-vs-runtime lanes, the `AiModelRouter` (cheap-default + escalate; Haiku classify / **Sonnet tutoring floor** / Opus offline-only), prompt caching, Batch API, per-plan quotas, cache-primary pre-generation.
> - [`docs/briefs/curriculum-system-of-record.md`](../docs/briefs/curriculum-system-of-record.md) — see the Backlog section below.
>
> **Pre-dispatch gates (lead must clear):** new `Ai` + `Curriculum` module approval (CLAUDE.md ask-before-new-modules — already approved for this breakdown), provider API keys provisioned, per-plan AI quota numbers, embedding model + vector dimension `N` fixed before the P3-07 migration, streaming wire format (SSE) pinned in HANDOFF before P3-12. Full blocker lists live in each `docs/plans/P3-xx.md`.

| Story | Title | Frontend | Backend |
|---|---|---|---|
| P3-01 | Route AI requests through an AI Gateway *(new `Ai` module; provider abstraction, retries, cost/usage)* | — | [BE](Backend/Phase-4-AI-Tutor/P3-01-BE.md) |
| P3-02 | Filter AI output through a Safety Layer *(no-bypass, block/regenerate, `ai.SafetyEvents`)* | — | [BE](Backend/Phase-4-AI-Tutor/P3-02-BE.md) |
| P3-03 | Build personalized tutor prompts *(grade/age/language/mastery; 4-subject templates)* | — | [BE](Backend/Phase-4-AI-Tutor/P3-03-BE.md) |
| P3-04 | Explain a concept on demand *(streaming, grounded, safety-screened)* | *(in P3-12)* | [BE](Backend/Phase-4-AI-Tutor/P3-04-BE.md) |
| P3-05 | Progressive hints & simpler re-explanations | *(in P3-12)* | [BE](Backend/Phase-4-AI-Tutor/P3-05-BE.md) |
| P3-06 | Generate curriculum-grounded questions (RAG) *(writes `QuizQuestion` Draft via Shared.Contracts)* | — | [BE](Backend/Phase-4-AI-Tutor/P3-06-BE.md) |
| P3-07 | Retrieve curriculum context via vector search *(new `Curriculum` module; pgvector top-k, seeded corpus)* | — | [BE](Backend/Phase-4-AI-Tutor/P3-07-BE.md) |
| P3-08 | Adjust difficulty adaptively *(extends `learning`; reads mastery)* | — | [BE](Backend/Phase-4-AI-Tutor/P3-08-BE.md) |
| P3-09 | Track per-skill mastery *(cumulative accuracy; cluster foundation)* | — | [BE](Backend/Phase-4-AI-Tutor/P3-09-BE.md) |
| P3-10 | Schedule spaced-repetition practice *(expanding ladder on `StudentSkillMastery`)* | — | [BE](Backend/Phase-4-AI-Tutor/P3-10-BE.md) |
| P3-11 | Serve adaptive quizzes *(difficulty-filtered `StartAttempt`)* | — | [BE](Backend/Phase-4-AI-Tutor/P3-11-BE.md) |
| P3-12 | Interact with the AI tutor UI *(Expo/Tamagui; designer → frontend → e2e)* | [FE](Frontend/student-app/Phase-4-AI-Tutor/P3-12-FE.md) | — |
| P3-13 | Build the adaptive student profile *(behavioral-only `StudentLearningProfile`; barrier-to-entry BE2)* | — | [BE](Backend/Phase-4-AI-Tutor/P3-13-BE.md) |

### Phase 7 — Admin Console *(post-MVP)*

Admin feature breakdown behind the P1-10 shell. **All FE work lands in `apps/admin-dashboard` (Next.js 15)**, reusing the shared `packages/` and the P1-10 admin shell. BE reuses the `learning`/`assessment`/`Identity` modules plus a new `Moderation` (governance) module for the moderation queue + audit log.

| Story | Title | Frontend | Backend |
|---|---|---|---|
| P7-01 | Manage subjects & units | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-01-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-01-BE.md) |
| P7-02 | Manage lessons & lesson content | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-02-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-02-BE.md) |
| P7-03 | Author skills & the skill dependency graph | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-03-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-03-BE.md) |
| P7-04 | Manage quizzes & questions | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-04-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-04-BE.md) |
| P7-05 | Publish, version & preview curriculum content | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-05-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-05-BE.md) |
| P7-06 | Search & inspect users | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-06-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-06-BE.md) |
| P7-07 | Suspend, reactivate & delete accounts | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-07-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-07-BE.md) |
| P7-08 | Manage child profiles & grade overrides | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-08-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-08-BE.md) |
| P7-09 | Content moderation queue & review actions | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-09-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-09-BE.md) |
| P7-10 | Platform analytics & KPI dashboard | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-10-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-10-BE.md) |
| P7-11 | AI-safety & quality monitoring dashboard | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-11-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-11-BE.md) |
| P7-11b | Streaming (SSE) AI usage capture *(backend-only; closes the P7-11 StreamAsync gap)* | — | [BE](Backend/Phase-7-Admin-Console/P7-11b-BE.md) |
| P7-12 | Admin action audit log | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-12-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-12-BE.md) |
| P7-13 | Gamification admin overrides *(tier / badge & mission catalog / timed-event write / streak-freeze)* | [FE](Frontend/admin-dashboard/Phase-7-Admin-Console/P7-13-FE.md) | [BE](Backend/Phase-7-Admin-Console/P7-13-BE.md) |

### Phase 8 — Localization

Learning language (medium of instruction) vs UI language; bilingual curriculum as parallel ar/en trees keyed on `Subject`. Backend is **merged to main** (P8-01/02/03 PR #90, P8-04 PR #91); the remaining work is the **app-side localization FE wave** (analyzer brief: [../docs/briefs/P8-localization-FE.md](../docs/briefs/P8-localization-FE.md)). **The FE wave is blocked on an api-client regeneration** (`refresh:swagger` → `gen:api`) — the committed Swagger snapshot predates the P8 contracts (no add-child `learningLanguage`, no `/Me` `learningLanguage`, no `Change-Learning-Language`). Design of record: [../docs/architecture/localization-architecture.md](../docs/architecture/localization-architecture.md).

| Story | Title | Frontend | Backend |
|---|---|---|---|
| P8-01 | Set a child's learning language *(parent-driven, JWT claim)* | [FE](Frontend/student-app/Phase-8-Localization/P8-01-FE.md) | [BE](Backend/Phase-8-Localization/P8-01-BE.md) |
| P8-02 | Author bilingual curriculum *(SubjectCode + Language; parallel trees)* | — | [BE](Backend/Phase-8-Localization/P8-02-BE.md) |
| P8-03 | Serve curriculum in the learning language *(read-path resolution)* | — | [BE](Backend/Phase-8-Localization/P8-03-BE.md) |
| P8-04 | Change a child's learning language *(parent-only, fresh start)* | [FE](Frontend/student-app/Phase-8-Localization/P8-04-FE.md) | [BE](Backend/Phase-8-Localization/P8-04-BE.md) |
| P8-99 | App-shell language foundation *(FE-only: api-client regen, fonts, UI-language persistence, RTL/i18n pass — folds in P6-03 FE-relevant)* | [FE](Frontend/student-app/Phase-8-Localization/P8-99-FE.md) | — |

### Phase 9 — Notifications *(post-MVP)*

Push end-to-end (Expo FE) + the full habit-forming notification catalog on the merged **P4-09** engine (`ExpoPushSender`, `DevicesController`, `NudgeDispatcher`, per-child preferences). **P9-01..04 are FE** (student-app: push, deep links, inbox, parent controls); **P9-05..12 are BE** (wire emitted events, new categories, arbitration + global budget, comeback ladder, analytics sink, timed-event nudges). **P9-05/06/07/08 merged to `main`.** P9-09 is **blocked by P3-10**; P9-12 **depends on P4-12**.

| Story | Title | Frontend | Backend |
|---|---|---|---|
| P9-01 | Turn on push notifications *(Expo permission + device token)* | [FE](Frontend/student-app/Phase-9-Notifications/P9-01-FE.md) | — |
| P9-02 | Notification tap routing + foreground + web fallback | [FE](Frontend/student-app/Phase-9-Notifications/P9-02-FE.md) | — |
| P9-03 | In-app notification inbox *(consumes existing InboxController)* | [FE](Frontend/student-app/Phase-9-Notifications/P9-03-FE.md) | — |
| P9-04 | Parent per-child notification controls | [FE](Frontend/student-app/Phase-9-Notifications/P9-04-FE.md) | — |
| P9-05 | Light up existing gamification events *(level-up, league, freeze, timed-event)* | — | [BE](Backend/Phase-9-Notifications/P9-05-BE.md) |
| P9-06 | New habit-loop categories *(✅ weekly recap + weekly-challenge ending-soon; streak-milestone dropped)* | — | [BE](Backend/Phase-9-Notifications/P9-06-BE.md) |
| P9-07 | Nudge arbitration + global daily push budget + cooldowns | — | [BE](Backend/Phase-9-Notifications/P9-07-BE.md) |
| P9-08 | Comeback escalation ladder *(day 2/5/14)* | — | [BE](Backend/Phase-9-Notifications/P9-08-BE.md) |
| P9-09 | Spaced-repetition review reminder *(✅ built — consumes P3-10 `ReviewDueIntegrationEvent`)* | — | [BE](Backend/Phase-9-Notifications/P9-09-BE.md) |
| P9-10 | Notification localization *(🟡 v1 — welcome localized; reset→P6-06, read-time→P9-03)* | — | [BE](Backend/Phase-9-Notifications/P9-10-BE.md) |
| P9-11 | Notification analytics sink *(✅ BE built — send/suppress/open → Analytics + admin endpoint)* | [FE](Frontend/admin-dashboard/Phase-9-Notifications/P9-11-FE.md) *(Next.js)* | [BE](Backend/Phase-9-Notifications/P9-11-BE.md) |
| P9-12 | Timed-event nudges *(✅ BE built — join/progress/ending/completion over P4-12)* | [FE](Frontend/student-app/Phase-9-Notifications/P9-12-FE.md) | [BE](Backend/Phase-9-Notifications/P9-12-BE.md) |

### Phase 10 — Payment, Billing & Credits *(story IDs `P10-xx`, post-MVP)*

The AI **credit economy** ("⚡ طاقة المساعد") + monetization. **Parent-driven: all purchasing/billing/payment is in the parent app/account** — the child only spends energy and sees a **read-only** meter (P10-10, the only student-app billing surface). Lives in a new **`Billing`** module (schema `billing`) owning the credit ledger (dual pool: monthly **granted**-expire vs **purchased**-persist), subscriptions, payments, and config; spend reaches the AI Gateway (P3-01) via a `Shared.Contracts` **`ICreditSpendService`** seam. **Charge-on-delivery**, cache-hits charged the same, no charge on refuse/error. Supersedes the AI-Helper MVP daily-cap guardrail. Parent billing FE lands in the **student-app parent area**; admin config FE in **admin-dashboard**. **Nothing built — all tasks 🔲.**

> **Family energy model (FINAL mid-cycle seat model, lead-approved 2026-06-17) — wave P10-13..18:** the P10-13..17 stories **re-home ownership** from the per-child `CreditAccount` dual-pool model (P10-01) onto a **parent/family `FamilyEnergyAccount`** with two non-convertible buckets — (A) subscription/entitlement (`PlanEnergyPerSeat × ActivePaidSeats`, allocated per active-seat child, resets each cycle) and (B) purchased packs (permanent shared family reserve). Per-child spend hits the child's **own allocation first** then the shared purchased row as fallback; seats define entitlement (P10-14). **Mid-cycle seat ADD/REACTIVATE: prorate MONEY only — no energy minted; mid-cycle energy via P10-16 wallet allocation only.** **Mid-cycle seat REMOVE/CANCEL (voluntary): effective at CYCLE END — no energy reclaim/forfeit/convert; no grace period triggered.** **7-day grace = payment-failure retry window at renewal boundary ONLY (not voluntary cancel/downgrade)**; on grace expiry limits enforce; children never deleted (P10-15); sibling-only redistribution + mid-cycle energy provision (P10-16); purchased-only refunds (P10-17); parent-control pause/unpause (P10-18 — real-time AI access gate only; zero billing/seat/energy side-effects). **This supersedes P10-01's ownership model — a data migration is required** (`credits.premium_monthly`/`credits.free_monthly` → `PlanEnergyPerSeat`). **Dependency order:** P10-13 (core) → P10-14 (seats) → P10-15 (lifecycle); P10-16 + P10-17 build on P10-13; P10-18 builds on P10-14 + P10-15. Security-auditor gate is mandatory on **P10-13, P10-14, P10-15, P10-16, P10-17, P10-18** (money + child data).

> **Pre-dispatch gates (lead must clear):** new `Billing` module approval (CLAUDE.md ask-before-new-modules), **payment provider Paymob vs Fawry** (P10-06/07), refund **clawback policy** (P10-09), EGP/VAT **receipt fields** (finance, P10-08), Hangfire for grant/renewal/dunning jobs, and provider keys/webhook secrets provisioning. **P10-03 (spend) is hard-blocked on the AI Helper cluster (P3-01..06) merging first.** Full blocker lists live in each `docs/plans/P10-xx.md`. Security-auditor gate is mandatory on **P10-03, P10-06, P10-07, P10-09** (and the P10-13..17 wave per above).

| Story | Title | Frontend | Backend |
|---|---|---|---|
| P10-01 | Credit (energy) account & ledger *(Technical Enabler — dual pool, append-only)* | — | [BE](Backend/Phase-10-Payments-Billing/P10-01-BE.md) |
| P10-02 | Grant monthly energy per plan *(Hangfire; granted-expire)* | — | [BE](Backend/Phase-10-Payments-Billing/P10-02-BE.md) |
| P10-03 | Spend energy on AI help *(charge-on-delivery; wires into P3-01)* | — | [BE](Backend/Phase-10-Payments-Billing/P10-03-BE.md) |
| P10-04 | Daily soft cap & low-energy warning *(bounded by monthly pool)* | — | [BE](Backend/Phase-10-Payments-Billing/P10-04-BE.md) |
| P10-05 | Manage subscription plan *(Free/Premium — parent)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-05-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-05-BE.md) |
| P10-06 | Pay for a subscription *(provider DECISION; recurring; security-sensitive)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-06-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-06-BE.md) |
| P10-07 | Buy an energy pack *(1000 credits / $5, never expires — parent → child)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-07-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-07-BE.md) |
| P10-08 | Billing history & receipts *(parent)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-08-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-08-BE.md) |
| P10-09 | Failed payments & refunds *(dunning + clawback)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-09-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-09-BE.md) |
| P10-10 | Kid-facing energy UI *(⚡ طاقة المساعد — read-only; distinct from hearts)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-10-FE.md) | — |
| P10-11 | Admin: configure plans, grants & action costs *(admin console)* | [FE](Frontend/admin-dashboard/Phase-10-Payments-Billing/P10-11-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-11-BE.md) |
| P10-12 | Runtime-configurable AI economy via Global Settings *(Technical Enabler — `IGlobalSettingsProvider`, DB-backed, Redis-cached, audited)* | — | [BE](Backend/Phase-10-Payments-Billing/P10-12-BE.md) |
| P10-13 | Family energy wallet & per-child allocation *(parent/family `FamilyEnergyAccount`; two non-convertible buckets; equal-split; child-first spend; supersedes `CreditAccount` + migration)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-13-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-13-BE.md) |
| P10-14 | Child seats & seat-reserved add-child *(included + extra paid seats; `PlanEnergyPerSeat × ActivePaidSeats`; webhook extra-seat purchase; add-child reserves a seat via `Shared.Contracts/Billing`)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-14-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-14-BE.md) |
| P10-15 | Seat enforcement, grace period & NoSeat/Locked child lifecycle *(grace → enforce; never delete children; over-limit → NoSeat/Locked, keep progress; parent chooses who keeps seats)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-15-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-15-BE.md) |
| P10-16 | Family energy redistribution & intra-family transfers *(parent moves unspent allocation sibling→sibling; family-only, zero-sum; paired immutable ledger)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-16-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-16-BE.md) |
| P10-17 | Refund reconciliation (unused purchased energy) *(purchased-only; refundable = purchased − consumed-purchased; ledger-reconciled; webhook-settled)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-17-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-17-BE.md) |
| P10-18 | Pause / unpause a child's access *(immediate parent-control toggle; blocks AI features only; no billing/seat/energy side-effects; `ParentPauseState` on Billing child record; dual independent spend-gate check with P10-15 `SeatState`; `IChildAccessStateQuery` combined seam; IDOR-security-auditor mandatory)* | [FE](Frontend/student-app/Phase-10-Payments-Billing/P10-18-FE.md) | [BE](Backend/Phase-10-Payments-Billing/P10-18-BE.md) |

### Backlog (Phase 2+) — Curriculum Intelligence *(story IDs `BL-xx`)*

The full OCR-driven curriculum pipeline, **deferred post-MVP** (P2-11 ships the hand-authored launch-bridge), built as a **system of record** — see [`docs/briefs/curriculum-system-of-record.md`](../docs/briefs/curriculum-system-of-record.md). Three stages: **Multimodal Parsing (BL-02) → Curriculum Ingestion (BL-05) → Knowledge Graph (BL-03)**, on the schema enabler **BL-04** behind the upload surface **BL-01**. Lives in the new **`Curriculum`** module (the *logical* owner of curriculum truth) and **reuses** the live P2-11 `KnowledgeNode`/`KnowledgeEdge` tables in `learning` (via `Shared.Contracts`, never relocated). Key model decisions baked in: a **provenance layer** (`ContentSource`/`Chapter`) distinct from the pedagogical tree; **immutable versioning** (`CurriculumVersion` Draft→Active switch) with a **stable `SkillKey`** so mastery survives re-publishing + version-aware retrieval/cache; a **separate versioned `chunk_embeddings` table** (BGE-M3, `vector(1024)`) for clean model migration; and the auto-extracted KG feeding a **review queue** (`KGSuggestion`) — only human/Claude-approved edges publish. **Both stacks in scope:** `-BE-n` tasks are .NET (orchestration/persistence/serve); `-PY-n` tasks are the Python pipeline (Azure Document Intelligence primary + MinerU/PaddleOCR fallback, RAG-Anything orchestration, LightRAG, BGE-M3 embeddings) — see each task file's second table. **Nothing built — all tasks 🔲.** Build order: **BL-04 → BL-01 → BL-02 → BL-05 → BL-03**.

> **Pre-dispatch gates (lead must clear):** vector dimension `N` + `EmbeddingVectorRef` semantics (BL-04), BL-02 trigger seam + file-type/size caps (BL-01), the .NET↔Python service boundary + Azure DI provisioning (BL-02/05/03), idempotency keys vs the P2-10 seed (BL-05), and `KnowledgeEdge` auto-build provenance (BL-03). Full blocker lists live in each `docs/plans/BL-xx.md`. BL-01's admin upload UI is a Phase-7-style admin surface — FE deferred (no FE task file).

| Story | Title | Frontend | Backend (.NET + Python) |
|---|---|---|---|
| BL-04 | Curriculum, knowledge-graph & vector schema *(Technical Enabler — `CurriculumChunk` + pgvector)* | — | [BE](Backend/Backlog-Phase-2-Plus/BL-04-BE.md) |
| BL-01 | Upload curriculum documents with metadata *(admin upload + ingestion queue; FE deferred to P7)* | *(deferred)* | [BE](Backend/Backlog-Phase-2-Plus/BL-01-BE.md) |
| BL-02 | Parse curriculum files into structured content *(Multimodal Parsing — OCR; Stage 1)* | — | [BE](Backend/Backlog-Phase-2-Plus/BL-02-BE.md) |
| BL-05 | Ingest parsed content into the curriculum hierarchy *(AI structuring + semantic chunking; Stage 2)* | — | [BE](Backend/Backlog-Phase-2-Plus/BL-05-BE.md) |
| BL-03 | Build & query the knowledge graph *(LightRAG; Neo4j Phase 3+; Stage 3)* | — | [BE](Backend/Backlog-Phase-2-Plus/BL-03-BE.md) |
