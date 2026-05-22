## Wave 4 — Phase 1 frontend: auth/onboarding + admin sign-in (+ FE foundation)

Two stories, each through the full pipeline (analyzer → planner → designer → frontend/backend → api-tester/security-auditor → reviewer **PASS**). This wave also lands the shared **FE foundation** both apps build on.

- **P1-09 — Auth & onboarding screens (Expo student app) + FE foundation + `Me` endpoint.** Commits `1126a91` (+ merge `e84a682`).
  - **Expo SDK 52** universal student-app shell (Expo Router + provider stack) with screens: parent **register**, shared **sign-in** (parent + child), **onboarding/profile**, **add-children** (multi-child + per-child success/failure), **link-child**, **my-children**/switcher, and a routing guard driven by `GET /api/Users/Me` (role + onboarding + language→locale). No anonymous student self-registration.
  - **Shared Tamagui `packages/ui` primitives** (web-safe, reused by the admin app): `TextField`/`FormField`, `Select`/`GradePicker`/`LanguageSelect`, `ProgressSteps`, `ChildCard`.
  - **api-client switched to NSwag**-generated typed client (replaces openapi-typescript; documented as the standard in FRONTEND_ARCHITECTURE). The single-flight 401-refresh `apiClient` transport + the public hook surface (`useSignIn`, `ApiClientProvider`, …) are preserved; new hooks `useMe`/`useRegisterParent`/`useAddChild`/`useLinkChild`/`useMyChildren`/`useSignOut`.
  - **Backend `GET /api/Users/Me`** — self-scoped (id, roles, fullName, preferredLanguage, isFirstLogin, hasChildren). Me api-tester **176/176**; security-auditor **PASS** (no IDOR/leakage).
- **P1-10 — Admin dashboard sign-in + shell.** Commits `5cedd6a` (+ merge `1d91c15`).
  - Next.js 15 App Router admin app (`@tamagui/next-plugin`) — sign-in built on the **same shared Tamagui `packages/ui` primitives** (reworked off the initial CSS-modules/plain-input approach for a unified design system), **JWT-decode admin role gate** (case-insensitive Admin/SuperAdmin, fail-closed), authenticated dashboard shell (placeholder nav + identity + sign-out), client route guard (loading skeleton, no flash), login-only (no admin self-registration). BE is verification-only (reuses the seeded superadmin + `AdminOnly`). security-auditor **PASS**.

### Tests & verification
- Backend **builds clean** on the merged wave (0 errors). Me endpoint integration tests 176/176.
- Frontend **type-check + lint + build green** for all packages, the Expo app, and the Next.js admin app.
- **CI (ubuntu-latest) is the source of truth for installs/builds.** `pnpm install` currently fails on the Windows dev box (a known Defender/symlink-rename contention issue — local-only, not a code problem); local verification used workspace junctions. **`pnpm-lock.yaml` is intentionally not regenerated in this branch — CI regenerates/validates it on ubuntu.**

### Follow-up debt (non-blocking)
- Admin: HttpOnly-cookie auth + CSP/security headers (client-only route guard today); rotate the seeded admin password before any non-dev deploy.
- Expo **SDK 53** bump deferred (SDK 52 + RN 0.76 New Architecture already meets the requirement; bumping now would force a native re-resolve during the unstable local install).
- Touch up the P1-10 design spec to reflect the shared-`TextField` rework.
- Consider **WSL2** for local pnpm/Expo stability.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
