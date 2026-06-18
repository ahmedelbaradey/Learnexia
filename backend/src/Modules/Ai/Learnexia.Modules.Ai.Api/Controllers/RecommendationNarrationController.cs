using System.Text.Json;
using Learnexia.Modules.Ai.Application.Features.Recommendation.Commands;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Api.Controllers;

/// <summary>
/// SSE endpoint for the AI Helper "Lexi recommendation narration" intent (P3-14).
///
/// <para><strong>SSE bypasses BaseResponse&lt;T&gt; by design — lead-approved rule-8 exception.</strong>
/// Buffer → safety → emit, never raw LLM tokens. The full response is buffered inside
/// <see cref="Learnexia.Shared.Contracts.Ai.ISafetyLayer.GenerateSafeAsync"/>, safety-screened,
/// then emitted as SSE <c>event: message</c> frames. No unscreened token ever reaches the student.</para>
///
/// <para><strong>Pinned SSE wire contract (do NOT deviate — P3-14 FE consumes this):</strong></para>
/// <list type="bullet">
///   <item><c>event: message</c> + <c>data: {"content":"&lt;approved narration&gt;"}</c> — content delivery.</item>
///   <item><c>event: declined</c> + <c>data: {"reason":"NoData"}</c> — no persisted recommendations yet (no debit).</item>
///   <item><c>event: error</c> + <c>data: {"code":"&lt;ErrorCode&gt;","message":"&lt;safe message&gt;"}</c> — safety/gateway failure.</item>
///   <item><c>event: done</c> + <c>data: [DONE]</c> — stream terminator (not emitted on error).</item>
/// </list>
///
/// <para><strong>Child id sourcing:</strong> the child id is always resolved from the JWT
/// (<c>ICurrentUserService.UserId</c>) inside the handler — never from the request body.
/// The command has no body fields.</para>
///
/// <para>Rate-limit: enforced in-handler by <see cref="Learnexia.Modules.Ai.Application.Services.AiTutorRateLimiter"/>
/// (shared with the other 4 intents).</para>
/// </summary>
[ApiController]
[Route("api/AiTutor")]
[Authorize(Roles = "Student")]
public sealed class RecommendationNarrationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public RecommendationNarrationController(
        IMediator mediator,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _mediator  = mediator;
        _logger    = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Narrate the authenticated student's persisted daily recommendations kid-style via Lexi
    /// (AI Helper intent #5 — "Ask Lexi" on the Recommendations card).
    ///
    /// Streams the safety-approved narration as Server-Sent Events.
    /// Costs 5 energy credits, charged on delivery (including cache hits).
    /// No charge on decline (empty recommendations), safety block, or error.
    /// Grade, age, and language are resolved from the student's JWT — never from the request body.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("Recommendation/Narrate")]
    public async Task NarrateRecommendations(CancellationToken ct)
    {
        // SSE headers — instruct proxies/CDN not to buffer the stream.
        Response.Headers["Content-Type"]         = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"]         = "no-cache";
        Response.Headers["X-Accel-Buffering"]    = "no";
        Response.Headers["Transfer-Encoding"]    = "chunked";

        RecommendationNarrationResult result;
        try
        {
            result = await _mediator.Send(new RecommendationNarrationCommand(), ct);
        }
        catch (FluentValidation.ValidationException vex)
        {
            // ValidationBehavior throws before the handler runs — surface validation errors
            // as a distinct SSE error code so FE can distinguish them from real failures.
            var combined = string.Join(" ", vex.Errors.Select(e => e.ErrorMessage));
            await WriteSseEventAsync(Response, "error",
                JsonSerializer.Serialize(new { code = "ValidationError", message = combined }),
                ct);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in RecommendationNarrationController — student-facing error suppressed");
            var safeMessage = _localizer[SharedResourcesKey.AiServiceUnavailable].Value;
            await WriteSseEventAsync(Response, "error",
                JsonSerializer.Serialize(new { code = "UnhandledError", message = safeMessage }),
                ct);
            return;
        }

        switch (result)
        {
            case RecommendationNarrationResult.Streamed streamed:
                // Safety-approved narration — buffer was already safety-filtered by ISafetyLayer.
                await WriteSseEventAsync(Response, "message",
                    JsonSerializer.Serialize(new { content = streamed.Content }),
                    ct);
                await WriteSseEventAsync(Response, "done", "[DONE]", ct);
                break;

            case RecommendationNarrationResult.Declined declined:
                // No persisted recommendations yet — friendly decline, no debit.
                await WriteSseEventAsync(Response, "declined",
                    JsonSerializer.Serialize(new { reason = declined.Reason }),
                    ct);
                await WriteSseEventAsync(Response, "done", "[DONE]", ct);
                break;

            case RecommendationNarrationResult.Error error:
                // Typed gentle error — no stack trace (AC-6).
                await WriteSseEventAsync(Response, "error",
                    JsonSerializer.Serialize(new { code = error.Code, message = error.Message }),
                    ct);
                // No event: done on error.
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
