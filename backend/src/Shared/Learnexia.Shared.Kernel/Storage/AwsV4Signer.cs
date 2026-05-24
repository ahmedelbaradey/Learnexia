using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Learnexia.Shared.Kernel.Storage;

/// <summary>
/// Hand-rolled AWS Signature V4 helper for the MinIO S3 HTTP API (path-style, service "s3").
/// Implements both flavours used by the storage adapter:
///   - Authorization-header signing for live PUT/GET/HEAD requests.
///   - Query-string presigning for read-only presigned GET URLs (no network call).
/// No AWS/MinIO SDK is used — only HttpClient (caller) + System.Security.Cryptography (here).
/// All literals here are S3/SigV4 protocol constants (algorithm name, header names, terminators),
/// not user-facing text.
/// </summary>
internal static class AwsV4Signer
{
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Service = "s3";
    private const string Request = "aws4_request";
    private const string EmptyBodySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    /// <summary>SHA256("") in lowercase hex — the payload hash for empty-body GET/HEAD requests.</summary>
    public static string EmptyPayloadHash => EmptyBodySha256;

    /// <summary>UTC timestamp in the AWS x-amz-date format (yyyyMMddTHHmmssZ).</summary>
    public static string AmzDate(DateTime utcNow) => utcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

    /// <summary>UTC date stamp in the credential-scope format (yyyyMMdd).</summary>
    public static string DateStamp(DateTime utcNow) => utcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    /// <summary>Lowercase-hex SHA256 of the given payload bytes.</summary>
    public static string Sha256Hex(byte[] payload) => ToHex(SHA256.HashData(payload));

    /// <summary>
    /// Builds the SigV4 Authorization header value for a live (header-signed) request. The caller must
    /// set Host, x-amz-date and x-amz-content-sha256 on the request with the SAME values passed here.
    /// </summary>
    public static string BuildAuthorizationHeader(
        string method,
        string canonicalUri,
        string host,
        string amzDate,
        string dateStamp,
        string payloadHash,
        string accessKey,
        string secretKey,
        string region)
    {
        // Canonical headers (lowercased names, sorted, "name:value\n"). We sign host + the two x-amz headers.
        var canonicalHeaders =
            $"host:{host}\n" +
            $"x-amz-content-sha256:{payloadHash}\n" +
            $"x-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";

        var canonicalRequest = string.Join("\n",
            method,
            canonicalUri,
            string.Empty, // canonical query string (none for these requests)
            canonicalHeaders,
            signedHeaders,
            payloadHash);

        var credentialScope = $"{dateStamp}/{region}/{Service}/{Request}";
        var stringToSign = string.Join("\n",
            Algorithm,
            amzDate,
            credentialScope,
            ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        var signingKey = DeriveSigningKey(secretKey, dateStamp, region);
        var signature = ToHex(HmacSha256(signingKey, stringToSign));

        return $"{Algorithm} Credential={accessKey}/{credentialScope}, " +
               $"SignedHeaders={signedHeaders}, Signature={signature}";
    }

    /// <summary>
    /// Builds a fully-qualified presigned GET URL using query-string SigV4. Pure computation — no
    /// network call. Signed headers = "host" only; payload hash = UNSIGNED-PAYLOAD.
    /// </summary>
    public static string BuildPresignedGetUrl(
        string baseUrl,
        string canonicalUri,
        string host,
        DateTime utcNow,
        int expirySeconds,
        string accessKey,
        string secretKey,
        string region)
    {
        var amzDate = AmzDate(utcNow);
        var dateStamp = DateStamp(utcNow);
        var credentialScope = $"{dateStamp}/{region}/{Service}/{Request}";
        var credential = $"{accessKey}/{credentialScope}";

        // Query params MUST be sorted by URI-encoded key; both keys and values are URI-encoded.
        var query = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Amz-Algorithm"] = Algorithm,
            ["X-Amz-Credential"] = credential,
            ["X-Amz-Date"] = amzDate,
            ["X-Amz-Expires"] = expirySeconds.ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = "host"
        };

        var canonicalQuery = string.Join("&", query.Select(kv =>
            $"{UriEncode(kv.Key, encodeSlash: true)}={UriEncode(kv.Value, encodeSlash: true)}"));

        var canonicalHeaders = $"host:{host}\n";
        const string signedHeaders = "host";

        var canonicalRequest = string.Join("\n",
            "GET",
            canonicalUri,
            canonicalQuery,
            canonicalHeaders,
            signedHeaders,
            UnsignedPayload);

        var stringToSign = string.Join("\n",
            Algorithm,
            amzDate,
            credentialScope,
            ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        var signingKey = DeriveSigningKey(secretKey, dateStamp, region);
        var signature = ToHex(HmacSha256(signingKey, stringToSign));

        return $"{baseUrl}{canonicalUri}?{canonicalQuery}&X-Amz-Signature={signature}";
    }

    /// <summary>
    /// RFC3986 URI-encoding as required by SigV4: unreserved chars (A-Z a-z 0-9 - _ . ~) pass through;
    /// everything else is percent-encoded with uppercase hex. "/" is encoded only when requested
    /// (object-key path segments keep their slashes; query keys/values encode slashes).
    /// </summary>
    public static string UriEncode(string value, bool encodeSlash)
    {
        var sb = new StringBuilder(value.Length * 2);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
                c == '-' || c == '_' || c == '.' || c == '~')
            {
                sb.Append(c);
            }
            else if (c == '/' && !encodeSlash)
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Builds a path-style canonical URI for an object: "/{bucket}/{encodedKey}". Each key path
    /// segment is RFC3986-encoded but the "/" separators are preserved.
    /// </summary>
    public static string BuildCanonicalUri(string bucket, string objectKey)
        => $"/{UriEncode(bucket, encodeSlash: true)}/{UriEncode(objectKey, encodeSlash: false)}";

    private static byte[] DeriveSigningKey(string secretKey, string dateStamp, string region)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + secretKey), dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, Service);
        return HmacSha256(kService, Request);
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
