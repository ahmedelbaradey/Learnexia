using Learnexia.Modules.Catalog.Api.Bases;
using Learnexia.Modules.Catalog.Application.Features.Categories.Commands.Add;
using Learnexia.Modules.Catalog.Application.Features.Categories.Queries.Get;
using Learnexia.Modules.Catalog.Application.Features.Categories.Queries.List;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Catalog.Api.Controllers;

[Route("api/Catalog/[controller]")]
[ApiController]
// P1-05 (RBAC): reads require an authenticated caller (any role); writes require AdminOnly.
[Authorize]
public class CategoriesController : AppControllerBase
{
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetQuery { Id = id }));

    [HttpPost("Create")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] AddCategoryCommand command)
        => NewResult(await Mediator.Send(command));
}
