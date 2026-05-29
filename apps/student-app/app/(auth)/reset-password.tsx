/**
 * Reset-password screen — `(auth)/reset-password`.
 *
 * Reads `email` and `token` from URL search params (Expo Router
 * `useLocalSearchParams`). Shows new-password + confirm-password fields and a
 * `PasswordStrengthMeter`. Submits via `useResetPassword({ email, token,
 * newPassword })`. On success: shows a success banner + link to sign in. On
 * error: localized error inline. Missing or malformed params show an
 * "invalid link" error state.
 *
 * Layout: `SplitFormScaffold` (same visual frame as Login/Register). EN + AR/RTL.
 */
import { useResetPassword } from '@learnexia/api-client';
import {
  Button,
  PasswordStrengthMeter,
  PASSWORD_STRENGTH,
  TextField,
  type PasswordStrength,
} from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { FormScaffold } from '../../src/components/FormScaffold';
import { ScreenHeader } from '../../src/components/ScreenHeader';
import { ServerErrorBanner } from '../../src/components/ServerErrorBanner';
import { useLocale } from '../../src/hooks/useLocale';
import { useServerError } from '../../src/hooks/useServerError';
import { LoginBrandPanel } from './_components/LoginBrandPanel';

const resetSchema = z
  .object({
    newPassword: z.string().min(6, 'auth.resetPassword.passwordTooShort'),
    confirmPassword: z.string().min(1, 'auth.resetPassword.confirmRequired'),
  })
  .refine((d) => d.newPassword === d.confirmPassword, {
    message: 'auth.resetPassword.passwordMismatch',
    path: ['confirmPassword'],
  });

type ResetFormValues = z.infer<typeof resetSchema>;

const STRENGTH_LABEL_KEY: Record<Exclude<PasswordStrength, 0>, string> = {
  1: 'auth.register.strength.weak',
  2: 'auth.register.strength.fair',
  3: 'auth.register.strength.good',
  4: 'auth.register.strength.strong',
};

function scorePassword(password: string): PasswordStrength {
  if (!password) return PASSWORD_STRENGTH.Empty;
  let score = 1;
  if (password.length >= 6) score = 2;
  if (/[a-z]/.test(password) && /[A-Z]/.test(password) && /\d/.test(password)) score = 3;
  if (score >= 3 && /[^a-zA-Z\d]/.test(password)) score = 4;
  return score as PasswordStrength;
}

export default function ResetPasswordScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const resolveError = useServerError();
  const params = useLocalSearchParams<{ email?: string; token?: string }>();
  const resetPassword = useResetPassword();
  const align = direction === 'rtl' ? 'right' : 'left';

  const [succeeded, setSucceeded] = useState(false);

  const email = typeof params.email === 'string' ? params.email : '';
  const token = typeof params.token === 'string' ? params.token : '';
  const isValidLink = Boolean(email && token);

  const { control, handleSubmit, formState } = useForm<ResetFormValues>({
    resolver: zodResolver(resetSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
    mode: 'onTouched',
  });

  const serverMessage = resetPassword.isError
    ? resolveError(resetPassword.error, {
        byStatus: {
          400: 'auth.resetPassword.invalidLink',
          404: 'auth.resetPassword.invalidLink',
          422: 'auth.resetPassword.invalidLink',
        },
      })
    : null;

  const onSubmit = handleSubmit(async (values) => {
    if (!isValidLink) return;
    try {
      await resetPassword.mutateAsync({
        email,
        token,
        newPassword: values.newPassword,
      });
      setSucceeded(true);
    } catch {
      // Error surfaced inline via serverMessage.
    }
  });

  return (
    <FormScaffold
      variant="split"
      brandPanel={<LoginBrandPanel direction={direction} appName={t('common.appName')} />}
      header={
        <ScreenHeader
          onBack={() => router.replace('/(auth)/login')}
          backLabel={t('auth.forgotPassword.backToLogin')}
        />
      }
    >
      <Stack gap="$6">
        {/* Header */}
        <Stack gap="$2" alignItems="center" $tablet={{ alignItems: 'flex-start' }}>
          <Text
            color="$fg1"
            fontSize={32}
            fontWeight="800"
            fontFamily="$heading"
            letterSpacing={-0.64}
            lineHeight={37}
            textAlign={align}
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('auth.resetPassword.title')}
          </Text>
          <Text
            color="$fg3"
            fontSize={14}
            fontFamily="$body"
            textAlign={align}
            writingDirection={direction}
          >
            {t('auth.resetPassword.subtitle')}
          </Text>
        </Stack>

        {!isValidLink ? (
          /* Missing / malformed URL params — show invalid link error. */
          <Stack
            backgroundColor="$dangerSoft"
            borderRadius="$card"
            borderWidth={1}
            borderColor="rgba(239,68,68,0.3)"
            padding="$5"
            gap="$3"
          >
            <Text
              color="$danger"
              fontSize={14}
              fontFamily="$body"
              textAlign={align}
              writingDirection={direction}
            >
              {t('auth.resetPassword.invalidLink')}
            </Text>
            <Text
              color="$primaryLight"
              fontSize={13}
              fontWeight="600"
              fontFamily="$body"
              cursor="pointer"
              onPress={() => router.replace('/(auth)/forgot-password')}
              accessibilityRole="link"
              writingDirection={direction}
            >
              {t('auth.forgotPassword.submitButton')}
            </Text>
          </Stack>
        ) : succeeded ? (
          /* Success state */
          <Stack
            backgroundColor="$successSoft"
            borderRadius="$card"
            borderWidth={1}
            borderColor="rgba(34,197,94,0.3)"
            padding="$5"
            gap="$2"
            accessibilityLiveRegion="polite"
          >
            <Text
              color="$success"
              fontSize={16}
              fontWeight="700"
              fontFamily="$heading"
              textAlign={align}
              writingDirection={direction}
            >
              {t('auth.resetPassword.successTitle')}
            </Text>
            <Text
              color="$fg2"
              fontSize={14}
              fontFamily="$body"
              textAlign={align}
              writingDirection={direction}
            >
              {t('auth.resetPassword.successBody')}
            </Text>
            <Stack marginTop="$3" minHeight={48} justifyContent="center">
              <Button
                variant="primary"
                size="md"
                accessibilityLabel={t('auth.signIn')}
                onPress={() => router.replace('/(auth)/login')}
              >
                {t('auth.signIn')}
              </Button>
            </Stack>
          </Stack>
        ) : (
          /* Password form */
          <Stack gap="$4">
            <Controller
              control={control}
              name="newPassword"
              render={({ field, fieldState }) => {
                const score = scorePassword(field.value ?? '');
                const strengthLabel = score > 0 ? t(STRENGTH_LABEL_KEY[score as Exclude<PasswordStrength, 0>]) : '';
                return (
                  <Stack gap="$2">
                    <TextField
                      label={t('auth.resetPassword.labelNew')}
                      value={field.value}
                      onChangeText={field.onChange}
                      secureTextEntry
                      error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                      direction={direction}
                      disabled={resetPassword.isPending}
                    />
                    {field.value ? (
                      <PasswordStrengthMeter
                        score={score}
                        label={strengthLabel}
                        direction={direction}
                        accessibilityLabel={t('auth.register.strength.a11y', { label: strengthLabel })}
                      />
                    ) : null}
                  </Stack>
                );
              }}
            />

            <Controller
              control={control}
              name="confirmPassword"
              render={({ field, fieldState }) => (
                <TextField
                  label={t('auth.resetPassword.labelConfirm')}
                  value={field.value}
                  onChangeText={field.onChange}
                  secureTextEntry
                  error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                  direction={direction}
                  disabled={resetPassword.isPending}
                />
              )}
            />

            <ServerErrorBanner message={serverMessage} direction={direction} />

            <Button
              variant="primary"
              size="full"
              accessibilityLabel={t('auth.resetPassword.submitButton')}
              loading={resetPassword.isPending}
              disabled={resetPassword.isPending || formState.isSubmitting}
              onPress={onSubmit}
            >
              {t('auth.resetPassword.submitButton')}
            </Button>
          </Stack>
        )}
      </Stack>
    </FormScaffold>
  );
}
