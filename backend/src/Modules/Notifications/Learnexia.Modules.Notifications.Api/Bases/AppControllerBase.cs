using System.Net;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Notifications.Api.Bases;

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
        HttpStatusCode.InternalServerError => new ObjectResult(response) { StatusCode = StatusCodes.Status500InternalServerError },
        _ => new ObjectResult(response) { StatusCode = (int)response.StatusCode },
    };
}
