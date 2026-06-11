# Turn on push notifications (Expo permission + device registration)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 9 — Notifications (post-MVP)
- **Epic:** Notifications Module
- **Issue type:** Story
- **Story Points:** 5 — Expo push setup + child-appropriate permission priming + device-token lifecycle against the existing API.
- **Labels:** `notifications`, `frontend`, `expo`, `habit`, `push`
- **Requirements:** FR-GM-8 (re-engagement notifications — SRS §4.6); closes business-gap-analysis **Gap A** (push end-to-end non-functional).

## Description
As a student (with my parent's consent), I want the app to ask permission and register my device for push notifications, so that the habit nudges the backend already produces (streak danger, comeback, achievements) can actually reach me.

> **Why this story exists:** the P4-09 backend nudge engine is merged — `ExpoPushSender`, `DevicesController` (`POST /api/Devices/Register`, revoke), `NudgeDispatcher`, per-child preferences — but **no client ever registers a device token**, so every push silently goes nowhere. This is the single highest-impact engagement gap at launch. This story builds only the missing client foundation; it does not change the backend.

## Acceptance Criteria
- The app integrates `expo-notifications`; on a deliberate, child-appropriate moment (not a cold first-launch wall), a **permission priming screen** explains the value ("نفكّرك تكمل سلسلتك 🔥") before the OS prompt fires.
- On permission granted, the app obtains the Expo push token and registers it via the existing `POST /api/Devices/Register` (device token, platform, locale); registration is idempotent and re-syncs on app launch and on token refresh.
- On sign-out (and on account switch), the device token is revoked via the existing revoke endpoint so a child's device never receives another child's nudges.
- Permission **denied / undetermined** is handled gracefully — the app still works, in-app inbox (P9-03) remains the fallback channel, and the parent can re-prompt later from settings.
- Parent-consent posture is respected: registration is gated by the parent-controlled preference state already in the backend; the child cannot enable a channel the parent disabled.
- Works on native (iOS/Android via Expo) and degrades cleanly on web (web-push handled in P9-02); no crash where the notifications API is unavailable.

## Notes
- **Security/privacy:** child device — route through `security-auditor` (no PII in token payloads, parent is consent authority). COPPA-appropriate.
- Consumes existing backend: `DevicesController`, preference state from `PreferencesController`. **Builds on P4-09 — do not duplicate the dispatcher or token store.**
- Blocks: P9-02 (deep links need a registered receiver), P9-03 uses the same inbox API independently.
- `analyzer` + `planner` first per CLAUDE.md.
