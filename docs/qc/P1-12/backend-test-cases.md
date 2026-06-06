# P1-12 — Backend test cases (for `api-tester`)

> Target agent: **`api-tester`** — implement each as one HTTP integration test against the running API.
> Envelope: every response is `BaseResponse<T>` with the success flag spelled **`Successed`** (camelCase JSON), a localized `Message`, and `StatusCode` mapped to the HTTP status by `AppControllerBase.NewResult`. Assert **HTTP status + `Successed` + envelope shape**; for validation assert **422 + non-empty field error message** (not exact resx string — see README Q4).
> Auth: obtain JWTs by `POST /api/Users/Authentication/Register-Parent` then `Sign-In` (or reuse a seeded parent). "Self" endpoints resolve the user from the JWT — there is no id on the route/body.
> **BLOCKED** markers (infra/test-double gaps) are per README §4 — implement the unblocked cases, skip BLOCKED ones with the noted reason; do **not** delete them.

Legend — Priority: P0 blocks release · P1 should · P2 nice. Target agent is `api-tester` for every case.

---

## A. Profile read/update + enriched /Me  (P1-12a)

### BE-TC-01 — GET own profile returns enriched shape
- **Type:** functional · **Priority:** P0
- **Preconditions:** registered+signed-in parent `P1` (fullName, no phone/country/avatar yet).
- **Steps:** 1) `GET /api/Users/Account/Profile` with `P1` bearer.
- **Expected:** `200`; `Successed=true`; `result` is `AccountProfileResponse` with `fullName` set, `phone`/`country`/`avatarUrl` = `null`. No `email`/`id`/`role` field in the payload.
- **Traces to:** P1-12a "GET profile".

### BE-TC-02 — GET profile without JWT → 401
- **Type:** auth-authz · **Priority:** P0
- **Steps:** 1) `GET /api/Users/Account/Profile` with no Authorization header.
- **Expected:** `401`; `Successed=false`. No profile data leaked.
- **Traces to:** P1-12a `[Authorize]` self.

### BE-TC-03 — PUT profile updates fullName/phone/country and persists
- **Type:** persistence · **Priority:** P0
- **Preconditions:** parent `P1` signed in.
- **Steps:** 1) `PUT /api/Users/Account/Profile` body `{fullName:"Sara Updated", phone:"+201234567", country:"EG"}`. 2) `GET /api/Users/Account/Profile` again.
- **Expected:** PUT → `200`, `Successed=true`, returned `result` echoes the new values. GET → same values persisted (`phone="+201234567"`, `country="EG"`). Confirms Phone/Country migration landed.
- **Traces to:** P1-12a update + migration.

### BE-TC-04 — PUT profile without JWT → 401
- **Type:** auth-authz · **Priority:** P0
- **Steps:** 1) `PUT /api/Users/Account/Profile` body valid, no bearer.
- **Expected:** `401`; no write occurs.
- **Traces to:** P1-12a `[Authorize]` self.

### BE-TC-05 — GET /Me without JWT → 401
- **Type:** auth-authz · **Priority:** P0
- **Steps:** 1) `GET /api/Users/Me` no bearer.
- **Expected:** `401`; `Successed=false`.
- **Traces to:** enriched `/Me` `[Authorize]`.

### BE-TC-06 — /Me reflects profile fields after update
- **Type:** functional/persistence · **Priority:** P0
- **Preconditions:** run after BE-TC-03 (or set fields fresh) for parent `P1`.
- **Steps:** 1) `GET /api/Users/Me`.
- **Expected:** `200`; `result` includes `fullName`, `phone="+201234567"`, `country="EG"`, plus `id`, `roles` containing `Parent`, `avatarUrl` (null until an avatar is uploaded). Confirms `/Me` enriched per BE-2.
- **Traces to:** P1-12a enriched `/Me`.

### BE-TC-07 — /Me role/grade shape for a parent account
- **Type:** functional · **Priority:** P1
- **Steps:** 1) `GET /api/Users/Me` as parent.
- **Expected:** `roles` = `["Parent"]` (PascalCase); `grade` = null for a parent; `learningLanguage` present (DB default `"ar"`). Asserts no role/grade injection and stable enriched shape.
- **Traces to:** enriched `/Me`; product override (no teacher role).

### BE-TC-08 — PUT profile with empty fullName → 422
- **Type:** validation · **Priority:** P0
- **Steps:** 1) `PUT /api/Users/Account/Profile` body `{fullName:"", phone:"+201234567"}` with bearer.
- **Expected:** `422` (UnprocessableEntity); `Successed=false`; non-empty localized field error for `fullName`; no write.
- **Traces to:** ValidationBehavior → 422.

### BE-TC-09 — PUT profile with malformed phone / oversize fullName → 422
- **Type:** validation/boundary · **Priority:** P1
- **Steps:** 1) `PUT … Profile` `{fullName:"Ok", phone:"12-ab-xx"}` (fails `^\+?[0-9]{7,15}$`). 2) Separately, `fullName` = 101 chars (max 100), `country` = 101 chars (max 100).
- **Expected:** both → `422` with field errors (phone format; fullName/country too long). Confirms the regex + length bounds. A `phone` of exactly 15 digits and 7 digits should be **accepted** (boundary control).
- **Traces to:** validator rules (phone pattern, length bounds).

---

## B. Avatar upload / remove  (P1-12b) — file-upload security weighted

> Multipart `POST /api/Users/Account/Avatar` form field name **`file`**. Gates in order: presence/non-empty → size ≤ `MaxFileSize` (**2 MB = 2097152 bytes**) → declared content-type in {image/png, image/jpeg, image/webp} → **magic-byte** detection (PNG `89 50 4E 47 …`, JPEG `FF D8 FF`, WEBP `RIFF…WEBP`). Any failure → **422**.

### BE-TC-10 — Upload valid PNG → 200, presigned URL returned
- **Type:** functional · **Priority:** P0 · **BLOCKED-infra if MinIO down (README Q3)**
- **Preconditions:** parent `P1` signed in; MinIO reachable.
- **Steps:** 1) `POST …/Avatar` multipart with a real ≤2 MB PNG (valid magic bytes), `Content-Type: image/png`.
- **Expected:** `200`; `Successed=true`; `result.avatarUrl` is a non-empty presigned GET URL.
- **Traces to:** P1-12b upload.

### BE-TC-11 — Uploaded avatar surfaces in profile + /Me
- **Type:** persistence · **Priority:** P0 · **BLOCKED-infra if MinIO down**
- **Steps:** after BE-TC-10: 1) `GET /api/Users/Account/Profile`. 2) `GET /api/Users/Me`.
- **Expected:** both return a non-null `avatarUrl` (a freshly presigned URL). Confirms the stored object KEY is re-presigned on read.
- **Traces to:** "returns the URL in /Me + profile".

### BE-TC-12 — Valid JPEG and WEBP both accepted
- **Type:** functional/boundary · **Priority:** P1 · **BLOCKED-infra if MinIO down**
- **Steps:** 1) upload a real JPEG (`image/jpeg`). 2) upload a real WEBP (`image/webp`).
- **Expected:** both `200`, `Successed=true`. Confirms all three allowed formats.
- **Traces to:** type allow-list (png/jpeg/webp).

### BE-TC-13 — Storage failure does not leak raw error text
- **Type:** negative/security · **Priority:** P1 · **BLOCKED-infra (needs storage-fault injection)**
- **Steps:** 1) force a storage failure (MinIO unreachable / bad bucket) and `POST …/Avatar` with a valid image.
- **Expected:** `500`; `Successed=false`; `Message` is the localized `AvatarUploadFailed`, **not** a raw `ex.Message`/stack/`StorageResult.ErrorMessage`. (Audit Medium #1.)
- **Traces to:** safe-storage / no info-disclosure.

### BE-TC-14 — Reject SVG (declared image/svg+xml) → 422
- **Type:** validation/security · **Priority:** P0
- **Steps:** 1) `POST …/Avatar` with an SVG payload, `Content-Type: image/svg+xml`.
- **Expected:** `422`; `Successed=false`; localized `AvatarFileInvalidType`. SVG is deliberately excluded (inline-SVG XSS vector).
- **Traces to:** "no executable uploads"; audit positive finding (SVG excluded).

### BE-TC-15 — Spoofed content-type on non-image bytes → 422 (magic-byte gate)
- **Type:** negative/security · **Priority:** P0
- **Steps:** 1) `POST …/Avatar` with an HTML/script/EXE payload but `Content-Type: image/png`.
- **Expected:** `422`; localized `AvatarFileInvalidType`. The declared MIME passes the allow-list but magic-byte detection returns null → rejected. **This is the core content-confusion defense.**
- **Traces to:** "no executable uploads" + audit magic-byte verification.

### BE-TC-16 — Valid image bytes but disallowed declared MIME → 422
- **Type:** negative · **Priority:** P1
- **Steps:** 1) `POST …/Avatar` with real PNG bytes but `Content-Type: application/octet-stream` (or `text/html`).
- **Expected:** `422` at the content-type allow-list gate (before magic-byte). Confirms the declared-type allow-list is enforced independently.
- **Traces to:** type validation.

### BE-TC-17 — GIF / BMP / TIFF (real but unsupported) → 422
- **Type:** boundary/negative · **Priority:** P2
- **Steps:** 1) upload a genuine GIF with `Content-Type: image/gif`.
- **Expected:** `422` (gif not in allow-list, and no magic-byte branch for it). Confirms only png/jpeg/webp pass.
- **Traces to:** type allow-list completeness.

### BE-TC-18 — Oversize file (> 2 MB) → 422
- **Type:** boundary/security · **Priority:** P0 · **BLOCKED-infra if MinIO down (rejection is pre-storage; still testable without MinIO)**
- **Steps:** 1) `POST …/Avatar` with a valid PNG of `2097153` bytes (1 byte over `MaxFileSize`).
- **Expected:** `422`; localized `AvatarFileTooLarge`; no storage write. Boundary control: a `2097152`-byte valid image is accepted (BE-TC-10 territory).
- **Traces to:** "no oversized uploads" + size cap.

### BE-TC-19 — Empty / zero-byte / missing file → 422
- **Type:** negative/boundary · **Priority:** P1
- **Steps:** 1) `POST …/Avatar` multipart with `file` of length 0. 2) `POST …/Avatar` with no `file` part at all.
- **Expected:** both → `422`; localized `AvatarFileRequired`.
- **Traces to:** presence validation.

### BE-TC-20 — Upload without JWT → 401 (no IDOR surface)
- **Type:** auth-authz · **Priority:** P0
- **Steps:** 1) `POST …/Avatar` with a valid PNG, no bearer.
- **Expected:** `401`; no write. Confirms `[Authorize]`; there is no id on the route so the avatar is always the caller's own.
- **Traces to:** `[Authorize]` self only.

### BE-TC-21 — One user cannot affect another's avatar (self-scope)
- **Type:** auth-authz/IDOR · **Priority:** P0 · **BLOCKED-infra if MinIO down**
- **Preconditions:** parents `P1` and `P2`.
- **Steps:** 1) `P1` uploads an avatar. 2) `P2` uploads a different avatar. 3) `GET /Me` for each.
- **Expected:** each sees only their own `avatarUrl`; `P2`'s upload does not overwrite/expose `P1`'s. There is no parameter to target another user — confirms the resolved-from-JWT design.
- **Traces to:** self-only; audit "no IDOR".

### BE-TC-22 — Remove avatar clears AvatarUrl
- **Type:** functional/persistence · **Priority:** P0 · **BLOCKED-infra if MinIO down**
- **Steps:** after BE-TC-10: 1) `DELETE /api/Users/Account/Avatar`. 2) `GET /api/Users/Me`.
- **Expected:** DELETE → `200`, `Successed=true`. `/Me` → `avatarUrl` = null afterwards.
- **Traces to:** P1-12b remove.

### BE-TC-23 — Remove avatar without JWT → 401
- **Type:** auth-authz · **Priority:** P1
- **Steps:** 1) `DELETE /api/Users/Account/Avatar` no bearer.
- **Expected:** `401`.
- **Traces to:** `[Authorize]` self.

---

## C. Google OAuth sign-in  (P1-12c) — audience binding weighted

> `POST /api/Users/Authentication/Google-SignIn` `[AllowAnonymous]` body `{idToken}`. The validator pins audience to `GoogleAuth__ClientId`; invalid/empty → fail-closed. Happy paths need a real/fakeable token (README Q1).

### BE-TC-24 — Valid Google token creates a parent + issues our JWT
- **Type:** functional · **Priority:** P0 · **BLOCKED (README Q1 — needs test idToken / fake validator)**
- **Steps:** 1) `POST …/Google-SignIn` with a valid idToken for a NEW verified email.
- **Expected:** `200`; `Successed=true`; `result` is a `JwtAuthResponse` (same shape as password sign-in — access + refresh token). A new Parent-role account is created; subsequent `/Me` shows `roles=["Parent"]`.
- **Traces to:** P1-12c create + same JWT.

### BE-TC-25 — Valid Google token links to existing email account
- **Type:** functional · **Priority:** P1 · **BLOCKED (README Q1)**
- **Preconditions:** an existing password account with email `E`.
- **Steps:** 1) `POST …/Google-SignIn` with a valid Google token whose verified email = `E`.
- **Expected:** `200`; signs into the existing account (links by verified email); no duplicate account created.
- **Traces to:** P1-12c link.

### BE-TC-26 — Google-created (passwordless) account cannot password-login
- **Type:** auth-authz/negative · **Priority:** P1 · **BLOCKED (README Q1)**
- **Steps:** after a Google-created account exists: 1) `POST …/Sign-In` with that email + any password.
- **Expected:** sign-in fails (no `PasswordHash`); generic failure, not a 200. Confirms audit finding #4.
- **Traces to:** OAuth account integrity.

### BE-TC-27 — Garbage / malformed idToken → 401 fail-closed
- **Type:** negative/security · **Priority:** P0
- **Steps:** 1) `POST …/Google-SignIn` `{idToken:"not-a-real-jwt"}`.
- **Expected:** `401`; `Successed=false`; generic localized message; **no** account created/linked. Fail-closed.
- **Traces to:** audience/issuer/signature validation, fail-closed.

### BE-TC-28 — Wrong-audience idToken → 401
- **Type:** negative/security · **Priority:** P0 · **partially BLOCKED (needs a token signed for a different aud)**
- **Steps:** 1) `POST …/Google-SignIn` with a structurally valid Google token whose `aud` ≠ configured `GoogleAuth__ClientId`.
- **Expected:** `401`; rejected by audience pinning; no create/link. *If a real wrong-aud token can't be sourced, a garbage token (BE-TC-27) covers the fail-closed path; mark this BLOCKED with the reason.*
- **Traces to:** "audience validation against GoogleAuth__ClientId".

### BE-TC-29 — Empty idToken → 422 (validator) ; absent → 422
- **Type:** validation · **Priority:** P0
- **Steps:** 1) `POST …/Google-SignIn` `{idToken:""}`. 2) `{}` (no idToken).
- **Expected:** `422`; `Successed=false`; localized `GoogleIdTokenRequired`. (ICommand → ValidationBehavior runs before the handler.)
- **Traces to:** GoogleSignInValidator.

### BE-TC-30 — Empty/unset ClientId makes every token fail closed
- **Type:** negative/config · **Priority:** P2 · **BLOCKED (needs a test host with empty GoogleAuth__ClientId)**
- **Steps:** with `GoogleAuth__ClientId` unset/empty, 1) `POST …/Google-SignIn` with any token.
- **Expected:** `401`/rejected for every token (audience `[""]` rejects all). Documents the inert-but-safe posture (audit finding #1). Mark BLOCKED if the test host can't override config per-test.
- **Traces to:** OAuth config fail-closed.

### BE-TC-31 — Google sign-in cannot inject a role
- **Type:** auth-authz/security · **Priority:** P1 · **BLOCKED (README Q1 — depends on a successful sign-in)**
- **Steps:** after a successful Google sign-in (BE-TC-24): decode the issued JWT.
- **Expected:** role claim is server-assigned `Parent` only — never Admin/Student, never sourced from the Google payload.
- **Traces to:** "links/creates the parent account"; no privilege escalation; product override (no teacher role).

---

## D. Forgot / Reset password  (P1-12d) — anti-enumeration weighted

> `POST …/Forgot-Password {email}` and `POST …/Reset-Password {email, token, newPassword}`, both `[AllowAnonymous]`. Both must return **identical** generic responses regardless of account existence.

### BE-TC-32 — Forgot-Password for a KNOWN email → generic 200
- **Type:** functional/anti-enumeration · **Priority:** P0
- **Preconditions:** registered parent `P1`.
- **Steps:** 1) `POST …/Forgot-Password {email: P1.email}`.
- **Expected:** `200`; `Successed=true`; `result`/`Message` is the generic localized confirmation (a string, **not** a token).
- **Traces to:** P1-12d request reset.

### BE-TC-33 — Forgot-Password response never contains a token
- **Type:** security · **Priority:** P0
- **Steps:** 1) `POST …/Forgot-Password` for a known email. 2) inspect the full response body.
- **Expected:** body carries only the generic localized string; **no** reset token, no reset URL, no user id. (Token rides only the email link.)
- **Traces to:** "token never echoed in API response".

### BE-TC-34 — Forgot-Password for UNKNOWN / inactive email → identical generic 200
- **Type:** anti-enumeration · **Priority:** P0
- **Steps:** 1) `POST …/Forgot-Password {email: "nobody@nowhere.test"}`. 2) compare status + body to BE-TC-32.
- **Expected:** `200`; `Successed=true`; **byte-identical** generic response to the known-email case. No way to tell the email is unregistered.
- **Traces to:** "no account enumeration".

### BE-TC-35 — Forgot-Password timing parity (observational)
- **Type:** security/timing · **Priority:** P2 · **observational (README §3.2 — flaky to assert hard)**
- **Steps:** 1) time N requests for a known email vs N for an unknown email.
- **Expected:** record the delta; flag if known-email is consistently/substantially slower (audit Finding #1 — synchronous email send is a timing oracle). **Report-only**, do not fail the suite on timing.
- **Traces to:** anti-enumeration (timing dimension).

### BE-TC-36 — Reset-Password with unknown email → generic failure
- **Type:** anti-enumeration · **Priority:** P0
- **Steps:** 1) `POST …/Reset-Password {email:"nobody@nowhere.test", token:"x", newPassword:"Aa1!aa"}`.
- **Expected:** `400` generic localized `ResetPasswordInvalidLink`; `Successed=false`. Indistinguishable from a real email with a bad token.
- **Traces to:** "no enumeration" on reset.

### BE-TC-37 — Reset-Password with known email + bad token → same generic failure
- **Type:** negative/anti-enumeration · **Priority:** P0
- **Preconditions:** parent `P1`.
- **Steps:** 1) `POST …/Reset-Password {email: P1.email, token:"tampered-token", newPassword:"Aa1!aa"}`.
- **Expected:** `400`; **same** generic `ResetPasswordInvalidLink` message + status as BE-TC-36. Caller cannot distinguish "email exists, bad token" from "email doesn't exist".
- **Traces to:** token validation + no oracle.

### BE-TC-38 — Reset-Password with weak new password → same generic failure (no oracle)
- **Type:** negative/security · **Priority:** P1 · **needs a valid token (README Q2) — else assert via the bad-token generic path**
- **Steps:** 1) `POST …/Reset-Password {email: P1.email, token:<valid>, newPassword:"weak"}`.
- **Expected:** `400` generic `ResetPasswordInvalidLink` — a password-policy failure surfaces as the SAME generic failure (no distinct error reveals the token was valid). Mark BLOCKED for the valid-token portion per Q2; the generic-failure shape is still asserted via BE-TC-37.
- **Traces to:** password policy enforced + no oracle.

### BE-TC-39 — Reset-Password success sets a new usable password
- **Type:** functional/persistence · **Priority:** P0 · **BLOCKED (README Q2 — needs real token capture)**
- **Steps:** 1) request reset for `P1`, capture the real token. 2) `POST …/Reset-Password {email, token, newPassword:"NewPass1!"}`. 3) `POST …/Sign-In {email, password:"NewPass1!"}`.
- **Expected:** reset → `200` `Successed=true`; subsequent sign-in with the new password succeeds; sign-in with the OLD password fails.
- **Traces to:** set-new-password.

### BE-TC-40 — Reset token is single-use
- **Type:** negative/security · **Priority:** P1 · **BLOCKED (README Q2)**
- **Steps:** after a successful reset (BE-TC-39): 1) replay the SAME token to `Reset-Password` with a different new password.
- **Expected:** `400` generic failure — the token is consumed/invalidated after first use.
- **Traces to:** single-use token.

### BE-TC-41 — Successful reset invalidates other sessions
- **Type:** auth-authz/persistence · **Priority:** P0 · **BLOCKED (README Q2)**
- **Steps:** 1) sign `P1` in on "session A" (capture refresh token). 2) reset `P1`'s password via a valid token. 3) attempt `POST …/Refresh-Token` with session A's refresh token.
- **Expected:** the old refresh token is revoked (security stamp updated + refresh cache cleared) → refresh fails. Confirms audit checklist #3.
- **Traces to:** "invalidate other sessions".

### BE-TC-42 — Reset-Password / Forgot-Password with malformed email → 422
- **Type:** validation · **Priority:** P1
- **Steps:** 1) `POST …/Forgot-Password {email:"not-an-email"}`. 2) `POST …/Reset-Password {email:"not-an-email", token:"x", newPassword:"Aa1!aa"}`.
- **Expected:** both → `422` (format validation, ICommand). Note (audit #3): this 422 vs the 200/400 generic is a format divergence, **not** enumeration (depends only on string shape, not account existence) — acceptable; documented.
- **Traces to:** validators (email format).

### BE-TC-43 — Reset token absent from any client-visible surface
- **Type:** security · **Priority:** P1 · **partial — log assertion not coverable by HTTP harness (gap G1)**
- **Steps:** 1) run the forgot→reset flow; inspect ALL HTTP responses in the flow.
- **Expected:** no response body/header at any step contains the reset token. *Log-scrubbing is out of HTTP-harness reach — defer the log dimension to the security audit's code grep (already PASS); assert only the response-side here.*
- **Traces to:** "token never logged" (response-boundary portion).

---

## E. Register — country + terms consent  (P1-12f)

### BE-TC-52 — Register stores country, reflected in /Me + profile
- **Type:** functional/persistence · **Priority:** P0
- **Steps:** 1) `POST …/Register-Parent {email, password:"Aa1!aa", fullName:"Sara", country:"EG", acceptedTerms:true}`. 2) sign in. 3) `GET /api/Users/Me` and `GET …/Account/Profile`.
- **Expected:** register → `200` `Successed=true` (JwtAuthResponse). `/Me` and profile both show `country="EG"`. Confirms country persisted on `Nationality`.
- **Traces to:** P1-12f country stored + reflected.

### BE-TC-53 — Register with country omitted → 200 (country optional)
- **Type:** functional/boundary · **Priority:** P1
- **Steps:** 1) `POST …/Register-Parent {email, password, acceptedTerms:true}` (no country).
- **Expected:** `200`; account created; `/Me` shows `country=null`. Country is optional.
- **Traces to:** country optional.

### BE-TC-54 — Register with oversize country (>100) → 422
- **Type:** validation/boundary · **Priority:** P2
- **Steps:** 1) `POST …/Register-Parent {…, country:<101 chars>, acceptedTerms:true}`.
- **Expected:** `422`; localized `ProfileCountryTooLong`.
- **Traces to:** country length bound.

### BE-TC-55 — Register with acceptedTerms=false → 422
- **Type:** validation/security · **Priority:** P0
- **Steps:** 1) `POST …/Register-Parent {email, password:"Aa1!aa", acceptedTerms:false}`.
- **Expected:** `422`; `Successed=false`; localized `TermsConsentRequired`; **no** account created. (COPPA — consent is mandatory.)
- **Traces to:** P1-12f consent required.

### BE-TC-56 — Register with acceptedTerms absent → 422 (default false)
- **Type:** validation/security · **Priority:** P0
- **Steps:** 1) `POST …/Register-Parent {email, password:"Aa1!aa"}` (no `acceptedTerms` field).
- **Expected:** `422` `TermsConsentRequired` — the `bool` default is `false`, so absence is rejected, not silently accepted. Confirms consent timestamp is never stamped without explicit accept.
- **Traces to:** consent integrity (audit finding #4).

### BE-TC-57 — Register cannot inject a role / create a non-parent
- **Type:** auth-authz/security · **Priority:** P0
- **Steps:** 1) `POST …/Register-Parent` with an extra unmapped `{"roles":["Admin"]}` (and `{"role":"Student"}`) in the body alongside valid fields.
- **Expected:** `200` but the created account's role is **Parent only** (extra fields ignored — no Roles field on the command). `/Me` → `roles=["Parent"]`. No Admin/Student account minted through this anonymous path.
- **Traces to:** product override (no teacher role, no student self-register); mass-assignment block (audit finding #5).

### BE-TC-58 — Register duplicate email → 422
- **Type:** negative/validation · **Priority:** P1
- **Steps:** 1) register `email=E` (success). 2) register `email=E` again with `acceptedTerms:true`.
- **Expected:** second → `422`; localized `ProfileDuplicateEmail`; no second account. (`BeUniqueEmail` async rule.) Asserts no account-takeover via re-register.
- **Traces to:** unique-email validation.

---

## F. Edit / update child  (P1-12e) — family-scope / IDOR weighted

> `PUT /api/Parent/Update-Child` `[Authorize(Roles=Parent,Admin,SuperAdmin)]` body `{childId, fullName, grade, language, country}`. Parent id is from the JWT (no `parentId` in body). `IsLinkedAsync` checked before any write; not-linked and not-found both return the SAME 403.

### BE-TC-44 — Parent edits own child → 200, returns updated child
- **Type:** functional/persistence · **Priority:** P0
- **Preconditions:** parent `P1` with linked child `C1` (Add-Child first).
- **Steps:** 1) `PUT …/Update-Child {childId: C1.id, fullName:"Omar Edited", grade:4, language:"en", country:"EG"}`. 2) `GET /api/Parent/My-Children`.
- **Expected:** PUT → `200` `Successed=true`; `result` is `UpdatedChildResponse` with the new values. `My-Children` reflects the update. (Email/login unchanged — out of scope.)
- **Traces to:** P1-12e update + returns updated child.

### BE-TC-45 — Edit-child without JWT → 401
- **Type:** auth-authz · **Priority:** P0
- **Steps:** 1) `PUT …/Update-Child` valid body, no bearer.
- **Expected:** `401`; no write.
- **Traces to:** controller `[Authorize]`.

### BE-TC-47 — Edit-child invalid grade (0 / 7) → 422
- **Type:** validation/boundary · **Priority:** P0
- **Steps:** 1) `PUT …/Update-Child {childId:C1, fullName:"x", grade:7, language:"en", country:"EG"}`. 2) `grade:0`.
- **Expected:** both → `422`; localized `GradeOutOfRange` (valid range 1–6 inclusive). Boundary control: `grade:1` and `grade:6` accepted.
- **Traces to:** ValidationBehavior shape-only (grade range).

### BE-TC-48 — Edit-child invalid language code → 422
- **Type:** validation · **Priority:** P1
- **Steps:** 1) `PUT …/Update-Child {…, language:"fr"}` (only `ar`/`en` allowed).
- **Expected:** `422`; localized `InvalidLanguageCode`.
- **Traces to:** language whitelist.

### BE-TC-49 — Edit-child empty fullName / missing childId / empty country → 422
- **Type:** validation · **Priority:** P1
- **Steps:** 1) `fullName:""`. 2) `childId:0`. 3) `country:""`.
- **Expected:** each → `422` with the corresponding field error (required / `ChildId>0` / required).
- **Traces to:** validator rules.

### BE-TC-50 — Cross-family edit (another parent's child) → 403, no write
- **Type:** auth-authz/IDOR · **Priority:** P0
- **Preconditions:** parent `P1` with child `C1`; parent `P2` with child `C2`.
- **Steps:** as `P2`, 1) `PUT …/Update-Child {childId: C1.id, fullName:"Hijack", grade:3, language:"ar", country:"SA"}`. 2) as `P1`, `GET /My-Children`.
- **Expected:** `403`; `Successed=false`; localized `CannotEditChildNotInFamily`. `C1` is **unchanged** for `P1`. Parent id comes from the JWT — `P2` cannot target `P1`'s child even by guessing the id.
- **Traces to:** family-scope authz (own child only); audit finding #1/#6.

### BE-TC-51 — Edit non-existent child → identical 403 (no id-enumeration oracle)
- **Type:** auth-authz/security · **Priority:** P0
- **Steps:** as `P1`, 1) `PUT …/Update-Child {childId: 999999999, …valid…}`.
- **Expected:** `403` with the **same** `CannotEditChildNotInFamily` message + status as the not-linked case (BE-TC-50). A caller cannot distinguish "child exists but not mine" from "child does not exist" → no child-id enumeration. (Audit finding #6.)
- **Traces to:** identical 403 not-linked vs not-found.

---

## Notes for the implementer
- Use the `BaseResponse<T>` JSON shape: assert `successed` (lowercase in JSON), `message`, `result`, and the HTTP status code — both must agree (e.g. a 422 body carries `successed:false`).
- For multipart avatar tests, the form field name is **`file`** (matches the `IFormFile file` action parameter).
- Seed parents/children via the API (`Register-Parent` → `Sign-In` → `POST /api/Parent/Add-Child`); name entities `P1`, `P2`, `C1`, `C2` as referenced above so cases are deterministic and cross-referenceable.
- BLOCKED cases: implement as skipped/pending tests carrying the README reason in a comment — do not delete them; they light up once the lead resolves Q1/Q2/Q3.
