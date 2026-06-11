# Get every notification in my selected language

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 5 — unify locale resolution + localize all notification channels (push / in-app / email).
- **Labels:** `notifications`, `backend`, `localization`, `i18n`, `habit`
- **Requirements:** FR-GM-8; SRS localization (ar/en); complements **P6-06** (transactional-email localization).

## Description
As a user (parent or student), I want every notification — push, in-app inbox, and email — rendered in **my selected language**, so that an Arabic-first user never receives English copy (or vice versa).

> **Why this story exists:** re-engagement nudges already localize via the recipient's `PreferredLanguage` (`ReengagementHandlerHelper.GetLocaleAsync` → `ReengagementCopyTemplates`, ar-EG/en-US). But the gap is the **non-re-engagement paths**: the welcome notification title/body are **hardcoded English** (`UserRegisteredIntegrationEventHandler` — "Welcome to Learnexia"), and `SendNotificationCommandHandler` / the welcome email pass un-localized strings straight to the `IEmailSender`. This story makes "render in the recipient's selected language" a single, consistent rule across **all** notification types and channels.

## Acceptance Criteria
- A **single locale-resolution rule** is applied for every notification: the recipient's selected UI/preferred language (`PreferredLanguage`), falling back to `ar-EG` (platform default) when unknown — reusing the existing `GetLocaleAsync` seam rather than per-handler ad-hoc logic.
- **All system/transactional notifications are templated** the same way re-engagement nudges are (code + ar/en template + placeholders) — starting with **welcome** (replace the hardcoded English) and any other system notifications, so no user-facing string is hardcoded in one language.
- **Email channel localizes** too: subject + body are rendered in the recipient's language before `IEmailSender.SendAsync` (coordinated with **P6-06**, which owns transactional-email localization — this story localizes the *content*, P6-06 localizes the *email infrastructure/headers*; avoid duplicate work).
- **Language source of truth is consistent** for child notifications: use the child's preferred/UI language (not learning language — P8 `LearningLanguage` governs curriculum medium, **not** notification copy); documented explicitly so the two are never confused.
- **Inbox render-time decision documented:** define whether stored inbox `Title`/`Body` stay in the send-time language or are re-localized on read (recommended: store the stable `Code` + `Data` and let the client localize on render, so changing language updates historical inbox items). Pick one, document the trade-off, keep it consistent.
- Missing-template / unknown-locale falls back gracefully (en-US → generic copy), never an empty or key-leaking string.
- All new/changed copy carries both ar-EG and en-US variants; ar is primary.

## Notes
- **Scope-definition only — will be developed later.** Builds on the existing `ReengagementCopyTemplates` + `GetLocaleAsync`; extends them to system/welcome + email paths. Coordinates with **P6-06** (don't duplicate email-infra localization). Child copy uses preferred/UI language, never `LearningLanguage`.
- `analyzer` + `planner` first per CLAUDE.md.
