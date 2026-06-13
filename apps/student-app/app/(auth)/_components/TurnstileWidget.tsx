/**
 * TurnstileWidget — Cloudflare Turnstile bot-challenge host (P1-11-FE-16 / CO-FE-3).
 *
 * WEB-ONLY: renders the Turnstile challenge into a DOM host node via the
 * official `api.js` script (loaded lazily, once per page, explicit render
 * mode). On native platforms it renders nothing — the challenge requirement is
 * a web-PWA concern this wave (native coverage is flagged in the task report).
 *
 * Behavior:
 *  - The widget is themed (`light`/`dark`) and localized (`ar`/`en`) from the
 *    live app state; a theme/locale flip re-renders the challenge in place.
 *  - `onToken(token)` fires when the challenge passes; `onToken(null)` fires
 *    when the token expires or the challenge errors, so the caller can gate
 *    its submit button on a *current* token.
 *  - `resetSignal` (a counter) lets the caller force a fresh challenge after a
 *    failed submit — Turnstile tokens are single-use, so every register
 *    attempt needs a new one.
 *  - Script/challenge load failures show a localized inline error (the page
 *    stays usable; submit simply remains gated).
 *
 * Security: the token is handed to the caller only (posted as `captchaToken`
 * on Register-Parent). It is never logged or stored.
 */
import { Stack, Text } from '@tamagui/core';
import { useEffect, useRef, useState } from 'react';
import { Platform } from 'react-native';
import { useTranslation } from 'react-i18next';
import type { Direction } from '@learnexia/shared';

/** Official Turnstile loader (explicit render mode — we call `render` ourselves). */
const TURNSTILE_SCRIPT_URL =
  'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';

/** Default rendered size of the managed Turnstile widget (px) — reserves layout space. */
const WIDGET_MIN_HEIGHT = 65;

/** Minimal surface of the Turnstile JS API that we consume. */
interface TurnstileRenderOptions {
  sitekey: string;
  theme?: 'light' | 'dark' | 'auto';
  language?: string;
  callback?: (token: string) => void;
  'expired-callback'?: () => void;
  'error-callback'?: () => void;
}

interface TurnstileApi {
  render: (container: HTMLElement, options: TurnstileRenderOptions) => string;
  reset: (widgetId: string) => void;
  remove: (widgetId: string) => void;
}

declare global {
  // eslint-disable-next-line @typescript-eslint/consistent-type-definitions
  interface Window {
    turnstile?: TurnstileApi;
  }
}

/** Singleton script-injection promise — the loader script is added at most once. */
let turnstileLoader: Promise<TurnstileApi> | null = null;

function loadTurnstile(): Promise<TurnstileApi> {
  if (turnstileLoader) return turnstileLoader;
  turnstileLoader = new Promise<TurnstileApi>((resolve, reject) => {
    if (window.turnstile) {
      resolve(window.turnstile);
      return;
    }
    const script = document.createElement('script');
    script.src = TURNSTILE_SCRIPT_URL;
    script.async = true;
    script.defer = true;
    script.onload = () => {
      if (window.turnstile) resolve(window.turnstile);
      else reject(new Error('turnstile-unavailable'));
    };
    script.onerror = () => {
      // Allow a later retry (e.g. after connectivity returns) to re-inject.
      turnstileLoader = null;
      reject(new Error('turnstile-load-failed'));
    };
    document.head.appendChild(script);
  });
  return turnstileLoader;
}

export interface TurnstileWidgetProps {
  /** Cloudflare Turnstile site key (public; from EXPO_PUBLIC_TURNSTILE_SITE_KEY). */
  siteKey: string;
  /** Receives the pass token, or null when it expires / the challenge errors. */
  onToken: (token: string | null) => void;
  /** Bump to force a fresh challenge (tokens are single-use). */
  resetSignal?: number;
  /** Widget color theme — follows the app theme. */
  theme: 'light' | 'dark';
  direction: Direction;
  testID?: string;
}

export function TurnstileWidget({
  siteKey,
  onToken,
  resetSignal = 0,
  theme,
  direction,
  testID,
}: TurnstileWidgetProps) {
  const { t, i18n } = useTranslation();
  const [loadError, setLoadError] = useState(false);
  const hostRef = useRef<HTMLElement | null>(null);
  const widgetIdRef = useRef<string | null>(null);

  // Latest-callback ref so the render effect doesn't re-run (and re-issue the
  // challenge) every time the parent re-renders with a new closure.
  const onTokenRef = useRef(onToken);
  onTokenRef.current = onToken;

  // Turnstile accepts ISO 639-1 language codes; mirror the app locale.
  const language = i18n.language?.startsWith('ar') ? 'ar' : 'en';

  // Render (and re-render on theme/locale change) the challenge into the host
  // node. Web only — native renders nothing and the effect is a no-op.
  useEffect(() => {
    if (Platform.OS !== 'web' || !siteKey) return;
    let cancelled = false;
    let localWidgetId: string | null = null;

    loadTurnstile()
      .then((turnstile) => {
        const host = hostRef.current;
        if (cancelled || !host) return;
        localWidgetId = turnstile.render(host, {
          sitekey: siteKey,
          theme,
          language,
          callback: (token) => onTokenRef.current(token),
          'expired-callback': () => onTokenRef.current(null),
          'error-callback': () => {
            onTokenRef.current(null);
            setLoadError(true);
          },
        });
        widgetIdRef.current = localWidgetId;
        setLoadError(false);
      })
      .catch(() => {
        if (!cancelled) setLoadError(true);
      });

    return () => {
      cancelled = true;
      // Any token issued by the disposed widget is no longer valid for submit.
      onTokenRef.current(null);
      if (localWidgetId && window.turnstile) {
        try {
          window.turnstile.remove(localWidgetId);
        } catch {
          // Already removed — nothing to clean up.
        }
      }
      widgetIdRef.current = null;
    };
  }, [siteKey, theme, language]);

  // Force a fresh challenge when the caller bumps resetSignal (post-failure).
  useEffect(() => {
    if (Platform.OS !== 'web' || resetSignal === 0) return;
    onTokenRef.current(null);
    const widgetId = widgetIdRef.current;
    if (widgetId && window.turnstile) {
      try {
        window.turnstile.reset(widgetId);
      } catch {
        setLoadError(true);
      }
    }
  }, [resetSignal]);

  if (Platform.OS !== 'web') return null;

  return (
    <Stack
      gap="$2"
      alignItems={direction === 'rtl' ? 'flex-end' : 'flex-start'}
      testID={testID}
      accessible
      accessibilityLabel={t('auth.register.captcha.a11y')}
      aria-label={t('auth.register.captcha.a11y')}
    >
      {/* DOM host node — Turnstile renders its iframe-based challenge here. */}
      <Stack ref={hostRef as never} minHeight={WIDGET_MIN_HEIGHT} alignSelf="stretch" />
      {loadError ? (
        <Text
          color="$danger"
          fontSize={13}
          fontFamily="$body"
          textAlign={direction === 'rtl' ? 'right' : 'left'}
          writingDirection={direction}
        >
          {t('auth.register.captcha.loadError')}
        </Text>
      ) : null}
    </Stack>
  );
}
