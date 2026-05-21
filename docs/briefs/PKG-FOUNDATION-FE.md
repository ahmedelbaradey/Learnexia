# Pipeline Brief — PKG-FOUNDATION-FE (Monorepo, api-client & shared)

## Summary & traceability
- **Task (1 line):** Stand up the Turborepo + pnpm frontend monorepo skeleton plus the two framework-agnostic foundation packages — `packages/api-client` (typed .NET API client, envelope handling, JWT attach/refresh, TanStack Query hooks) and `packages/shared` (domain types, constants, zod, i18n/RTL, Zustand stores) — that every student-app screen depends on.
- **Source of truth:** `tasks/Frontend/packages/PKG-FOUNDATION-FE.md` (tasks PKG-FE-1 … PKG-FE-7).
- **Not a numbered user story** — this is cross-cutting frontend infrastructure. It realizes **Build order steps 1 & 3** of `docs/dev/FRONTEND_ARCHITECTURE.md` (skeleton; then `api-client` + `shared`).
- **FR-IDs:** none directly (infrastructure). It is the enabling substrate for P1-01/02/03/04 (parent register, stay-signed-in, onboarding, link child) and all Phase 2 screens.
- **BRD goal:** indirect — underpins G1–G5 delivery by enabling the student/parent UI.
- **Phase/sprint:** Phase 1 Foundation, FE foundation track.
- **Deps:** PKG-FE-1 deps DevOps P1-07-BE (CI); PKG-FE-3 deps P1-02-BE-1 (refresh contract — already implemented on disk, see handoff).

## Business context & value
- **Who benefits:** the engineering effort itself (developer velocity) and, transitively, students + parents who use the resulting app. No end-user-visible feature ships in this task.
- **Value:** establishes the single source of truth for how the frontend talks to the backend (one typed client, one envelope contract, one auth/refresh flow, one i18n/RTL strategy, one set of domain constants). Prevents per-screen drift and ad-hoc fetch calls.
- **Success measure:** every later FE task imports `@learnexia/api-client` and `@learnexia/shared` instead of writing its own fetch/types/stores; the workspace builds, lints, and type-checks via Turborepo from the repo root.

## Acceptance criteria (testable)
**Monorepo skeleton (PKG-FE-1)**
- [ ] `pnpm install` from the monorepo root succeeds; `pnpm-workspace.yaml`, root `package.json`, `turbo.json`, `.npmrc`, `tsconfig.base.json` exist.
- [ ] `.npmrc` sets `node-linker=hoisted` (required for Expo/Metro resolution).
- [ ] Workspace packages resolve via `@learnexia/*` aliases; TS project references compile (`pnpm turbo run typecheck` / `build` is green).
- [ ] Shared `eslint-config` and `tsconfig` packages exist and are consumed by `api-client` + `shared`.
- [ ] Tooling versions pinned per architecture: **pnpm 9, Turborepo 2, Node LTS (20.x), TypeScript strict**. (`packageManager` field set in root `package.json`.)
- [ ] Folder layout matches `FRONTEND_ARCHITECTURE.md §2` (`apps/`, `packages/`). See open question on whether `apps/student-app` shell + `apps/admin-dashboard`/`apps/marketing-site` placeholders are created now.

**api-client (PKG-FE-2/3/4)**
- [ ] Types generated from backend **Swagger v2** (`/swagger/v2/swagger.json`) via openapi-typescript or NSwag; a regeneration script is committed.
- [ ] Envelope models: `BaseResponse<T>` with the `successed` flag (note the spelling), `statusCode`, `message`, `data`, `errors`; `PaginatedResult<T>` adds `currentPage`/`totalCount`/`totalPages`/`pageSize`; **422** validation shape mapped to `{ data, errors }`.
- [ ] JWT attach interceptor adds `Authorization: Bearer <accessToken>` from the token-storage abstraction in `shared`.
- [ ] Silent refresh on **401**: retry once by calling `POST /api/Users/Authentication/Refresh-Token` with body `{ accessToken, refreshToken }`; on success store the new tokens and replay; on failure clear tokens and trigger redirect/sign-out. Concurrent 401s are coalesced into a single refresh.
- [ ] TanStack Query v5 provider + base hooks `useApiQuery` / `useApiMutation` that unwrap the envelope to `{ data, errors }` and surface `successed === false` / non-2xx as query/mutation errors.
- [ ] Client is framework-agnostic (no Expo/Next imports) — consumable by both runtimes.

**shared (PKG-FE-5/6/7)**
- [ ] Zustand v5 `authStore` (tokens, current user, auth status) and `childContextStore` (active child for parent-driven multi-child context).
- [ ] Token-storage abstraction with a single interface, two implementations: `expo-secure-store` (native) and a secure web storage (cookie/web storage) — selected by platform. **No server data in Zustand** (server data is TanStack Query's job).
- [ ] Domain types (Subject/Unit/Lesson/Concept/Skill, Attempt, StudentAnswer…) + zod schemas + util helpers.
- [ ] Constants: `Roles` (parent, student, admin — **no teacher**), grades **1–6**, **4 subjects only** (Math, Science, Arabic, English — **no Social Studies**).
- [ ] i18n via react-i18next with `ar` + `en` resource scaffolding; RTL helpers; documented `react-native-restart` flow for native LTR↔RTL flip.
- [ ] All packages pass typecheck/lint/build through Turborepo.

## Affected modules & data
- **No backend entities, no DB.** This task creates the JS workspace only.
- **New (all of it):** monorepo root config, `packages/api-client`, `packages/shared`, shared `tsconfig`/`eslint-config` packages; possibly `apps/*` shells (see open question).
- **Existing (reused, not modified):** root `design-system/` reference kit — `colors_and_type.css` (`--lx-*` tokens) and `fonts/` (Poppins, Tajawal, Cairo). These are **read/referenced**; the Tamagui config that consumes them is **P1-08-FE, out of scope here**.
- **Existing (backend contract, consumed read-only):** Identity module auth endpoints. Verified on disk:
  - `POST /api/Users/Authentication/Sign-In`, `Validate-Token`, `Refresh-Token` (all AllowAnonymous), `Sign-Out` (Authorize).
  - Auth response `JwtAuthResponse`: `accessToken`, `refreshToken { userName, expireAt, tokenString }`, `userId`, `isFirstLogin`, `sessionTimeout`, `sessionId`.
  - **Refresh contract:** body `{ accessToken, refreshToken }` (refresh token travels in the **request body**, not a header) → `BaseResponse<JwtAuthResponse>`.
  - Envelope: `BaseResponse<T>` { statusCode, **successed**, message, data, errors }; `PaginatedResult<T>` adds paging; validation = HTTP 422.

## Explicitly OUT of scope
- `packages/design-system` (Tamagui config) and `packages/ui` (shared components) — these are **P1-08-FE**.
- Any student-app **screens/routes** — those are the `P1-xx-FE` / `P2-xx-FE` student-app tasks.
- `apps/admin-dashboard` and `apps/marketing-site` real content (Next.js) — later phases; at most empty placeholders here.

## Handoff → db-migration
**N/A.** No database, no entities, no migrations. Skip this stage.

## Handoff → backend-feature
**N/A.** No backend code. The backend auth contract is **already implemented** (see Affected modules). This task only consumes it. If the `api-client` reveals a contract gap, raise it as a new backend task — do not edit backend in this task.

## Handoff → designer
**N/A.** No screens or visual surfaces — this is packages/tooling only. The design tokens already exist in root `design-system/`; turning them into a Tamagui theme is P1-08-FE (its own designer/frontend pass).

## Handoff → frontend (the implementer for this task)
- **Targets:** monorepo root; `packages/api-client`; `packages/shared`; shared `tsconfig`/`eslint-config` packages.
- **Stack (fixed — do not introduce alternatives):** Turborepo 2 + pnpm 9, TypeScript strict, TanStack Query v5, Zustand v5, react-i18next, zod. Type-gen via openapi-typescript or NSwag.
- **Backend contract to type against (verified):**
  - Swagger v2 at `/swagger/v2/swagger.json`.
  - Refresh: `POST /api/Users/Authentication/Refresh-Token`, body `{ accessToken, refreshToken }`, returns `BaseResponse<JwtAuthResponse>`. Refresh on **401**, retry once, coalesce concurrent refreshes.
  - Envelope flag is **`successed`** (sic). Validation errors = HTTP **422**.
- **shared constants (product overrides — authoritative):** Roles = parent/student/admin (**no teacher**); subjects = Math/Science/Arabic/English (**no Social Studies**); grades 1–6.
- **Token storage:** one interface in `shared`; native = `expo-secure-store`, web = secure cookie/web storage. The `api-client` refresh interceptor reads/writes through this abstraction (keep `api-client` framework-agnostic — the platform-specific impl lives in `shared` or is injected).
- **State split:** Zustand = client/UI state only; TanStack Query = all server data.

## Handoff → api-tester
**Likely N/A for this task.** `api-client` is a typed wrapper with no runtime HTTP endpoints of its own; its correctness is verified by **type-generation + build/typecheck + unit tests of the envelope/refresh logic** (Vitest), not by hitting a running API. Integration testing of the live auth endpoints belongs to the backend story (P1-02) and to the student-app auth screen task (P1-09-FE) that actually performs sign-in. State this explicitly so api-tester is not dispatched for PKG-FOUNDATION-FE.

## Handoff → security-auditor
**Recommended (low surface, but auth-adjacent).** Security-relevant items:
- **Token storage abstraction** — native `expo-secure-store` vs web cookie/web storage. Auditor should confirm tokens are not persisted to insecure web `localStorage` if a secure cookie path is chosen, and that the abstraction does not leak tokens into logs.
- **Refresh flow** — ensure refresh failure path fully clears tokens and does not loop; ensure the access token is not logged when attached.
- No secrets, no PII processing, no file upload, no AI prompts in this task. A light pass is sufficient; this is not a Critical/High-likely surface.

## Open questions / assumptions / risks
1. **Monorepo root location.** `FRONTEND_ARCHITECTURE.md §2` shows `apps/` + `packages/` as **siblings of `backend/` at repo root**. **Recommendation: place the monorepo at repo root** (matches the architecture doc and the task's "Target: repo root"). Risk to flag: a root `package.json`/`node_modules` sits alongside the .NET solution — ensure `.gitignore`, `.dockerignore`, and CI path filters keep the two toolchains from interfering. (A `frontend/` subdir is the alternative if root pollution is a concern, but it contradicts the doc — surface to lead before deviating.)
2. **api-client type generation source.** Live running backend vs a **committed `swagger.json` snapshot**. **Recommendation: commit a `swagger.json` snapshot** under `packages/api-client` and generate types from it, with a documented script to refresh it from a running backend. Rationale: the backend will not reliably be running in CI, and a committed snapshot makes type-gen deterministic and reviewable. Flag: the snapshot must be regenerated when the backend contract changes (note this in the package README).
3. **Is the `apps/student-app` shell in scope?** PKG-FE-1's text says "create `apps/{student-app,admin-dashboard,marketing-site}` and `packages/{...}`" — i.e. it scaffolds the **app folders**. But `FRONTEND_ARCHITECTURE.md` Build order lists the **student-app shell (Expo Router, providers, layout) as step 4**, after this task. **Recommendation:** in PKG-FOUNDATION-FE create the `apps/*` directories as **minimal placeholders only** (so workspace globs and TS refs resolve); the real **student-app shell (Expo Router + theme/i18n/RTL providers + responsive layout) is a separate following task (build-order step 4)** and should not be built here. Confirm with lead which interpretation they want before the planner finalizes.
4. **pnpm `node-linker`.** **Recommendation: `node-linker=hoisted`** in `.npmrc` — required for Expo/Metro, called out in the task scope. Confirm acceptable for the Next.js apps (it is; hoisted is the safe default for this mixed RN+Next workspace).
5. **Tooling versions to pin.** From `FRONTEND_ARCHITECTURE.md §3`: Expo SDK 53, RN 0.76+ (New Arch), Next.js 15, TanStack Query v5, Zustand v5. **Recommendation: pin pnpm 9, Turborepo 2, Node 20 LTS, TypeScript latest-strict** at the root; defer Expo/Next/RN exact versions to the app-shell task (since no app code is built here). Confirm.
6. **Assumption:** the existing root `design-system/colors_and_type.css` + fonts are the canonical token source; this task only references them (no Tamagui config). Verified the files exist on disk.
7. **Risk — Tamagui compiler setup** is flagged as the steepest part of the stack (architecture §9) but lands in **P1-08-FE**, not here; keep it out of this task to avoid scope creep.

## Recommended pipeline order (first cut — planner finalizes)
- **designer:** skip (N/A).
- **db-migration:** skip (N/A).
- **Batch A (parallel):** `packages/shared` (PKG-FE-5/6/7) and `api-client` envelope/type-gen (PKG-FE-2) can start in parallel after the skeleton (PKG-FE-1) lands. PKG-FE-1 is the prerequisite for everything.
- **Sequence within api-client:** PKG-FE-2 (types/envelope) → PKG-FE-3 (JWT attach + refresh, depends on the `shared` token-storage abstraction from PKG-FE-5) and PKG-FE-4 (Query hooks) in parallel.
- **security-auditor:** light pass after the token-storage + refresh code exists, before the gate.
- **api-tester:** skip (no runtime endpoints).
- **reviewer:** gates the whole frontend batch against the acceptance criteria above + CONVENTIONS / FRONTEND_ARCHITECTURE.
- **committer:** after reviewer PASS, on a `feat/PKG-FOUNDATION-FE` branch.

**Status: clear to plan, pending lead confirmation on open questions 1–5** (location, type-gen source, app-shell scope, node-linker, version pins). None are blocking enough to stop the planner — defaults are recommended above — but Q3 (app-shell scope) materially changes the task size and should be confirmed first.
