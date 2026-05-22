# Learnexia — Engineering Task Breakdown (Phase 1 & 2)

Task decomposition for every **Phase 1 (Foundation)** and **Phase 2 (Learning Core)** user story, **split by stack into separate files** so a Frontend agent and a Backend agent can each own one tree independently.

Frontend follows the **Turborepo monorepo** decision (see [../docs/dev/FRONTEND_ARCHITECTURE.md](../docs/dev/FRONTEND_ARCHITECTURE.md)): **Expo universal** for the student app, **Next.js** for admin/marketing (later phases), with shared `packages/`. All Phase 1–2 FE work lands in **student-app** + **packages** (admin/marketing are out of scope until their phases).

```
tasks/
├── Frontend/
│   ├── packages/              PKG-FOUNDATION-FE (monorepo, api-client, shared), P1-08-FE (design-system + ui)
│   ├── student-app/           Expo universal screens
│   │   ├── Phase-1-Foundation/    P1-xx-FE.md
│   │   └── Phase-2-Learning-Core/ P2-xx-FE.md
│   └── admin-dashboard/       Next.js admin screens
│       └── Phase-1-Foundation/    P1-10-FE.md   (admin sign-in & shell)
└── Backend/
    ├── Phase-1-Foundation/   P1-xx-BE.md
    └── Phase-2-Learning-Core/ P2-xx-BE.md
```

> **Scope:** this tree covers **Phase 1 & 2** stories only. The barrier-to-entry stories added in Phase 3–5 — **P3-13** (adaptive student profile), **P4-09** (re-engagement notifications), **P4-10** (Redis realtime gamification), **P4-11** (streak freeze / timed events), **P5-07** (data feedback / calibration) — are **pending task breakdown** and will be decomposed when their phase trees are built. The Phase-2 story **P2-11** (skill dependency graph) is broken down here. See [../docs/briefs/barrier-to-entry-gap-analysis.md](../docs/briefs/barrier-to-entry-gap-analysis.md).

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
