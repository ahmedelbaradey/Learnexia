# Frontend Dev Manual — for a React dev new to this repo

> You know React. You do **not** need to know Expo, Tamagui, or Next.js up front — this maps
> everything to React concepts and tells you *which file to open* when you need to fix something.
> Grounded in the actual code as of branch `fix/frontend-rtl-alignment-polish`.

---

## 0. The one thing to internalize first

This monorepo has **three apps that use TWO different routing systems**. Don't mix them up:

| App | Folder | Framework | Routing | Dev port |
|---|---|---|---|---|
| **Student app** (parent + child + auth + onboarding) | [apps/student-app/](../../apps/student-app/) | **Expo Router** (React Native + `react-native-web`) | File-based, `app/` dir, `_layout.tsx` | **8081** |
| **Admin dashboard** | [apps/admin-dashboard/](../../apps/admin-dashboard/) | **Next.js** (App Router) | File-based, `app/` dir, `layout.tsx` | **3001** |
| **Marketing site** | [apps/marketing-site/](../../apps/marketing-site/) | **Next.js** (App Router) | File-based, `app/[locale]/` | **3002** |

The student app is the big one (parent + student/child UIs live *inside* it as route groups — they are **not** separate apps). Admin and marketing are plain Next.js sites you already understand.

**Shared packages** (used by all apps) live in [packages/](../../packages/):

| Package | What it is | React analogy |
|---|---|---|
| [`@learnexia/ui`](../../packages/ui/) | ~40 design-system components (Button, TextField, Card, ChildCard…) | your shared component library |
| [`@learnexia/design-system`](../../packages/design-system/) | tokens (colors, fonts), the Tamagui provider, RTL helpers | your theme + CSS variables |
| [`@learnexia/api-client`](../../packages/api-client/) | generated HTTP client + **TanStack Query hooks** (`useMe`, `useMyChildren`…) | your `api/` + react-query hooks |
| [`@learnexia/shared`](../../packages/shared/) | Zustand stores (`authStore`), i18n, types, token storage | your global state + utils |

---

## 1. Mental model: Expo/Tamagui vs the React you know

The student app is the unfamiliar one. Five differences that matter:

1. **Routing is file-based (Expo Router), not `<Routes>`.** There is no central router config. A file at `app/(parent)/overview.tsx` *is* the route `/overview`. Folders in `(parentheses)` are **route groups** — they organize files and share a layout **without adding a URL segment**. This is the same idea as Next.js App Router, just from Expo.

2. **`_layout.tsx` = nested layout + the place guards live.** Like Next.js `layout.tsx` / a React Router layout route. A `_layout.tsx` wraps every route in its folder. `<Slot />` is where children render (think `<Outlet />`).

3. **No HTML/CSS. You write React Native primitives styled by Tamagui.** Instead of `<div className="...">` you write `<Stack>` / `<Text>` from `@tamagui/core` with **props** for style: `<Stack flex={1} padding="$4" backgroundColor="$bg">`. Values like `$4`, `$bg`, `$primary` are **design tokens** (see [packages/design-system/src/tokens/](../../packages/design-system/src/tokens/)). On web these compile to CSS; on native they're RN styles. There is no `className`. There is no stylesheet file per component.

4. **It runs on web AND native from one codebase** via `react-native-web`. `Platform.OS === 'web'` branches handle web-only concerns (DOM, `@font-face`, scrollbars). You'll mostly care about web. Don't delete the native branches.

5. **RTL is first-class.** The app flips between English (LTR) and Arabic (RTL). Layout direction comes from the document `dir` (web) / `I18nManager` (native), not from per-component `row-reverse` — adding `row-reverse` on top of an already-flipped container **double-flips** it (a real recurring bug here). Use logical props and `direction` from `useLocale()`.

> Admin + marketing are normal Next.js + React. Admin uses Tamagui too; marketing uses plain CSS Modules (`*.module.css`).

---

## 2. Run the apps

Prereqs: **Node ≥20**, **pnpm 9.15** (`packageManager` is pinned), Docker (for the backend DB). All commands run from the repo root **inside WSL2**.

```bash
# once
pnpm install
```

### Backend (needed for anything that loads data)
```bash
# 1. Postgres
docker compose -f docker/docker-compose.yaml up -d postgres
# 2. API on :5080 (migrates + seeds a fresh DB on first boot)
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://localhost:5080 \
AllowedOrigins=http://localhost:8081,http://127.0.0.1:8081 \
dotnet run --no-launch-profile --project backend/src/Host/Learnexia.Host
```

### Student app (web PWA) — port 8081
```bash
cd apps/student-app
npx expo start --port 8081        # open http://localhost:8081
# native: npx expo start  then press i (iOS) / a (Android)
```
- API base URL comes from `apps/student-app/.env.local` → `EXPO_PUBLIC_API_BASE_URL=http://localhost:5080` (**gitignored**, create it if missing). Default fallback if unset is `https://localhost:7080` (see [apiBaseUrl.ts](../../apps/student-app/src/providers/apiBaseUrl.ts)).
- First web bundle takes ~50s. In a sandboxed/offline shell prepend `EXPO_OFFLINE=1 CI=1`.

### Admin dashboard — port 3001
```bash
pnpm --filter @learnexia/admin-dashboard dev   # → http://localhost:3001
```

### Marketing site — port 3002
```bash
pnpm --filter @learnexia/marketing-site dev    # → http://localhost:3002  (redirects to /en)
```

### Whole monorepo at once
```bash
pnpm dev          # turbo runs every app's dev script in parallel
pnpm type-check   # tsc --noEmit across all packages (your fastest "did I break it" check)
pnpm lint
```

**Sanity check it's working:** student app boots to the splash, then redirects to `/(auth)/login` (signed-out). Logging in as a parent lands on `/overview`; as a student on the child home.

---

## 3. Routing — how to find the file behind a URL

### Student app (Expo Router)

Everything lives under [apps/student-app/app/](../../apps/student-app/app/). Rules:

- A file = a route. `overview.tsx` → `/overview`. `index.tsx` → the group's root.
- `(name)` folder = **route group**: shared layout, **no URL segment**. So `(parent)/overview.tsx` is still just `/overview`.
- `[param].tsx` = dynamic segment. `(child)/lessons/[lessonId].tsx` → `/lessons/42`.
- `_layout.tsx` wraps its folder; `<Slot/>` (or `<Tabs/>`/`<Stack/>`) renders the child route.

The four groups:

| Group | Folder | Who | Layout style |
|---|---|---|---|
| `(auth)` | [app/(auth)/](../../apps/student-app/app/(auth)/) | signed-out | login / register / forgot- & reset-password |
| `(onboarding)` | [app/(onboarding)/](../../apps/student-app/app/(onboarding)/) | parent with 0 children | add-child → complete |
| `(parent)` | [app/(parent)/](../../apps/student-app/app/(parent)/) | parent | **web shell**: header + sidebar + `<Slot/>` |
| `(child)` | [app/(child)/](../../apps/student-app/app/(child)/) | student | **`<Tabs/>`** with floating `ChildTabBar` |

Boot/entry files:
- [app/_layout.tsx](../../apps/student-app/app/_layout.tsx) — the **root provider stack** (see §6) + i18n/font/token boot. Edit here for anything app-wide.
- [app/index.tsx](../../apps/student-app/app/index.tsx) — the **splash screen** + where the redirect guard (`useAuthRoute`) is mounted.

**"Which file renders `/reports`?"** → it's a parent page → [app/(parent)/reports.tsx](../../apps/student-app/app/(parent)/reports.tsx). The visible layout (header/sidebar) is in [(parent)/_layout.tsx](../../apps/student-app/app/(parent)/_layout.tsx).

**Child tab bar quirk:** in [(child)/_layout.tsx](../../apps/student-app/app/(child)/_layout.tsx), screens are registered as `<Tabs.Screen>`. Real tabs (home/missions/league/badges) show on the bar; `href: null` screens (xp, streak, hearts, lessons…) are push screens that hide the bar via an allowlist (`TAB_BAR_VISIBLE_ROUTES`) in `_components/ChildTabBar.tsx`.

### Admin dashboard (Next.js App Router)
- [app/layout.tsx](../../apps/admin-dashboard/app/layout.tsx) root → [app/(admin)/layout.tsx](../../apps/admin-dashboard/app/(admin)/layout.tsx) wraps the authed area in `AdminShell`.
- [app/login/page.tsx](../../apps/admin-dashboard/app/login/page.tsx) is the public login; [app/(admin)/dashboard/page.tsx](../../apps/admin-dashboard/app/(admin)/dashboard/page.tsx) the landing.

### Marketing site (Next.js App Router)
- Locale-segmented: [app/[locale]/page.tsx](../../apps/marketing-site/app/[locale]/page.tsx) is `/en` and `/ar`. `middleware.ts` redirects bare `/` → `/en`.
- No auth. Copy is **not** i18n-lib driven — it's locale-keyed objects in [lib/copy.ts](../../apps/marketing-site/lib/copy.ts). Styling is CSS Modules (`*.module.css`).

---

## 4. Route guards / auth — where redirects happen

### Student app — client-side, two hooks (keep them in sync)
Both read `authStore.status` (`unknown | signed-out | signed-in`) + the `useMe()` profile query, and redirect with `router.replace`. While `status === 'unknown'` (hydrating tokens) or `Me` is loading, **they render nothing / stay on splash** — no content flash.

- [src/hooks/useAuthRoute.ts](../../apps/student-app/src/hooks/useAuthRoute.ts) — mounted on the **splash** (`app/index.tsx`). Decides the *initial* destination:
  - signed-out → `/(auth)/login`
  - parent, no children → `/(onboarding)/add-child`
  - parent, has children → `/(parent)/overview`
  - student → `/(child)`
- [src/hooks/useGroupGuard.ts](../../apps/student-app/src/hooks/useGroupGuard.ts) — called inside each group's `_layout.tsx` (`useGroupGuard('(parent)')` etc.). Guards **direct navigation** (someone typing `/children` in the URL): bounces wrong-role/signed-out users. It deliberately mirrors `useAuthRoute` — **if you change redirect rules, change both.**

Auth state itself lives in [packages/shared/src/stores/authStore.ts](../../packages/shared/src/stores/authStore.ts) (Zustand). Tokens persist via `TokenStorage` (Keychain on native, sessionStorage on web). `signOut()` clears them.

### Admin — client-side guard only (documented limitation)
- [lib/hooks/useAdminGuard.ts](../../apps/admin-dashboard/lib/hooks/useAdminGuard.ts) decodes the JWT's role claims; non-admins get `router.replace('/login')`. Used by `AdminShell` inside `(admin)/layout.tsx`.
- [middleware.ts](../../apps/admin-dashboard/middleware.ts) is a **pass-through stub** — server-side enforcement is deferred (tokens live in sessionStorage, which the edge runtime can't read). So a non-admin may briefly receive shell HTML before the client redirect. The backend `AdminOnly` policy is the real gate.

### Marketing — no auth
[middleware.ts](../../apps/marketing-site/middleware.ts) only does locale routing (`/` → `/en`, injects `x-locale` header).

---

## 5. Pages vs components — where does UI live?

Four tiers, narrowest scope first:

1. **Route file** (`app/(group)/foo.tsx`) — a page. Composes components, calls data hooks.
2. **Route-local components** (`app/(group)/_components/…`) — used by **one group only**. The `_` prefix stops Expo Router from treating them as routes. e.g. [(parent)/_components/Sidebar.tsx](../../apps/student-app/app/(parent)/_components/Sidebar.tsx).
3. **App-shared components** ([apps/student-app/src/components/](../../apps/student-app/src/components/)) — used across groups within the student app, e.g. `AddChildModal`, `DotPulse`, `RestartPrompt`.
4. **Cross-app design-system components** ([packages/ui/src/components/](../../packages/ui/src/components/)) — `Button`, `TextField`, `ChildCard`, `Card`… reused by student-app **and** admin. **Editing one here affects every app.**

**Decision rule:** used by one screen → `_components/`. Reused in the student app → `src/components/`. A generic primitive any app would want → `@learnexia/ui`.

Admin mirrors this: page-local in [apps/admin-dashboard/components/](../../apps/admin-dashboard/components/), helpers in `lib/`. Marketing: section components in [app/_components/](../../apps/marketing-site/app/_components/).

---

## 6. Component hierarchy — one real screen traced

The **parent dashboard at `/overview`** (signed-in parent), top to bottom:

```
app/_layout.tsx  ........... RootLayout — provider stack + boot
  SafeAreaProvider
   └ LearnexiaProvider ...... Tamagui theme + locale + RTL  (@learnexia/design-system)
      └ QueryClientProvider .. TanStack Query (server cache)
         └ ApiClientProvider .. HTTP client w/ auth + refresh  (@learnexia/api-client)
            └ <Slot/>  ........ Expo Router renders the matched group
               │
               └ app/(parent)/_layout.tsx  ... ParentLayout
                    useGroupGuard('(parent)') ..... role/auth gate
                    [ web shell: header + Sidebar + ScrollView ]
                    AddChildModal (mounted here, opened from anywhere via Zustand)
                       └ <Slot/>
                          └ app/(parent)/overview.tsx  ... the page
                               OverviewWeb  (_components/OverviewWeb.tsx)
                                 ├ KPIStatCard        ← @learnexia/ui
                                 ├ DailyActivityCard  ← _components/
                                 ├ SubjectMasteryCard ← _components/
                                 ├ FocusAreasCard     ← _components/
                                 └ RecommendationsCard← _components/
```

**Data flow:** pages/components call **TanStack Query hooks** from `@learnexia/api-client` (`useMe`, `useMyChildren`, `useDashboard`…). Those hit the API client wired in the root layout. **Server data is never in Zustand** — Zustand (`@learnexia/shared/stores`) holds only auth + UI state (e.g. `activeChildStore` for which child is selected and whether the Add-Child modal is open). This split is a project rule.

> Cross-layout prop passing: Expo Router layouts **can't forward props into `<Slot/>` pages**. The pattern used here is a small Zustand store instead (see `activeChildStore.openAddChild()` in the parent layout).

---

## 7. "I need to fix X" playbook

| Symptom | Look here | What to do |
|---|---|---|
| **A style/spacing/color is wrong on one screen** | the page or its `_components/*` | Edit Tamagui props (`padding`, `gap`, `backgroundColor`). Use **tokens** (`$4`, `$primary`), not raw hex. Tokens defined in [packages/design-system/src/tokens/](../../packages/design-system/src/tokens/). |
| **A color/spacing is wrong everywhere** | [packages/design-system/src/tokens/colors.ts](../../packages/design-system/src/tokens/colors.ts) | Change the token; all apps update. |
| **A shared component (Button/TextField/Card) misbehaves** | [packages/ui/src/components/](../../packages/ui/src/components/) | Fix once — **but it changes every screen + app**. Type-check the whole repo after (`pnpm type-check`). |
| **Arabic layout flipped/misaligned, button unclickable in AR** | the component's flex direction | Don't stack `row-reverse` on an already-RTL container (double-flip). Use `direction`/`isRtl` from `useLocale()`; prefer logical alignment. (This exact class of bug is why this branch exists.) |
| **Wrong page after login / can't reach a page / redirect loop** | [useAuthRoute.ts](../../apps/student-app/src/hooks/useAuthRoute.ts) + [useGroupGuard.ts](../../apps/student-app/src/hooks/useGroupGuard.ts) | Both must agree. Check `authStore.status` and `useMe().data.roles`/`hasChildren`. |
| **A new route 404s / doesn't appear** | the `app/` folder | Filename = route. For a tab/push screen in `(child)`, also register a `<Tabs.Screen>` in [(child)/_layout.tsx](../../apps/student-app/app/(child)/_layout.tsx). |
| **Form validation/submit issue** | the form component (e.g. [(auth)/_components/LoginForm.tsx](../../apps/student-app/app/(auth)/_components/LoginForm.tsx)) | Forms use **react-hook-form + zod**. Schema → resolver → fields. The submit calls an `api-client` mutation hook (`useSignIn`, `useAddChild`…). |
| **API call failing / 401 / wrong data** | [packages/api-client/src/hooks/](../../packages/api-client/src/hooks/) + [.env.local](../../apps/student-app/) | Check `EXPO_PUBLIC_API_BASE_URL` points at `:5080` and backend is up. 401 → token/refresh wiring in [app/_layout.tsx](../../apps/student-app/app/_layout.tsx) `onSignOut`/`onTokensRefreshed`. |
| **Text wrong / missing translation** | [packages/shared/src/i18n/resources.ts](../../packages/shared/src/i18n/resources.ts) | Strings are i18n keys (`t('parent.nav.logout')`). Add **both** `en` and `ar`. |
| **Admin shows shell then bounces** | [useAdminGuard.ts](../../apps/admin-dashboard/lib/hooks/useAdminGuard.ts) | Expected (client-only guard). Real fix needs HttpOnly-cookie auth — out of current scope. |
| **Marketing copy/locale wrong** | [apps/marketing-site/lib/copy.ts](../../apps/marketing-site/lib/copy.ts) | Plain object, not an i18n lib. Edit `COPY.en` / `COPY.ar`. |

**Before opening a PR:** run `pnpm type-check` and `pnpm lint` from the root — those are the cheapest gates and they cover every package. The shared dev log is [docs/dev/HANDOFF.md](HANDOFF.md) — read it before, update it after.

---

## 8. Full diagram — the whole frontend at a glance

### 8a. Annotated repo tree (frontend only)

```
learnexia/
├── package.json ........................ root: pnpm workspaces + turbo scripts (dev/build/lint/type-check)
├── turbo.json .......................... task pipeline (caching, deps between package builds)
├── tsconfig.base.json .................. base TS config all packages extend
│
├── apps/
│   │
│   ├── student-app/  ........... EXPO ROUTER app (web PWA + iOS + Android). Parent+Child+Auth live here.
│   │   ├── app/  ............... ← ROUTES (file-based). Folder name in (parens) = group, no URL segment.
│   │   │   ├── _layout.tsx ..... ROOT provider stack + boot (i18n, fonts, token hydrate)   [§6]
│   │   │   ├── index.tsx ....... SPLASH screen + mounts useAuthRoute guard                  [§4]
│   │   │   │
│   │   │   ├── (auth)/  ........ signed-out surfaces
│   │   │   │   ├── _layout.tsx
│   │   │   │   ├── login.tsx  forgot-password.tsx  register.tsx  reset-password.tsx
│   │   │   │   └── _components/ ... LoginForm, RegisterForm, PersonaToggle, TurnstileWidget,
│   │   │   │                        LocaleThemeControls, SocialIcons, *BrandPanel/FeaturePanel
│   │   │   │
│   │   │   ├── (onboarding)/  .. parent with 0 children
│   │   │   │   ├── _layout.tsx   add-child.tsx   complete.tsx
│   │   │   │   └── _components/ ... AddChildForm, EditChildSheet
│   │   │   │
│   │   │   ├── (parent)/  ...... parent dashboard — WEB SHELL: header + Sidebar + <Slot/>
│   │   │   │   ├── _layout.tsx ... useGroupGuard('(parent)') + mounts AddChildModal
│   │   │   │   ├── index.tsx  overview.tsx  children.tsx  reports.tsx  settings.tsx  link-child.tsx
│   │   │   │   └── _components/ ... Sidebar, OverviewWeb, MyChildrenWeb, ReportsWeb, SettingsWeb,
│   │   │   │                        ChildSwitcher, *Card (DailyActivity/FocusAreas/SubjectMastery/…)
│   │   │   │
│   │   │   └── (child)/  ....... student surfaces — <Tabs/> with floating ChildTabBar
│   │   │       ├── _layout.tsx ... useGroupGuard('(child)') + <Tabs.Screen> registry
│   │   │       ├── index.tsx (home)  missions.tsx  league.tsx  badges.tsx   ← tab roots (on the bar)
│   │   │       ├── xp/streak/hearts/events/attempts.tsx  lessons/[lessonId].tsx  ← push (bar hidden)
│   │   │       └── _components/ ... ChildTabBar, SubjectsListSection, WhyLockedSheet
│   │   │
│   │   ├── src/  ............... NON-route app code
│   │   │   ├── hooks/ .......... useAuthRoute, useGroupGuard (GUARDS) · useLocale · useSignOutAction · useServerError
│   │   │   ├── components/ ..... app-shared: AddChildModal, ScreenHeader, FormScaffold, DotPulse, RestartPrompt
│   │   │   └── providers/ ...... apiBaseUrl · tokenStorage (web/native) · localeStore · themeStore · activeChildStore (Zustand)
│   │   ├── .env.local .......... EXPO_PUBLIC_API_BASE_URL=http://localhost:5080  (gitignored)
│   │   └── package.json ........ scripts: expo start --web (:8081)
│   │
│   ├── admin-dashboard/  ....... NEXT.JS App Router (:3001). Tamagui.
│   │   ├── app/
│   │   │   ├── layout.tsx  providers.tsx  page.tsx
│   │   │   ├── login/page.tsx ........... public sign-in
│   │   │   └── (admin)/layout.tsx ....... AdminShell wrapper (authed area)
│   │   │       └── dashboard/page.tsx
│   │   ├── components/ ......... AdminShell, AdminSideNav, AdminTopBar, AdminErrorBanner, AdminLoadingSkeleton
│   │   ├── lib/ ................ hooks/useAdminGuard (CLIENT GUARD via JWT) · jwt · apiClient · signInSchema
│   │   └── middleware.ts ....... pass-through STUB (no server guard yet)                    [§4]
│   │
│   └── marketing-site/  ........ NEXT.JS App Router (:3002). CSS Modules, NO auth.
│       ├── app/
│       │   ├── layout.tsx ...... sets <html lang dir> from x-locale header
│       │   ├── [locale]/ ....... /en and /ar  (page.tsx + layout.tsx provider-only)
│       │   └── _components/ .... Hero/Features/Subjects/CTA/Footer sections + *.module.css
│       ├── lib/copy.ts ......... COPY.en / COPY.ar  (locale-keyed strings, NOT an i18n lib)
│       └── middleware.ts ....... locale routing: / → /en, inject x-locale                    [§4]
│
└── packages/  .................. SHARED across all apps
    ├── ui/  ................... @learnexia/ui — ~40 design-system components (editing 1 hits every app)
    │   └── src/components/ .... Button TextField Card ChildCard Select Tabs Badge Avatar Hearts XPBar
    │                            StreakFlame KPIStatCard MCQOption QuestionCard LessonCard MasteryBar …
    ├── design-system/  ........ @learnexia/design-system — theme + tokens + RTL + fonts
    │   └── src/
    │       ├── tamagui.config.ts  themes/  media.ts (breakpoints)
    │       ├── tokens/ ........ colors · gradients · shadows · motion
    │       ├── rtl/ .......... LearnexiaProvider, useDirection, applyWebDirection
    │       └── fonts/ ........ Poppins / Cairo / Tajawal (web @font-face + native faces)
    ├── api-client/  ........... @learnexia/api-client — HTTP + data hooks
    │   └── src/
    │       ├── client/ ....... apiClient (auth header + token refresh), typedClient, errors
    │       ├── query/ ........ queryClient, queryKeys, QueryClientProvider
    │       ├── hooks/ ........ use* TanStack Query hooks (useMe, useMyChildren, useDashboard, useSignIn, …)
    │       ├── generated/ .... NSwag client from swagger.json (DO NOT hand-edit; regenerate)
    │       └── schemas.ts
    ├── shared/  .............. @learnexia/shared — state + i18n + types
    │   └── src/
    │       ├── stores/ ...... authStore · activeChild/childContext · flashMessage · restartPrompt (Zustand)
    │       ├── i18n/ ........ config · resources.ts (en+ar strings) · rtl
    │       ├── storage/ ..... tokenStorage abstraction → native (Keychain) / web (sessionStorage)
    │       └── types/ ....... auth · curriculum · learning · constants (ROLES, LOCALES)
    ├── eslint-config/ ........ shared lint rules
    └── tsconfig/ ............. shared TS configs
```

### 8b. Runtime architecture (student app — what wraps what, and where data comes from)

```
                          ┌─────────────────────────── BROWSER / DEVICE ───────────────────────────┐
                          │                                                                          │
   app/_layout.tsx →      │   SafeAreaProvider                                                       │
                          │    └ LearnexiaProvider ............ Tamagui theme · locale · RTL dir     │
                          │       └ QueryClientProvider ....... TanStack Query cache (SERVER data)   │
                          │          └ ApiClientProvider ...... HTTP client (auth + refresh)         │
                          │             └ <Slot/>  ── Expo Router picks the group by auth/role ──┐    │
                          │                                                                      │    │
   guards decide group →  │   useAuthRoute (splash)  /  useGroupGuard (per _layout)              │    │
                          │        reads authStore.status + useMe() → router.replace(...)        │    │
                          │                                                                      ▼    │
                          │   (auth)        (onboarding)        (parent)            (child)            │
                          │   login/reg     add-child           shell+Sidebar       <Tabs>+TabBar      │
                          │                                                                          │
                          │   STATE SPLIT:                                                            │
                          │     • Zustand (@learnexia/shared/stores) → auth tokens + UI state only    │
                          │     • TanStack Query (@learnexia/api-client) → ALL server data            │
                          └───────────────────────────────────┬──────────────────────────────────────┘
                                                               │ use* hooks → apiClient (Bearer token,
                                                               │ 401 → refresh → onSignOut)
                                                               ▼
                                       Backend API  http://localhost:5080  (.NET modular monolith)
```

### 8c. How the three apps relate

```
                    ┌──────────────────────── packages/ (shared) ────────────────────────┐
                    │   ui   ·   design-system   ·   api-client   ·   shared              │
                    └───────▲──────────────▲──────────────────▲───────────────▲───────────┘
                            │ (full)       │ (tokens/ui)       │ (full)        │ (auth/i18n)
            ┌───────────────┴───┐   ┌───────┴────────┐   ┌──────┴───────────────┴──┐
            │  student-app      │   │ admin-dashboard │   │   marketing-site         │
            │  Expo Router :8081│   │ Next.js :3001   │   │   Next.js :3002          │
            │  parent + child   │   │ admins only     │   │   public, /en + /ar      │
            │  + auth + onboard │   │ JWT client guard│   │   no auth                │
            └───────────────────┘   └─────────────────┘   └──────────────────────────┘
```

---

## Quick reference card

```
Student app routes ...... apps/student-app/app/(auth|onboarding|parent|child)/
Student providers/boot ... apps/student-app/app/_layout.tsx  +  app/index.tsx (splash+guard)
Student guards ........... src/hooks/useAuthRoute.ts (splash) + useGroupGuard.ts (per group)
Auth state ............... packages/shared/src/stores/authStore.ts
Data hooks ............... packages/api-client/src/hooks/use*.ts   (TanStack Query)
Shared components ........ packages/ui/src/components/*
Design tokens ............ packages/design-system/src/tokens/*
i18n strings ............. packages/shared/src/i18n/resources.ts
Admin (Next:3001) ........ apps/admin-dashboard/app/ + lib/hooks/useAdminGuard.ts
Marketing (Next:3002) .... apps/marketing-site/app/[locale]/ + lib/copy.ts
Run ...................... pnpm dev | pnpm type-check | pnpm lint
```
