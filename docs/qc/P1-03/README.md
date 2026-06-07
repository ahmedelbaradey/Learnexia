# QC Test Plan & Coverage Report — P1-03 (Backend)

**Story:** Parent completes onboarding and adds children
**Source of truth:** `user-stories/Phase-1-Foundation/P1-03-complete-onboarding.md`
**Brief:** `docs/briefs/P1-03.md` · **Plan:** `docs/plans/P1-03.md` · **Task file:** `tasks/Backend/Phase-1-Foundation/P1-03-BE.md`
**Run type:** Backend-only QC test-case design pass. No frontend cases produced.
**Designed by:** QC test architect (design only — no test code, no execution).

---

## 1. Summary

This run designs the API-level test catalog for the **Add-Child** flow: an authenticated **Parent** provisions one or more **Student-role** child accounts, each with a parent-assigned login email, grade (1–6), UI language, learning language, and country, with each child auto-linked to the acting parent.

### Implementation reality (drift from the brief/plan — see Open Questions)
The shipped code diverges from the brief and the plan. Test cases below are written against the **actual running code**, not the plan's assumptions:

- **Endpoint is `POST /api/Parent/Add-Child`** (not `/api/Users/Parent/Add-Child` and not the task file's `/api/Users/UserManagement/Add-Child`). It lives on `ParentController` in the **Parent module** (relocated from Identity), route prefix `api/Parent`, gated `[Authorize(Roles = "Parent,Admin,SuperAdmin")]`.
- **Command field names are `FullName`, `Email`, `Password`, `Grade`, `Language`, `Country`, `LearningLanguage`** — NOT the `childName`/`loginEmail` names in the story/brief. The FE contract maps to these.
- **A 7th field `LearningLanguage` (ar|en) was added by P8-01** (medium-of-instruction, distinct from `Language` the UI preference). It is **required** and validated identically to `Language`. The original P1-03 brief did not contain it; it must be covered.
- **Account creation is encapsulated behind the `IChildAccountService` cross-module seam** (`CreateChildAsync`), which owns the duplicate-email check, `CreateAsync`, `AddToRoleAsync(Student)` with a **compensating delete on role-assign failure**, and the best-effort `UserRegisteredIntegrationEvent` publish. Raw Identity errors are never leaked across the boundary.
- **Duplicate email returns HTTP 400** (`BadRequest` + `ProfileDuplicateEmail`), NOT 422 — only shape validation is 422.
- **Password policy is a regex**: at least one lowercase, one uppercase, one digit, one special char, min length 6 (`^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{6,}$`). The plan's "MinimumLength(8)" is NOT what shipped.
- **No batch endpoint.** Multiple children = the parent calls the single endpoint N times (FE loops). Partial-failure safety is therefore "each call is independent" rather than per-item batch results.
- Child sign-in path for the end-to-end persistence check is **`POST /api/Users/Authentication/Sign-In`**.

### Counts
| Metric | Count |
|---|---|
| Total cases | 28 |
| Backend (all) | 28 |
| Frontend | 0 (out of scope — backend-only run) |
| **P0** | 14 |
| **P1** | 10 |
| **P2** | 4 |

By type: functional 5 · validation 9 · negative 3 · boundary 3 · auth-authz 4 · persistence/linkage 3 · regression/product-override 1.

---

## 2. Coverage matrix — every acceptance criterion → case IDs

Acceptance criteria are taken from `docs/briefs/P1-03.md` §"Acceptance criteria (testable)" (AC-1..AC-8), reconciled with the story's bullet list.

| AC | Criterion (short) | Covered by | Verdict |
|---|---|---|---|
| AC-1 | Parent adds child + sets grade(1–6)/language/country → child created w/ Student role, profile returned | BE-TC-01, BE-TC-02, BE-TC-03, BE-TC-21 | Covered |
| AC-2 | Parent can add more than one child; each gets a distinct account+profile | BE-TC-04, BE-TC-05 | Covered |
| AC-3 | Child login email is the parent-assigned value; child later authenticates with it (UserName = email) | BE-TC-02, BE-TC-22 | Covered |
| AC-4 | Parent action only — endpoint Parent-gated; acting parent from JWT (not body); no child self-create path | BE-TC-15, BE-TC-16, BE-TC-17, BE-TC-18, BE-TC-28 | Covered |
| AC-5 | Each created child auto-linked to acting parent; appears in `GET /api/Parent/My-Children` | BE-TC-19, BE-TC-20, BE-TC-04 | Covered |
| AC-6 | Validation → 422: grade outside 1–6, blank required fields, malformed email, language not in {ar,en} | BE-TC-06..BE-TC-14, BE-TC-25, BE-TC-26 | Covered |
| AC-7 | Duplicate login email rejected with specific "email in use" message, no account created; siblings unaffected | BE-TC-23, BE-TC-24, BE-TC-05 | Covered |
| AC-8 | Child stored language drives locale/RTL at first login (this story persists the value) | BE-TC-03, BE-TC-21, BE-TC-22 | Covered (persistence only; locale consumption is P1-09) |

**Coverage verdict: PASS — all 8 acceptance criteria have at least one P0/P1 case. No uncovered criterion.**

Product-override coverage (CLAUDE.md):
- **No student self-register** → BE-TC-28 (asserts the child has no anonymous create path; only `Register-Parent` is anonymous; child only gets in via Sign-In).
- **Role is server-assigned Student, never client-supplied** → BE-TC-17, BE-TC-18.
- **No teacher role** → BE-TC-18 (a `role`/elevation attempt cannot mint anything but Student; there is no teacher option anywhere on this path).
- **4 subjects / no Social Studies** — not exercised by this endpoint (it provisions accounts, not subjects); noted as out-of-scope-for-this-surface in Open Questions.

---

## 3. Risk notes (where cases are weighted, and why)

1. **Privilege escalation / minor-account safety (highest).** This endpoint mints minor accounts and assigns roles. Heaviest weighting on auth-authz: that `Role`/`ParentId` cannot be injected from the body (BE-TC-17, BE-TC-18), that the acting parent is JWT-resolved only (BE-TC-28), and that the link is created only to the JWT parent (BE-TC-19). A defect here is a cross-family IDOR or a parent minting an Admin.
2. **Validation boundary at grade 1–6.** Off-by-one is the classic defect; BE-TC-06/07/08 pin 0, 7, and the in-range extremes (1 and 6 in BE-TC-21) explicitly.
3. **The 7th field `LearningLanguage` (P8-01 drift).** It is required and easy to forget; omitting it must 422, and it must be persisted distinctly from `Language`. BE-TC-12, BE-TC-13, BE-TC-21 cover it. **If a tester writes a "valid" payload without `LearningLanguage`, every happy-path case will wrongly fail with 422** — flagged in the test-case preconditions.
4. **Duplicate-email status-code confusion.** Story/brief language ("rejected") can read as 422; the code returns **400**. BE-TC-23 asserts the exact 400 + envelope to prevent a tester mis-asserting 422.
5. **Compensating delete on role-assign failure.** Hard to trigger via HTTP alone; flagged as partially-blocked (BE-TC-27) — needs a fault-injection hook the tester likely cannot reach from the API surface; documented rather than dropped.
6. **Password-policy regex vs the plan.** The plan said min-length-8; the code is a complexity regex min-6. BE-TC-09/10/11 pin the real policy; a tester following the plan would assert the wrong thing.

---

## 4. Open questions / assumptions (lead to resolve before/while testers implement)

1. **Q-1 (drift — confirm the endpoint contract is intentional).** The shipped endpoint (`POST /api/Parent/Add-Child`, field names `FullName`/`Email`, the extra required `LearningLanguage`) differs from both the brief and the plan. **Assumption for these cases:** the shipped code is correct and the brief/plan are stale. If the lead intends to align code back to the brief contract, several IDs (request bodies) change. Tester must use the **actual** request schema.
2. **Q-2 (`LearningLanguage` requiredness).** Story P1-03 never mentions a learning language; P8-01 added it as required at add-child. **Assumption:** every Add-Child request in Phase-1-era flows must now include `learningLanguage`. Confirm this is the intended onboarding contract (it affects every P1-03 FE/integration call).
3. **Q-3 (duplicate-email enumeration posture).** AC-7 wants a *specific* "email in use" message; this differs from Link-Child's deliberately-generic anti-enumeration message. **Assumption:** specific message is acceptable here because it's an authenticated parent action. Security-auditor should confirm the same 400 fires regardless of whether the email belongs to a sibling, a foreign family's child, or a parent (no cross-family info leak) — BE-TC-24 probes this.
4. **Q-4 (Age field).** `User.Age` exists but is not in the Add-Child request and not in `AddedChildResponse`. **Assumption:** Age is out of scope for P1-03 add-child; no case asserts it. Confirm.
5. **Q-5 (subjects / no Social Studies / no teacher role).** These product overrides are not exercisable on the account-provisioning surface (no subject or role selection in the request). **Assumption:** they are covered structurally (role hard-coded Student; no teacher enum value exists) rather than by a positive HTTP case. BE-TC-18 is the closest negative assertion. Confirm no additional surface is expected from this story.
6. **Q-6 (compensating-delete observability).** BE-TC-27 (role-assign-failure rollback) likely cannot be triggered from the HTTP surface without a fault hook. **Assumption:** the tester marks it BLOCKED with the reason unless an injection seam exists. Confirm whether a test seam is available.

---

## 5. Handoff

- `backend-test-cases.md` → **`api-tester`** to implement as integration tests against the running API on the P1-03 branch.
- `frontend-test-cases.md` → **not produced** (backend-only run).
- `execution-report.md` → empty template scaffolded by QC; **the testers fill it after running** (pass/fail per case + defects). QC never fills results.
- Defects found during execution: `api-tester` files them back to `backend-feature`; results feed the `reviewer` gate per the workflow in CLAUDE.md.

**Test cases ready — `api-tester` to implement `backend-test-cases.md`; results go into `execution-report.md`.**
