# Learnexia

An AI-powered, gamified, adaptive learning platform for Arabic-speaking primary & middle-school students. The product turns school learning into an engaging, game-like, reward-driven experience: AI tutoring, skill-tree learning paths, adaptive difficulty, and Duolingo-style gamification (XP, streaks, hearts, badges, missions, leagues), with a parent dashboard.

> Guiding principle: *success comes from habit loops, gamification, emotional design, and personalized learning — not the biggest AI model.*

## Repository layout

| Path | What |
|---|---|
| [backend/](backend/) | **.NET 10 modular monolith** (`Learnexia.Modular.sln`) — EF Core on **PostgreSQL** (Npgsql), MediatR CQRS, ASP.NET Identity + JWT. Modules: Identity, Catalog (reference/demo), Notifications. |
| [design-system/](design-system/) | Design-system-as-code: token + component HTML previews, SVG logo/mascot/icons, Poppins/Tajawal fonts, and `ui_kits/` for per-screen design specs. |
| [docs/](docs/) | Architecture, product specs, dev playbooks, ADRs, briefs & plans. |
| [tasks/](tasks/) | Per-stack engineering task breakdown (`Backend/`, `Frontend/`), one file per story. |
| [user-stories/](user-stories/) | **Source of truth** for scope — one story per file, organized by phase/sprint. |
| [info/](info/) | Source research: product, AI/architecture, curriculum, and UI/design material. |
| [docker/](docker/) | Compose stack (PostgreSQL + Redis + MinIO + API). |
| [.claude/](.claude/) | Multi-agent dev pipeline (agent definitions + skills). |

## Tech stack

- **Backend:** .NET 10, ASP.NET Core, modular monolith, MediatR (CQRS), FluentValidation, AutoMapper, EF Core 10 on **PostgreSQL** (+ pgvector planned for RAG), Redis (`IDistributedCache`), JWT via ASP.NET Identity. Uniform `BaseResponse<T>` API envelope.
- **Frontend (not started):** **Turborepo** monorepo — **Expo universal** student app (web PWA + iOS/Android), **Tamagui**, TanStack Query + Zustand, Reanimated/Skia, react-i18next (Arabic-first + RTL). Next.js for admin/marketing in later phases. See [docs/dev/FRONTEND_ARCHITECTURE.md](docs/dev/FRONTEND_ARCHITECTURE.md).

## Key documents

- **Architecture of record:** [docs/architecture.md](docs/architecture.md)
- **Product:** [docs/BRD.md](docs/BRD.md) · [docs/SRS.md](docs/SRS.md) · [docs/BUSINESS_PLAN.md](docs/BUSINESS_PLAN.md) · [docs/TASK_BREAKDOWN.md](docs/TASK_BREAKDOWN.md)
- **How to build (backend):** [docs/dev/FEATURE_PLAYBOOK.md](docs/dev/FEATURE_PLAYBOOK.md) · [docs/dev/CONVENTIONS.md](docs/dev/CONVENTIONS.md) · [docs/dev/CODE_TEMPLATES.md](docs/dev/CODE_TEMPLATES.md)
- **Decisions:** [docs/dev/adr/](docs/dev/adr/)

## Product decisions (override older source docs)

- **Parent-driven onboarding** — parents register and add children; students don't self-register.
- **4 subjects** — Math, Science, Arabic, English. *(No Social Studies.)*
- **No teacher role.**
- **Grade transition** preserves history (XP/badges/streaks/mastery).

## Getting started (backend)

```bash
# 1. Start infrastructure (PostgreSQL + Redis + MinIO)
docker compose -f docker/docker-compose.yaml up -d   # NOTE: compose currently provisions SQL Server — see docs/architecture.md §10

# 2. Apply migrations (per module, against PostgreSQL)
cd backend
dotnet ef database update --project src/Modules/Identity/Learnexia.Modules.Identity.Infrastructure --startup-project src/Host/Learnexia.Host

# 3. Run
dotnet run --project src/Host/Learnexia.Host
```

Connection string `Default` points at `Host=localhost;Port=5432;Database=Learnexia`. Browse design tokens/components by opening the HTML files in [design-system/preview/](design-system/preview/).

> ⚠️ Known gaps (tracked in [docs/architecture.md](docs/architecture.md)): docker-compose still provisions SQL Server (app uses PostgreSQL), `design-system/colors_and_type.css` is missing (previews import it), and Cairo font isn't shipped (only Tajawal).

## Multi-agent development pipeline

Development runs through a 7-agent pipeline defined in [.claude/agents/](.claude/agents/) and governed by [CLAUDE.md](CLAUDE.md):

```
analyzer → planner → (designer, for UI) → [db-migration · backend-feature · frontend] → reviewer
```

- **analyzer** — reads the user story + task files, writes a Pipeline Brief (`docs/briefs/`).
- **planner** — turns it into an Execution Plan (`docs/plans/`) of dependency-ordered, agent-assigned batches.
- **designer** — for UI stories, writes a Design Spec to `design-system/ui_kits/` from the design-system kit.
- **db-migration / backend-feature / frontend** — implement per the plan.
- **reviewer** — gates each batch against acceptance criteria + conventions.

To start work in a session: name a story ID (e.g. `P4-02`) and the pipeline runs analyzer-first. See [CLAUDE.md](CLAUDE.md) for the full workflow and rules.
