using System.Text.Json;
using Learnexia.Modules.Ai.Application.Features.SimilarExample.Commands;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Api.Controllers;

/// <summary>
/// SSE endpoint for the AI Helper "Similar Example" intent (P3-06 BE-11).
///
/// <para><strong>SSE bypasses BaseResponse&lt;T&gt; by design — lead-approved rule-8 exception.</strong>
/// Buffer → safety → emit, never raw LLM tokens. The full response is buffered inside
/// <see cref="Learnexia.Shared.Contracts.Ai.ISafetyLayer.GenerateSafeAsync"/>, safety-screened,
/// then emitted as SSE <c>event: message</c> frames. No unscreened token ever reaches the student.</para>
///
/// <para><strong>Pinned SSE wire contract (do NOT deviate — P3-12 FE consumes this):</strong></para>
/// <list type="bullet">
///   <item><c>event: message</c> + <c>data: {"content":"&lt;approved text&gt;"}</c> — content delivery.</item>
///   <item><c>event: redirect</c> + <c>data: {"type":"lesson","targetId":"&lt;skillId&gt;"}</c> — no-context refuse-and-redirect.</item>
///   <item><c>event: error</c> + <c>data: {"code":"&lt;ErrorCode&gt;","message":"&lt;safe message&gt;"}</c> — safety/gateway failure.</item>
///   <item><c>event: done</c> + <c>data: [DONE]</c> — stream terminator (not emitted on error).</item>
/// </list>
///
/// <para>Rate-limit: enforced in-handler by <see cref="Learnexia.Modules.Ai.Application.Services.AiTutorRateLimiter"/>
/// (in-process <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/> keyed by JWT student id,
/// ~10 requests per minute). There is no ASP.NET Core rate-limit policy or middleware for this endpoint.</para>
/// </summary>
[ApiController]
[Route("api/AiTutor")]
[Authorize(Roles = "Student")]
public sealed class SimilarExampleController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SimilarExampleController(
        IMediator mediator,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _mediator  = mediator;
        _logger    = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Request a similar worked example for the active skill (AI Helper intent #4 — "اديني مثال مشابه").
    ///
    /// Streams the safety-approved example as Server-Sent Events.
    /// Grade, age, and language are resolved from the student's JWT — never from the request body.
    /// </summary>
    /// <param name="request">Command body (SkillId required, QuestionId optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// Rate-limited per student: 10 requests per minute per student user-id.
    /// <c>AiTutorRateLimiter</c> enforces this in-handler via an in-process
    /// <c>ConcurrentDictionary</c> keyed by the JWT student id. There is no ASP.NET Core
    /// rate-limit policy or middleware on this action.
    /// </remarks>
    [HttpPost("SimilarExample")]
    public async Task SimilarExample([FromBody] SimilarExampleCommand request, CancellationToken ct)
    {
        // SSE headers — instruct proxies/CDN not to buffer the stream.
        Response.Headers["Content-Type"] = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        // Prevent response buffering at the ASP.NET Core layer.
        Response.Headers["Transfer-Encoding"] = "chunked";

        SimilarExampleResult result;
        try
        {
            result = await _mediator.Send(request, ct);
        }
        catch (FluentValidation.ValidationException vex)
        {
            // ValidationBehavior throws before the handler runs — surface validation errors
            // as a distinct SSE error code so P3-12 FE can distinguish them from real failures.
            // Errors are already localised by SimilarExampleCommandValidator.
            var combined = string.Join(" ", vex.Errors.Select(e => e.ErrorMessage));
            await WriteSseEventAsync(Response, "error",
                JsonSerializer.Serialize(new { code = "ValidationError", message = combined }),
                ct);
            return;
        }
        catch (Exception ex)
        {
            // Defense-in-depth: the handler already wraps exceptions, but catch here too.
            // Log server-side so operators can diagnose without leaking internals to the student.
            _logger.LogError(ex, "Unhandled exception in SimilarExampleController — student-facing error suppressed");
            // Emit a safe generic message; never ex.Message (info-disclosure on a child-facing endpoint).
            var safeMessage = _localizer[SharedResourcesKey.AiServiceUnavailable].Value;
            await WriteSseEventAsync(Response, "error",
                JsonSerializer.Serialize(new { code = "UnhandledError", message = safeMessage }),
                ct);
            return;
        }

        switch (result)
        {
            case SimilarExampleResult.Streamed streamed:
                // Safety-approved content — buffer was already safety-filtered by ISafetyLayer.
                // Emit as one message frame (the buffer is pre-checked — no raw streaming).
                await WriteSseEventAsync(Response, "message",
                    JsonSerializer.Serialize(new { content = streamed.Content }),
                    ct);
                await WriteSseEventAsync(Response, "done", "[DONE]", ct);
                break;

            case SimilarExampleResult.Redirect redirect:
                // No grounding context — emit redirect event (AC-7).
                var targetIdStr = redirect.TargetSkillId?.ToString() ?? string.Empty;
                await WriteSseEventAsync(Response, "redirect",
                    JsonSerializer.Serialize(new { type = "lesson", targetId = targetIdStr }),
                    ct);
                await WriteSseEventAsync(Response, "done", "[DONE]", ct);
                break;

            case SimilarExampleResult.Error error:
                // Typed gentle error — no stack trace (child-safe endpoint).
                await WriteSseEventAsync(Response, "error",
                    JsonSerializer.Serialize(new { code = error.Code, message = error.Message }),
                    ct);
                // No event: done on error — the client should handle this as a terminal error frame.
                break;
        }
    }

    /// <summary>
    /// Writes a single SSE event in the format:<br/>
    /// <c>event: &lt;eventName&gt;\ndata: &lt;data&gt;\n\n</c>
    /// </summary>
    private static async Task WriteSseEventAsync(
        HttpResponse response,
        string eventName,
        string data,
        CancellationToken ct)
    {
        var line = $"event: {eventName}\ndata: {data}\n\n";
        await response.WriteAsync(line, ct);
        await response.Body.FlushAsync(ct);
    }
}
