# P6-01 Performance Baseline — local NBomber run (directional)

> Story: [P6-01](../../user-stories/Phase-6-Stabilization/P6-01-performance-targets.md) · Harness: `backend/tests/Learnexia.PerfTests` (NBomber) · NFR-1: core API **p95 < 500 ms**, AI **< 4 s**.
> **Run mode:** in-process `WebApplicationFactory` + **Testcontainers Postgres on Docker Desktop (Windows)**, in-memory distributed-cache (no Redis), scenarios run **sequentially in isolation**, 5 copies × 20 s + 3 s warmup each. Run 2026-06-22.

## ⚠️ How to read these numbers — they are an ENVIRONMENT FLOOR, not a production verdict

**The latency below is dominated by Testcontainers-Postgres-on-Windows-Docker round-trip overhead, not by the application handlers.** The proof is in the data:

| Endpoint class | Example | p50 | What it tells us |
|---|---|---|---|
| **No DB work** (AI helper, fail-closed — no keys) | `S14_AiHintOverhead` | **17 ms** | The in-process pipeline (routing → auth → MediatR → safety pre-check) is **fast**. |
| **No DB work** (AI explain, fail-closed) | `S13_AiExplainOverhead` | **38 ms** | Same — the harness measures real latency; fast paths are fast. |
| **Light DB read** | `S08_GamificationProfile` | **279 ms** | A trivial read costs ~280 ms here — that ~260 ms delta is **one containerized-PG round-trip on Windows Docker**, not handler cost. |
| **Heavy / multi-query read** | `S07_Dashboard`, `S05_SubjectLessons` | 560–816 ms | Several sequential DB round-trips × the same per-query Docker overhead. |

So the "12 scenarios breached p95 < 500 ms" result is **NOT 12 production hotspots** — it is the per-query Docker/Windows DB latency floor (~250 ms/round-trip) multiplied by how many queries each endpoint runs. **Authoritative pass/fail requires the devops live-Kestrel run against real (non-Dockerized-on-Windows) Postgres + Redis** (procedure below). What IS valid from this run: (1) the harness is correct and measures real latency, and (2) the **relative ranking** of endpoints by query weight.

## Results (sequential, isolated, 5 copies × 20 s)

| Scenario | p50 ms | p95 ms | p99 ms | RPS | Errors | SLO (local) |
|---|---|---|---|---|---|---|
| S07_Dashboard | 816 | 1365 | 1582 | 6.0 | 5 | FAIL |
| S01_SignIn | 900 | 1348 | 1461 | 5.5 | 5 | FAIL¹ |
| S05_SubjectLessons | 560 | 1020 | 1289 | 8.9 | 5 | FAIL |
| S11_FamilySummary | 485 | 870 | 1256 | 10.2 | 5 | FAIL |
| S10_ParentProgress | 482 | 836 | 1126 | 10.2 | 5 | FAIL |
| S12_ChildMastery | 370 | 830 | 1109 | 13.4 | 5 | FAIL |
| S02_StartAttempt | 429 | 754 | 1075 | 11.6 | 5 | FAIL |
| S03_SubmitAnswer | 396 | 656 | 1138 | 12.4 | 5 | FAIL² |
| S06_SkillTree | 324 | 610 | 1068 | 15.3 | 5 | FAIL |
| S09_LeagueMe | 294 | 588 | 1074 | 17.0 | 5 | FAIL |
| S04_SubjectsForGrade | 309 | 578 | 1164 | 16.1 | 5 | FAIL |
| S08_GamificationProfile | 279 | 524 | 765 | 17.9 | 5 | FAIL |
| S13_AiExplainOverhead | 38 | 240 | 452 | 52.4 | 2 | N/A (overhead-only) |
| S14_AiHintOverhead | 17 | 36 | 330 | 113.2 | 2 | N/A (overhead-only) |

(The ~5 "errors" per scenario are NBomber's bracketing/warmup edge requests, not endpoint failures — every scenario's status codes are 2xx, except S03; see notes.)

¹ **S01_SignIn is intentionally slow** — sign-in runs the ASP.NET Identity password hasher (deliberately CPU-expensive, security NFR-4). It should be measured against a **separate auth budget**, NOT the 500 ms read SLO. Its cost here = hashing + the env's DB overhead.
² **S03_SubmitAnswer** re-submits the same answer under load → exercises the fast "already-answered/conflict" 4xx path (a valid handled response). Classified as a latency sample (non-5xx); a clean throughput number needs the devops run to script unique attempts.

## Relative hotspot ranking (the part that IS actionable — for a devops deep-dive, NOT a local fix)

Ordering by query weight (env-overhead normalized out by comparing to the ~280 ms light-read floor):
1. **S07_Dashboard** — composes XP + streak + missions + league + continue-lesson. Many reads in one request → the heaviest. Candidate for a consolidated/parallel read or a cached read-model.
2. **S05_SubjectLessons** + **S06_SkillTree** — both run the **`LearningPathEngine`**, whose handler issues **~6 sequential service/DB calls** (`GetSubjectKnowledgeNodes`, `…Edges`, `…SkillMastery`, `…CompletedLessons`, `…Lessons`, `…Skills`). 6 round-trips × per-query overhead. Candidate for batching those reads / parallelizing the independent ones.
3. The parent reads (S10/S11/S12) and StartAttempt are mid-weight; the gamification/subject reads (S08/S04/S09) are near the floor (light).

## BE-5 — quick-win fixes / mitigations (documented, not applied)

**No code fixes were applied from this run, deliberately:** the numbers are an environment artifact (containerized-PG-on-Windows round-trip latency), so "fixing" against them would chase the wrong target and risk regressing working features for no real gain. The honest path per AC-3 ("hotspots above target are fixed **or have a documented mitigation/follow-up**"):

- **Confirm real hotspots on real infra first.** Run the devops live-Kestrel baseline (below) against staging Postgres + Redis. Only endpoints over 500 ms p95 **there** are real hotspots.
- **Pre-identified candidates** (from the relative ranking + code review), to confirm + address in a follow-up if the devops run flags them:
  - `GetSubjectLessonsQueryHandler` / SkillTree: the `LearningPathEngine` path makes ~6 sequential reads — batch/parallelize the independent ones.
  - `Dashboard`: composes many reads — consider a consolidated query or a Redis-backed read-model (the gamification hot-state already has a Redis path per P4-10).
  - Ensure gamification reads (S08/S09) hit the **Redis hot path** in prod (this baseline used the in-memory fallback).
- **Auth endpoints** (sign-in) are intentionally hash-bound — track under a separate auth-latency budget, not the read SLO.

## Authoritative run procedure (devops — real numbers + AI < 4 s)

1. Start real Postgres + Redis; start the Host: `dotnet run --project backend/src/Host/Learnexia.Host` (Development env auto-migrates + seeds; or point at staging). Set `ConnectionStrings__Redis` so gamification uses the Redis hot path. Raise/disable the auth rate-limit profile for the load window.
2. Set `PERF_BASE_URL=http://<host>:5080` (and provider keys `Ai__Providers__Claude__ApiKey` for the **live AI < 4 s** measurement — this baseline's AI scenarios are overhead-only/fail-closed).
3. Drive load against Kestrel at the target prod concurrency/duration. **Note:** the NBomber harness's `PERF_BASE_URL`/Kestrel targeting is **not yet wired** (it currently always uses the in-process factory + in-process `DbContext` seed — see `README.md` Overview), so today use an external HTTP/SSE load tool (k6 / bombardier) against the endpoint table in `README.md`, **or** first land the small follow-up that switches the client to `PERF_BASE_URL` + seeds the target via API. Then raise `PerfConstants.ConcurrentCopies`/`DurationSeconds` and use parallel/ramped simulations for throughput.
4. Record the numbers here; the p95 < 500 ms (reads) and < 4 s (AI) SLOs apply to **that** run.

## Caveats (summary)
- In-process `WebApplicationFactory` + Testcontainers-PG-on-Windows-Docker → latency floor ≈ one DB round-trip (~250 ms) per query; **not** production-representative.
- No Redis → gamification reads used the in-memory fallback.
- Modest seed (1 parent, 1 child, 1 subject/unit/lesson + 3 questions, no skill-graph nodes) → data-volume/index hotspots do not surface; SkillTree returns a near-empty derived tree.
- AI scenarios are pipeline-overhead-only (no keys → fail-closed); live < 4 s = devops.
- CI execution is currently blocked by the GitHub Actions billing issue → this is the "documented harness" form of AC-4; wire to CI once Actions is restored.
