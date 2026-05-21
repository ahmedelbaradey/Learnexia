---
name: user-stories
description: Turn a feature idea, requirement, or rough description into well-formed agile user stories with acceptance criteria. Use this skill whenever the user asks to "write user stories", "generate user stories", "create user stories", "break this into user stories", "turn this into stories", "write acceptance criteria", "split this epic", "groom this backlog item", or pastes a feature/requirement and asks for stories. Also use when the user describes a feature and asks "how would you break this down" for a sprint or backlog. Do NOT use for writing technical design docs, test plans, or non-agile requirements specs — those are different artifacts even if they overlap.
---

# Generate User Stories

Take a user's feature idea, requirement, or rough description and turn it into well-formed agile user stories. The goal is stories a team can actually pick up and build — small, valuable, testable — not a wall of vague wishes or a single mega-story that hides all the real work.

## Core principles

**Preserve intent.** The user described a feature for a reason. Capture what they actually want delivered, and shape stories that produce *that*. Don't drift scope because a different feature would be easier to slice.

**Match the scale.** A small tweak comes back as one tight story. A broad capability comes back as an epic split into several stories. Don't explode a one-line ask into fifteen stories, and don't cram a whole epic into one story.

**Value beats ceremony.** Every story should deliver something a user or stakeholder can perceive. "As a user I want a database table" is not a story — it's a task. Frame around outcomes, not implementation steps.

**Flag assumptions, don't hide them.** If the feature is ambiguous and a story requires a guess about scope, persona, or behavior, say so briefly after the stories — don't bury the guess inside acceptance criteria as if it were settled fact.

## Diagnostic pass

Before writing stories, read the request and ask internally:

- **Persona** — Who is this for? Which role/user type benefits? (End user? Admin? API consumer?)
- **Goal** — What does that persona want to accomplish, in their terms?
- **Value** — Why does it matter to them? What outcome do they get?
- **Scope** — Is this one story or several? Where are the natural seams to split on?
- **Acceptance** — How would the team know the story is done and working?
- **Edge cases** — Errors, empty states, permissions, limits, concurrent use.
- **Dependencies** — Does one story have to ship before another can?
- **Out of scope** — What is explicitly *not* part of this, to prevent scope creep?

Most requests are missing 2–4 of these. Fill the gaps. Don't pad the ones that are already clear.

## How to slice

Reach for these splitting patterns, in roughly this order of usefulness:

1. **By workflow step.** A multi-step flow → one story per meaningful step a user completes.
2. **By persona.** Different roles (user vs. admin vs. guest) often want different stories.
3. **By CRUD/operation.** Create, view, edit, delete may each be a story when they carry distinct value and effort.
4. **By happy path vs. edge cases.** Ship the core path first; error handling, validation, and empty states can be follow-on stories.
5. **By data variation.** Different input types or rule sets that materially change behavior.
6. **By interface.** UI vs. API vs. batch, when they're built and delivered separately.

Keep stories INVEST: **I**ndependent, **N**egotiable, **V**aluable, **E**stimable, **S**mall, **T**estable. If a story fails one of these, reslice it.

## Estimating story points

Estimate each story in points on a Fibonacci scale (1, 2, 3, 5, 8, 13). Points measure relative effort/complexity/uncertainty — **not** hours. Anchor to a rough baseline:

- **1** — Trivial, well-understood. A copy change, a config tweak, one obvious field.
- **2** — Small, single clear path, minimal unknowns.
- **3** — Moderate. A normal feature slice with a couple of edge cases.
- **5** — Substantial. Multiple components, real edge cases, some unknowns.
- **8** — Large and risky. Several moving parts or notable uncertainty — consider splitting.
- **13** — Too big. This is a flag to reslice, not a real estimate.

Rules of thumb: anything **8 or above is a candidate to split**. Give a one-line rationale for each estimate (what drives the cost — integration, edge cases, unknowns). Make estimates *relative to each other* within the set, and state that they're indicative — the team's own calibration during planning poker is what counts. If the user gives a reference story ("our login was a 3"), calibrate against it.

## Story format (Jira)

Format stories so they paste cleanly into Jira. Write each as:

**Summary** — the Jira issue title: short, action-oriented, no "Story:" prefix (e.g. "Request a password reset").

**Issue type** — `Story` (or `Epic` for the parent when you split, with stories listed under it).

**Description** — the narrative: `As a [persona], I want [capability] so that [benefit].` All three parts; the "so that" is what keeps it honest about value.

**Acceptance Criteria** — testable conditions in Given/When/Then form where it fits, or a verifiable checklist otherwise. 3–6 per story is typical. Each must be objectively checkable — no "works well" or "is fast" without a number.

**Story Points** — a Fibonacci estimate (see *Estimating story points*) with a one-line rationale.

**Labels / Components** *(optional)* — suggest relevant labels (e.g. `auth`, `frontend`) when they're obvious from the feature.

**Notes** *(optional)* — dependencies (use Jira-style "blocked by / blocks" phrasing), out-of-scope flags, or open questions. Only when they add something.

When the request is an epic, present an **Epic** heading first, then the child stories beneath it so the hierarchy is clear.

## What not to do

- Don't write implementation tasks as stories ("create the users table", "add a Redux reducer"). Those are sub-tasks, not user-facing value.
- Don't invent acceptance criteria the user can't have meant. If they didn't specify a limit, don't assert "must load in under 200ms" — ask, or leave it as an open question.
- Don't write vague, untestable criteria ("the page looks good", "it's intuitive").
- Don't produce one giant story that hides three features. Split it.
- Don't over-split either — five trivial stories that each move nothing are worse than one real one.
- Don't drop the "so that" clause. A story with no stated benefit usually shouldn't exist.

## When to ask before writing

Default to writing the stories. But ask 1–3 targeted questions first if:

- The persona is genuinely unclear and the stories would differ a lot depending on who it's for.
- The scope could reasonably be one story or a ten-story epic, and guessing wrong wastes the user's time.
- The feature references a system, constraint, or workflow that wasn't shared and materially changes the breakdown.

For a casual "give me some stories for X," just do it. Don't turn every request into a grooming interrogation.

## Output format

Respond in this order:

1. **The user stories**, each in the Jira format above (Summary / Issue type / Description / Acceptance Criteria / Story Points / optional Labels / Notes). Use clear headings so they paste straight into Jira.
2. **A short breakdown note**, if the slicing wasn't obvious — 1–3 bullets on *why* you split it this way. Skip if the breakdown is self-evident.
3. **Flagged assumptions**, if any. One line each. E.g., "Assumed 'user' means an authenticated customer, not an admin."
4. **Optional follow-up question**, only if a high-value clarification would meaningfully improve the breakdown.

Keep it tight. The user wants usable stories, not an essay about agile.

## Examples

### Example 1: Vague feature request

**Input from user:**
> We need password reset.

**Good response:**

**Summary:** Request a password reset
**Issue type:** Story
**Description:** As a registered user who forgot my password, I want to request a reset link by email so that I can regain access to my account.
**Acceptance Criteria:**
- Given I'm on the login page, when I click "Forgot password" and enter a registered email, then a reset link is sent to that address.
- Given I enter an email that isn't registered, then I see the same confirmation message (no account enumeration).
- The reset link expires after a configurable time (default 1 hour).
**Story Points:** 3 — standard email flow; the no-enumeration and expiry rules add a little care.
**Labels:** `auth`

**Summary:** Set a new password from a reset link
**Issue type:** Story
**Description:** As a user with a valid reset link, I want to enter a new password so that I can log in again.
**Acceptance Criteria:**
- Given a valid, unexpired link, when I submit a new password meeting the strength rules, then my password is updated and I'm redirected to login.
- Given an expired or already-used link, then I see an error and an option to request a new one.
- After a successful reset, all existing sessions for that account are invalidated.
**Story Points:** 5 — token validation, password rules, and session invalidation are several distinct pieces.
**Labels:** `auth`, `security`
**Notes:** Blocked by "Request a password reset".

**Breakdown note:**
- Split on workflow step (request vs. set) so each ships and tests independently.

**Flagged assumptions:**
- Assumed reset is via email link, not SMS or security questions.

### Example 2: Already-scoped, just needs structure

**Input from user:**
> As an admin I want to export the user list to CSV.

**Good response:**

**Summary:** Export user list to CSV
**Issue type:** Story
**Description:** As an admin, I want to export the user list to a CSV file so that I can analyze user data in a spreadsheet.
**Acceptance Criteria:**
- Given I'm on the user management page, when I click "Export CSV", then a CSV downloads containing all users matching the current filters.
- The CSV includes columns: name, email, role, status, created date.
- Given there are no users matching the filter, then I get a CSV with headers only and a notice that the export is empty.
- Only admins can access the export action.
**Story Points:** 2 — single clear path; main cost is wiring the filter into the export and the permission check.
**Labels:** `admin`, `reporting`

**Breakdown note:**
- Single story — already small and testable; just added persona/value framing and concrete criteria.

### Example 3: Hidden epic that needs splitting

**Input from user:**
> Build a checkout flow.

**Good response:**

This is an epic, not one story.

**Epic:** Checkout flow

**Summary:** Review cart before checkout
**Issue type:** Story
**Description:** As a shopper, I want to review my cart items and total so that I can confirm before paying.
**Acceptance Criteria:**
- Cart shows each item with quantity, unit price, and line total.
- Subtotal, tax, and grand total are displayed and recalculate when quantities change.
- Given an empty cart, the checkout button is disabled.
**Story Points:** 3 — display plus live recalculation and the empty-cart edge case.
**Labels:** `checkout`, `frontend`

**Summary:** Enter shipping details
**Issue type:** Story
**Description:** As a shopper, I want to enter a shipping address so that my order can be delivered.
**Acceptance Criteria:**
- Required fields are validated before continuing.
- Given a saved address, I can select it instead of retyping.
**Story Points:** 3 — form validation plus saved-address selection.
**Labels:** `checkout`

**Summary:** Pay for the order
**Issue type:** Story
**Description:** As a shopper, I want to pay with my card so that I can complete the purchase.
**Acceptance Criteria:**
- A successful payment creates an order and shows a confirmation with order number.
- A declined payment shows an error and keeps the cart intact.
**Story Points:** 5 — payment-gateway integration and failure handling carry real complexity and risk.
**Labels:** `checkout`, `payments`
**Notes:** Blocked by "Review cart before checkout" and "Enter shipping details".

**Breakdown note:**
- Sliced by workflow step (review → ship → pay) so the team can ship and demo each independently.

**Flagged assumptions:**
- Assumed card payment only; ignored saved-card vaulting, promo codes, and guest vs. account checkout — flag if any are in scope.

**Follow-up question:**
- Is guest checkout in scope, or must shoppers have an account?

---

That's the skill. Read the request, diagnose what's missing, slice into INVEST stories with testable acceptance criteria, estimate each in Fibonacci points, and return them in Jira-ready format with a short note on how you split them.
