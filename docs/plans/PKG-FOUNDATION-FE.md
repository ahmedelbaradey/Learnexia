# Execution Plan — PKG-FOUNDATION-FE (Turborepo Monorepo + api-client + shared)

## Source

| Artifact | Path |
|---|---|
| Pipeline Brief | `docs/briefs/PKG-FOUNDATION-FE.md` |
| Task file | `tasks/Frontend/packages/PKG-FOUNDATION-FE.md` |
| Architecture | `docs/dev/FRONTEND_ARCHITECTURE.md` |
| Conventions | `docs/dev/CONVENTIONS.md` |
| Parallelism rules | `docs/dev/PARALLELISM.md` |
| CLAUDE.md | `CLAUDE.md` |

**Locked decisions (lead-accepted, plan against these):**
- Monorepo root = **repo root** (`apps/` + `packages/` siblings to `backend/`).
- `api-client` type-gen = **committed `swagger.json` snapshot** + a refresh script; no live backend in CI.
- `apps/*` = **placeholder folders only** (minimal `package.json`); real student-app shell is a separate following task.
- `.npmrc` sets `node-linker=hoisted`.
- Pins: **pnpm 9, Turborepo 2, Node 20 LTS, TypeScript strict**; Expo/Next/RN versions deferred to the app-shell task.

---

## Task Inventory

| ID | Stack | Summary | Est (h) | Depends-on |
|---|---|---|---|---|
| PKG-FE-1 | FE infra | Scaffold Turborepo + pnpm workspace at repo root; `apps/{student-app,admin-dashboard,marketing-site}` placeholders; `packages/{design-system,ui,api-client,shared}` stubs; TS project refs; `@learnexia/*` workspace aliases; `pnpm-workspace.yaml`, root `package.json`, `turbo.json`, `.npmrc` (hoisted), `tsconfig.base.json`; shared `packages/eslint-config` and `packages/tsconfig` | 6 | P1-07-BE-* (DevOps CI — note below) |
| PKG-FE-2 | FE — api-client | Typed client to .NET API: generate types from committed `swagger.json` snapshot (openapi-typescript or NSwag); model `BaseResponse<T>`, `PaginatedResult<T>`, 422 validation shape; commit `swagger.json` + regeneration script | 6 | PKG-FE-1 |
| PKG-FE-3 | FE — api-client | JWT attach interceptor + silent-refresh-on-401 (retry once, coalesce concurrent refreshes) using `POST /api/Users/Authentication/Refresh-Token`; reads/writes tokens via the `shared` token-storage abstraction; clear + redirect on refresh failure | 5 | PKG-FE-2, PKG-FE-5 |
| PKG-FE-4 | FE — api-client | TanStack Query v5 provider + base hooks (`useApiQuery` / `useApiMutation`) that unwrap envelope to `{ data, errors }`, surface `successed === false` / non-2xx as errors; framework-agnostic | 4 | PKG-FE-2 |
| PKG-FE-5 | FE — shared | Zustand v5 `authStore` (tokens, current user, auth status) + `childContextStore` (active child for parent-driven multi-child context); token-storage abstraction interface + two implementations: `expo-secure-store` (native) and secure cookie/web-storage (web) | 5 | PKG-FE-1 |
| PKG-FE-6 | FE — shared | Domain types (Subject/Unit/Lesson/Concept/Skill, Attempt, StudentAnswer…) + constants (`Roles` = parent/student/admin, **no teacher**; grades 1–6; **4 subjects**: Math, Science, Arabic, English, **no Social Studies**); zod schemas; util helpers | 4 | PKG-FE-2 (for generated API types cross-reference) |
| PKG-FE-7 | FE — shared | `react-i18next` setup; ar/en resource scaffolding; RTL helpers; `react-native-restart` flow for native LTR/RTL flip | 5 | PKG-FE-1 |

**Total estimate:** 35 h.

---

## Dependency Order

```
PKG-FE-1 (workspace skeleton + shared config packages)
    |
    +---> PKG-FE-5 (shared: authStore + token-storage)   ─┐
    +---> PKG-FE-7 (shared: i18n + RTL)                   │  independent; parallel
    |                                                       │
    +---> PKG-FE-2 (api-client: types from swagger)   ─────┘
              |
              +---> PKG-FE-4 (api-client: TanStack Query hooks)  ─┐
              +---> PKG-FE-6 (shared: domain types + constants)    │  parallel; PKG-FE-3
              |                                                      │  additionally needs PKG-FE-5
              +---> PKG-FE-3 (api-client: JWT interceptor + refresh) ─┘

    [All six tasks above must be complete before security-auditor + final reviewer]
```

**Rationale for ordering:**
- PKG-FE-1 is the **global prerequisite**: workspace globs, `tsconfig.base.json`, and `@learnexia/*` aliases must exist before any package can reference another.
- PKG-FE-5 and PKG-FE-7 depend only on PKG-FE-1 and share no files; they run in parallel.
- PKG-FE-2 depends only on PKG-FE-1 (and the committed swagger snapshot — see Blockers); it can also start once PKG-FE-1 lands, in parallel with PKG-FE-5/7.
- PKG-FE-3 depends on **both** PKG-FE-2 (envelope types + HTTP client) and PKG-FE-5 (token-storage abstraction interface — the interceptor reads/writes through it). Therefore PKG-FE-3 is the last api-client task and must wait for both.
- PKG-FE-4 depends only on PKG-FE-2; it can run in parallel with PKG-FE-3 once PKG-FE-2 is done.
- PKG-FE-6 depends on PKG-FE-2 in the task file (cross-references generated API types); it runs after PKG-FE-2 and in parallel with PKG-FE-3/4.

---

## Execution Batches

### Batch 1 — Monorepo skeleton (sequential — everything else gates on this)

| Agent | Model | Mode | Tasks |
|---|---|---|---|
| `frontend` | claude-sonnet-4-6 | sequential | PKG-FE-1 |

**What it produces:**
- Repo root: `package.json` (with `packageManager: pnpm@9.x`, `engines.node: >=20`), `pnpm-workspace.yaml`, `turbo.json`, `.npmrc` (`node-linker=hoisted`), `tsconfig.base.json` (strict).
- `packages/eslint-config/` and `packages/tsconfig/` — the shared config packages consumed by api-client and shared.
- Stub folders with minimal `package.json` for: `apps/student-app`, `apps/admin-dashboard`, `apps/marketing-site`, `packages/design-system`, `packages/ui`, `packages/api-client`, `packages/shared`.
- All `@learnexia/*` workspace aliases wired; `turbo run build lint typecheck` resolves (even on empty stubs).
- `.gitignore` updated for `node_modules/`, `.turbo/`, pnpm lockfile included.

**Review gate:** `reviewer` validates Batch 1 before Batch 2 starts.
- Checks: `pnpm install` succeeds (hoisted), `pnpm turbo run typecheck` green across stubs, `tsconfig.base.json` strict, version pins correct in `package.json`/`packageManager` field, `.npmrc` has `node-linker=hoisted`, folder layout matches `FRONTEND_ARCHITECTURE.md §2`.

---

### Batch 2 — Core packages (parallel — three independent workstreams after Batch 1)

All three tracks start simultaneously once Batch 1 passes review.

#### Track A — `shared`: authStore + token-storage (PKG-FE-5)

| Agent | Model | Mode | Tasks |
|---|---|---|---|
| `frontend` | claude-sonnet-4-6 | parallel with B & C | PKG-FE-5 |

**What it produces (`packages/shared`):**
- Zustand v5 `authStore`: tokens, current user, auth status; strict no-server-data policy.
- `childContextStore`: active child selection for parent-driven multi-child context.
- Token-storage abstraction: one `ITokenStorage` interface + two implementations — `ExpoSecureStoreTokenStorage` (`expo-secure-store`, native) and `WebTokenStorage` (secure cookie or `sessionStorage`, web). Platform selector (not Expo/Next imports in the interface itself; the interface is framework-agnostic).
- Exports wired via `packages/shared/src/index.ts`.

#### Track B — `api-client`: typed client + envelope models (PKG-FE-2)

| Agent | Model | Mode | Tasks |
|---|---|---|---|
| `frontend` | claude-sonnet-4-6 | parallel with A & C | PKG-FE-2 |

**What it produces (`packages/api-client`):**
- `swagger.json` snapshot committed at `packages/api-client/swagger.json` (from running backend once — see Blockers).
- `scripts/generate-api-types.sh` (or `.ps1`) invoking `openapi-typescript` (or NSwag) against the committed snapshot.
- Generated types in `packages/api-client/src/generated/` (committed output or generated at build time — document the choice in package README).
- Hand-written envelope types: `BaseResponse<T>` (`statusCode`, `successed` — exact spelling, `message`, `data`, `errors`), `PaginatedResult<T>` (`currentPage`, `totalCount`, `totalPages`, `pageSize`), 422 validation shape.
- Base HTTP client (fetch-based or axios, framework-agnostic) with no JWT logic yet (that is PKG-FE-3).

#### Track C — `shared`: i18n + RTL (PKG-FE-7)

| Agent | Model | Mode | Tasks |
|---|---|---|---|
| `frontend` | claude-sonnet-4-6 | parallel with A & B | PKG-FE-7 |

**What it produces (`packages/shared`):**
- `react-i18next` configuration; `ar` and `en` resource namespaces scaffolded (empty or minimal key sets).
- RTL helpers: direction detection, `I18nManager` wrapper.
- `react-native-restart` flow documented and wired for native LTR/RTL flip (safe-restart utility, not triggered automatically — trigger is the app's language-switch action).
- Exports appended to `packages/shared/src/index.ts`.

**Note:** Tracks A and C both write to `packages/shared`. The frontend agent must coordinate commits to avoid intra-batch conflicts; the simplest approach is to handle them sequentially within the same agent session (A then C, or merge them into one session), while B runs truly in parallel. The lead may dispatch A+C as one sub-task and B as another.

**Review gate:** `reviewer` validates Batch 2 (all three tracks together) before Batch 3.
- Checks: `pnpm turbo run typecheck build` still green; `authStore`/`childContextStore` exported and typed; token-storage interface + both implementations present; i18n resources scaffolded; envelope types match the brief (`successed` spelled correctly); swagger.json committed; type-gen script runs without error.

---

### Batch 3 — api-client completion (parallel sub-batch, sequential after Batch 2)

PKG-FE-3 and PKG-FE-4 both depend on PKG-FE-2 (Batch 2, Track B). PKG-FE-3 additionally depends on PKG-FE-5 (Batch 2, Track A). PKG-FE-6 depends on PKG-FE-2. All three can proceed once Batch 2 is reviewed and passed. PKG-FE-4 and PKG-FE-6 are mutually independent; PKG-FE-3 is also independent of PKG-FE-4/6 within this batch.

#### Sub-task 3A — JWT interceptor + silent refresh (PKG-FE-3)

| Agent | Model | Mode | Tasks |
|---|---|---|---|
| `frontend` | claude-sonnet-4-6 | parallel with 3B & 3C | PKG-FE-3 |

**What it produces (`packages/api-client`):**
- Request interceptor: attaches `Authorization: Bearer <accessToken>` by reading through the `shared` token-storage abstraction (injected, not imported directly — preserves framework-agnosticism).
- Response interceptor: on 401, calls `POST /api/Users/Authentication/Refresh-Token` with body `{ accessToken, refreshToken }` exactly once, coalesces concurrent 401s into a single in-flight refresh promise, stores new tokens on success, replays the original request, clears tokens and triggers a sign-out callback on failure (no looping).
- Access token must not be logged at any point in the interceptor path.
- Vitest unit tests covering: normal attach, 401 + successful refresh + replay, 401 + failed refresh + clear, concurrent 401 coalescing.

#### Sub-task 3B — TanStack Query hooks (PKG-FE-4)

| Agent | Model | Mode | Tasks |
|---|---|---|---|
| `frontend` | claude-sonnet-4-6 | parallel with 3A & 3C | PKG-FE-4 |

**What it produces (`packages/api-client`):**
- `QueryClientProvider` wrapper (exported; app shell imports it).
- `useApiQuery<T>`: unwraps `BaseResponse<T>`, surfaces `successed === false` or non-2xx as a query error.
- `useApiMutation<T>`: same envelope unwrap; exposes typed error to `onError`.
- Framework-agnostic (no Expo/Next imports in the package itself).
- Vitest unit tests for envelope unwrapping (happy path + `successed: false` + non-2xx).

#### Sub-task 3C — Domain types, constants, zod schemas (PKG-FE-6)

| Agent | Model | Mode | Tasks |
|---|---|---|---|
| `frontend` | claude-sonnet-4-6 | parallel with 3A & 3B | PKG-FE-6 |

**What it produces (`packages/shared`):**
- Domain types: `Subject`, `Unit`, `Lesson`, `Concept`, `Skill`, `Attempt`, `StudentAnswer` (and any others the generated Swagger types imply).
- Constants: `Roles` = `{ Parent, Student, Admin }` — **no Teacher**; `GRADES` = `[1, 2, 3, 4, 5, 6]`; `SUBJECTS` = `['Math', 'Science', 'Arabic', 'English']` — **no Social Studies**.
- Zod schemas for all domain types + the envelope shapes.
- Util helpers (date formatting, grade label, subject label, etc.).
- Exports appended to `packages/shared/src/index.ts`.

**Review gate (interim):** after Batch 3 all sub-tasks complete, a brief `reviewer` pass confirms `pnpm turbo run build typecheck lint` is fully green and no cross-package import rule is violated, before the security audit.

---

### Batch 4 — Security audit (sequential, after Batch 3 review)

| Agent | Mode | Scope |
|---|---|---|
| `security-auditor` | sequential (light pass) | Token-storage abstraction + JWT refresh interceptor in `packages/shared` and `packages/api-client` |

**Specific checks:**
1. Token storage: confirm that on web, tokens are NOT persisted to insecure `localStorage`; if `sessionStorage` is used, document the trade-off; if secure cookies are used, confirm `HttpOnly`/`Secure`/`SameSite` attributes are set where the package controls them.
2. Refresh interceptor: confirm the failure path fully clears both tokens and does not retry after failure (no loop).
3. Confirm access token is not logged in the attach or refresh interceptor.
4. Confirm refresh token travels in the **request body** (not a header or URL param), matching the backend contract.
5. Confirm no token values appear in error messages surfaced to callers.

**Severity gate:** Critical or High findings block the final reviewer gate; Medium/Low findings are noted and remediated at lead discretion before committer runs.

---

### Batch 5 — Final reviewer gate (sequential, after Batch 4 security audit passes)

| Agent | Mode | Scope |
|---|---|---|
| `reviewer` | sequential | Full PKG-FOUNDATION-FE delivery against all acceptance criteria |

**Acceptance criteria checklist (from brief):**

Monorepo skeleton:
- [ ] `pnpm install` from repo root succeeds; all five workspace config files exist and are correct.
- [ ] `.npmrc` contains `node-linker=hoisted`.
- [ ] `pnpm turbo run typecheck build lint` green across all packages (including stubs).
- [ ] Shared `eslint-config` and `tsconfig` packages exist and are referenced by api-client and shared.
- [ ] Tooling versions pinned: pnpm 9, Turborepo 2, Node 20 LTS, TypeScript strict.
- [ ] Folder layout matches `FRONTEND_ARCHITECTURE.md §2`.
- [ ] `apps/*` are placeholders only (no Expo Router / providers / real content).

api-client:
- [ ] `swagger.json` committed; type-gen script runs from it.
- [ ] `BaseResponse<T>` has `successed` (exact spelling), `statusCode`, `message`, `data`, `errors`.
- [ ] `PaginatedResult<T>` adds `currentPage`, `totalCount`, `totalPages`, `pageSize`.
- [ ] 422 validation shape mapped.
- [ ] JWT attach interceptor adds `Authorization: Bearer <accessToken>` from token-storage abstraction.
- [ ] Refresh interceptor: calls correct endpoint with correct body `{ accessToken, refreshToken }`; retries once; coalesces concurrent 401s; clears + signals sign-out on failure.
- [ ] `useApiQuery` / `useApiMutation` unwrap envelope; `successed === false` surfaces as error.
- [ ] api-client has no Expo/Next imports (framework-agnostic).

shared:
- [ ] `authStore` and `childContextStore` exported and correctly typed (Zustand v5).
- [ ] Token-storage abstraction: one interface, two implementations (native / web).
- [ ] No server data in Zustand stores.
- [ ] Constants: `Roles` has no Teacher; `SUBJECTS` has no Social Studies; `GRADES` = 1–6.
- [ ] Zod schemas and domain types exported.
- [ ] i18n: ar + en resources scaffolded; RTL helpers present; `react-native-restart` flow documented.
- [ ] All packages pass `pnpm turbo run typecheck build lint`.

Security:
- [ ] Security-auditor reported no Critical/High findings (or all have been remediated).

---

### Batch 6 — Commit (sequential, after Batch 5 reviewer PASS)

| Agent | Mode | Branch |
|---|---|---|
| `committer` | sequential | `feat/PKG-FOUNDATION-FE` |

Conventional commit message: `feat(frontend): turborepo monorepo skeleton + api-client + shared foundation (PKG-FOUNDATION-FE)`.
Scope of staged files: all new files under repo root (`package.json`, `pnpm-workspace.yaml`, `turbo.json`, `.npmrc`, `tsconfig.base.json`, `.gitignore` updates), `packages/eslint-config/`, `packages/tsconfig/`, `packages/api-client/`, `packages/shared/`, stub `packages/design-system/`, stub `packages/ui/`, stub `apps/student-app/`, stub `apps/admin-dashboard/`, stub `apps/marketing-site/`.
Do **not** commit changes to any file under `backend/`.

---

## Review Gates Summary

| After | Gate | Agent | Blocks |
|---|---|---|---|
| Batch 1 | Monorepo skeleton review | `reviewer` | Batch 2 |
| Batch 2 | Core packages review | `reviewer` | Batch 3 |
| Batch 3 | api-client completion + brief typecheck | `reviewer` | Batch 4 |
| Batch 4 | Security audit (light pass) | `security-auditor` | Batch 5 |
| Batch 5 | Full acceptance-criteria review | `reviewer` | Batch 6 |
| Batch 6 | Commit | `committer` | (story done) |

---

## Blockers / Prerequisites

### BLOCKER 1 — committed `swagger.json` snapshot (must resolve before Batch 2, Track B)

**What:** PKG-FE-2 generates types from a committed `swagger.json` snapshot. That snapshot does not yet exist on disk.

**How to produce it:** run the .NET backend once locally (`dotnet run` in `backend/src/Host/`) and export:
```
GET http://localhost:5000/swagger/v2/swagger.json
```
Save the response to `packages/api-client/swagger.json` and commit it. Alternatively, if the Host is configured with `SwaggerGen`, export can be scripted with:
```
dotnet run --project backend/src/Host -- --export-swagger
```
(Check if a CLI export option exists; otherwise the manual curl/browser save is sufficient.)

**Resolution:** the lead or the frontend agent must produce and commit this file before Batch 2 Track B begins. Track B's first action is to verify the snapshot exists; if not, it must generate it before proceeding.

**Risk if unresolved:** PKG-FE-2 cannot generate any types; PKG-FE-3, PKG-FE-4, and PKG-FE-6 are all blocked downstream.

### BLOCKER 2 — Node 20 + pnpm 9 available in the agent environment

**What:** the `frontend` agent must be able to run `pnpm install` and `turbo run` in the agent shell. If the executing machine does not have Node 20 LTS and pnpm 9 installed, Batch 1 cannot be validated.

**Resolution:** confirm the lead's machine (or CI) has:
```
node --version   # must be >= 20.x
pnpm --version   # must be 9.x
```
If not, install before dispatching Batch 1. `corepack enable && corepack prepare pnpm@9 --activate` is the recommended path.

### Prerequisite — P1-07-BE-* (DevOps / CI)

PKG-FE-1's task file lists P1-07-BE-* as a dependency. In practice, the monorepo scaffold does not need CI to be live to be built — CI will consume the scaffold, not the reverse. This dependency means: when CI is configured (P1-07), it must include a pnpm/Node 20/Turborepo build step for the FE packages. The frontend agent should include a sample GitHub Actions workflow step in Batch 1 (or a `TODO: wire into CI` comment in the existing CI file) so the handoff to P1-07-BE is clean. This is **not a blocking prerequisite** for Batch 1.

### Non-blocker noted — P1-02-BE-1 (Refresh-Token endpoint)

PKG-FE-3 lists `P1-02-BE-1` as a dependency (refresh contract). The brief confirms this endpoint is **already implemented on disk** (`POST /api/Users/Authentication/Refresh-Token`, body `{ accessToken, refreshToken }`, returns `BaseResponse<JwtAuthResponse>`). The dependency is informational only — the contract is known and stable. No action needed.

---

## Definition of Done

### Per batch

| Batch | Done when |
|---|---|
| 1 | `pnpm install` succeeds hoisted; `pnpm turbo run typecheck` green on stubs; all five root config files committed; reviewer passed |
| 2 | `packages/shared` exports `authStore`, `childContextStore`, token-storage abstraction, i18n/RTL, and `packages/api-client` exports envelope types + generated client; all typecheck; reviewer passed |
| 3 | `packages/api-client` exports JWT interceptor (with Vitest tests passing) + Query hooks; `packages/shared` exports domain types + constants + zod schemas; full `pnpm turbo run build typecheck lint` green; reviewer passed |
| 4 | Security auditor report issued; zero Critical/High findings (or all remediated) |
| 5 | All acceptance criteria checked off; reviewer formally passed |
| 6 | `feat/PKG-FOUNDATION-FE` branch has one conventional commit; no backend files staged; branch ready for lead review and merge to `main` |

### Overall (story complete)

- `pnpm install` from repo root installs all packages, hoisted, no errors.
- `pnpm turbo run build lint typecheck` exits 0 across all packages.
- `packages/api-client` type-checks against the committed `swagger.json` snapshot with zero type errors.
- `@learnexia/api-client` and `@learnexia/shared` resolve via workspace aliases in any package that imports them.
- `authStore`, `childContextStore`, envelope types, JWT refresh, Query hooks, domain constants (no Teacher, no Social Studies), and i18n scaffolding are all importable.
- No Expo/Next imports in `packages/api-client` (framework-agnostic confirmed).
- Security-auditor: token storage and refresh flow cleared (no Critical/High issues).
- No backend files modified.
- Committed on `feat/PKG-FOUNDATION-FE`; ready to merge before any `P1-xx-FE` screen task starts.

---

## Stage Applicability (for the record)

| Stage | Status | Reason |
|---|---|---|
| designer | N/A | No screens or visual surfaces |
| db-migration | N/A | No database, no entities |
| backend-feature | N/A | Backend contract already implemented; this task is read-only on backend |
| api-tester | N/A | No runtime HTTP endpoints; correctness is type-gen + Vitest |
| security-auditor | ONE light pass (Batch 4) | Token-storage + refresh interceptor are auth-adjacent; low surface but worth a fast audit |
| reviewer | Gates Batches 1, 2, 3, 5 | Standard gate per CLAUDE.md |
| committer | Batch 6, after Batch 5 PASS | On `feat/PKG-FOUNDATION-FE` branch |
