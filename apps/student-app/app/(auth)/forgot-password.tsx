/**
 * ForgotPassword screen — `(auth)/forgot-password` (P1-12-FE-4).
 *
 * Mirrors `login.tsx` exactly: same `FormScaffold variant="split"`,
 * `LoginBrandPanel`, `LocaleThemeControls` top row, eyebrow→title→subtitle
 * header, and phone-only logo mark. Single email field (RHF + zod) posts via
 * `useForgotPassword`.
 *
 * ANTI-ENUMERATION: on any 2xx the generic success panel is shown regardless
 * of whether the email exists. No retry/resend. Only non-enumeration failures
 * (5xx / network) surface an error banner.
 *
 * RTL: direction from `useLocale`; email field `forceLtr` (Latin technical);
 * eyebrow `textTransform none` in RTL (mirrors Login). All copy via i18n.
 */
import { useForgotPassword } from '@learnexia/api-client';
import { type Direction } from '@learnexia/shared';
import { Button, TextField } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { FormScaffold } from '../../src/components/FormScaffold';
import { ServerErrorBanner } from '../../src/components/ServerErrorBanner';
import { useLocale } from '../../src/hooks/useLocale';
import { useServerError } from '../../src/hooks/useServerError';
import { LoginBrandPanel } from './_components/LoginBrandPanel';
import { LocaleThemeControls } from './_components/LocaleThemeControls';

/* ------------------------------------------------------------------ */
/* Schema                                                               */
/* ------------------------------------------------------------------ */

const forgotPasswordSchema = z.object({
  email: z.string().trim().email('auth.forgotPassword.errors.invalidEmail'),
});

type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;

/* ------------------------------------------------------------------ */
/* Screen                                                               */
/* ------------------------------------------------------------------ */

export default function ForgotPasswordScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();

  return (
    <FormScaffold
      variant="split"
      brandPanel={<LoginBrandPanel direction={direction} appName={t('common.appName')} />}
    >
      <Stack gap="$6">
        {/* Top controls — language + theme switch, aligned to the end. */}
        <Stack
          flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'}
          justifyContent={direction === 'rtl' ? 'flex-start' : 'flex-end'}
        >
          <LocaleThemeControls direction={direction} />
        </Stack>

        {/* App-icon logo mark — phone only (web shows it in the brand panel). */}
        <Stack alignItems="center" $tablet={{ display: 'none' }}>
          <Stack
            width={72}
            height={72}
            borderRadius="$card"
            backgroundColor="$primary"
            alignItems="center"
            justifyContent="center"
          >
            <Text fontSize={36} accessibilityElementsHidden>
              ✦
            </Text>
          </Stack>
        </Stack>

        {/* Header — eyebrow + heading + subtitle */}
        <Stack gap="$2" alignItems="center" $tablet={{ alignItems: 'flex-start' }}>
          <Text
            color="$primaryLight"
            fontSize={12}
            fontWeight="800"
            fontFamily="$heading"
            textTransform={direction === 'rtl' ? 'none' : 'uppercase'}
            letterSpacing={1.44}
            writingDirection={direction}
          >
            {t('auth.forgotPassword.eyebrow')}
          </Text>
          <Text
            color="$fg1"
            fontSize={32}
            fontWeight="800"
            fontFamily="$heading"
            letterSpacing={-0.64}
            lineHeight={37}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('auth.forgotPassword.title')}
          </Text>
          <Text
            color="$fg3"
            fontSize={14}
            fontFamily="$body"
            textAlign={direction === 'rtl' ? 'right' : 'left'}
            writingDirection={direction}
          >
            {t('auth.forgotPassword.subtitle')}
          </Text>
        </Stack>

        <ForgotPasswordForm direction={direction} />

        {/* Back to Sign in */}
        <Stack flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'} justifyContent="center">
          <Text
            color="$primaryLight"
            fontSize={14}
            fontWeight="600"
            fontFamily="$body"
            cursor="pointer"
            onPress={() => router.replace('/(auth)/login')}
            accessibilityRole="link"
            accessibilityLabel={t('auth.forgotPassword.backToSignIn')}
            aria-label={t('auth.forgotPassword.backToSignIn')}
            writingDirection={direction}
          >
            {t('auth.forgotPassword.backToSignIn')}
          </Text>
        </Stack>
      </Stack>
    </FormScaffold>
  );
}

/* ------------------------------------------------------------------ */
/* Form component                                                        */
/* ------------------------------------------------------------------ */

function ForgotPasswordForm({ direction }: { direction: Direction }) {
  const { t } = useTranslation();
  const forgotPassword = useForgotPassword();
  const resolveError = useServerError();

  const { control, handleSubmit, formState } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: '' },
    mode: 'onTouched',
  });

  // ANTI-ENUMERATION: success state is shown on any 2xx regardless of whether
  // the account exists. The copy is generic and identical for known/unknown email.
  const isSuccess = forgotPassword.isSuccess;

  // Only non-enumeration failures (network / 5xx) surface an error banner.
  const serverMessage = forgotPassword.isError
    ? resolveError(forgotPassword.error, {
        byStatus: { 500: 'auth.forgotPassword.errors.generic' },
        hints: [{ contains: ['network', 'offline'], key: 'auth.forgotPassword.errors.generic' }],
      })
    : null;

  const disabled = forgotPassword.isPending;

  const onSubmit = handleSubmit(async (values) => {
    try {
      await forgotPassword.mutateAsync({ email: values.email.trim() });
    } catch {
      // Error surfaced inline via forgotPassword.error → serverMessage.
    }
  });

  if (isSuccess) {
    // Anti-enumeration success state: replace form with a generic confirmation.
    return (
      <Stack
        backgroundColor="$successSoft"
        borderRadius="$card"
        padding="$5"
        gap="$3"
        accessibilityLiveRegion="polite"
        accessible
        accessibilityLabel={`${t('auth.forgotPassword.successTitle')} ${t('auth.forgotPassword.successBody')}`}
      >
        <Text fontSize={28} textAlign="center" accessibilityElementsHidden>
          ✉️
        </Text>
        <Text
          color="$success"
          fontSize={16}
          fontWeight="800"
          fontFamily="$heading"
          textAlign="center"
          writingDirection={direction}
        >
          {t('auth.forgotPassword.successTitle')}
        </Text>
        <Text
          color="$fg2"
          fontSize={14}
          fontFamily="$body"
          textAlign="center"
          lineHeight={20}
          writingDirection={direction}
        >
          {t('auth.forgotPassword.successBody')}
        </Text>
      </Stack>
    );
  }

  return (
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
            disabled={disabled}
          />
        )}
      />

      <ServerErrorBanner message={serverMessage} direction={direction} />

      <Button
        variant="primary"
        size="full"
        accessibilityLabel={t('auth.forgotPassword.submitButton')}
        loading={forgotPassword.isPending}
        disabled={disabled || formState.isSubmitting}
        onPress={onSubmit}
      >
        {t('auth.forgotPassword.submitButton')}
      </Button>
    </Stack>
  );
}
