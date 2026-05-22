import type { NextConfig } from 'next';
import { withTamagui } from '@tamagui/next-plugin';

/**
 * Next.js 15 config for the Learnexia admin dashboard.
 *
 * Tamagui integration:
 *  - `withTamagui` wires the design-system config (`tamagui.config.ts`, which
 *    re-exports `@learnexia/design-system/config`) into the build so token-driven
 *    `styled()` components (`Button`, `Card` from `@learnexia/ui`) compile and
 *    extract CSS.
 *  - `components` lists the packages whose Tamagui components should be
 *    optimized/extracted. Only the web-safe `@learnexia/ui` primitives are used
 *    (we import Button/Card via their subpath exports — never the package root,
 *    which re-exports native-only XPBar/Hearts/etc.).
 *
 * Native peer-dep safety (Blocker 4): the design-system + ui packages declare
 * native peers (react-native, reanimated, skia, moti, expo-linear-gradient).
 * The Button/Card primitives we use only touch `@tamagui/core`, but the module
 * graph can still reference `react-native`. `withTamagui` aliases `react-native`
 * to `react-native-web` is NOT pulled in here because Button/Card use
 * `@tamagui/core` Stack/Text (web-native). We additionally null-alias the
 * native-only optional peers so a stray transitive import cannot break the web
 * build.
 */

const workspacePackages = [
  '@learnexia/ui',
  '@learnexia/design-system',
  '@learnexia/api-client',
  '@learnexia/shared',
  '@tamagui/core',
  'tamagui',
];

const tamaguiPlugin = withTamagui({
  config: './tamagui.config.ts',
  components: ['@learnexia/ui', 'tamagui'],
  appDir: true,
  // Generate CSS at build time; disable in dev for faster HMR.
  disableExtraction: process.env.NODE_ENV === 'development',
});

const nextConfig: NextConfig = {
  reactStrictMode: true,
  transpilePackages: workspacePackages,
  experimental: {
    // Tamagui ships ESM; allow Next to treat workspace ESM externals correctly.
    esmExternals: true,
  },
  webpack: (config) => {
    // Null-alias native-only optional peers so any stray transitive import from
    // the shared packages does not break the web (Next.js) build. The admin app
    // only renders web-safe primitives; these modules are never needed here.
    config.resolve = config.resolve ?? {};
    config.resolve.alias = {
      ...(config.resolve.alias ?? {}),
      'react-native-reanimated': false,
      'react-native-restart': false,
      'moti': false,
      '@shopify/react-native-skia': false,
      'expo-linear-gradient': false,
      'expo-secure-store': false,
    };
    return config;
  },
};

export default tamaguiPlugin(nextConfig as Record<string, unknown>) as NextConfig;
