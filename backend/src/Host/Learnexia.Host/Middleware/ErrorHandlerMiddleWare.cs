using System.Net;
using System.Text.Json;
using FluentValidation;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

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
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ErrorHandlerMiddleWare(
        RequestDelegate next,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _next = next;
        _logger = logger;
        _localizer = localizer;
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
                responseModel.Message = _localizer[SharedResourcesKey.ValidationFailed];
                responseModel.StatusCode = HttpStatusCode.UnprocessableEntity;
                response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
                responseModel.Errors = GetErrors(error)!;
                break;

            case KeyNotFoundException:
                responseModel.Message = error!.Message;
                responseModel.StatusCode = HttpStatusCode.NotFound;
                response.StatusCode = (int)HttpStatusCode.NotFound;
                break;

            // ArgumentNullException with parameter name "request" is raised by MediatR when ASP.NET
            // Core model binding fails to parse a malformed JSON body (Newtonsoft catches the parse
            // error, adds it to ModelState, binds null, and the action receives a null command).
            // SuppressModelStateInvalidFilter=true means the framework does not return 400 on its own,
            // so we catch the null-dereference here and return 400 instead of 500.
            case ArgumentNullException argEx when argEx.ParamName == "request":
                responseModel.Message = _localizer[SharedResourcesKey.EmptyRequestValidation];
                responseModel.StatusCode = HttpStatusCode.BadRequest;
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            // D-P2-05-01 fix: BadHttpRequestException is thrown by ASP.NET Core's request
            // pipeline when the JSON body cannot be parsed (e.g. malformed/truncated JSON).
            // SuppressModelStateInvalidFilter=true means the [ApiController] auto-400 doesn't fire,
            // so we handle it here and return 400 instead of 500.
            case BadHttpRequestException:
                responseModel.Message = _localizer[SharedResourcesKey.EmptyRequestValidation];
                responseModel.StatusCode = HttpStatusCode.BadRequest;
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            // DEFECT-1 defense-in-depth: DbUpdateException from a unique-constraint violation
            // (SqlState 23505) means the request tried to insert a duplicate row. The pre-check
            // in the handler should normally prevent this, but if it is somehow bypassed (e.g.
            // a race condition), we return 422 instead of letting the exception bubble up to 500.
            case DbUpdateException dbEx when IsUniqueConstraintViolation(dbEx):
                responseModel.Message = _localizer[SharedResourcesKey.UniqueConstraintViolation];
                responseModel.StatusCode = HttpStatusCode.UnprocessableEntity;
                response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
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

    /// <summary>
    /// Returns true when the DbUpdateException inner exception carries PostgreSQL SqlState 23505
    /// (unique_violation) or a message fragment that identifies it as a unique-index violation.
    /// Avoids a direct Npgsql type reference in the Host project.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null) return false;

        // SqlState 23505 is the PostgreSQL unique_violation code.
        // Npgsql.PostgresException exposes it as a "23505" substring in the message or data.
        var msg = inner.Message;
        return msg.Contains("23505", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("unique_violation", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("IX_Subjects_GradeId_SubjectCode_Language", StringComparison.OrdinalIgnoreCase);
    }
}
