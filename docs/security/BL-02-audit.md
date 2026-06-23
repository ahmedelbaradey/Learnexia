# Security Audit — BL-02 Multimodal-parsing pipeline (Stage 1)

> Defensive audit, 2026-06-23. Scope: the BL-02 .NET orchestration slice (`Curriculum` module) + the
> new Python `curriculum-intelligence` worker. Audit/report only — no code edited.
> Context read: `docs/briefs/BL-02.md`, `docs/plans/BL-02.md`, `docs/dev/adr/0004-python-curriculum-pipeline-service.md`.

## Live-Azure-DI gating note (affects severities below)

Per ADR-0004 §5 + plan Q5, the **live OCR path is mocked / devops-gated** and is NOT exercised yet:
the default `PARSER_BACKEND=mock` runs a deterministic in-process parser (`MockParser`), and the Azure DI,
MinerU/PaddleOCR, DOCX-zip, and Claude/VLM code paths are **either not yet implemented or unreachable
without a provisioned key**. Concretely:

- `azure_di_parser.py` is wired but only selected when `PARSER_BACKEND=azure_di` AND endpoint+key present.
- `fallback_parser.py` (MinerU) raises `mineru_not_wired` — no real extraction yet.
- There is **no DOCX/zip parsing, no XML parsing, and no Claude/VLM captioning code** in the tree at all
  (`diagram_caption` is only a passive artifact field; the `anthropic` dep lives in the unused `[live]` extra).

Consequence for this audit: the OCR-of-untrusted-files attack surface (zip-bomb, XXE, decompression,
prompt-injection, download size bounds) is **latent, not active**. Findings against those paths are filed
at the severity they will carry **when the devops flip happens**, but are flagged "not-yet-exercised" and
do **not** block this gate as Critical/High today. They are recorded so the live-flip follow-up cannot ship
without addressing them. The findings that DO bite today are the .NET `ResultJson`-trust path (the Python
worker writes those rows even under the mock) — those are assessed at full severity.

## Scope reviewed (files / endpoints)

**.NET (`backend/src/Modules/Curriculum/`)**
- `Infrastructure/Jobs/ParseJobAdvanceService.cs` — advance poller, `ResultJson` deserialization, claim SQL, provenance build.
- `Infrastructure/Features/CurriculumDocuments/Commands/ReparseCurriculumDocument/ReparseCurriculumDocumentCommandHandler.cs`
- `Api/Controllers/CurriculumDocumentsController.cs` — `POST /{id}/reparse` authz.
- `Application/Abstractions/IParsingServiceClient.cs`, `Domain/Entities/{PipelineJob,CurriculumDocument}.cs`
- `Infrastructure/Persistence/Configurations/{CurriculumDocument,PipelineJob,Chapter,ContentSource}Config.cs`

**Python (`python/curriculum_intelligence/`)**
- `app/{db,storage,config,logging,health}.py`, `main.py`
- `parsers/{base,artifact,factory,mock_parser,azure_di_parser,fallback_parser}.py`
- `workers/{poller,pipeline}.py`
- `Dockerfile`, `pyproject.toml`, `.env.example`, `.gitignore`
- `docker/docker-compose.yaml` (`curriculum-intelligence` service + `minio-setup`), `.github/workflows/ci.yml` (python job)

## Findings

| # | Severity | Issue | Location | Remediation |
|---|----------|-------|----------|-------------|
| 1 | **High** | **Unbounded `ResultJson` field lengths from the Python worker wedge the poller.** `DeserializeResult` copies `artifact_key` and each `chapters[].title` verbatim into `CurriculumDocument.ParsedArtifactObjectKey` (`varchar(512)`) and `Chapter.Title` (`varchar(512)`) with no length validation. A malformed/over-long value throws on `db.SaveChangesAsync()` *after* the claim transaction already committed `Status='Processing'`. The per-job exception is caught + logged, but the job is now stranded at `Processing` forever — the poller only ever claims `Status IN ('Done','Failed')`, so it is never retried and the document is stuck. A single bad job (Python bug or a tampered `ResultJson` row) silently halts advancement for that document and burns a claim slot each cycle. | `ParseJobAdvanceService.cs:243` (`ParsedArtifactObjectKey = result.ArtifactKey`), `:307-328` (chapter titles), interacting with the claim commit at `:150-157` and per-job catch at `:187-191` | Validate/bound all `ResultJson`-derived strings before assignment (truncate or reject `artifact_key`>512, `title`>512, chapter count, etc.); on a non-deserialize processing exception, transition the job to `Failed`/`PermanentlyFailed` (not leave it at `Processing`) so it cannot be orphaned. Treat `ResultJson` as untrusted cross-process input, mirroring the existing `PayloadJson` validation on the Python side. |
| 2 | **Medium** | **Over-long failure reason throws on the Failed path.** On the failure path `MarkDocumentFailed` writes `job.ErrorMessage` (DB-capped at 2048; Python truncates to 2048 in `mark_failed`) into `CurriculumDocument.StatusReason`, which is `varchar(1024)`. A 1025–2048-char error message throws on `SaveChanges`, and via the same claim-then-process flow as #1 strands the *failed* job at `Processing`. The deserialize-failure branch (`:235-236`) also writes `$"...: {ex.Message}"` into `StatusReason` with no length guard. | `ParseJobAdvanceService.cs:377-393`, `:235-236`; `app/db.py:176` (`[:2048]`); `CurriculumDocumentConfig.cs:75-77` (1024) | Truncate the reason to the column length (≤1024) before assignment, or widen `StatusReason` to ≥2048 to match `ErrorMessage`. Same "never leave a job at Processing on a write failure" fix as #1 covers the wedge. |
| 3 | **Low** | **Raw exception text persisted into document state.** Deserialize/failure paths embed `ex.Message` and the worker's `ErrorMessage` into `StatusReason`, which is surfaced by the admin `GET /{id}` detail. Audience is admin-only (low disclosure risk), but raw internal exception text in a stored, API-readable field is an info-hygiene smell and feeds #2's length problem. | `ParseJobAdvanceService.cs:236`, `:381` | Store a stable diagnostic code + bounded message; log the full `ex` via `ILoggerManager` (already done) rather than persisting raw `ex.Message`. |
| 4 | **Low** | **No download / decompression size bound in the Python pipeline (latent — live path).** `ParsePipeline.run` does `self._storage.get_object(...)` into memory with no max-size guard, and the future DOCX (zip) / MinerU paths have no decompression-ratio limit. Harmless under the mock (no real files), but once live this is a memory-exhaustion / zip-bomb vector on untrusted uploads. The upload side caps files at 100 MB, which bounds the *download* but not in-worker decompression expansion. | `workers/pipeline.py:100`; `app/storage.py:75-84` | At the live flip: enforce a max object size before `read()`, and a decompression-ratio/zip-entry-count cap when DOCX/zip parsing is added; stream rather than load whole files where the SDK allows. |
| 5 | **Info** | **XXE / prompt-injection surfaces are not yet present (track for live flip).** No XML/DOCX parser and no Claude/VLM prompt code exists today, so XXE and VLM prompt-injection are not exploitable. When PY-3 adds DOCX (python-docx → zipfile/lxml) and the Claude captioning seam lands, they must: disable XML external-entity resolution, and keep untrusted document text strictly in the user/content role (never the system/tool context) of the VLM prompt, with bounded output. | future `parsers/` (DOCX, VLM) | Add to the live-flip checklist; re-audit PY-3 when implemented. |
| 6 | **Info** | **`pytest 8.3.4` has a known vuln (CVE-2025-71176, fixed 9.0.3); test-only.** Surfaced by `pip-audit`. Not shipped in the worker image (it is in the `[test]` extra, used in CI only). The pinned runtime deps (`boto3 1.35.71`, `fastapi 0.115.5`, `uvicorn 0.32.1`, `psycopg 3.2.3`) showed no advisories. | `pyproject.toml:33-37` | Bump `pytest` to ≥9.0.3 at convenience; no production exposure. |
| 7 | **Info** | **.NET test-project vulnerable packages (pre-existing, out of BL-02 scope).** `dotnet list --vulnerable` flags `SQLitePCLRaw.lib.e_sqlite3 2.1.11` and `MessagePack 2.5.192` (high) — but only in `*.UnitTests`/`PerfTests` projects, not in any `Curriculum` production project (all Curriculum projects: "no vulnerable packages"). Not introduced by BL-02. | `backend/tests/*` | Track separately; not a BL-02 blocker. |

## What was verified clean (no finding)

- **Admin authz on reparse — correct.** `CurriculumDocumentsController` has class-level `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` (Admin/SuperAdmin, the established cross-module policy); `POST /{id}/reparse` inherits it. Non-admin → 403, unauthenticated → 401.
- **No IDOR.** Curriculum documents are admin-global resources (no per-user ownership axis; Admin is the only role that reaches the controller). `{id}` lookup is by design; reparse also re-checks `userId` and uses the document's own stored `ObjectKey`/`ContentType` to rebuild the payload — no client-supplied path.
- **No injection in SQL.** The .NET claim uses a constant interpolated-string-literal CTE with **no** runtime concatenation of user/job input (`ParseJobAdvanceService.cs:110-147`). Python `db.py` uses fully parameterized `%(name)s` queries throughout; the only f-string is the schema/table identifier from trusted config, defensively quoted (`_qualified`). `FromSqlRaw`-style interpolation of untrusted input: none.
- **`object_key` path-traversal — defended.** `assert_safe_object_key` rejects empty, absolute (`/`,`\`), `..`/`.` segments, and null bytes; it is enforced in `ParsePayload.parse` (turning a bad key into a terminal `Failed`, not an uncaught error) and again in both `get_object`/`put_object`. `artifact_object_key` strips any directory component and forces the `artifacts/` prefix, so a crafted source key cannot write outside that prefix or overwrite the source object.
- **Over-posting / mass-assignment — none.** `ReparseCurriculumDocumentCommand` carries only `DocumentId`; no client-controlled entity fields (no `Status`, role, audit fields). The advance poller sets entity fields server-side from `ResultJson` only (subject to finding #1's length issue, not a mass-assignment issue).
- **`ServerError<T>` does not leak internals to clients.** The reparse handler returns `ServerError<T>(localizedKey)` (a localized resource string), not `ex.Message`. The known `ServerError<T>(ex.Message)` anti-pattern is **not** present in BL-02 handlers.
- **Secrets — env-only, none committed.** No hardcoded Azure DI key, Anthropic key, or MinIO secret in source. `config.py` reads all from env; `.env.example` contains only dev placeholders (`minioadmin`, local DSN) clearly marked. Compose passes secrets via `${VAR}` references (`AZURE_DI_KEY`, `ANTHROPIC_API_KEY` default empty). `.gitignore` excludes `.env` and `.venv`; confirmed neither is git-tracked. Logging (`app/logging.py`, all `logger.*` calls) logs object keys/ids/counts but never the DSN password, MinIO secret, or API keys.
- **No secrets/PII in AI prompts or logs.** No child/parent PII flows through this pipeline (admin-uploaded curriculum only); no PII in logs.
- **Service exposure minimal.** The `curriculum-intelligence` compose service publishes **no host ports** (only internal `EXPOSE 8091` for the in-container healthcheck on `localhost`); the poll loop has no listening port; FastAPI exposes only `/health` (no parse endpoint — the worker is poll-driven). CI workflow does not echo secrets (`PARSER_BACKEND: mock` only).
- **Outbox payload trust — Python side validates.** `ParsePayload.parse` enforces the `{object_key, content_type}` shape and key-safety before any storage call; no shell-out, `eval`, `exec`, `subprocess`, `pickle`, or `os.system` anywhere in the Python source. SDK imports are lazy and only reached when the live backend is configured.

## Verdict: FAIL — 1 blocking (High) finding

Finding #1 (High) blocks the reviewer gate. Finding #2 (Medium) is closely related and should be fixed in the
same pass. Both are the same root cause: **`ResultJson` written by the Python worker is treated as fully
trusted, and a write failure during advance strands a job at `Processing`**, which is reachable today even
under the mock backend (the worker writes those rows in dev/CI). Findings #4/#5 are latent live-flip items
(not blocking now); #3/#6/#7 are Low/Info.

## Top must-fix (before the gate clears)

1. **(High, #1)** Bound/validate every `ResultJson`-derived string against its DB column before assignment
   (`artifact_key`→512, chapter `title`→512), and on any per-job processing exception transition the job to a
   terminal status instead of leaving it `Processing` — otherwise a malformed result permanently wedges that document.
2. **(Medium, #2)** Truncate `StatusReason` to ≤1024 (or widen the column to 2048 to match `ErrorMessage`) so a
   long failure message cannot throw on the Failed path.

## Notes / accepted risks

- Live Azure DI / MinerU / Claude / DOCX paths are mocked-and-gated; their attack surface (#4, #5) is latent.
  Recommend a **mandatory re-audit of PY-3 + the live OCR flip** before `PARSER_BACKEND=azure_di` ships, with #4/#5
  on that checklist.
- Dependency scans: Python runtime deps clean; `pytest` (test-only) CVE noted (#6). .NET vulnerable packages are
  test-project-only and pre-existing (#7) — neither is a BL-02 production exposure.

---

## Re-audit addendum — 2026-06-23 (fix verification for #1 / #2 / #3)

Re-audited the `backend-feature` fixes for findings **#1 (High)**, **#2 (Medium)**, **#3 (Low)**.
Scope of re-review: `Infrastructure/Jobs/ParseJobAdvanceService.cs` (rewritten advance/terminal logic) +
the two new resource keys (`CurriculumParseResultInvalid`, `CurriculumDocumentParseProcessingFailed`)
in `SharedResourcesKey.cs` and both resx files. Build: `Curriculum.Infrastructure` compiles clean
(0 errors; only pre-existing NU1510 prune warnings). Audit-only — no code edited.

### Verification results

1. **No-stranding under normal operation — VERIFIED.** `ProcessJobAsync` (`:215-243`) wraps the entire
   normal path in `try/catch (Exception)`; any deserialize / string-length / `SaveChanges` exception is
   caught and routed to `TerminateJobOnExceptionAsync` (`:254-296`), which writes `PermanentlyFailed`
   (job) + `Failed` (document) in a **fresh** transaction using only **bounded constants** — the
   localized `CurriculumDocumentParseProcessingFailed` reason (138 chars EN / 76 chars AR, both well
   under the 1024 `StatusReason` cap) and constant status strings — so the terminal write itself cannot
   throw a length/serialization error. Under normal operation a malformed `ResultJson` row no longer
   wedges the document. **Residual (acceptable):** the only remaining strand path is a *full DB
   outage/conflict* on the terminal write (or a process exit between claim-commit and terminal write),
   which is logged-and-swallowed and documented as manual-recovery — this is a DB-availability residual,
   not a normal-operation logic defect, and is **accepted** (same class explicitly excluded by the
   re-audit scope).

2. **Every DB-bound string is bounded; artifact_key reject-path correct — VERIFIED.** Column constants
   (`:81-84`) match the EF configs exactly (ParsedArtifactObjectKey 512, Chapter.Title 512, StatusReason
   1024, ErrorMessage 2048 — confirmed against `CurriculumDocumentConfig`/`ChapterConfig`/`PipelineJobConfig`).
   `artifact_key` > 512 → `ParseResultValidationException` → `MarkDocumentFailed` with a safe localized
   reason; **no key is ever written when over-length** (no wrong-key/truncated-key write — `:550-553`,
   `:313-324`). Chapter `Title` → `TruncateWithEllipsis(…, 512)` before assignment (`:402`).
   `ErrorMessage` → `StatusReason` is `SanitizeControlChars` then `TruncateWithEllipsis(…, 1024)` (`:478-482`).
   Re-enqueue path writes `StatusReason = null` (no length risk). All localized reason constants verified
   < 1024.

3. **Defensive deserialization cannot throw an unhandled exception — VERIFIED.** `DeserializeAndValidateResult`
   (`:516-603`) guards `JsonDocument.Parse` (→ `ParseResultValidationException`), null/empty/whitespace
   and over-long `artifact_key`, missing/non-array `chapters` (→ empty), missing/non-integer chapter
   `number` (try/catch → validation), null/missing `title` (→ empty), and `page_start`/`page_end` via
   `ValueKind` checks. Extra/unknown JSON fields are ignored. Caller catches `ParseResultValidationException`
   and fails the document cleanly. **Minor robustness nit (not a finding):** if `artifact_key` /
   `parse_status` / `diagnostics` / `title` arrive as a *non-string JSON kind* (number/object/array),
   `JsonElement.GetString()` throws `InvalidOperationException`, which is **not** caught inside
   `DeserializeAndValidateResult` — it propagates to the `ProcessJobAsync` catch-all and routes through
   the generic terminal path (document → Failed, generic reason) instead of the cleaner
   `CurriculumParseResultInvalid` path. No crash, no stranding, no wrong write; only a slightly less
   specific failure reason. Recommend (low, optional) guarding those `GetString()` calls with a
   `ValueKind == String` check for a cleaner reason — not gate-blocking.

4. **`ChangeTracker.Clear()` + re-attach introduces no new defect — VERIFIED.** `Clear()` (`:266`)
   detaches stale uncommitted mutations from the failed per-job transaction; because jobs are processed
   sequentially and each job owns its own rolled-back transaction, **no other job's pending writes can be
   lost** (there are none in flight). `Attach(job)` marks the reachable graph `Unchanged`; the subsequent
   `Attach(job.Document)` on the same in-memory instance is a no-op (no duplicate-tracking, no accidental
   INSERT). Setting `job.Status` / document Status+Reason marks only those properties Modified, so
   SaveChanges emits exactly two scoped UPDATEs. The `job.Document` C# reference survives `Clear()`
   (Clear detaches tracking, not navigation references), so the null-check and attach are valid. No
   wrong-key write, no data loss, no detached-graph mishandling.

5. **#3 — no raw exception text persisted; localized constants present — VERIFIED.** No path persists
   `ex.Message` into `StatusReason`. The deserialize-fail path uses `CurriculumParseResultInvalid` (`:322`),
   the terminal path uses `CurriculumDocumentParseProcessingFailed` (`:276`), and the dead-letter path uses
   the sanitized/bounded Python `ErrorMessage` or the localized fallback (`:478-482`). Full `ex` detail is
   logged via `ILoggerManager` only (`:235-236`, `:318-320`). Both keys exist in
   `SharedResourcesKey.cs` and carry en-US **and** ar-EG values in both resx files (confirmed).

### Updated verdict for #1 / #2 / #3

| # | Prior severity | Re-audit status |
|---|----------------|-----------------|
| 1 | High           | **PASS (resolved)** — bounded + no-stranding under normal operation; DB-outage residual accepted. |
| 2 | Medium         | **PASS (resolved)** — `StatusReason` sanitized + truncated to 1024; localized fallbacks bounded. |
| 3 | Low            | **PASS (resolved)** — localized constants persisted, raw `ex` logged not stored. |

**Gate block: LIFTED.** The High finding (#1) that blocked the reviewer gate is resolved; no Critical/High
remains open against the BL-02 .NET slice. Findings **#4 (zip-bomb / download size bound)** and
**#5 (XXE / VLM prompt-injection)** remain **deferred / latent** — they were never gate-blocking now and
stay on the **mandatory live Azure-DI / PY-3 re-audit checklist** before `PARSER_BACKEND=azure_di` ships.
Optional low nit from point 3 (non-string JSON kind → generic reason) noted for convenience; non-blocking.

**Re-audit verdict: PASS** (was FAIL).
