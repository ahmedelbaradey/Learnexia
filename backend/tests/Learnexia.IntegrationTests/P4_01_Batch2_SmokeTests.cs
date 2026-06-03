using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// Smoke / regression tests that gate P4-01 Batch 3.
///
/// Tests validate two things after the Batch-2 MediatR rewire (single AddCrossModuleMediatR)
/// and the new Identity UnitOfWorkBehavior:
///
///   1. AppBoots     — the host builds and DI resolves (broken registration → startup exception).
///   2. SignIn       — POST /api/Users/Authentication/Sign-In with the seeded superadmin succeeds
///                     end-to-end through the new UnitOfWorkBehavior (the prime regression suspect).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P4_01_Batch2_SmokeTests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // Options that tolerate the PascalCase serialisation Newtonsoft uses in this project
    // (FluentAssertions just checks property existence / values from the JsonElement).
    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public P4_01_Batch2_SmokeTests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Migrations + seed run once per factory instance (fixture), but we guard idempotently.
        await _factory.ApplyMigrationsAndSeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Test 1: App boots — DI resolves with the unified MediatR
    // =========================================================================
    [Fact(DisplayName = "T1 AppBoots: host builds and DI container resolves")]
    public void T1_AppBoots_HostBuilds_NoStartupException()
    {
        // If the factory threw during CreateClient() / InitializeAsync(), this line is never
        // reached and xUnit reports the class-level failure. Reaching here proves the host built.
        _client.Should().NotBeNull("the WebApplicationFactory must have built the host successfully");
    }

    // =========================================================================
    // Test 2: Identity sign-in — exercises UnitOfWorkBehavior on existing command
    // =========================================================================
    [Fact(DisplayName = "T2 SignIn: seeded superadmin signs in through UnitOfWorkBehavior")]
    public async Task T2_SignIn_SuperAdmin_Returns200_WithToken()
    {
        // Arrange
        var body = new { UserName = "superadmin", Password = "123Pa$$word!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/Users/Authentication/Sign-In", body);

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in with valid seeded credentials must return HTTP 200 OK; " +
            "HTTP 500 indicates a UnitOfWorkBehavior transaction error wrapping SignInCommandHandler");

        var content = await response.Content.ReadAsStringAsync();

        // Parse using case-insensitive options (project uses PascalCase JSON keys)
        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // BaseResponse envelope shape
        root.TryGetProperty("successed", out var succeededProp).Should().BeTrue(
            "response body must contain 'successed' (sic) key per BaseResponse<T>");
        succeededProp.GetBoolean().Should().BeTrue(
            "successed must be true for a valid sign-in; false signals an application-level error: {0}", content);

        // Data must contain an AccessToken
        root.TryGetProperty("data", out var dataProp).Should().BeTrue("response must contain 'data'");
        dataProp.TryGetProperty("accessToken", out var tokenProp).Should().BeTrue(
            "'data.accessToken' must be present in the JwtAuthResponse payload");
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "AccessToken must be a non-empty JWT string");
    }
}

/// <summary>
/// xUnit collection fixture that shares one PostgreSQL container + factory across all tests in
/// this file, avoiding per-test container spin-up cost.
/// </summary>
[CollectionDefinition("IntegrationTests")]
public sealed class IntegrationTestsCollection : ICollectionFixture<LearnexiaWebAppFactory>
{
}
