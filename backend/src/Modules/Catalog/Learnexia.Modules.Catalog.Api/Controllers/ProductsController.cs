using Learnexia.Modules.Catalog.Api.Bases;
using Learnexia.Modules.Catalog.Application.Features.Products.Commands.Add;
using Learnexia.Modules.Catalog.Application.Features.Products.Commands.Delete;
using Learnexia.Modules.Catalog.Application.Features.Products.Commands.Edit;
using Learnexia.Modules.Catalog.Application.Features.Products.Queries.Get;
using Learnexia.Modules.Catalog.Application.Features.Products.Queries.List;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Catalog.Api.Controllers;

[Route("api/Catalog/[controller]")]
[ApiController]
// P1-05 (RBAC): reads require an authenticated caller (any role); writes require AdminOnly.
[Authorize]
public class ProductsController : AppControllerBase
{
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetQuery { Id = id }));

    [HttpPost("Create")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] AddProductCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Update([FromBody] EditProductCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteProductCommand { Id = id }));
}
