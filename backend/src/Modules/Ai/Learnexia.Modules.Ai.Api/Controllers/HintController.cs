using System.Text.Json;
using Learnexia.Modules.Ai.Application.Features.Hint.Commands;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Api.Controllers;

/// <summary>
/// SSE endpoints for the AI Helper "Hint" and "WhyWrong" intents (P3-05 BE-7).
///
/// <para><strong>SSE bypasses BaseResponse&lt;T&gt; by design — lead-approved rule-8 exception.</strong>
/// Buffer → safety → emit, never raw LLM tokens. The full response is buffered inside
/// <see cref="Learnexia.Shared.Contracts.Ai.ISafetyLayer.GenerateSafeAsync"/>, safety-screened,
/// then emitted as SSE frames. No unscreened token ever reaches the student.</para>
///
/// <para><strong>Pinned SSE wire contract (do NOT deviate — P3-12 FE consumes this):</strong></para>
/// <list type="bullet">
///   <item><c>event: message</c> + <c>data: {"hintLevel":n,"nextHintLevel":n+1}</c> — Hint intent preamble ONLY (not for WhyWrong).</item>
///   <item><c>event: message</c> + <c>data: {"content":"&lt;approved text&gt;"}</c> — content delivery (Hint + WhyWrong).</item>
///   <item><c>event: redirect</c> + <c>data: {"type":"lesson","targetId":"&lt;skillId&gt;"}</c> — no-context refuse-and-redirect.</item>
///   <item><c>event: error</c> + <c>data: {"code":"&lt;ErrorCode&gt;","message":"&lt;safe message&gt;"}</c> — safety/gateway/validation failure.</item>
///   <item><c>event: done</c> + <c>data: [DONE]</c> — stream terminator (not emitted on error).</item>
/// </list>
///
/// <para><strong>Hint vs WhyWrong:</strong> both intents are dispatched through a single
/// <c>POST /api/AiTutor/Hint</c> endpoint. The <see cref="GetHintCommand.Intent"/> field
/// discriminates which path the handler takes.</para>
///
/// <para><strong>No-reveal guard (OQ-4, AC-1):</strong> for Hint intent, the handler performs
/// a post-generation check that the approved content does NOT contain the question's CorrectAnswer.
/// If it does, the response is blocked (error frame, no stream). WhyWrong has no no-reveal check.</para>
///
/// <para>Rate-limit: enforced in-handler by <see cref="Learnexia.Modules.Ai.Application.Services.AiTutorRateLimiter"/>
/// (~10 requests per minute per JWT student id). No ASP.NET Core rate-limit policy on this endpoint.</para>
/// </summary>
[ApiController]
[Route("api/AiTutor")]
[Authorize(Roles = "Student")]
public sealed class HintController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public HintController(
        IMediator mediator,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _mediator  = mediator;
        _logger    = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Request a progressive hint or a why-wrong explanation for the current quiz question.
    ///
    /// <para>Dispatches to both <c>Hint</c> and <c>WhyWrong</c> intents via the <c>Intent</c>
    /// discriminator in the request body. <c>WrongAnswer</c> is required when <c>Intent == WhyWrong</c>.</para>
    ///
    /// <para>Streams the safety-approved response as Server-Sent Events.
    /// Grade, age, and language are resolved from the student's JWT — never from the request body.
    /// Hint level is server-derived from the attempt state — never trusted from the client.</para>
    ///
    /// <para>Hint intent: emits a preamble <c>event: message</c> frame with
    /// <c>{"hintLevel":n,"nextHintLevel":n+1}</c> before the content frame.
    /// WhyWrong intent: no preamble frame — only content + done.</para>
    /// </summary>
    /// <param name="request">Command body (QuestionId, AttemptId, Intent, optional HintLevel, optional WrongAnswer).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// Rate-limited per student: ~10 requests per minute per student user-id.
    /// <c>AiTutorRateLimiter</c> enforces this in-handler. No ASP.NET Core rate-limit policy on this action.
    /// </remarks>
    [HttpPost("Hint")]
    public async Task Hint([FromBody] GetHintCommand request, CancellationToken ct)
    {
        // SSE headers — instruct proxies/CDN not to buffer the stream.
        Response.Headers["Content-Type"] = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        // Prevent response buffering at the ASP.NET Core layer.
        Response.Headers["Transfer-Encoding"] = "chunked";

        HintResult result;
        try
        {
            result = await _mediator.Send(request, ct);
        }
        catch (FluentValidation.ValidationException vex)
        {
            // ValidationBehavior throws before the handler runs — surface validation errors
            // as a distinct SSE error code so P3-12 FE can distinguish them from real failures.
            var combined = string.Join(" ", vex.Errors.Select(e => e.ErrorMessage));
            await WriteSseEventAsync(Response, "error",
                JsonSerializer.Serialize(new { code = "ValidationError", message = combined }),
                ct);
            return;
        }
        catch (Exception ex)
        {
            // Defense-in-depth: the handler already wraps exceptions, but catch here too.
            _logger.LogError(ex, "Unhandled exception in HintController — student-facing error suppressed");
            var safeMessage = _localizer[SharedResourcesKey.AiServiceUnavailable].Value;
            await WriteSseEventAsync(Response, "error",
                JsonSerializer.Serialize(new { code = "UnhandledError", message = safeMessage }),
                ct);
            return;
        }

        switch (result)
        {
            case HintResult.Streamed streamed:
                // For Hint intent: emit preamble frame with hint level metadata first
                // so P3-12 can show "Hint n of N" before content arrives.
                if (streamed.CurrentHintLevel.HasValue)
                {
                    await WriteSseEventAsync(Response, "message",
                        JsonSerializer.Serialize(new
                        {
                            hintLevel     = streamed.CurrentHintLevel.Value,
                            nextHintLevel = streamed.NextHintLevel
                        }),
                        ct);
                }

                // Safety-approved content — buffer was already safety-filtered by ISafetyLayer.
                await WriteSseEventAsync(Response, "message",
                    JsonSerializer.Serialize(new { content = streamed.Content }),
                    ct);
                await WriteSseEventAsync(Response, "done", "[DONE]", ct);
                break;

            case HintResult.Redirect redirect:
                // No grounding context — emit redirect event (AC-3).
                var targetIdStr = redirect.TargetSkillId?.ToString() ?? string.Empty;
                await WriteSseEventAsync(Response, "redirect",
                    JsonSerializer.Serialize(new { type = "lesson", targetId = targetIdStr }),
                    ct);
                await WriteSseEventAsync(Response, "done", "[DONE]", ct);
                break;

            case HintResult.Error error:
                // Typed gentle error — no stack trace (AC-6, child-safe endpoint).
                await WriteSseEventAsync(Response, "error",
                    JsonSerializer.Serialize(new { code = error.Code, message = error.Message }),
                    ct);
                // No event: done on error — the client handles this as a terminal error frame.
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
