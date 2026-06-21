using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;

namespace Learnexia.PerfTests;

/// <summary>
/// AI pipeline overhead scenario (BE-3).
///
/// IMPORTANT: This scenario measures the PIPELINE OVERHEAD of the AI endpoints
/// (auth → validation → rate-limit → safety-layer → fail-closed SSE error frame),
/// NOT model latency. The real AI gateway is dormant without API keys and
/// fails closed (AiError.Unavailable), surfaced as an SSE "event: error" frame.
///
/// The response status is 200 (SSE stream always opens); the SSE body contains
/// "event: error" or "event: done". We treat any 200 + readable body as "ok"
/// for the overhead measurement.
///
/// DEVOPS PROCEDURE — live &lt;4 s measurement:
///   The authoritative AI &lt;4 s number (NFR-1) is produced by devops with live keys:
///   1. Set Ai__Providers__Claude__ApiKey (or OpenAI equivalent) in the environment.
///   2. Run the Host: dotnet run --project backend/src/Host/Learnexia.Host
///   3. Set PERF_BASE_URL=http://localhost:5080 (or staging URL).
///   4. Run this scenario against the live host with PERF_AI_LIVE=true.
///   5. The p95 of S13_AiExplainOverhead must be &lt; 4000 ms with live keys.
///   This local in-process run does NOT produce the live AI measurement.
///   See docs/perf/P6-01-baseline.md for the caveat.
///
/// Concurrency: low (2 copies) to respect the in-handler rate limit of ~10/min/student.
/// The AI rate limit is keyed per student; with a single perf student the limit fires quickly
/// above ~10 req/min. We run at 2 copies for 30 s to avoid the rate-limit distorting numbers.
/// There is NO p95 assertion on this scenario locally (threshold = devops with live keys).
/// </summary>
internal static class AiOverheadScenario
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    // Low concurrency: respect ~10/min/student in-handler rate limit
    private const int AiConcurrentCopies = 2;
    private const int AiDurationSeconds  = 30;

    public static ScenarioProps[] BuildAll(HttpClient client, PerfContext ctx)
    {
        return
        [
            S13_AiExplainOverhead(client, ctx),
            S14_AiHintOverhead(client, ctx),
        ];
    }

    // ------------------------------------------------------------------
    // S13 — AI Explain pipeline overhead
    // Endpoint: POST /api/AiTutor/Explain   SSE response
    // Without keys: fails-closed → SSE event:error frame (HTTP 200, SSE body with error)
    // ------------------------------------------------------------------

    private static ScenarioProps S13_AiExplainOverhead(HttpClient client, PerfContext ctx)
    {
        return Scenario.Create("S13_AiExplainOverhead_PipelineOnly", async context =>
        {
            if (ctx.LessonId <= 0) return Response.Ok(sizeBytes: 0);

            var body = new
            {
                lessonId  = ctx.LessonId,
                question  = "Can you explain this concept?",
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "api/AiTutor/Explain")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body, JsonOpts),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.StudentToken);
            // Tell the HTTP client to buffer the full SSE stream (don't stream — just collect the overhead)
            request.Headers.Accept.TryParseAdd("text/event-stream");

            var response = await client.SendAsync(request, context.ScenarioCancellationToken);

            // SSE endpoint returns 200 even on fail-closed; read the body to force the stream to complete.
            _ = await response.Content.ReadAsStringAsync(context.ScenarioCancellationToken);

            // 200 = SSE stream opened (overhead measured); 429 = rate-limit hit (expected at low concurrency)
            // 401/403 = auth misconfiguration (treat as fail)
            var statusCode = (int)response.StatusCode;
            return statusCode is 200 or 429
                ? Response.Ok(statusCode: statusCode.ToString())
                : Response.Fail(statusCode: statusCode.ToString());
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: AiConcurrentCopies, during: TimeSpan.FromSeconds(AiDurationSeconds)));
    }

    // ------------------------------------------------------------------
    // S14 — AI Hint pipeline overhead
    // Endpoint: POST /api/AiTutor/Hint   SSE response
    // Requires a live attemptId + questionId; skip if warmup did not produce them.
    // ------------------------------------------------------------------

    private static ScenarioProps S14_AiHintOverhead(HttpClient client, PerfContext ctx)
    {
        return Scenario.Create("S14_AiHintOverhead_PipelineOnly", async context =>
        {
            if (ctx.AttemptId <= 0 || ctx.QuestionId <= 0)
                return Response.Ok(sizeBytes: 0);

            var body = new
            {
                questionId  = ctx.QuestionId,
                attemptId   = ctx.AttemptId,
                intent      = "Hint",   // HintIntent enum: Hint | WhyWrong
                hintLevel   = 1,
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "api/AiTutor/Hint")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body, JsonOpts),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.StudentToken);
            request.Headers.Accept.TryParseAdd("text/event-stream");

            var response = await client.SendAsync(request, context.ScenarioCancellationToken);
            _ = await response.Content.ReadAsStringAsync(context.ScenarioCancellationToken);

            var statusCode = (int)response.StatusCode;
            return statusCode is 200 or 429
                ? Response.Ok(statusCode: statusCode.ToString())
                : Response.Fail(statusCode: statusCode.ToString());
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.KeepConstant(copies: AiConcurrentCopies, during: TimeSpan.FromSeconds(AiDurationSeconds)));
    }
}
