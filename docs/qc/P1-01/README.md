# P1-01 — QC Test Plan & Coverage Report (Backend-only)

**Story:** P1-01 — Register as a student or parent (parent self-registration).
**Run scope:** Backend API surface only. No frontend cases this run (`frontend-test-cases.md` intentionally omitted).
**Author:** QC test architect (design only — no test code, no execution).
**Sources:** `user-stories/Phase-1-Foundation/P1-01-register-student-or-parent.md` · `docs/briefs/P1-01.md` · `docs/plans/P1-01.md` · `tasks/Backend/Phase-1-Foundation/P1-01-BE.md` · live controller/command/handler/validator (Identity module).

---

## 1. Summary

The only backend surface for P1-01 is the anonymous endpoint **`POST /api/Users/Authentication/Register-Parent`** (`AuthenticationController`), returning `BaseResponse<JwtAuthResponse>`. It is fully implemented (the original brief contract has since been extended by BE-9 / P1-13 with `Country`, `AcceptedTerms`, and `CaptchaToken` fields — all grounded into the cases below). An xUnit integration suite already exists (`backend/tests/Learnexia.IntegrationTests/P1_01_RegisterParent_Tests.cs`); this catalog supersedes/extends it with explicit traceability and adds the consent, country-bound, captcha-default, case-insensitive-duplicate, boundary, and robustness gaps it does not cover.

**Counts**
| Metric | Value |
|---|---|
| Total cases | **39** |
| Backend (`api-tester`) | 39 |
| Frontend | 0 (out of scope this run) |
| P0 | 18 |
| P1 | 12 |
| P2 | 9 |
| Conditional / environment-dependent | 2 (BE-TC-31 captcha-enabled; BE-TC-39 seeded accounts) |

**In scope:** the register endpoint, its validation envelope (422), business-rule envelope (400), success envelope (200), password hashing/hygiene, server-side role assignment, and the negative space proving no anonymous child/Student creation path. Regression on existing sign-in (seed change).

**Out of scope this run:** all student-app web/native UI (separate FE pipeline), `Sign-In`/`Refresh`/`Sign-Out` except where used as a round-trip oracle, child provisioning (P1-03), the captcha-enabled internals (owned by `P1_13_BE4_Captcha_Tests.cs`).

---

## 2. Coverage matrix (every acceptance criterion → case IDs)

### Story acceptance criteria (source of truth)
| Story AC | Covered by | Verdict |
|---|---|---|
| Valid email+password → parent account created + JWT returned | BE-TC-01, 02, 03, 04, 05, 32 | ✅ covered |
| Child account is NOT self-registered (parent-provisioned only) | BE-TC-27, 28, 29 | ✅ covered |
| Already-registered email → clear error, no duplicate account | BE-TC-18, 19, 20, 21 | ✅ covered |
| Weak password → blocked with specific message | BE-TC-09, 10, 11, 12, 13, 14 (negative); BE-TC-15 (allowed boundary) | ✅ covered |
| Passwords stored hashed, never returned | BE-TC-04, 25, 26 | ✅ covered |

### Brief testable AC (AC-1 … AC-6)
| Brief AC | Covered by | Verdict |
|---|---|---|
| AC-1 Happy path → 200, `Successed=true`, non-empty `AccessToken`, Parent role | BE-TC-01, 02, 03, 04, 05, 06, 07, 32 | ✅ |
| AC-2 No anonymous child/Student; role server-decided | BE-TC-05, 27, 28, 29 | ✅ |
| AC-3 Duplicate email rejected, no second account | BE-TC-18, 19, 20, 21 | ✅ |
| AC-4 Weak password blocked before user creation | BE-TC-09…14, 15 | ✅ |
| AC-5 Password hashed, never echoed | BE-TC-04, 25, 26 | ✅ |
| AC-6 Validation → 422 `BaseResponse` with `Errors[]` `{PropertyName, ErrorMessage}` | BE-TC-09…14, 16, 17, 22, 23, 24, 33, 34, 35 | ✅ |

### Post-brief extensions (BE-9 / P1-13 — present in the running command, covered as no-regression)
| Behaviour | Covered by | Verdict |
|---|---|---|
| Terms consent mandatory (`AcceptedTerms` must be true) | BE-TC-22, 23 | ✅ |
| Country optional + length-bound (≤100) | BE-TC-08, 24 | ✅ |
| Captcha gate transparent when disabled (default) | BE-TC-30 | ✅ |
| Captcha blocks when enabled | BE-TC-31 | ⚠️ conditional (harness toggle) |
| Seed-change regression on existing sign-in | BE-TC-39 | ⚠️ environment-dependent |

**Coverage verdict: every story acceptance criterion and every brief AC (AC-1…AC-6) has at least one P0/P1 case. No uncovered criterion.** Two cases are conditional on harness/environment capability (captcha toggle, seeded legacy accounts) and are explicitly flagged rather than dropped.

---

## 3. Risk notes (where cases are weighted, and why)

1. **Anonymous account-creating endpoint (highest risk).** It mints accounts and issues JWTs with no auth. Weighted heavily on the negative space: role-escalation via extra JSON (BE-TC-27), no `Register-Student` route (BE-TC-28), no anonymous `AddUser` (BE-TC-29). This is the product's hard line ("children are parent-provisioned, no self-register, no teacher role").
2. **Duplicate-account integrity & enumeration.** Two rejection paths exist (validator 422 vs handler 400 backstop) plus Identity email normalization. Cases probe both paths, case-insensitivity (BE-TC-20), and whitespace bypass (BE-TC-21) — a case-bypassable uniqueness check would be both a data-integrity bug and an enumeration oracle.
3. **Password policy parity.** The validator regex must match the configured Identity policy exactly; a gap lets a weak password through the handler's defense-in-depth. Per-rule coverage (BE-TC-09…14) plus the allowed minimum boundary (BE-TC-15) pin both sides.
4. **Password hygiene (AC-5).** Plaintext leakage in any response/log is a P0 security defect — checked on both success and failure paths (BE-TC-25) and proven indirectly via hash round-trip (BE-TC-26).
5. **Seed-change blast radius.** The plan's RoleSeeder casing/idempotency change touches startup seeding for all roles; BE-TC-39 guards against regressing existing seeded sign-ins.

---

## 4. Open questions / assumptions (lead to resolve before/with implementation)

1. **Q1 — Captcha toggle in the integration harness.** Can the `api-tester` host run with `Captcha:Enabled=true` and a stub verifier to exercise BE-TC-31? If not, that case stays BLOCKED and we rely on `P1_13_BE4_Captcha_Tests.cs`. **Assumption:** default Testing profile has captcha disabled; BE-TC-31 is conditional.
2. **Q2 — Role-claim observability.** BE-TC-05 / BE-TC-27 need to confirm the created user is in role `Parent` only. Preferred mechanism: an authenticated `Me`/profile endpoint that returns roles, else decode the JWT `role` claim. **Lead: confirm which is available**, otherwise the tester documents the JWT-decode method used.
3. **Q3 — Seeded legacy accounts in Testing.** Does the Testing environment seed `superadmin`/`basicuser` (BE-TC-39)? The existing test asserts it does in dev; the comments suggest Testing may not. **Assumption:** if absent, BE-TC-39 is N/A and the round-trip (BE-TC-04) covers the regression intent.
4. **Q4 — Duplicate-email status code (422 vs 400).** Brief Q5 flagged the FE wants a single shape. Cases accept either but BE-TC-19 will flag when the 400 backstop fires instead of the preferred 422 `Errors[]`. **Lead: confirm whether 400-on-duplicate is acceptable to ship** or must always route through the validator. No new design pattern proposed here — flagging only.
5. **Assumption — `refreshToken` may be empty on register** (brief Q2: `GetJwtToken` returns access token only at parity with SignIn). No case asserts a populated `refreshToken`; if that becomes a contract requirement, add a case.

---

## 5. Handoff

| File | Goes to | Action |
|---|---|---|
| `docs/qc/P1-01/backend-test-cases.md` | `api-tester` | Implement BE-TC-01…39 1:1 as integration tests against the running API; honour the conditional/N-A markers. |
| `docs/qc/P1-01/frontend-test-cases.md` | — | Not produced (backend-only run). |
| `docs/qc/P1-01/execution-report.md` | `api-tester` (filler) | Empty template scaffolded by QC. The tester fills pass/fail per case + defects **after** running. QC never fills results. |

**Reviewer gate:** the filled `execution-report.md` feeds the `reviewer` batch against AC-1…AC-6. Any P0 failure blocks; conditional cases must be recorded as not-run-with-reason, not silently passed.
