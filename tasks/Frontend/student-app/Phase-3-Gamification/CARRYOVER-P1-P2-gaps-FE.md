# Carry-over (Frontend) — Phase 1 & 2 gap closure, scheduled into the Phase 3 wave

> **Type:** carry-over / cleanup batch (not a gamification story). These are the **Phase 1/2 frontend gaps** surfaced by the 2026-06-08 feature gap-analysis (verified against `origin/main`), pulled into the Phase-3 wave. Each task keeps its **original story/task ID**.
> **Source:** feature gap-analysis (Phase 1 + Phase 2, both stacks). Backend for all of these is already on `main`.
> Pair with the BE carry-over: [../../../Backend/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-BE.md](../../../Backend/Phase-3-Gamification/CARRYOVER-P1-P2-gaps-BE.md).

## Tasks
| ID | Origin | Task | Target | Deps | Est (h) |
|---|---|---|---|---|---|
| CO-FE-1 | P1-11-FE-9 | **Build the Reports page (chart-less)** — currently a blank "coming soon" stub. Render KPIs, subject-mastery bars, date-range selector + Send Report. (Charts themselves stay deferred to P5-05.) | `apps/student-app/app/(parent)/reports.tsx` | (BE on main) | 6 |
| CO-FE-2 | P1-11-FE-15 | **Account-locked sign-in message** — add an `accountLocked` branch + `auth.login.errors.accountLocked` i18n key (en/ar). Backend lockout already returns the locked result. | `apps/student-app/app/(auth)/_components/LoginForm.tsx` + `packages/shared/src/i18n/resources.ts` | — | 2 |
| CO-FE-3 | P1-11-FE-16 | **CAPTCHA on Register** — send `captchaToken` (Cloudflare Turnstile) when the server advertises the requirement; backend `TurnstileCaptchaVerifier` + `RegisterParentCommand.CaptchaToken` already exist and go unfed. | `apps/student-app/app/(auth)/_components/RegisterForm.tsx` | — | 3 |
| CO-FE-4 | P1-11b / P1-11-FE-12 | **Marketing landing ar/RTL** — layout hardcodes `lang="en" dir="ltr"` and EN-only copy; add Arabic + RTL so the "renders in en (LTR) and ar (RTL)" AC is met. *(Confirm with product it isn't intentionally EN-first before building.)* | `apps/marketing-site/app/layout.tsx` + `lib/copy.ts` | — | 4 |
| CO-FE-5 | P2-06-FE-2 | **Matching question UI** — replace the `MatchingPanel` stub ("coming soon", submits empty payload) with a real tap/drag pairing UI; submit the pair-mapping payload agreed with CO-BE-1. | `packages/ui/src/components/MatchingPanel/` + `apps/student-app/app/(child)/lessons/[lessonId].tsx` | CO-BE-1 | 5 |
| CO-FE-6 | P1-11-FE-13 / P2-06 | **e2e + QA**: Reports, locked-login, register-with-CAPTCHA, landing ar/RTL, and a Matching quiz flow — drive via the Playwright harness; run the pixel-perfect QA pass against `design-system/screenshots/web/*`. | `frontend-e2e-tester` | CO-FE-1..5 | 4 |

## Acceptance-criteria coverage
- Reports chart-less build → **CO-FE-1** (closes P1-11g) · account-locked UX → **CO-FE-2** (P1-11-FE-15) · Register CAPTCHA → **CO-FE-3** (P1-11-FE-16) · landing ar/RTL → **CO-FE-4** (P1-11b)
- Matching pairing UI + payload → **CO-FE-5** (closes P2-06 frontend gap, paired with CO-BE-1/2)
- Runtime e2e + pixel QA → **CO-FE-6**

## Notes
- Stack per `FRONTEND_ARCHITECTURE.md`: Expo universal / Next.js (marketing) / Tamagui / TanStack Query + Zustand / react-i18next + RTL. No API calls in components (use `api-client` hooks); no server data in Zustand. Pass through the `designer` for any new screen (Reports) before building.
- **Tracker note:** PROGRESS marks P1-12-FE 🟡, but it's effectively complete (profile/avatar/Google/reset/edit-child/consent all wired) — only Apple/Microsoft OAuth buttons remain UI-only by design. Those + confetti-on-correct (Phase 4 polish), AI hints (Phase 4), and plan/billing (payments) are **deferred, not part of this carry-over**.
