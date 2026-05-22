# Learnexia — Frontend Architecture

> **Status:** decided (2026-05). Owner: solo full-stack dev + AI pair.
> **Decision:** a Turborepo monorepo with **Expo universal** for the student experience and **Next.js** for admin + marketing, sharing UI/logic packages.

## 1. Why this shape

The product has two very different frontend surfaces:

- **Student experience** — gamified, animation-heavy, kid-facing; must run on **web (laptop/tablet)** *and* **native mobile** (mobile is a co-priority). One person cannot maintain two separate UIs for this, so it is built **once** as a universal Expo app (React Native + React Native Web).
- **Admin + marketing** — data-dense admin (curriculum upload/content) and a public SEO marketing site. These are exactly where RN-Web is weak and Next.js is strong, so they live in **Next.js** apps.

Shared design tokens, UI components, the API client, and domain logic live in **packages/** so both runtimes stay DRY. UI is shared via **Tamagui**, which compiles the same components to React Native (Expo) and the web (Next.js).

## 2. Repository layout

```
learnexia/
├── apps/
│   ├── student-app        Expo (SDK 53, RN 0.76+ New Arch) + Expo Router + RN Web
│   │                      → web PWA (390/768/1024) + iOS/Android from one codebase
│   │                      → hosts: auth, parent onboarding, parent dashboard, all student screens
│   ├── admin-dashboard    Next.js 15 (App Router) — curriculum upload & content mgmt (Phase 2+/Backlog)
│   └── marketing-site     Next.js 15 (SSG/ISR) — public, SEO
│
├── packages/
│   ├── design-system      Tamagui config: tokens, themes, fonts, media queries (single source of truth)
│   ├── ui                 Shared Tamagui components (universal: Expo + Next)
│   ├── api-client         Typed .NET API client + TanStack Query hooks (framework-agnostic)
│   └── shared             Domain types, Zustand stores, i18n, zod validation, constants
│
└── backend/               .NET 10 modular monolith (see ../architecture.md)
```

## 3. Toolchain

| Need | Choice | Notes |
|---|---|---|
| Monorepo | **Turborepo + pnpm workspaces** | Task graph + caching; pnpm for strict deps |
| Language | **TypeScript** (strict) | everywhere, incl. packages |
| Student app | **Expo** SDK 53, RN 0.76+ (New Arch), **Expo Router**, RN Web | universal web + native |
| Admin / marketing | **Next.js 15** App Router | data-dense + SEO surfaces |
| Shared UI | **Tamagui** | universal components + `@tamagui/next-plugin` for the Next apps |
| Animation | **Reanimated 3 + Moti + React Native Skia** | XP/streak/confetti/skill-tree; Skia for canvas-grade FX |
| Client state | **Zustand v5** | auth, child context |
| Server state | **TanStack Query v5** | caching, retries, later AI streaming |
| Forms / validation | **react-hook-form + zod** | zod schemas shared in `packages/shared` |
| i18n | **react-i18next** | ar/en; RTL handling (see §6) |
| Testing | **Jest + RN Testing Library** (student-app), **Vitest** (packages), **Playwright** (web), **Maestro** (native E2E) | |

## 4. Package responsibilities

| Package | Owns |
|---|---|
| `design-system` | Tamagui `createTamagui()` config: colors (`#4F46E5` primary, `#22C55E` success, `#F59E0B` reward, `#EF4444` danger, `#A855F7` badge; bg `#0F172A`, card `#1E293B`), spacing 4–48, radius 8/16/20/24, shadows/glow, fonts (Poppins / Cairo / Tajawal), media queries (`sm`, `tablet`=768, `laptop`=1024), light + dark themes |
| `ui` | Universal Tamagui components: `Button`, `Card`, `XPBar`, `Hearts`, `StreakFlame`, `Badge`, `AITutorBubble`, `RewardPopup`, `SkillNode`, `LessonCard`, `QuizCard`. Naming `Component/Category/Variant` |
| `api-client` | Typed client to the .NET API; **NSwag** generates a full typed Fetch client (DTOs + method per endpoint) from the committed Swagger v2 snapshot (`refresh:swagger` → `gen:api`); `BaseResponse<T>` / `PaginatedResult<T>` envelope handling; JWT attach + single-flight refresh interceptor; TanStack Query hooks wrap the generated methods through the transport |
| `shared` | Domain types (Subject/Unit/Lesson/Concept/Skill, Attempt, StudentAnswer…), constants (`Roles`, grades 1–6, **4 subjects**), zod schemas, i18n resources (ar/en) + RTL helpers, Zustand stores (`authStore`, `childContextStore`), utils |

## 5. App responsibilities

- **student-app (Expo universal)** — everything in Phases 1–5 that a student or parent touches: splash, parent register/login, **parent-driven onboarding & add-children**, child login, home dashboard, subject selection, skill tree, lesson, quiz, feedback, gamification screens, and the **parent dashboard** (charts via `victory-native`/Skia).
- **admin-dashboard (Next.js)** — **admin sign-in + dashboard shell start in Phase 1 (story P1-10)**; the data-heavy features (curriculum upload + metadata, content management, moderation config) come in Phase 2+/Backlog. Data-dense: TanStack Table + Recharts on Tamagui primitives.
- **marketing-site (Next.js)** — public marketing, SEO (SSG/ISR). Not in the MVP feature phases.

## 6. Cross-cutting concerns

- **Theming/tokens:** one Tamagui config in `design-system`; Expo loads it via Metro, Next via `@tamagui/next-plugin`. Default theme is the dark game-world palette; tokens drive every component.
- **RTL / i18n:** `react-i18next` + Tamagui logical props. On native, flipping LTR↔RTL requires an app reload (`react-native-restart`); design the language-switch UX around that. Fonts: Cairo/Tajawal (ar), Poppins (en).
- **Auth token storage:** abstract in `shared` — `expo-secure-store` on native, secure cookie / web storage on web. Refresh interceptor lives in `api-client`.
- **Responsive web:** Tamagui media queries target 390 (phone), 768 (tablet), 1024+ (laptop); cap content with max-width containers; add web-only hover/focus states.
- **State split:** Zustand for client/UI state, TanStack Query for all server data (no server data in Zustand).

## 7. Build order

1. Turborepo + pnpm workspaces skeleton (depends on DevOps story P1-07).
2. `design-system` Tamagui config → `ui` core components (story **P1-08**).
3. `api-client` + `shared` (auth store, i18n, types) — foundation for all data calls.
4. `student-app` shell: Expo Router, theme + i18n/RTL providers, responsive layout.
5. Feature screens in Phase 1 → Phase 2 story order.

## 8. Conventions

- **Where code goes:** reusable visuals → `packages/ui`; tokens/theme → `packages/design-system`; anything that calls the API → `packages/api-client`; types/stores/validation/i18n → `packages/shared`; screens/routes → the relevant `apps/*`.
- **No API calls in components** — go through `api-client` hooks.
- **No server data in Zustand** — that's TanStack Query's job.
- **Imports** use workspace aliases (`@learnexia/ui`, `@learnexia/api-client`, …).
- Components follow `Component/Category/Variant`; screens are Expo Router route files.
- **API client generation standard:** **NSwag** is the generator for `api-client`
  (config in `packages/api-client/nswag.json`). Flow: `refresh:swagger` pulls the
  Swagger v2 snapshot from the running backend → `gen:api` runs NSwag to emit
  `src/generated/nswag-client.ts` (typed `Client` + DTO interfaces) → hooks wrap
  the generated methods via `createTypedClient` + `unwrapEnvelope`, reusing the
  single-flight 401-refresh transport. This **supersedes** the earlier
  `openapi-typescript` types-only approach. The generated client is committed; do
  not hand-edit `src/generated/**`.

## 9. Open questions

- RTL language-switch UX on native (reload requirement). *(open)*
- Token storage abstraction details across native/web. *(open)*
- Whether the parent dashboard's richer analytics eventually moves to `admin-dashboard` (Phase 5 decision).
- Tamagui compiler setup time — steepest part of the stack; budget for it.

> Task-level breakdown lives in [../../tasks/](../../tasks/) (Frontend split into `student-app/` + `packages/`).
