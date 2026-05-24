# Security Audit — P1-12 BE-4 Avatar upload (HttpClient + hand-rolled AWS SigV4 MinIO adapter)

Branch: `feat/P1-12-avatar-minio` · Repo root: `e:\Wrokspace\Learnexia` · Date: 2026-05-24
Reviewer: security-auditor (defensive, report-only). **Context: self-hosted MinIO container — no AWS-hosted dependency.**

## Scope reviewed (files / endpoints)
- `…Identity.Infrastructure/Services/Storage/AwsV4Signer.cs` — SigV4 header + query presign.
- `…Identity.Infrastructure/Services/Storage/StorageService.cs` — HttpClient PUT/GET/HEAD/presign.
- `…Identity.Infrastructure/Services/Storage/MinIODependencies.cs` — typed HttpClient + DI.
- `…Identity.Application/Configurations/MinIOConfiguration.cs` — bound config.
- `…Identity.Application/Features/Account/Avatars/AvatarImageValidator.cs` — allow-list + magic bytes.
- `…Identity.Application/Features/Account/Commands/UploadAvatar/*` and `…/RemoveAvatar/*`.
- `…Identity.Application/Features/Account/Queries/GetMyProfile/GetMyProfileQueryHandler.cs`,
  `…/Authentications/Queries/GetMe/GetMeQueryHandler.cs` (read/presign path).
- `…Identity.Api/Controllers/AccountController.cs` (`POST`/`DELETE /api/Users/Account/Avatar`).
- `backend/src/Host/Learnexia.Host/appsettings.json` (`MinIOConfiguration`), Program.cs, ServiceExtensions.cs (CORS/rate limit), DependencyInjection.cs (JWT).
- `docker/docker-compose.yaml` (minio + minio-setup).

## Findings

| # | Severity | Issue | Location | Remediation |
|---|----------|-------|----------|-------------|
| 1 | Medium | **Raw storage error text returned to client.** Both handlers return `ServerError<T>` with a localized string (good), BUT `StorageService.UploadFileAsync`/`DownloadFileAsync` set `StorageResult.ErrorMessage = ex.Message` from the caught exception. The upload handler only surfaces a localized message, so the raw text is not currently returned to the client on the avatar path — however the contract leaks internals to any future caller and the message is logged. Confirm no handler ever maps `result.ErrorMessage` into a response. | `StorageService.cs:88, 125` | Don't store `ex.Message` in `ErrorMessage`; use a fixed generic string and keep the exception only in the logger. Treat `ErrorMessage` as client-safe by contract. |
| 2 | Medium | **Presigned URL lifetime is 7 days (10080 min).** A leaked profile/Me response (logs, browser history, referrer, shared screenshot) yields a 7-day window to fetch a child's avatar from the private bucket without auth. The read path re-presigns every call, so a short TTL costs nothing. | `MinIOConfiguration.cs:35`, `appsettings.json:48` | Reduce `DefaultUrlExpiryMinutes` to minutes/low hours (e.g. 15–60). The read path always re-mints, so freshness is preserved. Child-PII risk justifies the tighter bound. |
| 3 | Medium | **Stored object Content-Type is the client-declared MIME, not the verified format.** Upload sets `content.Headers.ContentType = file.ContentType`. Magic bytes are verified (good) and the format is constrained to png/jpeg/webp, so a script payload cannot masquerade as `text/html`. Residual risk: declared type may disagree with true bytes (e.g. declared `image/png` on a JPEG) and the bucket has no forced `Content-Disposition`. | `StorageService.cs:58-60`, `UploadAvatarCommandHandler.cs:77-78` | Set the stored Content-Type from the **detected** `AvatarImageType` (already computed), not `file.ContentType`. Optionally set `Content-Disposition: inline` is fine for these 3 raster types; SVG is correctly excluded. |
| 4 | Low | **Magic-byte check re-opens the stream; whole file buffered into memory after the size check.** Size is enforced in-handler before upload (good), but `UploadFileAsync` reads the entire `IFormFile` into a `MemoryStream`/`byte[]` (required to compute the SigV4 payload hash). Capped at 2 MB so bounded, but the multipart body is already buffered by MVC before the handler runs — the cap does not prevent a large body from being received. | `StorageService.cs:44-50`, `AvatarImageValidator.cs:39-50` | Add a per-endpoint `[RequestSizeLimit]` / `[RequestFormLimits(MultipartBodyLengthLimit=…)]` on `UploadAvatar` so an oversize body is rejected at the framework boundary before buffering, not just after. Acceptable as-is for MVP given the 200/min global rate limit. |
| 5 | Low | **No image re-encoding / decompression-bomb defense.** Magic bytes confirm the container but a 2 MB crafted PNG/WEBP can still be a decompression bomb when a downstream client renders it. Server never decodes the image, so server-side risk is low; risk is shifted to viewers. | `AvatarImageValidator.cs`, `UploadAvatarCommandHandler.cs` | Accepted risk for MVP (server doesn't decode). Note for follow-up: re-encode/normalize via an imaging lib if avatars are ever processed server-side. |
| 6 | Info | **JWT `Secret` is the `CHANGE_ME…` default in appsettings.json.** Pre-existing platform config, not introduced by this story, but it gates the `[Authorize]` that protects these endpoints. Flagging because avatar auth depends on it. | `appsettings.json:8` | Must be overridden from env/secret store in staging/prod (out of scope for this branch; track on the platform hardening story P1-13). |
| 7 | Info | **CORS falls back to `*` + `AllowCredentials()` only if `AllowedOrigins` is unset.** Current appsettings sets explicit localhost origins, so the dangerous wildcard+credentials combo is not active. Risk only if `AllowedOrigins` is ever blank in a deployed env. Pre-existing, not from this story. | `ServiceExtensions.cs:14-21` | Remove the `"*"` default or guard against `AllowCredentials()` when origins are `*`. Track on platform hardening. |
| 8 | Info | **`RequireHttpsMetadata = false` for JWT.** Pre-existing, dev-acceptable. Ensure it is `true` (or env-gated) in prod. | `DependencyInjection.cs:136` | Gate by environment. Out of scope for this branch. |

## Positive findings (verified, no action)
- **Self-scope / no IDOR:** upload/delete/read resolve the user from `ICurrentUserService.UserId` (JWT), never from route or body. No id parameter on the controller. `UploadAvatarCommand` carries only `IFormFile`. No mass-assignment surface (no client-set `IsActive`/role/audit fields).
- **AuthZ:** all four `AccountController` actions carry `[Authorize]`.
- **Object key is a server-generated GUID** (`Guid.NewGuid():N` + verified extension) — no user-controlled path → no path traversal / key injection. The extension comes from the detected format, not the filename.
- **SSRF:** target host is `_config.Endpoint` (config/env only). No request input reaches the URL host; the only request-derived value (object key on read) is the server-minted GUID stored in `User.AvatarUrl`.
- **Secret handling:** `SecretKey` is used only as the HMAC seed in `DeriveSigningKey`; it is never sent on the wire, never logged, and never appears in the presigned URL (only `X-Amz-Credential` = access-key-id + `X-Amz-Signature` appear, which is expected). The `Authorization` header / signature is not logged.
- **SigV4 correctness as a security property:** presigned URL carries a bounded `X-Amz-Expires`; signature derived via HMAC chain; canonicalization RFC3986-compliant.
- **Bucket is private:** `minio-setup` sets no anonymous policy; access is presigned-only.
- **Magic-byte validation present** in addition to the content-type allow-list; SVG is excluded (no inline-SVG XSS vector).
- **Server-side size cap** (`MaxFileSize`, 2 MB) enforced before upload.
- **Dependency scan:** `dotnet list … --vulnerable` → no vulnerable packages across all 24 projects.
- **Global IP rate limit** 200/min applies to the avatar endpoints (health probes whitelisted).

## Dependency-scan result
`dotnet list backend/Learnexia.Modular.sln package --vulnerable` → **no vulnerable packages** in any project. (Frontend `npm audit` not applicable — this is a backend-only branch.)

## Verdict: PASS-with-notes
No Critical or High findings → does not block the reviewer gate. Three Medium items (#1 raw error text in `StorageResult`, #2 7-day presign TTL, #3 stored Content-Type from declared MIME) are recommended fixes before this is exposed to production traffic; file back to `backend-feature`. Items #6/#7/#8 are pre-existing platform config, tracked separately (P1-13 hardening).

## Top must-fix (non-blocking, recommended)
1. Stop putting `ex.Message` into `StorageResult.ErrorMessage` (`StorageService.cs:88,125`).
2. Cut `DefaultUrlExpiryMinutes` from 10080 to ≤60 (child-PII exposure window).
3. Set stored object Content-Type from the detected format, not `file.ContentType`.
