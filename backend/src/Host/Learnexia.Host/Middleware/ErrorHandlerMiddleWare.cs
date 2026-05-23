using System.Net;
using System.Text.Json;
using FluentValidation;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Host.Middleware;

public class ErrorHandlerMiddleWare
{
    // Match the controllers' BaseResponse output (MVC uses camelCase). Without
    // this, the exception path emits PascalCase (StatusCode/Errors), so error
    // envelopes were inconsistent with success envelopes and broke client parsing.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILoggerManager _logger;
    private readonly RequestDelegate _next;

    public ErrorHandlerMiddleWare(RequestDelegate next, ILoggerManager logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            await HandleExceptionAsync(context, e);
        }
    }

    public async Task HandleExceptionAsync(HttpContext context, Exception error)
    {
        var response = context.Response;
        response.ContentType = "application/json";
        var responseModel = new BaseResponse<string> { Successed = false, Message = error?.Message };

        switch (error)
        {
            case UnauthorizedAccessException:
                responseModel.Message = error!.Message;
                responseModel.StatusCode = HttpStatusCode.Unauthorized;
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                break;

            case ValidationException:
                responseModel.Message = "Validation Failed";
                responseModel.StatusCode = HttpStatusCode.UnprocessableEntity;
                response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
                responseModel.Errors = GetErrors(error)!;
                break;

            case KeyNotFoundException:
                responseModel.Message = error!.Message;
                responseModel.StatusCode = HttpStatusCode.NotFound;
                response.StatusCode = (int)HttpStatusCode.NotFound;
                break;

            default:
                responseModel.Message = error?.Message;
                responseModel.Message += (error as Exception)?.InnerException == null ? "" : "\n" + error!.InnerException!.Message;
                responseModel.StatusCode = HttpStatusCode.InternalServerError;
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        var result = JsonSerializer.Serialize(responseModel, SerializerOptions);
        await response.WriteAsync(result);
    }

    private static IReadOnlyList<object>? GetErrors(Exception exception)
    {
        if (exception is ValidationException validationException)
        {
            return validationException.Errors
                .Select(e => new { e.PropertyName, e.ErrorMessage })
                .ToList();
        }

        return null;
    }
}
