/**
 * RegisterForm — parent registration form (Design Spec Screen 2).
 *
 * react-hook-form + zod (`registerParentSchema`). Posts via `useRegisterParent`;
 * on success persists tokens (`authStore.setTokens`) and routes to onboarding.
 * Maps `BaseResponse.errors` to localized banner copy. Client-only
 * `confirmPassword` is dropped before posting. RTL-aware via `useLocale`.
 */
import { useRegisterParent } from '@learnexia/api-client';
import { registerParentSchema, useAuthStore, type RegisterParentFormValues } from '@learnexia/shared';
import { Button, TextField } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';

export function RegisterForm() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const setTokens = useAuthStore((s) => s.setTokens);
  const register = useRegisterParent();
  const resolveError = useServerError();

  const { control, handleSubmit, formState } = useForm<RegisterParentFormValues>({
    resolver: zodResolver(registerParentSchema),
    defaultValues: { fullName: '', email: '', password: '', confirmPassword: '' },
    mode: 'onTouched',
  });

  const serverMessage = register.isError
    ? resolveError(register.error, {
        hints: [
          { contains: ['exists', 'duplicate', 'taken'], key: 'auth.register.errors.duplicateEmail' },
          { contains: ['password', 'weak'], key: 'auth.register.errors.weakPassword' },
        ],
        byStatus: { 409: 'auth.register.errors.duplicateEmail', 422: 'auth.register.errors.weakPassword' },
      })
    : null;

  const onSubmit = handleSubmit(async (values) => {
    try {
      const res = await register.mutateAsync({
        email: values.email.trim(),
        password: values.password,
        fullName: values.fullName?.trim() || undefined,
      });
      if (res.accessToken && res.refreshToken?.tokenString) {
        await setTokens({ accessToken: res.accessToken, refreshToken: res.refreshToken.tokenString });
        router.replace('/(onboarding)/add-child');
      }
    } catch {
      // Failure is surfaced inline via register.error → serverMessage; swallow
      // the rejection so it doesn't bubble as an uncaught promise error.
    }
  });

  const disabled = register.isPending;

  return (
    <Stack gap="$4" accessibilityRole={undefined}>
      <Controller
        control={control}
        name="fullName"
        render={({ field }) => (
          <TextField
            label={t('auth.register.labelFullName')}
            value={field.value ?? ''}
            onChangeText={field.onChange}
            autoCapitalize="words"
            direction={direction}
            disabled={disabled}
          />
        )}
      />
      <Controller
        control={control}
        name="email"
        render={({ field, fieldState }) => (
          <TextField
            label={t('auth.register.labelEmail')}
            value={field.value}
            onChangeText={field.onChange}
            keyboardType="email-address"
            autoCapitalize="none"
            autoComplete="email"
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
            label={t('auth.register.labelPassword')}
            value={field.value}
            onChangeText={field.onChange}
            secureTextEntry
            error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
            direction={direction}
            disabled={disabled}
          />
        )}
      />
      <Controller
        control={control}
        name="confirmPassword"
        render={({ field, fieldState }) => (
          <TextField
            label={t('auth.register.labelConfirmPassword')}
            value={field.value}
            onChangeText={field.onChange}
            secureTextEntry
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
        accessibilityLabel={t('auth.register.submitButton')}
        loading={register.isPending}
        disabled={disabled || formState.isSubmitting}
        onPress={onSubmit}
      >
        {t('auth.register.submitButton')}
      </Button>

      <Stack flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'} justifyContent="center" gap="$1" marginTop="$2">
        <Text color="$fg3" fontSize={14} fontFamily="$body">
          {t('auth.register.haveAccount')}
        </Text>
        <Text
          color="$primaryLight"
          fontSize={14}
          fontWeight="600"
          fontFamily="$body"
          cursor="pointer"
          onPress={() => router.replace('/(auth)/login')}
          accessibilityRole="link"
          accessibilityLabel={t('auth.register.signInLink')}
          aria-label={t('auth.register.signInLink')}
        >
          {t('auth.register.signInLink')}
        </Text>
      </Stack>
    </Stack>
  );
}
