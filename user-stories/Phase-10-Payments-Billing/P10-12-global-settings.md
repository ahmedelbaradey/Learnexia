# Runtime-configurable AI economy via Global Settings

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Technical Enabler
- **Story Points:** 5 — DB-backed settings store + `IGlobalSettingsProvider` + Redis cache/invalidation + audit; consumed across two phases.
- **Labels:** `billing`, `ai`, `platform`, `config`, `enabler`
- **Requirements:** FR-PAY-7 *(new — Phase 10)*

## Description
As the platform, I want every AI credit-economy value and cache/review threshold stored in **DB-backed Global Settings** (not hardcoded), read through a single **`IGlobalSettingsProvider`** with Redis caching, so that Product/Admin can tune the economy after seeing real usage data **without a deployment**.

## Acceptance Criteria
- A `GlobalSetting` store (key, value, type, `UpdatedBy`, `UpdatedAt`) holds the managed values below; **safe defaults live in code/config only for bootstrap** — used when a key is absent from the DB.
- Consumers read values **only through `IGlobalSettingsProvider`** (typed getters), never via hardcoded constants or per-module `*Options`.
- Values are **cached in Redis/memory and invalidated on change** — no stale read after an admin update.
- Every change is **audited** (who / when / old → new) via the existing Moderation audit seam.
- **Managed keys** cover:
  - *Credit economy:* free monthly credits, premium monthly credits, free daily limit, premium daily limit, extra-pack price, credits-per-pack.
  - *Subscription pricing:* `subscription.monthlyPriceEgp` = 199, `subscription.annualPriceEgp` = 1990.
  - *Payment / FX:* `payment.providerFees` (payment-provider fee config — for margin/accounting; **unit flagged: % vs flat EGP — confirm with lead before seeding**), `fx.usdExchangeRateBuffer` (FX buffer for AI cost margin protection, AI costs are USD and pricing is EGP; **unit flagged: % multiplier vs flat buffer — confirm with lead before seeding**).
  - *AI action costs:* hint, explain-mistake, deep-explanation, practice-generation.
  - *Cache/review thresholds:* auto-approval confidence threshold, WhyWrong variant cap, practice pool size.
- Changes take effect at the **next read** (post-invalidation); economy grant/cost changes are **never applied retroactively** to past actions/charges.

## Notes
- **New shared infrastructure:** the `IGlobalSettingsProvider` contract lives in `Shared.Contracts`; the store + Redis cache in Shared infrastructure (schema `platform`/`settings`). **Module placement needs lead sign-off** (recommend Shared infra, NOT a new business module).
- **Cross-phase seam:** the `IGlobalSettingsProvider` *contract* + a **bootstrap-default implementation** can ship with the AI Helper (Phase 4) so the cache thresholds (P3-04/05/06) are coded against it from day one; **this story (P10-12) upgrades it to the DB-backed + Redis-cached + audited + admin-editable implementation** (mirrors the `ILearningContextProvider` Seeded→Rag pattern).
- **Consumed by:** P10-02 (grants), P10-03 (action costs), P10-04 (daily caps), P10-07 (pack price/size), P10-11 (admin write-surface), and P3-04/05/06 (cache thresholds). **P10-11 writes these via this provider** (supersedes the bespoke `BillingConfigVersion` mechanism).
- **Out of scope / flagged:** a "tutor session cost" key was requested in the example list but the Tutor-Session action was previously removed (AI-Helper-not-Teacher MVP) — key **omitted pending lead confirmation**.
- Admin-only edits, behind the P7/P10-11 admin console. Blocked by **P10-01** (Billing module / audit seam available).
