# @learnexia/api-client

Typed client to the .NET API + TanStack Query layer. **Framework-agnostic** — no
Expo/Next imports. The host app injects platform token storage (via
`@learnexia/shared` `TokenStorage`) and a sign-out callback.

## What's here

| Path | Purpose |
|---|---|
| `src/client/` | `ApiClient`: attaches JWT, unwraps `BaseResponse<T>`, maps 422, single-flight 401 refresh. |
| `src/query/` | `QueryClient` factory, `ApiQueryProvider`, `queryKeys` conventions. |
| `src/hooks/` | TanStack Query hooks (one per endpoint) + `ApiClientProvider` / `useTypedClient` context. |
| `src/client/typedClient.ts` | Bridges the NSwag client to the `ApiClient` transport + `BaseResponse` envelope (`unwrapEnvelope`). |
| `src/schemas.ts` | Hand-written typed DTO aliases re-exported from the generated NSwag DTOs. |
| `src/generated/nswag-client.ts` | **Generated** (committed) NSwag Fetch client: one `Client` class with a typed method per endpoint, plus DTO interfaces + `*BaseResponse` envelope shapes. Do not hand-edit. |
| `src/generated/index.ts` | Barrel over the generated client (`GeneratedApiClient`, `ApiException`, DTO types). |
| `swagger.json` | Committed Swagger v2 snapshot — the source for type-gen. |

## Type generation — NSwag is the standard

**NSwag is the API-client generation standard** (it supersedes the earlier
`openapi-typescript` approach, which only produced a types map with no typed
client). NSwag produces a full typed Fetch client (DTO interfaces + a `Client`
class with a method per endpoint) into `src/generated/nswag-client.ts`,
configured by `nswag.json`.

Generation flow:

1. **`refresh:swagger`** — pull the OpenAPI snapshot from the running backend.
2. **`gen:api` (NSwag)** — generate the typed client + DTOs from the snapshot.
3. **hooks wrap the typed client** — each hook is a thin TanStack Query wrapper
   that calls a generated method through the existing `ApiClient` transport
   (`createTypedClient` + `unwrapEnvelope`), preserving the single-flight
   401-refresh interceptor and `BaseResponse`/422 mapping.

Types are generated from a **committed** `swagger.json` snapshot, so type-gen and
CI need no running backend. The generated client is **committed** (it is the typed
contract); the eslint config ignores `src/generated/**`.

```bash
# Regenerate the client from the committed snapshot (NSwag):
corepack pnpm --filter @learnexia/api-client gen:api

# Re-pull the snapshot from a running backend, then regenerate:
corepack pnpm --filter @learnexia/api-client refresh:swagger
corepack pnpm --filter @learnexia/api-client gen:api
```

`refresh:swagger` pulls from `https://localhost:7080/swagger/v2/swagger.json`
(override with `SWAGGER_URL`; dev self-signed cert is accepted). Equivalent
manual command:

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

- Read the typed client from `useTypedClient()` — never construct one in a screen.
  (`useApiClient()` still returns the raw transport `ApiClient` for the rare
  hand-rolled call.)
- Call the generated method and unwrap with `unwrapEnvelope`:
  `queryFn: () => unwrapEnvelope(client.me())`. `unwrapEnvelope` returns `data`
  on success and throws our typed errors (`ValidationError` on 422,
  `ApiAuthError` on 401, `ApiEnvelopeError` on `successed === false`).
- Queries: use a key from `queryKeys`, e.g. `queryKeys.auth.me()`.
- Mutations: `useMutation({ mutationFn: (input) => unwrapEnvelope(client.signIn(input)) })`.
- 401-refresh and 422/401 mapping are handled by the transport + `unwrapEnvelope`;
  hooks stay thin wrappers and keep their public signatures stable.

### Preserved public surface (consumed by other apps, e.g. admin P1-10)

`createApiClient`, `createQueryClient`, the `ApiClient` type, `ApiClientProvider`,
`useApiClient`, `isApiError`, and `useSignIn` are all preserved across the NSwag
migration. New hooks added in P1-09: `useRegisterParent`, `useSignOut`,
`useAddChild`, `useLinkChild`, `useMyChildren`, `useMe`.
