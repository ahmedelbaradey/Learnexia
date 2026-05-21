using System.Net;
using FluentValidation.Results;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Host.Extensions;

public static class ValidationErrorResponseFactory
{
    public static IActionResult CreateResponse(ActionContext context)
    {
        var errors = context.ModelState
            .Where(p => p.Value!.Errors.Count > 0)
            .SelectMany(kvp => kvp.Value!.Errors.Select(e => new ValidationFailure(kvp.Key, e.ErrorMessage)))
            .ToList();

        var response = new BaseResponse<object>
        {
            StatusCode = HttpStatusCode.BadRequest,
            Successed = false,
            Message = "Validation Failed",
            Data = errors,
        };

        var logger = context.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("ValidationError");
        logger?.LogWarning("Validation Failed: {@Errors}", errors);

        return new UnprocessableEntityObjectResult(response);
    }
}
