# Web account backend — profile, avatar, OAuth, password reset (Phase 1 · Batch 2)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation · **Batch 2 (deferred)**
- **Epic:** Web account backend
- **Issue type:** Epic
- **Story Points:** 21 (sum of children) — split below.
- **Labels:** `backend`, `batch-2`, `account`, `auth`, `web`
- **Status:** **Deferred — Batch 2.** Picked up after the current batch; **parallel-safe** with the Phase 2 backend another lead owns (Identity-module only, no cross-module coupling).

## Description
As the team, we need the backend that the redesigned web pages (P1-11) imply but Phase-1 Batch 1 never planned — profile read/update, avatar upload, social login, and password reset — so the web account experience is fully functional.

> **Why a separate batch:** surfaced by `docs/briefs/phase-1-design-gap-analysis.md`. Frontend (P1-11) builds the UI now against stubs/disabled affordances; this batch makes those affordances real. Each child is **Identity-module-scoped** to stay parallel-safe with the Phase 2 BE lead.

## Child stories

### P1-12a — Account profile API + enriched `/Me`
**Issue type:** Story · **Points:** 5 — gates Settings → Profile (P1-11h).
**Description:** As a parent, I want to read and update my profile (full name, phone, country) so that my account details are correct.
**Acceptance Criteria:**
- `GET` profile + `PUT`/update profile (full name, phone, country) for the authenticated user; `BaseResponse<T>` via `BaseResponseHandler`, `[Authorize]` (self).
- **`/Me` enriched** to return fullName, phone, country (and avatarUrl once P1-12b lands) — the web dashboard header + Settings Profile read from it.
- Migration: add **`Phone`** (and `Country` if not present) to the Identity `User`; Npgsql, identity schema.
- Validation via `ValidationBehavior` (ICommand); en/ar localized messages.
**Labels:** `backend`, `batch-2`, `account`

### P1-12b — Avatar upload & remove
**Issue type:** Story · **Points:** 5 — security-sensitive (file upload).
**Description:** As a parent, I want to upload/remove my profile photo so that my account feels personal.
**Acceptance Criteria:**
- Upload endpoint (type/size validation, safe storage) + remove; sets/clears `AvatarUrl` on the user; returns the URL in `/Me` + profile.
- File-storage decision recorded (local/dev vs object store); no executable/oversized uploads; `[Authorize]` (self only).
- **security-auditor** reviews before the gate (file upload + user data).
**Labels:** `backend`, `batch-2`, `account`, `upload`
**Notes:** until this lands, FE uses initials/placeholder avatars (P1-11).

### P1-12c — Social login (OAuth: Google / Apple / Microsoft)
**Issue type:** Story · **Points:** 5
**Description:** As a parent, I want to sign in with Google/Apple/Microsoft so that sign-in is faster.
**Acceptance Criteria:**
- OAuth backend for the three providers; issues the same JWT/refresh as password login; links/creates the parent account.
- The P1-11 Login social buttons (UI-only today) wire to these flows.
- **security-auditor** reviews (auth/secrets).
**Labels:** `backend`, `batch-2`, `auth`
**Notes:** provider credentials/config required; until then the buttons stay UI-only placeholders.

### P1-12d — Password reset (forgot password)
**Issue type:** Story · **Points:** 5
**Description:** As a parent who forgot my password, I want to request a reset and set a new one so that I can regain access.
**Acceptance Criteria:**
- Request reset (email link, no account enumeration) + set-new-password (token validation, password policy, invalidate other sessions).
- Wires the P1-11 Login "Forgot password?" link.
**Labels:** `backend`, `batch-2`, `auth`, `security`
**Notes:** reuses Identity password/session services; needs email delivery (Notifications module).

## Notes
- **Parallel-safe:** all Identity-module; cross-module only via `Shared.Contracts`. No Unit of Work; explicit transaction for multi-write. **Ask before adding any design pattern.**
- **Blocked by** nothing in Batch 1 except it consumes the existing Identity foundation; **blocks** the *functional* parts of P1-11h (Profile save), the Login social buttons, and forgot-password — those ship as UI-first in P1-11 and light up when this batch merges.
- Source: `docs/briefs/phase-1-design-gap-analysis.md`.
