using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Learnexia.Host.Middleware;

public class AuthorizationLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILoggerManager _logger;

    public AuthorizationLoggingMiddleware(RequestDelegate next, ILoggerManager logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var policyMetadata = endpoint?.Metadata.GetMetadata<AuthorizeAttribute>();

        if (policyMetadata != null && !string.IsNullOrWhiteSpace(policyMetadata.Policy))
            _logger.LogInfo($"Authorization Required: {policyMetadata.Policy} for {context.Request.Path}");

        await _next(context);
    }
}
