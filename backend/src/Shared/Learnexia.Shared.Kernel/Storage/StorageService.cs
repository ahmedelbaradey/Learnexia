using System.Net;
using System.Net.Http.Headers;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace Learnexia.Shared.Kernel.Storage;

/// <summary>
/// MinIO implementation of <see cref="IStorageService"/> over a raw <see cref="HttpClient"/> against the
/// S3 path-style HTTP API, signed with hand-rolled AWS Signature V4 (<see cref="AwsV4Signer"/>). No MinIO
/// SDK. Avatar object keys are stored in User.AvatarUrl; the read path serves a freshly-computed presigned
/// GET URL (no network). All operations catch their own exceptions, log via ILoggerManager (Learnexia rule
/// — never ILogger&lt;T&gt;), and return failure results — they never throw to callers. On failure the
/// public ErrorMessage is a GENERIC string (internal detail is logged server-side, never surfaced).
/// </summary>
public class StorageService : IStorageService
{
    // Generic, non-leaking failure message. Internal exception detail is logged via ILoggerManager and
    // never assigned to a result. Callers map this to their own localized user-facing message.
    private const string GenericStorageError = "Storage operation failed.";

    private const string DefaultContentType = "application/octet-stream";

    private readonly HttpClient _httpClient;
    private readonly MinIOConfiguration _config;
    private readonly ILoggerManager _logger;

    public StorageService(HttpClient httpClient, IOptions<MinIOConfiguration> config, ILoggerManager logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
    }

    // Base URL (scheme + host:port) for the configured MinIO endpoint, e.g. "http://localhost:9000".
    private string BaseUrl => $"{(_config.UseSSL ? "https" : "http")}://{_config.Endpoint}";

    public async Task<StorageResult> UploadFileAsync(Stream content, string fileName, string contentType, long length, string bucketName, CancellationToken ct = default)
    {
        try
        {
            if (!_config.Enabled)
                return new StorageResult { Success = false, ErrorMessage = GenericStorageError };

            if (content is null || length == 0)
                return new StorageResult { Success = false, ErrorMessage = GenericStorageError };

            // Stream the upload body directly — no full-file buffer.
            // SigV4 UNSIGNED-PAYLOAD: set x-amz-content-sha256 to the literal "UNSIGNED-PAYLOAD"
            // and sign with that value instead of a body hash. MinIO and S3 both accept this when
            // Content-Length is known (which it always is here — callers pass file.Length).
            // This eliminates the OOM/DoS vector of materialising a 100 MB curriculum file in the
            // managed heap (BL-01 security finding #1, High).
            var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? DefaultContentType : contentType;

            var canonicalUri = AwsV4Signer.BuildCanonicalUri(bucketName, fileName);
            var url = $"{BaseUrl}{canonicalUri}";

            // UNSIGNED-PAYLOAD is already a constant in AwsV4Signer; reuse it as the payload hash
            // value passed to Sign() so the canonical request and Authorization header are consistent.
            const string unsignedPayloadHash = "UNSIGNED-PAYLOAD";

            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            var httpContent = new StreamContent(content);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue(resolvedContentType);
            // Set Content-Length explicitly from the caller-supplied length so HttpClient sends the
            // PUT with a known-length body (required by S3/MinIO path-style API; avoids chunked TE).
            httpContent.Headers.ContentLength = length;
            request.Content = httpContent;

            Sign(request, HttpMethod.Put.Method, canonicalUri, unsignedPayloadHash);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await SafeReadBody(response, ct);
                _logger.LogError(null, $"MinIO upload failed ({(int)response.StatusCode}) for {fileName}: {detail}");
                return new StorageResult { Success = false, ErrorMessage = GenericStorageError };
            }

            var previewUrl = await GetPreviewUrlAsync(fileName, bucketName, _config.DefaultUrlExpiryMinutes, ct);

            return new StorageResult
            {
                Success = true,
                FilePath = fileName,
                FileName = fileName,
                FileSize = length,
                ContentType = resolvedContentType,
                PreviewUrl = previewUrl
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error uploading {fileName} to {bucketName}");
            return new StorageResult { Success = false, ErrorMessage = GenericStorageError };
        }
    }

    public async Task<FileDownloadResult> DownloadFileAsync(string objectKey, string bucketName, CancellationToken ct = default)
    {
        try
        {
            if (!_config.Enabled)
                return new FileDownloadResult { Success = false, ErrorMessage = GenericStorageError };

            var canonicalUri = AwsV4Signer.BuildCanonicalUri(bucketName, objectKey);
            var url = $"{BaseUrl}{canonicalUri}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            Sign(request, HttpMethod.Get.Method, canonicalUri, AwsV4Signer.EmptyPayloadHash);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(null, $"MinIO download failed ({(int)response.StatusCode}) for {objectKey}");
                return new FileDownloadResult { Success = false, ErrorMessage = GenericStorageError };
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            return new FileDownloadResult
            {
                Success = true,
                FileBytes = bytes,
                FileName = objectKey,
                FileSize = bytes.LongLength,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? DefaultContentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error downloading {objectKey} from {bucketName}");
            return new FileDownloadResult { Success = false, ErrorMessage = GenericStorageError };
        }
    }

    public Task<string> GetPreviewUrlAsync(string objectKey, string bucketName, int expiryInMinutes = 60, CancellationToken ct = default)
    {
        try
        {
            // Graceful degradation: when storage is off, echo the object key unchanged (no network, no crypto).
            if (!_config.Enabled || string.IsNullOrWhiteSpace(objectKey))
                return Task.FromResult(objectKey);

            var canonicalUri = AwsV4Signer.BuildCanonicalUri(bucketName, objectKey);
            var expirySeconds = Math.Max(1, expiryInMinutes) * 60;

            var url = AwsV4Signer.BuildPresignedGetUrl(
                BaseUrl,
                canonicalUri,
                _config.Endpoint,
                DateTime.UtcNow,
                expirySeconds,
                _config.AccessKey,
                _config.SecretKey,
                _config.Region);

            return Task.FromResult(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error building preview URL for {objectKey}");
            // Never throw to callers — degrade to the raw key.
            return Task.FromResult(objectKey);
        }
    }

    public async Task<bool> FileExistsAsync(string objectKey, string bucketName, CancellationToken ct = default)
    {
        try
        {
            if (!_config.Enabled)
                return false;

            var canonicalUri = AwsV4Signer.BuildCanonicalUri(bucketName, objectKey);
            var url = $"{BaseUrl}{canonicalUri}";

            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            Sign(request, HttpMethod.Head.Method, canonicalUri, AwsV4Signer.EmptyPayloadHash);

            using var response = await _httpClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return false;

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unexpected error checking existence of {objectKey} in {bucketName}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> EnsureBucketAsync(string bucketName, CancellationToken ct = default)
    {
        try
        {
            if (!_config.Enabled)
            {
                _logger.LogWarn($"EnsureBucket: MinIO disabled — skipping ensure for '{bucketName}'.");
                return false;
            }

            // Canonical URI for a bucket HEAD/PUT: "/<bucketName>/"
            var canonicalUri = $"/{bucketName}/";
            var url          = $"{BaseUrl}{canonicalUri}";

            // Step 1: HEAD — check existence.
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            Sign(headRequest, HttpMethod.Head.Method, canonicalUri, AwsV4Signer.EmptyPayloadHash);
            using var headResponse = await _httpClient.SendAsync(headRequest, ct);

            if (headResponse.IsSuccessStatusCode)
            {
                _logger.LogInfo($"EnsureBucket: bucket '{bucketName}' already exists.");
                return true;
            }

            if (headResponse.StatusCode != System.Net.HttpStatusCode.NotFound &&
                headResponse.StatusCode != System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarn($"EnsureBucket: HEAD /{bucketName}/ → {(int)headResponse.StatusCode}. Attempting PUT.");
            }

            // Step 2: PUT — create the bucket.
            using var putRequest = new HttpRequestMessage(HttpMethod.Put, url);
            putRequest.Content = new ByteArrayContent(Array.Empty<byte>());
            Sign(putRequest, HttpMethod.Put.Method, canonicalUri, AwsV4Signer.Sha256Hex(Array.Empty<byte>()));

            using var putResponse = await _httpClient.SendAsync(putRequest, ct);

            if (putResponse.IsSuccessStatusCode)
            {
                _logger.LogInfo($"EnsureBucket: bucket '{bucketName}' created successfully.");
                return true;
            }

            var body = await SafeReadBody(putResponse, ct);
            _logger.LogError(null,
                $"EnsureBucket: failed to create bucket '{bucketName}' ({(int)putResponse.StatusCode}): {body}.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"EnsureBucket: unexpected error for bucket '{bucketName}'.");
            return false;
        }
    }

    // Sets Host/x-amz-date/x-amz-content-sha256 and the SigV4 Authorization header on a live request.
    private void Sign(HttpRequestMessage request, string method, string canonicalUri, string payloadHash)
    {
        var now = DateTime.UtcNow;
        var amzDate = AwsV4Signer.AmzDate(now);
        var dateStamp = AwsV4Signer.DateStamp(now);
        var host = _config.Endpoint;

        request.Headers.Host = host;
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);

        var authorization = AwsV4Signer.BuildAuthorizationHeader(
            method,
            canonicalUri,
            host,
            amzDate,
            dateStamp,
            payloadHash,
            _config.AccessKey,
            _config.SecretKey,
            _config.Region);

        request.Headers.TryAddWithoutValidation("Authorization", authorization);
    }

    private static async Task<string> SafeReadBody(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadAsStringAsync(ct); }
        catch { return string.Empty; }
    }
}
