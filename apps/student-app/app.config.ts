import type { ExpoConfig } from 'expo/config';

/**
 * Expo config for the Learnexia universal student app (web PWA + iOS + Android).
 * New Architecture enabled; Expo Router as the entry. Arabic-first product, but
 * the device/user locale + RTL wiring is handled at runtime by the app shell
 * (LearnexiaProvider + i18n), not here.
 */
const config: ExpoConfig = {
  name: 'Learnexia',
  slug: 'learnexia-student',
  scheme: 'learnexia',
  version: '0.0.0',
  orientation: 'portrait',
  userInterfaceStyle: 'dark',
  newArchEnabled: true,
  platforms: ['ios', 'android', 'web'],
  assetBundlePatterns: ['**/*'],
  ios: {
    supportsTablet: true,
    bundleIdentifier: 'app.learnexia.student',
  },
  android: {
    package: 'app.learnexia.student',
  },
  web: {
    bundler: 'metro',
    output: 'static',
  },
  plugins: ['expo-router', 'expo-secure-store'],
  experiments: {
    typedRoutes: true,
  },
  /**
   * Extra config values injected as `Constants.expoConfig.extra` at runtime.
   * `googleClientId` is read from the environment so it is NEVER hardcoded —
   * the build must set EXPO_PUBLIC_GOOGLE_CLIENT_ID for the env-var path (web),
   * or EAS build env for the native standalone build (via this extra block).
   *
   * The value here intentionally falls back to '' so the app starts gracefully
   * even without a client ID configured. Google sign-in will be disabled (button
   * greyed out) until the lead provisions the real client ID.
   */
  extra: {
    googleClientId: process.env.EXPO_PUBLIC_GOOGLE_CLIENT_ID ?? '',
    eas: {
      projectId: '3b8ed4d1-0e2b-4df2-9da4-ece1431c69ce',
    },
  },
};

export default config;
