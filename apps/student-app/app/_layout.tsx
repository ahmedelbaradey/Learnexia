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
import { LearnexiaProvider } from '@learnexia/design-system';
import {
  initI18n,
  changeLocale,
  useAuthStore,
  useFlashMessageStore,
  type Locale,
} from '@learnexia/shared';
import { QueryClientProvider } from '@tanstack/react-query';
import { Slot } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useMemo, useRef } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { resolveApiBaseUrl } from '../src/providers/apiBaseUrl';
import { useLocaleStore } from '../src/providers/localeStore';
import { createPlatformTokenStorage } from '../src/providers/tokenStorage';

export default function RootLayout() {
  const locale = useLocaleStore((s) => s.locale);

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

  // i18n init + token-storage wiring + hydrate (once).
  useEffect(() => {
    initI18n({ locale });
    const store = useAuthStore.getState();
    store.setStorage(createPlatformTokenStorage());
    void store.hydrate();
    // Intentionally run once on mount (no deps): one-time i18n + storage wiring.
  }, []);

  // Keep i18n language in sync with the active locale (web flips dir instantly).
  useEffect(() => {
    void changeLocale(locale as Locale);
  }, [locale]);

  return (
    <SafeAreaProvider>
      <LearnexiaProvider locale={locale} theme="dark">
        <QueryClientProvider client={queryClient}>
          <ApiClientProvider client={clientRef.current}>
            <StatusBar style="light" />
            <Slot />
          </ApiClientProvider>
        </QueryClientProvider>
      </LearnexiaProvider>
    </SafeAreaProvider>
  );
}
