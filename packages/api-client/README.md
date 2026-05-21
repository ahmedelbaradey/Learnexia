# @learnexia/api-client

Typed client to the .NET API + TanStack Query layer. **Framework-agnostic** — no
Expo/Next imports. The host app injects platform token storage (via
`@learnexia/shared` `TokenStorage`) and a sign-out callback.

## What's here

| Path | Purpose |
|---|---|
| `src/client/` | `ApiClient`: attaches JWT, unwraps `BaseResponse<T>`, maps 422, single-flight 401 refresh. |
| `src/query/` | `QueryClient` factory, `ApiQueryProvider`, `queryKeys` conventions. |
| `src/hooks/` | Representative TanStack Query hooks + `ApiClientProvider` context. |
| `src/schemas.ts` | Hand-written typed DTO aliases over the generated Swagger contract. |
| `src/generated/api-types.ts` | **Generated** (committed) openapi-typescript output. Do not hand-edit. |
| `swagger.json` | Committed OpenAPI v3 snapshot — the source for type-gen. |

## Type generation

Types are generated from a **committed** `swagger.json` snapshot, so type-gen and
CI need no running backend. The generated file is **committed** (it is the typed
contract); the eslint config ignores `src/generated/**`.

```bash
# Regenerate types from the committed snapshot:
corepack pnpm --filter @learnexia/api-client gen:api

# Re-pull the snapshot from a running backend, then regenerate:
corepack pnpm --filter @learnexia/api-client refresh:swagger
corepack pnpm --filter @learnexia/api-client gen:api
```

`refresh:swagger` pulls from `https://localhost:7080/swagger/v2/swagger.json`
(override with `SWAGGER_URL`). Equivalent manual command:

```bash
curl -sk https://localhost:7080/swagger/v2/swagger.json -o swagger.json
```

## Wiring (app shell)

```tsx
import { createApiClient, ApiClientProvider, ApiQueryProvider } from '@learnexia/api-client';
import { useAuthStore } from '@learnexia/shared';

const client = createApiClient({
  baseUrl: API_BASE_URL,
  tokenStorage,                      // platform impl from @learnexia/shared
  onSignOut: () => useAuthStore.getState().signOut(),
  onTokensRefreshed: (t) => useAuthStore.getState().setTokens(t),
});

<ApiQueryProvider>
  <ApiClientProvider client={client}>{children}</ApiClientProvider>
</ApiQueryProvider>
```

## Conventions for new hooks

- Read the client from `useApiClient()` — never construct one in a screen.
- Queries: use a key from `queryKeys`, pass `signal` for cancellation, let the
  client unwrap the envelope (`client.get<T>` returns `data` or throws).
- Paginated lists: `client.getPaginated<T>` returns the full `PaginatedResult<T>`.
- Mutations: `useMutation({ mutationFn: (input) => client.post<T>(...) })`.
- 401-refresh and 422 mapping are handled by the client; hooks just map
  endpoints to keys and surface `data`/`error`.
