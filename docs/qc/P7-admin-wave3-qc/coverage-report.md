# P7 Admin Wave 3 — Coverage Report (Frontend E2E)

> Scope: frontend E2E test-case design for the **shipped + merged** admin-dashboard Wave 3 — **P7-09** moderation queue, **P7-12** audit-log viewer, **P7-13** gamification overrides. Backends are merged + tested; this run designs FE-only Playwright coverage against the `admin` project (port 3001). Design only — no executable code, no execution.

## Summary

Total authored frontend cases: **91** (MOD-TC 35 + AUD-TC 19 + GAM-TC 34 + XC-TC 3). Backend contract-smoke: 4 (reference only, not implemented here).

| Surface | Cases | P0 | P1 | P2 |
|---|---|---|---|---|
| P7-09 Moderation | 35 | 14 | 13 | 8 |
| P7-12 Audit | 19 | 7 | 8 | 4 |
| P7-13 Gamification | 34 | 13 | 13 | 8 |
| Cross-cutting | 3 | 0 | 2 | 1 |
| **Total** | **91** | **34** | **36** | **21** |

Priorities are indicative; the implementer may re-weight P1/P2 by available seed.

## Coverage matrix — acceptance criterion → case IDs

### P7-09 Moderation (9 AC)
| AC | Summary | Cases | Status |
|---|---|---|---|
| AC1 | Queue list paginated table, newest-first, keepPreviousData | MOD-TC-01, 11, 12, 14, 15 | Covered |
| AC2 | Filters + search (status/source/subject/grade/date/search), reset to p1 | MOD-TC-05, 06, 07, 08, 09, 10, 11, 13 | Covered |
| AC3 | Four list states (loading/empty/error/results); empty ≠ error | MOD-TC-02, 03, 04, 06 | Covered |
| AC4 | Detail: facets + SafetyVerdict (no raw content) + review fields + 404 | MOD-TC-16, 17, 18, 19, 20, 21 | Covered |
| AC5 | Review action Approve/Reject(reason ≤2000)/Flag | MOD-TC-22, 23, 24, 25, 26, 27 | Covered |
| AC6 | Pending/Flagged gating; terminal hides buttons; no re-flag | MOD-TC-22, 28, 29 | Covered |
| AC7 | No optimistic — invalidate+refetch; error keeps dialog open | MOD-TC-23, 25, 30, 31 | Covered |
| AC8 | AdminOnly guard + nav item | MOD-TC-33, 34 | Covered |
| AC9 | RTL+i18n+a11y (caption/scope, keyboard rows, dialog trap, aria-live) | MOD-TC-15, 32, 35 | Covered (RTL portion BLOCKED) |

### P7-12 Audit (AC 1–12)
| AC | Summary | Cases | Status |
|---|---|---|---|
| AC1 | `/audit` guarded; anon redirect, no flash | AUD-TC-16 | Covered |
| AC2 | Nav item active-aware | AUD-TC-17 | Covered |
| AC3 | Paginated read-only table, 4 columns, newest-first | AUD-TC-01, 10 | Covered |
| AC4 | Filters map to query params; reset to p1 | AUD-TC-05, 07, 08, 09 | Covered |
| AC5 | Four states + keepPreviousData + aria-live | AUD-TC-02, 03, 04, 10 | Covered |
| AC6 | Row expand detail; all fields; read-only; details text/JSON | AUD-TC-11, 12, 13 | Covered |
| AC7 | Action filter includes newer admin actions | AUD-TC-06 | Covered |
| AC8 | No write affordances anywhere | AUD-TC-13 | Covered |
| AC9 | EN+AR + RTL; ltr islands | AUD-TC-19 | Covered (BLOCKED-RTL) |
| AC10 | A11y: caption/scope, aria-live, keyboard expand | AUD-TC-18 | Covered |
| AC11 | PII safety — ids/states only, no name/email join | AUD-TC-15 | Covered |
| AC12 | Export — blocked/deferred (AC gap) | AUD-TC-14 | Covered as a negative (gap recorded) |

### P7-13 Gamification (10 AC)
| AC | Summary | Cases | Status |
|---|---|---|---|
| AC1 | Nav + guard; redirect; no flash | GAM-TC-01, 02, 03 | Covered |
| AC2 | Badge catalog list + create/edit + PATCH active; no delete | GAM-TC-04..12 | Covered |
| AC3 | Mission catalog same; Daily/Weekly only | GAM-TC-13..18 | Covered |
| AC4 | Timed events list + status derivation + create + activate/expire | GAM-TC-19..24 | Covered |
| AC5 | League-tier override (student-only, reason, no-op gated) | GAM-TC-25, 26, 27, 28 | Covered |
| AC6 | Streak-freeze grant (student-only, count 1–2, reason) | GAM-TC-25, 29, 30, 31 | Covered |
| AC7 | Reason-required pattern (both overrides) | GAM-TC-26, 27, 29, 30 | Covered |
| AC8 | RTL + bilingual + a11y | GAM-TC-32, 33, 34 | Covered (RTL portion BLOCKED) |
| AC9 | Audit — FE just calls audited endpoints (no FE audit UI) | GAM-TC-27, 30 (assert no admin id in body) | Covered indirectly |
| AC10 | No optimistic mutations — invalidate+refetch | GAM-TC-06, 09, 15, 17, 20 | Covered |

**Verdict: every acceptance criterion across P7-09 (9), P7-12 (12), P7-13 (10) has ≥1 P0/P1 case. No uncovered AC.**

## Gaps & caveats (must read before implementation)

1. **AC gap (by design) — P7-12 export (AC12).** No backend export endpoint exists. AUD-TC-14 asserts the *absence* of an export control. This is a recorded story-AC gap, not a defect — a backend `GET .../Log/Export` follow-up is required to satisfy the original "export the filtered log" AC. Do not implement a client-side single-page CSV.
2. **Live RTL is BLOCKED.** `ADMIN_LOCALE` is a build-time constant (`'en'`); there is no runtime locale toggle and no `ar` build in this run. MOD-TC-35, AUD-TC-19, GAM-TC-34 are **static checks only** (assert AR+EN strings + `dir="ltr"` islands in source). To truly verify RTL, a separate `ADMIN_LOCALE='ar'` build + run is needed — flag to the lead.
3. **Current league tier is hard-coded `null`** on `users/[id]` (`currentTier={null}` passed to `LeagueTierOverrideDialog`). GAM-TC-26 therefore treats "current tier" as Unknown — the no-op-prevention assertion is limited to the select excluding whatever `currentTier` is (here, nothing). Note in execution: the dialog shows `gamLeagueTierUnknown`. This matches brief Q1 (no reliable single-student tier read). Not a Wave-3 defect, but record it.
4. **Streak-freeze balance is "not available"** by design (brief Q2 — no read endpoint). GAM-TC-29 asserts the unavailable notice rather than a numeric balance.
5. **Create-response id may be 0** (brief Q3). All create cases (GAM-TC-06/15/20) assert the new row via **refetch**, never via the returned id — already baked into the steps.

## Test Data / Seeding (CRITICAL)

### Moderation items — the main seeding gap
The moderation queue is populated **only by AI-flagged safety events** (`AiOutputFlaggedEventHandler` → `ModerationQueueWriter`); there is **no admin "create moderation item" endpoint** and the queue may be **completely empty** in a fresh environment. The schema lives in `Learnexia.Modules.Moderation` (`ModerationItemConfig`, migration `P7_09_AddModerationItem`).

To run the seed-dependent cases (`[SEED-DEPENDENT]`: MOD-TC-01, 14, 16, 17, 19, 22–32), the tester needs ≥1 `ModerationItem` in each of the states **Pending, Flagged, Approved, Rejected**, ideally one with a populated `safetyVerdict` (failedChecks + reasonCodes). Options, in order of preference:
- **(a) DB seed (recommended for determinism):** insert rows directly into the Moderation schema's `ModerationItem` table with chosen `status`, `source=0 (AiOutput)`, a content reference, and a `safetyVerdict` JSON string like `{"failedChecks":["ToxicityCheck"],"reasonCodes":["UNSAFE_TONE"],"actionTaken":"Blocked","modelId":"gpt-x"}`. Then drive Approve→Approved, Flag→Flagged, etc. from the UI to produce the terminal/flagged variants.
- **(b) Trigger the AI safety path** (if a dev endpoint exists to force an unsafe AI generation) so the event handler writes a real item — closest to production but slower and less deterministic.
- **(c) Route interception fallback** — for pure FE-state cases (states, verdict rendering, error mapping, terminal gating), **intercept** the `Queue`/detail/`Review` responses with fabricated payloads. This is acceptable for MOD-TC-02/03/04/17/18/20/21/28/29/30 (state + gating logic) but does NOT prove end-to-end persistence (MOD-TC-23/25/27/31 need a real item).
- **(d) If none available:** mark the affected `[SEED-DEPENDENT]` cases **BLOCKED-NO-SEED** in `execution-report.md` with this note; still run the interception-based subset.

### Audit log — auto-populates
The audit log fills as admins act: **any admin mutation emits `AdminActionPerformedEvent`** → an `AuditLogDto` row. To guarantee ≥1 row for AUD-TC-01/10/11/15/18, perform a seeding mutation first (e.g. create a badge in GAM-TC-06, or create/deactivate any catalog item) — these Wave-3 actions themselves generate audit rows (`Gamification.*`, `Badge.*`). For empty/error/pagination states use interception (AUD-TC-02/03/04/10).

### Gamification catalogs — seeded
Badge/Mission/TimedEvent catalogs are seeded (`BadgeSeeder`/`MissionSeeder`/`TimedEventSeeder`), so list cases (GAM-TC-04/13/19) should find rows without extra seeding. For create/edit/toggle, **always use a unique `code`** per run to avoid duplicate-conflict 400s. For an **Active** timed event (GAM-TC-22) and an **Expired** one (GAM-TC-23), the seeder may not provide all states — create one with a past `startUtc` then activate, and create one with a past `endUtc`, or intercept the list to inject the needed windows.

### Student / parent accounts for overrides
GAM-TC-25/26/27/29/30 need a **Student** user id and (for GAM-TC-25 negative) a **Parent** id. Seed via the parent-onboarding API used in `tests/e2e/specs/P7-admin-batch1.spec.ts` (`Register-Parent` → add child). The child is the Student; the registering account is the Parent.

## Handoff notes

### `data-testid` inventory (all confirmed present in shipped code)
- **Moderation list:** `mod-table`, `mod-row-{id}`, `mod-col-{source|contentRef|subjectGrade|taskKind|status|detected}`, `mod-search-input`, `mod-status-filter`, `mod-source-filter`, `mod-subject-filter`, `mod-grade-filter`, `mod-date-from`, `mod-date-to`, `mod-clear-filters`, `mod-loading`, `mod-error-banner`, `mod-retry-btn`, `mod-empty-state`, `mod-pagination-prev/next`, `mod-page-indicator`.
- **Moderation detail:** `mod-detail-card`, `mod-facets-card`, `mod-verdict-section`, `mod-review-history`, `mod-review-actions-panel`, `mod-review-approve-btn`, `mod-review-reject-btn`, `mod-review-flag-btn`, `mod-terminal-notice`, `mod-detail-loading`, `mod-detail-error`, `mod-detail-back-btn`. Dialog: `review-item-dialog`, `dialog-confirm-btn`, `dialog-cancel-btn`, `review-reason-field`. Note: the post-success banner has **no dedicated testid** — assert on text (`modReviewSuccess`) / the `AdminErrorBanner variant=success`. The 404 not-found block has **no testid** — assert on `modNotFoundHeading` text.
- **Audit:** `audit-table`, `audit-table-wrapper`, `audit-row-{id}`, `audit-expand-{id}`, `audit-detail-{id}`, `audit-detail-copy-{id}`, `audit-filter-admin-id`, `audit-filter-action-type`, `audit-filter-target-type`, `audit-filter-date-from`, `audit-filter-date-to`, `audit-clear-filters`, `audit-loading`, `audit-error-banner`, `audit-retry-button`, `audit-empty-state`, `audit-result-count`, `audit-pagination-prev/next`, `audit-page-indicator`.
- **Gamification hub:** `gamification-hub`, `gam-hub-title`, `hub-card-{badges|missions|events}` (+ `-manage`), `student-overrides-notice`.
- **Badges:** `badge-catalog-page`, `badge-table`, `badge-row-{id}`, `badge-create-btn`, `badge-empty-create-btn`, `badge-retry-btn`, `badge-results-region`, `badge-edit-{id}`, `badge-activate-{id}`, `badge-deactivate-{id}`, dialogs `badge-form-dialog` (+ `badge-form-code/name/description/icon-key/rarity/trigger/threshold/reward-xp/sort-order/cancel/save`), `badge-activate-dialog`/`badge-activate-confirm-btn`, `badge-deactivate-dialog`/`badge-deactivate-confirm-btn`.
- **Missions:** `mission-table`, `mission-row-{id}`, `mission-edit-{id}`, `mission-activate-{id}`/`mission-deactivate-{id}` (verify exact suffix at runtime — confirmed `mission-form-*` testids: dialog/code/icon-key/title-key/cadence/target-type/target/reward-xp/sort-order/cancel/save).
- **Events:** `timed-events-page`, `event-table`, `event-row-{id}`, `event-create-btn`, `event-empty-create-btn`, `event-retry-btn`, `event-results-region`, `event-edit-{id}`, `event-activate-{id}`, `event-expire-{id}`, `event-activate-dialog`/`event-activate-confirm-btn`, `event-expire-dialog`/`event-expire-confirm-btn`, form `timed-event-form-dialog` (+ `event-form-code/name-en/name-ar/desc-en/desc-ar/scope/multiplier/start/end/cancel/save/edit-gap-notice`).
- **Overrides (users/[id]):** `gamification-overrides-card`, `league-tier-override-btn`, `grant-streak-freeze-btn`; dialogs `league-tier-override-dialog`/`league-tier-select`/`league-tier-confirm-btn`/`league-tier-reason` (ReasonField id), `grant-streak-freeze-dialog`/`freeze-count-input`/`freeze-reason`/`grant-freeze-confirm-btn`.

### Missing / weak testids to flag (do NOT block — use fallbacks)
- Moderation **success banner** and **404 not-found** block lack dedicated testids — assert on rendered string. (Suggest the frontend add `mod-review-success` and `mod-not-found` testids in a follow-up for robustness.)
- **Mission row activate/deactivate** action buttons — confirm exact testid suffix at runtime (badge page uses `badge-activate-{id}`/`badge-deactivate-{id}`; missions likely mirror but verify).
- Confirm-dialog **backdrop** element has no testid — click outside the `role="dialog"` card to test no-dismiss.

### Seed prerequisites summary
- Moderation: ≥1 item per state (DB seed preferred) OR route interception for state/logic cases. See Test Data.
- Audit: perform 1 admin mutation to guarantee a row (or reuse a GAM create).
- Gamification: catalogs seeded; use unique codes for writes; manufacture Active/Expired event states.
- Overrides: seed 1 parent + 1 child (Student) via `Register-Parent`.

### Execution
This is a **frontend** QC run: **`frontend-e2e-tester` implements `frontend-test-cases.md`** and writes pass/fail + defects into `execution-report.md`. The BE contract-smoke list is reference-only (already covered by the merged BE integration tests).
