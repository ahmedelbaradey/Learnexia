# BL-01 (Backend) — Upload curriculum documents with metadata

> Story: [../../../user-stories/Backlog-Phase-2-Plus/BL-01-upload-curriculum-documents.md](../../../user-stories/Backlog-Phase-2-Plus/BL-01-upload-curriculum-documents.md)
> Backlog Phase 2+ — Curriculum Intelligence · Epic: Curriculum Intelligence · SP: 3 · FR-CI-1 · Est. unit: hours
> **Backend only (admin upload + queue + status). Frontend (admin upload UI) is DEFERRED — Phase-7-style admin surface, not started; confirm with lead.**
>
> **Cross-cutting system decisions (A–E) are in `docs/briefs/curriculum-system-of-record.md`.** BL-01 is not directly impacted by Decisions B/C/D/E (those are BL-04/BL-05/BL-03 concerns), but implementers must be aware that: uploaded documents become the root of the provenance tree (Decision B — `ContentSource` created later by BL-02); version context is Draft until publish (Decision C — BL-05 sets this). BL-01 itself only stores and queues.

> **Status: 🔲 Not started** — `CurriculumDocument` entity + upload command (mirror `UploadAvatarCommandHandler`) + list/detail queries + BL-02 trigger event. Lands in whichever module BL-04 establishes.

> **LEAD DECISIONS REQUIRED (inherit from BL-04 + BL-01 open questions):**
> - **Q1 — Module placement:** inherits BL-04's new-`curriculum`-module decision. Do not scaffold independently.
> - **Q2 — Queue model:** status field on `CurriculumDocument` as the queue (recommended) vs. a separate queue/job table?
> - **Q3 — .NET-vs-Python boundary / trigger mechanism:** integration event consumed by a Python AI Gateway, background hosted service, or message broker? Shapes BL-01's `CurriculumDocumentUploaded` trigger.
> - **Q4 — Allowed types & max file size:** PDF + DOCX + images confirmed? Max file-size cap (textbooks can be 50–100 MB)?
> - **Q5 — FE deferral confirmed:** build backend + queue only now; admin upload UI deferred.

## Backend tasks (.NET)

| ID | Task | Artifact | Deps | Est (h) | Status |
|---|---|---|---|---|---|
| BL-01-BE-1 | Add `CurriculumDocument` domain entity: `Id`, `FileName`, `ObjectKey` (storage key — never a URL), `ContentType`, `FileSize`, `GradeId` (int), `SubjectId` (int), `Language` (`ContentLanguage` enum — reuse existing), `Country` (string), `DocumentStatus` enum (`Received`/`Processing`/`Done`/`Failed`), `StatusReason` (string, nullable diagnostics), timestamps. Derive from `AggregateRoot`, mirror `Skill.cs`. Add `DocumentStatus` enum to `Domain/Enums/`. | `Domain/Entities/CurriculumDocument.cs`, `Domain/Enums/DocumentStatus.cs` | BL-04 complete (module established) | 3 | 🔲 |
| BL-01-BE-2 | EF config `CurriculumDocumentConfig.cs`: `ToTable("CurriculumDocuments", schema)`, index on `Status` (queue scans), composite index on `(GradeId, SubjectId)` (admin filtering), `ObjectKey` required. Add `DbSet<CurriculumDocument>` to chosen DbContext. Npgsql migration `AddCurriculumDocumentTable`. | `Persistence/Configurations/CurriculumDocumentConfig.cs`, chosen DbContext, migration | BL-01-BE-1 | 3 | 🔲 |
| BL-01-BE-3 | `CurriculumFileValidator`: extend the `AvatarImageValidator` magic-byte pattern to support PDF (`%PDF-`), DOCX (PK ZIP header), and common image signatures (JPEG/PNG/WEBP). Config-driven max file-size cap (`CurriculumUploadConfiguration.MaxFileSizeBytes`). Return a localized validation failure, not a 500. | `Application/Features/CurriculumDocuments/Validators/CurriculumFileValidator.cs`, `Infrastructure/Configuration/CurriculumUploadConfiguration.cs` | BL-01-BE-1 | 3 | 🔲 |
| BL-01-BE-4 | `UploadCurriculumDocumentCommand` + handler: accept file (`IFormFile` at controller boundary, passed as `Stream`/metadata into the command) + metadata (GradeId, SubjectId, Language, Country). Validate via `CurriculumFileValidator`. Upload via `IStorageService` to `curriculum` bucket with a GUID object key. Persist `CurriculumDocument` with `Status = Received`. Mirror `UploadAvatarCommandHandler` exactly (validation order, `ServerError<T>` on exception, store key not URL). | `Application/Features/CurriculumDocuments/Commands/UploadCurriculumDocument/UploadCurriculumDocumentCommand.cs`, `...Handler.cs`, `...Validator.cs` | BL-01-BE-2, BL-01-BE-3 | 5 | 🔲 |
| BL-01-BE-5 | On successful save in the upload handler: publish a `CurriculumDocumentUploaded` integration event (via `Shared.Contracts` / existing messaging seam) OR enqueue a background job — per lead decision Q3. Define the `CurriculumDocumentUploaded` event record in `Shared.Contracts`. | `Shared/Learnexia.Shared.Contracts/IntegrationEvents/CurriculumDocumentUploaded.cs`, wiring in handler | BL-01-BE-4 | 3 | 🔲 |
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
- AC 5 — Document in `Received` status on queue; BL-02 trigger fired → **BE-4, BE-5**
- AC 6 — Status visible via list/detail endpoints → **BE-6, BE-7, BE-8**

## Notes

- Reference implementation: `UploadAvatarCommandHandler` (`backend/src/Modules/Identity/.../Account/Commands/UploadAvatar/`). Mirror it exactly — do not invent a different pattern.
- `IStorageService` (`backend/src/Shared/Learnexia.Shared.Kernel/Abstractions/Storage/IStorageService.cs`) is platform-wide and injectable. Object DELETE is not in the contract (MVP trade-off).
- `ContentLanguage` enum already exists — reuse; do not duplicate.
- No cross-module FK: `GradeId`/`SubjectId` are plain int references (they live in `learning`; `curriculum` module must not carry an EF nav to them if in a separate module).
- **Security-auditor gate is REQUIRED** (file upload + admin authz surface) before reviewer.
- **api-tester stage required** (HTTP endpoints exposed).
