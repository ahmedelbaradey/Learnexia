# Coverage Report — P8 Learning-Language FE QC

> Companion to `frontend-test-cases.md` in this folder. Maps every acceptance criterion of the
> three in-scope FE surfaces to the test case(s) covering it, and summarizes counts, risks, and gaps.

## Summary

- **Story scope:** Phase-8 Localization **frontend** — P8-01-FE (set child learning language at add-child), P8-04-FE (parent change learning language), P8-99-FE / app-shell (UI-language switch + RTL foundation), plus axis-independence (UI vs learning language).
- **In scope:** the three built+merged FE surfaces; **out of scope:** backend reset semantics (Math/Science attempt deletion, gamification retention, JWT claim re-issue) — those are `api-tester`/P8-04-BE integration coverage.
- **Total cases:** **42**
  - P8-01-FE (add-child learning language): **14**
  - P8-04-FE (change learning language): **19**
  - P8-SHELL / P8-99-FE (UI-language + RTL foundation): **7** (3 net-new persistence/sweep, 2 overlap-with-existing, 1 duplicate, 1 best-effort)
  - Axis independence (cross-cutting): **3**
- **By priority:** **P0 = 22**, **P1 = 11**, **P2 = 9**.
- **By type:** functional/state ~18, validation/negative/boundary ~10, RTL-i18n ~8, auth-authz ~4, persistence ~4, a11y ~2.

## Coverage matrix

### P8-01-FE — add-child learning language
| Acceptance criterion (brief §P8-01-FE / story) | Case(s) | Covered? |
|---|---|---|
| AC1 — required learning-language choice (ar\|en), phrased as medium of instruction, distinct from UI language | P8-01-TC-01, -03, -04, -09, -10, -11 | ✅ |
| AC2 — submit blocked (zod) when empty; error i18n'd (ar+en) | P8-01-TC-02, -12 (modal) | ✅ |
| AC3 — UI `PreferredLanguage` defaults to match, independently editable | P8-01-TC-05, -06; (modal difference) P8-01-TC-13 | ✅ (onboarding form); ⚠ modal has no auto-fill — see Open Q2 |
| AC4 — learning language sent on add-child mutation | P8-01-TC-07, -08, -12 | ✅ |
| AC5 — no student-facing surface sets/changes learning language | P8-01-TC-14 | ✅ (pending student-login fixture) |
| Story — `/Me`/child profile returns `learningLanguage` (FE surfacing) | P8-01-TC-08 | ✅ (via LL row round-trip) |

### P8-04-FE — change learning language (fresh start)
| Acceptance criterion (brief §P8-04-FE / story) | Case(s) | Covered? |
|---|---|---|
| AC1 — parent-only entry point in settings/manage-child; family-scoped (403 foreign child) | P8-04-TC-01, -02, -12, -18, -19 | ✅ |
| AC2 — different language opens explicit fresh-start warning (resets Math/Science; Arabic/English + XP/streak/badges retained; rare) | P8-04-TC-04, -05 | ✅ (UI copy); reset semantics = backend |
| AC3 — only submitted after explicit confirm; carries `confirmFreshStart:true`; no silent change | P8-04-TC-04, -06, -07, -09, -10, -13 | ✅ |
| AC4 — same language = no-op (disable confirm / "no change") | P8-04-TC-03, -15 | ✅ |
| AC5 — success & error states surfaced; success refreshes child data | P8-04-TC-08, -11, -12, -13, -14 | ✅ |
| Story AC1 — student cannot change | P8-04-TC-18 | ✅ (pending student-login fixture) |

### P8-99-FE — app-shell UI-language foundation + RTL
| Acceptance criterion (brief §P8-99-FE) | Case(s) | Covered? |
|---|---|---|
| Brand fonts render on web | P8-SHELL-TC-07 | ✅ best-effort (visibility, not exact face) |
| UI-language switch promoted to settings + persists to backend | P8-SHELL-TC-01, -02 | ✅ |
| Choice survives sign-out/sign-in | P8-SHELL-TC-03 | ✅ (net-new) |
| RTL flips instantly on web; Arabic default RTL | P8-SHELL-TC-01, -05 | ✅ (TC-05 duplicates existing specs) |
| i18n key-completeness (no hardcoded strings) on built screens | P8-SHELL-TC-06 (P8 surfaces); P8-01-TC-09/-10, P8-04-TC-16/-17 | ✅ for P8 surfaces |
| Login control still works (now in addition to settings) | P8-SHELL-TC-04 | ✅ |
| Native restart-prompt UX on RTL toggle | — | ⚠ **GAP (out of E2E scope)**: web-PWA Playwright cannot exercise `react-native-restart` / the native RestartPrompt. Native-only; not coverable by web-E2E. Logged as known limitation. |

### Axis independence (cross-cutting, derived from P8-01 "separate from PreferredLanguage")
| Property | Case(s) | Covered? |
|---|---|---|
| Changing UI language ⇏ child learning language | P8-AXIS-TC-01 | ✅ |
| Changing child learning language ⇏ parent UI language | P8-AXIS-TC-02 | ✅ |
| Add-child: learning ≠ app language both honored | P8-AXIS-TC-03 | ✅ |

## Coverage verdict

**Every FE acceptance criterion has at least one P0/P1 case**, with **two flagged gaps/caveats**:
1. **Native restart-prompt UX** (P8-99) — not coverable by web-E2E (native RN concern). Out of scope by surface; recommend a native/manual check separately.
2. **Math/Science reset + gamification retention** (P8-04 AC2 semantics) — backend behavior; FE asserts the request flag + UX only. Belongs to `api-tester` (P8-04-BE integration tests). The FE/E2E boundary is explicit.

No FE acceptance criterion is left **uncovered** within the web-E2E surface.

## Risk notes (where cases were weighted)

1. **P8-04 destructive flow (highest risk).** Irreversible Math/Science reset gated only by a UI checkbox + confirm. Weighted with 19 cases: confirm-gating (TC-06), exact request body incl. `confirmFreshStart:true` and the `newLearningLanguage` field-name trap (TC-07), no-mutation-on-cancel/backdrop (TC-09/-10), error/403/424/pending (TC-11..14). A silent or accidental fire here is a data-loss defect.
2. **Two "language" axes confusion.** The product's biggest UX trap. Dedicated axis-independence cases (P8-AXIS-TC-01..03) plus disambiguation (P8-01-TC-11) ensure a UI-language change never silently resets a child's curriculum and vice-versa.
3. **Two divergent add-child surfaces.** `AddChildForm` (onboarding, with auto-fill) vs `AddChildModal` (dashboard, the real user path, **no** auto-fill). Risk that AC3 (default-to-match) is silently absent on the path users actually use — TC-12/-13 surface this explicitly (Open Q2).
4. **Persistence vs in-session toggle.** Existing specs only toggle-and-revert within a session; the actual P8-99 acceptance (survives reload + sign-out/in via backend `PreferredLanguage`) was untested — TC-02/-03 close that.
5. **i18n raw-key leakage on the newest surfaces.** The change-LL flow has the densest new key set (`learningLanguage.*`); a missing key shows the raw dotted path. TC-06 + RTL cases sweep it in both locales.

## Top open questions needing a lead decision (full list in the test-cases doc §6)

1. **Add stable testIDs to the P8-04 change-LL flow** (row/Change/picker/CTA/ack/confirm/modal) — currently only locale-brittle accessible names exist.
2. **Is app-language auto-fill expected in the dashboard `AddChildModal`** (not just the onboarding form)? Decides whether TC-13 is "documented difference" or a defect.
3. **Confirm the FE↔backend test split** for P8-04 reset semantics (FE asserts flag+UX; api-tester asserts the actual Math/Science reset + gamification retention).
4. **Student-login fixture** availability for the parent-only negative cases (TC-14, TC-18).
5. **`⚠` Unicode glyph in the confirm overlay** — file as a design-conformance defect or accept?

## Handoff

- `frontend-test-cases.md` → **`frontend-e2e-tester`** to implement (web PWA, Playwright). Reuse seeding/login helpers from `tests/e2e/specs/rtl-alignment-polish.spec.ts`; extend `parent-lang-check.spec.ts` for P8-SHELL-TC-01 rather than duplicating its harness; cite `rtl-*` specs for the RTL-foundation duplicates instead of re-running them.
- After running, the tester records pass/fail per case ID + any defects in `execution-report.md` in this folder.
- No `backend-test-cases.md` in this run — this scope is FE-only (the P8-01/P8-04 backend endpoints already have BE integration coverage per the briefs).
