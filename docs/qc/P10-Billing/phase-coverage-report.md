# Phase 10 — Backend QC coverage report (phase-level matrix)

**Scope:** ALL Phase-10 backend stories — the money paths (**P10-01..12**) PLUS the family/seats cluster (**P10-13..18**) and the **payment-provider mock simulation**. This report reconciles the two QC efforts so the lead can see, per story, *who* QC'd it and *where the genuine gaps are*.

**Two QC efforts being reconciled:**
1. **Money-path QC (pre-existing):** `docs/qc/P10-Billing/` — `coverage-report.md` + `backend-test-cases.md` = **75 cases (BE-TC-01..75)** covering the LOCKED energy economy across P10-01..12 (spend/debit-on-delivery, caps, idempotency, concurrency, subscription, webhook security, packs/refunds, GlobalSettings, IDOR/authz, envelope/validation). **Not re-covered here.**
2. **Family/seats/sim QC (THIS run):** `docs/qc/P10-13..18/` + `docs/qc/P10-payment-sim/` — **110 cases mapped to existing integration tests** + **20 flagged gaps** for the `api-tester` to close. These stories are **already QC'd by integration tests**; this run is the *traceable design doc + coverage reconciliation*, mapping every case 1:1 to an existing `P10_1x` / `P10_PaymentSimulation` test and flagging genuine gaps.

> Spelling: the envelope success flag is **`Successed`** (do not rename). Validation 422 = `ICommand<>` bodies only.

## A. Who QC'd which story

| Story | Title | QC source | Existing integration tests | This run adds |
|-------|-------|-----------|----------------------------|---------------|
| P10-01 | Credit ledger + idempotency primitives | **Money-path QC** (`P10-Billing`) | `P10_01_12_Billing` (shared money-path suite) | — |
| P10-02 | Monthly grant job | **Money-path QC** | `P10_01_12_Billing` | — |
| P10-03 | Charge-on-delivery (AI gateway) | **Money-path QC** | `P10_01_12_Billing` | — |
| P10-04 | Daily soft cap / monthly hard cap | **Money-path QC** | `P10_01_12_Billing` | — |
| P10-05 | Plan + Subscription state machine | **Money-path QC** | `P10_01_12_Billing`, `P10_W3_SubscriptionPayment` | — |
| P10-06 | Payment provider seam + webhook | **Money-path QC** (webhook security) **+ payment-sim** | `P10_W3_SubscriptionPayment`, `P10_PaymentSimulation_Tests` | **`docs/qc/P10-payment-sim/`** (simulate endpoint, 14 cases) |
| P10-07 | Purchased packs | **Money-path QC** (packs/refunds) | `P10_01_12_Billing` | pack re-home cross-ref in P10-13 (PACK-01..04) |
| P10-08 | Billing history / receipts | **Money-path QC** | `P10_01_12_Billing` | — |
| P10-09 | Dunning / refund webhook | **Money-path QC** | `P10_01_12_Billing` | refined by P10-17 (this run) |
| P10-10 | Energy meter (child read) | **Money-path QC** | `P10_01_12_Billing` | — |
| P10-11 | (money path) | **Money-path QC** | `P10_01_12_Billing` | — |
| P10-12 | GlobalSettings (AdminOnly + cross-key) | **Money-path QC** | `P10_01_12_Billing` | seat/grace config keys consumed by 13/14/15 |
| **P10-13** | **Family energy wallet & per-child allocation** | **THIS run** | `P10_13_FamilyEnergyWallet_Tests` (27) | **`docs/qc/P10-13/` — 21 cases + 3 gaps** |
| **P10-14** | **Child seats & seat-reserved add-child** | **THIS run** | `P10_14_ChildSeats_IntegrationTests` (22) | **`docs/qc/P10-14/` — 22 cases + 4 gaps** |
| **P10-15** | **Seat enforcement / grace / NoSeat-Locked** | **THIS run** | `P10_15_SeatLifecycle_IntegrationTests` (26) | **`docs/qc/P10-15/` — 26 cases + 4 gaps** |
| **P10-16** | **Family energy redistribution** | **THIS run** | `P10_16_FamilyAllocation_IntegrationTests` (19) | **`docs/qc/P10-16/` — 19 cases + 3 gaps** |
| **P10-17** | **Refund reconciliation (unused purchased)** | **THIS run** | `P10_17_Refunds_IntegrationTests` (18) | **`docs/qc/P10-17/` — 18 cases + 3 gaps** |
| **P10-18** | **Pause child access** | **THIS run** | `P10_18_PauseChild_IntegrationTests` (11) | **`docs/qc/P10-18/` — 11 cases + 3 gaps** |
| **Payment-sim** | **Mock simulate endpoint (gated)** | **THIS run** | `P10_PaymentSimulation_Tests` (14) | **`docs/qc/P10-payment-sim/` — 14 cases + 2 gaps** |

## B. Integration-test counts (the cluster + sim — THIS run's scope)

| Suite | Tests | QC doc | Cases mapped | Gaps flagged |
|-------|------:|--------|-------------:|-------------:|
| `P10_13_FamilyEnergyWallet_Tests` | 27 | `P10-13/backend-test-cases.md` | 21 | 3 |
| `P10_14_ChildSeats_IntegrationTests` | 22 | `P10-14/backend-test-cases.md` | 22 | 4 |
| `P10_15_SeatLifecycle_IntegrationTests` | 26 | `P10-15/backend-test-cases.md` | 26 | 4 |
| `P10_16_FamilyAllocation_IntegrationTests` | 19 | `P10-16/backend-test-cases.md` | 19 | 3 |
| `P10_17_Refunds_IntegrationTests` | 18 | `P10-17/backend-test-cases.md` | 18 | 3 |
| `P10_18_PauseChild_IntegrationTests` | 11 | `P10-18/backend-test-cases.md` | 11 | 3 |
| `P10_PaymentSimulation_Tests` | 14 | `P10-payment-sim/backend-test-cases.md` | 14 | 2 |
| **TOTAL (this run)** | **137** | 7 docs | **131** | **22** |

> The mapped-cases count (131) is slightly below the test count (137) because a few existing tests collapse two AC codes into one method (e.g. P10-13 `SPEND01_02` covers SPEND-01 + SPEND-02; P10-15 carries `ENFORCE-04b` as a hardening backstop of `ENFORCE-04`). Every existing test is accounted for in the per-story docs; the QC case count is one-per-AC-assertion, mapped 1:1 to its covering test.

## C. AC × coverage matrix (cluster + sim)

### P10-13 — Family energy wallet
| AC | Covered by | Status |
|----|-----------|--------|
| AC-1 wallet, two non-convertible buckets | QC-13-03/04/15/17 | Covered |
| AC-2 grant = PlanEnergyPerSeat × ActivePaidSeats | QC-13-05/08/09 (+ P10-14 GRANT-JOB) | Covered |
| AC-3 equal-split, deterministic remainder | QC-13-06/07 | Covered |
| AC-4 spend allocation-first → purchased-fallback | QC-13-10/11/12 | Covered |
| AC-5 immutable per-child ledger, source bucket, idempotency | QC-13-07/08/12/16/18 | Covered |
| **AC-6 monthly reset/expire, no convert** | — | **GAP-13-A (P0)** |
| AC-7 charging unchanged (HIT+MISS, daily cap) | QC-13-13/14 | Covered (cross-ref money-path QC for HIT/MISS) |
| AC-8 migration off CreditAccount | QC-13-19/20/21 (post-cutover) | Partial — **GAP-13-C** (pre-migration reconcile) |
| AC-9 module ownership/isolation, parent authz | QC-13-01/02/17 | Covered |

### P10-14 — Child seats
| AC | Covered by | Status |
|----|-----------|--------|
| Plan included seats + max ceiling | QC-14-01/02/03 | Covered |
| Active seat count formula | QC-14-21/22 | Covered |
| View seat status | QC-14-04/05 | Covered |
| Extra-seat purchase prorated money, server-side | QC-14-17 | Covered (— **GAP-14-C** client-amount-ignored) |
| Verified webhook adds seat, idempotent, no energy | QC-14-12..16 | Covered (— **GAP-14-D** payment.failed) |
| Cancel extra seat = cycle-end, no grace | QC-14-18/19 | Covered |
| Seat-reserved add-child + compensation | QC-14-06..11 | Covered |
| Free-plan extra-seat gating | — | **GAP-14-A (P1)** |
| Checkout/Cancel anon+child authz | partial | **GAP-14-B (P1)** |
| Seat purchase ledgered | QC-15-24 (SeatLedgerEntry) | Covered (cross-ref P10-15) |

### P10-15 — Seat enforcement / grace / lifecycle
| AC | Covered by | Status |
|----|-----------|--------|
| Voluntary removal effective at cycle-end | QC-15-25/26 | Covered |
| Payment-failure 7-day grace, config-driven, idempotent | QC-15-13/14/15 | Covered |
| **Enforcement on grace expiry (end-to-end)** | partial (ENFORCE direct + renewal) | **GAP-15-A (P0)** |
| **Payment success within grace clears it** | — | **GAP-15-B (P1)** |
| Enforcement locks over-limit, no forfeit, never delete | QC-15-08..12 | Covered (— **GAP-15-C** explicit Learning/XP no-touch) |
| NoSeat/Locked spend gate (graceful) | QC-15-16/17 | Covered |
| Reactivation prorated + zero energy + webhook | QC-15-21/22/23 | Covered (— **GAP-15-D** no-seat-no-payment) |
| Parent chooses active children, ≤ limit, family-scope, ledgered | QC-15-04..07 | Covered |
| Seat-state seam cross-module | QC-15-16/17 (via ISeatStateQuery) | Covered |
| Reservation idempotent + concurrency-safe | QC-15-18/19/20 | Covered |
| Seat events → dedicated SeatLedgerEntry | QC-15-07/10/24/25 | Covered |

### P10-16 — Family redistribution
| AC | Covered by | Status |
|----|-----------|--------|
| Zero-sum move between own children | QC-16-06/08 | Covered |
| Only UNSPENT movable; cap = remaining | QC-16-11/15 | Covered |
| Family-only; cross-family rejected | QC-16-09/10 | Covered |
| Bucket A only, non-convertible | QC-16-08 | Covered |
| Paired immutable ledger, CorrelationId, idempotency | QC-16-07/17 | Covered (— **GAP-16-B** idempotent INSERT replay) |
| **Atomic (all or none)** | — | **GAP-16-A (P1, best-effort)** |
| View per-child split incl. zero-allocation mid-cycle | QC-16-01/02 | Covered |
| Mid-cycle zero-allocation destination INSERT | QC-16-16 | Covered |
| Parent-gated, children can't transfer | QC-16-03/04 | Covered |
| Active-seat source/destination only | QC-16-18/19 | Covered |
| (concurrency on same source) | — | **GAP-16-C (P1, best-effort)** |

### P10-17 — Refund reconciliation
| AC | Covered by | Status |
|----|-----------|--------|
| Refundable = purchased − consumed, ledger-reconciled | QC-17-01/02/09/17 | Covered |
| Subscription (bucket A) never refundable | QC-17-08 | Covered |
| Already-consumed never refunded; clamp ≥ 0 | QC-17-02/13 | Covered |
| Idempotent Refund ledger row + balance decrement, never negative | QC-17-06/07/13/15/16 | Covered (— **GAP-17-B** concurrent race) |
| Reconciled against ledger as source of truth | QC-17-09/16/17 | Covered |
| Settled only via verified webhook | QC-17-06 | Covered |
| Parent-gated, owning-parent only | QC-17-03/04/05/18 | Covered |
| Admin can also initiate (admin authz, any family) | QC-17-10/11 | Covered |
| Envelope + validation | QC-17-12/14 | Covered (— **GAP-17-C** RefundReason enum) |
| (cross-kind refund isolation) | — | **GAP-17-A (P1)** |

### P10-18 — Pause child access
| AC | Covered by | Status |
|----|-----------|--------|
| Pause immediate; no side-effects | QC-18-01/02/03 | Covered (— **GAP-18-B** explicit exact-balance) |
| Paused child denied AI, no energy charged | QC-18-08 | Covered |
| Energy/seat/progress untouched | QC-18-01/02 | Covered |
| IDOR — own children only | QC-18-06/07 | Covered |
| State model separate from SeatState; gate checks both independently | QC-18-09/10/11 | Covered (— **GAP-18-A** combined locked+paused) |
| Ledger ParentPause/ParentUnpause; idempotent | QC-18-03/04/05 | Covered |
| Localized strings; child JWT rejected | QC-18-07/08 | Covered |
| (validation childId ≤ 0) | — | **GAP-18-C (P2)** |

### Payment-sim — gated simulate endpoint
| Contract | Covered by | Status |
|----------|-----------|--------|
| Happy path succeed / fail / refund (reuses real webhook) | QC-SIM-01/03/04 | Covered |
| Idempotent replay (deterministic eventId) | QC-SIM-02/14 | Covered |
| Gate: AllowSimulation off → 404 | QC-SIM-05 | Covered |
| Gate: AdminOnly (anon 401 / parent 403) | QC-SIM-06/07 | Covered |
| **Gate: Provider != Fake → 404 (third leg)** | — | **GAP-SIM-A (P0)** |
| Validation (paymentId, eventType) + NotFound | QC-SIM-08..12 | Covered |
| Envelope shape | QC-SIM-13 | Covered |
| (seat-kind simulate) | — | **GAP-SIM-B (P2)** |

## D. Coverage verdict

- **Stories already QC'd by integration tests (this run mapped them traceably):** P10-13, P10-14, P10-15, P10-16, P10-17, P10-18, payment-sim — every AC has at least one P0/P1 covering test EXCEPT the gaps listed below.
- **Money paths (P10-01..12):** covered by the pre-existing `P10-Billing` 75-case QC (all 18 locked-economy invariants covered; see `coverage-report.md`). Not re-covered here.

## E. Genuine gaps for `api-tester` to close (priority-ordered)

**P0 (block release — money/safety-critical, AC with no covering test):**
- **GAP-13-A** — AC-6 monthly subscription reset/expire + no-convert at cycle rollover. The ONLY cluster AC with zero covering test. Drive the rollover/expire seam; assert `SubscriptionBalance` resets, `Expire` ledger entries, `PurchasedBalance` untouched.
- **GAP-15-A** — payment-failure grace expiry → enforcement, end-to-end (seed an expired `SeatGraceEndsAt`).
- **GAP-SIM-A** — third triple-gate leg: `Provider != "Fake"` → 404 even with `AllowSimulation=true` + admin JWT. Production safety — a real provider must never be simulable.

**P1 (should — AC partial or important negative paths):**
- GAP-13-C (pre-migration CreditAccount reconcile — blocked if legacy model not seedable)
- GAP-14-A (free-plan extra-seat gating), GAP-14-B (checkout/cancel authz matrix), GAP-14-C (client-amount ignored), GAP-14-D (seat payment.failed)
- GAP-15-B (payment success within grace clears it), GAP-15-C (locked child Learning/XP untouched — explicit)
- GAP-16-A/B/C (atomicity rollback; idempotent INSERT replay; concurrent same-source — last two best-effort)
- GAP-17-A (cross-kind refund isolation), GAP-17-B (concurrent negative-balance race — best-effort), GAP-17-C (RefundReason enum validation)
- GAP-18-A (combined NoSeatLocked AND Paused)

**P2 (nice):** GAP-13-B (explicit non-convertibility assertion), GAP-15-D, GAP-18-B/C, GAP-SIM-B.

## F. Better suited to UNIT tests (recommend to lead — out of integration scope)
- P10-13 equal-split remainder math (grant < childCount, grant=0, 1 child).
- P10-14 proration ratio at boundaries (0 days, full cycle, 1 day).
- P10-16 `Remaining = Allocated − Spent` cap + same-family guard predicate.
- P10-17 `ComputeRefundableAsync` FIFO math (BE-2 explicitly calls for unit tests: bought 10000/used 3000→7000; fully spent→0; subscription rows excluded; multi-pack FIFO).
- P10-18 `IsChildAccessAllowedAsync` truth table (Active/Active=true; all else=false).
- Payment-sim deterministic-eventId formatter (`sim-{paymentId}-{eventType}`).

## G. Open questions for the lead (resolve before `api-tester` closes gaps)
- **OQ-A (GAP-13-A / GAP-15-A clock control):** Can the integration harness advance time / seed past-due cycle + grace boundaries, or must these gap cases seed expired timestamps directly? Confirm the approach.
- **OQ-B (GAP-13-C):** Is the legacy per-child `CreditAccount` model still seedable in the test host to prove the destructive migration's pre-cutover reconcile, or is it already retired (making this a documentation-only / blocked case)?
- **OQ-C (GAP-14-A / OQ-2 from P10-14-BE):** Is the free-plan extra-seat gate (and the extra-seat feature flag) enabled? The gap negative case depends on the lead's gating decision.
- **OQ-D (GAP-SIM-B):** Is the simulate endpoint intended to drive Seat-kind / SeatReactivation payments, or is it limited to subscription + refund? Determines whether GAP-SIM-B is a real gap or a documented boundary.

## Handoff
- `api-tester` runs the seven existing suites (commands in each folder's `execution-report.md`), records Pass/Fail/Blocked per QC case, and **implements the flagged gap cases** (P0 first) in the matching `Learnexia.IntegrationTests` file (or unit-test project for section F), then writes results into each folder's `execution-report.md`.
- No frontend surface in this scope (these are backend stories) — no `frontend-e2e-tester` work for this run. The parent-facing FE surfaces (P10-13/14/15/16/17/18 FE) are owned by the frontend lead and are out of scope here.
