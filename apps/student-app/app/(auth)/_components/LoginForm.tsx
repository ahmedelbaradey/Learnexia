/**
 * LoginForm — shared parent + child sign-in (Design Spec Screen 3).
 *
 * react-hook-form + zod (`signInSchema`). Posts via `useSignIn`; on success
 * persists tokens then routes to `/` (splash), where the routing guard reads
 * `Me` and redirects by role (parent → onboarding/dashboard; student → child
 * home in the child's language). Invalid credentials show a generic banner (no
 * field-level reveal). RTL-aware.
 */
import { useSignIn } from '@learnexia/api-client';
import { signInSchema, useAuthStore, type SignInFormValues } from '@learnexia/shared';
import { Button, TextField } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';

export function LoginForm() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const setTokens = useAuthStore((s) => s.setTokens);
  const signIn = useSignIn();
  const resolveError = useServerError();

  const { control, handleSubmit, formState } = useForm<SignInFormValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { userName: '', password: '' },
    mode: 'onTouched',
  });

  const serverMessage = signIn.isError
    ? resolveError(signIn.error, {
        hints: [{ contains: ['not found', 'no account'], key: 'auth.login.errors.notFound' }],
        byStatus: { 401: 'auth.login.errors.invalidCredentials', 404: 'auth.login.errors.notFound' },
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

  return (
    <Stack gap="$4">
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
    </Stack>
  );
}
