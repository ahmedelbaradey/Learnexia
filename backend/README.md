# Learnexia — Modular Monolith

Sibling rewrite of `backend/` as a **Modular Monolith** with:

- **Clean Architecture per module** — each module has its own `Domain`, `Application`, `Infrastructure`, `Api` projects.
- **Vertical Slice Architecture** — feature folders under `Application/Features/<Feature>/` colocate command/query, handler, validator, and DTOs.
- **MVC controllers + Minimal APIs** — both allowed inside each module's `.Api` project.
- **Module isolation** — modules never reference each other's `Domain`/`Application`/`Infrastructure`. Cross-module talk goes through `Shared.Contracts` (interfaces + integration events) dispatched via MediatR.

## Modules

| Module | Responsibility |
| --- | --- |
| `Identity` | Authentication (JWT, refresh tokens), authorization, users, audit history, user sessions |
| `Catalog` | Products, categories |
| `Notifications` | Notifications, message requests, notification types/modules. Subscribes to integration events from other modules. |

## Shared projects

| Project | Purpose |
| --- | --- |
| `Shared.Kernel` | Base entity types, result wrappers, pagination, domain event interfaces |
| `Shared.Contracts` | Public interfaces + integration event contracts for cross-module talk |
| `Shared.Resources` | Localization resources |

## Persistence

Single physical database, **schema-per-module**, separate `DbContext` per module:

- `identity.*` — `IdentityDbContext`
- `catalog.*` — `CatalogDbContext`
- `notifications.*` — `NotificationsDbContext`

## Layout

```
backend/
├── Learnexia.Modular.sln
├── src/
│   ├── Host/Learnexia.Host                     # composition root
│   ├── Modules/<Module>/<Module>.{Domain,Application,Infrastructure,Api}
│   └── Shared/Learnexia.Shared.{Kernel,Contracts,Resources}
└── tests/Modules.<Module>.UnitTests
```

## Reference project rules

- `<Module>.Api` → `<Module>.Application` + `<Module>.Infrastructure` + `Shared.Contracts`
- `<Module>.Infrastructure` → `<Module>.Application` + `<Module>.Domain` + `Shared.Kernel`
- `<Module>.Application` → `<Module>.Domain` + `Shared.Kernel` + `Shared.Contracts`
- `<Module>.Domain` → `Shared.Kernel`
- `Host` → each `<Module>.Api`

## Status

Scaffolding only. Handlers throw `NotImplementedException`. Port behavior from `backend/` feature by feature.
