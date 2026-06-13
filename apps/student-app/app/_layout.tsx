/**
 * Root layout — the provider stack + boot wiring.
 *
 * Outer→inner: SafeAreaProvider → LearnexiaProvider (Tamagui + locale + RTL +
 * theme) → QueryClientProvider → ApiClientProvider → app slot.
 *
 * Boot sequence (P1-02-FE-1, merged here):
 *   1. init i18n once.
 *   2. inject the platform token storage into authStore.
 *   3. hydrate persisted tokens (flips status away from `unknown`).
 *   4. the api client is wired with onSignOut → flash "session expired" +
 *      authStore.signOut(), and onTokensRefreshed → authStore.setTokens.
 *
 * The routing guard (`useAuthRoute`) lives in `app/index.tsx` (the splash) so it
 * runs inside the provider tree.
 */
import { createApiClient, ApiClientProvider, createQueryClient } from '@learnexia/api-client';
import { LearnexiaProvider, loadWebFonts, nativeFontMap, applyNativeRtl } from '@learnexia/design-system';
import {
  initI18n,
  changeLocale,
  useAuthStore,
  useFlashMessageStore,
  type Locale,
} from '@learnexia/shared';
import { QueryClientProvider } from '@tanstack/react-query';
import { useFonts } from 'expo-font';
import { Slot } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useMemo, useRef } from 'react';
import { I18nManager, Platform } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { resolveApiBaseUrl } from '@/providers/apiBaseUrl';
import { useLocaleStore } from '@/providers/localeStore';
import { useThemeStore } from '@/providers/themeStore';
import { createPlatformTokenStorage } from '@/providers/tokenStorage';

// Initialize i18n synchronously at module load (inline resources → `ready` is
// true immediately). Must happen before the first render: react-i18next's
// useTranslation calls a different number of hooks while it's not ready, so an
// unready→ready transition mid-mount changes SplashScreen's hook order and
// crashes ("Should have a queue"). The active locale is applied via changeLocale.
initI18n();

// WEB: inject the brand `@font-face` rules at module load so the typeface is
// available as early as possible (Poppins / Cairo / Tajawal, per weight). No-op
// on native (no DOM). Native loads the same faces via `useFonts` below.
loadWebFonts();

export default function RootLayout() {
  // Load the brand `.ttf` faces on NATIVE via expo-font. On web this resolves
  // immediately (the faces are wired through the injected `@font-face` CSS), so
  // it never blocks the web render. Declared first so the hook order is stable.
  const [fontsLoaded] = useFonts(nativeFontMap);

  const locale = useLocaleStore((s) => s.locale);
  const theme = useThemeStore((s) => s.theme);

  // One stable QueryClient + ApiClient for the app lifetime.
  const queryClient = useMemo(() => createQueryClient(), []);
  const clientRef = useRef(
    createApiClient({
      baseUrl: resolveApiBaseUrl(),
      tokenStorage: createPlatformTokenStorage(),
      onSignOut: () => {
        useFlashMessageStore.getState().setMessage('auth.sessionExpired');
        void useAuthStore.getState().signOut();
      },
      onTokensRefreshed: (tokens) => {
        void useAuthStore.getState().setTokens(tokens);
      },
    }),
  );

  // Token-storage wiring + hydrate (once). i18n is initialized at module load.
  useEffect(() => {
    const store = useAuthStore.getState();
    store.setStorage(createPlatformTokenStorage());
    void store.hydrate();
    // Intentionally run once on mount (no deps): one-time storage wiring.
  }, []);

  // Keep i18n language in sync with the active locale (web flips dir instantly).
  useEffect(() => {
    void changeLocale(locale as Locale);
  }, [locale]);

  // NATIVE RTL — apply I18nManager.forceRTL from the app root so native layout
  // direction matches the active locale. forceRTL only takes full effect after an
  // app reload; `applyNativeRtl` returns true when the direction changed and a
  // reload is needed. We do NOT auto-restart here to avoid restart loops — the
  // language-switch action in settings (LanguagePanel) owns the restart prompt
  // (via restartPromptStore). On web, direction is handled by `LearnexiaProvider`
  // calling `applyWebDirection` — no double-flip risk because web direction is
  // a DOM attribute, not a native module.
  useEffect(() => {
    if (Platform.OS === 'web') return;
    // Structural-type injection — avoids importing RN native modules in the
    // design-system package. I18nManager is already imported from react-native.
    applyNativeRtl(locale as Locale, I18nManager);
    // NOTE: we intentionally omit the `restart` argument here; the app's
    // LanguagePanel already triggers the restart prompt via restartPromptStore
    // when the locale actually changes. Calling restart() here would cause a
    // double-restart on the first locale change.
  }, [locale]);

  // NATIVE: hold the first render until the brand faces are loaded so text never
  // flashes in a fallback face. WEB renders immediately — `@font-face`/`swap`
  // handles progressive loading, and `useFonts` is effectively instant there.
  if (Platform.OS !== 'web' && !fontsLoaded) {
    return null;
  }

  return (
    <SafeAreaProvider>
      <LearnexiaProvider locale={locale} theme={theme}>
        <QueryClientProvider client={queryClient}>
          <ApiClientProvider client={clientRef.current}>
            <StatusBar style={theme === 'dark' ? 'light' : 'dark'} />
            <Slot />
          </ApiClientProvider>
        </QueryClientProvider>
      </LearnexiaProvider>
    </SafeAreaProvider>
  );
}
