using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P4-01 Batch 3: cross-module event delivery + AdminOnly policy integration tests.
///
/// Security changes landed in P4-01-BE-10 (security-audit batch 3):
///   - GET /api/Notifications/Notifications/List is now ADMIN-ONLY ([Authorize(Policy="AdminOnly")],
///     RequireRole("Admin","SuperAdmin")), so anonymous → 401, non-admin → 403, admin → 200.
///   - NotificationResponse dropped RecipientExternalUserId (keyed on opaque Id Guid).
///   - UserRegisteredIntegrationEvent dropped Email.
///
/// Tests in this file:
///   T4 – Anonymous call → 401 (security: endpoint must deny unauthenticated callers).
///   T5 – Superadmin creates user → resolve id → admin-gated List → 200 + Welcome notification
///        (end-to-end cross-module delivery proof; does NOT assert recipientExternalUserId).
///   T6 – Non-admin (seeded basicuser) → 403 (policy denies non-Admin/SuperAdmin roles).
///
/// Regression: T1/T2/T3 smoke tests (boot, sign-in, catalog) stay green in P4_01_Batch2_SmokeTests.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P4_01_Batch3_CrossModuleDeliveryTests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public P4_01_Batch3_CrossModuleDeliveryTests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Idempotent — migrations + seed are safe to call multiple times.
        await _factory.ApplyMigrationsAndSeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // T4: Anonymous → 401  (endpoint locked behind AdminOnly policy)
    // =========================================================================
    [Fact(DisplayName = "T4 NotificationsList_Anonymous: GET without token returns 401 Unauthorized")]
    public async Task T4_NotificationsList_NoToken_Returns401()
    {
        // Ensure no Authorization header is attached (factory client starts clean; be explicit).
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync(
            "/api/Notifications/Notifications/List?recipientUserId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Notifications/List is [Authorize(Policy=\"AdminOnly\")]; an anonymous request must " +
            "be denied with 401, not {0}. A 200 means the [Authorize] attribute is missing or " +
            "not registered; a 500 signals a startup/DI error.",
            (int)response.StatusCode);
    }

    // =========================================================================
    // T5: Admin (superadmin) creates user → cross-module delivery → admin-gated List → 200
    // =========================================================================
    [Fact(DisplayName = "T5 CrossModuleDelivery: superadmin AddUser → Welcome notification visible via admin-gated List")]
    public async Task T5_AddUser_PublishesEvent_AdminListReturns200_WithWelcomeNotification()
    {
        // ---- Step 1: sign in as superadmin to get a bearer token ----
        var signInBody = new { UserName = "superadmin", Password = "123Pa$$word!" };
        var signInResponse = await _client.PostAsJsonAsync("/api/Users/Authentication/Sign-In", signInBody);
        signInResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: superadmin sign-in must succeed; HTTP {0} indicates a startup/seed problem: {1}",
            (int)signInResponse.StatusCode,
            await signInResponse.Content.ReadAsStringAsync());

        var signInContent = await signInResponse.Content.ReadAsStringAsync();
        var signInDoc = JsonDocument.Parse(signInContent);
        var accessToken = signInDoc.RootElement
            .GetProperty("data")
            .GetProperty("accessToken")
            .GetString();
        accessToken.Should().NotBeNullOrWhiteSpace("a valid JWT token must be returned on sign-in");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // ---- Step 2: create a new user via AddUser (bearer token attached) ----
        // Use a timestamp-suffixed username/email to guarantee uniqueness across test runs.
        var uniqueSuffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var newUserName = $"testuser_{uniqueSuffix}";
        var newEmail = $"testuser_{uniqueSuffix}@integration.test";

        var addUserBody = new
        {
            FullName = "Integration Test User",
            UserName = newUserName,
            Email = newEmail,
            Roles = new[] { "USER" },
        };

        var addUserResponse = await _client.PostAsJsonAsync(
            "/api/Users/UserManagement/AddUser", addUserBody);

        var addUserContent = await addUserResponse.Content.ReadAsStringAsync();
        addUserResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "AddUser must return HTTP 200; actual body: {0}", addUserContent);

        var addUserDoc = JsonDocument.Parse(addUserContent);
        addUserDoc.RootElement
            .GetProperty("successed").GetBoolean()
            .Should().BeTrue("AddUser response must have successed=true; body: {0}", addUserContent);

        // ---- Step 3: resolve the new user's integer Id by querying UserList ----
        var userListResponse = await _client.GetAsync(
            $"/api/Users/UserManagement/UserList?search={newUserName}&pageNumber=1&pageSize=5");

        var userListContent = await userListResponse.Content.ReadAsStringAsync();
        userListResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "UserList must return HTTP 200 after user creation; body: {0}", userListContent);

        var userListDoc = JsonDocument.Parse(userListContent);
        var userListRoot = userListDoc.RootElement;

        userListRoot.TryGetProperty("data", out var dataEl).Should().BeTrue(
            "UserList response must contain 'data'; body: {0}", userListContent);

        // data may be an array directly or an object wrapping an inner array.
        JsonElement usersArray;
        if (dataEl.ValueKind == JsonValueKind.Array)
        {
            usersArray = dataEl;
        }
        else if (dataEl.ValueKind == JsonValueKind.Object &&
                 dataEl.TryGetProperty("data", out var innerData) &&
                 innerData.ValueKind == JsonValueKind.Array)
        {
            usersArray = innerData;
        }
        else
        {
            usersArray = dataEl;
        }

        int newUserId = -1;
        foreach (var user in usersArray.EnumerateArray())
        {
            if (user.TryGetProperty("userName", out var unProp) &&
                unProp.GetString()?.Equals(newUserName, StringComparison.OrdinalIgnoreCase) == true)
            {
                newUserId = user.GetProperty("id").GetInt32();
                break;
            }
        }

        newUserId.Should().BeGreaterThan(0,
            "a valid integer user Id must be found in UserList for username '{0}'; " +
            "body: {1}", newUserName, userListContent);

        // ---- Step 4: query Notifications/List as superadmin (bearer token still attached) ----
        // MediatR notification dispatch is synchronous in-process, so no polling needed.
        var notifResponse = await _client.GetAsync(
            $"/api/Notifications/Notifications/List?recipientUserId={newUserId}");

        var notifContent = await notifResponse.Content.ReadAsStringAsync();
        notifResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "Notifications/List called with a superadmin bearer token must return HTTP 200; " +
            "403 means the AdminOnly policy did not recognise the superadmin role in the JWT " +
            "(check role name casing — RoleSeeder seeds lowercase; RequireRole expects PascalCase); " +
            "401 means the JWT was rejected; body: {0}", notifContent);

        var notifDoc = JsonDocument.Parse(notifContent);
        var notifRoot = notifDoc.RootElement;

        notifRoot.TryGetProperty("successed", out var notifSuccessed).Should().BeTrue(
            "Notifications/List response must have 'successed' key; body: {0}", notifContent);
        notifSuccessed.GetBoolean().Should().BeTrue(
            "successed must be true for a valid admin Notifications/List call; body: {0}", notifContent);

        notifRoot.TryGetProperty("data", out var notifData).Should().BeTrue(
            "Notifications/List response must have 'data' key; body: {0}", notifContent);

        JsonElement notifArray;
        if (notifData.ValueKind == JsonValueKind.Array)
        {
            notifArray = notifData;
        }
        else
        {
            Assert.Fail($"Expected 'data' to be a JSON array, got {notifData.ValueKind}. Body: {notifContent}");
            return; // unreachable — silences CS0165
        }

        notifArray.GetArrayLength().Should().Be(1,
            "exactly ONE Welcome notification must exist for user {0} (cross-module delivery proof); " +
            "actual notification list: {1}", newUserId, notifContent);

        var firstNotif = notifArray.EnumerateArray().First();

        // Type=2 => NotificationType.Welcome
        firstNotif.TryGetProperty("type", out var typeProp).Should().BeTrue(
            "notification must have 'type' field; body: {0}", notifContent);
        typeProp.GetInt32().Should().Be(2,
            "notification Type must be 2 (NotificationType.Welcome); body: {0}", notifContent);

        // Title must be non-empty (set by Notification.CreateWelcome factory)
        firstNotif.TryGetProperty("title", out var titleProp).Should().BeTrue(
            "notification must have 'title' field; body: {0}", notifContent);
        titleProp.GetString().Should().NotBeNullOrWhiteSpace(
            "notification Title must not be empty; body: {0}", notifContent);

        // NotificationResponse.Id must be a non-empty Guid (opaque key; RecipientExternalUserId removed).
        firstNotif.TryGetProperty("id", out var idProp).Should().BeTrue(
            "NotificationResponse must expose opaque 'id' (Guid); " +
            "'recipientExternalUserId' was removed in security audit P4-01 #2; body: {0}", notifContent);
        idProp.GetGuid().Should().NotBeEmpty(
            "notification Id must be a non-empty Guid; body: {0}", notifContent);

        // Confirm recipientExternalUserId is NOT present in the response (intentional removal).
        firstNotif.TryGetProperty("recipientExternalUserId", out _).Should().BeFalse(
            "RecipientExternalUserId was deliberately removed from NotificationResponse " +
            "(security audit P4-01 #2); it must not appear in the JSON body: {0}", notifContent);
    }

    // =========================================================================
    // T6: Non-admin user → 403 Forbidden
    // =========================================================================
    [Fact(DisplayName = "T6 NotificationsList_NonAdmin: seeded basicuser gets 403 Forbidden")]
    public async Task T6_NotificationsList_NonAdminToken_Returns403()
    {
        // basicuser is seeded with only the 'Basic' role (not Admin/SuperAdmin).
        var signInBody = new { UserName = "basicuser", Password = "123Pa$$word!" };
        var signInResponse = await _client.PostAsJsonAsync("/api/Users/Authentication/Sign-In", signInBody);
        signInResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: basicuser sign-in must succeed; HTTP {0} body: {1}",
            (int)signInResponse.StatusCode,
            await signInResponse.Content.ReadAsStringAsync());

        var signInContent = await signInResponse.Content.ReadAsStringAsync();
        var signInDoc = JsonDocument.Parse(signInContent);
        var basicToken = signInDoc.RootElement
            .GetProperty("data")
            .GetProperty("accessToken")
            .GetString();
        basicToken.Should().NotBeNullOrWhiteSpace("basicuser sign-in must return a JWT");

        // Attach the basic (non-admin) token.
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", basicToken);

        var response = await _client.GetAsync(
            "/api/Notifications/Notifications/List?recipientUserId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Notifications/List requires Admin or SuperAdmin role; a non-admin JWT must receive " +
            "403 Forbidden (authenticated but not authorized). " +
            "401 means the JWT was rejected outright; 200 means the policy is not enforced. " +
            "Status: {0}", (int)response.StatusCode);
    }
}
