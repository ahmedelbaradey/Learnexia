# Security Audit — chore/hardening-test-infra-and-security

**Date:** 2026-06-05
**Auditor:** security-auditor agent
**Branch:** chore/hardening-test-infra-and-security (unstaged working tree changes against HEAD = main@32a546a)
**Scope:** Re-audit of 2 pre-existing platform Highs (Newtonsoft.Json CVE pin, JWT secret guard) plus test-infra Program.cs lazy connection resolution.

---

## Scope Reviewed (files / endpoints)

| File | Reason |
|---|---|
| `backend/Directory.Packages.props` | Newtonsoft.Json transitive pin via `CentralPackageTransitivePinningEnabled` |
| `backend/src/Host/Learnexia.Host/Program.cs` | Hangfire + Npgsql health-check lazy connection resolution |
| `backend/src/Modules/Identity/Learnexia.Modules.Identity.Infrastructure/DependencyInjection.cs` | `GuardJwtSecret` dev-warn path addition |
| `backend/tests/Learnexia.IntegrationTests/LearnexiaWebAppFactory.cs` | ConnectionStrings:Default override injection |
| `backend/tests/Learnexia.IntegrationTests/P1_12_BE4_AvatarUpload_Tests.cs` | Same override — AvatarWebAppFactory |
| `backend/tests/Learnexia.IntegrationTests/P1_12_BE5_GoogleSignIn_Tests.cs` | Same override — GoogleSignInWebAppFactory |
| `backend/tests/Learnexia.IntegrationTests/P1_13_BE4_Captcha_Tests.cs` | Same override — CaptchaWebAppFactory |
| `backend/tests/Learnexia.IntegrationTests/P1_13b_BE1_AuthRateLimit_Tests.cs` | Same override — RateLimitWebAppFactory |
| `backend/src/Host/Learnexia.Host/appsettings.json` | Committed placeholder secret baseline |
| `backend/src/Host/Learnexia.Host/appsettings.Development.json` | Dev-environment overrides |
| Dependency scan: `dotnet list Learnexia.Modular.sln package --vulnerable --include-transitive` | Full vulnerability scan |

---

## Findings

| # | Severity | Issue | Location (file:line) | Remediation |
|---|---|---|---|---|
| 1 | Info | `RequireHttpsMetadata = false` is a pre-existing flag, not introduced by this diff. No change in this pass; must be verified environment-gated before production deploy. | `DependencyInjection.cs:162` | Conditionally set to `true` in Production/Staging via env or appsettings override. Tracked pre-existing risk — not in scope of this pass. |
| 2 | Info | `AddHangfireServer()` is now called unconditionally (moved outside the `if (!string.IsNullOrWhiteSpace(...))` guard), so a Hangfire worker process starts even if no storage is configured. Not a security issue; no exploit surface. Minor functional regression risk: if `ConnectionStrings:Default` is empty at startup the Hangfire server starts with no backend. | `Program.cs:123` | Acceptable: production always has a `ConnectionStrings:Default`; the inner guard at DI resolution time prevents misconfiguration. No security impact. |

No Critical or High findings introduced by this diff.

---

## Prior High Findings — Closure Status

### High #1 — Newtonsoft.Json CVE (GHSA-5crp-9r3c-p9vr, was 11.0.1 transitive via Hangfire.PostgreSql)

**Status: CLOSED.**

Evidence:
- `Directory.Packages.props` line 8: `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>` added.
- `Newtonsoft.Json` is pinned at `13.0.3` in the central package manifest (line 101).
- `dotnet list Learnexia.Modular.sln package --vulnerable --include-transitive`: zero vulnerable packages across all 28 projects.
- `dotnet list Learnexia.Modular.sln package --include-transitive | grep -i newtonsoft.json`: every resolved version is `13.0.3`; no `11.x` remains anywhere.
- The transitive pin is effective: NuGet's central package management now forces 13.0.3 even when `Hangfire.PostgreSql` requests 11.x.

### High #2 — JWT Secret (committed CHANGE_ME placeholder — forgeable tokens)

**Status: CLOSED.**

Evidence:
- `GuardJwtSecret` (DependencyInjection.cs:206–256) throws `InvalidOperationException` at startup when environment is `Production` or `Staging` AND the secret is empty or equals the `DefaultJwtSecret` constant (`CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789`). No weakening introduced.
- The new `isDevelopment` warn path (lines 230–242) fires ONLY when `environment == "Development"` (case-insensitive). It is a non-fatal `LogWarn` that does NOT log the secret value — it logs only that the placeholder is in use.
- In `Testing` environment (all WebApplicationFactory factories call `builder.UseEnvironment("Testing")`), `isDevelopment` is `false`, so the warn does NOT fire and the throw path does NOT fire. Integration tests are unaffected.
- `TokenValidationParameters`, `IssuerSigningKey`, signing algorithm, and all auth-flow wiring are untouched by this diff (confirmed by reviewing the diff hunk).
- `JwtSettings__Secret` environment variable override works via standard ASP.NET Core configuration binding (double-underscore convention); the guard reads the already-bound `jwtSettings.Secret` value which reflects env overrides.
- The committed placeholder posture is: dev placeholder is accepted in Development/Testing (non-fatal warn); rejected at startup in Production/Staging. This is the correct and accepted remediation posture.

---

## Dependency Scan Results

- `dotnet list backend/Learnexia.Modular.sln package --vulnerable`: **0 vulnerable packages** (28/28 projects clean).
- `dotnet list backend/Learnexia.Modular.sln package --vulnerable --include-transitive`: **0 vulnerable packages including transitives** (28/28 projects clean).
- `Newtonsoft.Json`: resolved to `13.0.3` in all projects. No `11.x` anywhere.
- Frontend (`npm audit`): out of scope for this pass (no frontend files in diff).

---

## Test-Infra Change Analysis (Program.cs Lazy Connection Resolution)

**No production security behavior changed.**

1. **Hangfire** — was: `UseNpgsqlConnection(defaultConnectionString)` (local variable captured at builder setup). Now: `sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")` resolved at DI resolution time. In production both paths read the same `ConnectionStrings:Default` config key. No connection string is logged. No new exposure.

2. **Npgsql health check** — was: `AddNpgSql(defaultConnectionString, ...)`. Now: `AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")!, ...)`. Same config key, same value in production. The change is purely for test-factory override correctness.

3. **Hangfire dashboard** — confirmed Development-only gate at line 216: `if (app.Environment.IsDevelopment()) { app.UseHangfireDashboard("/hangfire"); }`. Unchanged in this diff. Outside Development, `/hangfire` returns 404.

4. **No secrets logged** — connection string is passed into Hangfire/Npgsql internal objects only; not written to any logger.

5. **Factory connection string override** — `config.AddInMemoryCollection(["ConnectionStrings:Default"] = _postgres.GetConnectionString())` is added to 5 test factory files. This only affects the `Testing` environment and is correct and safe: it replaces the localhost:5432 placeholder with the Testcontainers container string at config-binding time, which is then picked up lazily by both Hangfire and the health check.

---

## Notes / Accepted Risks

- **`RequireHttpsMetadata = false`**: pre-existing, not introduced here. Should be environment-gated for production. Tracked separately.
- **`AddHangfireServer()` unconditional**: low severity functional risk, no security impact. Acceptable.
- **Committed placeholder secret in `appsettings.json`**: inherent in a public dev repository. Mitigated by `GuardJwtSecret` throwing in Production/Staging. Posture confirmed correct.
- **`appsettings.Development.local.json`** containing a real remote DB connection string: file is gitignored (`.gitignore:33`) and NOT tracked/staged. No leak risk. Developers are responsible for keeping local override files out of commits.

---

## Verdict: PASS

No Critical or High findings. The 2 prior High findings are **CLOSED**. The test-infra change introduces no security regression.
