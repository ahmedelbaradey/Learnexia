/**
 * LoginForm — shared parent + child sign-in (Design Spec Screen 2, P1-11).
 *
 * react-hook-form + zod (`signInSchema`). Posts via `useSignIn`; on success
 * persists tokens then routes to `/` (splash), where the routing guard reads
 * `Me` and redirects by role (parent → onboarding/dashboard; student → child
 * home in the child's language). Invalid credentials show a generic banner (no
 * field-level reveal). RTL-aware.
 *
 * P1-11 additions (re-skin around the same wiring): a Parent/Student persona
 * toggle (UI-only hint — does NOT enable student self-register), a "Remember me"
 * checkbox, a "Forgot password?" link, an "OR CONTINUE WITH" divider, and
 * Google/Apple/Microsoft social buttons. The persona, remember-me, and social
 * buttons are UI-only placeholders wired to no-op TODO handlers (no faked auth)
 * until the corresponding backend stories land. The auth mutation, error
 * mapping, token persistence and routing are unchanged.
 */
import { useSignIn } from '@learnexia/api-client';
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
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';
import { Checkbox, OrDivider, SocialButton, SocialRow } from './loginParts';
import { PersonaToggle } from './PersonaToggle';
import { AppleIcon, GoogleIcon, MicrosoftIcon } from './SocialIcons';

export function LoginForm() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const setTokens = useAuthStore((s) => s.setTokens);
  const signIn = useSignIn();
  const resolveError = useServerError();

  // UI-only client state (NOT server data → local component state, not Zustand).
  const [persona, setPersona] = useState<LoginPersona>(LOGIN_PERSONAS.Parent);
  const [rememberMe, setRememberMe] = useState(false);

  const { control, handleSubmit, formState } = useForm<SignInFormValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { userName: '', password: '' },
    mode: 'onTouched',
  });

  const serverMessage = signIn.isError
    ? resolveError(signIn.error, {
        hints: [{ contains: ['not found', 'no account'], key: 'auth.login.errors.notFound' }],
        byStatus: {
          400: 'auth.login.errors.invalidCredentials',
          401: 'auth.login.errors.invalidCredentials',
          404: 'auth.login.errors.notFound',
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

  const disabled = signIn.isPending;

  // Social auth is UI-only until the OAuth story lands — no faked auth.
  const handleSocial = (provider: string) => {
    // TODO(P-OAuth): wire `provider` to the OAuth flow once the backend exists.
    void provider;
  };

  const handleForgotPassword = () => {
    // TODO(P-ForgotPassword): route to the forgot-password screen once it exists.
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
          fontSize={14}
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
        <SocialButton
          label={t('auth.login.socialGoogle')}
          icon={<GoogleIcon />}
          onPress={() => handleSocial(SOCIAL_GOOGLE)}
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
const SOCIAL_GOOGLE = 'google';
const SOCIAL_APPLE = 'apple';
const SOCIAL_MICROSOFT = 'microsoft';
