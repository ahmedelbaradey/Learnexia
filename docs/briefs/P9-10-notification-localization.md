# Pipeline Brief — P9-10 Notification localization

> Source of truth: [user-stories/Phase-9-Notifications/P9-10-notification-localization.md](../../user-stories/Phase-9-Notifications/P9-10-notification-localization.md)
> Tasks: [tasks/Backend/Phase-9-Notifications/P9-10-BE.md](../../tasks/Backend/Phase-9-Notifications/P9-10-BE.md)
> Analyzer: grounded in current code (2026-06-20). **Backend-only. Must not block on FE (P9-03) or on the pending P6-06.**

## Summary & traceability
- **One-line task:** Make "render every notification (push / in-app inbox / email) in the recipient's selected language" a single consistent rule across **all** notification paths — closing the gap that re-engagement nudges already localize but **welcome + system + email** paths are hardcoded English.
- **User story:** P9-10 — "Get every notification in my selected language" (SP 5).
- **FR-IDs / goals:** FR-GM-8 (habit loop / re-engagement); SRS localization (ar/en, ar-first); BRD G4 (engagement/retention). Complements (does NOT duplicate) **P6-06** (transactional-email localization).
- **Phase/sprint:** Phase 9 — Notifications (post-MVP). Epic: Notifications Module.
- **Product overrides in force:** child notification copy uses **`PreferredLanguage`** (UI language), **never** P8 `LearningLanguage` (curriculum medium). Parent-driven onboarding; no teacher role (no impact here).

## Business context & value
- **Who benefits:** every recipient — Arabic-first parents and students. Today an Arabic-first user who registers receives an English "Welcome to Learnexia" inbox item + English welcome email, and any password-reset email is English-only. That breaks the Arabic-first promise on the very first touch.
- **Value:** consistency and trust on the first notification a user ever sees (welcome), and on the channels they rely on (push/inbox/email). Re-engagement nudges already do this correctly; this story removes the inconsistency for the non-re-engagement paths.
- **Success measure:** no user-facing notification string is emitted in a single hardcoded language; locale resolution is centralized through one seam; unit tests prove ar-EG/en-US selection + graceful fallback.

## Current-state investigation (verified, cited)

### 1. Re-engagement localization seam — CONFIRMED working
- Locale resolved by `ReengagementHandlerHelper.GetLocaleAsync(IUserLookup?, userId, ct)` — [ReengagementHandlerHelper.cs:80](../../backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Application/IntegrationEventHandlers/Reengagement/ReengagementHandlerHelper.cs). It calls `IUserLookup.FindByIdAsync(userId)` and reads `user.PreferredLanguage`; falls back to const `DefaultLocale = "ar-EG"` when `userLookup` is null, the field is null/blank, or the lookup throws (full try/catch).
- Source of `PreferredLanguage`: the Identity `User.PreferredLanguage` column — `character varying(10)`, **DB default `"ar-EG"`**, surfaced cross-module by `UserLookup.FindByIdAsync` → `new UserSummary(..., user.PreferredLanguage)` ([UserLookup.cs:26](../../backend/src/Modules/Identity/Learnexia.Modules.Identity.Infrastructure/Services/UserLookup.cs)). `IUserLookup`/`UserSummary` defined in [IUserLookup.cs](../../backend/src/Shared/Learnexia.Shared.Contracts/Identity/IUserLookup.cs); `PreferredLanguage` is nullable on the contract, callers fall back to `ar-EG`.
- Copy templating: `ReengagementCopyTemplates.Render(category, code, locale, placeholders)` → static dictionary keyed `"{category}:{code}:{locale}"`, both `ar-EG` (primary) + `en-US`, with `{name}` placeholder substitution. Two-stage fallback: missing key → en-US for same code → generic `("New notification", "You have a new notification.")`. Locale normalized by prefix (`ar*` → ar-EG, else en-US). [ReengagementCopyTemplates.cs](../../backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Domain/Templates/ReengagementCopyTemplates.cs).
- **This is the seam to reuse — do NOT fork it.**

### 2. Welcome + system paths — CONFIRMED hardcoded English
- `UserRegisteredIntegrationEventHandler` ([handler](../../backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Application/IntegrationEventHandlers/UserRegisteredIntegrationEventHandler.cs)): line 45-46 are literal English —
  `const string title = "Welcome to Learnexia";` / `var body = $"Welcome {UserName}! Your account has been created.";`
  It writes the inbox row via `INotificationInboxService.WriteWelcomeIfAbsentAsync` and then best-effort emails the same English strings. **Crucially, it already calls `IUserLookup.FindByIdAsync` in `TrySendWelcomeEmailAsync`** — so `PreferredLanguage` is already on hand; localizing welcome needs no new lookup and no contract change.
- `Notification.CreateWelcome(...)` factory already stamps `Code = "WELCOME"`, `Category = System (6)`, `Type = Welcome` ([Notification.cs:74](../../backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Domain/Entities/Notification.cs)). So the welcome row is template-ready; only the title/body strings need to come from a template instead of literals.
- **Sweep of every user-facing-copy producer in the module:**
  | Producer | Channels | Localized today? |
  |---|---|---|
  | 11 re-engagement integration handlers (`...IntegrationEventHandlers/Reengagement/*`) | inbox + push | YES — via `GetLocaleAsync` + `ReengagementCopyTemplates` |
  | `UserRegisteredIntegrationEventHandler` (welcome) | inbox + email | **NO — hardcoded English** |
  | `PasswordResetRequestedIntegrationEventHandler` (reset email) | email only | **NO — hardcoded English** (`EmailSubject` const + inline English body) |
  | `SendNotificationCommandHandler` | email only | **NO — passes `request.Title`/`request.Body` straight through** (no localization; copy supplied by caller) |
- `NotificationCategory.System = 6` already exists — the natural category for `WELCOME` templates.

### 3. Email channel — CONFIRMED un-localized
- Single seam `IEmailSender.SendAsync(to, subject, htmlBody, ct)` ([IEmailSender.cs](../../backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Application/Abstractions/IEmailSender.cs)); adapter chosen by config (`SmtpEmailSender` / `LogEmailSender`). The seam takes already-rendered subject+body — **localization is the caller's job**, and no caller localizes today.
- Welcome email: `UserRegisteredIntegrationEventHandler.TrySendWelcomeEmailAsync` sends the same hardcoded English title/body.
- Reset email: `PasswordResetRequestedIntegrationEventHandler` builds an inline English body + English `EmailSubject` const (line 25 explicitly flags "no string-localizer wired into this email path yet; templating is a follow-up").
- `SendNotificationCommandHandler` is a thin email passthrough — does not localize.

### 4. Inbox storage/read — CONFIRMED frozen send-time text
- `Notification` entity HAS `Code` + `Data` (jsonb, default `"{}"`) columns (P4-09) — confirmed [Notification.cs:45,51](../../backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Domain/Entities/Notification.cs).
- The inbox read path returns **frozen send-time `Title`/`Body`**: `InboxController GET /api/Notifications/Inbox/Me` → `ListMyInboxQuery` → `ListMyInboxQueryHandler` → `INotificationInboxService.ListInboxAsync` → `InboxItemDto`. `InboxItemDto` carries `Title`, `Body`, `Category`, `Code`, `Data` ([InboxItemDto.cs](../../backend/src/Modules/Notifications/Learnexia.Modules.Notifications.Application/Features/Reengagement/Dtos/InboxItemDto.cs)) and its own doc comment says **"the backend does not localise here"** — the FE is expected to render from `Code`+`Data`. **Nothing re-renders from Code+Data today**; the stored Title/Body are returned verbatim.

---

## FORK RESOLUTIONS

### Fork A — P6-06 overlap (email). RESOLUTION: P9-10 owns welcome-content localization (inbox + welcome email, self-contained); P6-06 owns the **reset email** + email infra. Defer reset-email localization to P6-06.

**Evidence:**
- **P6-06 is NOT started** — `🔲` in both [tasks/PROGRESS.md](../../tasks/PROGRESS.md) and HANDOFF; HANDOFF explicitly lists "localize reset + welcome emails (English-only) — ⏳ P6-06". P6-06-BE-2 reads: *"Localize transactional emails: carry the recipient locale on the welcome (`UserRegisteredIntegrationEvent`) + reset (`PasswordResetRequestedIntegrationEvent`) events (or resolve via `IUserLookup`); render subject/body via the localizer (ar/en) in the Notifications consumers."* → **P6-06-BE-2 and P9-10-BE-3 textually overlap on both welcome AND reset emails.**
- This is a real duplication risk. The line must be drawn by **what each can deliver without a contract change**:
  - **Welcome:** `UserRegisteredIntegrationEvent` carries no email/locale, **but the welcome handler already resolves `IUserLookup.FindByIdAsync`** (which returns `PreferredLanguage`). So localizing the welcome **inbox row + welcome email** is fully self-contained in the Notifications module — **no Shared.Contracts change, no Identity change**. P9-10 can and should own this now.
  - **Reset:** `PasswordResetRequestedIntegrationEvent` carries `Email + ResetUrl + UserName` only — **no `UserId` and no `Locale`** ([PasswordResetRequestedIntegrationEvent.cs](../../backend/src/Shared/Learnexia.Shared.Contracts/Identity/PasswordResetRequestedIntegrationEvent.cs)). `IUserLookup` is `FindByIdAsync(int)` only — there is **no lookup-by-email seam**. So localizing the reset email **requires a Shared.Contracts/Identity change** (add `Locale` to the event, or add an Identity lookup-by-email seam). That is precisely **P6-06's** charter (it explicitly says "may add a `Locale` field to the existing welcome/reset events"), and it also bundles the timing-oracle decouple that touches the same reset path.

**Decision — exact line:**
- **P9-10 owns:** the **welcome notification copy** (inbox title/body) + the **welcome email** subject/body localization — both via `IUserLookup.PreferredLanguage` already on hand; plus `SendNotificationCommandHandler` (clarify it stays a caller-localized passthrough — see note). **No contract change.**
- **P6-06 owns:** the **password-reset email** localization (needs the event `Locale` field / lookup-by-email seam), the timing-oracle decouple, and email transport/secret/header hygiene (HTTPS gating, env secrets, Redis rate-limit store).
- **Do NOT touch `PasswordResetRequestedIntegrationEventHandler` in P9-10.** Localizing it now would force a premature Shared.Contracts change that P6-06 must make anyway with the timing-oracle work — duplication and a likely merge conflict. P9-10 leaves a one-line code comment + brief note pointing to P6-06-BE-2.
- **Net effect on P9-10-BE-3:** scope **shrinks** from "all email" to "welcome email only" (reset email explicitly deferred to P6-06). This keeps P9-10 unblocked even though P6-06 hasn't started.

### Fork B — Inbox render-time policy (BE-4). RESOLUTION: localize **at send time** now (backend-only, no contract change); document re-localize-on-read as the forward target owned by/awaiting P9-03 FE.

**Evidence:**
- The richer option (store `Code`+`Data`, **re-localize on read** so a language switch retranslates historical inbox items) would change the inbox read contract / behavior and is designed to be consumed by the **FE inbox (P9-03)**, which is owned by a **different lead and NOT built**. P9-10-BE-4's own dep is listed as `P9-03-FE (consumer)`. Making P9-10 deliver read-time re-localization now creates a **blocking FE dependency** — violating the hard "backend-only, must not block on FE" constraint.
- Send-time localization is concrete and self-contained: once BE-1/BE-2 land, the welcome row is **written in the recipient's language at send time** (the existing re-engagement rows already are). The inbox returns frozen-but-correct-language text. No contract change, no FE dependency.
- Read-time re-localization is **not cheap-and-contract-compatible** here: it would require the read path to re-run template rendering from `Code`+`Data` per row (and the `Data` payloads for legacy/welcome rows are `"{}"` — placeholders like `{xp}`/`{badgeCode}` aren't all persisted in `Data` today, so a faithful re-render would also need a `Data`-population audit across producers). That is a larger change than this story's 5 SP and properly belongs with the P9-03 FE work that consumes it.

**Decision:**
- **v1 (this story):** localize **at send time**. The welcome fix lands; every newly written inbox row is in the recipient's `PreferredLanguage`. Inbox read stays as-is (returns stored Title/Body) — **no read-contract change, no FE dependency.**
- **Documented forward target (deferred, NOT dropped):** re-localize-on-read from `Code`+`Data` so a mid-stream language switch retranslates history. Owned by / sequenced with **P9-03 FE** (the consumer). BE-4 in this story is reduced to a **decision note** (this section) + a code comment on `InboxItemDto`/`ListMyInboxQueryHandler` recording the policy and the forward target — no read-path code change.
- Trade-off recorded: send-time text means a user who switches language **after** a notification was sent sees the old item in the old language until P9-03 ships read-time re-localization. Accepted for v1 (matches existing re-engagement behavior; the `Code`+`Data` columns are already in place to enable the upgrade later without a migration).

---

## Acceptance criteria (testable — what the reviewer checks)
1. **Single locale rule:** every non-re-engagement notification that has a recipient resolves locale through the **same `GetLocaleAsync` seam** (recipient `PreferredLanguage` → `ar-EG` fallback). No new per-handler ad-hoc locale logic. (BE-1)
2. **Welcome templated:** `UserRegisteredIntegrationEventHandler` no longer contains hardcoded English; welcome title/body come from a new `System:WELCOME:ar-EG` + `:en-US` template in `ReengagementCopyTemplates`, rendered with `{userName}`. Both inbox row and welcome email use the rendered, locale-correct copy. (BE-2, BE-3)
3. **No remaining hardcoded single-language user-facing copy** in the module **except** the password-reset email (explicitly deferred to P6-06 with a code comment + note). The sweep table above is the checklist. (BE-2)
4. **Welcome email localizes** subject+body in the recipient's language before `IEmailSender.SendAsync`, reusing the `PreferredLanguage` already resolved in the handler. (BE-3)
5. **Language source documented + guarded:** child notification copy uses `PreferredLanguage` (UI language), **never** `LearningLanguage` (P8). A code comment + brief in this brief states the rule; `GetLocaleAsync` (the only locale source) reads `PreferredLanguage` exclusively — verified there is no path that feeds `LearningLanguage` into copy. (BE-5)
6. **Inbox render-time policy documented + consistent:** send-time localization chosen; read-time re-localization deferred to P9-03; recorded as a code comment on the inbox read DTO/handler + this brief. No inbox read-contract change. (BE-4)
7. **Graceful fallback:** unknown/missing locale → en-US → generic copy; never an empty or key-leaking string. Covered by unit tests over `ReengagementCopyTemplates` resolution (existing two-stage fallback already does this; add WELCOME coverage). (BE-6)
8. **All new copy carries both ar-EG (primary) + en-US**; child-safe, encouraging tone (BRD §8). (story AC7)

## Affected modules & data
- **Module:** Notifications only (Application + Domain). **No new entities, no migration, no new columns** — `Code`/`Data` already exist; `WELCOME` code + `System` category already exist; `PreferredLanguage` already flows via `IUserLookup`.
- **No Shared.Contracts change** in P9-10 (the welcome path resolves locale via the existing `IUserLookup`). The reset-event `Locale` field is **P6-06's** change, not this story's.

## Handoff → db-migration
- **None.** No schema change. This story is pure Application/Domain copy + locale wiring. (Skip the db-migration stage entirely.)

## Handoff → backend-feature
- **BE-1 (locale unification):** confirm `GetLocaleAsync` is the single locale source for ALL recipient-bound notifications. `ReengagementHandlerHelper` is `internal static` in the Application project — the welcome handler is in the same project, so it can call `GetLocaleAsync` directly (it already injects `IServiceProvider` and resolves `IUserLookup`; prefer injecting `IUserLookup` directly like the re-engagement handlers, or keep the existing service-locator and pass the resolved `IUserLookup` into `GetLocaleAsync`). **Do not duplicate** the `ar-EG` fallback constant — reuse the helper.
- **BE-2 (welcome template):** add to `ReengagementCopyTemplates.Templates`:
  - `System:WELCOME:ar-EG` and `System:WELCOME:en-US`, with a `{userName}` placeholder, child-safe ar-first copy (ar primary). Then in `UserRegisteredIntegrationEventHandler.Handle`: resolve locale, `Render(NotificationCategory.System, "WELCOME", locale, ("userName", notification.UserName))`, and pass the rendered title/body to both `WriteWelcomeIfAbsentAsync` and the email send. Remove the two literal strings. Keep the existing idempotency (`WriteWelcomeIfAbsentAsync`) and fail-soft email isolation.
  - Sweep result is already done (table above) — only welcome needs templating; reset is deferred.
- **BE-3 (welcome email localized):** the welcome email already runs inside `TrySendWelcomeEmailAsync` which resolves `IUserLookup` (so `PreferredLanguage` is available without a second call) — render subject/body from the same WELCOME template + locale and pass to `IEmailSender.SendAsync`. **Scope-limited to welcome** (reset → P6-06). For `SendNotificationCommandHandler`: it is a generic email passthrough whose copy is supplied by the caller; add a code comment clarifying that callers must supply locale-correct copy (it has no recipient-id to resolve a locale from). **No reset-email change.**
- **BE-4 (inbox policy):** code comment on `InboxItemDto` + `ListMyInboxQueryHandler` recording "send-time localization; read-time re-localization deferred to P9-03 (consumer)". No read-path code change.
- **BE-5 (language-source guard):** code comment at the locale-resolution call site(s) stating copy uses `PreferredLanguage` (UI), never `LearningLanguage` (P8). No code change beyond the comment — `GetLocaleAsync` already reads only `PreferredLanguage`.
- **BE-6 (fallback + tests):** unit tests (xUnit, mirror existing template tests if any) over `ReengagementCopyTemplates`: WELCOME resolves ar-EG and en-US; unknown locale → en-US; missing code → generic non-empty, non-key-leaking copy; `{userName}` substitution. No DB needed (pure static).
- **Hard rules to honor:** module isolation (no cross-module project refs; cross-module only via `IUserLookup` in Shared.Contracts — already used); `ILoggerManager` not `ILogger<T>`; rule 8 — **extend `ReengagementCopyTemplates`, do not invent a new templating abstraction**; copy carries BOTH ar-EG + en-US (ar primary); no hardcoded single-language user-facing strings (except the explicitly-deferred reset email). Use the **template** shape for notification COPY (title/body/subject), **not** resx/`SharedResourcesKey` — resx is reserved for API-envelope `BaseResponse.Message` text (CONVENTIONS §10).

## Handoff → frontend
- **None for P9-10.** Backend-only story. The read-time re-localization upgrade (Fork B forward target) is consumed by **P9-03 FE** (different lead, not built) — flagged, not scheduled here.

## Open questions / assumptions / risks
- **OQ-1 (Fork A confirm):** Confirm the lead is OK with P9-10 owning welcome-email localization and **deferring reset-email localization to P6-06** (rather than P9-10 doing both). Recommended: yes — reset needs a Shared.Contracts change P6-06 must make anyway. *(Assumption used in this brief.)*
- **OQ-2 (Fork B confirm):** Confirm send-time localization for v1 with read-time re-localization deferred to P9-03. Recommended: yes — read-time path creates a blocking FE dependency + needs a `Data`-population audit. *(Assumption used in this brief.)*
- **OQ-3 (welcome email = same template?):** Assumed the welcome email subject = WELCOME title and body = WELCOME body (plain). If a richer HTML welcome email is wanted, that's extra copy — flag, don't invent. *(Assumption: reuse the WELCOME template for both inbox + email.)*
- **OQ-4 (SendNotificationCommandHandler):** It has no recipient user-id to resolve a locale from (only `RecipientEmail`/`RecipientUserId` int + caller-supplied copy). Assumed it stays a caller-localized passthrough (comment only). Confirm no current caller relies on it for system copy that should be templated.
- **Risk — P6-06 sequencing:** if P6-06 lands first and adds a `Locale` field to `UserRegisteredIntegrationEvent`, P9-10's welcome change must use it rather than re-resolving via `IUserLookup`. Low risk (P6-06 not started; welcome handler already resolves the user). The committer must rebase if P6-06 merges first; they touch the same handler.
- **Assumption — locale format:** templates normalize by `ar*`/else prefix, so both `"ar"` and `"ar-EG"` resolve correctly; `PreferredLanguage` is stored as full culture (`ar-EG`/`en-US`). No mismatch.

## Recommended pipeline order (first cut — planner finalizes)
1. **No db-migration** (no schema change) — skip the stage.
2. **backend-feature** — single batch (all six BE tasks are in one module, tightly coupled): BE-1 (locale unification) → BE-2 (WELCOME template + welcome handler) → BE-3 (welcome email, scoped per Fork A) → BE-4/BE-5 (doc comments) → BE-6 (unit tests). One agent, sequential within the batch.
3. **api-tester** — **optional/low-value.** No new endpoint; welcome is event-driven (hard to drive over HTTP). The unit tests in BE-6 are the primary safety net. Recommend skipping api-tester unless the planner wants an integration smoke that a registration produces an ar-EG welcome inbox row.
4. **security-auditor** — not required (no auth/authz/PII/file-upload/AI/secrets surface; locale read is already exposed). Skip unless the lead wants it.
5. **reviewer** — gate against ACs 1-8 + CONVENTIONS (rule 8, ILoggerManager, module isolation, ar-first copy, no hardcoded single-language strings except deferred reset).
6. **committer** — branch `feat/P9-10-notification-localization`, conventional commit, push + PR. Update HANDOFF: welcome localized; reset-email localization remains P6-06; read-time re-localization deferred to P9-03.
