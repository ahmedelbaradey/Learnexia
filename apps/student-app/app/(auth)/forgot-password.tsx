/**
 * Forgot-password screen — `(auth)/forgot-password`.
 *
 * Accepts an email address and submits to `useForgotPassword`. Per P1-13
 * anti-enumeration rules the UI always shows the SAME confirmation message
 * regardless of whether the email is registered ("If an account exists for that
 * email, we sent a reset link."). Even on a network/server error the same
 * confirmation is shown — the user cannot learn whether the address is in the
 * system.
 *
 * Layout: `SplitFormScaffold` (same visual frame as Login/Register). Back link
 * routes to `/(auth)/login`. EN + AR/RTL throughout; all copy via i18n.
 */
import { useForgotPassword } from '@learnexia/api-client';
import { Button, TextField } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { FormScaffold } from '../../src/components/FormScaffold';
import { ScreenHeader } from '../../src/components/ScreenHeader';
import { useLocale } from '../../src/hooks/useLocale';
import { LoginBrandPanel } from './_components/LoginBrandPanel';

const forgotPasswordSchema = z.object({
  email: z.string().min(1, 'auth.register.errors.invalidEmail').email('auth.register.errors.invalidEmail'),
});
type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>;

export default function ForgotPasswordScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const forgotPassword = useForgotPassword();
  const align = direction === 'rtl' ? 'right' : 'left';

  // Anti-enumeration: once submitted (success OR error) show the confirmation.
  const [submitted, setSubmitted] = useState(false);

  const { control, handleSubmit, formState } = useForm<ForgotPasswordValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: '' },
    mode: 'onTouched',
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await forgotPassword.mutateAsync({ email: values.email.trim() });
    } catch {
      // Intentionally swallowed — the same confirmation is shown regardless.
    } finally {
      setSubmitted(true);
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
            {t('auth.forgotPassword.title')}
          </Text>
          <Text
            color="$fg3"
            fontSize={14}
            fontFamily="$body"
            textAlign={align}
            writingDirection={direction}
          >
            {t('auth.forgotPassword.subtitle')}
          </Text>
        </Stack>

        {submitted ? (
          /* Anti-enumeration confirmation — always shown after submit. */
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
              {t('auth.forgotPassword.confirmation')}
            </Text>
            <Stack
              marginTop="$3"
              minHeight={48}
              justifyContent="center"
            >
              <Text
                color="$primaryLight"
                fontSize={14}
                fontWeight="600"
                fontFamily="$body"
                cursor="pointer"
                onPress={() => router.replace('/(auth)/login')}
                accessibilityRole="link"
                accessibilityLabel={t('auth.forgotPassword.backToLogin')}
                aria-label={t('auth.forgotPassword.backToLogin')}
                textAlign={align}
                writingDirection={direction}
              >
                {t('auth.forgotPassword.backToLogin')}
              </Text>
            </Stack>
          </Stack>
        ) : (
          /* Email form */
          <Stack gap="$4">
            <Controller
              control={control}
              name="email"
              render={({ field, fieldState }) => (
                <TextField
                  label={t('auth.forgotPassword.labelEmail')}
                  value={field.value}
                  onChangeText={field.onChange}
                  keyboardType="email-address"
                  autoCapitalize="none"
                  autoComplete="email"
                  forceLtr
                  error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                  direction={direction}
                  disabled={forgotPassword.isPending}
                />
              )}
            />

            <Button
              variant="primary"
              size="full"
              accessibilityLabel={t('auth.forgotPassword.submitButton')}
              loading={forgotPassword.isPending}
              disabled={forgotPassword.isPending || formState.isSubmitting}
              onPress={onSubmit}
            >
              {t('auth.forgotPassword.submitButton')}
            </Button>

            <Stack alignItems="center">
              <Text
                color="$primaryLight"
                fontSize={13}
                fontWeight="600"
                fontFamily="$body"
                cursor="pointer"
                onPress={() => router.replace('/(auth)/login')}
                accessibilityRole="link"
                accessibilityLabel={t('auth.forgotPassword.backToLogin')}
                aria-label={t('auth.forgotPassword.backToLogin')}
                writingDirection={direction}
              >
                {t('auth.forgotPassword.backToLogin')}
              </Text>
            </Stack>
          </Stack>
        )}
      </Stack>
    </FormScaffold>
  );
}
