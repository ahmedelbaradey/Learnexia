# P7-08 — Manage child profiles & grade overrides — Frontend (web E2E) test cases (reference)

**Target agent:** `frontend-e2e-tester` (FE lead owns the Next.js `admin-dashboard`).
**Status:** REFERENCE. If the admin dashboard UI is not built, mark all **Blocked (UI not implemented)**.

The UI MUST distinguish the **non-destructive** grade override (soft confirm) from the **destructive** learning-language change (same hard warning + typed confirmation as the parent P8-04 flow).

| ID | Title | Type | Pri | Steps | Expected |
|----|-------|------|-----|-------|----------|
| FE-TC-08-01 | Non-admin cannot reach child-management actions | authz | P0 | Parent opens a child page | Edit/override controls absent; route blocked |
| FE-TC-08-02 | Edit preferredLanguage + country saves, no progress warning | functional | P0 | Edit profile fields | Saved; no destructive warning (harmless write) |
| FE-TC-08-03 | Grade override shows soft confirm, then updates | functional | P0 | Override grade | Soft confirm dialog; on confirm, grade updates; history-preserved messaging |
| FE-TC-08-04 | Learning-language change shows DESTRUCTIVE warning + typed confirm | functional | P0 | Change LearningLanguage | Hard warning ("Math/Science progress will be reset"); typed confirmation required (mirrors P8-04) |
| FE-TC-08-05 | Cancelling language-change dialog performs no reset | state | P0 | Open language dialog, cancel | No request; language + progress unchanged |
| FE-TC-08-06 | Invalid grade (outside 1–6) blocked client-side + server 422 surfaced | validation | P1 | Enter grade 0 / 7 | Inline validation; if submitted, localized 422 message |
| FE-TC-08-07 | Unsupported language rejected with clear message | validation | P1 | Force an unsupported language | Localized validation error, not raw key |
| FE-TC-08-08 | Same-grade override surfaces "no change" message | error | P2 | Set grade = current grade | Localized "grade unchanged" message |
| FE-TC-08-09 | Profile shows both language fields distinctly (UI vs medium) | functional | P1 | Open child profile | PreferredLanguage and LearningLanguage labelled separately |
| FE-TC-08-10 | RTL dialogs + warnings when locale = ar | RTL-i18n | P2 | Switch to Arabic | Override/language dialogs mirror RTL; Arabic copy |
| FE-TC-08-11 | Error surfacing uses i18n copy, not raw envelope keys | error | P1 | Force a 4xx/424 | Friendly localized message |
