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
│   │   ├── Phase-5-Parent-Analytics/ P5-xx-FE.md
│   │   ├── Phase-8-Localization/  P8-xx-FE.md   (add-child learning lang, parent change flow, app-shell fonts/RTL)
│   │   └── Phase-9-Notifications/ P9-0x-FE.md   (expo push, deep links, in-app inbox, parent per-child controls)
│   └── admin-dashboard/       Next.js admin screens
│       ├── Phase-1-Foundation/    P1-10-FE.md   (admin sign-in & shell)
│       └── Phase-7-Admin-Console/ P7-xx-FE.md   (admin feature screens)
└── Backend/
    ├── Phase-1-Foundation/   P1-xx-BE.md
    ├── Phase-2-Learning-Core/ P2-xx-BE.md
    ├── Phase-3-Gamification/ P4-xx-BE.md
    ├── Phase-6-Stabilization/ P6-06-BE.md
    ├── Phase-7-Admin-Console/ P7-xx-BE.md
    ├── Phase-8-Localization/  P8-xx-BE.md
    └── Phase-9-Notifications/ P9-0x-BE.md   (wire emitted events, new habit categories, arbitration, comeback ladder)
```

> **Scope:** this tree covers **Phase 1 & 2** stories, the **Phase 3 — Gamification** breakdown (`P4-xx`, both stacks), the **Phase 7 — Admin Console** feature breakdown (`P7-xx`), plus the **Phase 6 — `P6-06`** backend security-hardening pass (relocated from P1-13b). The barrier-to-entry stories **P4-09** (re-engagement notifications), **P4-10** (Redis realtime gamification) and **P4-11** (streak freeze / timed events) are now decomposed in the Phase 3 tree below. The remaining barrier-to-entry stories — **P3-13** (adaptive student profile) and **P5-07** (data feedback / calibration) — are **pending task breakdown** and will be decomposed when their phase trees are built. The Phase-2 story **P2-11** (skill dependency graph) is broken down here. See [../docs/briefs/barrier-to-entry-gap-analysis.md](../docs/briefs/barrier-to-entry-gap-analysis.md).
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
