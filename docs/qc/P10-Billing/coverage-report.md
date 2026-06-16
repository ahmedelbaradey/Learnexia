# P10-Billing — Coverage Report

**Story/scope:** Billing module money paths, post Option-C (service-only) refactor on `refactor/optionc-billing`.
**Nature:** Behavior-preserving refactor — these cases pin the *observable* money behavior so `api-tester` can prove the refactored services still honor the LOCKED energy economy end-to-end.
**Companion:** `docs/qc/P10-Billing/backend-test-cases.md` (75 cases, BE-TC-01..75).

## Summary

- **Total cases:** 75 (all backend / API-integration; this story has no new student-app UI surface, so no `frontend-test-cases.md`).
- **By priority:** P0 = 36, P1 = 27, P2 = 12.
- **By group:** A Spend/debit-on-delivery (13) · B Caps (4) · C Idempotency (5) · D Concurrency (3) · E Subscription (12) · F Webhook security (6) · G Packs/refunds (9) · H GlobalSettings (10) · I IDOR/authz (8) · J Envelope/validation (5).

## Coverage matrix — money invariants × test cases

| Invariant | Description | Covering cases | Status |
|---|---|---|---|
| INV-1 | HIT and MISS both charge | BE-TC-01, 02, 12 | Covered (P0) |
| INV-2 | Pre-authorize; debit only on delivery | BE-TC-01, 06 | Covered (P0) |
| INV-3 | No delivery → no debit; failures free | BE-TC-06, 07, 08, 09, 10 | Covered (P0/P1) |
| INV-4 | Per-intent costs Hint1/WhyWrong2/Explain3/Practice5 from GlobalSettings | BE-TC-01, 03, 04, 05, 54 | Covered (P0/P1) |
| INV-5 | Monthly HARD limit blocks | BE-TC-06, 13, 14, 24 | Covered (P0) |
| INV-6 | Daily SOFT (no block unless HardStop) | BE-TC-15, 16, 17 | Covered (P0/P1) |
| INV-7 | Idempotency / no double-charge | BE-TC-18, 19, 22, 34, 51 | Covered (P0) |
| INV-8 | Optimistic concurrency (xmin retry, never-negative) | BE-TC-23, 24, 25 | Covered (P0; 25 best-effort) |
| INV-9 | Granted-first / Mixed split | BE-TC-11, 12, 23 | Covered (P0) |
| INV-10 | Webhook idempotency (eventId dedupe) | BE-TC-20, 21, 74, 75 | Covered (P0/P1) |
| INV-11 | HMAC verified first; forged amount ignored | BE-TC-38, 39, 40, 41, 42, 43, 75 | Covered (P0) |
| INV-12 | Subscription activation + event | BE-TC-26, 30, 35, 36 | Covered (P0) |
| INV-13 | Server-side amount + two-save flush | BE-TC-27, 28, 29, 37, 44 | Covered (P0/P1) |
| INV-14 | Packs & refunds; clamp; no double-refund | BE-TC-44, 46, 47, 48, 49, 50, 51, 52 | Covered (P0) |
| INV-15 | GlobalSettings AdminOnly + cross-key validation | BE-TC-53..62 | Covered (P0) |
| INV-16 | IDOR / authz deny-by-default | BE-TC-44, 45, 63..70 | Covered (P0) |
| INV-17 | Envelope `Successed` + status mapping + 422 | BE-TC-12, 29, 47, 48, 60, 71, 72, 73, 74 | Covered (P0/P1) |
| INV-18 | Dunning / grace | BE-TC-31, 32, 33, 34 | Covered (P1) |

**Every locked-economy invariant has at least one P0 or P1 case. No invariant is uncovered.**

## Endpoint coverage

| Endpoint | Cases |
|---|---|
| AI Helper flow (HIT/MISS/no-debit, intra-process debit seam) | BE-TC-01..11, 15, 16, 17, 54 |
| `POST /Credits/Spend` | BE-TC-12, 13, 18, 19, 23, 24, 71 |
| `GET /Credits/{childId}` | BE-TC-63, 64, 65, 70, 73 |
| `GET /Credits/{childId}/Reconcile` | BE-TC-68 |
| `POST /Credits/Grant` | BE-TC-68 (authz); used as seed seam throughout |
| `GET /Energy/{childId}/Status` | BE-TC-15, 17, 66 |
| `POST /Subscription/Upgrade` | BE-TC-26 |
| `POST /Subscription/Checkout` | BE-TC-27, 28, 29, 70 |
| `POST /Subscription/Downgrade` / `Cancel` | BE-TC-35, 36 |
| `GET /Subscription/Current` | BE-TC-26, 30, 35, 36 |
| `GET /Plans/Comparison` | BE-TC-37 |
| `POST /Packs/Checkout` | BE-TC-44, 45, 70 |
| `GET /Billing/History` + `/Receipt/{id}` | BE-TC-67 |
| `POST /Admin/Billing/Refunds/{id}` | BE-TC-47, 48, 69, 72 |
| `GET/PUT /Admin/GlobalSettings` | BE-TC-53..62 |
| `POST /Webhooks/Provider` | BE-TC-20, 21, 30, 31, 32, 33, 34, 38..43, 46, 49..52, 74, 75 |

All 18 enumerated routes have at least one case.

## Gaps & known limitations (called out)

1. **G-1 — Direct cross-module debit seam (`ICreditSpendService.TryDebitAsync`) has no HTTP route.** The HIT/MISS/no-debit invariants (INV-1/2/3) are exercised end-to-end through the **AI Helper SSE endpoints + balance assertions** (BE-TC-01..11) and the admin `/Credits/Spend` ledger seam (BE-TC-12, 13, 18, 19, 23, 24). If `api-tester` cannot drive the AI Helper flow deterministically in the integration harness (depends on Ai module + Safety Layer fakes), cases BE-TC-01..05, 07..10, 15..17 should fall back to asserting the ledger/debit invariants via `/Credits/Spend` and mark the AI-flow-specific HIT-vs-MISS distinction (BE-TC-02) as the residual gap. **This is the one place where the locked economy's "HIT also charges" rule cannot be proven without the Ai flow — flag prominently if blocked.**
2. **G-2 — BE-TC-25 (retries-exhausted)** is non-deterministic; `MaxRetries=3` contention is hard to force reliably. Marked P2 / best-effort; record as blocked if not reproducible.
3. **G-3 — BE-TC-43 (empty signing secret)** requires a host variant with no secret. If the test harness boots a single fixed config, mark blocked.
4. **G-4 — Post-commit integration events** (`SubscriptionActivatedIntegrationEvent`, `PaymentFailedIntegrationEvent`) are fire-and-forget after commit. BE-TC-30/31 assert the DB state change directly; asserting the *event* itself requires an observable downstream consumer effect or an event-log probe — note as a soft assertion if no consumer hook is available in the harness.
5. **G-5 — `InitiateRefundValidator` reason bounds (BE-TC-72)** assumed (required, max 1000) from the controller XML doc; `api-tester` should confirm the actual rule before asserting the exact boundary.
6. **G-6 — Premium-tier daily/monthly caps** (premium_daily_cap=250, premium_monthly=5000) are covered structurally by the free-tier cases (same code path); a dedicated premium-tier cap case was not added because the cap value is resolved from settings and the block logic is tier-agnostic in `CreditSpendService`. Add one if the lead wants explicit premium-tier proof.

## Open questions for the lead (resolve before `api-tester` implements)

- **OQ-1:** Can the integration harness drive the AI Helper endpoints with deterministic Safety-Layer + cache fakes so BE-TC-01/02 (the HIT-vs-MISS-both-charge crux) can be proven? If not, do we accept the `/Credits/Spend` ledger-seam proxy as sufficient for the refactor sign-off (G-1)?
- **OQ-2:** Is `Billing:HardStopEnabled=true` runnable as a config variant in the test host (for BE-TC-16), and can a no-secret variant run (BE-TC-43)? If single-config only, those two are blocked.
- **OQ-3:** Is there an observable hook (consumer side-effect or event log) to assert post-commit integration events (G-4), or should BE-TC-30/31 assert only the committed DB state?
- **OQ-4:** Confirm the `InitiateRefundValidator` reason rule (G-5) so BE-TC-72 asserts the right boundary.

## Handoff

- `api-tester` implements `backend-test-cases.md` against the running API and writes pass/fail + defects into `execution-report.md`.
- There is no frontend surface in this scope, so no `frontend-e2e-tester` work and no `frontend-test-cases.md`.
