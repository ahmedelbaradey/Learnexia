# backend — Feature Implementation Playbook (Agent Instructions)

> **Audience:** you, the implementing agent. **Source of truth:** [../architecture.md](../architecture.md). **Reference implementation:** the **Catalog module** — mirror it exactly.
> **Companion docs:** [CONVENTIONS.md](CONVENTIONS.md) (rules) · [CODE_TEMPLATES.md](CODE_TEMPLATES.md) (paste-able skeletons).
> Imperative voice. When a step says "mirror Catalog", open the cited Catalog file and copy its shape.

## Table of Contents
1. [Before you start](#1-before-you-start)
2. [Track A — add a feature to an existing module](#2-track-a--add-a-feature-to-an-existing-module)
3. [Track B — scaffold a brand-new module](#3-track-b--scaffold-a-brand-new-module)
4. [Registration wiring reference](#4-registration-wiring-reference)
5. [Migrations](#5-migrations)
6. [Definition of Done](#6-definition-of-done)

---

## 1. Before you start

- Read [CONVENTIONS.md](CONVENTIONS.md) and the **Known gaps** table — do not replicate the listed mistakes.
- DB is **PostgreSQL**; EF provider is **`UseNpgsql`** for all modules (runtime DI + design-time factory). Connection string `Default` → `Host=localhost;Port=5432;Database=Learnexia`.
- Identify whether the work is **Track A** (new command/query in an existing module — usually Catalog) or **Track B** (a new module).

## 2. Track A — add a feature to an existing module

Example: add an `Edit<Aggregate>` command to Catalog. Use [CODE_TEMPLATES.md](CODE_TEMPLATES.md) for each artifact.

**Ordered checklist:**

1. **Domain** (only if the data shape changes): add/extend the entity in `…Domain/Entities/`, deriving from `FullAuditedEntity`. → DB change ⇒ go to [§5 Migrations](#5-migrations).
2. **DTOs**: in `…Application/Features/<Aggregate>/Dtos/` create the input DTO (`record … : <ParentDto>`) and, for reads, a `<Single><Aggregate>Response : <Dto>`. Mirror [Product DTOs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Dtos/).
3. **Command/Query**: in `Features/<Aggregate>/Commands/<Verb>/` (or `Queries/<Verb>/`) create `record <Verb><Aggregate>Command : <Dto>, ICommand<BaseResponse<string>>` (or `IQuery<…>`). Mirror [AddProductCommand.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Commands/Add/AddProductCommand.cs).
4. **Handler**: same folder, `class <Verb><Aggregate>CommandHandler : BaseResponseHandler, ICommandHandler<…>`; inject `IServiceManager, IMapper, ILoggerManager`; `try/catch`; call `_service.<Aggregate>Service.<Op>Async(...)`. Mirror [AddProductCommandHandler.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Commands/Add/AddProductCommandHandler.cs).
5. **Validator** (commands only): `Features/<Aggregate>/Validation/<Verb>Validation.cs`, `AbstractValidator<TCommand>`, `Include(new BaseValidation())`. Mirror [AddValidation.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Validation/AddValidation.cs).
6. **Mapping**: add `CreateMap<TCommand, TEntity>()` / `CreateMap<TEntity, TResponse>()` to the aggregate `Profile`. Mirror [ProductsProfile.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Mapping/ProductsProfile.cs).
7. **Service / repository** (only if `BaseService<TEntity>` lacks the operation): add a method to the typed service or add a repository method via `IRepositoryManager`. For standard CRUD, `BaseService` already provides `AddAsync/UpdateAsync/DeleteAsync/GetByIdAsync/GetAllPagedAsync` — reuse it.
8. **Controller action**: add the action to the aggregate controller; body is `=> NewResult(await Mediator.Send(command));`. Mirror [ProductsController.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Api/Controllers/ProductsController.cs).
9. **No new registration needed** — MediatR handlers, validators, and profiles are picked up by assembly scan ([Catalog Application DependencyInjection.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/DependencyInjection.cs)). Register **only** if you added a new service/repository interface (see [§4](#4-registration-wiring-reference)).
10. Go to [Definition of Done](#6-definition-of-done).

## 3. Track B — scaffold a brand-new module

Create the four projects and wire them in. Replace `<Module>` (e.g. `Learning`) and `<schema>` (e.g. `learning`) throughout.

**Ordered checklist:**

1. **Create 4 projects** under `src/Modules/<Module>/` named `Learnexia.Modules.<Module>.{Domain,Application,Infrastructure,Api}` and add them to `Learnexia.Modular.sln`. Set project references: `Application→Domain+Shared.Kernel+Shared.Contracts`; `Infrastructure→Application`; `Api→Application+Infrastructure`.
2. **Domain**: add entities under `…Domain/Entities/` deriving from `FullAuditedEntity`.
3. **Persistence**: create `<Module>DbContext` with `public const string Schema = "<schema>";`, `DbSet<>` per entity, `HasDefaultSchema(Schema)`, `ApplyConfigurationsFromAssembly(...)`, and the audit-stamping `SaveChangesAsync(int userId)` override. Mirror [CatalogDbContext.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Persistence/CatalogDbContext.cs). Add a design-time factory mirroring [CatalogDbContextFactory.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Persistence/CatalogDbContextFactory.cs).
4. **Repository/Service layer**: copy `GenericRepository`, `RepositoryManager`, `BaseService<TEntity>`, `ServiceManager`, `CurrentUserService` shapes from Catalog Infrastructure; define `I<Module>RepositoryManager`/`I<Module>ServiceManager` + typed `I<Aggregate>Service : IBaseService<TEntity>` in Application/Abstractions.
5. **Application DI** (`AddXApplication`): MediatR + validators + AutoMapper + `ValidationBehavior` (mirror [Catalog Application DependencyInjection.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/DependencyInjection.cs)).
6. **Infrastructure DI** (`AddXInfrastructure`): `AddDbContext<…>` using **`UseNpgsql`** + schema migrations table, then `AddScoped` repos/services + `ICurrentUserService` + `ILoggerManager`. Mirror [Catalog Infrastructure DependencyInjection.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/DependencyInjection.cs).
7. **`<Module>Module.cs`** (Api): `Add<Module>Module` calls `Add<Module>Application()` + `Add<Module>Infrastructure(config)` + `AddControllers().AddApplicationPart(typeof(<Some>Controller).Assembly)`; add a `Map<Module>Module` if you use minimal APIs. Mirror [CatalogModule.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Api/CatalogModule.cs).
8. **Controllers**: add `AppControllerBase` in `Api/Bases/` (copy Catalog's) and your controllers under `Api/Controllers/`, routes `api/<Module>/[controller]`.
9. **Host wiring** ([§4](#4-registration-wiring-reference)).
10. **Permissions**: add `"<Module>"` to `Claims.GenerateModules()` so `{Module}.View/List/Create/Edit/Delete` policies are generated. Mirror [Claims.cs](../../backend/src/Modules/Identity/Learnexia.Modules.Identity.Domain/Constants/Claims.cs).
11. **Migration** ([§5](#5-migrations)) + [Definition of Done](#6-definition-of-done).

## 4. Registration wiring reference

Touch these files when adding a **module** (Track B). For Track A you usually touch **none** of these.

- **[Program.cs](../../backend/src/Host/Learnexia.Host/Program.cs)** — add two lines:
  ```csharp
  builder.Services.Add<Module>Module(builder.Configuration);   // with the others, before Build()
  app.Map<Module>Module();                                      // after MapControllers(), if minimal APIs
  ```
- **Host csproj** — add a project reference to `Learnexia.Modules.<Module>.Api`.
- **`<Module>Module.cs`** — `AddXApplication` + `AddXInfrastructure` + `AddApplicationPart(...)`.
- **Application/Infrastructure `DependencyInjection.cs`** — handlers/validators/profiles auto-scan; register only new typed services/repos.
- **`Claims.GenerateModules()`** — add the module name (permissions).

## 5. Migrations

```bash
# from repo root; --startup-project supplies ConnectionStrings:Default
dotnet ef migrations add <Name> \
  --project src/Modules/<Module>/Learnexia.Modules.<Module>.Infrastructure \
  --startup-project src/Host/Learnexia.Host

dotnet ef database update \
  --project src/Modules/<Module>/Learnexia.Modules.<Module>.Infrastructure \
  --startup-project src/Host/Learnexia.Host
```

- Migrations go into the module's own `Infrastructure/Migrations/` with `MigrationsHistoryTable("__EFMigrationsHistory", <Module>DbContext.Schema)`.
- **There is no startup auto-migrate** for non-Identity modules — apply updates manually. (Only Identity seeding runs at startup.)
- Provider is **`UseNpgsql`** (PostgreSQL) in both the DI and the design-time factory — keep new modules consistent.

## 6. Definition of Done

- [ ] Solution **builds**: `dotnet build backend/Learnexia.Modular.sln`.
- [ ] New command has a **validator**; query input handled (queries skip `ValidationBehavior`).
- [ ] Handler returns the **`BaseResponse<T>` envelope** via `BaseResponseHandler` helpers; controller uses `NewResult(...)`.
- [ ] **AutoMapper** maps verified (`Command→Entity`, `Entity→Response`).
- [ ] **Migration** added + applied; new tables land in the module **schema**.
- [ ] Module isolation respected — **no cross-module project references**; cross-module needs go via `Shared.Contracts`.
- [ ] (Track B) module added to `Program.cs`, `Claims.GenerateModules()`, and the solution file.
- [ ] **Tests**: add/extend a unit test project mirroring [tests/Modules.Catalog.UnitTests](../../backend/tests/) (xUnit + Moq + FluentAssertions); cover the new handler(s). Run `dotnet test`.
- [ ] If the endpoint must be secured, add `[Authorize(policy)]` explicitly (policies are **not** enforced by default).
- [ ] No new instance of a **Known gap** introduced (UoW assumptions, `Successed` rename, duplicate logger registration — see [CONVENTIONS.md §13](CONVENTIONS.md)).
