# BL-01 (Backend) — Upload curriculum documents with metadata

> Story: [../../../user-stories/Backlog-Phase-2-Plus/BL-01-upload-curriculum-documents.md](../../../user-stories/Backlog-Phase-2-Plus/BL-01-upload-curriculum-documents.md)
> Backlog Phase 2+ — Curriculum Intelligence · Epic: Curriculum Intelligence · SP: 3 · FR-CI-1 · Est. unit: hours
> **Backend only (admin upload + queue + status). Frontend (admin upload UI) is DEFERRED — Phase-7-style admin surface, not started; confirm with lead.**
>
> **Cross-cutting system decisions (A–E) are in `docs/briefs/curriculum-system-of-record.md`.** BL-01 is not directly impacted by Decisions B/C/D/E (those are BL-04/BL-05/BL-03 concerns), but implementers must be aware that: uploaded documents become the root of the provenance tree (Decision B — `ContentSource` created later by BL-02); version context is Draft until publish (Decision C — BL-05 sets this). BL-01 itself only stores and queues.

> **Status: ✅ BUILT + MERGED** (`feat/BL-01-curriculum-upload`, 2026-06-23) — `CurriculumDocument` entity + upload command (mirror `UploadAvatarCommandHandler`) + list/detail queries + BL-02 trigger event, in the `Curriculum` module. *(Prior "Not started" marker was stale planning-time.)*

> **LEAD DECISIONS REQUIRED (inherit from BL-04 + BL-01 open questions):**
> - **Q1 — Module placement:** inherits BL-04's new-`curriculum`-module decision. Do not scaffold independently.
> - **Q2 — Queue model:** status field on `CurriculumDocument` as the queue (recommended) vs. a separate queue/job table?
> - ~~**Q3 — .NET-vs-Python boundary / trigger mechanism:**~~ **DECIDED: DB-outbox + Python poller** (see `docs/briefs/curriculum-system-of-record.md` §4b). On upload success, write a `PipelineJobs` row (`JobType='parse'`, `Status='Pending'`) — no MediatR integration event. MediatR cannot reach a separate Python process. Task `BL-01-BE-9` covers the `PipelineJobs` entity + migration.
> - **Q4 — Allowed types & max file size:** PDF + DOCX + images confirmed? Max file-size cap (textbooks can be 50–100 MB)?
> - **Q5 — FE deferral confirmed:** build backend + queue only now; admin upload UI deferred.

## Backend tasks (.NET)

| ID | Task | Artifact | Deps | Est (h) | Status |
|---|---|---|---|---|---|
| BL-01-BE-1 | Add `CurriculumDocument` domain entity: `Id`, `FileName`, `ObjectKey` (storage key — never a URL), `ContentType`, `FileSize`, `GradeId` (int), `SubjectId` (int), `Language` (`ContentLanguage` enum — reuse existing), `Country` (string), `DocumentStatus` enum (`Received`/`Processing`/`Done`/`Failed`), `StatusReason` (string, nullable diagnostics), timestamps. Derive from `AggregateRoot`, mirror `Skill.cs`. Add `DocumentStatus` enum to `Domain/Enums/`. | `Domain/Entities/CurriculumDocument.cs`, `Domain/Enums/DocumentStatus.cs` | BL-04 complete (module established) | 3 | 🔲 |
| BL-01-BE-2 | EF config `CurriculumDocumentConfig.cs`: `ToTable("CurriculumDocuments", schema)`, index on `Status` (queue scans), composite index on `(GradeId, SubjectId)` (admin filtering), `ObjectKey` required. Add `DbSet<CurriculumDocument>` to chosen DbContext. Npgsql migration `AddCurriculumDocumentTable`. | `Persistence/Configurations/CurriculumDocumentConfig.cs`, chosen DbContext, migration | BL-01-BE-1 | 3 | 🔲 |
| BL-01-BE-3 | `CurriculumFileValidator`: extend the `AvatarImageValidator` magic-byte pattern to support PDF (`%PDF-`), DOCX (PK ZIP header), and common image signatures (JPEG/PNG/WEBP). Config-driven max file-size cap (`CurriculumUploadConfiguration.MaxFileSizeBytes`). Return a localized validation failure, not a 500. | `Application/Features/CurriculumDocuments/Validators/CurriculumFileValidator.cs`, `Infrastructure/Configuration/CurriculumUploadConfiguration.cs` | BL-01-BE-1 | 3 | 🔲 |
| BL-01-BE-4 | `UploadCurriculumDocumentCommand` + handler: accept file (`IFormFile` at controller boundary, passed as `Stream`/metadata into the command) + metadata (GradeId, SubjectId, Language, Country). Validate via `CurriculumFileValidator`. Upload via `IStorageService` to `curriculum` bucket with a GUID object key. Persist `CurriculumDocument` with `Status = Received`. Mirror `UploadAvatarCommandHandler` exactly (validation order, `ServerError<T>` on exception, store key not URL). | `Application/Features/CurriculumDocuments/Commands/UploadCurriculumDocument/UploadCurriculumDocumentCommand.cs`, `...Handler.cs`, `...Validator.cs` | BL-01-BE-2, BL-01-BE-3 | 5 | 🔲 |
| BL-01-BE-5 | **REVISED (DB-outbox, not MediatR event):** On successful save in the upload handler, write a `PipelineJobs` row: `JobType='parse'`, `Status='Pending'`, `DocumentId = <new document Id>`, `PayloadJson = { "object_key": "<key>", "content_type": "<type>" }`, `CreatedAt = now`. This is the cross-process trigger for the Python parsing service — **do NOT publish a MediatR integration event** (MediatR events are in-process only and cannot reach a separate Python process). The `PipelineJobs` table is defined in BL-01-BE-9 (must exist first). Write is within the same `SaveChangesAsync` scope as the document insert (same transaction — if the document insert fails, the job row is rolled back too). | Extend `UploadCurriculumDocumentCommandHandler` to write `PipelineJobs` row via `DbContext` | BL-01-BE-4, BL-01-BE-9 | 2 | 🔲 |
| BL-01-BE-9 | **`PipelineJobs` entity + config + migration (DB-outbox cross-process seam — Decision, see `curriculum-system-of-record.md` §4b):** Add `PipelineJob` domain entity: `Id` (bigserial PK), `JobType` (string varchar 64 — `'parse'` / `'ingest'` / `'embed'`), `Status` (string varchar 16 — `'Pending'` / `'Processing'` / `'Done'` / `'Failed'` / `'PermanentlyFailed'`), `DocumentId` (int — plain indexed int FK → `CurriculumDocument`), `PayloadJson` (string — input for Python worker), `ResultJson` (string, nullable — output written by Python worker), `ErrorMessage` (string, nullable), `CreatedAt` (DateTimeOffset), `ClaimedAt` (DateTimeOffset, nullable — set when Python worker claims the job), `CompletedAt` (DateTimeOffset, nullable), `RetryCount` (int, default 0). EF config `PipelineJobConfig.cs`: `ToTable("PipelineJobs", schema)`, index on `(Status, JobType)` (polling query), index on `DocumentId`. Npgsql migration `AddPipelineJobsTable`. Add code comment: "Cross-process seam between .NET and the Python curriculum-intelligence service. .NET writes Pending rows; Python polls, claims atomically (UPDATE ... WHERE Status='Pending'), and writes results. .NET BackgroundService advances document status on Done/Failed. No external broker required. See curriculum-system-of-record.md §4b for the upgrade path (broker swap)." Add `DbSet<PipelineJob>` to chosen DbContext. | `Domain/Entities/PipelineJob.cs`, `Domain/Enums/PipelineJobStatus.cs` (or string const), `Persistence/Configurations/PipelineJobConfig.cs`, chosen DbContext, `Migrations/AddPipelineJobsTable.cs` | BL-04 complete (module/schema established) | 3 | 🔲 |
| BL-01-BE-6 | `ListCurriculumDocumentsQuery` + handler: paged list, filterable by `Status`, `GradeId`, `SubjectId`. DTO `CurriculumDocumentListDto` + AutoMapper `ProjectTo`. Admin-only. Returns `BaseResponse<PagedList<CurriculumDocumentListDto>>`. | `Application/Features/CurriculumDocuments/Queries/ListCurriculumDocuments/**`, DTO, profile | BL-01-BE-2 | 3 | 🔲 |
| BL-01-BE-7 | `GetCurriculumDocumentQuery` + handler: detail view (all fields including Status, StatusReason, ObjectKey). DTO `CurriculumDocumentDto` + mapping. Admin-only. | `Application/Features/CurriculumDocuments/Queries/GetCurriculumDocument/**` | BL-01-BE-2 | 2 | 🔲 |
| BL-01-BE-8 | `CurriculumDocumentsController`: `[Route("api/curriculum/documents")]` (or `api/learning/...` in option B), `[Authorize(Roles="Admin")]`. Endpoints: `POST /` (upload — multipart), `GET /` (list), `GET /{id}` (detail). `NewResult(await Mediator.Send(...))`. Register assembly part in module's `AddXModule` if new module. | `Api/Controllers/CurriculumDocumentsController.cs` | BL-01-BE-4, BL-01-BE-6, BL-01-BE-7 | 2 | 🔲 |

## Frontend tasks (DEFERRED)

> The admin upload UI (file picker + metadata form + ingestion-queue table with live status) is a **Phase-7-style admin surface**. The admin console frontend is **not started**. This is post-MVP and deferred until the admin FE wave is scoped. A frontend task file (`BL-01-FE.md`) will be created when the FE wave is planned. The backend API shapes (POST multipart `api/curriculum/documents`, GET list/detail with `Status` field) are the interface contract.

## Acceptance-criteria coverage

- AC 1 — Upload PDF/DOCX/image with metadata (grade, subject, language, country) → **BE-4**
- AC 2 — Type allow-list + magic-byte validation + size cap; invalid → 422 → **BE-3, BE-4**
- AC 3 — Admin-only `[Authorize]` → **BE-8** + security-auditor gate
- AC 4 — Durable storage via `IStorageService`; object key persisted → **BE-4**
- AC 5 — Document in `Received` status on queue; BL-02 trigger fired (via `PipelineJobs` DB-outbox row, not MediatR event) → **BE-4, BE-5, BE-9**
- AC 6 — Status visible via list/detail endpoints → **BE-6, BE-7, BE-8**

## Notes

- Reference implementation: `UploadAvatarCommandHandler` (`backend/src/Modules/Identity/.../Account/Commands/UploadAvatar/`). Mirror it exactly — do not invent a different pattern.
- `IStorageService` (`backend/src/Shared/Learnexia.Shared.Kernel/Abstractions/Storage/IStorageService.cs`) is platform-wide and injectable. Object DELETE is not in the contract (MVP trade-off).
- `ContentLanguage` enum already exists — reuse; do not duplicate.
- No cross-module FK: `GradeId`/`SubjectId` are plain int references (they live in `learning`; `curriculum` module must not carry an EF nav to them if in a separate module).
- **Security-auditor gate is REQUIRED** (file upload + admin authz surface) before reviewer.
- **api-tester stage required** (HTTP endpoints exposed).
