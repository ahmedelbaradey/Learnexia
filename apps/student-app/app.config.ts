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
};

export default config;
