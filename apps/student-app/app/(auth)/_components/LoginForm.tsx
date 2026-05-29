/**
 * LoginForm — shared parent + child sign-in (Design Spec Screen 2, P1-11).
 *
 * react-hook-form + zod (`signInSchema`). Posts via `useSignIn`; on success
 * persists tokens then routes to `/` (splash), where the routing guard reads
 * `Me` and redirects by role (parent → onboarding/dashboard; student → child
 * home in the child's language). Invalid credentials show a generic banner (no
 * field-level reveal — P1-13 anti-enumeration: sign-in errors are now uniform,
 * all 400/401 → invalidCredentials, no branching on not-found vs wrong-password).
 * RTL-aware.
 *
 * P1-11 additions (re-skin around the same wiring): a Parent/Student persona
 * toggle (UI-only hint — does NOT enable student self-register), a "Remember me"
 * checkbox, a "Forgot password?" link (→ /forgot-password), an "OR CONTINUE WITH"
 * divider, and Google/Apple/Microsoft social buttons. Google is wired to
 * `useGoogleSignIn` when EXPO_PUBLIC_GOOGLE_CLIENT_ID is set (web: Google
 * Identity Services); Apple/Microsoft remain UI-only stubs.
 */
import { useSignIn, useGoogleSignIn } from '@learnexia/api-client';
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
import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Platform } from 'react-native';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';
import { Checkbox, OrDivider, SocialButton, SocialRow } from './loginParts';
import { PersonaToggle } from './PersonaToggle';
import { AppleIcon, GoogleIcon, MicrosoftIcon } from './SocialIcons';

/** EXPO_PUBLIC_ env vars are inlined at build time (string or undefined). */
const GOOGLE_CLIENT_ID =
  typeof process !== 'undefined'
    ? (process.env['EXPO_PUBLIC_GOOGLE_CLIENT_ID'] ?? '')
    : '';

/** True only when the Google client ID env var is set. */
const GOOGLE_ENABLED = Boolean(GOOGLE_CLIENT_ID);

/**
 * Load the Google Identity Services script (web only, once). The script
 * auto-initializes from the meta client_id or programmatic init call. We call
 * `google.accounts.id.initialize` after it loads. Called at module level so
 * subsequent renders skip the script injection.
 */
let gisScriptLoaded = false;
function ensureGisScript(clientId: string, callback: (idToken: string) => void): void {
  if (Platform.OS !== 'web' || !clientId) return;
  if (gisScriptLoaded) {
    initializeGis(clientId, callback);
    return;
  }
  const existingScript = document.getElementById('google-gis-script');
  if (existingScript) {
    // Script tag already injected by a previous render cycle.
    gisScriptLoaded = true;
    initializeGis(clientId, callback);
    return;
  }
  const script = document.createElement('script');
  script.id = 'google-gis-script';
  script.src = 'https://accounts.google.com/gsi/client';
  script.async = true;
  script.defer = true;
  script.onload = () => {
    gisScriptLoaded = true;
    initializeGis(clientId, callback);
  };
  document.head.appendChild(script);
}

function initializeGis(clientId: string, callback: (idToken: string) => void): void {
  const g = (window as unknown as { google?: { accounts?: { id?: { initialize?: (o: object) => void; prompt?: () => void } } } }).google;
  if (g?.accounts?.id?.initialize) {
    g.accounts.id.initialize({
      client_id: clientId,
      callback: (response: { credential?: string }) => {
        if (response.credential) {
          callback(response.credential);
        }
      },
      auto_select: false,
    });
  }
}

function promptGoogleSignIn(): void {
  const g = (window as unknown as { google?: { accounts?: { id?: { prompt?: () => void } } } }).google;
  g?.accounts?.id?.prompt?.();
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

  const { control, handleSubmit, formState } = useForm<SignInFormValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { userName: '', password: '' },
    mode: 'onTouched',
  });

  // P1-13 anti-enumeration: ALL sign-in errors collapse to one message.
  // No branching on not-found vs wrong-password — server now returns uniform responses.
  const serverMessage = signIn.isError
    ? resolveError(signIn.error, {
        byStatus: {
          400: 'auth.login.errors.invalidCredentials',
          401: 'auth.login.errors.invalidCredentials',
          403: 'auth.login.errors.lockout',
          404: 'auth.login.errors.invalidCredentials',
          423: 'auth.login.errors.lockout',
        },
      })
    : null;

  const googleErrorMessage = googleSignIn.isError
    ? resolveError(googleSignIn.error, {
        byStatus: {
          400: 'auth.login.errors.invalidCredentials',
          401: 'auth.login.errors.invalidCredentials',
        },
      })
    : null;

  const onSubmit = handleSubmit(async (values) => {
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

  // Handle the Google ID token once GIS fires the callback.
  const handleGoogleIdToken = async (idToken: string) => {
    try {
      const res = await googleSignIn.mutateAsync({ idToken });
      if (res.accessToken && res.refreshToken?.tokenString) {
        await setTokens({ accessToken: res.accessToken, refreshToken: res.refreshToken.tokenString });
        router.replace('/');
      }
    } catch {
      // Error surfaced inline via googleErrorMessage.
    }
  };

  // Initialize GIS script on web when GOOGLE_ENABLED. handleGoogleIdToken is
  // intentionally not in the dep array — GIS callbacks are registered once and
  // the identity of the callback is stable across renders (refs would add
  // unnecessary complexity; GIS itself does not re-initialize).
  useEffect(() => {
    if (GOOGLE_ENABLED) {
      ensureGisScript(GOOGLE_CLIENT_ID, handleGoogleIdToken);
    }
  }, []);

  const handleGooglePress = () => {
    if (!GOOGLE_ENABLED) return;
    if (Platform.OS === 'web') {
      promptGoogleSignIn();
    }
    // Native: no expo-image-picker dep — button stays disabled below.
  };

  const handleForgotPassword = () => {
    router.push('/(auth)/forgot-password');
  };

  // Apple/Microsoft remain UI-only stubs.
  const handleSocial = (provider: string) => {
    // TODO(P-OAuth): wire Apple/Microsoft OAuth when BE stories land.
    void provider;
  };

  const disabled = signIn.isPending || googleSignIn.isPending;

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
        disabled={disabled}
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
            disabled={disabled}
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
            disabled={disabled}
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
          disabled={disabled}
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
        >
          {t('auth.login.forgotPassword')}
        </Text>
      </Stack>

      <ServerErrorBanner message={serverMessage} direction={direction} />
      <ServerErrorBanner message={googleErrorMessage} direction={direction} />

      <Button
        variant="primary"
        size="full"
        accessibilityLabel={t('auth.login.submitButton')}
        loading={signIn.isPending}
        disabled={disabled || formState.isSubmitting}
        onPress={onSubmit}
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

      {/* Social buttons — Google + Apple on all sizes; Microsoft on tablet+. */}
      <SocialRow direction={direction}>
        {/* Google: wired when EXPO_PUBLIC_GOOGLE_CLIENT_ID is set; disabled with
            a "Coming soon" note otherwise. Native always disabled (no GIS on RN). */}
        <SocialButton
          label={
            GOOGLE_ENABLED && Platform.OS === 'web'
              ? t('auth.login.continueWithGoogle')
              : t('auth.login.googleComingSoon')
          }
          icon={<GoogleIcon />}
          onPress={handleGooglePress}
          disabled={!GOOGLE_ENABLED || Platform.OS !== 'web' || googleSignIn.isPending}
          direction={direction}
        />
        <SocialButton
          label={t('auth.login.socialApple')}
          icon={<AppleIcon />}
          onPress={() => handleSocial(SOCIAL_APPLE)}
          direction={direction}
        />
        <Stack display="none" flex={1} $tablet={{ display: 'flex' }}>
          <SocialButton
            label={t('auth.login.socialMicrosoft')}
            icon={<MicrosoftIcon />}
            onPress={() => handleSocial(SOCIAL_MICROSOFT)}
            direction={direction}
          />
        </Stack>
      </SocialRow>
    </Stack>
  );
}

// Non-user-facing technical identifiers for the no-op social handlers.
const SOCIAL_APPLE = 'apple';
const SOCIAL_MICROSOFT = 'microsoft';
