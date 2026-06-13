/**
 * LoginForm — shared parent + child sign-in (Design Spec Screen 2, P1-11 +
 * P1-12-FE Batch 3: Google OAuth flow).
 *
 * react-hook-form + zod (`signInSchema`). Posts via `useSignIn`; on success
 * persists tokens then routes to `/` (splash), where the routing guard reads
 * `Me` and redirects by role (parent → onboarding/dashboard; student → child
 * home in the child's language). Invalid credentials show a generic banner (no
 * field-level reveal). RTL-aware.
 *
 * P1-11 additions: persona toggle, remember-me, forgot-password link, OR divider,
 * Google/Apple/Microsoft social buttons.
 *
 * P1-12 Batch 3 Google OAuth:
 *  - expo-auth-session `useAuthRequest` + `useAutoDiscovery` against Google.
 *  - Response type = id_token (implicit IdToken grant over OIDC).
 *  - Client ID from `process.env.EXPO_PUBLIC_GOOGLE_CLIENT_ID`. If missing/empty,
 *    the Google button is disabled and a console.warn is printed — NO crash.
 *  - On success: idToken → `useGoogleSignIn` → authStore.setTokens →
 *    router.replace('/') — SAME path as email sign-in `onSubmit`.
 *  - User cancel (type === 'dismiss' | 'cancel'): silent, no error shown.
 *  - Network/auth failure: ServerErrorBanner with `auth.login.errors.socialFailed`.
 *  - While Google flow is in flight: Google button shows loading state; email
 *    submit + Apple + Microsoft buttons are disabled (one-action-at-a-time).
 *  - Apple + Microsoft: remain dimmed `disabled` visual placeholders (no-op).
 *
 * Environment / setup:
 *  - Web: `EXPO_PUBLIC_GOOGLE_CLIENT_ID` must be set in `.env.local`.
 *  - The `learnexia` URI scheme is registered in `app.config.ts` and is used as
 *    the native redirect. On web, `makeRedirectUri()` uses `window.location.origin`.
 *  - `expo-web-browser` `maybeCompleteAuthSession()` is called at module load to
 *    close the popup after the OAuth redirect lands back on the page (web-only).
 *  - PKCE is enabled by default in expo-auth-session (code_challenge_method=S256).
 *    Do NOT disable it.
 *  - The client ID is NEVER hardcoded. The flow ships wired; the lead provisions
 *    the real client ID before live testing.
 *
 * Security:
 *  - `idToken` travels only to `useGoogleSignIn` (backend). It is never logged,
 *    echoed in the UI, stored in Zustand, or reflected in error messages.
 *  - Token persistence goes through `authStore.setTokens` (same path as
 *    email sign-in — no separate token storage path).
 */
import { isApiError, useGoogleSignIn, useSignIn } from '@learnexia/api-client';
import {
  LOGIN_PERSONAS,
  signInSchema,
  useAuthStore,
  type LoginPersona,
  type SignInFormValues,
} from '@learnexia/shared';
import { Button, TextField } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import {
  makeRedirectUri,
  ResponseType,
  useAuthRequest,
  useAutoDiscovery,
} from 'expo-auth-session';
import Constants from 'expo-constants';
import { useRouter } from 'expo-router';
import * as WebBrowser from 'expo-web-browser';
import { useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';
import { Checkbox, OrDivider, SocialButton, SocialRow } from './loginParts';
import { PersonaToggle } from './PersonaToggle';
import { AppleIcon, GoogleIcon, MicrosoftIcon } from './SocialIcons';

/**
 * Close the OAuth popup after the redirect lands back on the web page.
 * On native this is a no-op. Must be called at module level (outside any
 * component) per the expo-auth-session docs.
 */
WebBrowser.maybeCompleteAuthSession();

/** Google OIDC discovery endpoint. */
const GOOGLE_DISCOVERY_URL = 'https://accounts.google.com';

/**
 * Backend-message hint substrings (P1-11-FE-15 / CO-FE-2) — matched
 * case-insensitively against the `BaseResponse.message` text. The backend
 * localizes its messages (default culture ar-EG; English when Accept-Language
 * is en), so each hint set carries BOTH the en-US and ar-EG resource substrings
 * (`SharedResources.{en-US,ar-EG}.resx`: `LoginTooManyFailedAttempts`,
 * `LoginAccountDeactivated`). These are technical matchers, not user-facing copy.
 *
 * Anti-enumeration: there is deliberately NO "user not found" hint — the
 * backend returns one uniform invalid-credentials message for both unknown
 * users and wrong passwords, and the FE must never branch on the cause.
 */
const ACCOUNT_LOCKED_HINTS = [
  'too many failed',
  'temporarily locked',
  'قفل الحساب مؤقتاً',
  'محاولات تسجيل دخول فاشلة',
];
const ACCOUNT_DEACTIVATED_HINTS = ['account is inactive', 'غير نشط'];

/**
 * Map a Google sign-in backend rejection to an i18n key. The Google handler
 * returns the same distinct "account deactivated" message as email sign-in
 * (`GoogleSignInCommandHandler` → `LoginAccountDeactivated`); everything else
 * stays the generic social failure. Pure module-scope helper so the OAuth
 * response effect's dependency list is unchanged.
 */
function googleErrorKey(err: unknown): string {
  if (isApiError(err)) {
    const text = [err.message ?? '', ...err.errors.filter((e): e is string => typeof e === 'string')]
      .join(' ')
      .toLowerCase();
    if (ACCOUNT_DEACTIVATED_HINTS.some((h) => text.includes(h.toLowerCase()))) {
      return 'auth.login.errors.accountDeactivated';
    }
  }
  return 'auth.login.errors.socialFailed';
}

export function LoginForm() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const setTokens = useAuthStore((s) => s.setTokens);
  const signIn = useSignIn();
  const googleSignIn = useGoogleSignIn();
  const resolveError = useServerError();

  // UI-only client state (NOT server data → local component state, not Zustand).
  const [persona, setPersona] = useState<LoginPersona>(LOGIN_PERSONAS.Parent);
  const [rememberMe, setRememberMe] = useState(false);
  // Google-specific error surfaced via the shared ServerErrorBanner.
  const [googleError, setGoogleError] = useState<string | null>(null);

  // Read client ID from env at runtime — never hardcoded.
  // Web: EXPO_PUBLIC_GOOGLE_CLIENT_ID from .env.local.
  // Native: Constants.expoConfig?.extra?.googleClientId from app.config.ts extra block.
  // If missing/empty the Google button is disabled with a console.warn (no crash).
  const googleClientId =
    (Constants.expoConfig?.extra as Record<string, string> | undefined)?.googleClientId ??
    process.env.EXPO_PUBLIC_GOOGLE_CLIENT_ID ??
    '';
  const isGoogleConfigured = Boolean(googleClientId);
  // Warn once on mount if the client ID is missing so the developer knows why
  // the button is disabled. Not an error — the button gracefully disables
  // instead of crashing. Production is silent.
  useEffect(() => {
    // Dev-only diagnostic — `__DEV__` blocks are stripped from production bundles, so this never
    // surfaces config details in a release build's console.
    if (__DEV__ && !isGoogleConfigured) {
      // eslint-disable-next-line no-console
      console.warn('[LoginForm] EXPO_PUBLIC_GOOGLE_CLIENT_ID is not set. Google sign-in is disabled.');
    }
    // isGoogleConfigured is stable — derived purely from an env var constant.
  }, [isGoogleConfigured]);

  // ── expo-auth-session OAuth setup ────────────────────────────────────────
  // Discovery doc is fetched once at component mount. On web, makeRedirectUri()
  // returns `window.location.origin` (e.g. http://localhost:8081). On native it
  // uses the `learnexia://` scheme registered in app.config.ts.
  const discovery = useAutoDiscovery(GOOGLE_DISCOVERY_URL);
  const redirectUri = makeRedirectUri({ scheme: 'learnexia' });

  const [request, response, promptAsync] = useAuthRequest(
    {
      clientId: googleClientId,
      responseType: ResponseType.IdToken,
      scopes: ['openid', 'email', 'profile'],
      redirectUri,
      // PKCE is on by default (code_challenge_method=S256). Do NOT disable it.
    },
    // When clientId is missing, discovery is ignored — request will be null.
    isGoogleConfigured ? discovery : null,
  );

  // Track whether the Google sign-in mutation (backend call) is in flight.
  const isGoogleMutating = googleSignIn.isPending;
  // Track whether the OAuth prompt is in flight (request loading or promptAsync
  // called but not yet resolved). Combined flag drives the in-flight UI state.
  const [isPromptPending, setIsPromptPending] = useState(false);
  const isGoogleInFlight = isGoogleMutating || isPromptPending;

  // ── Handle OAuth response (fires once response changes) ────────────────
  useEffect(() => {
    if (!response) return;

    const handle = async () => {
      setIsPromptPending(false);

      if (response.type === 'cancel' || response.type === 'dismiss') {
        // User dismissed the Google dialog — silent, no error shown.
        return;
      }

      if (response.type === 'error') {
        // OAuth protocol error (not user-cancel).
        setGoogleError(t('auth.login.errors.socialFailed'));
        return;
      }

      if (response.type === 'success') {
        const idToken = response.params?.id_token;
        if (!idToken) {
          setGoogleError(t('auth.login.errors.socialFailed'));
          return;
        }
        try {
          // idToken goes ONLY to the backend (never logged/stored elsewhere).
          const res = await googleSignIn.mutateAsync({ idToken });
          if (res.accessToken && res.refreshToken?.tokenString) {
            // Mirror the EXACT same token-persistence + routing path as email sign-in.
            await setTokens({ accessToken: res.accessToken, refreshToken: res.refreshToken.tokenString });
            router.replace('/');
          } else {
            setGoogleError(t('auth.login.errors.socialFailed'));
          }
        } catch (err) {
          // Error surfaced inline via googleError state. A deactivated/suspended
          // account gets its distinct message (same contract as email sign-in);
          // any other backend rejection stays the generic social failure.
          setGoogleError(t(googleErrorKey(err)));
        }
      }
    };

    void handle();
  }, [response, googleSignIn, setTokens, router, t]);

  const { control, handleSubmit, formState } = useForm<SignInFormValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { userName: '', password: '' },
    mode: 'onTouched',
  });

  // Email sign-in error message (shared banner — google error takes precedence when present).
  // CO-FE-2 / P1-11-FE-15: distinct localized branches for the backend's
  // lockout (`LoginTooManyFailedAttempts`) and deactivated/suspended
  // (`LoginAccountDeactivated`) responses; every other credential failure —
  // including unknown user — collapses to the SAME uniform invalid-credentials
  // message (anti-enumeration: never branch user-not-found vs wrong-password).
  const emailServerMessage = signIn.isError
    ? resolveError(signIn.error, {
        hints: [
          { contains: ACCOUNT_LOCKED_HINTS, key: 'auth.login.errors.accountLocked' },
          { contains: ACCOUNT_DEACTIVATED_HINTS, key: 'auth.login.errors.accountDeactivated' },
        ],
        byStatus: {
          400: 'auth.login.errors.invalidCredentials',
          401: 'auth.login.errors.invalidCredentials',
          404: 'auth.login.errors.invalidCredentials',
        },
      })
    : null;

  const serverMessage = googleError ?? emailServerMessage;

  const onSubmit = handleSubmit(async (values) => {
    // Clear any previous Google error before a new email attempt.
    setGoogleError(null);
    try {
      const res = await signIn.mutateAsync({ userName: values.userName.trim(), password: values.password });
      if (res.accessToken && res.refreshToken?.tokenString) {
        await setTokens({ accessToken: res.accessToken, refreshToken: res.refreshToken.tokenString });
        // Hand off to the routing guard (reads Me, routes by role + locale).
        router.replace('/');
      }
    } catch {
      // Failure is surfaced inline via signIn.error → serverMessage; swallow the
      // rejection so it doesn't bubble as an uncaught promise error.
    }
  });

  // While Google flow is in flight: disable email form + Apple + Microsoft buttons.
  const emailFormDisabled = signIn.isPending || isGoogleInFlight;
  const emailButtonDisabled = emailFormDisabled || formState.isSubmitting;

  const handleForgotPassword = () => {
    router.push('/(auth)/forgot-password');
  };

  const handleGoogleSignIn = async () => {
    if (!isGoogleConfigured || !request || isGoogleInFlight) return;
    setGoogleError(null);
    setIsPromptPending(true);
    try {
      await promptAsync();
    } catch {
      setIsPromptPending(false);
      setGoogleError(t('auth.login.errors.socialFailed'));
    }
  };

  return (
    <Stack gap="$4">
      <PersonaToggle
        value={persona}
        onChange={setPersona}
        labelFor={(p) =>
          p === LOGIN_PERSONAS.Parent ? t('auth.login.personaParent') : t('auth.login.personaStudent')
        }
        accessibilityLabel={t('auth.login.personaToggleLabel')}
        direction={direction}
        disabled={emailFormDisabled}
        testID="login-persona-toggle"
      />

      <Controller
        control={control}
        name="userName"
        render={({ field, fieldState }) => (
          <TextField
            label={t('auth.login.labelUsername')}
            value={field.value}
            onChangeText={field.onChange}
            keyboardType="email-address"
            autoCapitalize="none"
            autoComplete="username"
            forceLtr
            error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
            direction={direction}
            disabled={emailFormDisabled}
            testID="login-username"
          />
        )}
      />
      <Controller
        control={control}
        name="password"
        render={({ field, fieldState }) => (
          <TextField
            label={t('auth.login.labelPassword')}
            value={field.value}
            onChangeText={field.onChange}
            secureTextEntry
            autoComplete="password"
            error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
            direction={direction}
            disabled={emailFormDisabled}
            testID="login-password"
          />
        )}
      />

      {/* Remember me + Forgot password row */}
      <Stack
        flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'}
        alignItems="center"
        justifyContent="space-between"
        flexWrap="wrap"
        gap="$2"
      >
        <Checkbox
          checked={rememberMe}
          onChange={setRememberMe}
          label={t('auth.login.rememberMe')}
          direction={direction}
          disabled={emailFormDisabled}
        />
        <Text
          color="$primaryLight"
          fontSize={12}
          fontWeight="600"
          fontFamily="$body"
          cursor="pointer"
          onPress={handleForgotPassword}
          accessibilityRole="link"
          accessibilityLabel={t('auth.login.forgotPassword')}
          aria-label={t('auth.login.forgotPassword')}
          writingDirection={direction}
          testID="login-forgot-password"
        >
          {t('auth.login.forgotPassword')}
        </Text>
      </Stack>

      {/* Shared error banner — covers both email and Google errors. */}
      <ServerErrorBanner message={serverMessage} direction={direction} testID="login-error" />

      <Button
        variant="primary"
        size="full"
        accessibilityLabel={t('auth.login.submitButton')}
        loading={signIn.isPending}
        disabled={emailButtonDisabled}
        onPress={onSubmit}
        testID="login-submit"
      >
        {t('auth.login.submitButton')}
      </Button>

      {/* OR divider — "Or" on phones, "Or continue with" on tablet+. */}
      <Stack $tablet={{ display: 'none' }}>
        <OrDivider label={t('auth.login.orDivider')} direction={direction} />
      </Stack>
      <Stack display="none" $tablet={{ display: 'flex' }}>
        <OrDivider label={t('auth.login.orContinueWith')} direction={direction} />
      </Stack>

      {/* Social buttons — Google + Apple on all sizes; Microsoft on tablet+.
          Google: fully wired (P1-12-FE-3). Apple + Microsoft: dimmed placeholders.
          While Google is in flight: Apple + MS are disabled; email form disabled too. */}
      <SocialRow direction={direction}>
        <SocialButton
          label={t('auth.login.socialGoogle')}
          icon={<GoogleIcon />}
          onPress={handleGoogleSignIn}
          direction={direction}
          loading={isGoogleInFlight}
          disabled={!isGoogleConfigured || !request}
          labelLtr
          testID="login-social-google"
        />
        <SocialButton
          label={t('auth.login.socialApple')}
          icon={<AppleIcon />}
          onPress={() => {
            /* TODO(P-OAuth): Apple sign-in — placeholder, no-op. */
          }}
          direction={direction}
          disabled
          testID="login-social-apple"
        />
        <Stack display="none" flex={1} $tablet={{ display: 'flex' }}>
          <SocialButton
            label={t('auth.login.socialMicrosoft')}
            icon={<MicrosoftIcon />}
            onPress={() => {
              /* TODO(P-OAuth): Microsoft sign-in — placeholder, no-op. */
            }}
            direction={direction}
            disabled
            testID="login-social-microsoft"
          />
        </Stack>
      </SocialRow>
    </Stack>
  );
}
