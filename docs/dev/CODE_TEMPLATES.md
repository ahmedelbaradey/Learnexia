# backend — Code Templates (Agent Instructions)

> **Audience:** you, the implementing agent. Copy-paste skeletons that **compile-shape against the real base types** in the codebase. Replace `Foo`/`Foos`/`<Module>` placeholders.
> **Companion docs:** [FEATURE_PLAYBOOK.md](FEATURE_PLAYBOOK.md) · [CONVENTIONS.md](CONVENTIONS.md) · [../architecture.md](../architecture.md).
> Each block is annotated with the path it lives at and the Catalog file it derives from. Do not add abstractions not shown here.

## Table of Contents
1. [Entity](#1-entity)
2. [DbContext addition](#2-dbcontext-addition)
3. [Entity configuration (optional)](#3-entity-configuration-optional)
4. [DTOs](#4-dtos)
5. [Command + Handler + Validator](#5-command--handler--validator)
6. [Query + Handler](#6-query--handler)
7. [AutoMapper profile](#7-automapper-profile)
8. [Service & abstraction](#8-service--abstraction)
9. [Repository method](#9-repository-method)
10. [Controller action](#10-controller-action)
11. [DI registration deltas](#11-di-registration-deltas)

---

## 1. Entity
`…Domain/Entities/Foo.cs` — derives from `FullAuditedEntity` (Id + audit fields). Mirror [Product.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Domain/Entities/Product.cs).

```csharp
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.<Module>.Domain.Entities;

public class Foo : FullAuditedEntity
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    // FK by id only — NO cross-module navigation properties.
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;   // same-module navigation is fine
}
```

## 2. DbContext addition
`…Infrastructure/Persistence/<Module>DbContext.cs` — add a `DbSet`. Mirror [CatalogDbContext.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Persistence/CatalogDbContext.cs).

```csharp
public DbSet<Foo> Foos => Set<Foo>();
// Schema, HasDefaultSchema(Schema), ApplyConfigurationsFromAssembly(...),
// and the audit-stamping SaveChangesAsync(int userId) override already exist on the context.
```

## 3. Entity configuration (optional)
Only when EF conventions are insufficient (Catalog has none; Identity does). `…Infrastructure/Persistence/Configurations/FooEntityConfig.cs`.

```csharp
using Learnexia.Modules.<Module>.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.<Module>.Infrastructure.Persistence.Configurations;

public class FooEntityConfig : IEntityTypeConfiguration<Foo>
{
    public void Configure(EntityTypeBuilder<Foo> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
    }
}
```

## 4. DTOs
`…Application/Features/Foos/Dtos/`. Input DTOs derive from a shared `FooDto : BaseDto` (BaseDto carries `Id`). Mirror [Product DTOs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Dtos/).

```csharp
using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.<Module>.Application.Features.Foos.Dtos;

public record FooDto : BaseDto                 // BaseDto => int Id
{
    public string Name { get; set; } = null!;
}

public record AddFooDto : FooDto
{
    public string Description { get; set; } = null!;
    public int CategoryId { get; set; }
}

public record SingleFooResponse : FooDto { }   // read model
```

## 5. Command + Handler + Validator
`…Application/Features/Foos/Commands/Add/`. Mirror [AddProductCommand.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Commands/Add/AddProductCommand.cs), [AddProductCommandHandler.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Commands/Add/AddProductCommandHandler.cs), [AddValidation.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Validation/AddValidation.cs).

```csharp
// AddFooCommand.cs
using Learnexia.Modules.<Module>.Application.Features.Foos.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.<Module>.Application.Features.Foos.Commands.Add;

public record AddFooCommand : AddFooDto, ICommand<BaseResponse<string>> { }
```

```csharp
// AddFooCommandHandler.cs
using AutoMapper;
using Learnexia.Modules.<Module>.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.<Module>.Application.Features.Foos.Commands.Add;

public class AddFooCommandHandler : BaseResponseHandler, ICommandHandler<AddFooCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly IServiceManager _service;
    private readonly IMapper _mapper;

    public AddFooCommandHandler(IServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _service = service; _mapper = mapper; _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(AddFooCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
                return BadRequest<string>("the request can't be blank");

            return await _service.FooService.AddAsync<AddFooCommand>(request);   // BaseService stamps audit + SaveChanges
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddFooCommand");
            return ServerError<string>(ex.Message);
        }
    }
}
```

```csharp
// AddValidation.cs   (commands only — queries are NOT validated)
using FluentValidation;
using Learnexia.Modules.<Module>.Application.Features.Foos.Commands.Add;

namespace Learnexia.Modules.<Module>.Application.Features.Foos.Validation;

public class AddValidation : AbstractValidator<AddFooCommand>
{
    public AddValidation()
    {
        Include(new BaseValidation());   // shared rules (Name etc.)
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Category Id can't be empty.");
    }
}
```

## 6. Query + Handler
`…Application/Features/Foos/Queries/List/`. Mirror [ListQuery.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Queries/List/ListQuery.cs), [ListQueryHandler.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Features/Products/Queries/List/ListQueryHandler.cs).

```csharp
// ListQuery.cs
using Learnexia.Modules.<Module>.Application.Features.Foos.Dtos;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.<Module>.Application.Features.Foos.Queries.List;

public record ListQuery : BaseListDto, IQuery<BaseResponse<PaginatedResult<SingleFooResponse>>> { }
```

```csharp
// ListQueryHandler.cs
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.<Module>.Application.Abstractions;
using Learnexia.Modules.<Module>.Application.Features.Foos.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.<Module>.Application.Features.Foos.Queries.List;

public class ListQueryHandler : BaseResponseHandler, IQueryHandler<ListQuery, BaseResponse<PaginatedResult<SingleFooResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly IServiceManager _service;
    private readonly IMapper _mapper;

    public ListQueryHandler(IServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _service = service; _mapper = mapper; _logger = logger;
    }

    public async Task<BaseResponse<PaginatedResult<SingleFooResponse>>> Handle(ListQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = _service.FooService.GetAllAsync(false);
            if (!result.Any())
                return EmptyCollection(PaginatedResult<SingleFooResponse>.Success(new List<SingleFooResponse>(), 0, 0, 0));

            var list = await _mapper.ProjectTo<SingleFooResponse>(result)
                .ToPaginatedListAsync(request.PageNumber, request.PageSize, request.OrderBy);
            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListQuery");
            return ServerError<PaginatedResult<SingleFooResponse>>(ex.Message);
        }
    }
}
```

## 7. AutoMapper profile
`…Application/Mapping/FoosProfile.cs`. Mirror [ProductsProfile.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Mapping/ProductsProfile.cs).

```csharp
using AutoMapper;
using Learnexia.Modules.<Module>.Application.Features.Foos.Commands.Add;
using Learnexia.Modules.<Module>.Application.Features.Foos.Dtos;
using Learnexia.Modules.<Module>.Domain.Entities;

namespace Learnexia.Modules.<Module>.Application.Mapping;

public class FoosProfile : Profile
{
    public FoosProfile()
    {
        CreateMap<AddFooCommand, Foo>();
        CreateMap<Foo, SingleFooResponse>();
    }
}
```

## 8. Service & abstraction
Typed service derives from `BaseService<TEntity>` and implements `I<Aggregate>Service : IBaseService<TEntity>`. Expose it on `IServiceManager` via `Lazy`. Mirror [IProductService.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Application/Abstractions/IProductService.cs), [ProductService.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Service/Catalog/ProductService.cs), [ServiceManager.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Service/ServiceManager.cs).

```csharp
// Application/Abstractions/IFooService.cs
using Learnexia.Modules.<Module>.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.<Module>.Application.Abstractions;

public interface IFooService : IBaseService<Foo> { }   // inherits Add/Update/Delete/GetById/GetAllPaged/GetAllAsync
```

```csharp
// Infrastructure/Service/<Module>/FooService.cs
using AutoMapper;
using Learnexia.Modules.<Module>.Application.Abstractions;
using Learnexia.Modules.<Module>.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.<Module>.Infrastructure.Service.<Module>;

public class FooService : BaseService<Foo>, IFooService
{
    public FooService(IGenericRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer) { _repository = repository; }
}
```

```csharp
// Infrastructure/Service/ServiceManager.cs — add a Lazy member
private readonly Lazy<IFooService> _fooService;
// in ctor:
_fooService = new Lazy<IFooService>(() => new FooService(repository, mapper, localizer));
public IFooService FooService => _fooService.Value;
```

## 9. Repository method
Only when `BaseService`/`IGenericRepository` doesn't cover the access. Add to the typed repository (`IFooRepository : IGenericRepository`) registered via `IRepositoryManager`. Mirror [CategoryRepository.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Repository/CategoryRepository.cs) + [GenericRepository.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Repository/GenericRepository.cs).

```csharp
public class FooRepository : GenericRepository, IFooRepository
{
    public FooRepository(<Module>DbContext ctx, ICurrentUserService currentUser) : base(ctx, currentUser) { }

    public async Task<Foo?> GetByNameAsync(string name) =>
        await GetByCondition<Foo>(x => x.Name == name, trackChanges: false).FirstOrDefaultAsync();
    // NOTE: write methods on GenericRepository call SaveChangesAsync per-call (no Unit of Work).
}
```

## 10. Controller action
`…Api/Controllers/FoosController.cs`, inheriting `AppControllerBase`. Mirror [ProductsController.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Api/Controllers/ProductsController.cs).

```csharp
using Learnexia.Modules.<Module>.Api.Bases;
using Learnexia.Modules.<Module>.Application.Features.Foos.Commands.Add;
using Learnexia.Modules.<Module>.Application.Features.Foos.Queries.List;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.<Module>.Api.Controllers;

[Route("api/<Module>/[controller]")]
[ApiController]
public class FoosController : AppControllerBase
{
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpPost("Create")]
    // [Authorize("<Module>.Create")]   // add explicitly if this endpoint must be secured (policies NOT enforced by default)
    public async Task<IActionResult> Create([FromBody] AddFooCommand command)
        => NewResult(await Mediator.Send(command));
}
```

## 11. DI registration deltas
Handlers/validators/profiles auto-scan — register **only** new typed services/repos. Mirror [Catalog Infrastructure DependencyInjection.cs](../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/DependencyInjection.cs).

```csharp
// In Add<Module>Infrastructure(...) — only if you added new abstractions:
services.AddScoped<IFooRepository, FooRepository>();   // if you added a typed repository
// IServiceManager / IRepositoryManager are already registered and resolve FooService via their Lazy members.
```

```csharp
// Application DI (already correct in Catalog — shown for a NEW module):
var assembly = Assembly.GetExecutingAssembly();
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
services.AddValidatorsFromAssembly(assembly);
services.AddAutoMapper(cfg => cfg.AddMaps(assembly));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));  // commands only
```
