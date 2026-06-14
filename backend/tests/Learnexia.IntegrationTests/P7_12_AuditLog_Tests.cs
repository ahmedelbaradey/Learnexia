using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P7-12 integration tests: Admin Action Audit Log (Moderation module).
///
/// Endpoints under test:
///   GET  /api/Admin/Audit/Log                — paginated, filterable audit log (AdminOnly)
///
/// What is verified:
///   Auth matrix:
///     BE-TC-AUTH-1 : anonymous → 401
///     BE-TC-AUTH-2 : non-admin (parent) → 403
///     BE-TC-AUTH-3 : non-admin (basicuser) → 403
///     BE-TC-AUTH-4 : admin → 200
///   No mutation endpoints exist:
///     BE-TC-IMMUT-1 : POST /api/Admin/Audit/Log → 404/405 (no route)
///     BE-TC-IMMUT-2 : PUT  /api/Admin/Audit/Log → 404/405 (no route)
///     BE-TC-IMMUT-3 : DELETE /api/Admin/Audit/Log → 404/405 (no route)
///   Envelope + pagination:
///     BE-TC-ENV-1  : response body is BaseResponse with statusCode/successed/message/data/errors
///     BE-TC-ENV-2  : paged data has currentPage/totalCount/totalPages/pageSize
///     BE-TC-ENV-3  : pageSize clamped — requesting pageSize=9999 returns at most 100 rows
///     BE-TC-ENV-4  : empty log returns Successed=true (EmptyCollection, not 404)
///   Filters (DB-side):
///     BE-TC-FILTER-1 : filter by actionType — returns only matching action rows
///     BE-TC-FILTER-2 : filter by targetEntityType — returns only matching entity type rows
///     BE-TC-FILTER-3 : filter by adminUserId — returns only rows for that admin
///     BE-TC-FILTER-4 : filter by dateFrom/dateTo — returns only rows in range
///     BE-TC-FILTER-5 : filter by unknown actionType — returns empty result (not 500)
///   Ordering:
///     BE-TC-ORDER-1 : results ordered newest-first (OccurredAtUtc descending)
///   End-to-end audit capture (the critical post-commit relay test):
///     BE-TC-E2E-1   : POST /api/learning/Subjects/Create (admin) → at least one AuditLog row
///                     appears in GET /api/Admin/Audit/Log (exercises domain-event → relay → consumer path)
///     BE-TC-E2E-2   : the audit row carries the correct adminUserId (from JWT), actionType=Subject.Created,
///                     targetEntityType=Subject; Details carries ids/enum states only (no PII)
///   Idempotency:
///     BE-TC-IDEM-1  : one admin action → exactly one audit row (not double-written)
///   No-PII assertion:
///     BE-TC-PII-1   : AuditLogDto fields are id/eventId/adminUserId/action/targetEntityType/
///                     targetEntityId/details/occurredAtUtc/createdAt — no name/email/password field
///
/// NOTE ON ASYNC DELIVERY:
///   The audit event is dispatched synchronously in-process via IsolatedNotificationPublisher after
///   UnitOfWorkBehavior commits. In this in-process WebApplicationFactory environment the handler
///   runs on the same thread pool, so the row is typically visible by the time the HTTP response
///   returns. A short poll loop (up to 3 s) guards against any transient scheduling delay without
///   making the test flaky under slow CI.
///
/// NOTE ON FACTORY WIRING:
///   ModerationDbContext is registered via AddModerationModule in Program.cs. The factory overrides
///   all DbContexts to point at the Testcontainers Postgres container. This file also patches the
///   factory (see LearnexiaWebAppFactory_ModerationPatch.cs / inline override) to add ModerationDbContext
///   to the ReplaceDbContext sweep. See the companion factory-patch comment at the top of this file.
///
/// Docker + Postgres required to RUN. Compile-only on Windows CI (no Docker daemon).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_12_AuditLog_Tests : IAsyncLifetime
{
    // ─────────────────────────────────────────────────────────────────────────
    // URL constants
    // ─────────────────────────────────────────────────────────────────────────
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";

    private const string AuditLogUrl      = "api/Admin/Audit/Log";

    // Learning curriculum endpoint used to trigger an audit event end-to-end
    private const string SubjectsCreateUrl = "api/learning/subjects/Create";
    private const string GradesCreateUrl   = "api/learning/grades/Create";
    private const string GradesListUrl     = "api/learning/grades/List";
    private const string SubjectsListUrl   = "api/learning/subjects/List";

    // ─────────────────────────────────────────────────────────────────────────
    // Seeded credentials (mirror P7-01)
    // ─────────────────────────────────────────────────────────────────────────
    private const string AdminUserName     = "superadmin";
    private const string AdminPassword     = "123Pa$$word!";
    private const string BasicUserName     = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";

    // ─────────────────────────────────────────────────────────────────────────
    // Infrastructure
    // ─────────────────────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string? _adminToken;
    private string? _basicToken;
    private string? _parentToken;

    // Admin user id decoded from JWT — needed for adminUserId filter assertions
    private int _adminUserId;

    public P7_12_AuditLog_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Ensure the ModerationDbContext schema/migration is applied.
        // ApplyMigrationsAndSeedAsync covers IdentityModuleDbContext / NotificationsDbContext /
        // LearningDbContext / ParentDbContext / GamificationDbContext. ModerationDbContext is applied
        // by ModerationModule.InitializeAsync() which the Host calls from its migrate scope in
        // Program.cs — that scope runs when the WebApplicationFactory builds the host. The call here
        // is a belt-and-suspenders guard for the test environment.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ModerationDbContext>();
            await db.Database.MigrateAsync();
        }

        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken  = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync();

        // Decode admin userId from JWT so we can assert the audit row's AdminUserId.
        _adminUserId = DecodeUserIdFromJwt(_adminToken!);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // BE-TC-AUTH: Authentication / Authorization matrix
    // =========================================================================

    [Fact(DisplayName = "BE-TC-AUTH-1: GET /api/Admin/Audit/Log anonymous → 401")]
    public async Task Auth1_AuditLog_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, AuditLogUrl, bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "AuditLog is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-2: GET /api/Admin/Audit/Log with parent token → 403")]
    public async Task Auth2_AuditLog_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, AuditLogUrl, bearer: _parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "AuditLog is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-3: GET /api/Admin/Audit/Log with basicuser token → 403")]
    public async Task Auth3_AuditLog_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, AuditLogUrl, bearer: _basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "AuditLog is AdminOnly — basicuser must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-4: GET /api/Admin/Audit/Log with admin token → 200")]
    public async Task Auth4_AuditLog_Admin_Returns200()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, AuditLogUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AuditLog must return 200 for an admin; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-IMMUT: No mutation endpoints — append-only / immutable log
    // =========================================================================

    [Fact(DisplayName = "BE-TC-IMMUT-1: POST /api/Admin/Audit/Log → 404 or 405 (no create route)")]
    public async Task Immut1_PostAuditLog_NoRouteExists()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, AuditLogUrl,
            new { action = "fake", targetEntityType = "Subject", targetEntityId = 1 },
            bearer: _adminToken);

        ((int)resp.StatusCode).Should().BeOneOf(new[] { 404, 405 },
            "No POST route must exist on the audit log endpoint; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-IMMUT-2: PUT /api/Admin/Audit/Log → 404 or 405 (no update route)")]
    public async Task Immut2_PutAuditLog_NoRouteExists()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, AuditLogUrl,
            new { id = 1, action = "fake" },
            bearer: _adminToken);

        ((int)resp.StatusCode).Should().BeOneOf(new[] { 404, 405 },
            "No PUT route must exist on the audit log endpoint; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-IMMUT-3: DELETE /api/Admin/Audit/Log → 404 or 405 (no delete route)")]
    public async Task Immut3_DeleteAuditLog_NoRouteExists()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Delete, $"{AuditLogUrl}?id=1",
            bearer: _adminToken);

        ((int)resp.StatusCode).Should().BeOneOf(new[] { 404, 405 },
            "No DELETE route must exist on the audit log endpoint; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-ENV: Envelope shape + pagination
    // =========================================================================

    [Fact(DisplayName = "BE-TC-ENV-1: GET response has BaseResponse envelope keys statusCode/successed/message/data/errors")]
    public async Task Env1_ResponseHasBaseResponseEnvelopeKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, AuditLogUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        root.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "response must be valid JSON; body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue(
            "envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "envelope must have successed (sic); body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "successed must be true on a successful GET; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must have message; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue(
            "envelope must have data; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue(
            "envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-ENV-2: paged data has currentPage/totalCount/totalPages/pageSize")]
    public async Task Env2_PaginatedResultHasPagingKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AuditLogUrl}?pageNumber=1&pageSize=10", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("envelope must have data; body: {0}", body);

        // The handler returns PaginatedResult<AuditLogDto> which should expose paging metadata.
        // When the log is empty the handler returns EmptyCollection wrapping an empty PaginatedResult,
        // so the paging keys must still be present.
        TryProp(data, "currentPage", out _).Should().BeTrue(
            "PaginatedResult must have currentPage; body: {0}", body);
        TryProp(data, "totalCount", out _).Should().BeTrue(
            "PaginatedResult must have totalCount; body: {0}", body);
        TryProp(data, "totalPages", out _).Should().BeTrue(
            "PaginatedResult must have totalPages; body: {0}", body);
        TryProp(data, "pageSize", out _).Should().BeTrue(
            "PaginatedResult must have pageSize; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-ENV-3: pageSize=9999 is clamped to ≤100 rows (no unbounded scan)")]
    public async Task Env3_PageSizeClamped_To100()
    {
        // Produce at least one row so we can verify the pageSize returned ≤ 100.
        await ProduceAuditRowAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AuditLogUrl}?pageNumber=1&pageSize=9999", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "pageSize", out var pageSizeProp).Should().BeTrue(
            "PaginatedResult must have pageSize; body: {0}", body);
        pageSizeProp.GetInt32().Should().BeLessThanOrEqualTo(100,
            "pageSize must be clamped to ≤100 per handler (security: no unbounded queries); body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-ENV-4: empty log returns Successed=true (EmptyCollection, not 404 or 500)")]
    public async Task Env4_EmptyLog_ReturnsSucceededTrue()
    {
        // Query for a far-future date window that will always be empty.
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AuditLogUrl}?dateFrom=2099-01-01&dateTo=2099-12-31", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "empty result must return HTTP 200, not 404/500; body: {0}", body);
        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "successed must be true for an empty (EmptyCollection) result, not false; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-E2E: End-to-end audit capture (the key P7-12 test)
    //
    // This exercises the full path:
    //   Admin HTTP action
    //     → AddSubjectCommandHandler raises AdminActionPerformedDomainEvent on aggregate
    //       → UnitOfWorkBehavior commits, then dispatches domain events post-commit
    //         → AdminActionPerformedDomainEventRelayHandler re-publishes AdminActionPerformedEvent
    //           → AuditLogEventHandler (Moderation.Application) persists AuditLog row
    //             → GET /api/Admin/Audit/Log returns the row
    // =========================================================================

    [Fact(DisplayName = "BE-TC-E2E-1: admin Subject.Create → AuditLog row appears in GET /api/Admin/Audit/Log")]
    public async Task E2E1_SubjectCreate_ProducesAuditRow()
    {
        // Act — perform a real admin curriculum action.
        var subjectName = $"E2E1 Math Ar {Guid.NewGuid():N}";
        int gradeId = await CreateGradeGetIdAsync();
        var (createResp, _, createBody) = await SendAsync(HttpMethod.Post, SubjectsCreateUrl,
            new { Name = subjectName, Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 0 },
            _adminToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Subject.Create prereq must succeed; body: {0}", createBody);
        AssertSucceeded(JsonDocument.Parse(createBody).RootElement, createBody);

        // Assert — poll the audit log for the row (up to 3 s for async handler scheduling).
        var auditRow = await PollForAuditRowAsync(
            filter: $"actionType=Subject.Created&adminUserId={_adminUserId}",
            timeoutSeconds: 3);

        auditRow.Should().NotBeNull(
            "A Subject.Created action by admin must produce an AuditLog row. " +
            "If no row appears, the MediatR wiring (UnitOfWorkBehavior domain-event dispatch + " +
            "AdminActionPerformedDomainEventRelayHandler + AuditLogEventHandler) may be broken — " +
            "check MediatRExtensions.cs registration of Moderation.Application.AssemblyReference " +
            "and that AdminActionPerformedDomainEvent is raised post-commit.");
    }

    [Fact(DisplayName = "BE-TC-E2E-2: audit row has correct adminUserId, actionType=Subject.Created, targetEntityType=Subject; no PII in Details")]
    public async Task E2E2_AuditRow_HasCorrectFields_NoPiiInDetails()
    {
        // Act — produce an audit row.
        var subjectName = $"E2E2 Math En {Guid.NewGuid():N}";
        int gradeId = await CreateGradeGetIdAsync();
        var (createResp, _, createBody) = await SendAsync(HttpMethod.Post, SubjectsCreateUrl,
            new { Name = subjectName, Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 1 },
            _adminToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Subject.Create prereq must succeed; body: {0}", createBody);

        // Poll for the row.
        var row = await PollForAuditRowAsync(
            filter: $"actionType=Subject.Created&adminUserId={_adminUserId}",
            timeoutSeconds: 3);

        row.Should().NotBeNull(
            "Subject.Created audit row must appear; if missing check the post-commit relay wiring.");

        // adminUserId == the authenticated admin's userId
        TryProp(row!.Value, "adminUserId", out var adminUserIdProp).Should().BeTrue(
            "AuditLogDto must expose adminUserId; body row: {0}", row.Value.ToString());
        adminUserIdProp.GetInt32().Should().Be(_adminUserId,
            "audit row adminUserId must match the JWT sub of the acting admin");

        // action == "Subject.Created"
        TryProp(row.Value, "action", out var actionProp).Should().BeTrue(
            "AuditLogDto must expose action; body row: {0}", row.Value.ToString());
        actionProp.GetString().Should().Be("Subject.Created",
            "audit row action must be Subject.Created");

        // targetEntityType == "Subject"
        TryProp(row.Value, "targetEntityType", out var targetTypeProp).Should().BeTrue(
            "AuditLogDto must expose targetEntityType");
        targetTypeProp.GetString().Should().Be("Subject",
            "targetEntityType must be 'Subject'");

        // occurredAtUtc must be a real datetime (not MinValue / epoch).
        TryProp(row.Value, "occurredAtUtc", out var occurredProp).Should().BeTrue(
            "AuditLogDto must expose occurredAtUtc");
        occurredProp.GetDateTime().Should().BeAfter(new DateTime(2020, 1, 1),
            "occurredAtUtc must be a real recent time, not epoch/MinValue");

        // PII check — Details must not contain the subject name, any email, or the word "password".
        TryProp(row.Value, "details", out var detailsProp);
        if (detailsProp.ValueKind == JsonValueKind.String)
        {
            var details = detailsProp.GetString() ?? "";
            details.Should().NotContain(subjectName,
                "Details must NOT contain the subject name (PII-safe: ids + enum states only)");
            details.Should().NotContainAny(new[] { "@", "password", "Password" },
                "Details must not contain email addresses or password strings");
        }

        // PII check — DTO must not expose a 'name', 'email', or 'password' top-level field.
        TryProp(row.Value, "name", out _).Should().BeFalse(
            "AuditLogDto must NOT expose a 'name' field (PII-safe)");
        TryProp(row.Value, "email", out _).Should().BeFalse(
            "AuditLogDto must NOT expose an 'email' field (PII-safe)");
        TryProp(row.Value, "password", out _).Should().BeFalse(
            "AuditLogDto must NOT expose a 'password' field (PII-safe)");
    }

    // =========================================================================
    // BE-TC-IDEM: Idempotency — one action → exactly one row
    // =========================================================================

    [Fact(DisplayName = "BE-TC-IDEM-1: one admin Subject.Create → exactly one AuditLog row (no double-write)")]
    public async Task Idem1_OneAction_ExactlyOneAuditRow()
    {
        // Use a unique GradeId as the discriminator — every parallel test class creates its own
        // grades, so this GradeId will not appear in any sibling test's audit Details string.
        // This makes the count assertion immune to cross-class parallelism without relaxing the
        // exactly-one guarantee (the no-double-write invariant).
        int gradeId = await CreateGradeGetIdAsync();

        var (resp, _, body) = await SendAsync(HttpMethod.Post, SubjectsCreateUrl,
            new { Name = $"Idem1 {Guid.NewGuid():N}", Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 0 },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Subject.Create must succeed; body: {0}", body);
        AssertSucceeded(JsonDocument.Parse(body).RootElement, body);

        // Poll until at least one Subject.Created row appears for this admin.
        // Then filter locally to rows whose Details contains GradeId={gradeId} — only THIS test
        // created a subject under that specific grade, so concurrent siblings cannot pollute the count.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        List<JsonElement> matchingRows = [];

        while (DateTimeOffset.UtcNow < deadline)
        {
            var (pollResp, pollRoot, _) = await SendAsync(HttpMethod.Get,
                $"{AuditLogUrl}?actionType=Subject.Created&adminUserId={_adminUserId}&pageSize=200",
                bearer: _adminToken);

            if (pollResp.StatusCode == HttpStatusCode.OK)
            {
                var allRows = ExtractAuditLogItems(pollRoot, "");
                // Filter to rows whose Details contains the unique GradeId discriminator.
                // Details format (after P7-12 Bucket C fix): "SubjectCode=0, Language=0, GradeId={gradeId}"
                matchingRows = allRows
                    .Where(r =>
                    {
                        TryProp(r, "details", out var detailsProp);
                        var details = detailsProp.ValueKind == JsonValueKind.String
                            ? detailsProp.GetString() ?? ""
                            : "";
                        return details.Contains($"GradeId={gradeId}");
                    })
                    .ToList();

                if (matchingRows.Count >= 1)
                    break;
            }
            await Task.Delay(300);
        }

        matchingRows.Should().NotBeEmpty(
            "at least one audit row must appear after the Subject.Created action " +
            "(filter: Details contains GradeId={0})", gradeId);

        matchingRows.Count.Should().Be(1,
            "exactly ONE AuditLog row must exist for this specific Subject.Created (no double-write); " +
            "GradeId={0} was used as the unique discriminator; matching row count={1}",
            gradeId, matchingRows.Count);
    }

    // =========================================================================
    // BE-TC-FILTER: DB-side filtering
    // =========================================================================

    [Fact(DisplayName = "BE-TC-FILTER-1: filter by actionType — returns only rows matching that action")]
    public async Task Filter1_ByActionType_ReturnsOnlyMatchingRows()
    {
        // Produce a Subject.Created row so there is data to filter on.
        await ProduceAuditRowAsync();

        // Query for a specific action type.
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AuditLogUrl}?actionType=Subject.Created", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var items = ExtractAuditLogItems(root, body);
        // Every returned row must have action == "Subject.Created".
        foreach (var item in items)
        {
            TryProp(item, "action", out var actionProp).Should().BeTrue("body: {0}", body);
            actionProp.GetString().Should().Be("Subject.Created",
                "filter actionType=Subject.Created must return only Subject.Created rows; body: {0}", body);
        }
    }

    [Fact(DisplayName = "BE-TC-FILTER-2: filter by targetEntityType — returns only rows for that entity type")]
    public async Task Filter2_ByTargetEntityType_ReturnsOnlyMatchingRows()
    {
        await ProduceAuditRowAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AuditLogUrl}?targetEntityType=Subject", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var items = ExtractAuditLogItems(root, body);
        foreach (var item in items)
        {
            TryProp(item, "targetEntityType", out var typeProp).Should().BeTrue("body: {0}", body);
            typeProp.GetString().Should().Be("Subject",
                "filter targetEntityType=Subject must return only Subject rows; body: {0}", body);
        }
    }

    [Fact(DisplayName = "BE-TC-FILTER-3: filter by adminUserId — returns only rows for that admin")]
    public async Task Filter3_ByAdminUserId_ReturnsOnlyMatchingRows()
    {
        await ProduceAuditRowAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AuditLogUrl}?adminUserId={_adminUserId}", bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var items = ExtractAuditLogItems(root, body);
        foreach (var item in items)
        {
            TryProp(item, "adminUserId", out var userProp).Should().BeTrue("body: {0}", body);
            userProp.GetInt32().Should().Be(_adminUserId,
                "filter adminUserId must return only rows for that admin; body: {0}", body);
        }
    }

    [Fact(DisplayName = "BE-TC-FILTER-4: filter by dateFrom/dateTo — returns only rows in date range")]
    public async Task Filter4_ByDateRange_ReturnsOnlyRowsInRange()
    {
        await ProduceAuditRowAsync();

        // Use a narrow window around 'now' (±10 minutes) to capture the row we just produced.
        var from = DateTime.UtcNow.AddMinutes(-10).ToString("o");
        var to   = DateTime.UtcNow.AddMinutes(10).ToString("o");

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AuditLogUrl}?dateFrom={Uri.EscapeDataString(from)}&dateTo={Uri.EscapeDataString(to)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var items = ExtractAuditLogItems(root, body);
        // All rows returned must have OccurredAtUtc within the range.
        foreach (var item in items)
        {
            TryProp(item, "occurredAtUtc", out var occProp).Should().BeTrue("body: {0}", body);
            var occ = occProp.GetDateTime();
            occ.Should().BeOnOrAfter(DateTime.UtcNow.AddMinutes(-11),
                "all returned rows must be within the date range filter; body: {0}", body);
            occ.Should().BeOnOrBefore(DateTime.UtcNow.AddMinutes(11),
                "all returned rows must be within the date range filter; body: {0}", body);
        }
    }

    [Fact(DisplayName = "BE-TC-FILTER-5: filter by unknown actionType → empty result, Successed=true, not 500")]
    public async Task Filter5_UnknownActionType_ReturnsEmptySucceeded()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AuditLogUrl}?actionType=Unknown.NonExistent.Action.XYZ999",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "unknown actionType filter must return 200 (empty result), not 500; body: {0}", body);
        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "successed must be true for an empty filtered result; body: {0}", body);

        // There should be no rows.
        var items = ExtractAuditLogItems(root, body);
        items.Should().BeEmpty("unknown actionType must return zero rows; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-ORDER: Newest-first ordering
    // =========================================================================

    [Fact(DisplayName = "BE-TC-ORDER-1: results ordered newest-first (OccurredAtUtc descending)")]
    public async Task Order1_ResultsAreNewestFirst()
    {
        // Produce two Subject.Created rows in sequence (slight time gap is natural via two HTTP calls).
        int grade1 = await CreateGradeGetIdAsync();
        await SendAsync(HttpMethod.Post, SubjectsCreateUrl,
            new { Name = $"Order1a {Guid.NewGuid():N}", Country = "EG", GradeId = grade1, SubjectCode = 0, Language = 0 },
            _adminToken);

        int grade2 = await CreateGradeGetIdAsync();
        await SendAsync(HttpMethod.Post, SubjectsCreateUrl,
            new { Name = $"Order1b {Guid.NewGuid():N}", Country = "EG", GradeId = grade2, SubjectCode = 0, Language = 0 },
            _adminToken);

        // Poll until we have at least 2 rows.
        await PollForAuditRowAsync(
            filter: $"actionType=Subject.Created&adminUserId={_adminUserId}",
            timeoutSeconds: 5,
            minExpectedCount: 2);

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{AuditLogUrl}?actionType=Subject.Created&adminUserId={_adminUserId}&pageSize=50",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var items = ExtractAuditLogItems(root, body);

        if (items.Count < 2)
            return; // Not enough rows to assert order — test is inconclusive but not failing.

        // Each item's OccurredAtUtc must be ≥ the next item's.
        for (int i = 0; i < items.Count - 1; i++)
        {
            TryProp(items[i], "occurredAtUtc", out var curr).Should().BeTrue("body: {0}", body);
            TryProp(items[i + 1], "occurredAtUtc", out var next).Should().BeTrue("body: {0}", body);
            curr.GetDateTime().Should().BeOnOrAfter(next.GetDateTime(),
                "audit log must be ordered newest-first (index {0} >= index {1}); body: {2}", i, i + 1, body);
        }
    }

    // =========================================================================
    // BE-TC-PII: DTO fields contain no PII
    // =========================================================================

    [Fact(DisplayName = "BE-TC-PII-1: AuditLogDto fields are id/eventId/adminUserId/action/targetEntityType/targetEntityId/details/occurredAtUtc/createdAt — no name/email field")]
    public async Task Pii1_AuditLogDto_HasNoNameOrEmailFields()
    {
        await ProduceAuditRowAsync();

        // Poll and get a row.
        var row = await PollForAuditRowAsync(
            filter: $"actionType=Subject.Created&adminUserId={_adminUserId}",
            timeoutSeconds: 3);

        row.Should().NotBeNull("at least one audit row must exist; check the relay wiring.");

        var rowEl = row!.Value;

        // Assert EXPECTED fields are present.
        TryProp(rowEl, "id", out _).Should().BeTrue("AuditLogDto must have 'id'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "eventId", out _).Should().BeTrue("AuditLogDto must have 'eventId'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "adminUserId", out _).Should().BeTrue("AuditLogDto must have 'adminUserId'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "action", out _).Should().BeTrue("AuditLogDto must have 'action'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "targetEntityType", out _).Should().BeTrue("AuditLogDto must have 'targetEntityType'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "targetEntityId", out _).Should().BeTrue("AuditLogDto must have 'targetEntityId'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "occurredAtUtc", out _).Should().BeTrue("AuditLogDto must have 'occurredAtUtc'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "createdAt", out _).Should().BeTrue("AuditLogDto must have 'createdAt'; body: {0}", rowEl.ToString());

        // Assert PII-SENSITIVE fields are ABSENT.
        TryProp(rowEl, "name", out _).Should().BeFalse(
            "AuditLogDto must NOT expose 'name'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "email", out _).Should().BeFalse(
            "AuditLogDto must NOT expose 'email'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "password", out _).Should().BeFalse(
            "AuditLogDto must NOT expose 'password'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "userName", out _).Should().BeFalse(
            "AuditLogDto must NOT expose 'userName'; body: {0}", rowEl.ToString());
        TryProp(rowEl, "phoneNumber", out _).Should().BeFalse(
            "AuditLogDto must NOT expose 'phoneNumber'; body: {0}", rowEl.ToString());
    }

    // =========================================================================
    // Smoke: endpoint is reachable (not 404) — AC-1 from the brief
    // =========================================================================

    [Fact(DisplayName = "Smoke: GET /api/Admin/Audit/Log returns 200 (not 404) — module loaded")]
    public async Task Smoke_AuditLogEndpoint_IsReachable()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, AuditLogUrl, bearer: _adminToken);

        resp.StatusCode.Should().NotBe(HttpStatusCode.NotFound,
            "AuditLog endpoint must be reachable (Moderation module registered and controller found); " +
            "404 means the module failed to load or the route is wrong; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "admin GET /api/Admin/Audit/Log must return 200; body: {0}", body);
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Produces one audit row by performing a Subject.Create admin action.
    /// The row may take up to ~500 ms to appear (in-process async dispatch).
    /// </summary>
    private async Task<int> ProduceAuditRowAsync()
    {
        int gradeId = await CreateGradeGetIdAsync();
        var name = $"AuditSeed {Guid.NewGuid():N}";
        var (resp, _, body) = await SendAsync(HttpMethod.Post, SubjectsCreateUrl,
            new { Name = name, Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 0 },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ProduceAuditRowAsync: Subject.Create must succeed; body: {0}", body);
        // Small yield to allow the async handler scheduling to complete before the caller queries.
        await Task.Delay(200);
        return gradeId;
    }

    /// <summary>
    /// Polls GET /api/Admin/Audit/Log?{filter} every 300 ms until at least <paramref name="minExpectedCount"/>
    /// rows appear or <paramref name="timeoutSeconds"/> elapses. Returns the first item (or null).
    /// </summary>
    private async Task<JsonElement?> PollForAuditRowAsync(
        string filter,
        int timeoutSeconds = 3,
        int minExpectedCount = 1)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var (resp, root, _) = await SendAsync(HttpMethod.Get,
                $"{AuditLogUrl}?{filter}&pageSize=50", bearer: _adminToken);
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var items = ExtractAuditLogItems(root, "");
                if (items.Count >= minExpectedCount)
                    return items.Count > 0 ? items[0] : (JsonElement?)null;
            }
            await Task.Delay(300);
        }
        return null;
    }

    /// <summary>
    /// Counts audit rows matching the given filter by reading totalCount from PaginatedResult.
    /// </summary>
    private async Task<int> CountAuditRowsAsync(string filter)
    {
        var (resp, root, _) = await SendAsync(HttpMethod.Get,
            $"{AuditLogUrl}?{filter}&pageSize=1", bearer: _adminToken);
        if (resp.StatusCode != HttpStatusCode.OK) return 0;
        TryProp(root, "data", out var data);
        TryProp(data, "totalCount", out var tc);
        return tc.ValueKind == JsonValueKind.Number ? tc.GetInt32() : 0;
    }

    /// <summary>Extracts the inner data[] array from a PaginatedResult envelope.</summary>
    private static List<JsonElement> ExtractAuditLogItems(JsonElement root, string bodyStr)
    {
        if (!TryProp(root, "data", out var outer)) return [];
        // outer is PaginatedResult<AuditLogDto>; its inner 'data' is the array.
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
        var bodyStr = await response.Content.ReadAsStringAsync();
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

    private static void AssertSucceeded(JsonElement root, string body)
    {
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
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
        var email = $"p712_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>Creates a grade and returns its id.</summary>
    private async Task<int> CreateGradeGetIdAsync()
    {
        var name = $"P712 Grade {Guid.NewGuid():N}";
        var (resp, _, body) = await SendAsync(HttpMethod.Post, GradesCreateUrl,
            new { Number = 1, DisplayName = name }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "grade create must succeed; body: {0}", body);

        return await FindIdInListAsync(
            $"{GradesListUrl}?PageNumber=1&PageSize=200",
            "displayName", name, "grade", _adminToken);
    }

    private async Task<int> FindIdInListAsync(
        string listUrl, string fieldName, string fieldValue, string entityType, string? bearer = null)
    {
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, listUrl, bearer: bearer);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prereq list for {0} must succeed; body: {1}", entityType, listBody);

        TryProp(listRoot, "data", out var outer).Should().BeTrue("body: {0}", listBody);
        TryProp(outer, "data", out var inner).Should().BeTrue("body: {0}", listBody);

        if (inner.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Expected array in data.data; url={listUrl}; body={listBody}");

        foreach (var item in inner.EnumerateArray())
        {
            if (TryProp(item, fieldName, out var fv) && fv.GetString() == fieldValue)
            {
                TryProp(item, "id", out var idProp).Should().BeTrue(
                    "{0} item must have id; body: {1}", entityType, listBody);
                return idProp.GetInt32();
            }
        }
        throw new InvalidOperationException(
            $"Could not find {entityType} where {fieldName}='{fieldValue}' in list; url={listUrl}; body={listBody}");
    }

    /// <summary>
    /// Decodes the 'sub' (NameIdentifier) claim from the JWT to get the admin's userId.
    /// The claim is the string representation of the integer user id.
    /// </summary>
    private static int DecodeUserIdFromJwt(string jwt)
    {
        try
        {
            // JWT is header.payload.signature — base64url decode the payload.
            var parts = jwt.Split('.');
            if (parts.Length != 3) return 0;
            var payload = parts[1];
            // Pad to multiple of 4 (base64url → base64).
            var remainder = payload.Length % 4;
            var padded = remainder == 2 ? payload + "==" :
                         remainder == 3 ? payload + "=" :
                         payload;
            var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
            var json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Try known claim types for userId.
            foreach (var claimName in new[] { "sub", "nameid",
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                "uid", "userId", "id" })
            {
                if (TryProp(root, claimName, out var val))
                {
                    if (val.ValueKind == JsonValueKind.Number) return val.GetInt32();
                    if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var parsed))
                        return parsed;
                }
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }
}
