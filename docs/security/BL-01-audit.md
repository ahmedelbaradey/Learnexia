# Security Audit — BL-01 Upload curriculum documents with metadata

> Defensive audit, 2026-06-23. Reviewer-gate input (R4). Critical/High findings BLOCK R2.
> Scope: file upload + admin authz + storage + DB-outbox. No code edited (audit-only).

## Scope reviewed (files / endpoints)

- `CurriculumDocumentsController` — `api/curriculum/documents` (POST upload, GET list, GET /{id} detail).
- `UploadCurriculumDocumentCommandHandler` (`...Infrastructure/Features/CurriculumDocuments/Commands/UploadCurriculumDocument/`).
- `CurriculumFileValidator` + `CurriculumFileType` (magic-byte detection / allow-list).
- `UploadCurriculumDocumentCommand` + `UploadCurriculumDocumentCommandValidator`.
- `ListCurriculumDocumentsQueryHandler`, `GetCurriculumDocumentQueryHandler`.
- DTOs: `CurriculumDocumentResponse`, `CurriculumDocumentListDto`, `CurriculumDocumentDto`.
- Entities/config: `CurriculumDocument`, `PipelineJob`.
- `StorageService` (`UploadFileAsync`, `EnsureBucketAsync`) + `CurriculumBucketEnsureService` + `CurriculumUploadConfiguration` + `MinIOConfiguration`.
- `AuthorizationPolicies.AdminOnly` registration (Identity `DependencyInjection.cs`).

## Findings

| # | Severity | Issue | Location | Remediation |
|---|---|---|---|---|
| 1 | High | **Full-file in-memory buffering defeats the "stream a 100 MB upload" requirement (OOM / DoS).** The handler comments and brief (Q3) claim the upload is streamed and "never buffered." But `IStorageService.UploadFileAsync` copies the entire `IFormFile` stream into a `MemoryStream` then `ToArray()`s it — so each upload allocates the full file in the managed heap (transiently ~2× during `ToArray()`), plus ASP.NET buffers the multipart body. With a 100 MB route cap, a handful of concurrent admin uploads (or one compromised/abused admin token) can exhaust the LOH / OOM the host. The route size cap limits per-request size but not aggregate memory pressure. | `backend/src/Shared/Learnexia.Shared.Kernel/Storage/StorageService.cs:49-54` (`MemoryStream` + `ms.ToArray()`); claim contradicted at `UploadCurriculumDocumentCommandHandler.cs:36,122-132` | Stream to storage without a full buffer: pass `IFormFile.OpenReadStream()` straight into the HTTP PUT body (`StreamContent`) using `x-amz-content-sha256: UNSIGNED-PAYLOAD` (or chunked SigV4) so the file is never fully materialized; or spool to a temp file. As a stop-gap, lower `MaxFileSizeBytes` for curriculum and bound upload concurrency. This is a shared-kernel fix (`StorageService`) — file back to `backend-feature`; note it also affects avatar uploads (smaller cap, lower impact). |
| 2 | Low | **Documented "Step 4" content-type/magic-byte consistency check is not implemented.** The handler's XML doc and inline "Step 5/Step 4" comments describe a cross-check that the *declared* content-type must agree with the *detected* type; the code never compares `file.ContentType` to `detected`. In practice this is safe — the object is stored under the **detected** type and a detected-type-derived extension (a spoofed declared CT is silently corrected, not trusted), mirroring the avatar handler. But the doc overstates the control. | `UploadCurriculumDocumentCommandHandler.cs:112-117` (comment vs. absent check) | Either implement the cross-check (reject when declared CT maps to a different `CurriculumFileType` than detected — tightens the DOCX-as-zip guard) or correct the comments to state that the detected type is authoritative and the declared CT is only an allow-list pre-filter. Behavioral fix preferred for the DOCX case (see #3). |
| 3 | Low | **DOCX validated only as a generic ZIP (`PK\x03\x04`); any zip masquerades as DOCX if the declared CT is also spoofed.** Magic-byte detection returns `Docx` for *any* ZIP-based file (ODS, XLSX, JAR, zip-bomb). The only secondary guard is the declared content-type, which a caller fully controls. Risk is bounded here because (a) the file is only stored, never parsed/decompressed in .NET (no zip-bomb expansion in this service), (b) the endpoint is AdminOnly, and (c) downstream Python parse is a separate hardening surface. Residual risk is a malformed/oversized zip reaching the Python parser (BL-02). | `CurriculumFileValidator.cs:100-104`; declared-CT guard not enforced (see #2) | Enforce the declared-CT↔detected cross-check for DOCX (require the OOXML word MIME) AND validate the central-directory/`[Content_Types].xml` presence, or defer strict OOXML validation to BL-02's Python parser with explicit decompression-ratio/size limits. Document the accepted residual risk if deferred. |
| 4 | Low | **Original client filename persisted and echoed verbatim (stored-XSS / spoofing seed for the future admin UI).** `CurriculumDocument.FileName = file.FileName` is stored unsanitized and returned in `CurriculumDocumentResponse` / `CurriculumDocumentDto` / list DTO. It is never used in the storage key (GUID-based — good) and is not logged. No active vector today (no FE), but a future admin console rendering `FileName` without escaping inherits a stored-XSS / homoglyph-spoofing risk. | `UploadCurriculumDocumentCommandHandler.cs:150`; DTOs `CurriculumDocumentDto.cs:18`, `CurriculumDocumentResponse.cs`, `CurriculumDocumentListDto.cs` | Strip path components and control chars and length-cap the filename on persist (display-only, so normalize aggressively); rely on the admin FE to output-encode. Note as a hard requirement in the deferred `BL-01-FE` contract. |
| 5 | Info | **Bucket-ensure failure is fail-soft (by design) — uploads then fail closed, not open.** `CurriculumBucketEnsureService` logs and continues on ensure failure; `StorageService.UploadFileAsync` returns a failure result (→ `ServerError`, document NOT persisted, no orphan outbox row). Correct posture. No credentials/secret-key/endpoint are logged (`EnsureBucketAsync` logs only the bucket name; `Sign` never logs the secret). Bucket is created with a bare PUT (no `x-amz-acl: public-read`) so it defaults to private. | `CurriculumBucketEnsureService.cs:43-50`, `StorageService.cs:191-245` | None — confirm in ops that the MinIO server default bucket policy is private (no anonymous read) and that `MinIOConfiguration:SecretKey`/`AccessKey` come from env/secret store in staging/prod (defaults are empty strings, not hardcoded secrets — good). |

## Verdict: PASS-with-notes

No Critical findings. **Finding #1 (full-file buffering vs. the 100 MB streaming requirement) is High** and contradicts an explicit acceptance constraint (Q3 / AC: "STREAM to storage; do not buffer in memory"). Because the fix lives in the shared `StorageService` (not BL-01 feature code) and the practical exposure is gated to authenticated admins with a 100 MB per-request cap, it is High (not Critical) — but per the gate rule **any High blocks R2** until fixed or explicitly risk-accepted by the lead.

### What is correctly secured (verified)
- **Authz:** class-level `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` covers all three actions (POST/GET list/GET detail); no per-action `[AllowAnonymous]` override. `AdminOnly` is genuinely registered as `RequireRole(Admin, SuperAdmin)` (Identity `DependencyInjection.cs:268-269`) — this is a real role check, not an unenforced generated permission policy. Anonymous → 401, non-admin → 403.
- **IDOR:** list/detail are unscoped-by-object **by design** — curriculum documents are global admin resources, not per-user/per-child data; every reader is already an admin. No child/user PII is involved. No object-level-auth gap.
- **Object key / path injection:** key is `Guid.NewGuid():N` + a fixed enum-derived extension — no user-controlled filename or path segment; no traversal; collisions practically impossible (no overwrite of other keys).
- **Magic-byte ordering:** signature detection runs BEFORE storage (handler steps 1-3 → step 5 upload); invalid files return 422, never 500. Storage stores the **detected** content-type, not the client-declared one.
- **Size enforcement is server-side and layered:** route `[RequestSizeLimit]` + `[RequestFormLimits]` AND a handler check on `file.Length` against config — not reliant on the client `Content-Length` header alone.
- **Outbox safety:** `PayloadJson` is server-built from only the GUID `object_key` + the enum-mapped `content_type`, serialized via `JsonSerializer` — no user-controlled string (notably NOT `FileName`) flows into the row the Python poller trusts; `JobType`/`Status` are server-set constants. No downstream injection seam.
- **Atomicity:** document + outbox job written inside an explicit `BeginTransactionAsync` (job-write failure rolls back the document) — no orphaned outbox row.
- **Error hygiene:** no `ServerError<T>(ex.Message)` anywhere — all catch blocks return a localized generic message; raw exception text is logged server-side only. `StorageService` returns a constant generic error and never surfaces MinIO internals.
- **Secrets:** JWT `CHANGE_ME…` default is guarded in Production/Staging (`GuardJwtSecret`); MinIO access/secret keys default to empty (env-supplied), never hardcoded, never logged.
- **Child privacy / AI:** no child PII and no AI-layer touch in BL-01 (curriculum documents are not child data; AI safety layer not invoked here).

## Dependency scan
`dotnet list backend/Learnexia.Modular.sln package --vulnerable` → **no vulnerable packages** across all projects (incl. `Learnexia.Modules.Curriculum.*`, `Curriculum.IntegrationTests`, `Learnexia.Shared.Kernel`). No new NuGet added (magic-byte checks are hand-rolled). `npm audit` not applicable — BL-01 is backend-only (FE deferred).

## Notes / accepted risks (lead to confirm)
- **Rate limiting:** there is no rate limiter on the upload endpoint — but this is a **platform-wide** gap (no `AddRateLimiter` anywhere in the Host), not introduced by BL-01. Recommend adding throttling on this expensive endpoint when platform rate limiting is introduced; out of BL-01 scope.
- **`RequireHttpsMetadata` / HSTS / CORS:** not touched by BL-01; no new transport/CORS surface added. Existing posture is owned by the Host config and out of this story's scope.
- Findings #2/#3/#4 are Low and may be risk-accepted for BL-01 if the lead notes the DOCX residual risk is owned by BL-02's Python parser and the filename-sanitization requirement is carried into the deferred `BL-01-FE` contract.

## Top must-fix
1. **(#1, High — blocks R2):** Stop buffering the whole file in `StorageService.UploadFileAsync`; stream `OpenReadStream()` into the PUT body (UNSIGNED-PAYLOAD or chunked SigV4). File back to `backend-feature`.

Security: FAIL — 1 blocking finding (High #1).

---

## Re-audit addendum — 2026-06-23 (fix for #1 High, #2 Low)

Scope: re-verify ONLY the `backend-feature` fix to finding #1 (and the #2 comment correction). No new audit of other findings. Files re-read: `StorageService.cs` (`UploadFileAsync`), `AwsV4Signer.cs`, `UploadCurriculumDocumentCommandHandler.cs`, `CurriculumDocumentsController.cs`. No code edited.

### #1 (High — full-file buffering OOM/DoS) → RESOLVED
- `UploadFileAsync` now wraps the caller stream in `StreamContent(content)` (`StorageService.cs:65`), sets `ContentLength = length` (`:69`), and PUTs it. **No `MemoryStream` / `ToArray()` / `ReadAllBytes` remains anywhere in the upload path.** The handler passes `file.OpenReadStream()` (`Handler:129`), which `HttpClient` streams to MinIO. The 100 MB managed-heap allocation per upload is gone; the OOM/DoS vector is closed.
- **UNSIGNED-PAYLOAD used correctly:** the literal `"UNSIGNED-PAYLOAD"` is passed as `payloadHash` to `Sign` (`:72`), set as `x-amz-content-sha256` (`:262`), and fed into `BuildAuthorizationHeader` — so the signed header value and the canonical-request payload-hash trailer match (consistent SigV4 unsigned-payload signing). No new auth weakness.
- **Body cannot exceed the declared/validated size:** `Content-Length` is set from `length` (= `file.Length`), forcing a fixed-length PUT (no chunked TE); `HttpClient` sends exactly `length` bytes. `file.Length` is itself bounded server-side by `[RequestSizeLimit(100MB)]` + `[RequestFormLimits(MultipartBodyLengthLimit=100MB)]` (`Controller:48-49`) and re-checked against `MaxFileSizeBytes` in the handler (`:96`). A client cannot stream more bytes than `length`, and the per-request ceiling is unchanged. No regression to the size bound.

### Residual note (Info, not blocking)
- With UNSIGNED-PAYLOAD the request body is no longer cryptographically hash-bound, so MinIO does not content-verify the bytes. Acceptable here: transport to MinIO is operator-controlled and the stored file is re-validated/parsed downstream (BL-02). If end-to-end integrity is later required, switch to streaming chunked SigV4 (per-chunk hashes) without reintroducing a full buffer.

### No regression (verified)
- **Access control:** class-level `AdminOnly` unchanged; upload still admin-gated.
- **Object-key handling:** key is still `{Guid:N}{extension}` (`Handler:127`); `BuildCanonicalUri` still RFC3986-encodes the key — no traversal, no user-controlled path.
- **Error handling:** null/empty content and `length == 0` short-circuit to generic failure (`:46-47`); non-2xx logs detail server-side and returns the constant `GenericStorageError` (`:75-79`); catch returns generic. No `ex.Message` / MinIO internals surfaced to clients. `SafeReadBody` detail is logged only.

### #2 (Low — misleading Step 4 comment) → RESOLVED
- Handler "Step 4" comment (`:112-119`) and XML doc (`:32`) now correctly state the detected magic-byte type is authoritative, the declared CT is an allow-list pre-filter only, and no CT↔detected cross-check is enforced — matches actual behavior. No behavioral change. (DOCX/zip residual risk #3 still deferred to BL-02 as previously accepted.)

### Updated verdict: PASS-with-notes
No Critical/High findings remain. #1 RESOLVED, #2 RESOLVED. Remaining open items are Low (#3, #4) and Info (#5) — previously eligible for lead risk-acceptance, unchanged by this fix. The R2 gate block is lifted.

Security: PASS — 0 blocking findings (was 1; High #1 resolved).
