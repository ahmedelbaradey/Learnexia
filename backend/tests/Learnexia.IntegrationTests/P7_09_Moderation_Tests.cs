using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Ai;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P7-09 integration tests: Admin Moderation Queue.
///
/// Endpoints under test:
///   GET  api/Admin/Moderation/Queue    — paged + filtered moderation queue (AdminOnly)
///   GET  api/Admin/Moderation/{id}     — single item detail (AdminOnly)
///   POST api/Admin/Moderation/{id}/Review — review a pending item (AdminOnly)
///
/// Cases covered:
///   BE-TC-AUTH : anonymous/parent/student → 401/403; admin → 200 on all three routes.
///   BE-TC-EMPTY: empty queue → 200 with empty paged result (not 500).
///   BE-TC-INGEST: dispatch AiOutputFlaggedIntegrationEvent in-process → item appears (Status=Pending, Source=AiOutput).
///   BE-TC-IDEM : duplicate SourceEventId → only ONE row persisted.
///   BE-TC-VERDICT: SafetyVerdict.failedChecks is a real JSON array, NOT a double-escaped string.
///   BE-TC-DETAIL: GET /{id} → 200 with verdict/facets/contentReference; unknown id → 404 (not 500).
///   BE-TC-REVIEW: Approve happy path; Reject without reason → 422; re-review terminal → 400; filter drops reviewed items.
///   BE-TC-FILTER: status= and source= query params filter DB-side; paging keys present.
///
/// Docker + Postgres required to RUN (Testcontainers). Compile-only on Windows CI (no Docker daemon).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_09_Moderation_Tests : IAsyncLifetime
{
    // ── URL constants ──────────────────────────────────────────────────────────
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string ModerationQueueUrl = "api/Admin/Moderation/Queue";

    // ── Seeded credentials (mirror P7-01 / P7-12 pattern) ─────────────────────
    private const string AdminUserName     = "superadmin";
    private const string AdminPassword     = "123Pa$$word!";
    private const string BasicUserName     = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";

    // ── Infrastructure ────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string? _adminToken;
    private string? _basicToken;    // "basicuser" — not an admin role
    private string? _parentToken;

    public P7_09_Moderation_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Belt-and-suspenders: ensure the P7-09 migration is applied
        // (ApplyMigrationsAndSeedAsync already calls MigrateAsync on ModerationDbContext,
        //  which covers both P7_12_InitialModeration and P7_09_AddModerationItem).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ModerationDbContext>();
            await db.Database.MigrateAsync();
        }

        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken  = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // BE-TC-AUTH: Authorization matrix — all three moderation routes
    // =========================================================================

    [Fact(DisplayName = "BE-TC-AUTH-1: GET /api/Admin/Moderation/Queue anonymous → 401")]
    public async Task Auth1_Queue_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, ModerationQueueUrl, bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Queue is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-2: GET /api/Admin/Moderation/Queue with parent token → 403")]
    public async Task Auth2_Queue_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, ModerationQueueUrl, bearer: _parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Queue is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-3: GET /api/Admin/Moderation/Queue with basicuser token → 403")]
    public async Task Auth3_Queue_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, ModerationQueueUrl, bearer: _basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Queue is AdminOnly — basicuser must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-4: GET /api/Admin/Moderation/Queue with admin token → 200")]
    public async Task Auth4_Queue_Admin_Returns200()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, ModerationQueueUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Queue must return 200 for admin; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-5: GET /api/Admin/Moderation/{id} anonymous → 401")]
    public async Task Auth5_Detail_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, "api/Admin/Moderation/999", bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Detail is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-6: GET /api/Admin/Moderation/{id} with parent token → 403")]
    public async Task Auth6_Detail_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, "api/Admin/Moderation/999", bearer: _parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Detail is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-7: POST /api/Admin/Moderation/{id}/Review anonymous → 401")]
    public async Task Auth7_Review_Anonymous_Returns401()
    {
        // Decision=1 is ModerationStatus.Approved (enum int value; no global JsonStringEnumConverter).
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "api/Admin/Moderation/999/Review",
            new { Decision = 1, Reason = (string?)null }, bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Review is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-8: POST /api/Admin/Moderation/{id}/Review with parent token → 403")]
    public async Task Auth8_Review_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "api/Admin/Moderation/999/Review",
            new { Decision = 1, Reason = (string?)null }, bearer: _parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Review is AdminOnly — parent must get 403; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-EMPTY: Empty queue returns 200 with empty paged result
    // =========================================================================

    [Fact(DisplayName = "BE-TC-EMPTY-1: GET Queue with no items → 200, empty paged result, Successed=true")]
    public async Task Empty1_EmptyQueue_Returns200WithEmptyPage()
    {
        // Filter by a far-future date range that can never have items.
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{ModerationQueueUrl}?DateFrom=2099-01-01&DateTo=2099-12-31",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "empty queue must return 200, not 500; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "successed must be true for an empty paged result; body: {0}", body);

        // Envelope shape
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // PaginatedResult keys must be present even for an empty page.
        TryProp(data, "currentPage", out _).Should().BeTrue("paginatedResult must have currentPage; body: {0}", body);
        TryProp(data, "totalCount", out var totalCountProp).Should().BeTrue("paginatedResult must have totalCount; body: {0}", body);
        TryProp(data, "totalPages", out _).Should().BeTrue("paginatedResult must have totalPages; body: {0}", body);
        TryProp(data, "pageSize", out _).Should().BeTrue("paginatedResult must have pageSize; body: {0}", body);

        totalCountProp.GetInt32().Should().Be(0, "totalCount must be 0 for an empty queue; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-INGEST: Dispatch AiOutputFlaggedIntegrationEvent in-process → item appears
    // =========================================================================

    [Fact(DisplayName = "BE-TC-INGEST-1: dispatching AiOutputFlaggedIntegrationEvent → item appears in queue (Status=Pending, Source=AiOutput)")]
    public async Task Ingest1_DispatchEvent_ItemAppearsInQueue()
    {
        // Arrange: produce a unique SourceEventId to avoid cross-test collision.
        var sourceEventId = Guid.NewGuid();
        var eventId       = Guid.NewGuid();
        var detectedAt    = DateTime.UtcNow;

        await PublishAiOutputFlaggedEventAsync(
            sourceEventId:    sourceEventId,
            eventId:          eventId,
            contentReference: $"safety-event-{sourceEventId}",
            failedChecks:     "[\"ToxicityCheck\"]",
            reasonCodes:      "[\"TOXICITY_HIGH\"]",
            actionTaken:      "Blocked",
            taskKind:         "Hint",
            studentId:        42,
            subjectCode:      "Math",
            grade:            5,
            detectedAt:       detectedAt);

        // Poll the queue until the item appears (handler is in-process; typically <500 ms).
        var item = await PollForModerationItemAsync(
            filter: $"pageSize=50",
            matchPredicate: el =>
                TryProp(el, "sourceEventId", out var evProp) &&
                evProp.GetString() == sourceEventId.ToString(),
            timeoutSeconds: 5);

        item.Should().NotBeNull(
            "AiOutputFlaggedIntegrationEvent dispatch must produce a ModerationItem in the queue. " +
            "If null, check AiOutputFlaggedEventHandler + ModerationQueueWriter registration in ModerationApplication DI.");

        // Status must be Pending.
        TryProp(item!.Value, "status", out var statusProp).Should().BeTrue("item must have status; body: {0}", item.Value.ToString());
        statusProp.GetString().Should().Be("Pending", "newly-enqueued item must have Status=Pending");

        // Source must be AiOutput.
        TryProp(item.Value, "source", out var sourceProp).Should().BeTrue("item must have source; body: {0}", item.Value.ToString());
        sourceProp.GetString().Should().Be("AiOutput", "event-sourced item must have Source=AiOutput");
    }

    // =========================================================================
    // BE-TC-IDEM: Idempotency — same SourceEventId published twice → ONE row
    // =========================================================================

    [Fact(DisplayName = "BE-TC-IDEM-1: same SourceEventId published twice → exactly ONE row in queue")]
    public async Task Idem1_DuplicateSourceEventId_OnlyOneRow()
    {
        var sourceEventId = Guid.NewGuid();
        var detectedAt    = DateTime.UtcNow;

        // Publish the same SourceEventId twice.
        await PublishAiOutputFlaggedEventAsync(
            sourceEventId, Guid.NewGuid(), $"idem-ref-{sourceEventId}",
            "[\"ToxicityCheck\"]", "[\"TOXICITY_HIGH\"]", "Blocked", "Explain",
            studentId: 10, subjectCode: "Arabic", grade: 3, detectedAt: detectedAt);

        await PublishAiOutputFlaggedEventAsync(
            sourceEventId, Guid.NewGuid(), $"idem-ref-{sourceEventId}-dup",
            "[\"ToxicityCheck\"]", "[\"TOXICITY_HIGH\"]", "Blocked", "Explain",
            studentId: 10, subjectCode: "Arabic", grade: 3, detectedAt: detectedAt);

        // Give the async handler a moment.
        await Task.Delay(500);

        // Count rows in the DB directly via ModerationDbContext.
        int count;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ModerationDbContext>();
            count = await db.ModerationItems
                .CountAsync(m => m.SourceEventId == sourceEventId);
        }

        count.Should().Be(1,
            "idempotency: publishing the same SourceEventId twice must yield exactly ONE row; " +
            "the unique index on moderation.ModerationItems.SourceEventId must prevent duplicates");
    }

    // =========================================================================
    // BE-TC-VERDICT: SafetyVerdict must NOT be double-escaped
    // =========================================================================

    [Fact(DisplayName = "BE-TC-VERDICT-1: SafetyVerdict.failedChecks is a real JSON array, not a double-escaped string")]
    public async Task Verdict1_SafetyVerdict_FailedChecks_IsRealArray()
    {
        // Seed an item whose FailedChecks arrives as a JSON-array string (the raw event contract).
        var sourceEventId = Guid.NewGuid();
        await PublishAiOutputFlaggedEventAsync(
            sourceEventId, Guid.NewGuid(), $"verdict-ref-{sourceEventId}",
            failedChecks: "[\"ToxicityCheck\"]",
            reasonCodes:  "[\"TOXICITY_HIGH\"]",
            actionTaken:  "Blocked",
            taskKind:     "WhyWrong",
            studentId:    7, subjectCode: "Science", grade: 6,
            detectedAt:   DateTime.UtcNow);

        // Poll until the item appears.
        var item = await PollForModerationItemAsync(
            filter: $"pageSize=50",
            matchPredicate: el =>
                TryProp(el, "sourceEventId", out var evProp) &&
                evProp.GetString() == sourceEventId.ToString(),
            timeoutSeconds: 5);

        item.Should().NotBeNull("item must appear in queue after dispatch");

        // The safetyVerdict field is a JSON string from the DB (stored as jsonb, serialized to the DTO as string).
        TryProp(item!.Value, "safetyVerdict", out var verdictProp).Should().BeTrue(
            "ModerationItemDto must have safetyVerdict; body: {0}", item.Value.ToString());

        var verdictStr = verdictProp.GetString() ?? verdictProp.ToString();

        // The key assertion: failedChecks must NOT contain escaped-quote sequences like \\"ToxicityCheck\\"
        // which would indicate double-JSON-serialization.
        verdictStr.Should().NotContain("\\\"",
            "SafetyVerdict must not contain double-escaped quotes (\\\" sequences). " +
            "If it does, AiOutputFlaggedEventHandler.ParseJsonOrLiteral is not working — " +
            "FailedChecks is being stored as a JSON-escaped string rather than an embedded array. " +
            "The stored value is: {0}", verdictStr);

        // The value must contain the actual check name directly — not nested inside extra quotes.
        verdictStr.Should().Contain("ToxicityCheck",
            "SafetyVerdict must contain the check name 'ToxicityCheck' as plain text within the JSON; " +
            "actual safetyVerdict: {0}", verdictStr);

        // Parse the stored verdict as JSON and verify failedChecks is an array.
        JsonDocument verdictDoc;
        try
        {
            verdictDoc = JsonDocument.Parse(verdictStr);
        }
        catch (JsonException ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"SafetyVerdict is not valid JSON. Stored value: '{verdictStr}'. Parse error: {ex.Message}");
        }

        TryProp(verdictDoc.RootElement, "failedChecks", out var failedChecksProp).Should().BeTrue(
            "SafetyVerdict JSON must have a 'failedChecks' key; actual verdict: {0}", verdictStr);

        failedChecksProp.ValueKind.Should().Be(JsonValueKind.Array,
            "SafetyVerdict.failedChecks must be a JSON array (not a string). " +
            "If it is a string, AiOutputFlaggedEventHandler.ParseJsonOrLiteral failed to parse the " +
            "raw '[\"ToxicityCheck\"]' and fell back to storing it as a literal string. " +
            "Actual verdict: {0}", verdictStr);

        // The array must contain at least one entry — "ToxicityCheck".
        var failedChecksArr = failedChecksProp.EnumerateArray().Select(e => e.GetString()).ToList();
        failedChecksArr.Should().Contain("ToxicityCheck",
            "failedChecks array must contain 'ToxicityCheck'; actual: [{0}]",
            string.Join(", ", failedChecksArr));
    }

    // =========================================================================
    // BE-TC-DETAIL: GET /{id} returns full detail; unknown id → 404 (not 500)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-DETAIL-1: GET /api/Admin/Moderation/{id} for seeded item → 200 with detail fields")]
    public async Task Detail1_GetItem_Returns200WithDetailFields()
    {
        // Seed an item.
        var sourceEventId = Guid.NewGuid();
        var contentRef    = $"detail-ref-{sourceEventId}";
        await PublishAiOutputFlaggedEventAsync(
            sourceEventId, Guid.NewGuid(), contentRef,
            "[\"ToxicityCheck\"]", "[\"TOXICITY_HIGH\"]", "Blocked", "Hint",
            studentId: 55, subjectCode: "English", grade: 7, detectedAt: DateTime.UtcNow);

        // Wait for the item to land.
        await Task.Delay(600);

        // Retrieve the item's id from DB directly.
        int itemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ModerationDbContext>();
            var row = await db.ModerationItems
                .FirstOrDefaultAsync(m => m.SourceEventId == sourceEventId);
            row.Should().NotBeNull("item must be persisted after event dispatch");
            itemId = row!.Id;
        }

        // GET the detail endpoint.
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"api/Admin/Moderation/{itemId}", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /{id} for an existing item must return 200; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true for an existing item; body: {0}", body);

        TryProp(root, "data", out var dataEl).Should().BeTrue("body: {0}", body);

        // Required detail fields.
        TryProp(dataEl, "id", out _).Should().BeTrue("detail must have id; body: {0}", body);
        TryProp(dataEl, "sourceEventId", out var sevProp).Should().BeTrue("detail must have sourceEventId; body: {0}", body);
        sevProp.GetString().Should().Be(sourceEventId.ToString(), "sourceEventId must match what was seeded");
        TryProp(dataEl, "contentReference", out var refProp).Should().BeTrue("detail must have contentReference; body: {0}", body);
        refProp.GetString().Should().Be(contentRef, "contentReference must match what was seeded");
        TryProp(dataEl, "safetyVerdict", out _).Should().BeTrue("detail must have safetyVerdict; body: {0}", body);
        TryProp(dataEl, "source", out _).Should().BeTrue("detail must have source; body: {0}", body);
        TryProp(dataEl, "status", out _).Should().BeTrue("detail must have status; body: {0}", body);
        TryProp(dataEl, "detectedAt", out _).Should().BeTrue("detail must have detectedAt; body: {0}", body);

        // Review fields must exist (nullable, so null is fine).
        TryProp(dataEl, "reviewedByUserId", out _).Should().BeTrue("detail must have reviewedByUserId (nullable); body: {0}", body);
        TryProp(dataEl, "reviewedAtUtc", out _).Should().BeTrue("detail must have reviewedAtUtc (nullable); body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-DETAIL-2: GET /api/Admin/Moderation/{id} for unknown id → 404 (not 500)")]
    public async Task Detail2_UnknownId_Returns404()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            "api/Admin/Moderation/999999999", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Unknown id must return 404, not 500; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false for a 404; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-REVIEW: Review happy path + validation rules
    // =========================================================================

    [Fact(DisplayName = "BE-TC-REVIEW-1: POST Review {decision:Approved} on Pending item → 200, status flips to Approved, ReviewedBy/At set")]
    public async Task Review1_ApprovePendingItem_FlipsToApproved()
    {
        int itemId = await SeedModerationItemAsync();

        // Decision=1 is ModerationStatus.Approved.
        // System.Text.Json deserializes enums by integer value by default (no global JsonStringEnumConverter).
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"api/Admin/Moderation/{itemId}/Review",
            new { Decision = 1, Reason = (string?)null },
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Approve on a Pending item must return 200; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true for a successful review; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "status", out var statusProp).Should().BeTrue("body: {0}", body);
        statusProp.GetString().Should().Be("Approved", "status must flip to Approved after the review; body: {0}", body);

        // ReviewedByUserId must be set (non-null, non-zero).
        TryProp(data, "reviewedByUserId", out _); // field is on ModerationItemDetailDto; queue endpoint returns ModerationItemDto without it — that's expected
        // The handler returns ModerationItemDto (not detail). Just confirm the status field on the response.
    }

    [Fact(DisplayName = "BE-TC-REVIEW-2: POST Review {decision:Rejected} without reason → 422 (validation failure)")]
    public async Task Review2_RejectWithoutReason_Returns422()
    {
        int itemId = await SeedModerationItemAsync();

        // Decision=2 is ModerationStatus.Rejected. Reject without reason → ValidationBehavior → 422.
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"api/Admin/Moderation/{itemId}/Review",
            new { Decision = 2, Reason = (string?)null },
            bearer: _adminToken);

        ((int)resp.StatusCode).Should().Be(422,
            "Reject without reason must return 422 (ValidationBehavior); body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-REVIEW-3: POST Review on a terminal item (Approved) → 400 (not 500)")]
    public async Task Review3_ReReviewTerminalItem_Returns400()
    {
        int itemId = await SeedModerationItemAsync();

        // Approve it first (Decision=1 = Approved; transitions to terminal state).
        var (approveResp, _, approveBody) = await SendAsync(HttpMethod.Post,
            $"api/Admin/Moderation/{itemId}/Review",
            new { Decision = 1, Reason = (string?)null },
            bearer: _adminToken);
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Initial approval must succeed; body: {0}", approveBody);

        // Try to re-review the now-terminal item (Decision=2 = Rejected).
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"api/Admin/Moderation/{itemId}/Review",
            new { Decision = 2, Reason = "Changed mind" },
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Re-reviewing a terminal (Approved/Rejected) item must return 400, not 500; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false for a terminal-item re-review; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-REVIEW-4: after Approve, filter Queue by Status=Pending → reviewed item no longer appears")]
    public async Task Review4_AfterApprove_ItemDropsFromPendingFilter()
    {
        // Seed and approve an item.
        int itemId = await SeedModerationItemAsync();
        Guid? sourceEventId = null;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ModerationDbContext>();
            var row = await db.ModerationItems.FindAsync(itemId);
            row.Should().NotBeNull("seeded item must exist");
            sourceEventId = row!.SourceEventId;
        }

        var (approveResp, _, approveBody) = await SendAsync(HttpMethod.Post,
            $"api/Admin/Moderation/{itemId}/Review",
            new { Decision = 1, Reason = (string?)null },
            bearer: _adminToken);
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Approval must succeed; body: {0}", approveBody);

        // Query Queue filtered to Pending — the approved item must not appear.
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{ModerationQueueUrl}?Status=Pending&PageSize=100",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var items = ExtractModerationItems(root, body);
        var matchingItem = items.FirstOrDefault(el =>
            TryProp(el, "sourceEventId", out var evProp) &&
            evProp.GetString() == sourceEventId.ToString());

        // The approved item must not appear in the Pending sub-queue.
        (matchingItem.ValueKind == JsonValueKind.Undefined || matchingItem.ValueKind == JsonValueKind.Null)
            .Should().BeTrue(
                "an Approved item must not appear in a Pending filter; " +
                "item sourceEventId={0} was still found in the Pending queue", sourceEventId);
    }

    // =========================================================================
    // BE-TC-FILTER: Queue filters (status, source) and paging keys
    // =========================================================================

    [Fact(DisplayName = "BE-TC-FILTER-1: Queue envelope has BaseResponse keys statusCode/successed/message/data/errors")]
    public async Task Filter1_QueueEnvelope_HasBaseResponseKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{ModerationQueueUrl}?PageNumber=1&PageSize=10", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var succ).Should().BeTrue("envelope must have successed (sic); body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-FILTER-2: Queue data has PaginatedResult keys currentPage/totalCount/totalPages/pageSize")]
    public async Task Filter2_QueueData_HasPaginationKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{ModerationQueueUrl}?PageNumber=1&PageSize=10", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "currentPage", out _).Should().BeTrue("data must have currentPage; body: {0}", body);
        TryProp(data, "totalCount", out _).Should().BeTrue("data must have totalCount; body: {0}", body);
        TryProp(data, "totalPages", out _).Should().BeTrue("data must have totalPages; body: {0}", body);
        TryProp(data, "pageSize", out _).Should().BeTrue("data must have pageSize; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-FILTER-3: pageSize=9999 is clamped to ≤100")]
    public async Task Filter3_PageSize9999_IsClamped()
    {
        // Seed at least one item so we can assert clamping is meaningful.
        await SeedModerationItemAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{ModerationQueueUrl}?PageSize=9999", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "pageSize", out var pageSizeProp).Should().BeTrue("body: {0}", body);
        pageSizeProp.GetInt32().Should().BeLessThanOrEqualTo(100,
            "pageSize must be clamped to ≤100 to prevent unbounded scans; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-FILTER-4: filter Status=Approved returns only Approved rows")]
    public async Task Filter4_StatusFilter_ReturnsOnlyMatchingRows()
    {
        // Seed and approve an item so there is at least one Approved row.
        int itemId = await SeedModerationItemAsync();
        await SendAsync(HttpMethod.Post,
            $"api/Admin/Moderation/{itemId}/Review",
            new { Decision = 1, Reason = (string?)null },
            bearer: _adminToken);

        // Query for Approved rows.
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{ModerationQueueUrl}?Status=Approved&PageSize=50", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var items = ExtractModerationItems(root, body);

        foreach (var item in items)
        {
            TryProp(item, "status", out var statusProp).Should().BeTrue("body: {0}", body);
            statusProp.GetString().Should().Be("Approved",
                "filter Status=Approved must return only Approved rows; body: {0}", body);
        }
    }

    [Fact(DisplayName = "BE-TC-FILTER-5: filter Source=AiOutput returns only AiOutput rows")]
    public async Task Filter5_SourceFilter_ReturnsOnlyMatchingRows()
    {
        // Ensure at least one AiOutput row exists.
        await SeedModerationItemAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{ModerationQueueUrl}?Source=AiOutput&PageSize=50", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var items = ExtractModerationItems(root, body);

        foreach (var item in items)
        {
            TryProp(item, "source", out var sourceProp).Should().BeTrue("body: {0}", body);
            sourceProp.GetString().Should().Be("AiOutput",
                "filter Source=AiOutput must return only AiOutput rows; body: {0}", body);
        }
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Seeds a single ModerationItem by dispatching an AiOutputFlaggedIntegrationEvent
    /// through the running app's MediatR, waits for it to land, then returns its DB id.
    /// </summary>
    private async Task<int> SeedModerationItemAsync()
    {
        var sourceEventId = Guid.NewGuid();
        await PublishAiOutputFlaggedEventAsync(
            sourceEventId, Guid.NewGuid(), $"seed-ref-{sourceEventId}",
            "[\"ToxicityCheck\"]", "[\"TOXICITY_HIGH\"]", "Blocked", "Hint",
            studentId: 0, subjectCode: null, grade: null, detectedAt: DateTime.UtcNow);

        // Poll the DB until the row appears (writer is in-process; typically <500 ms).
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ModerationDbContext>();
            var row = await db.ModerationItems
                .FirstOrDefaultAsync(m => m.SourceEventId == sourceEventId);
            if (row is not null)
                return row.Id;
            await Task.Delay(200);
        }

        throw new InvalidOperationException(
            $"SeedModerationItemAsync: item for SourceEventId={sourceEventId} did not appear after 5 s. " +
            "Check AiOutputFlaggedEventHandler and ModerationQueueWriter registration.");
    }

    /// <summary>
    /// Publishes an <see cref="AiOutputFlaggedIntegrationEvent"/> through the running app's MediatR
    /// bus (the IPublisher/IMediator registered in the DI container). This exercises the full
    /// AiOutputFlaggedEventHandler → ModerationQueueWriter path.
    /// </summary>
    private async Task PublishAiOutputFlaggedEventAsync(
        Guid sourceEventId,
        Guid eventId,
        string contentReference,
        string failedChecks,
        string reasonCodes,
        string actionTaken,
        string taskKind,
        int studentId,
        string? subjectCode,
        int? grade,
        DateTime detectedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var @event = new AiOutputFlaggedIntegrationEvent(
            EventId:          eventId,
            SourceEventId:    sourceEventId,
            ContentReference: contentReference,
            FailedChecks:     failedChecks,
            ReasonCodes:      reasonCodes,
            ActionTaken:      actionTaken,
            ModelId:          "gpt-4o-mini",
            TaskKind:         taskKind,
            StudentId:        studentId,
            SubjectCode:      subjectCode,
            Grade:            grade,
            DetectedAt:       detectedAt);

        await publisher.Publish(@event);
    }

    /// <summary>
    /// Polls GET /api/Admin/Moderation/Queue?{filter} every 300 ms until
    /// <paramref name="matchPredicate"/> returns true for at least one item,
    /// or <paramref name="timeoutSeconds"/> elapses.
    /// </summary>
    private async Task<JsonElement?> PollForModerationItemAsync(
        string filter,
        Func<JsonElement, bool> matchPredicate,
        int timeoutSeconds = 5)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var (resp, root, _) = await SendAsync(HttpMethod.Get,
                $"{ModerationQueueUrl}?{filter}", bearer: _adminToken);

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var items = ExtractModerationItems(root, "");
                var match = items.FirstOrDefault(matchPredicate);
                if (match.ValueKind != JsonValueKind.Undefined)
                    return match;
            }
            await Task.Delay(300);
        }
        return null;
    }

    /// <summary>Extracts the inner data[] array from a PaginatedResult envelope.</summary>
    private static List<JsonElement> ExtractModerationItems(JsonElement root, string bodyStr)
    {
        if (!TryProp(root, "data", out var outer)) return [];
        // outer is PaginatedResult<ModerationItemDto>; its inner 'data' is the array.
        if (!TryProp(outer, "data", out var inner)) return [];
        if (inner.ValueKind != JsonValueKind.Array) return [];
        return inner.EnumerateArray().ToList();
    }

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var response = await _client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>Case-insensitive JSON property lookup (camelCase + PascalCase + linear scan).</summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object) { value = default; return false; }
        if (element.TryGetProperty(name, out value)) return true;
        if (element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value)) return true;
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private async Task<string> SignInGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in prerequisite must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<string> RegisterParentGetTokenAsync()
    {
        var email = $"p709_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }
}
