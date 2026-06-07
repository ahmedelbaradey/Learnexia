# QC Test Plan — P1-12 Web account backend (Batch 2) — BACKEND ONLY

> Story: [`user-stories/Phase-1-Foundation/P1-12-web-account-backend-batch2.md`](../../../user-stories/Phase-1-Foundation/P1-12-web-account-backend-batch2.md)
> Brief/Plan: [`tasks/Backend/Phase-1-Foundation/P1-12-BE.md`](../../../tasks/Backend/Phase-1-Foundation/P1-12-BE.md)
> Security audits mined: `docs/briefs/P1-12-avatar-minio-security-audit.md`, `…-password-reset-…`, `…-google-signin-…`, `…-editchild-consent-…`
> QC architect: design-only. No test code, no execution, no feature edits. Implementation belongs to `api-tester`.

## 1. Summary

Backend QC pass for the P1-12 Batch-2 account surface: **profile read/update**, **enriched `/Me`**, **avatar upload/remove (MinIO)**, **Google OAuth sign-in**, **forgot/reset password**, **register country + terms consent**, and **edit-child** (family-scoped). All endpoints live in the **Identity** module except edit-child, which was relocated to the **Parent** module (`PUT /api/Parent/Update-Child`).

This run is **backend-only** by lead scope — no `frontend-test-cases.md` is produced. Frontend (P1-12-FE) ships separately and is covered by `frontend-e2e-tester` if/when the lead scopes it.

### Endpoints under test
| Method + Route | Auth | Surface |
|---|---|---|
| `GET /api/Users/Account/Profile` | `[Authorize]` self | profile read |
| `PUT /api/Users/Account/Profile` | `[Authorize]` self | profile update (ICommand → 422) |
| `GET /api/Users/Me` | `[Authorize]` self | enriched Me (fullName/phone/country/avatarUrl) |
| `POST /api/Users/Account/Avatar` | `[Authorize]` self · multipart | avatar upload (type/size/magic-byte → 422) |
| `DELETE /api/Users/Account/Avatar` | `[Authorize]` self | avatar remove (clears AvatarUrl) |
| `POST /api/Users/Authentication/Google-SignIn` | `[AllowAnonymous]` | OAuth idToken → JWT |
| `POST /api/Users/Authentication/Forgot-Password` | `[AllowAnonymous]` | reset request (anti-enumeration) |
| `POST /api/Users/Authentication/Reset-Password` | `[AllowAnonymous]` | set-new (token, generic failure) |
| `POST /api/Users/Authentication/Register-Parent` | `[AllowAnonymous]` | register + country + consent |
| `PUT /api/Parent/Update-Child` | `[Authorize(Roles=Parent,Admin,SuperAdmin)]` family-scope | edit child |

### Counts
- **Total cases:** 58 (all backend, target agent `api-tester`).
- **By priority:** **P0 = 31**, **P1 = 21**, **P2 = 6**.
- **By surface:** Profile/Me 9 · Avatar 13 · Google OAuth 8 · Forgot/Reset password 13 · Register consent 7 · Edit-child 8.
- **By type (weighted to security per lead ask):** auth-authz/IDOR 11 · validation 12 · negative 9 · file-upload-security 8 · anti-enumeration 5 · persistence 6 · functional 7.

## 2. Coverage matrix — acceptance criterion → case IDs

| Child story | Acceptance criterion | Case IDs | Covered |
|---|---|---|---|
| **P1-12a** profile | GET + PUT profile (fullName/phone/country); BaseResponse; `[Authorize]` self | BE-TC-01, 02, 03, 04 | yes |
| P1-12a | `/Me` enriched (fullName/phone/country/avatarUrl) | BE-TC-05, 06, 07 | yes |
| P1-12a | Migration: Phone/Country on User (observable via persistence) | BE-TC-03, 06 | yes (behaviorally) |
| P1-12a | Validation via ValidationBehavior; en/ar localized → 422 | BE-TC-08, 09 | yes |
| **P1-12b** avatar | Upload (type/size validation, safe storage) + remove; sets/clears AvatarUrl; URL in Me + profile | BE-TC-10, 11, 12, 13, 22, 23 | yes |
| P1-12b | No executable/oversized uploads (file-storage decision) | BE-TC-14, 15, 16, 17, 18, 19 | yes |
| P1-12b | `[Authorize]` self only (no IDOR) | BE-TC-20, 21 | yes |
| **P1-12c** OAuth | Google idToken → same JWT/refresh as password login; link/create parent | BE-TC-24, 25, 26 | yes |
| P1-12c | Audience pinned to ClientId; invalid/unverified token rejected (fail-closed) | BE-TC-27, 28, 29, 30 | yes |
| P1-12c | No role injection; role server-assigned Parent | BE-TC-31 | yes |
| **P1-12d** reset | Request reset, **no account enumeration** | BE-TC-32, 33, 34, 35 | yes |
| P1-12d | Set-new (token validation, password policy, invalidate other sessions) | BE-TC-36, 37, 38, 39, 40, 41 | yes |
| P1-12d | Generic failure for bad email / bad-expired token (no oracle) | BE-TC-36, 37, 38, 42 | yes |
| P1-12d | Token never echoed in API response / never logged | BE-TC-33, 43 | yes (response-side; log assertion partial — see gap G1) |
| **P1-12e** edit-child | Update command (fullName/grade/language/country); **family-scope authz** (own child only) | BE-TC-44, 45, 50, 51 | yes |
| P1-12e | Returns updated child; ValidationBehavior shape-only | BE-TC-44, 47, 48, 49 | yes |
| P1-12e | IDOR / cross-family blocked; identical 403 not-linked vs not-found | BE-TC-50, 51 | yes |
| **P1-12f** register | `country` accepted, validated, stored; reflected in Me/profile | BE-TC-52, 53, 54 | yes |
| P1-12f | Terms-consent stored (bool + timestamp); `AcceptedTerms=false`/absent → 422 | BE-TC-55, 56 | yes |
| P1-12f | Role injection blocked (no Roles field) | BE-TC-57 | yes |
| **Product overrides** | No teacher role / no student self-register reachable via these paths | BE-TC-58 | yes |

**Coverage verdict: every acceptance criterion has at least one P0/P1 case. No uncovered criterion.** One partial — see gap G1.

### Gaps / partials
- **G1 (partial, documented):** "token never **logged**" (reset) is asserted only at the **response** boundary (BE-TC-33, BE-TC-43 confirm the token is not echoed in the body and reset works). A true log-scrubbing assertion needs log capture, which the HTTP integration harness does not expose. The security audit already verified by code grep that the token is logged nowhere. BE-TC-43 is marked **partial — see note**; recommend the lead accept the audit's grep as the authority for the log path.

## 3. Risk notes (where cases are weighted, and why)

Per the lead's instruction the catalog is weighted toward **security**, guided by the four security audits:

1. **Avatar upload (highest weight — 13 cases).** File upload is the classic RCE/content-confusion vector. The handler does three gates: declared content-type allow-list, **magic-byte detection**, and a **2 MB** size cap (`MaxFileSize = 2097152`), all returning **422**. Cases attack each gate independently: spoofed content-type on non-image bytes (BE-TC-15), real image with disallowed declared MIME (BE-TC-16), magic-bytes-only image with valid MIME (control, BE-TC-10), SVG/HTML/script payload rejection (BE-TC-14, 17), oversize (BE-TC-18), zero-byte/empty (BE-TC-19), and IDOR (no id on route → BE-TC-20/21). Audit Mediums (#1 raw error text, #2 7-day TTL — **already cut to 60 min in appsettings**, #3 stored Content-Type) are pre-prod recommendations; BE-TC-13 asserts the response never leaks raw storage `ex.Message`.
2. **Anti-enumeration on forgot/reset (5 cases).** Body/status MUST be identical for known/unknown/inactive email on both endpoints. Audit Finding #1 flags a **timing** oracle (synchronous email send) — BE-TC-35 records a timing observation but is **P2/observational** because a deterministic timing assertion is flaky in an integration harness; the body/status identity is the P0 assertion (BE-TC-32/34/36/37/38).
3. **OAuth audience binding (8 cases).** `GoogleTokenValidator` pins audience to `GoogleAuth__ClientId`; a mismatched-audience or otherwise invalid idToken must fail **closed** (generic 401, no create/link). Real Google tokens cannot be minted in CI, so the validation-failure paths (invalid/garbage/empty/wrong-audience token) are the testable core (BE-TC-27–30); the happy-path link/create (BE-TC-24–26) is **blocked** unless a test idToken + matching ClientId is provisioned (see open question Q1).
4. **Edit-child IDOR / family-scope (3 cases + the controller role gate).** Parent id comes from the JWT, never the body; `IsLinkedAsync` is checked before any write; not-linked and not-found both return the **same 403** message (no child-id enumeration). BE-TC-50/51 are P0.
5. **Mass-assignment / privilege escalation (register + profile + edit-child).** None of these commands expose `Roles`/`Id`/`Email`(profile)/`IsActive`. BE-TC-31, 57, 58 assert role injection is impossible through these anonymous/self paths.

## 4. Open questions / assumptions (lead must resolve before some cases run)

- **Q1 — Google happy-path testability.** A valid Google idToken cannot be minted in CI, and the audience must match `GoogleAuth__ClientId`. The find-or-create/link happy path (BE-TC-24/25/26) is **BLOCKED** unless the lead provides either (a) a fakeable `IGoogleTokenValidator` registered in the test host, or (b) a seeded test idToken + matching ClientId. The **fail-closed** paths (BE-TC-27–30) are fully testable today with garbage tokens. **Assumption:** no test double exists yet → 24/25/26 ship marked BLOCKED with the reason.
- **Q2 — Email delivery / reset token capture.** Reset email is dispatched via an in-process integration event to Notifications; the token rides only in the email link, never in the API response. End-to-end reset (BE-TC-39/40/41, the *successful* set-new path) needs a way to capture the real token (test email sink, or an internal test-only token mint). **Assumption:** none available → the successful-reset cases ship BLOCKED; the **anti-enumeration + invalid-token** cases (BE-TC-36/37/38/42) are fully testable.
- **Q3 — Avatar storage in CI.** Upload happy-path (BE-TC-10/11/22/23) requires a reachable MinIO (docker-compose `minio` + `minio-setup`). HANDOFF notes the integration suite already needs Postgres; confirm MinIO is up in the api-tester environment or these are BLOCKED-on-infra. **Assumption:** MinIO is provisioned per docker-compose; if not, mark BLOCKED-infra, do not drop.
- **Q4 — Localized-message assertion depth.** Validation cases assert **422 + field-error presence**, not exact ar/en string equality (resx keys may evolve). Confirm the lead is fine asserting status + that a non-empty localized message is present (not the raw resx key). **Assumption:** yes.
- **Q5 — Per-endpoint rate limiting.** Audits flag missing dedicated rate limits on Forgot/Reset/Register (platform follow-up P1-13). No rate-limit cases are included here beyond noting it; confirm that's deferred to P1-13 QC. **Assumption:** deferred.

## 5. Handoff

- **`backend-test-cases.md`** → `api-tester`: implement all 58 BE-TC-* cases as HTTP integration tests against the running API (`BaseResponse<T>` envelope, `Successed` flag, status codes per `AppControllerBase.NewResult`). Respect the BLOCKED markers — implement what is unblocked, leave the blocked cases stubbed/skipped with the documented reason rather than deleting them.
- **`execution-report.md`** → `api-tester` fills it **after** running: pass/fail per case ID + defects. The QC architect scaffolded the empty template only and never fills results.
- Results feed the **reviewer** gate per the CLAUDE.md pipeline.
