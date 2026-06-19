using System.Net;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Ai.Api.Bases;

/// <summary>
/// Base controller for the Ai module's standard (non-SSE) endpoints.
/// Mirrors <c>ModerationController</c>'s <c>AppControllerBase</c> shape exactly.
/// SSE endpoints (ExplainController etc.) continue to derive directly from ControllerBase
/// because they bypass <see cref="BaseResponse{T}"/> by design (lead-approved rule-8 exception).
/// </summary>
[ApiController]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public class AppControllerBase : ControllerBase
{
    private IMediator? _mediator;
    protected IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

    public ObjectResult NewResult<T>(BaseResponse<T> response) => response.StatusCode switch
    {
        HttpStatusCode.OK => new OkObjectResult(response),
        HttpStatusCode.Created => new CreatedResult(string.Empty, response),
        HttpStatusCode.BadRequest => new BadRequestObjectResult(response),
        HttpStatusCode.Unauthorized => new UnauthorizedObjectResult(response),
        HttpStatusCode.NotFound => new NotFoundObjectResult(response),
        HttpStatusCode.Conflict => new ConflictObjectResult(response),
        HttpStatusCode.FailedDependency => new ObjectResult(response) { StatusCode = StatusCodes.Status424FailedDependency },
        HttpStatusCode.InternalServerError => new ObjectResult(response) { StatusCode = StatusCodes.Status500InternalServerError },
        _ => new ObjectResult(response) { StatusCode = (int)response.StatusCode },
    };
}
