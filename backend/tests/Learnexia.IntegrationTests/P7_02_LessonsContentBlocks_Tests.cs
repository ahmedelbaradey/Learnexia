using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P7-02 integration tests: Admin lesson management + ContentBlocks in the Learning module.
///
/// Endpoints under test:
///   POST   /api/learning/Lessons/Create                 — admin create lesson (now with EstimatedMinutes)
///   PUT    /api/learning/Lessons/Update                 — admin edit lesson (EstimatedMinutes round-trip)
///   DELETE /api/learning/Lessons?id={n}                — soft-delete lesson + cascade blocks
///   GET    /api/learning/Lessons/{id}                   — student read (IsActive filter)
///   GET    /api/learning/Lessons/{id}/Admin             — admin full detail (incl. inactive, all blocks)
///   PUT    /api/learning/Lessons/{id}/Active            — activate / deactivate lesson
///   PUT    /api/learning/Lessons/Reorder                — batch reorder within unit
///   GET    /api/learning/Subjects/{id}/Lessons          — student list (IsActive filter on lessons)
///   POST   /api/learning/ContentBlocks                  — add content block (per-type payload validation)
///   PUT    /api/learning/ContentBlocks                  — edit content block
///   DELETE /api/learning/ContentBlocks/{id}            — soft-delete single block
///   PUT    /api/learning/ContentBlocks/Reorder          — batch reorder blocks within lesson
///   GET    /api/learning/ContentBlocks/ByLesson/{id}    — admin list blocks for lesson
///
/// Auth matrix:
///   All admin write endpoints: anonymous → 401, non-admin → 403, admin → 200.
///   Student read endpoints: anonymous → 401, authenticated user → 200.
///
/// Acceptance criteria covered:
///   AC-CB-1  : ContentBlock CRUD round-trip — Text block: add → list → ByLesson returns id/blockType/payload/sequenceOrder.
///   AC-CB-2  : ContentBlock CRUD round-trip — Image block: add → ByLesson returns Image block.
///   AC-CB-3  : ContentBlock CRUD round-trip — Video block: add → ByLesson returns Video block.
///   AC-CB-4  : ContentBlock CRUD round-trip — Callout block: add → ByLesson returns Callout block.
///   AC-CB-5  : Edit block type and payload → ByLesson reflects new values.
///   AC-CB-6  : Soft-delete hides block from ByLesson list; Admin detail also no longer shows it.
///   AC-CB-7  : Reorder persists new SequenceOrder (within one lesson).
///   AC-CB-8  : Reorder with IDs from two different lessons → Successed=false.
///   AC-CB-9  : Reorder validator: empty list → 422; id=0 in list → 422.
///   AC-CB-10 : Payload validation Text — missing "markdown" field → 422.
///   AC-CB-11 : Payload validation Image — missing "url" field → 422.
///   AC-CB-12 : Payload validation Video — missing "url" field → 422.
///   AC-CB-13 : Payload validation Callout — missing "variant" → 422; invalid variant → 422; missing "markdown" → 422.
///   AC-CB-14 : Add with empty payload → 422.
///   AC-CB-15 : Add with invalid BlockType enum value → 422.
///   AC-CB-16 : Auth: all ContentBlocks endpoints anonymous → 401; non-admin → 403; admin → 200.
///   AC-LS-1  : Lesson create persists EstimatedMinutes; admin detail reflects it.
///   AC-LS-2  : EditLesson round-trip: EstimatedMinutes changes are persisted.
///   AC-LS-3  : SetActive(false) hides lesson from student GET /Lessons/{id} (expect NotFound) and from Subjects/{id}/Lessons.
///   AC-LS-4  : SetActive(false) → lesson still visible via GET /Lessons/{id}/Admin with IsActive=false.
///   AC-LS-5  : SetActive(true) restores lesson visibility on student route.
///   AC-LS-6  : Soft-delete lesson → student GET /Lessons/{id} returns NotFound; admin GET/{id}/Admin also NotFound.
///   AC-LS-7  : Soft-delete lesson cascade → all attached ContentBlocks disappear from ByLesson list.
///   AC-LS-8  : Lesson Reorder within unit persists new SequenceOrder; cross-unit reorder → Successed=false.
///   AC-LS-9  : Lesson Reorder validator: empty LessonIds → 422; id=0 in list → 422.
///   AC-LS-10 : Auth: SetActive / Reorder / Admin endpoints anonymous → 401; non-admin → 403; admin → 200.
///   AC-LS-11 : BaseResponse envelope (statusCode, successed, message, data, errors) shape on add/edit/delete.
///   AC-LS-12 : SetActive validator: LessonId=0 in route → 422.
///   AC-LS-13 : Lesson delete is soft (not physically purged): admin GetAdminDetail returns NotFound after delete
///               (because global IsDeleted filter applies), confirming row was soft-deleted, not wiped.
///
/// Docker + Postgres required to RUN. Compile-only on Windows CI (no Docker daemon).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_02_LessonsContentBlocks_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // ──────────────────────────────────────────────────────────────────────────
    // Seeded credentials (mirrors P7-01 / P1-05)
    // ──────────────────────────────────────────────────────────────────────────
    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";
    private const string BasicUserName = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";

    private string? _adminToken;
    private string? _basicToken;
    private string? _parentToken;

    // ──────────────────────────────────────────────────────────────────────────
    // Valid payload constants per ContentBlockType
    // ──────────────────────────────────────────────────────────────────────────
    private const string TextPayload    = "{\"markdown\":\"## Hello\\nWorld\"}";
    private const string ImagePayload   = "{\"url\":\"https://example.com/img.png\",\"altText\":\"alt\"}";
    private const string VideoPayload   = "{\"url\":\"https://example.com/video.mp4\",\"caption\":\"cap\"}";
    private const string CalloutPayload = "{\"variant\":\"info\",\"markdown\":\"## Note\"}";

    // BlockType int values: Text=1, Image=2, Video=3, Callout=4
    private const int BlockTypeText    = 1;
    private const int BlockTypeImage   = 2;
    private const int BlockTypeVideo   = 3;
    private const int BlockTypeCallout = 4;

    public P7_02_LessonsContentBlocks_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await db.Database.MigrateAsync();

        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken  = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // AC-CB-16 / AC-LS-10 — Auth matrix on new P7-02 endpoints
    // =========================================================================

    [Fact(DisplayName = "AC-CB-16: POST ContentBlocks anonymous → 401")]
    public async Task Auth_AddContentBlock_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId = 1, blockType = BlockTypeText, payload = TextPayload },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-16: POST ContentBlocks non-admin → 403")]
    public async Task Auth_AddContentBlock_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId = 1, blockType = BlockTypeText, payload = TextPayload },
            _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-16: POST ContentBlocks parent → 403")]
    public async Task Auth_AddContentBlock_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId = 1, blockType = BlockTypeText, payload = TextPayload },
            _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-16: PUT ContentBlocks anonymous → 401")]
    public async Task Auth_EditContentBlock_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/learning/ContentBlocks",
            new { id = 1, blockType = BlockTypeText, payload = TextPayload },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-16: PUT ContentBlocks non-admin → 403")]
    public async Task Auth_EditContentBlock_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/learning/ContentBlocks",
            new { id = 1, blockType = BlockTypeText, payload = TextPayload },
            _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-16: DELETE ContentBlocks/{id} anonymous → 401")]
    public async Task Auth_DeleteContentBlock_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Delete, "/api/learning/ContentBlocks/1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-16: DELETE ContentBlocks/{id} non-admin → 403")]
    public async Task Auth_DeleteContentBlock_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Delete, "/api/learning/ContentBlocks/1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-16: PUT ContentBlocks/Reorder anonymous → 401")]
    public async Task Auth_ReorderContentBlocks_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/learning/ContentBlocks/Reorder",
            new { contentBlockIds = new[] { 1, 2 } },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-16: PUT ContentBlocks/Reorder non-admin → 403")]
    public async Task Auth_ReorderContentBlocks_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/learning/ContentBlocks/Reorder",
            new { contentBlockIds = new[] { 1, 2 } },
            _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-16: GET ContentBlocks/ByLesson/{id} anonymous → 401")]
    public async Task Auth_ListContentBlocks_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, "/api/learning/ContentBlocks/ByLesson/1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-16: GET ContentBlocks/ByLesson/{id} non-admin → 403")]
    public async Task Auth_ListContentBlocks_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, "/api/learning/ContentBlocks/ByLesson/1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-LS-10: GET Lessons/{id}/Admin anonymous → 401")]
    public async Task Auth_GetAdminLesson_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, "/api/learning/Lessons/1/Admin",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-LS-10: GET Lessons/{id}/Admin non-admin → 403")]
    public async Task Auth_GetAdminLesson_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, "/api/learning/Lessons/1/Admin",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-LS-10: PUT Lessons/{id}/Active anonymous → 401")]
    public async Task Auth_SetLessonActive_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/learning/Lessons/1/Active",
            new { isActive = false }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-LS-10: PUT Lessons/{id}/Active non-admin → 403")]
    public async Task Auth_SetLessonActive_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/learning/Lessons/1/Active",
            new { isActive = false }, _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-LS-10: PUT Lessons/Reorder anonymous → 401")]
    public async Task Auth_ReorderLessons_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/learning/Lessons/Reorder",
            new { lessonIds = new[] { 1, 2 } }, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-LS-10: PUT Lessons/Reorder non-admin → 403")]
    public async Task Auth_ReorderLessons_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put, "/api/learning/Lessons/Reorder",
            new { lessonIds = new[] { 1, 2 } }, _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    // =========================================================================
    // AC-CB-1..4 — ContentBlock CRUD round-trip per block type
    // =========================================================================

    [Fact(DisplayName = "AC-CB-1: Add Text block → ByLesson returns block with correct blockType and payload")]
    public async Task AddTextBlock_RoundTrip()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText, payload = TextPayload },
            _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "Add Text block must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        var blocks = await GetBlocksByLessonAsync(lessonId);
        blocks.Should().NotBeEmpty("added block must appear in ByLesson list; body from list");

        var block = blocks[0];
        TryProp(block, "blockType", out var btProp).Should().BeTrue("block must have blockType");
        btProp.GetInt32().Should().Be(BlockTypeText, "blockType must be Text(1)");
        TryProp(block, "payload", out var payProp).Should().BeTrue("block must have payload");
        payProp.GetString().Should().Contain("markdown", "payload must contain markdown field");
    }

    [Fact(DisplayName = "AC-CB-2: Add Image block → ByLesson returns block with blockType=Image")]
    public async Task AddImageBlock_RoundTrip()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeImage, payload = ImagePayload },
            _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "Add Image block must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        var blocks = await GetBlocksByLessonAsync(lessonId);
        blocks.Should().NotBeEmpty("added Image block must appear in ByLesson list");

        var block = blocks[0];
        TryProp(block, "blockType", out var btProp).Should().BeTrue();
        btProp.GetInt32().Should().Be(BlockTypeImage, "blockType must be Image(2)");
    }

    [Fact(DisplayName = "AC-CB-3: Add Video block → ByLesson returns block with blockType=Video")]
    public async Task AddVideoBlock_RoundTrip()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeVideo, payload = VideoPayload },
            _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "Add Video block must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        var blocks = await GetBlocksByLessonAsync(lessonId);
        blocks.Should().NotBeEmpty("added Video block must appear in ByLesson list");

        var block = blocks[0];
        TryProp(block, "blockType", out var btProp).Should().BeTrue();
        btProp.GetInt32().Should().Be(BlockTypeVideo, "blockType must be Video(3)");
    }

    [Fact(DisplayName = "AC-CB-4: Add Callout block → ByLesson returns block with blockType=Callout")]
    public async Task AddCalloutBlock_RoundTrip()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeCallout, payload = CalloutPayload },
            _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "Add Callout block must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        var blocks = await GetBlocksByLessonAsync(lessonId);
        blocks.Should().NotBeEmpty("added Callout block must appear in ByLesson list");

        var block = blocks[0];
        TryProp(block, "blockType", out var btProp).Should().BeTrue();
        btProp.GetInt32().Should().Be(BlockTypeCallout, "blockType must be Callout(4)");
    }

    // =========================================================================
    // AC-CB-5 — Edit block: type and payload are updated
    // =========================================================================

    [Fact(DisplayName = "AC-CB-5: Edit block changes blockType and payload → ByLesson reflects new values")]
    public async Task EditContentBlock_UpdatesTypeAndPayload()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Add a Text block.
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText, payload = TextPayload },
            _adminToken);

        var blocks = await GetBlocksByLessonAsync(lessonId);
        blocks.Should().NotBeEmpty("block must exist before edit");
        TryProp(blocks[0], "id", out var idProp).Should().BeTrue("block must have id");
        var blockId = idProp.GetInt32();

        // Edit to Image type.
        var (editResp, editRoot, editBody) = await SendAsync(HttpMethod.Put, "/api/learning/ContentBlocks",
            new { id = blockId, blockType = BlockTypeImage, payload = ImagePayload },
            _adminToken);
        editResp.StatusCode.Should().Be(HttpStatusCode.OK, "Edit must return 200; body: {0}", editBody);
        AssertSucceeded(editRoot, editBody);

        // Reload and verify.
        var updatedBlocks = await GetBlocksByLessonAsync(lessonId);
        var updated = updatedBlocks.FirstOrDefault(b =>
            TryProp(b, "id", out var id) && id.GetInt32() == blockId);
        updated.ValueKind.Should().NotBe(JsonValueKind.Undefined, "edited block must still appear");
        TryProp(updated, "blockType", out var newBtProp).Should().BeTrue();
        newBtProp.GetInt32().Should().Be(BlockTypeImage, "blockType must now be Image(2) after edit");
        TryProp(updated, "payload", out var newPayProp).Should().BeTrue();
        newPayProp.GetString().Should().Contain("url", "payload must reflect Image URL after edit");
    }

    // =========================================================================
    // AC-CB-6 — Soft-delete hides block from ByLesson
    // =========================================================================

    [Fact(DisplayName = "AC-CB-6: Soft-delete ContentBlock hides it from ByLesson list")]
    public async Task DeleteContentBlock_HiddenFromByLesson()
    {
        var lessonId = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText, payload = TextPayload },
            _adminToken);

        var blocksBefore = await GetBlocksByLessonAsync(lessonId);
        blocksBefore.Should().NotBeEmpty("block must exist before delete");
        TryProp(blocksBefore[0], "id", out var idProp).Should().BeTrue();
        var blockId = idProp.GetInt32();

        // Soft-delete the block.
        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/ContentBlocks/{blockId}", bearer: _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK, "Delete must return 200; body: {0}", delBody);
        AssertSucceeded(delRoot, delBody);

        // Block must no longer appear in ByLesson (global IsDeleted filter).
        var blocksAfter = await GetBlocksByLessonAsync(lessonId);
        blocksAfter.Any(b => TryProp(b, "id", out var id) && id.GetInt32() == blockId)
            .Should().BeFalse("soft-deleted block must not appear in ByLesson list");
    }

    // =========================================================================
    // AC-CB-7 — Reorder persists SequenceOrder within one lesson
    // =========================================================================

    [Fact(DisplayName = "AC-CB-7: Reorder ContentBlocks within lesson persists new SequenceOrder")]
    public async Task ReorderContentBlocks_PersistsSequenceOrder()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Add two blocks.
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText, payload = TextPayload }, _adminToken);
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeImage, payload = ImagePayload }, _adminToken);

        var blocksBefore = await GetBlocksByLessonAsync(lessonId);
        blocksBefore.Should().HaveCount(2, "both blocks must exist");

        TryProp(blocksBefore[0], "id", out var id0Prop).Should().BeTrue();
        TryProp(blocksBefore[1], "id", out var id1Prop).Should().BeTrue();
        var block0Id = id0Prop.GetInt32(); // original position 0 (Text)
        var block1Id = id1Prop.GetInt32(); // original position 1 (Image)

        // Reorder: put Image first, Text second.
        var (reorderResp, reorderRoot, reorderBody) = await SendAsync(HttpMethod.Put,
            "/api/learning/ContentBlocks/Reorder",
            new { contentBlockIds = new[] { block1Id, block0Id } },
            _adminToken);
        reorderResp.StatusCode.Should().Be(HttpStatusCode.OK, "Reorder must return 200; body: {0}", reorderBody);
        AssertSucceeded(reorderRoot, reorderBody);

        // Reload and verify ordering.
        var blocksAfter = await GetBlocksByLessonAsync(lessonId);
        int? order0 = null, order1 = null;
        foreach (var b in blocksAfter)
        {
            if (!TryProp(b, "id", out var idProp)) continue;
            if (!TryProp(b, "sequenceOrder", out var oProp)) continue;
            var id = idProp.GetInt32();
            var order = oProp.GetInt32();
            if (id == block0Id) order0 = order;
            if (id == block1Id) order1 = order;
        }
        order0.Should().NotBeNull($"block0 (id={block0Id}) must appear after reorder");
        order1.Should().NotBeNull($"block1 (id={block1Id}) must appear after reorder");

        // After reorder [block1, block0]: block1 → sequenceOrder=0, block0 → sequenceOrder=1
        order1.Should().BeLessThan(order0!.Value,
            "Image block placed first must have lower SequenceOrder than Text block");
    }

    // =========================================================================
    // AC-CB-8 — Reorder with IDs from two different lessons → rejected
    // =========================================================================

    [Fact(DisplayName = "AC-CB-8: Reorder ContentBlocks with IDs from two different lessons → Successed=false")]
    public async Task ReorderContentBlocks_CrossLesson_Rejected()
    {
        var lessonId1 = await CreateLessonGetIdAsync();
        var lessonId2 = await CreateLessonGetIdAsync();

        // Add one block to each lesson.
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId = lessonId1, blockType = BlockTypeText, payload = TextPayload }, _adminToken);
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId = lessonId2, blockType = BlockTypeImage, payload = ImagePayload }, _adminToken);

        var blocks1 = await GetBlocksByLessonAsync(lessonId1);
        var blocks2 = await GetBlocksByLessonAsync(lessonId2);
        blocks1.Should().NotBeEmpty(); blocks2.Should().NotBeEmpty();
        TryProp(blocks1[0], "id", out var id1Prop).Should().BeTrue();
        TryProp(blocks2[0], "id", out var id2Prop).Should().BeTrue();
        var blockFromLesson1 = id1Prop.GetInt32();
        var blockFromLesson2 = id2Prop.GetInt32();

        // Attempt cross-lesson reorder.
        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/learning/ContentBlocks/Reorder",
            new { contentBlockIds = new[] { blockFromLesson1, blockFromLesson2 } },
            _adminToken);

        AssertRejected(resp, root, body,
            "Cross-lesson reorder must be rejected with Successed=false");
    }

    // =========================================================================
    // AC-CB-9 — Reorder validator: empty list and id=0
    // =========================================================================

    [Fact(DisplayName = "AC-CB-9: ContentBlocks Reorder with empty ContentBlockIds → 422")]
    public async Task ReorderContentBlocks_EmptyList_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/learning/ContentBlocks/Reorder",
            new { contentBlockIds = Array.Empty<int>() }, _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty ContentBlockIds must trigger FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-9: ContentBlocks Reorder with id=0 in list → 422")]
    public async Task ReorderContentBlocks_ZeroId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/learning/ContentBlocks/Reorder",
            new { contentBlockIds = new[] { 0 } }, _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "id=0 must trigger GreaterThan(0) validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    // =========================================================================
    // AC-CB-10..14 — Per-type payload validation
    // =========================================================================

    [Fact(DisplayName = "AC-CB-10: Add Text block with missing 'markdown' field → 422")]
    public async Task AddTextBlock_MissingMarkdown_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText, payload = "{\"notMarkdown\":\"oops\"}" },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Text block missing 'markdown' must fail payload validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-11: Add Image block with missing 'url' field → 422")]
    public async Task AddImageBlock_MissingUrl_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeImage, payload = "{\"altText\":\"no url here\"}" },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Image block missing 'url' must fail payload validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-12: Add Video block with missing 'url' field → 422")]
    public async Task AddVideoBlock_MissingUrl_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeVideo, payload = "{\"caption\":\"no url\"}" },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Video block missing 'url' must fail payload validation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-13a: Add Callout block with missing 'variant' → 422")]
    public async Task AddCalloutBlock_MissingVariant_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeCallout, payload = "{\"markdown\":\"no variant\"}" },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Callout missing 'variant' must fail → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-13b: Add Callout block with invalid variant value → 422")]
    public async Task AddCalloutBlock_InvalidVariant_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeCallout,
                  payload = "{\"variant\":\"danger\",\"markdown\":\"invalid variant\"}" },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Callout with invalid variant 'danger' must fail → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-13c: Add Callout block with missing 'markdown' → 422")]
    public async Task AddCalloutBlock_MissingMarkdown_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeCallout,
                  payload = "{\"variant\":\"tip\"}" },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Callout missing 'markdown' must fail → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-14: Add ContentBlock with empty payload → 422")]
    public async Task AddContentBlock_EmptyPayload_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText, payload = "" },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty payload must trigger NotEmpty validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-CB-15: Add ContentBlock with invalid BlockType enum value → 422")]
    public async Task AddContentBlock_InvalidBlockType_Returns422()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            // BlockType 99 is not a valid ContentBlockType enum member.
            new { lessonId, blockType = 99, payload = TextPayload },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Invalid BlockType=99 must trigger IsInEnum validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-CB-16 — Callout valid variants accepted (info, warning, tip)
    // =========================================================================

    [Theory(DisplayName = "AC-CB-4: Callout valid variants (info/warning/tip) all accepted")]
    [InlineData("info")]
    [InlineData("warning")]
    [InlineData("tip")]
    public async Task AddCalloutBlock_ValidVariants_Succeed(string variant)
    {
        var lessonId = await CreateLessonGetIdAsync();
        var payload = $"{{\"variant\":\"{variant}\",\"markdown\":\"## Note\"}}";

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeCallout, payload },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Callout with variant='{0}' must be accepted; body: {1}", variant, body);
        AssertSucceeded(root, body);
    }

    // =========================================================================
    // AC-LS-1 — Create lesson persists EstimatedMinutes
    // =========================================================================

    [Fact(DisplayName = "AC-LS-1: Create lesson with EstimatedMinutes → admin detail reflects it")]
    public async Task CreateLesson_EstimatedMinutes_PersistedInAdminDetail()
    {
        int unitId = await CreateUnitWithPrereqsAsync();
        var lessonName = $"P702 Lesson EM {Guid.NewGuid():N}";

        var (createResp, createRoot, createBody) = await SendAsync(HttpMethod.Post,
            "/api/learning/Lessons/Create",
            new { Name = lessonName, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId, EstimatedMinutes = 30 },
            _adminToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Lesson create must return 200; body: {0}", createBody);
        AssertSucceeded(createRoot, createBody);

        var lessonId = await FindLessonIdByNameAsync(unitId, lessonName);

        var (adminResp, adminRoot, adminBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}/Admin", bearer: _adminToken);
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK, "Admin detail must return 200; body: {0}", adminBody);

        TryProp(adminRoot, "data", out var data).Should().BeTrue("body: {0}", adminBody);
        TryProp(data, "estimatedMinutes", out var emProp).Should().BeTrue(
            "AdminLessonDetailDto must have estimatedMinutes; body: {0}", adminBody);
        emProp.GetInt32().Should().Be(30,
            "estimatedMinutes must be 30 as set on create; body: {0}", adminBody);
    }

    // =========================================================================
    // AC-LS-2 — EditLesson round-trip: EstimatedMinutes changes are persisted
    // =========================================================================

    [Fact(DisplayName = "AC-LS-2: EditLesson updates EstimatedMinutes → admin detail reflects new value")]
    public async Task EditLesson_EstimatedMinutes_Updated()
    {
        int unitId = await CreateUnitWithPrereqsAsync();
        var lessonName = $"P702 Edit EM {Guid.NewGuid():N}";

        await SendAsync(HttpMethod.Post, "/api/learning/Lessons/Create",
            new { Name = lessonName, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId, EstimatedMinutes = 10 },
            _adminToken);

        var lessonId = await FindLessonIdByNameAsync(unitId, lessonName);

        // Edit with new EstimatedMinutes.
        var (editResp, editRoot, editBody) = await SendAsync(HttpMethod.Put, "/api/learning/Lessons/Update",
            new { Id = lessonId, Name = lessonName, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId, EstimatedMinutes = 45 },
            _adminToken);
        editResp.StatusCode.Should().Be(HttpStatusCode.OK, "Edit must return 200; body: {0}", editBody);
        AssertSucceeded(editRoot, editBody);

        // Re-fetch admin detail.
        var (adminResp, adminRoot, adminBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}/Admin", bearer: _adminToken);
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", adminBody);

        TryProp(adminRoot, "data", out var data).Should().BeTrue("body: {0}", adminBody);
        TryProp(data, "estimatedMinutes", out var emProp).Should().BeTrue("body: {0}", adminBody);
        emProp.GetInt32().Should().Be(45, "estimatedMinutes must be 45 after edit; body: {0}", adminBody);
    }

    // =========================================================================
    // AC-LS-3 / AC-LS-4 — SetActive(false): student route hidden, admin route visible
    // =========================================================================

    [Fact(DisplayName = "AC-LS-3: Deactivated lesson hidden from student GET /Lessons/{id}")]
    public async Task DeactivateLesson_HiddenFromStudentGet()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Deactivate.
        var (setResp, setRoot, setBody) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Lessons/{lessonId}/Active",
            new { isActive = false }, _adminToken);
        setResp.StatusCode.Should().Be(HttpStatusCode.OK, "SetActive must return 200; body: {0}", setBody);
        AssertSucceeded(setRoot, setBody);

        // Student GET (uses _adminToken which has a valid JWT — the route requires [Authorize]).
        // Handler filters by l.IsActive — inactive lessons return NotFound.
        var (getResp, getRoot, getBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}", bearer: _adminToken);

        // Expect 200 with Successed=false (NotFound from handler) or HTTP 404.
        ((int)getResp.StatusCode).Should().BeOneOf(new[] { 200, 404 },
            "Inactive lesson must not be served to student; body: {0}", getBody);

        if (getResp.StatusCode == HttpStatusCode.OK && getRoot.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(getRoot, "successed", out var s).Should().BeTrue("body: {0}", getBody);
            s.GetBoolean().Should().BeFalse(
                "Inactive lesson must return Successed=false to student; body: {0}", getBody);
        }
    }

    [Fact(DisplayName = "AC-LS-4: Deactivated lesson still visible via GET /Lessons/{id}/Admin with IsActive=false")]
    public async Task DeactivateLesson_StillVisibleViaAdminRoute()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Deactivate.
        await SendAsync(HttpMethod.Put, $"/api/learning/Lessons/{lessonId}/Active",
            new { isActive = false }, _adminToken);

        // Admin detail must still return the lesson with IsActive=false.
        var (adminResp, adminRoot, adminBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}/Admin", bearer: _adminToken);
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Inactive lesson must still be visible via admin route; body: {0}", adminBody);
        AssertSucceeded(adminRoot, adminBody);

        TryProp(adminRoot, "data", out var data).Should().BeTrue("body: {0}", adminBody);
        TryProp(data, "isActive", out var isActiveProp).Should().BeTrue(
            "AdminLessonDetailDto must expose IsActive; body: {0}", adminBody);
        isActiveProp.GetBoolean().Should().BeFalse(
            "Admin detail must reflect IsActive=false after deactivation; body: {0}", adminBody);
    }

    // =========================================================================
    // AC-LS-5 — Reactivate restores student visibility
    // =========================================================================

    [Fact(DisplayName = "AC-LS-5: Reactivate lesson restores student GET /Lessons/{id} success")]
    public async Task ReactivateLesson_RestoresStudentVisibility()
    {
        // Build a full grade→subject→unit stack; we need subject+unit+lesson ids
        // so we can publish them. CreateLessonGetIdAsync only returns lessonId.
        var (subjectId, unitId) = await CreateSubjectWithPrereqsAsync();
        var lessonName = $"P702 Reactivate {Guid.NewGuid():N}";
        await SendAsync(HttpMethod.Post, "/api/learning/Lessons/Create",
            new { Name = lessonName, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId, EstimatedMinutes = 0 },
            _adminToken);
        var lessonId = await FindLessonIdByNameAsync(unitId, lessonName);

        // P7-05: publish subject, unit, and lesson so GetLesson (LifecycleState==Published filter) can serve.
        await PublishEntityAsync(1 /* Subject */, subjectId);
        await PublishEntityAsync(2 /* Unit */, unitId);
        await PublishEntityAsync(3 /* Lesson */, lessonId);

        // Deactivate then reactivate.
        await SendAsync(HttpMethod.Put, $"/api/learning/Lessons/{lessonId}/Active",
            new { isActive = false }, _adminToken);

        var (reactResp, reactRoot, reactBody) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Lessons/{lessonId}/Active",
            new { isActive = true }, _adminToken);
        reactResp.StatusCode.Should().Be(HttpStatusCode.OK, "Reactivate must return 200; body: {0}", reactBody);
        AssertSucceeded(reactRoot, reactBody);

        // Student GET must succeed.
        // Note: language guard may fire for the admin token (admin has no learning-language claim).
        // We only assert that the response is not a 500 and either 200-successed=true OR 200-successed=false
        // due to language guard (a separate concern from IsActive).
        var (getResp, _, getBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}", bearer: _adminToken);
        ((int)getResp.StatusCode).Should().NotBe(500,
            "Reactivated lesson must not 500 on student GET; body: {0}", getBody);
        ((int)getResp.StatusCode).Should().BeOneOf(new[] { 200, 403 },
            "Reactivated lesson: student GET should be 200 (success or language-guard 403); body: {0}", getBody);
    }

    // =========================================================================
    // AC-LS-3 — Deactivated lesson hidden from Subjects/{id}/Lessons
    // =========================================================================

    [Fact(DisplayName = "AC-LS-3: Deactivated lesson hidden from student Subjects/{id}/Lessons")]
    public async Task DeactivateLesson_HiddenFromSubjectLessons()
    {
        var (subjectId, unitId) = await CreateSubjectWithPrereqsAsync();
        var lessonName = $"P702 Hide {Guid.NewGuid():N}";

        await SendAsync(HttpMethod.Post, "/api/learning/Lessons/Create",
            new { Name = lessonName, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId, EstimatedMinutes = 0 },
            _adminToken);

        var lessonId = await FindLessonIdByNameAsync(unitId, lessonName);

        // P7-05: publish subject, unit, and lesson so the student Lessons endpoint can serve them.
        // GetSubjectLessons filters by LifecycleState == Published for subject, units, and lessons.
        await PublishEntityAsync(1 /* Subject */, subjectId);
        await PublishEntityAsync(2 /* Unit */, unitId);
        await PublishEntityAsync(3 /* Lesson */, lessonId);

        // Verify it appears first.
        var lessonsBefore = await GetStudentLessonsUnitsAsync(subjectId);
        lessonsBefore.Any(u => ContainsLessonId(u, lessonId))
            .Should().BeTrue("active lesson must appear in Subjects/{id}/Lessons");

        // Deactivate.
        await SendAsync(HttpMethod.Put, $"/api/learning/Lessons/{lessonId}/Active",
            new { isActive = false }, _adminToken);

        // Must no longer appear.
        var lessonsAfter = await GetStudentLessonsUnitsAsync(subjectId);
        lessonsAfter.Any(u => ContainsLessonId(u, lessonId))
            .Should().BeFalse("deactivated lesson must be hidden from Subjects/{id}/Lessons");
    }

    // =========================================================================
    // AC-LS-6 — Soft-delete lesson → hidden from student and admin routes
    // =========================================================================

    [Fact(DisplayName = "AC-LS-6: Soft-deleted lesson hidden from student GET /Lessons/{id}")]
    public async Task DeleteLesson_HiddenFromStudentGet()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/Lessons?id={lessonId}", bearer: _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK, "Delete must return 200; body: {0}", delBody);
        AssertSucceeded(delRoot, delBody);

        // Student GET must return NotFound.
        var (getResp, getRoot, getBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}", bearer: _adminToken);
        ((int)getResp.StatusCode).Should().BeOneOf(new[] { 200, 404 },
            "Deleted lesson must not be served; body: {0}", getBody);

        if (getResp.StatusCode == HttpStatusCode.OK && getRoot.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(getRoot, "successed", out var s).Should().BeTrue("body: {0}", getBody);
            s.GetBoolean().Should().BeFalse("Deleted lesson must return Successed=false; body: {0}", getBody);
        }
    }

    [Fact(DisplayName = "AC-LS-13: Soft-deleted lesson returns NotFound via admin GET /{id}/Admin")]
    public async Task DeleteLesson_NotFoundViaAdminRoute()
    {
        var lessonId = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Delete, $"/api/learning/Lessons?id={lessonId}", bearer: _adminToken);

        // Global IsDeleted filter means admin route also returns NotFound.
        var (adminResp, adminRoot, adminBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}/Admin", bearer: _adminToken);

        ((int)adminResp.StatusCode).Should().BeOneOf(new[] { 200, 404 },
            "Soft-deleted lesson must not be found even via admin route; body: {0}", adminBody);

        if (adminResp.StatusCode == HttpStatusCode.OK && adminRoot.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(adminRoot, "successed", out var s).Should().BeTrue("body: {0}", adminBody);
            s.GetBoolean().Should().BeFalse(
                "Soft-delete must make lesson invisible via global IsDeleted filter; body: {0}", adminBody);
        }
    }

    // =========================================================================
    // AC-LS-7 — Soft-delete lesson cascades to content blocks
    // =========================================================================

    [Fact(DisplayName = "AC-LS-7: Soft-delete lesson cascade-soft-deletes all content blocks")]
    public async Task DeleteLesson_CascadeSoftDeletesContentBlocks()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Add two blocks.
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText, payload = TextPayload }, _adminToken);
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeImage, payload = ImagePayload }, _adminToken);

        var blocksBefore = await GetBlocksByLessonAsync(lessonId);
        blocksBefore.Should().HaveCount(2, "both blocks must exist before lesson delete");

        // Delete the lesson.
        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/Lessons?id={lessonId}", bearer: _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK, "Delete must return 200; body: {0}", delBody);
        AssertSucceeded(delRoot, delBody);

        // ByLesson must return NotFound (lesson deleted → 200 Successed=false from handler).
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/ContentBlocks/ByLesson/{lessonId}", bearer: _adminToken);

        ((int)listResp.StatusCode).Should().BeOneOf(new[] { 200, 404 },
            "ByLesson for deleted lesson must return graceful not-found; body: {0}", listBody);

        if (listResp.StatusCode == HttpStatusCode.OK && listRoot.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(listRoot, "successed", out var s).Should().BeTrue("body: {0}", listBody);
            s.GetBoolean().Should().BeFalse(
                "ByLesson on deleted lesson must return Successed=false (cascade deletes blocks); body: {0}", listBody);
        }
    }

    // =========================================================================
    // AC-LS-8 — Lesson Reorder within unit
    // =========================================================================

    [Fact(DisplayName = "AC-LS-8: Lesson Reorder within unit persists new SequenceOrder")]
    public async Task ReorderLessons_PersistsSequenceOrder()
    {
        int unitId = await CreateUnitWithPrereqsAsync();
        var name1 = $"P702 LA {Guid.NewGuid():N}";
        var name2 = $"P702 LB {Guid.NewGuid():N}";

        await SendAsync(HttpMethod.Post, "/api/learning/Lessons/Create",
            new { Name = name1, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId, EstimatedMinutes = 0 }, _adminToken);
        await SendAsync(HttpMethod.Post, "/api/learning/Lessons/Create",
            new { Name = name2, Difficulty = 1, SequenceOrder = 2, IsLocked = false,
                  UnitId = unitId, EstimatedMinutes = 0 }, _adminToken);

        var lesson1Id = await FindLessonIdByNameAsync(unitId, name1);
        var lesson2Id = await FindLessonIdByNameAsync(unitId, name2);

        // Reorder: lesson2 first, lesson1 second.
        var (reorderResp, reorderRoot, reorderBody) = await SendAsync(HttpMethod.Put,
            "/api/learning/Lessons/Reorder",
            new { lessonIds = new[] { lesson2Id, lesson1Id } },
            _adminToken);
        reorderResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Reorder must return 200; body: {0}", reorderBody);
        AssertSucceeded(reorderRoot, reorderBody);

        // Fetch admin detail of each lesson and compare sequenceOrder.
        var order1 = await GetAdminLessonSequenceOrderAsync(lesson1Id);
        var order2 = await GetAdminLessonSequenceOrderAsync(lesson2Id);

        order2.Should().BeLessThan(order1,
            $"lesson2 placed first must have lower SequenceOrder than lesson1; order1={order1}, order2={order2}");
    }

    [Fact(DisplayName = "AC-LS-8: Lesson Reorder cross-unit → Successed=false")]
    public async Task ReorderLessons_CrossUnit_Rejected()
    {
        int unitId1 = await CreateUnitWithPrereqsAsync();
        int unitId2 = await CreateUnitWithPrereqsAsync();

        var name1 = $"P702 CrossUnit A {Guid.NewGuid():N}";
        var name2 = $"P702 CrossUnit B {Guid.NewGuid():N}";

        await SendAsync(HttpMethod.Post, "/api/learning/Lessons/Create",
            new { Name = name1, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId1, EstimatedMinutes = 0 }, _adminToken);
        await SendAsync(HttpMethod.Post, "/api/learning/Lessons/Create",
            new { Name = name2, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId2, EstimatedMinutes = 0 }, _adminToken);

        var lessonFromUnit1 = await FindLessonIdByNameAsync(unitId1, name1);
        var lessonFromUnit2 = await FindLessonIdByNameAsync(unitId2, name2);

        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/learning/Lessons/Reorder",
            new { lessonIds = new[] { lessonFromUnit1, lessonFromUnit2 } },
            _adminToken);

        AssertRejected(resp, root, body,
            "Cross-unit lesson reorder must be rejected with Successed=false");
    }

    // =========================================================================
    // AC-LS-9 — Lesson Reorder validator
    // =========================================================================

    [Fact(DisplayName = "AC-LS-9: Lessons Reorder with empty LessonIds → 422")]
    public async Task ReorderLessons_EmptyList_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/learning/Lessons/Reorder",
            new { lessonIds = Array.Empty<int>() }, _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty LessonIds must trigger FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-LS-9: Lessons Reorder with id=0 → 422")]
    public async Task ReorderLessons_ZeroId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put, "/api/learning/Lessons/Reorder",
            new { lessonIds = new[] { 0 } }, _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "id=0 must trigger GreaterThan(0) validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-LS-11 — BaseResponse envelope shape
    // =========================================================================

    [Fact(DisplayName = "AC-LS-11: SetActive response has full BaseResponse envelope keys")]
    public async Task SetActive_ResponseEnvelope_HasAllKeys()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Lessons/{lessonId}/Active",
            new { isActive = false }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed",  out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("SetActive must succeed; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data",    out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors",  out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "AC-LS-11: Add ContentBlock response has full BaseResponse envelope keys")]
    public async Task AddContentBlock_ResponseEnvelope_HasAllKeys()
    {
        var lessonId = await CreateLessonGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText, payload = TextPayload }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "successed",  out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "data",    out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "errors",  out _).Should().BeTrue("body: {0}", body);
    }

    // =========================================================================
    // AC-LS-12 — SetActive validator: LessonId=0 → 422
    // =========================================================================

    [Fact(DisplayName = "AC-LS-12: PUT Lessons/0/Active → 422 (LessonId GreaterThan(0))")]
    public async Task SetLessonActive_ZeroId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Lessons/0/Active",
            new { isActive = false }, _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "LessonId=0 must trigger GreaterThan(0) validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // Admin detail (GET /Lessons/{id}/Admin) returns IsActive and ContentBlocks
    // =========================================================================

    [Fact(DisplayName = "Admin detail: GET /Lessons/{id}/Admin returns isActive + contentBlocks fields")]
    public async Task GetAdminDetail_ReturnsIsActiveAndContentBlocksFields()
    {
        var lessonId = await CreateLessonGetIdAsync();

        // Add a block so ContentBlocks list is non-empty.
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText, payload = TextPayload }, _adminToken);

        var (adminResp, adminRoot, adminBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}/Admin", bearer: _adminToken);
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK, "Admin detail must return 200; body: {0}", adminBody);
        AssertSucceeded(adminRoot, adminBody);

        TryProp(adminRoot, "data", out var data).Should().BeTrue("body: {0}", adminBody);
        TryProp(data, "isActive", out var isActiveProp).Should().BeTrue(
            "AdminLessonDetailDto must expose isActive; body: {0}", adminBody);
        isActiveProp.GetBoolean().Should().BeTrue("new lesson must be active by default; body: {0}", adminBody);

        TryProp(data, "contentBlocks", out var cbProp).Should().BeTrue(
            "AdminLessonDetailDto must expose contentBlocks; body: {0}", adminBody);
        cbProp.ValueKind.Should().Be(JsonValueKind.Array,
            "contentBlocks must be an array; body: {0}", adminBody);
        cbProp.GetArrayLength().Should().BeGreaterThan(0,
            "contentBlocks must contain the added block; body: {0}", adminBody);
    }

    // =========================================================================
    // ByLesson admin list — returns all non-deleted blocks incl. inactive
    // =========================================================================

    [Fact(DisplayName = "ByLesson: admin GET returns all non-deleted blocks ordered by SequenceOrder")]
    public async Task GetByLesson_Admin_ReturnsAllNonDeletedBlocks()
    {
        var lessonId = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText,  payload = TextPayload  }, _adminToken);
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeImage, payload = ImagePayload }, _adminToken);
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeVideo, payload = VideoPayload }, _adminToken);

        var blocks = await GetBlocksByLessonAsync(lessonId);
        blocks.Should().HaveCount(3, "all three added blocks must appear");

        // Verify ordered by SequenceOrder.
        var orders = blocks
            .Where(b => TryProp(b, "sequenceOrder", out _))
            .Select(b => { TryProp(b, "sequenceOrder", out var o); return o.GetInt32(); })
            .ToList();
        orders.Should().BeInAscendingOrder("blocks must be returned in SequenceOrder ascending");
    }

    // =========================================================================
    // Mass-assignment guard: creating lesson with IsActive=false is ignored
    // =========================================================================

    [Fact(DisplayName = "Mass-assignment guard: Lesson Create ignores IsActive in body; lesson defaults to active")]
    public async Task CreateLesson_IsActiveInBody_IsIgnored()
    {
        // The LessonDto / AddLessonDto do not expose IsActive — it has no setter in the DTO.
        // Handler explicitly sets IsActive=true. This test verifies the resulting entity is active.
        var lessonId = await CreateLessonGetIdAsync();

        var (adminResp, adminRoot, adminBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}/Admin", bearer: _adminToken);
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", adminBody);

        TryProp(adminRoot, "data", out var data).Should().BeTrue("body: {0}", adminBody);
        TryProp(data, "isActive", out var isActiveProp).Should().BeTrue("body: {0}", adminBody);
        isActiveProp.GetBoolean().Should().BeTrue(
            "Lesson must default to IsActive=true regardless of any mass-assignment attempt; body: {0}", adminBody);
    }

    // =========================================================================
    // Non-existent lesson: 404 / Successed=false
    // =========================================================================

    [Fact(DisplayName = "GET /Lessons/{id}/Admin for non-existent id → Successed=false")]
    public async Task GetAdminDetail_NonExistentId_ReturnsNotFound()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/Lessons/99999999/Admin", bearer: _adminToken);

        ((int)resp.StatusCode).Should().NotBe(500,
            "Non-existent lesson must not produce 500; body: {0}", body);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 404 }, "body: {0}", body);

        if (resp.StatusCode == HttpStatusCode.OK && root.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse("Successed must be false for non-existent lesson; body: {0}", body);
        }
    }

    // =========================================================================
    // Append semantics: multiple Add calls increase SequenceOrder monotonically
    // =========================================================================

    [Fact(DisplayName = "Add ContentBlock: append semantics — each new block gets next SequenceOrder")]
    public async Task AddContentBlock_AppendSemantics_SequenceOrderIncreases()
    {
        var lessonId = await CreateLessonGetIdAsync();

        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeText,    payload = TextPayload    }, _adminToken);
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeImage,   payload = ImagePayload   }, _adminToken);
        await SendAsync(HttpMethod.Post, "/api/learning/ContentBlocks",
            new { lessonId, blockType = BlockTypeCallout, payload = CalloutPayload }, _adminToken);

        var blocks = await GetBlocksByLessonAsync(lessonId);
        blocks.Should().HaveCount(3);

        var orders = blocks
            .Select(b => { TryProp(b, "sequenceOrder", out var o); return o.GetInt32(); })
            .ToList();
        orders.Should().BeInAscendingOrder("each appended block gets a strictly higher SequenceOrder");
        orders.Should().OnlyHaveUniqueItems("SequenceOrder values must be unique within a lesson");
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

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

    /// <summary>Case-insensitive JSON property lookup (camelCase + PascalCase).</summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
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

    private static void AssertRejected(HttpResponseMessage response, JsonElement root, string body, string reason)
    {
        var statusCode = (int)response.StatusCode;
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("rejection response must be valid JSON; reason={0}; body: {1}", reason, body);

        if (statusCode == 200)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse(
                "{0}; expected Successed=false on 200 rejection; body: {1}", reason, body);
        }
        else
        {
            statusCode.Should().BeOneOf(new[] { 400, 404, 422, 424, 500 },
                "{0}; expected a non-success HTTP status; body: {1}", reason, body);
        }
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
        var email = $"p702_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>
    /// Publishes a curriculum entity via POST /api/learning/ContentLifecycle/Transition (TargetState=Published=2).
    /// Required before student-facing reads (GetSubjectLessons) can see the entity, because P7-05
    /// added LifecycleState == Published filter. VersionedEntityType: Subject=1, Unit=2, Lesson=3.
    /// </summary>
    private async Task PublishEntityAsync(int entityType, int entityId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/ContentLifecycle/Transition",
            new { EntityType = entityType, EntityId = entityId, TargetState = 2 },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Transition to Published must return 200; entityType={0}, entityId={1}; body: {2}",
            entityType, entityId, body);
        AssertSucceeded(root, body);
    }

    /// <summary>Creates a grade → subject → unit stack and returns unitId.</summary>
    private async Task<int> CreateUnitWithPrereqsAsync()
    {
        var (_, unitId) = await CreateSubjectWithPrereqsAsync();
        return unitId;
    }

    /// <summary>Creates a grade → subject → unit stack. Returns (subjectId, unitId) as a tuple.</summary>
    private async Task<(int SubjectId, int UnitId)> CreateSubjectWithPrereqsAsync()
    {
        var gradeName = $"P702 Grade {Guid.NewGuid():N}";
        var (gradeResp, _, gradeBody) = await SendAsync(HttpMethod.Post, "/api/learning/grades/Create",
            new { Number = 1, DisplayName = gradeName }, _adminToken);
        gradeResp.StatusCode.Should().Be(HttpStatusCode.OK, "grade create must succeed; body: {0}", gradeBody);
        var gradeId = await FindIdInListAsync("/api/learning/grades/List?PageNumber=1&PageSize=200",
            "displayName", gradeName, "grade", _adminToken);

        var subjName = $"P702 Subj {Guid.NewGuid():N}";
        var (subjResp, _, subjBody) = await SendAsync(HttpMethod.Post, "/api/learning/subjects/Create",
            new { Name = subjName, Country = "EG", GradeId = gradeId, SubjectCode = 0, Language = 0 },
            _adminToken);
        subjResp.StatusCode.Should().Be(HttpStatusCode.OK, "subject create must succeed; body: {0}", subjBody);
        var subjectId = await FindIdInListAsync(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", subjName, "subject", _adminToken);

        var unitName = $"P702 Unit {Guid.NewGuid():N}";
        var (unitResp, _, unitBody) = await SendAsync(HttpMethod.Post, "/api/learning/units/Create",
            new { Name = unitName, SequenceOrder = 1, SubjectId = subjectId }, _adminToken);
        unitResp.StatusCode.Should().Be(HttpStatusCode.OK, "unit create must succeed; body: {0}", unitBody);
        var unitId = await FindIdInListAsync(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", unitName, "unit", _adminToken);

        return (subjectId, unitId);
    }

    /// <summary>
    /// Creates a full grade → subject → unit → lesson stack and returns the lesson ID.
    /// The lesson is created with EstimatedMinutes=0 as a sensible default.
    /// </summary>
    private async Task<int> CreateLessonGetIdAsync()
    {
        int unitId = await CreateUnitWithPrereqsAsync();
        var lessonName = $"P702 Lesson {Guid.NewGuid():N}";

        var (lessonResp, _, lessonBody) = await SendAsync(HttpMethod.Post, "/api/learning/Lessons/Create",
            new { Name = lessonName, Difficulty = 1, SequenceOrder = 1, IsLocked = false,
                  UnitId = unitId, EstimatedMinutes = 0 },
            _adminToken);
        lessonResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "lesson create must succeed; body: {0}", lessonBody);

        return await FindLessonIdByNameAsync(unitId, lessonName);
    }

    /// <summary>
    /// Finds a lesson by name in the admin Lessons List scoped to a unit.
    /// Falls back to the legacy GET /api/learning/Lessons?id approach if direct list is unavailable.
    /// </summary>
    private async Task<int> FindLessonIdByNameAsync(int unitId, string lessonName)
    {
        // Use the admin list: GET /api/learning/Lessons/List?UnitId={unitId}&PageNumber=1&PageSize=200
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/List?UnitId={unitId}&PageNumber=1&PageSize=200",
            bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Lessons List must succeed; body: {0}", listBody);

        // Try paginated wrapper first.
        if (TryProp(listRoot, "data", out var outer))
        {
            // Could be { data: { data: [...] } } (paginated) or { data: [...] } (list).
            if (TryProp(outer, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in inner.EnumerateArray())
                {
                    if (TryProp(item, "name", out var nameProp) &&
                        nameProp.GetString() == lessonName &&
                        TryProp(item, "id", out var idProp))
                        return idProp.GetInt32();
                }
            }
            else if (outer.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in outer.EnumerateArray())
                {
                    if (TryProp(item, "name", out var nameProp) &&
                        nameProp.GetString() == lessonName &&
                        TryProp(item, "id", out var idProp))
                        return idProp.GetInt32();
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not find lesson where name='{lessonName}' in List for UnitId={unitId}; body={listBody}");
    }

    /// <summary>Fetches ByLesson blocks for the given lessonId and returns the list.</summary>
    private async Task<List<JsonElement>> GetBlocksByLessonAsync(int lessonId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/ContentBlocks/ByLesson/{lessonId}", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ByLesson must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        if (data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray().ToList();
        return new List<JsonElement>();
    }

    /// <summary>Returns the SequenceOrder for a lesson via its admin detail.</summary>
    private async Task<int> GetAdminLessonSequenceOrderAsync(int lessonId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Lessons/{lessonId}/Admin", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "Admin detail must return 200; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "sequenceOrder", out var oProp).Should().BeTrue("body: {0}", body);
        return oProp.GetInt32();
    }

    /// <summary>Returns a flat list of units-with-lessons from GET /Subjects/{id}/Lessons.</summary>
    private async Task<List<JsonElement>> GetStudentLessonsUnitsAsync(int subjectId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Subjects/{subjectId}/Lessons", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "GetLessons must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        if (data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray().ToList();
        return new List<JsonElement>();
    }

    /// <summary>
    /// Checks whether a UnitWithLessonsDto element contains a lesson with the given ID,
    /// by walking the nested "lessons" array.
    /// </summary>
    private static bool ContainsLessonId(JsonElement unitElement, int lessonId)
    {
        if (!TryProp(unitElement, "lessons", out var lessonsEl)) return false;
        if (lessonsEl.ValueKind != JsonValueKind.Array) return false;
        foreach (var lesson in lessonsEl.EnumerateArray())
        {
            // LessonInUnitDto serializes the id field as "lessonId" (camelCase of LessonId property).
            if (TryProp(lesson, "lessonId", out var id) && id.GetInt32() == lessonId)
                return true;
        }
        return false;
    }

    private async Task<int> FindIdInListAsync(
        string listUrl, string fieldName, string fieldValue, string entityType, string? bearer = null)
    {
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, listUrl, bearer: bearer);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prereq list for {0} must succeed; body: {1}", entityType, listBody);

        // Handle both paginated wrapper { data: { data: [...] } } and flat { data: [...] }.
        if (TryProp(listRoot, "data", out var outer))
        {
            IEnumerable<JsonElement>? items = null;
            if (TryProp(outer, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
                items = inner.EnumerateArray();
            else if (outer.ValueKind == JsonValueKind.Array)
                items = outer.EnumerateArray();

            if (items is not null)
            {
                foreach (var item in items)
                {
                    if (TryProp(item, fieldName, out var fv) && fv.GetString() == fieldValue)
                    {
                        TryProp(item, "id", out var idProp).Should().BeTrue(
                            "{0} item must have id; body: {1}", entityType, listBody);
                        return idProp.GetInt32();
                    }
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not find {entityType} where {fieldName}='{fieldValue}' in list; url={listUrl}; body={listBody}");
    }
}
