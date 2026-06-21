# P7 Admin Wave 3 — E2E Execution Report (Moderation + Audit + Gamification)

> Filled by the frontend lead after implementing + running the Wave-3 admin E2E on the live stack.
> Per-surface detail: [`execution-audit.md`](./execution-audit.md) · [`execution-gamification.md`](./execution-gamification.md) · [`execution-moderation.md`](./execution-moderation.md)
> Source cases: [`frontend-test-cases.md`](./frontend-test-cases.md) · Coverage: [`coverage-report.md`](./coverage-report.md)

## Run metadata
| Field | Value |
|-------|-------|
| Date | 2026-06-20/21 |
| Branch | `test/P7-admin-wave3-e2e` (off main @ 6d55a0a) |
| Admin app | http://localhost:3001 (Next.js 15, `ADMIN_LOCALE='en'` build-time) |
| Backend | http://localhost:5080 (.NET 10 dev, seeded) |
| Playwright | `--config=playwright.admin.config.ts --workers=1` (admin-only project; reuses :3001, ignores :8081/:3002) |
| Auth | `superadmin` / `123Pa$$word!` |

## Result summary (combined run of all 3 specs)

| Status | Count |
|--------|------|
| PASS | 103 |
| FAIL | 0 |
| BLOCKED-RTL | 3 |
| **Total Playwright tests** | **106** |

Covers all designed cases: **MOD-TC-01..35, GAM-TC-01..34, AUD-TC-01..19** (~88 QC cases → 106 Playwright items; several cases have 2–4 sub-tests). Combined run: `103 passed / 3 skipped (15.7m)`, exit 0.

| Surface | Spec | PASS | FAIL | BLOCKED-RTL |
|---------|------|------|------|-------------|
| Audit (P7-12) | `P7-admin-audit.spec.ts` | 20 | 0 | 1 (AUD-TC-19) |
| Gamification (P7-13) | `P7-admin-gamification.spec.ts` | 45 | 0 | 1 (GAM-TC-34) |
| Moderation (P7-09) | `P7-admin-moderation.spec.ts` | 38 | 0 | 1 (MOD-TC-35) |

The 3 BLOCKED share one root cause: the admin app locale is a **build-time constant `'en'`** — runtime ar/RTL is unreachable, so each surface's RTL/bilingual case is blocked. AR strings verified statically in `lib/strings.ts` (+ `auditActionLabels.ts`); `dir`-pinned LTR islands confirmed in source. A runtime locale toggle is a separate story.

## Seeding approach
- **Audit** — 252 real seeded rows; empty/error states via `page.route()` interception.
- **Gamification** — throwaway badges/missions/timed-events created via the admin POST endpoints in test helpers (unique-timestamp codes), deactivated after; student/parent IDs found via `GET /api/Admin/Users?Role=…`.
- **Moderation** — the queue is empty and items **only** enter via the `AiOutputFlaggedIntegrationEvent` (no HTTP/seed endpoint reachable from the test layer). The 19 seed-dependent MOD cases use `page.route()` interception with synthetic queue/detail payloads matching the real wire shape; structure/filters/auth/a11y/validation cases run against the live empty surface. (Documented, not faked.)

## Defects found

### DEF-GAM-01 — **BACKEND, Medium** (GAM-TC-23)
`ExpireTimedEvent` calls `timedEvent.Deactivate()`, which only sets `IsActive=false` — it does **not** rewind `EndUtc` to now. The FE `deriveStatus` is timestamp-based (`now > endUtc → EXPIRED`), so an "expired" event still has a future `EndUtc` and renders as **SCHEDULED** with its edit button still enabled. Fix in `GamificationAdminService.ExpireTimedEventAsync`: after `Deactivate()`, set `EndUtc = DateTime.UtcNow` (or have the domain `Deactivate()` rewind `EndUtc`). → **for the backend lead.**

### Minor FE notes (non-blocking, mine — optional follow-ups)
- **Moderation date-range input**: the date filter stores an ISO datetime (`2026-06-20T00:00:00Z`) but feeds it as the `value` of an `<input type="date">`, which only accepts `YYYY-MM-DD`, so the picked date visually clears after selection (the API still receives the param). Format the `value` to `YYYY-MM-DD`.
- **`AdminConfirmDialog` ESC**: ESC is handled via `onKeyDown` on the dialog div, so it doesn't fire when focus is on the backdrop. A `useEffect` window keydown listener while open would make ESC unconditional.
- (Wire note, not a bug: moderation responses serialize enums as **strings** (`"Pending"`), while filter/action params are sent as **ints** — the FE handles both correctly.)

## Wire/infra notes
- The paginated-envelope normalization shipped in #200 (`api-client requestPaginated`) is load-bearing here too — audit + moderation lists are double-wrapped (`.data.data`); without it those pages crash.
