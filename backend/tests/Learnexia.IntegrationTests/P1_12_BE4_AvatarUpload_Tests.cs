using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P1-12 BE-4 integration tests: Avatar upload/remove via POST/DELETE /api/Users/Account/Avatar.
///
/// The headline test (AC-2) proves the full SigV4 round-trip:
///   1. The app signs a PUT to the MinIO container with hand-rolled AWS SigV4 (AwsV4Signer).
///   2. The presigned GET URL returned by the upload is fetched with a plain HttpClient (no auth headers).
///   3. The bytes retrieved equal the bytes uploaded — proving both the SigV4 PUT stored the object
///      AND the presigned-GET signature is a valid MinIO SigV4 authorization.
///
/// External dependency: a self-hosted MinIO container started by Testcontainers (image
/// quay.io/minio/minio:latest, port 9000). The bucket "avatars" is pre-created in fixture setup via
/// the MinIO S3 API (PUT /{bucket}). No AWS services are used.
///
/// Test-only NuGet used: Testcontainers (base generic container builder, already in Directory.Packages.props).
///
/// Endpoints under test:
///   [Authorize] POST   /api/Users/Account/Avatar       multipart IFormFile → BaseResponse&lt;AvatarUploadResponse&gt;
///   [Authorize] DELETE /api/Users/Account/Avatar       → BaseResponse (clears AvatarUrl)
///   [Authorize] GET    /api/Users/Account/Profile      → avatarUrl populated after upload
///   [Authorize] GET    /api/Users/Me                   → avatarUrl populated after upload
///
/// Acceptance criteria:
///   AC-1  Auth        Unauthenticated POST/DELETE → 401.
///   AC-2  SigV4 PUT+presign happy path: upload valid PNG → 200/Successed=true + non-empty avatarUrl;
///         fetching that presigned URL with a plain HttpClient → 200 + bytes match what was uploaded.
///   AC-3  Profile/Me  After upload, GET /Profile and GET /Me return non-null avatarUrl (presigned).
///   AC-4  Persistence Object key stored on user; a second GET /Profile after the first still yields a
///         working presigned URL.
///   AC-5  Validation  Wrong content-type (text/plain) → 422; oversized file → 422; empty file → 422;
///         magic-byte spoofing (PNG named .txt with text content-type) → 422.
///         (Relocated to Shared.Kernel: UploadAvatarCommandHandler now returns UnprocessableEntity for all
///         invalid-file rejections. Auth cases stay at 401.)
///   AC-6  Delete      After DELETE /Avatar, GET /Me returns null avatarUrl.
/// </summary>
[Collection("AvatarIntegrationTests")]
public sealed class P1_12_BE4_AvatarUpload_Tests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // URLs
    // ---------------------------------------------------------------------------
    private const string AvatarUrl = "api/Users/Account/Avatar";
    private const string ProfileUrl = "api/Users/Account/Profile";
    private const string MeUrl = "api/Users/Me";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------
    private readonly AvatarTestFixture _fixture;
    private readonly HttpClient _client;

    public P1_12_BE4_AvatarUpload_Tests(AvatarTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    public async Task InitializeAsync() => await _fixture.Factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static string UniqueEmail(string tag = "")
        => $"p112be4_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@avatar.test";

    /// <summary>
    /// Case-insensitive property lookup (handles Newtonsoft camelCase + STJ PascalCase paths).
    /// </summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out value)) return true;
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

    private static async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url, HttpContent? content = null, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (content is not null)
            request.Content = content;
        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await client.SendAsync(request);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    private static async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendJsonAsync(HttpClient client, HttpMethod method, string url, object? body = null, string? bearerToken = null)
    {
        HttpContent? content = null;
        if (body is not null)
            content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return await SendAsync(client, method, url, content, bearerToken);
    }

    /// <summary>
    /// Registers a parent and returns the access token.
    /// </summary>
    private async Task<string> RegisterParentAsync(string email, string password = "Str0ng@Pass", string fullName = "Test Parent")
    {
        var payload = new { Email = email, Password = password, FullName = fullName, AcceptedTerms = true };
        var (resp, root, body) = await SendJsonAsync(_client, HttpMethod.Post, RegisterParentUrl, payload);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", body);
        return tokenProp.GetString()!;
    }

    /// <summary>
    /// Builds a minimal valid PNG file (1x1 pixel). The PNG magic bytes are correct so the
    /// magic-byte validator accepts it.
    /// </summary>
    private static byte[] BuildMinimalPng()
    {
        // A real 1x1 pixel, 1-bit depth, grayscale PNG (67 bytes).
        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        // IHDR chunk (1x1 px, bit depth 1, color type 0 = grayscale, no interlace)
        // IDAT chunk (compressed image data)
        // IEND chunk
        return new byte[]
        {
            // PNG signature
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            // IHDR chunk: length=13
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52, // "IHDR"
            0x00, 0x00, 0x00, 0x01, // width=1
            0x00, 0x00, 0x00, 0x01, // height=1
            0x08,                   // bit depth=8
            0x02,                   // color type=2 (RGB)
            0x00,                   // compression method=0
            0x00,                   // filter method=0
            0x00,                   // interlace method=0
            0x90, 0x77, 0x53, 0xDE, // CRC32
            // IDAT chunk: compressed pixel data
            0x00, 0x00, 0x00, 0x0C,
            0x49, 0x44, 0x41, 0x54, // "IDAT"
            0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01,
            0xE2, 0x21, 0xBC, 0x33, // CRC32
            // IEND chunk
            0x00, 0x00, 0x00, 0x00,
            0x49, 0x45, 0x4E, 0x44, // "IEND"
            0xAE, 0x42, 0x60, 0x82  // CRC32
        };
    }

    /// <summary>
    /// Builds a multipart form-data content carrying the given file bytes as "file".
    /// </summary>
    private static MultipartFormDataContent BuildFileContent(byte[] fileBytes, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }

    // ===========================================================================
    // AC-1: Auth — unauthenticated POST/DELETE → 401
    // ===========================================================================

    [Fact(DisplayName = "AC-1-a: POST /api/Users/Account/Avatar without token → 401")]
    public async Task Auth_Post_NoToken_Returns401()
    {
        var pngBytes = BuildMinimalPng();
        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Avatar POST without bearer token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-1-b: DELETE /api/Users/Account/Avatar without token → 401")]
    public async Task Auth_Delete_NoToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Delete, AvatarUrl);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Avatar DELETE without bearer token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-1-c: POST /api/Users/Account/Avatar with invalid token → 401")]
    public async Task Auth_Post_InvalidToken_Returns401()
    {
        var pngBytes = BuildMinimalPng();
        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, "not.a.valid.jwt");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Avatar POST with invalid bearer token must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-1-d: DELETE /api/Users/Account/Avatar with invalid token → 401")]
    public async Task Auth_Delete_InvalidToken_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Delete, AvatarUrl,
            bearerToken: "not.a.valid.jwt");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Avatar DELETE with invalid bearer token must return 401; body: {0}", body);
    }

    // ===========================================================================
    // AC-2: SigV4 PUT + presign happy path
    // This is the headline test: proves the hand-rolled AwsV4Signer works end-to-end.
    // ===========================================================================

    [Fact(DisplayName = "AC-2-a: Upload valid PNG → 200/Successed=true, non-empty avatarUrl (SigV4 PUT succeeds)")]
    public async Task Upload_ValidPng_Returns200_WithAvatarUrl()
    {
        var token = await RegisterParentAsync(UniqueEmail("upload-happy"));
        var pngBytes = BuildMinimalPng();

        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Valid PNG upload with valid token must return 200; body: {0}", body);

        TryProp(root, "successed", out var succeeded).Should().BeTrue("body: {0}", body);
        succeeded.GetBoolean().Should().BeTrue(
            "Successed must be true on successful upload; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null on success; body: {0}", body);

        TryProp(data, "avatarUrl", out var avatarUrlProp).Should().BeTrue(
            "data must contain 'avatarUrl'; body: {0}", body);
        var avatarUrlStr = avatarUrlProp.GetString();
        avatarUrlStr.Should().NotBeNullOrWhiteSpace(
            "avatarUrl must be a non-empty presigned URL; body: {0}", body);
    }

    [Fact(DisplayName = "AC-2-b: Presigned GET URL returned by upload → HTTP 200 + bytes match what was uploaded (SigV4 round-trip proof)")]
    public async Task Upload_PresignedUrl_Roundtrip_BytesMatch()
    {
        var token = await RegisterParentAsync(UniqueEmail("sigv4-roundtrip"));
        var pngBytes = BuildMinimalPng();

        // Upload
        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (uploadResp, uploadRoot, uploadBody) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);

        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Upload prerequisite must succeed; body: {0}", uploadBody);
        TryProp(uploadRoot, "data", out var uploadData).Should().BeTrue("body: {0}", uploadBody);
        TryProp(uploadData, "avatarUrl", out var avatarUrlProp).Should().BeTrue("body: {0}", uploadBody);
        var presignedUrl = avatarUrlProp.GetString();
        presignedUrl.Should().NotBeNullOrWhiteSpace("Presigned URL must be non-empty; body: {0}", uploadBody);

        // Fetch the presigned URL with a plain HttpClient — no auth headers. This validates:
        // (a) The SigV4 PUT actually stored the object in MinIO.
        // (b) The presigned GET URL signature is valid (MinIO honours it).
        using var plainClient = new HttpClient();
        var getResp = await plainClient.GetAsync(presignedUrl);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Fetching the presigned URL without auth headers must return 200; URL: {0}", presignedUrl);

        var downloadedBytes = await getResp.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(pngBytes,
            "The downloaded bytes must exactly match what was uploaded (SigV4 PUT round-trip proof)");
    }

    // ===========================================================================
    // AC-3: Profile and Me reflect the avatarUrl after upload
    // ===========================================================================

    [Fact(DisplayName = "AC-3-a: After upload, GET /Profile returns non-null avatarUrl")]
    public async Task Upload_Profile_ReflectsAvatarUrl()
    {
        var token = await RegisterParentAsync(UniqueEmail("profile-avatar"));
        var pngBytes = BuildMinimalPng();

        // Upload
        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (uploadResp, _, uploadBody) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Upload prerequisite must succeed; body: {0}", uploadBody);

        // GET Profile
        var (profResp, profRoot, profBody) = await SendAsync(_client, HttpMethod.Get, ProfileUrl,
            bearerToken: token);
        profResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Profile after upload must return 200; body: {0}", profBody);

        TryProp(profRoot, "data", out var data).Should().BeTrue("body: {0}", profBody);
        TryProp(data, "avatarUrl", out var avatarUrlProp).Should().BeTrue("body: {0}", profBody);
        var avatarUrlStr = avatarUrlProp.GetString();
        avatarUrlStr.Should().NotBeNullOrWhiteSpace(
            "/Profile avatarUrl must be non-null after upload; body: {0}", profBody);
    }

    [Fact(DisplayName = "AC-3-b: After upload, GET /Me returns non-null avatarUrl")]
    public async Task Upload_Me_ReflectsAvatarUrl()
    {
        var token = await RegisterParentAsync(UniqueEmail("me-avatar"));
        var pngBytes = BuildMinimalPng();

        // Upload
        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (uploadResp, _, uploadBody) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Upload prerequisite must succeed; body: {0}", uploadBody);

        // GET /Me
        var (meResp, meRoot, meBody) = await SendAsync(_client, HttpMethod.Get, MeUrl,
            bearerToken: token);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Me after upload must return 200; body: {0}", meBody);

        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);
        TryProp(meData, "avatarUrl", out var avatarUrlProp).Should().BeTrue("body: {0}", meBody);
        var avatarUrlStr = avatarUrlProp.GetString();
        avatarUrlStr.Should().NotBeNullOrWhiteSpace(
            "/Me avatarUrl must be non-null after upload; body: {0}", meBody);
    }

    [Fact(DisplayName = "AC-3-c: The avatarUrl returned in /Me is a valid presigned URL that serves the uploaded bytes")]
    public async Task Upload_Me_AvatarUrl_IsFetchable()
    {
        var token = await RegisterParentAsync(UniqueEmail("me-url-fetch"));
        var pngBytes = BuildMinimalPng();

        // Upload
        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (uploadResp, _, uploadBody) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", uploadBody);

        // GET /Me → get the presigned URL
        var (meResp, meRoot, meBody) = await SendAsync(_client, HttpMethod.Get, MeUrl,
            bearerToken: token);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBody);

        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);
        TryProp(meData, "avatarUrl", out var avatarUrlProp).Should().BeTrue("body: {0}", meBody);
        var presignedUrl = avatarUrlProp.GetString();
        presignedUrl.Should().NotBeNullOrWhiteSpace("body: {0}", meBody);

        // Fetch with plain HttpClient (no auth)
        using var plainClient = new HttpClient();
        var getResp = await plainClient.GetAsync(presignedUrl);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Presigned URL from /Me must be fetchable without auth; URL: {0}", presignedUrl);

        var downloadedBytes = await getResp.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(pngBytes,
            "Bytes from presigned URL in /Me must match what was uploaded");
    }

    // ===========================================================================
    // AC-4: Persistence — object key persisted; a second GET /Profile yields a working presigned URL
    // ===========================================================================

    [Fact(DisplayName = "AC-4: Second GET /Profile returns a still-valid presigned URL (key persisted correctly)")]
    public async Task Upload_Persistence_SecondGetStillYieldsValidUrl()
    {
        var token = await RegisterParentAsync(UniqueEmail("persist"));
        var pngBytes = BuildMinimalPng();

        // Upload once
        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (uploadResp, _, uploadBody) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", uploadBody);

        // First GET /Profile (verify URL exists)
        var (prof1Resp, prof1Root, prof1Body) = await SendAsync(_client, HttpMethod.Get, ProfileUrl,
            bearerToken: token);
        prof1Resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", prof1Body);
        TryProp(prof1Root, "data", out var data1).Should().BeTrue("body: {0}", prof1Body);
        TryProp(data1, "avatarUrl", out var url1Prop).Should().BeTrue("body: {0}", prof1Body);
        var url1 = url1Prop.GetString();
        url1.Should().NotBeNullOrWhiteSpace("body: {0}", prof1Body);

        // Second GET /Profile (key still persisted)
        var (prof2Resp, prof2Root, prof2Body) = await SendAsync(_client, HttpMethod.Get, ProfileUrl,
            bearerToken: token);
        prof2Resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", prof2Body);
        TryProp(prof2Root, "data", out var data2).Should().BeTrue("body: {0}", prof2Body);
        TryProp(data2, "avatarUrl", out var url2Prop).Should().BeTrue("body: {0}", prof2Body);
        var url2 = url2Prop.GetString();
        url2.Should().NotBeNullOrWhiteSpace(
            "Second GET /Profile must return a non-null presigned URL; body: {0}", prof2Body);

        // Validate the second URL is fetchable
        using var plainClient = new HttpClient();
        var getResp = await plainClient.GetAsync(url2);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Presigned URL from second GET /Profile must be fetchable; URL: {0}", url2);
    }

    // ===========================================================================
    // AC-5: Validation — bad content-type, oversized, empty, magic-byte spoofing
    // ===========================================================================

    [Fact(DisplayName = "AC-5-a: POST with text/plain content-type → 422 (wrong MIME type)")]
    public async Task Validation_TextPlainContentType_Rejected()
    {
        var token = await RegisterParentAsync(UniqueEmail("val-mime"));

        // Send a file declared as text/plain (not an allowed image type)
        var textBytes = Encoding.UTF8.GetBytes("this is not an image at all");
        using var form = BuildFileContent(textBytes, "notanimage.txt", "text/plain");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);

        // After relocation to Shared.Kernel the handler returns UnprocessableEntity (422) for all
        // invalid-file rejections — not 400. Auth cases are still 401.
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "text/plain content-type must be rejected with 422; body: {0}", body);

        // Successed must be false
        if (root.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(root, "successed", out var succeededProp);
            if (succeededProp.ValueKind != JsonValueKind.Undefined)
                succeededProp.GetBoolean().Should().BeFalse("body: {0}", body);
        }
    }

    [Fact(DisplayName = "AC-5-b: POST with application/octet-stream content-type → 422 (not allowed image type)")]
    public async Task Validation_OctetStreamContentType_Rejected()
    {
        var token = await RegisterParentAsync(UniqueEmail("val-octet"));

        var bytes = Encoding.UTF8.GetBytes("not an image");
        using var form = BuildFileContent(bytes, "file.bin", "application/octet-stream");
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);

        // Strict 422 — invalid-file rejections are UnprocessableEntity after the relocation.
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "application/octet-stream must be rejected with 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-5-c: POST with empty file → 422 (file is required and non-empty)")]
    public async Task Validation_EmptyFile_Rejected()
    {
        var token = await RegisterParentAsync(UniqueEmail("val-empty"));

        // 0-byte file with PNG content-type
        using var form = BuildFileContent(Array.Empty<byte>(), "empty.png", "image/png");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);

        // Strict 422 — empty-file is an UnprocessableEntity (AvatarFileRequired) after the relocation.
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty file must be rejected with 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-5-d: POST with oversized file (> MaxFileSize 2 MB) → 422")]
    public async Task Validation_OversizedFile_Rejected()
    {
        var token = await RegisterParentAsync(UniqueEmail("val-size"));

        // Build a fake PNG with correct magic bytes but padded to exceed 2 MB
        var header = new byte[]
        {
            // PNG signature
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };
        var padding = new byte[2 * 1024 * 1024 + 100]; // slightly over 2 MB
        var oversized = header.Concat(padding).ToArray();

        using var form = BuildFileContent(oversized, "big.png", "image/png");
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);

        // Strict 422 — oversized is an UnprocessableEntity (AvatarFileTooLarge) after the relocation.
        // (413 RequestEntityTooLarge is no longer accepted — the handler gates size before touching storage.)
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Oversized file (> 2 MB) must be rejected with 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-5-e: POST with PNG content-type but text content (magic-byte mismatch) → 422")]
    public async Task Validation_MagicByteMismatch_Rejected()
    {
        var token = await RegisterParentAsync(UniqueEmail("val-magic"));

        // Declare image/png but send text content — the magic-byte check must catch this
        var textBytes = Encoding.UTF8.GetBytes("This is just a plain text file masquerading as a PNG.");
        using var form = BuildFileContent(textBytes, "fake.png", "image/png");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);

        // Strict 422 — magic-byte mismatch is an UnprocessableEntity (AvatarFileInvalidType) after the relocation.
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Spoofed content-type (PNG name, text body) must be rejected with 422; body: {0}", body);
    }

    // ===========================================================================
    // AC-6: Delete — after DELETE /Avatar, /Me returns null avatarUrl
    // ===========================================================================

    [Fact(DisplayName = "AC-6-a: DELETE /Avatar returns 200/Successed=true")]
    public async Task Delete_Returns200_Successed()
    {
        var token = await RegisterParentAsync(UniqueEmail("del-success"));
        var pngBytes = BuildMinimalPng();

        // Upload first
        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (uploadResp, _, uploadBody) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK, "Upload prerequisite; body: {0}", uploadBody);

        // DELETE
        var (delResp, delRoot, delBody) = await SendAsync(_client, HttpMethod.Delete, AvatarUrl,
            bearerToken: token);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "DELETE /Avatar must return 200; body: {0}", delBody);

        TryProp(delRoot, "successed", out var succeeded).Should().BeTrue("body: {0}", delBody);
        succeeded.GetBoolean().Should().BeTrue(
            "Successed must be true on successful delete; body: {0}", delBody);
    }

    [Fact(DisplayName = "AC-6-b: After DELETE /Avatar, GET /Me returns null avatarUrl")]
    public async Task Delete_Me_AvatarUrl_IsNull()
    {
        var token = await RegisterParentAsync(UniqueEmail("del-null-me"));
        var pngBytes = BuildMinimalPng();

        // Upload
        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (uploadResp, _, uploadBody) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK, "Upload prerequisite; body: {0}", uploadBody);

        // Verify avatarUrl is set
        var (meBeforeResp, meBeforeRoot, meBeforeBody) = await SendAsync(_client, HttpMethod.Get, MeUrl,
            bearerToken: token);
        meBeforeResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBeforeBody);
        TryProp(meBeforeRoot, "data", out var beforeData).Should().BeTrue("body: {0}", meBeforeBody);
        TryProp(beforeData, "avatarUrl", out var beforeUrl).Should().BeTrue("body: {0}", meBeforeBody);
        beforeUrl.GetString().Should().NotBeNullOrWhiteSpace(
            "/Me avatarUrl must be set after upload; body: {0}", meBeforeBody);

        // DELETE
        var (delResp, _, delBody) = await SendAsync(_client, HttpMethod.Delete, AvatarUrl,
            bearerToken: token);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK, "DELETE must succeed; body: {0}", delBody);

        // GET /Me — avatarUrl must now be null
        var (meAfterResp, meAfterRoot, meAfterBody) = await SendAsync(_client, HttpMethod.Get, MeUrl,
            bearerToken: token);
        meAfterResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meAfterBody);
        TryProp(meAfterRoot, "data", out var afterData).Should().BeTrue("body: {0}", meAfterBody);
        TryProp(afterData, "avatarUrl", out var afterUrl).Should().BeTrue("body: {0}", meAfterBody);
        afterUrl.ValueKind.Should().Be(JsonValueKind.Null,
            "/Me avatarUrl must be null after DELETE; body: {0}", meAfterBody);
    }

    [Fact(DisplayName = "AC-6-c: After DELETE /Avatar, GET /Profile returns null avatarUrl")]
    public async Task Delete_Profile_AvatarUrl_IsNull()
    {
        var token = await RegisterParentAsync(UniqueEmail("del-null-profile"));
        var pngBytes = BuildMinimalPng();

        // Upload
        using var form = BuildFileContent(pngBytes, "avatar.png", "image/png");
        var (uploadResp, _, uploadBody) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.OK, "Upload prerequisite; body: {0}", uploadBody);

        // DELETE
        var (delResp, _, delBody) = await SendAsync(_client, HttpMethod.Delete, AvatarUrl,
            bearerToken: token);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK, "DELETE must succeed; body: {0}", delBody);

        // GET /Profile — avatarUrl must now be null
        var (profResp, profRoot, profBody) = await SendAsync(_client, HttpMethod.Get, ProfileUrl,
            bearerToken: token);
        profResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", profBody);
        TryProp(profRoot, "data", out var data).Should().BeTrue("body: {0}", profBody);
        TryProp(data, "avatarUrl", out var avatarUrlProp).Should().BeTrue("body: {0}", profBody);
        avatarUrlProp.ValueKind.Should().Be(JsonValueKind.Null,
            "/Profile avatarUrl must be null after DELETE; body: {0}", profBody);
    }

    [Fact(DisplayName = "AC-6-d: DELETE /Avatar on a user with no avatar → 200/Successed=true (idempotent)")]
    public async Task Delete_WithNoAvatar_Returns200_Idempotent()
    {
        var token = await RegisterParentAsync(UniqueEmail("del-idempotent"));

        // DELETE without ever uploading — must be idempotent (already cleared)
        var (delResp, delRoot, delBody) = await SendAsync(_client, HttpMethod.Delete, AvatarUrl,
            bearerToken: token);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "DELETE on user with no avatar must be 200 (idempotent); body: {0}", delBody);

        TryProp(delRoot, "successed", out var succeeded).Should().BeTrue("body: {0}", delBody);
        succeeded.GetBoolean().Should().BeTrue("body: {0}", delBody);
    }

    // ===========================================================================
    // BE-TC-14: SVG (image/svg+xml) is explicitly excluded → 422
    // SVG must be rejected before magic-byte check (content-type allow-list gate)
    // because inline SVG is an XSS vector.
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-14: POST with image/svg+xml content-type → 422 (SVG excluded — XSS vector)")]
    public async Task Validation_SvgContentType_Rejected()
    {
        var token = await RegisterParentAsync(UniqueEmail("val-svg"));

        // SVG payload with correct content-type — must be rejected by the content-type allow-list
        var svgBytes = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
        using var form = BuildFileContent(svgBytes, "evil.svg", "image/svg+xml");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "SVG (image/svg+xml) must be rejected with 422 — it is not in the allowed type list (png/jpeg/webp); body: {0}", body);

        if (root.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(root, "successed", out var succeededProp);
            if (succeededProp.ValueKind != JsonValueKind.Undefined)
                succeededProp.GetBoolean().Should().BeFalse(
                    "Successed must be false for SVG rejection; body: {0}", body);
        }
    }

    // ===========================================================================
    // BE-TC-17: GIF (real but unsupported type) → 422
    // GIF is not in the allow-list (only png/jpeg/webp). Real GIF magic bytes = 47 49 46 38.
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-17: POST with genuine GIF (image/gif) → 422 (gif not in allow-list)")]
    public async Task Validation_GifContentType_Rejected()
    {
        var token = await RegisterParentAsync(UniqueEmail("val-gif"));

        // Minimal GIF87a: magic bytes GIF87a (47 49 46 38 37 61)
        var gifBytes = new byte[]
        {
            0x47, 0x49, 0x46, 0x38, 0x37, 0x61, // "GIF87a"
            0x01, 0x00, 0x01, 0x00,             // logical screen (1x1)
            0x00,                               // global color table flag = 0
            0x00,                               // background color index
            0x00,                               // aspect ratio
            // (minimal — no global color table because flag=0)
            0x3B                                // GIF trailer
        };

        using var form = BuildFileContent(gifBytes, "image.gif", "image/gif");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "GIF (image/gif) must be rejected with 422 — GIF is not in the allowed MIME type list; body: {0}", body);

        if (root.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(root, "successed", out var succeededProp);
            if (succeededProp.ValueKind != JsonValueKind.Undefined)
                succeededProp.GetBoolean().Should().BeFalse("body: {0}", body);
        }
    }

    // ===========================================================================
    // BE-TC-21: IDOR — two users upload different avatars; /Me for each returns only
    // their own avatarUrl. The endpoint is self-scoped via JWT; there is no id on the route.
    // ===========================================================================

    [Fact(DisplayName = "BE-TC-21: Two users upload different avatars; each /Me returns only their own avatarUrl (no IDOR)")]
    public async Task Upload_SelfScoped_NoIDOR_TwoUsers()
    {
        var tokenP1 = await RegisterParentAsync(UniqueEmail("idor-p1"));
        var tokenP2 = await RegisterParentAsync(UniqueEmail("idor-p2"));

        var pngBytes1 = BuildMinimalPng(); // same bytes but different users
        var pngBytes2 = BuildMinimalPng();

        // P1 uploads
        using var form1 = BuildFileContent(pngBytes1, "avatar1.png", "image/png");
        var (uploadResp1, uploadRoot1, uploadBody1) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form1, tokenP1);
        uploadResp1.StatusCode.Should().Be(HttpStatusCode.OK, "P1 upload prerequisite; body: {0}", uploadBody1);

        // P2 uploads
        using var form2 = BuildFileContent(pngBytes2, "avatar2.png", "image/png");
        var (uploadResp2, uploadRoot2, uploadBody2) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form2, tokenP2);
        uploadResp2.StatusCode.Should().Be(HttpStatusCode.OK, "P2 upload prerequisite; body: {0}", uploadBody2);

        // Get P1's avatarUrl from /Me
        var (meResp1, meRoot1, meBody1) = await SendAsync(_client, HttpMethod.Get, MeUrl, bearerToken: tokenP1);
        meResp1.StatusCode.Should().Be(HttpStatusCode.OK, "P1 /Me; body: {0}", meBody1);
        TryProp(meRoot1, "data", out var meData1).Should().BeTrue("body: {0}", meBody1);
        TryProp(meData1, "avatarUrl", out var avatarUrlProp1).Should().BeTrue("body: {0}", meBody1);
        var avatarUrlP1 = avatarUrlProp1.GetString();
        avatarUrlP1.Should().NotBeNullOrWhiteSpace("P1 avatarUrl must be non-null after upload; body: {0}", meBody1);

        // Get P2's avatarUrl from /Me
        var (meResp2, meRoot2, meBody2) = await SendAsync(_client, HttpMethod.Get, MeUrl, bearerToken: tokenP2);
        meResp2.StatusCode.Should().Be(HttpStatusCode.OK, "P2 /Me; body: {0}", meBody2);
        TryProp(meRoot2, "data", out var meData2).Should().BeTrue("body: {0}", meBody2);
        TryProp(meData2, "avatarUrl", out var avatarUrlProp2).Should().BeTrue("body: {0}", meBody2);
        var avatarUrlP2 = avatarUrlProp2.GetString();
        avatarUrlP2.Should().NotBeNullOrWhiteSpace("P2 avatarUrl must be non-null after upload; body: {0}", meBody2);

        // The URLs must be different objects (different object keys per user), confirming the
        // endpoint is self-scoped: there is no route id, so each upload only affects the caller.
        // Even if the byte content is identical, the S3 object key includes the user id.
        avatarUrlP1.Should().NotBe(avatarUrlP2,
            "P1's avatarUrl must be a DIFFERENT S3 key than P2's — each user's avatar is stored " +
            "under their own user-id prefixed key; urls are P1={0}, P2={1}", avatarUrlP1, avatarUrlP2);
    }

    // ===========================================================================
    // Supplemental: JPEG upload also works (exercises the JPEG magic-byte branch)
    // ===========================================================================

    [Fact(DisplayName = "Supplemental: POST with valid JPEG (FF D8 FF magic bytes) → 200/Successed=true")]
    public async Task Upload_ValidJpeg_Returns200()
    {
        var token = await RegisterParentAsync(UniqueEmail("jpeg-happy"));

        // Minimal JPEG: SOI (FF D8) + APP0 marker (FF E0) + minimal data
        // This is a real minimal JPEG that passes magic-byte detection.
        var jpegBytes = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, // SOI + APP0 marker
            0x00, 0x10,             // APP0 length = 16
            0x4A, 0x46, 0x49, 0x46, // "JFIF"
            0x00,                   // null terminator
            0x01, 0x01,             // version 1.1
            0x00,                   // pixel aspect ratio units
            0x00, 0x01,             // X density = 1
            0x00, 0x01,             // Y density = 1
            0x00, 0x00,             // thumbnail width/height = 0
            0xFF, 0xD9              // EOI
        };

        using var form = BuildFileContent(jpegBytes, "photo.jpg", "image/jpeg");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, AvatarUrl, form, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Valid JPEG upload must return 200; body: {0}", body);

        TryProp(root, "successed", out var succeeded).Should().BeTrue("body: {0}", body);
        succeeded.GetBoolean().Should().BeTrue("body: {0}", body);
    }
}

/// <summary>
/// xUnit collection fixture that owns the MinIO Testcontainer and a dedicated WebApplicationFactory.
/// The MinIO container is started ONCE per test collection (not once per test), and the factory
/// is configured to point MinIOConfiguration at the container's mapped port.
///
/// The "avatars" bucket is created in InitializeAsync via a direct SigV4-signed PUT request.
/// Because LearnexiaWebAppFactory is sealed we cannot subclass it; instead we build our own
/// WebApplicationFactory&lt;Program&gt; instance inline here and wire both Postgres and MinIO into it.
/// </summary>
public sealed class AvatarTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("Learnexia")
        .WithUsername("postgres")
        .WithPassword("testpassword")
        .Build();

    private IContainer? _minioContainer;
    private AvatarWebAppFactory? _factory;

    public AvatarWebAppFactory Factory => _factory!;

    public async Task InitializeAsync()
    {
        // Start both containers in parallel.
        await Task.WhenAll(_postgres.StartAsync(), StartMinIOAsync());
    }

    private async Task StartMinIOAsync()
    {
        _minioContainer = new ContainerBuilder()
            .WithImage("quay.io/minio/minio:latest")
            .WithCommand("server", "/data")
            .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
            .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(req => req
                    .ForPort(9000)
                    .ForPath("/minio/health/live")))
            .Build();

        await _minioContainer.StartAsync();

        var mappedPort = _minioContainer.GetMappedPublicPort(9000);

        // Create the "avatars" bucket via SigV4-signed PUT.
        await CreateBucketAsync("avatars", mappedPort);

        var minioEndpoint = $"localhost:{mappedPort}";
        _factory = new AvatarWebAppFactory(_postgres.GetConnectionString(), minioEndpoint);
    }

    private static async Task CreateBucketAsync(string bucket, int port)
    {
        const string accessKey = "minioadmin";
        const string secretKey = "minioadmin";
        const string region = "us-east-1";
        const string service = "s3";

        var endpoint = $"localhost:{port}";
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var canonicalUri = $"/{bucket}";
        var url = $"http://{endpoint}{canonicalUri}";

        var emptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var canonicalHeaders =
            $"host:{endpoint}\n" +
            $"x-amz-content-sha256:{emptyHash}\n" +
            $"x-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";

        var canonicalRequest = string.Join("\n",
            "PUT", canonicalUri, string.Empty, canonicalHeaders, signedHeaders, emptyHash);

        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var stringToSign = string.Join("\n",
            "AWS4-HMAC-SHA256", amzDate, credentialScope,
            ToHex(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        var signingKey = DeriveKey(secretKey, dateStamp, region, service);
        var signature = ToHex(HmacSha256(signingKey, stringToSign));

        var authHeader = $"AWS4-HMAC-SHA256 Credential={accessKey}/{credentialScope}, " +
                         $"SignedHeaders={signedHeaders}, Signature={signature}";

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Host = endpoint;
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", emptyHash);
        request.Headers.TryAddWithoutValidation("Authorization", authHeader);
        request.Content = new ByteArrayContent(Array.Empty<byte>());

        var response = await client.SendAsync(request);
        // 200 = created; 409 = BucketAlreadyOwnedByYou (acceptable).
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to create MinIO bucket '{bucket}': HTTP {(int)response.StatusCode} — {body}");
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (_minioContainer is not null) await _minioContainer.DisposeAsync();
        await _postgres.StopAsync();
    }

    // SigV4 helpers (self-contained — no dependency on internal production AwsV4Signer).
    private static byte[] DeriveKey(string secretKey, string dateStamp, string region, string service)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + secretKey), dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}

/// <summary>
/// Standalone WebApplicationFactory for avatar tests that wires BOTH the Postgres test container
/// AND the MinIO test container into the host. Cannot inherit from LearnexiaWebAppFactory (sealed),
/// so it re-implements the same DbContext overrides and rate-limiter bypass inline.
/// </summary>
public sealed class AvatarWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _postgresConnectionString;
    private readonly string _minioEndpoint;

    public AvatarWebAppFactory(string postgresConnectionString, string minioEndpoint)
    {
        _postgresConnectionString = postgresConnectionString;
        _minioEndpoint = minioEndpoint;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Override all DbContexts to use the test Postgres container.
            ReplaceDbContext<Learnexia.Modules.Identity.Infrastructure.Persistence.IdentityModuleDbContext>(
                services, _postgresConnectionString, "identity");
            ReplaceDbContext<Learnexia.Modules.Notifications.Infrastructure.Persistence.NotificationsDbContext>(
                services, _postgresConnectionString, "notifications");
            ReplaceDbContext<Learnexia.Modules.Learning.Infrastructure.Persistence.LearningDbContext>(
                services, _postgresConnectionString, "learning");

            // Neutralise rate limiter.
            services.Configure<AspNetCoreRateLimit.IpRateLimitOptions>(opt =>
            {
                opt.EnableEndpointRateLimiting = false;
                opt.GeneralRules = new List<AspNetCoreRateLimit.RateLimitRule>
                {
                    new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" }
                };
            });
        });

        // Override MinIOConfiguration to point at the Testcontainers MinIO container, and
        // override ConnectionStrings:Default to the Testcontainers Postgres so Hangfire (which reads
        // ConnectionStrings:Default via IConfiguration at DI resolution time) uses the container DB
        // rather than the real localhost:5432 — making this factory fully self-contained.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgresConnectionString,
                ["MinIOConfiguration:Endpoint"] = _minioEndpoint,
                ["MinIOConfiguration:AccessKey"] = "minioadmin",
                ["MinIOConfiguration:SecretKey"] = "minioadmin",
                ["MinIOConfiguration:UseSSL"] = "false",
                ["MinIOConfiguration:Region"] = "us-east-1",
                ["MinIOConfiguration:DefaultBucket"] = "avatars",
                ["MinIOConfiguration:DefaultUrlExpiryMinutes"] = "10080",
                ["MinIOConfiguration:MaxFileSize"] = "2097152",
                ["MinIOConfiguration:Enabled"] = "true",
            });
        });
    }

    /// <summary>Apply EF migrations and seed Identity roles + dev accounts.</summary>
    public async Task ApplyMigrationsAndSeedAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        await sp.GetRequiredService<Learnexia.Modules.Identity.Infrastructure.Persistence.IdentityModuleDbContext>()
            .Database.MigrateAsync();
        await sp.GetRequiredService<Learnexia.Modules.Notifications.Infrastructure.Persistence.NotificationsDbContext>()
            .Database.MigrateAsync();
        await sp.GetRequiredService<Learnexia.Modules.Learning.Infrastructure.Persistence.LearningDbContext>()
            .Database.MigrateAsync();

        await Learnexia.Modules.Identity.Api.IdentityModule.SeedAsync(sp);

        var userManager = sp.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Learnexia.Modules.Identity.Domain.Entities.User>>();
        var roleManager = sp.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Learnexia.Modules.Identity.Domain.Entities.Role>>();
        await Learnexia.Modules.Identity.Infrastructure.Persistence.Seed.UserSeeder.SeedBasicUserAsync(userManager, roleManager);
        await Learnexia.Modules.Identity.Infrastructure.Persistence.Seed.UserSeeder.SeedSuperAdminAsync(userManager, roleManager);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public new Task DisposeAsync() => base.DisposeAsync().AsTask();

    private static void ReplaceDbContext<TContext>(
        IServiceCollection services, string connectionString, string schema)
        where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<TContext>();
        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MigrationsHistoryTable("__EFMigrationsHistory", schema)
                    .MigrationsAssembly(typeof(TContext).Assembly.FullName)));
    }
}

/// <summary>
/// xUnit collection definition: all avatar tests share one MinIO container + WebApplicationFactory.
/// </summary>
[CollectionDefinition("AvatarIntegrationTests")]
public sealed class AvatarIntegrationTestsCollection : ICollectionFixture<AvatarTestFixture> { }
