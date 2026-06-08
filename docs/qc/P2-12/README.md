# P2-12 — QC Test Plan & Coverage Report (BACKEND ONLY)

**Story:** Parent account settings — Notifications / Linked children (P2-12, child stories P2-12a notifications + P2-12b linked children)
**Surface in scope:** the **settings-tab API surface** this story added — Notifications preferences (`GET/PUT /api/Notifications/Preferences`) and the linked-children settings management (`DELETE /api/Parent/Unlink-Child`, plus the list/link read context).
**Out of scope here:** all frontend (no `frontend-test-cases.md`); P2-12c Security (change-password/sessions) and P2-12d Plan stub are *adjacent* settings tabs but were **not named in this run's scope** — see "Open questions" for the call to extend.
**Designer/QC author:** qc-test-designer (Opus pass), design-only. No test code, no execution, no feature edits.

---

## 1. Summary

This run designs **backend integration test cases** for the two settings-tab surfaces P2-12 added on top of the P1 family/notification building blocks. The implementation is already merged and audited (per task file "Status: Done", 2026-06-07), so these cases validate the *running* API against the story's acceptance criteria and the IDOR/auth/last-parent guards called out in the run brief.

The over-arching rule for this run: **cross-reference, do not duplicate** the P1 stories that own the underlying mechanics — P1-04/P1-12 (link/edit/unlink child mechanics) and P1-13a (notification-prefs base). P2-12 cases focus on the *settings-tab behaviour and guards* (defaults-on-first-GET, PUT persistence/validation, unlink last-parent block, unlink-not-linked 404, and IDOR/self-scope across both surfaces).

### Counts

| Dimension | Count |
|---|---|
| **Total cases** | **24** |
| Backend (`api-tester`) | 24 |
| Frontend | 0 (out of scope) |
| **P0** | 13 |
| **P1** | 8 |
| **P2** | 3 |

By surface:

| Surface | Cases |
|---|---|
| Notification preferences — `GET /api/Notifications/Preferences` | BE-TC-01 … BE-TC-05 (5) |
| Notification preferences — `PUT /api/Notifications/Preferences` | BE-TC-06 … BE-TC-14 (9) |
| Linked children — `DELETE /api/Parent/Unlink-Child` | BE-TC-15 … BE-TC-21 (7) |
| Linked children — list/link read context (cross-ref scope) | BE-TC-22 … BE-TC-24 (3) |

---

## 2. Coverage matrix (acceptance criterion → case IDs)

### P2-12a — Notification preferences (BE)

| Acceptance criterion (story / brief) | Case IDs | Verdict |
|---|---|---|
| BE: endpoint to **read** notification preferences for the parent | BE-TC-01, BE-TC-02, BE-TC-03 | Covered |
| First read with no saved rows returns **defaults**, not 404, nothing persisted | BE-TC-02, BE-TC-03 | Covered |
| BE: endpoint to **update** preferences; **persisted per user** | BE-TC-06, BE-TC-07, BE-TC-08 | Covered |
| Self-scoped — UserId from JWT, never body (no IDOR) | BE-TC-04, BE-TC-13, BE-TC-14 | Covered |
| PUT validation (reject empty / unknown category / duplicate category) → 422 | BE-TC-09, BE-TC-10, BE-TC-11 | Covered |
| Per category (4: WeeklyReport/StreakAtRisk/ProductAnnouncement/Achievement) × channel (Email/Push) | BE-TC-01, BE-TC-06, BE-TC-12 | Covered |
| Re-engagement categories (4/5/6) NOT surfaced on self endpoint | BE-TC-05, BE-TC-12 | Covered |
| `BaseResponse<T>` envelope, `Successed` spelling, status mapping | BE-TC-01, BE-TC-06, BE-TC-09 | Covered |
| Auth — 401 anonymous | BE-TC-04 (GET), BE-TC-13 (PUT) | Covered |

### P2-12b — Linked children (BE, unlink settings action)

| Acceptance criterion (story / brief) | Case IDs | Verdict |
|---|---|---|
| BE: **unlink** endpoint; removes the `ParentStudent` row for (caller, child) | BE-TC-15 | Covered |
| **Last-parent guard** — cannot unlink the only parent → 400 | BE-TC-16 | Covered |
| **Family scope** — a parent only manages their own children (unlink non-linked / other-family child) → generic 404 | BE-TC-17, BE-TC-18 | Covered |
| Acting parent always from JWT; no `ParentId` in body (IDOR) | BE-TC-18, BE-TC-19 | Covered |
| Auth — 401 anonymous on unlink | BE-TC-20 | Covered |
| Unlink validation (ChildId > 0) → 422 | BE-TC-21 | Covered |
| BE: **list** only the caller's own linked children (settings list reads) | BE-TC-22, BE-TC-23 | Covered (cross-ref to P1-04) |
| Link already-linked → 409 (recently fixed) | BE-TC-24 | Covered (cross-ref to P1-04 / BUG-P104-02) |

**Coverage verdict: every in-scope acceptance criterion has at least one P0/P1 case. No gaps for the named scope.** The only "gap" relative to the *full* P2-12 epic is P2-12c (Security) and P2-12d (Plan) — intentionally excluded by this run's scope, listed as an open question below.

---

## 3. Cross-references (NOT duplicated here)

These behaviours are owned/already-covered by earlier stories; P2-12 reuses them. We list the thin confirmation cases we kept and explicitly defer the rest:

| Mechanic | Owning story | What P2-12 keeps | What we defer (do not re-test) |
|---|---|---|---|
| Link-by-email happy path, cross-family fail-closed, self-link, non-Student reject | **P1-04** | BE-TC-22/23 (list self-scope) + BE-TC-24 (already-linked 409, recently fixed and in P2-12's settings flow) | Full link-by-email enumeration-defence matrix, Add-Child creation, role checks — owned by P1-04 api-tester suite |
| Edit-child / Update-Child profile mutation + family scope | **P1-12** | none new here | Update-Child 403-on-non-linked is in the P2-12 plan's tester table but is a P1-12 mechanic — cross-ref, don't duplicate |
| Notification-prefs base entity + first GET defaults concept | **P1-13a** | BE-TC-02/03 (defaults-on-first-GET as the P2-12 settings contract) | Delivery-side `NotificationType` behaviour, inbox, devices — separate Notifications surfaces |
| Change-password + session invalidation + plan stub | **P2-12c / P2-12d** | none (not in this run's scope) | Entire Security + Plan tabs — see open question 1 |

The unlink **last-parent guard** and **unlink-not-linked 404** are P2-12-original additions (the `UnlinkChildCommandHandler` and `UnlinkIfNotLastParentAsync` were new in this story), so they are tested here in full, not cross-referenced.

---

## 4. Risk notes (where cases are weighted, and why)

1. **IDOR / self-scope is the top risk** and is weighted heaviest. Both surfaces resolve identity from `ICurrentUserService` (JWT) and *intentionally carry no `ParentId`/`UserId` in the body*. The notification command/query and unlink command have no identity field at all — but a regression that accidentally trusts a body field, or a handler that forgets the `userId is null → Unauthorized` guard, would be a cross-user data leak. BE-TC-04/13/14/18/19 attack this directly (parent A cannot read/mutate parent B's prefs or children; injected body identity fields are ignored).

2. **Last-parent guard + TOCTOU.** `UnlinkIfNotLastParentAsync` runs the count-check + delete inside a REPEATABLE READ transaction to stop two concurrent unlinks both passing the guard and orphaning the child. BE-TC-16 covers the single-call block; BE-TC-19 (concurrent) is the boundary case that proves the atomicity fix — a minor must never be left with zero guardians.

3. **Defaults-on-read must not persist.** The GET handler synthesises defaults in-memory and must write nothing. A bug that lazily inserts default rows on read would (a) break "nothing persisted on read" and (b) silently change behaviour of a later PUT upsert. BE-TC-03 asserts no rows exist after a first GET (verified by a subsequent state read, not just status).

4. **PUT upsert atomicity + partial set.** The command replaces rows for the supplied categories inside an explicit transaction. A PUT carrying a subset of categories must not wipe the others (it only upserts what's supplied). BE-TC-08 (subset) and BE-TC-07 (round-trip persistence) guard this; BE-TC-11 (duplicate category) guards the validator that prevents double-writing one row.

5. **Anti-enumeration shape.** Unlink of a non-linked / other-family child must collapse to the *same generic 404* regardless of whether the child exists — disclosing "exists but not yours" vs "doesn't exist" is an enumeration leak. BE-TC-17/18 assert identical response shape for both.

---

## 5. Open questions / assumptions (lead decisions before testers implement)

1. **Scope confirmation — Security + Plan tabs.** This run was scoped to Notifications + Linked-children only. P2-12 the *epic* also shipped P2-12c (`POST /api/Users/Account/ChangePassword`, `GET /api/Users/Account/Sessions`, `POST .../SignOutOthers`) and P2-12d (`GET /api/Users/Account/Plan`). If the lead wants a complete P2-12 QC pass, say so and I will add a Security + Plan section (change-password wrong-current → 400, other-sessions-invalidated, current-session-survives, refresh-token-revoked, sessions self-scope IDOR, plan stub determinism). **Assumption for now: out of scope, deliberately omitted.**

2. **Default values contract.** The GET handler defaults **WeeklyReport → Email on**, everything else off (Push always off by default). Confirm this is the agreed product default for the settings tab so BE-TC-02 can assert exact values rather than just "4 categories present". If product wants all-off defaults, BE-TC-02's expected values change (the test is otherwise structurally correct).

3. **PUT partial-set semantics.** Implementation upserts **only** the categories present in the request (it does not delete unlisted ones). The story says "Changes save" without specifying full-replace vs partial-upsert. BE-TC-08 is written to the *implemented* partial-upsert behaviour. If product intends a full replace (4 categories always required), BE-TC-08's expectation flips and the validator's `NotEmpty`-only rule (it does **not** require all 4) becomes a defect to file. **Flag for lead: is partial PUT acceptable, or must all 4 categories be sent?**

4. **Unlink HTTP verb + body.** `Unlink-Child` is `DELETE` with a JSON **body** (`{ "childId": N }`), which some HTTP clients/proxies strip on DELETE. Not a correctness bug for the integration tests (the in-process/`HttpClient` harness sends it), but worth a note if any gateway sits in front in other environments. No case fails on this; documented only.

5. **Seed/auth facts.** Per HANDOFF: the running BE seeds a fresh dev DB on first boot; auth is JWT bearer. Testers must seed at least **two distinct parents** (A and B) each with their own child, plus **one child with two parents** (for the last-parent guard) and **one child with a single parent** (for the block). These named entities are specified per-case in `backend-test-cases.md`.

---

## 6. Handoff

- **`backend-test-cases.md`** → **`api-tester`** — implement all 24 cases as integration tests against the running API (real Parent-role JWTs). Where a case is a cross-reference confirmation (BE-TC-22/23/24), reuse existing P1-04 fixtures rather than re-deriving them.
- **`frontend-test-cases.md`** → **not produced** (backend-only run).
- **`execution-report.md`** → scaffolded empty in this folder. The **testers** fill it after running (pass/fail per case + defects). The QC author never fills results.

---

**Test cases ready — `api-tester` to implement `backend-test-cases.md`; results go into `execution-report.md`. No frontend surface in this run.**
