# PKG-FOUNDATION (Frontend) — Monorepo, api-client & shared

> Cross-cutting frontend foundation for the Turborepo monorepo. Underpins every student-app screen.
> Stack: Turborepo + pnpm, TypeScript, Expo/Next, Tamagui, TanStack Query v5, Zustand v5, react-i18next.
> Related backend: see [../../Backend/Phase-1-Foundation/](../../Backend/Phase-1-Foundation/) for API contracts.
> **Status: ✅ Done** — full monorepo, typed api-client (envelope + 401 silent-refresh), TanStack Query v5 hooks, Zustand stores + token storage, domain constants/schemas, i18n + RTL helpers all implemented. _(Audited 2026-06-07.)_

## Tasks
| ID | Task | Target | Deps | Est (h) | Status |
|---|---|---|---|---|---|
| PKG-FE-1 | Scaffold Turborepo + pnpm workspaces; create `apps/{student-app,admin-dashboard,marketing-site}` and `packages/{design-system,ui,api-client,shared}` with TS project refs and workspace aliases (`@learnexia/*`) | repo root | P1-07-BE-* (DevOps) | 6 | ✅ |
| PKG-FE-2 | `api-client`: typed client to the .NET API; generate types from Swagger v2 (NSwag/openapi-typescript); model `BaseResponse<T>` + `PaginatedResult<T>` + 422 validation shape | `packages/api-client` | — | 6 | ✅ |
| PKG-FE-3 | `api-client`: JWT attach interceptor + silent-refresh-on-401 (retry once) using `Refresh-Token`; clear+redirect on failure | `packages/api-client` | PKG-FE-2, P1-02-BE-1 | 5 | ✅ |
| PKG-FE-4 | `api-client`: TanStack Query v5 provider + base hooks (`useApiQuery`/`useApiMutation`) mapping the envelope to `{data, errors}` | `packages/api-client` | PKG-FE-2 | 4 | ✅ |
| PKG-FE-5 | `shared`: `authStore` + `childContextStore` (Zustand v5); token storage abstraction (`expo-secure-store` native / secure web storage) | `packages/shared` | — | 5 | ✅ |
| PKG-FE-6 | `shared`: domain types + constants (`Roles`, grades 1–6, **4 subjects**), zod schemas, util helpers | `packages/shared` | PKG-FE-2 | 4 | ✅ |
| PKG-FE-7 | `shared`: i18n setup (react-i18next) with ar/en resources + RTL helpers; `react-native-restart` flow for native RTL flip | `packages/shared` | — | 5 | ✅ |

## Notes
- This file is the **monorepo + data/logic plumbing** referenced by every student-app screen task (token attach/refresh, query hooks, stores, i18n). It is not tied to one user story; it realizes the "Build order" steps 1 & 3 in [../../../docs/dev/FRONTEND_ARCHITECTURE.md](../../../docs/dev/FRONTEND_ARCHITECTURE.md).
- **Contract with backend:** all calls go through `api-client`; envelope is `BaseResponse<T>` (architecture.md §6), refresh via `/api/Users/Authentication/Refresh-Token`.
