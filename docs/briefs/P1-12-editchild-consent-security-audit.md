# Security Audit — P1-12 BE-9 (register country+consent) + BE-8 (edit-child)

Branch: `feat/P1-12-register-consent-editchild` · Module: Identity · Date: 2026-05-24
Auditor: security-auditor (defensive review only — no code edits)

## Scope reviewed (files / endpoints)

**Edit-child (BE-8) — `PUT api/Users/Parent/Update-Child`**
- `ParentController.cs` (controller gate)
- `Features/Family/Commands/UpdateChild/UpdateChildCommand.cs`
- `Features/Family/Commands/UpdateChild/UpdateChildCommandHandler.cs`
- `Features/Family/Commands/UpdateChild/UpdatedChildResponse.cs`
- `Features/Family/Validation/UpdateChildCommandValidator.cs`
- `Mapping/Family/FamilyProfile.cs` (User → UpdatedChildResponse map)
- `Infrastructure/Services/LinkParentStudentService.cs` (`IsLinkedAsync`)
- `Infrastructure/Services/CurrentUserService.cs` (JWT-claim resolution)

**Register + consent (BE-9) — register parent path**
- `RegisterParentCommand.cs`, `RegisterParentCommandHandler.cs`, `RegisterParentCommandValidator.cs`
- `Domain/Entities/User.cs` (`AcceptedTermsAtUtc`, `Nationality`)
- Migration `20260524024238_AddAcceptedTermsAtUtc.cs`

**Shared**
- `Shared.Kernel/Responses/BaseResponseHandler.cs` (`Forbidden<T>()`)
- `AppControllerBase.cs` (`NewResult` status mapping)

## Findings

| # | Severity | Issue | Location | Remediation |
|---|----------|-------|----------|-------------|
| 1 | Info (PASS) | **IDOR / family-scope correctly enforced.** Parent id is read from `ICurrentUserService.UserId` (JWT `Id` claim, `CurrentUserService.cs:19-26`), never from the body — there is no `ParentId` field on `UpdateChildCommand`. `IsLinkedAsync(parentId, ChildId)` (`LinkParentStudentService.cs:63-65`) is a parameterized EF `AnyAsync` scoped by **both** `ParentId` and `StudentId`, checked **before** any mutation. Null `UserId` → fail-closed `Unauthorized`. No write occurs on the not-linked path. | `UpdateChildCommandHandler.cs:43-55` | None — implemented as required. |
| 2 | Info (PASS) | **No TOCTOU gap of consequence.** Link check → `FindByIdAsync` → field mutation → `UpdateAsync` all run in one request with no privilege re-derivation from the body; the only mutable identity (`parentId`) is fixed from the token at the top. A link could theoretically be revoked between check and write, but that only allows an edit by a parent who was authorized microseconds earlier — not a cross-family escalation. Acceptable. | `UpdateChildCommandHandler.cs:50-74` | None. |
| 3 | Info (PASS) | **No mass-assignment.** Command exposes only `ChildId, FullName, Grade, Language, Country`. Handler mutates only `FullName`, `Grade`, `PreferredLanguage`, `Nationality`, `UpdatedAt`, `UpdatedBy`. Email/UserName, password, role, Id, `IsActive`, `IsDeleted`, and ParentStudent linkage are untouched. Audit fields are server-stamped (`UpdatedBy = parentId`, `UpdatedAt = UtcNow`). | `UpdateChildCommandHandler.cs:64-70`, `UpdateChildCommand.cs:11-18` | None. |
| 4 | Info (PASS) | **Consent integrity solid.** `AcceptedTermsAtUtc` is stamped server-side as `DateTime.UtcNow` (`Handler:63`) and is **not** a command field — the client cannot forge the timestamp. Validator requires `AcceptedTerms == true` (`Validator:40-41`), so `false`/absent (default `bool` = false) blocks registration via 422. | `RegisterParentCommandHandler.cs:63`, `RegisterParentCommandValidator.cs:40-41` | None. |
| 5 | Info (PASS) | **Role injection blocked on register.** `RegisterParentCommand` has no role/roles field; role is server-assigned `Roles.Parent` (`Handler:78`). An anonymous caller cannot mint an Admin or Student account through this path. | `RegisterParentCommand.cs:11-23`, `RegisterParentCommandHandler.cs:78` | None. |
| 6 | Info (PASS) | **`Forbidden<T>()` maps to a real 403 and does not leak existence.** `BaseResponseHandler.Forbidden` sets `HttpStatusCode.Forbidden`; `NewResult` default arm returns `ObjectResult { StatusCode = (int)Forbidden }` = 403 (`AppControllerBase.cs:31`). Both the not-linked branch and the not-found branch return the **identical** localized message `CannotEditChildNotInFamily` (`Handler:54, 61`), so a caller cannot distinguish "child exists but not mine" from "child does not exist" — no child-id enumeration oracle. | `BaseResponseHandler.cs:60-65`, `AppControllerBase.cs:22-32`, `UpdateChildCommandHandler.cs:54,61` | None. |
| 7 | Info (PASS) | **No raw-exception / Identity-error leakage on edit-child.** Catch-all returns localized `SystemErrorSavingData` via `ServerError` (no `ex.Message`); `UpdateAsync` failure logs the Identity error descriptions server-side but returns a generic localized message to the client. | `UpdateChildCommandHandler.cs:77-79, 85-89` | None. |
| 8 | Low | **`UpdatedChildResponse` (and `AddedChildResponse`) echo the child's `Email`.** For a children's platform this is child PII in the response body. It is only returned to the verified-linked parent (their own child), so exposure is scoped — but the FE list refresh likely does not need the child login/email. Pre-existing pattern (mirrors `AddedChildResponse`), not a regression. | `UpdatedChildResponse.cs:12`, `FamilyProfile.cs:28` | Confirm the FE actually consumes `Email`; if not, drop it from both child responses to minimize PII surface. Accept as-is if the FE needs to display the child login. |
| 9 | Low | **Raw Identity error descriptions still concatenated into the client message on the register path.** `RegisterParentCommandHandler.cs:74` returns `"{SystemErrorSavingData}: {errors}"` where `errors` is `result.Errors.Select(e => e.Description)`. Identity descriptions are user-facing (e.g. "Passwords must have…") and low-risk, but appending raw framework text to an anonymous-facing response is mild info disclosure and inconsistent with the hardened catch-all just below it. | `RegisterParentCommandHandler.cs:73-74` | Return the localized message only; log the joined `errors` server-side (as the edit-child handler already does at line 78). |
| 10 | Info | **No row-level guard that the linked target is a Student.** The handler trusts the `ParentStudent` link rather than re-checking `child` is in the Student role. This is safe **as long as** `ParentStudent.StudentId` only ever points at Student-role users (the link is only created by Add-Child/Link-Child). No exploit found; noting as an invariant the link table must uphold. | `UpdateChildCommandHandler.cs:57` | No action; keep the invariant enforced at link-creation time. |
| 11 | Info | **Admin/SuperAdmin role on the controller bypasses the family-link check only if they are themselves linked.** The gate `[Authorize(Roles="Parent,Admin,SuperAdmin")]` lets admins reach the endpoint, but the handler still requires `IsLinkedAsync(adminId, childId)` — an admin who is not a linked parent gets 403. So there is **no** blanket admin override here; an admin editing arbitrary children would need a separate admin endpoint. Intended and safe. | `ParentController.cs:20,40-43` | None. Confirm with lead that admins are *not* expected to edit unlinked children via this route (they currently cannot). |
| 12 | Info | **No per-endpoint rate limiting.** No `AddRateLimiter`/`RequireRateLimiting` anywhere in the backend. Register (anonymous, account-creation) and edit-child are not individually throttled. Platform-wide gap, not introduced by this change. | (no rate limiter registered) | Track as a platform follow-up (P1-13 hardening) — add rate limiting to anonymous auth endpoints (register/sign-in). |

## Other checks (clean)
- **Injection:** all data access via parameterized EF LINQ (`AnyAsync`, `FindByIdAsync`, `UpdateAsync`); no raw SQL, no dynamic `OrderBy` from user input, no file-path handling. PASS.
- **Secrets:** no hardcoded secrets in changed files. JWT default secret (`CHANGE_ME…`) is pre-existing and guarded by a fail-fast `GuardJwtSecret` that rejects the default in Production/Staging (`DependencyInjection.cs:113-119`) — not part of this change.
- **Transport/CORS/HSTS:** `app.UseHsts()` present; CORS uses `WithOrigins(...).AllowCredentials()` (not wildcard-with-credentials); `RequireHttpsMetadata=false` is the known dev posture. All pre-existing, outside this change's surface.
- **Logging:** uses `ILoggerManager` (per convention). Logs contain ids only (parentId, childId) — no tokens, no passwords, no email/PII. PASS.
- **Dependency scan:** `dotnet list backend/Learnexia.Modular.sln package --vulnerable` → **no vulnerable packages** across all projects.

## Verdict: PASS-WITH-FOLLOWUPS

No Critical or High findings. The headline IDOR/family-scope, mass-assignment, and consent-integrity requirements are all correctly implemented (parent id from JWT, link-checked before write, fail-closed, identical 403 for not-linked vs not-found). Two Low items (#8 child-email exposure, #9 raw Identity error text on register) and platform follow-ups (#12 rate limiting) are non-blocking.

## Notes / accepted risks
- #8 / #9 are Low — recommend fixing #9 (one-line: drop the `: {errors}` concat, log instead) in this PR for consistency with the edit-child handler; #8 depends on FE need.
- #12 (rate limiting) is a platform-wide gap; defer to P1-13 hardening.
- Migration is additive nullable column — no data-migration or destructive risk.
