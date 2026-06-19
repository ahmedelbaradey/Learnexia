// ReSharper disable InconsistentNaming
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// Auth coverage for ConceptsController after class-level [Authorize] was added.
///
/// Endpoints under test (api/learning/Concepts):
///   GET /api/learning/Concepts/List  — class-level [Authorize]; reads require a valid JWT.
///   GET /api/learning/Concepts?id=1  — class-level [Authorize]; reads require a valid JWT.
///
/// Cases:
///   CONCEPTS-AUTH-1 : GET /List anonymous (no token)       → 401.
///   CONCEPTS-AUTH-2 : GET ?id=1  anonymous (no token)      → 401.
///   CONCEPTS-AUTH-3 : GET /List  with admin JWT            → 200 (or 404 — NOT 401).
///   CONCEPTS-AUTH-4 : GET ?id=1  with admin JWT            → 200/404 (NOT 401).
///
/// Docker + Postgres required to RUN (Testcontainers). Compile-only on Windows CI (no Docker daemon).
/// Runs in the shared "IntegrationTests" xUnit collection (LearnexiaWebAppFactory, Testcontainers PG).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_ConceptsController_Auth_Tests : IAsyncLifetime
{
    private const string SignInUrl   = "api/Users/Authentication/Sign-In";
    private const string ListUrl     = "api/learning/Concepts/List";
    private const string GetByIdUrl  = "api/learning/Concepts?id=1";

    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string? _adminToken;

    public P7_ConceptsController_Auth_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();
        _adminToken = await SignInGetTokenAsync(AdminUserName, AdminPassword);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // CONCEPTS-AUTH-1: anonymous List → 401
    // =========================================================================

    [Fact(DisplayName = "CONCEPTS-AUTH-1: GET /api/learning/Concepts/List anonymous → 401 (class-level [Authorize])")]
    public async Task ConceptsList_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, ListUrl, bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "ConceptsController has class-level [Authorize]; anonymous GET /List must return 401; body: {0}", body);
    }

    // =========================================================================
    // CONCEPTS-AUTH-2: anonymous GetById → 401
    // =========================================================================

    [Fact(DisplayName = "CONCEPTS-AUTH-2: GET /api/learning/Concepts?id=1 anonymous → 401 (class-level [Authorize])")]
    public async Task ConceptsGetById_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, GetByIdUrl, bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "ConceptsController has class-level [Authorize]; anonymous GET ?id=1 must return 401; body: {0}", body);
    }

    // =========================================================================
    // CONCEPTS-AUTH-3: authenticated List → 200 (NOT 401)
    // =========================================================================

    [Fact(DisplayName = "CONCEPTS-AUTH-3: GET /api/learning/Concepts/List with admin JWT → 200 (auth passes, not 401)")]
    public async Task ConceptsList_WithAdminToken_NotUnauthorized()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, ListUrl, bearer: _adminToken);

        // The endpoint returns 200 (empty list when no concepts seeded) for an authenticated request.
        // We assert NOT 401 — the precise status depends on whether concepts are seeded.
        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "a valid admin JWT must pass the [Authorize] gate; must NOT return 401; body: {0}", body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /List with a valid JWT should return 200 (possibly empty list); body: {0}", body);
    }

    // =========================================================================
    // CONCEPTS-AUTH-4: authenticated GetById → 200 or 404 (NOT 401)
    // =========================================================================

    [Fact(DisplayName = "CONCEPTS-AUTH-4: GET /api/learning/Concepts?id=1 with admin JWT → 200 or 404 (NOT 401)")]
    public async Task ConceptsGetById_WithAdminToken_NotUnauthorized()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, GetByIdUrl, bearer: _adminToken);

        // id=1 may or may not exist in the test DB — both 200 and 404 are valid.
        // The critical assertion: must NOT be 401 (auth must have passed).
        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "a valid admin JWT must pass the [Authorize] gate; must NOT return 401; body: {0}", body);

        new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }.Should().Contain(resp.StatusCode,
            "authenticated GET ?id=1 must return 200 (found) or 404 (not found), never 401; body: {0}", body);
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
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
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
}
