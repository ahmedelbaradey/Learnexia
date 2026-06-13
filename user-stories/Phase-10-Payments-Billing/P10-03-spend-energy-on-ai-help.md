# Spend energy on AI help (charge-on-delivery)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 10 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — AI-gateway integration + atomic debit + cache/refuse rules.
- **Labels:** `billing`, `credits`, `ai`, `backend`
- **Requirements:** FR-CREDIT-3 *(new — Phase 10)*

## Description
As a child using the AI Helper, I want my energy charged only when I actually receive help, so that I'm never charged for a refusal or an error.

## Acceptance Criteria
- Per-action cost (config-driven, P10-11): **hint = 1, explain-mistake = 3, deep-explanation = 5, practice-generation = 5**.
- Credits are debited **on successful delivery** of the response (after the safety filter), never on request.
- **Cache-hit** responses are charged the **same** as live responses (predictable for the child; cache = margin).
- A **refuse-and-redirect**, an **error**, or **insufficient balance** results in **no charge**.
- Insufficient balance → the Helper declines gracefully with a low-energy message (P10-04) and serves a cached canned explanation when available — **never blocks learning**.
- Debit + delivery are **atomic**: no charge without delivery, and no delivery (when balance suffices) without charge.

## Notes
- Wires into the **AI Gateway (P3-01)** and **replaces** the AI-Helper MVP daily-cap guardrail.
- Charge-on-delivery / cache-charged-same / no-charge-on-refuse are **lead-confirmed**.
- Blocked by **P10-01** and **P3-01**.
