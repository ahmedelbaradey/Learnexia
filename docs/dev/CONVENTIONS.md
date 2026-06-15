# backend — Coding Conventions (Agent Instructions)

> **Audience:** you, the implementing agent. **Source of truth:** [../architecture.md](../architecture.md) + the **Catalog module** as the canonical reference implementation.
> **Companion docs:** [FEATURE_PLAYBOOK.md](FEATURE_PLAYBOOK.md) (step-by-step) · [CODE_TEMPLATES.md](CODE_TEMPLATES.md) (skeletons).
> Follow these conventions exactly. Do **not** invent abstractions that are not already in the codebase.

## Table of Contents
1. [Module structure & layering](#1-module-structure--layering)
2. [Naming](#2-naming)
3. [CQRS: commands & queries](#3-cqrs-commands--queries)
4. [Validation](#4-validation)
5. [Response & result handling](#5-response--result-handling)
6. [Mapping (AutoMapper)](#6-mapping-automapper)
7. [Repository vs. Service Manager](#7-repository-vs-service-manager)
8. [Audit & SaveChanges rules](#8-audit--savechanges-rules)
9. [Persistence & schema isolation](#9-persistence--schema-isolation)
10. [Localization](#10-localization)
11. [Error handling & logging](#11-error-handling--logging)
12. [Module isolation rule](#12-module-isolation-rule)
13. [Known gaps — do not replicate](#13-known-gaps--do-not-replicate)

---

## 1. Module structure & layering

Every module has **four projects** (Clean/Onion); `Application` and `Domain` have **no outward dependencies** ([architecture.md §2](../architecture.md)):

```
src/Modules/<Module>/
  Learnexia.Modules.<Module>.Api/             # controllers, *Module.cs (registration), Bases/AppControllerBase
  Learnexia.Modules.<Module>.Application/     # Features/, Abstractions/, Mapping/, DependencyInjection.cs
  Learnexia.Modules.<Module>.Domain/          # Entities/, Constants/, Enums/, Helpers/
  Learnexia.Modules.<Module>.Infrastructure/  # Persistence/, Repository/, Service/, Migrations/, DependencyInjection.cs
```

Dependency direction: `Api → Application → Domain`; `Infrastructure → Application + Domain`. Reference [`Shared.Kernel`](../../backend/src/Shared/Learnexia.Shared.Kernel/) for base types and [`Shared.Contracts`](../../backend/src/Shared/Learnexia.Shared.Contracts/) only for cross-module seams.

## 2. Naming

- Projects: `Learnexia.Modules.<Module>.<Layer>`; namespaces match folders.
- Feature folders: `Features/<Aggregate>/Commands/<Verb>/` and `Features/<Aggregate>/Queries/<Verb>/`.
- Commands/queries: `<Verb><Aggregate>Command` / `ListQuery` / `GetQuery`; handlers add `Handler`; validators add `Validation`/`Validator`.
- **Match the handler class name to its command** — `backend/` had copy-paste typos (handler named after the wrong command). See the NOTE in [AddProductCommandHandler.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Commands/Add/AddProductCommandHandler.cs).

## 3. CQRS: commands & queries

Use the `Shared.Kernel.Messaging` markers, never raw MediatR `IRequest` directly:
- Command: `record XCommand : <XDto>, ICommand<BaseResponse<T>>`.
- Query: `record XQuery : <BaseListDto?>, IQuery<BaseResponse<T>>`.
- Handler: inherit `BaseResponseHandler` and implement `ICommandHandler<,>` / `IQueryHandler<,>`.
- Inject `IServiceManager`, `IMapper`, `ILoggerManager` (the Catalog handler constructor signature). Reference: [AddProductCommandHandler.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Commands/Add/AddProductCommandHandler.cs), [ListQueryHandler.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Queries/List/ListQueryHandler.cs).

## 4. Validation

- One `AbstractValidator<TCommand>` per command, in `Features/<Aggregate>/Validation/`.
- Compose shared rules with `Include(new BaseValidation())`. Reference: [AddValidation.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Validation/AddValidation.cs).
- Validators run via `ValidationBehavior` **only for commands** (`ICommand<>`). **Queries are NOT validated** — do not rely on a validator firing for a query. See [§13](#13-known-gaps--do-not-replicate).
- A failed validation throws `ValidationException` → shaped as **HTTP 422** by the host.

## 5. Response & result handling

- Every handler returns `BaseResponse<T>`; list/paged handlers return `BaseResponse<PaginatedResult<T>>`.
- Build responses with `BaseResponseHandler` helpers only: `Success`, `Created`, `BadRequest`, `NotFound`, `Unauthorized`, `BusinessValidation` (→424), `ServerError`, `EmptyCollection`. Reference: [architecture.md §6](../architecture.md), [BaseResponse.cs](../../backend/src/Shared/Learnexia.Shared.Kernel/Responses/BaseResponse.cs).
- Controllers convert with `NewResult(...)` ([AppControllerBase.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Api/Bases/AppControllerBase.cs)).
- The success boolean is spelled **`Successed`** (sic). Do not "fix" it casually — clients depend on the existing JSON key. See [§13](#13-known-gaps--do-not-replicate).

## 6. Mapping (AutoMapper)

- One `Profile` per aggregate in `Application/Mapping/` (e.g. `ProductsProfile`). Map `Command → Entity` and `Entity → <Single>Response`. Reference: [ProductsProfile.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Mapping/ProductsProfile.cs).
- Queries project with `_mapper.ProjectTo<TResponse>(queryable)` then `.ToPaginatedListAsync(...)` ([QueryableExtensions.cs](../../backend/src/Shared/Learnexia.Shared.Kernel/Pagination/QueryableExtensions.cs)).

## 7. Persistence architecture (CURRENT STANDARD, decided 2026-06-15)

> **The standard for all NEW modules + features, and the target for refactoring existing ones. Supersedes the Catalog repository/service-manager and the `I{Module}DbContext`-in-Application patterns below.**

**The Application layer is EF-free AND depends on SERVICES ONLY — the hard, non-negotiable rule.** The Application layer (handlers + the service-interface abstractions) may depend ONLY on **service interfaces**. FORBIDDEN anywhere in `{Module}.Application`:
- `DbSet`, `IQueryable`
- `Microsoft.EntityFrameworkCore` (any using/package reference)
- **EF exceptions** (`DbUpdateException`/`DbUpdateConcurrencyException` — catch/translate at the Infrastructure boundary, e.g. `UnitOfWorkBehavior` or the repository, surfacing a domain-neutral result)
- **a `DbContext`/`I{Module}DbContext`**, AND **a repository injected into a handler** — handlers must NOT inject/call repositories or the DbContext directly; they go through services.

The chain is **Handler → Service → Repository → EF/DbContext**:
- **Application = services only** — handlers inject + call **service interfaces** (declared in `{Module}.Application/Abstractions`). No repository, no DbContext, no EF in Application.
- **Services own workflows** — orchestration, business rules, idempotency. Service interface in `Application/Abstractions`, impl in `Infrastructure`; the impl uses repositories for persistence. Explicit transactions live in Infrastructure (the service / `UnitOfWorkBehavior`), never in a handler.
- **Repositories own persistence** — they live in `Infrastructure` (own the `DbContext`), are used BY services (NOT injected into Application handlers), and return **materialized** results (entities/DTOs/`PaginatedResult<T>`) — never `IQueryable`/`DbSet` across a layer boundary (compose `IQueryable`+`ProjectTo`+pagination INSIDE the impl so it stays server-side — don't lose a vector `ORDER BY`/`LIMIT` or an HNSW index to client-evaluation).
- **Handlers MAY orchestrate** — NOT required to be one-line shells; a handler may validate/authorize and coordinate **multiple service calls** + domain engines — but only via services (never a repo/DbContext), and never owning transactions/EF.

Status (2026-06-15): ✅ Parent, Curriculum, Moderation (PR #152 — handlers call services). 🔴 **Gamification** (handlers inject `IGamificationRepository` directly → must go behind services; EF-free already done), Notifications + Billing (`IXDbContext`→services), Learning (`IBaseService<T>` `IQueryable` leak + repo-in-handlers → services). All 🔴 = handlers repointed to services; services use repositories internally. 🚫 **Ai + Identity excluded — left as-is, do NOT convert.**

### Legacy aggregators (pre-Option-C; do not adopt for new work)
Two access aggregators, both registered Scoped:
- **`IServiceManager`** → typed services (`IProductService : IBaseService<Product>`). Handlers call `_service.ProductService.AddAsync(...)`. Services derive from `BaseService<TEntity>`. Use this for standard CRUD-style features. Reference: [ServiceManager.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Service/ServiceManager.cs), [BaseService.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Service/BaseService.cs).
- **`IRepositoryManager`** → typed repositories (`ICategoryRepository : IGenericRepository`). Use for custom data access not covered by `BaseService`. Reference: [RepositoryManager.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Repository/RepositoryManager.cs).
- Both aggregators expose sub-members via `Lazy<T>`.

## 8. Audit & SaveChanges rules

- Entities derive from `FullAuditedEntity` (Id, CreatedAt/By, UpdatedAt/By, DeletedAt/By, IsDeleted) — see [architecture.md §2](../architecture.md).
- The DbContext override `SaveChangesAsync(int userId)` stamps audit fields. The repository passes `CurrentUserService.UserId` automatically. **Do not stamp `CreatedBy`/`UpdatedAt` by hand** in handlers.
- **Unit of Work — per [ADR 0001](adr/0001-unit-of-work.md):**
  - **Catalog (and any code reusing its `GenericRepository`)** commits **per repository call** (`SaveChangesAsync` inside each write). Do not assume multiple writes are atomic there.
  - **New modules (Learning, Gamification, Curriculum, …)** use **deferred commit**: repositories only `Add/Update/Remove` (no `SaveChangesAsync`); a MediatR **`UnitOfWorkBehavior<TRequest,TResponse>`** (constrained to `ICommand<>`, registered **after** `ValidationBehavior`) opens a transaction, runs the handler, calls the module `DbContext.SaveChangesAsync(currentUserId)` once, and commits — rolling back on exception. Queries never commit.
  - **Scope = one module DbContext.** Never open a transaction spanning modules; cross-module consistency uses integration events + the **Outbox** pattern, and events publish **after** commit.
  - **Domain events dispatch after commit — per [ADR 0002](adr/0002-domain-events-and-dispatch.md).** After its single `SaveChangesAsync` + `CommitAsync`, `UnitOfWorkBehavior` collects the `DomainEvents` from tracked `AggregateRoot`s, dispatches them via `IDomainEventDispatcher`, then clears them — **only on a successful commit, never on rollback**. Cross-module fan-out works because MediatR is registered once at the Host across all module Application assemblies, behind the `IsolatedNotificationPublisher` (one failing handler does not abort its siblings).

## 9. Persistence & schema isolation

- One `DbContext` per module, one SQL **schema** (`public const string Schema = "<module>";`, applied via `modelBuilder.HasDefaultSchema(Schema)`).
- Migrations live in that module's `Infrastructure/Migrations/` with `MigrationsHistoryTable("__EFMigrationsHistory", Schema)`.
- Entity configurations are **optional**: Catalog uses EF conventions (no `IEntityTypeConfiguration` files); Identity has explicit configs. Add a config only when conventions aren't enough.
- **Provider = PostgreSQL (`UseNpgsql`)** in both runtime DI and the design-time factory, for **all** modules (`Npgsql.EntityFrameworkCore.PostgreSQL`). Connection string `Default` points at PostgreSQL (`Host=localhost;Port=5432;Database=Learnexia`). Use `UseNpgsql` for any new module. **Note:** [architecture.md](../architecture.md) still describes the *previous* SQL Server setup and is stale on persistence — trust the code, not that doc, for the DB engine.

## 10. Localization

- Inject `IStringLocalizer<SharedResources>` for user-facing messages; use `SharedResourcesKey.*` constants (Arabic/English in `Shared.Resources`). `BaseService` already does this — follow its pattern rather than hard-coding English strings in services.

## 11. Error handling & logging

- Wrap handler bodies in `try/catch`; on exception, `_logger.LogError(ex, "Error: in <Command>")` and return `ServerError<T>(ex.Message)` (Catalog pattern).
- Inject `ILoggerManager` (NLog wrapper), not `ILogger<T>` directly, for app logging.
- The host's `ErrorHandlerMiddleWare` is the global backstop; per-handler try/catch is still the convention here.

## 12. Module isolation rule

- **A module must never reference another module's projects.** Cross-module communication goes through `Shared.Contracts` only:
  - Publish/consume **integration events** (`IIntegrationEvent : INotification`, dispatched in-process by MediatR).
  - Call **interface seams** (e.g. `IUserNotificationService`, `IFilePreviewUrlProvider`, `IUserLookup`) implemented on the providing side.
- **No cross-module foreign keys.** `CreatedBy` is a plain `int`, not an FK to `identity.AspNetUsers`. See [architecture.md §4.4 & §5](../architecture.md).

## 13. Known gaps — do not replicate

When you touch these areas, be aware (don't "fix" silently, don't depend on the broken assumption):

| Gap | Implication for you |
|---|---|
| **Unit of Work** | Catalog commits per repo call; **new modules** use deferred commit + `UnitOfWorkBehavior` per [ADR 0001](adr/0001-unit-of-work.md). Don't reuse Catalog's save-at-call-time `BaseService` coupling in new modules. |
| **ValidationBehavior runs for commands only** | Validate query inputs inside the handler, or accept they're unvalidated. |
| **`[Authorize]` not enforced** on most endpoints | Permission policies exist (`{Module}.{Action}`) but aren't applied. Add `[Authorize(policy)]` deliberately when securing a feature. |
| **`Successed` spelling** in `BaseResponse` | Keep the key as-is; renaming breaks API contract. |
| **architecture.md is stale on the DB** | It says SQL Server; the code uses **PostgreSQL/Npgsql** everywhere. Trust the code. |
| **No startup auto-migrate for Catalog** | Only Identity seeds at startup. Apply Catalog/new-module migrations manually (`dotnet ef database update`). |
| **Duplicate `ILoggerManager` registration** (Singleton + Scoped) | Don't add a third; rely on the existing Scoped resolution. |
| **No `nlog.config`** | NLog has no targets; logs may go nowhere until config is added. Don't assume file/console output exists. |
| **`"Onion"` assembly-scan hook** in Catalog infra | Matches nothing currently; don't name types to accidentally trigger it unless intended. |
