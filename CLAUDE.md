# Learnexia — Project Context (read first)

AI-powered, gamified, adaptive learning platform for Arabic-speaking school students. This file is the shared rulebook for **all** agents (subagents auto-inherit nothing — they get this file as project context). When in doubt, the linked docs win over your general knowledge.

## Shared dev memory — read first, update last (every cycle)
Private Claude/session memory does **not** travel with the repo, and one lead's notes aren't visible to another. The **single shared memory is [docs/dev/HANDOFF.md](docs/dev/HANDOFF.md)** — the one place people *and* Claude sessions sync context. The protocol is mandatory for every dev cycle:
1. **Before starting new development:** read `docs/dev/HANDOFF.md` first (then the relevant story/tasks). It carries load-bearing config, how to run the stack, decisions, and open backlogs that aren't obvious from the code or git history.
2. **Before opening the PR for that work:** update `docs/dev/HANDOFF.md` with anything non-obvious the next person needs — new/changed load-bearing config (and *why*), decisions, gotchas, what's done, what's next — and include the update **in the same PR**. Prune anything now stale.
3. That is how memory is shared: write it to HANDOFF.md → commit → it's in `main` for everyone (human or agent) on the next pull. If it isn't in HANDOFF.md, assume the next lead won't know it.

## Hard facts (do not contradict)
- **Backend:** [backend/](backend/) — `Learnexia.Modular.sln` (.NET 10 modular monolith). *(The legacy clean-architecture solution that previously sat here has been removed; this is the only backend.)*
- **Stack:** **.NET 10**, ASP.NET Core, **modular monolith**, MediatR CQRS, FluentValidation, AutoMapper, ASP.NET Identity + JWT.
- **Database:** **PostgreSQL** via `UseNpgsql` (Npgsql), DB `Learnexia`, schema-per-module. **NOT SQL Server** — ignore any stale "SQL Server" wording.
- **Reference module:** mirror an existing module's structure (e.g. **Learning**) for any new backend work.
- **Frontend:** not started yet. **Turborepo monorepo** — Expo universal student app (web PWA + native), Tamagui, TanStack Query + Zustand; Next.js admin/marketing later. See [docs/dev/FRONTEND_ARCHITECTURE.md](docs/dev/FRONTEND_ARCHITECTURE.md). Design tokens/kit in [design-system/](design-system/).

## Work intake (source of truth for what to build)
- **User stories** (one per file, by phase) → [user-stories/](user-stories/) + [user-stories/README.md](user-stories/README.md). **These are the source of truth** for scope & acceptance criteria.
- **Per-stack task breakdown** → [tasks/](tasks/) (`tasks/Backend/...`, `tasks/Frontend/...`) + [tasks/README.md](tasks/README.md). Task IDs `P1-01-BE-1` etc.
- The lead names a story/task ID (e.g. `P4-02`); the `analyzer` reads its story + task files first.

## Product decisions (override BRD/SRS where they conflict)
- **Parent-driven onboarding** — parents register + add children; students don't self-register.
- **4 subjects** — Math, Science, Arabic, English. **No Social Studies.**
- **No teacher role.**
- **Grade transition** preserves history (XP/badges/streaks/mastery).

## Authoritative docs (consult before coding)
- **How to build backend features:** [docs/dev/FEATURE_PLAYBOOK.md](docs/dev/FEATURE_PLAYBOOK.md)
- **Conventions + known gaps:** [docs/dev/CONVENTIONS.md](docs/dev/CONVENTIONS.md)
- **Copy-paste C# skeletons:** [docs/dev/CODE_TEMPLATES.md](docs/dev/CODE_TEMPLATES.md)
- **Unit of Work decision:** [docs/dev/adr/0001-unit-of-work.md](docs/dev/adr/0001-unit-of-work.md)
- **Architecture of record:** [docs/architecture.md](docs/architecture.md)
- **Product spec (background):** [docs/BRD.md](docs/BRD.md) · [docs/SRS.md](docs/SRS.md) · [docs/TASK_BREAKDOWN.md](docs/TASK_BREAKDOWN.md)
- **Frontend architecture:** [docs/dev/FRONTEND_ARCHITECTURE.md](docs/dev/FRONTEND_ARCHITECTURE.md)
- **Lead handoff (read if picking up web FE / dev env):** [docs/dev/HANDOFF.md](docs/dev/HANDOFF.md) — what's done, decisions, load-bearing config, how to run the stack, and the P1-12 Batch-2 backend backlog.

## Non-negotiable rules
1. **Module isolation** — a module never references another module's projects. Cross-module = `Shared.Contracts` only (integration events / interface seams). No cross-module FKs.
2. **Response envelope** — handlers return `BaseResponse<T>` via `BaseResponseHandler`; controllers use `NewResult(...)`. The success flag is spelled **`Successed`** (do not rename).
3. **No Unit of Work** — `GenericRepository` commits per call (`SaveChangesAsync`). If you need atomic multi-writes, open an explicit transaction.
4. **Validation** — `ValidationBehavior` runs for `ICommand<>` only; queries are not auto-validated.
5. **Logging** — inject `ILoggerManager`, not `ILogger<T>`. Don't add a second logger registration.
6. **Auth** — permission policies (`{Module}.{Action}`) exist but aren't enforced; add `[Authorize(policy)]` deliberately.
7. **No teacher role** in the product.
8. **Design patterns — ask first.** Default to mirroring existing shapes (existing modules on backend, the decided architecture + existing component/hook shapes on frontend); do not invent abstractions. If a task genuinely calls for a design pattern (Strategy, Factory, Decorator, provider/compound-component, etc.), **stop and ask the lead/user before implementing it** — name the pattern, where it applies, and why. Wait for approval; never introduce one unilaterally. This applies to both backend and frontend agents.

## Multi-agent workflow
Specialized agents live in [.claude/agents/](.claude/agents/): `analyzer`, `planner`, `designer`, `db-migration`, `backend-feature`, `api-tester`, `frontend`, `frontend-e2e-tester`, `qc-test-designer`, `security-auditor`, `reviewer`, `committer`.

**Fixed order — analyzer → planner → (designer for UI) → implementers → reviewer:**
1. **`analyzer`** (first, always) — reads the user story + task files, builds business + technical understanding, and writes a **Pipeline Brief** to `docs/briefs/<story>.md` (traceability, acceptance criteria, per-agent handoffs, open questions). If anything is ambiguous, it returns questions for the lead to ask the user **before** planning.
2. **`planner`** — turns the brief + task files into an **Execution Plan** in `docs/plans/<story>.md`: task inventory, dependency order, agent-assigned **batches** (parallel vs sequential), review gates, blockers. The plan flags whether a **design stage** is needed.
3. **`designer`** (only for stories with a UI surface) — turns the story into a **Design Spec** in `design-system/ui_kits/<surface>/<story>.md`, grounded in the `design-system/` kit + UI docs. Runs **before** the frontend batch. Skip for backend-only stories.
4. The lead **dispatches implementer agents batch by batch per the plan** — `db-migration`, `backend-feature`, `frontend` — parallel where independent, sequential where dependent. The `frontend` batch consumes the Design Spec. For stories exposing **HTTP endpoints**, **`api-tester`** runs after `backend-feature` to validate the running API (integration tests). For stories with a **student-app UI surface**, **`frontend-e2e-tester`** runs after `frontend` to drive the running web PWA with Playwright (user flows, RTL/ar+en, validation, auth/role routing).
4b. For **security-sensitive** batches (auth/authz, user or child data, file upload, AI prompts, secrets, payments), **`security-auditor`** audits before the gate; Critical/High findings block.
5. **`reviewer`** gates each batch against the brief's acceptance criteria + CONVENTIONS.md (including `api-tester`, `frontend-e2e-tester`, and `security-auditor` results) before it's done.
6. **`committer`** — only after `reviewer` PASSES — stages and commits the batch on a per-story branch (`feat/<StoryID>-…`) with a conventional message, then **always pushes the branch and opens a Pull Request** (with a full description). Never on `main`, never amends/force-pushes, and never merges the PR itself unless explicitly told.

- **`qc-test-designer` (on-demand QC stage, not in the fixed order):** when the lead asks for it, this Opus-pinned agent designs comprehensive backend + frontend test cases for a story and writes a per-run folder `docs/qc/<StoryID>/` (test-case docs + a coverage report). It only *designs* — `api-tester` then implements `backend-test-cases.md` and `frontend-e2e-tester` implements `frontend-test-cases.md`, both writing results into that folder's `execution-report.md`. Run it before those tester stages when you want a deliberate, traceable test plan rather than ad-hoc coverage.
- Downstream agents consume the **Pipeline Brief + Execution Plan** (and frontend also the **Design Spec**) as their spec, follow the docs above, and report back: what changed, files touched, build/test status, any rule they had to bend (with why).
- Do not skip analyzer or planner for anything beyond a trivial one-line fix.

**Parallel pipelines:** multiple stories may run at once only per [docs/dev/PARALLELISM.md](docs/dev/PARALLELISM.md) — independent siblings only, each in its own `feat/<StoryID>` git worktree, respecting the dependency order; shared-file edits (Program.cs / .sln / Claims / Directory.Packages.props) are serialized. Within a single story, run independent batches in parallel (Mode A).
