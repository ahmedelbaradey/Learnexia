# Learnexia.PerfTests — NBomber Performance Harness

## Overview

NBomber load/perf harness for NFR-1: core API p95 < 500 ms.

**Implemented mode — in-process (the only wired mode today):** `WebApplicationFactory` + Testcontainers PG. Rate-limiter neutralised, deterministic in-process `DbContext` seed, self-contained, scenarios run **sequentially in isolation**. This is what `dotnet run`/`dotnet test` executes.

> ⚠️ **Live-Kestrel targeting is NOT yet wired.** `PERF_BASE_URL` is reserved but **currently ignored** — the harness always spins the in-process factory (because the warmup seeds via an in-process `LearningDbContext`, which can't reach a remote DB). The in-process baseline therefore measures a **Testcontainers-PG-on-Docker latency floor**, not production (see `docs/perf/P6-01-baseline.md`). The **authoritative** prod-scale + AI<4s numbers are a **devops responsibility** — via either (a) a small follow-up that switches the HTTP client to `PERF_BASE_URL` + seeds the target via API, or (b) pointing an external HTTP load tool (k6 / bombardier) at the endpoint table below against a seeded staging Host. The "Live-Kestrel run" section is the **procedure for that** (once wired), not a currently-working `dotnet run` flag.

---

## In-process run (Docker required for Testcontainers PG)

```bash
# From the repo root — runs the xUnit test:
dotnet test backend/tests/Learnexia.PerfTests -filter "Category=Perf" --no-build

# OR as a console app (more verbose output):
dotnet run --project backend/tests/Learnexia.PerfTests
```

Reports land in `docs/perf/` (bulky NBomber HTML/CSV are gitignored; the committed artifact is the authored `P6-01-baseline.md`).

Concurrency defaults: **5 copies, 20 s, 3 s warmup, scenarios run sequentially in isolation** (edit `PerfConstants.cs`). Kept low because all scenarios share one in-process TestServer — this measures handler latency, not throughput.

---

## Live-Kestrel run (devops procedure — ⚠️ targeting not yet wired; see Overview)

### Prerequisites

1. A running PostgreSQL instance (default `localhost:5432`, DB `Learnexia`).
2. **Optional but recommended for representative Gamification numbers:** Redis at `localhost:6379`.
3. Rate-limit cap raised for the perf run (see Step 3 below).

### Step 1 — Start the Host

```bash
ASPNETCORE_ENVIRONMENT=Development \
dotnet run --project backend/src/Host/Learnexia.Host
# Default: http://localhost:5080
```

Migrations and Dev curriculum seed apply automatically on startup (Development only).

### Step 2 — Set Redis (for representative Gamification numbers)

```bash
export ConnectionStrings__Redis="localhost:6379"
# Then restart the Host with this env var set.
```

Without Redis the Gamification endpoints (S08_GamificationProfile, S09_LeagueMe)
use the in-memory distributed-cache fallback — numbers are NOT representative of
the prod Redis hot path.

### Step 3 — Raise the rate-limit cap for the perf run

The global rule is `Endpoint "*", Limit 200, Period "1m"`. At 30 VUs × 60 s you will
exceed 200 req/min and get 429s that corrupt the numbers.

Create a **gitignored** local override `backend/src/Host/Learnexia.Host/appsettings.Development.local.json`:

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": false,
    "GeneralRules": [
      { "Endpoint": "*", "Limit": 999999, "Period": "1m" }
    ]
  }
}
```

The Host loads this file automatically in Development (gitignored — does not affect prod).

### Step 4 — Run the load against Kestrel

⚠️ **`PERF_BASE_URL` is not yet consumed by this harness** (see Overview). Until the small follow-up wiring lands, run the load with an external HTTP tool against the endpoint table below, e.g.:

```bash
# k6 / bombardier against the running Host (the target must be Dev-seeded):
bombardier -c 30 -d 60s -m POST -H "Authorization: Bearer $JWT" \
  http://localhost:5080/api/Learning/Dashboard
```

The follow-up to make `dotnet run` target Kestrel: switch the client in `PerfRunner`/`Program` to `new HttpClient { BaseAddress = PerfConstants.PerfBaseUrl }` when `PERF_BASE_URL` is set, and replace the in-process `DbContext` seed in `PerfWarmup` with API-based discovery (the target is Dev-seeded on startup).

---

## AI live < 4 s measurement (devops only)

The in-process run measures AI pipeline overhead (auth → validation → rate-limit → safety-layer → fail-closed SSE error). The authoritative NFR-1 AI < 4 s number requires live keys **and** a Kestrel target — and since `PERF_BASE_URL` is not yet wired (see Overview), measure it with an external HTTP/SSE load tool against the running keyed Host (or after the Kestrel-targeting follow-up lands). Reference setup:

```bash
# Set real keys
export Ai__Providers__Claude__ApiKey="<your-key>"
# (or OpenAI equivalent)

# Start the Host with the live key
dotnet run --project backend/src/Host/Learnexia.Host

# Run the harness, which includes S13_AiExplainOverhead and S14_AiHintOverhead
PERF_BASE_URL=http://localhost:5080 \
dotnet run --project backend/tests/Learnexia.PerfTests
```

With live keys the SSE stream will contain `event: message` + `event: done` instead of `event: error`. Check the NBomber report for the p95 of S13/S14 — target < 4000 ms.

**The committed baseline does NOT assert the AI < 4 s SLO.** Only core-API scenarios assert p95 < 500 ms.

---

## Scenario map

| ID | Scenario | Endpoint | Auth | SLO |
|---|---|---|---|---|
| S01 | SignIn | POST /api/Users/Authentication/Sign-In | Anonymous | p95 < 500 ms |
| S02 | StartAttempt | POST /api/Learning/Quizzes/{lessonId}/Attempt | Student | p95 < 500 ms |
| S03 | SubmitAnswer | POST /api/Learning/Quizzes/{attemptId}/Answers | Student | p95 < 500 ms |
| S04 | SubjectsForGrade | GET /api/learning/Subjects/ForGrade?grade=1 | Any | p95 < 500 ms |
| S05 | SubjectLessons | GET /api/learning/Subjects/{id}/Lessons | Any | p95 < 500 ms |
| S06 | SkillTree | GET /api/learning/Subjects/{id}/SkillTree | Student | p95 < 500 ms |
| S07 | Dashboard | GET /api/Learning/Dashboard | Student | p95 < 500 ms |
| S08 | GamificationProfile | GET /api/Gamification/Profile | Student | p95 < 500 ms |
| S09 | LeagueMe | GET /api/Gamification/Leagues/Me | Student | p95 < 500 ms |
| S10 | ParentProgress | GET /api/Parent/Children/{id}/Progress | Parent | p95 < 500 ms |
| S11 | FamilySummary | GET /api/Parent/Family/Summary | Parent | p95 < 500 ms |
| S12 | ChildMastery | GET /api/Parent/Children/{id}/SubjectMastery | Parent | p95 < 500 ms |
| S13 | AiExplainOverhead | POST /api/AiTutor/Explain (SSE) | Student | N/A local; devops < 4000 ms |
| S14 | AiHintOverhead | POST /api/AiTutor/Hint (SSE) | Student | N/A local; devops < 4000 ms |

---

## Report location

- `docs/perf/P6-01-baseline.md` — hand-authored baseline analysis (**committed** — the authoritative artifact)
- `docs/perf/*.html` / `*.csv` / `*.txt` / `P6-01-run-summary.md` / per-scenario reports — regenerated each run (**gitignored**)
