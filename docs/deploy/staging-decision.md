# Staging deploy — provider decision (OPEN)

**Status:** OPEN — pending lead/infra decision (P1-07-BE-4, lead decision #6 deferred).

P1-07 delivers host-agnostic staging scaffolding only. The concrete provider binding is intentionally
deferred so we do not commit provider credentials or lock into a platform before it is confirmed.

## What is delivered now (no provider lock-in)

| Artifact | Path | Purpose |
|---|---|---|
| Staging compose override | `docker/docker-compose.staging.yml` | Env-injected secrets (no hardcoded creds); pulls the pushed image instead of building on the host |
| Deploy workflow stub | `.github/workflows/deploy-staging.yml` | Builds + pushes the API image to a registry via secrets; placeholder deploy step with a provider TODO |
| This document | `docs/deploy/staging-decision.md` | Records the open decision + the secrets to fill in |

## The open decision

Pick ONE staging host. Trade-offs (summary — confirm with infra):

- **Azure** (App Service / Container Apps) — strongest fit if the rest of the org is on Azure; managed Postgres (Flexible Server) + Redis available; more setup.
- **Railway** — fastest to stand up a container + managed Postgres/Redis; good for an early staging env; cost scales with usage.
- **Render** — simple container + managed Postgres; deploy hooks are trivial to wire from CI.

No code change in the app is required to switch; only the deploy step in `deploy-staging.yml` and the
secret values change.

## Secrets to configure (GitHub repo → Settings → Secrets and variables → Actions)

Referenced by `deploy-staging.yml` / `docker-compose.staging.yml`:

| Secret | Used by | Notes |
|---|---|---|
| `REGISTRY_URL` | workflow login | e.g. `ghcr.io`, `<acr>.azurecr.io`, Docker Hub |
| `REGISTRY_USER` | workflow login | registry username / token name |
| `REGISTRY_PASSWORD` | workflow login | registry password / token |
| `REGISTRY_IMAGE` | workflow build/push + compose | full image repo, e.g. `ghcr.io/<org>/learnexia-api` |
| `DEPLOY_TARGET` | deploy step | provider identifier once chosen |
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | compose override | staging DB credentials |
| `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` | compose override | object-storage credentials |
| `REDIS_URL` | compose override | staging Redis endpoint |
| `JWT_SECRET` | compose override | staging JWT signing key (NOT the `CHANGE_ME` dev value) |
| provider token(s) | deploy step | e.g. `AZURE_CREDENTIALS`, `RAILWAY_TOKEN`, `RENDER_DEPLOY_HOOK` |

## When the provider is chosen — next steps

1. Replace the `Deploy (placeholder)` step in `.github/workflows/deploy-staging.yml` with the provider's
   official deploy action (see the TODO comment in that file for per-provider hints).
2. Populate the secrets above in the repo settings.
3. Decide the trigger policy (currently `workflow_dispatch` + `staging-*` tags).
4. Rotate the dev-only `CHANGE_ME` JWT secret and the hardcoded compose credentials
   (`postgres/admin`, `minioadmin`) into staging secrets — these are dev defaults only.
