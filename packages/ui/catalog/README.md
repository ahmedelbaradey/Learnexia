# `@learnexia/ui` Component Catalog

A typed, runnable gallery of every P1-08 component across **variants × en/ar
locale × dark/light theme**. This is a React component tree, not a test.

## How to render

In any RN-Web / Expo host:

```tsx
import { Catalog } from '@learnexia/ui/catalog';

export default function CatalogScreen() {
  return <Catalog />;
}
```

- `Catalog` — full gallery with locale (en/ar) + theme (dark/light) switchers.
- `LocaleColumn` — one locale/theme column (use for side-by-side comparison).
- `Smoke` (`@learnexia/ui/catalog/smoke`) — minimal Button + Card + XPBar subset
  with no Skia in the render path; safe for a Node/DOM smoke check.

Each section wraps `LearnexiaProvider` from `@learnexia/design-system`, which
injects the Tamagui config, default theme, and the locale-driven direction +
fonts (Poppins for en, Cairo/Tajawal for ar).

## Passing render criteria

An entry "passes" when: it renders with no TS error; all required props are
supplied; no `undefined`/empty visuals; `ar` renders RTL with Cairo/Tajawal;
`en` renders LTR with Poppins; dark shows `$bg` #0F172A / `$card` #1E293B; light
shows `$bgLight` #F8FAFC.

## Verification status (P1-08)

| Check | Status |
|---|---|
| `pnpm --filter @learnexia/design-system build` | PASS (clean `tsc --build`) |
| `pnpm --filter @learnexia/ui build` | PASS (clean `tsc --build`) |
| `pnpm --filter @learnexia/design-system type-check` | PASS |
| `pnpm --filter @learnexia/ui type-check` | PASS (includes `catalog/`) |
| Catalog type-check (no `any` escapes in prop types) | PASS |
| RN-Web runtime smoke render | Deferred to P1-09 (see below) |

## Deferred to P1-09 (no host shell yet)

1. **Full Expo-native render** — `apps/student-app` has no Metro config / entry
   yet, so a real native render (fonts via `expo-font`, Reanimated/Moti/Skia
   bindings) is verified in P1-09.
2. **Next.js render** — `@tamagui/next-plugin` is not configured in any app; the
   Next.js host render is verified in P1-09.
3. **Runtime RN-Web smoke** — a DOM-mounted render needs a bundler/test harness
   (jest + react-native-web + babel preset) that is part of the app tooling
   landing in P1-09. The package-level guarantee here is a clean TypeScript
   compile of the entire catalog + smoke tree.

## Skia mock for Node-env

The components guard Skia behind `tryLoadSkia()` and degrade gracefully (no
confetti / no native blur / static legendary badge), so they never import the
real native module under Node. If a future jest harness DOES resolve
`@shopify/react-native-skia`, map it to the stub:

```js
// jest.config.js
moduleNameMapper: {
  '^@shopify/react-native-skia$':
    '<rootDir>/__mocks__/@shopify/react-native-skia.ts',
}
```

The same lazy-guard pattern (`tryLoadMoti`, `expo-linear-gradient` loader)
applies to Moti and the gradient module — all degrade to static fallbacks when
absent.
