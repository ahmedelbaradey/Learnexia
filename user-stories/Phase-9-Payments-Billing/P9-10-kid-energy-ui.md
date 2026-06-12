# Kid-facing energy UI (⚡ طاقة المساعد)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Payment, Billing & Credits (post-MVP)
- **Epic:** Payment, Billing & Credits
- **Issue type:** Story
- **Story Points:** 5 — meter + cost affordances + states + RTL; needs a designer stage.
- **Labels:** `billing`, `credits`, `frontend`, `student-app`
- **Requirements:** FR-CREDIT-5 *(new — Phase 9)*

## Description
As a child, I want to see my **⚡ طاقة المساعد** (Helper Energy) and what each kind of help costs, so that I understand and pace my AI use.

## Acceptance Criteria
- An energy meter shows the current balance, **visually distinct from gamification hearts** (different icon, colour, and placement) to avoid confusion with the hearts/lives mechanic (P4-04).
- Each Helper action shows its **energy cost before** the child confirms (hint ⚡1, explain-mistake ⚡3, deep-explanation ⚡5, similar-example ⚡5).
- A **spend animation** plays on charge; a friendly **low-energy / out-of-energy** state explains when it refills (monthly) and offers "ask a parent" — **children cannot purchase**.
- Full **RTL/Arabic + English**; kid-appropriate copy; the word **"Credits" is never shown to the child** (always "طاقة"/"energy").

## Notes
- **The only student-app billing surface — and it is read-only.** The child sees their energy and costs but **never** a payment/purchase/plan UI; the out-of-energy state routes to **"ask a parent"** (all top-ups happen in the parent app, P9-05/06/07). Pipeline: `designer` → `frontend` → `frontend-e2e-tester`. Distinct from hearts (**P4-04**). Blocked by **P9-03**.
