/**
 * ResetPassword screen — `(auth)/reset-password` (P1-12-FE-4).
 *
 * Reached from the password-reset email deep-link carrying `?email=&token=`
 * query params. Reads `email` + `token` via `useLocalSearchParams()`.
 *
 * Fields: email (read-only, identifies the account, forceLtr), newPassword,
 * confirmPassword. Reuses `PasswordStrengthMeter` + `scorePassword` pattern
 * from `RegisterForm`. Posts via `useResetPassword({ email, token, newPassword })`.
 *
 * Security notes:
 *  - The `token` param travels ONLY in the mutation body. It never appears in
 *    any i18n string, aria-label, log, or component key.
 *  - Token-invalid/expired errors (400/410/422) are mapped to dedicated i18n copy;
 *    the raw backend message is NEVER echoed.
 *  - If `token` is absent from the URL, the form is hidden and the token-invalid
 *    block is shown immediately.
 *  - On success: show a brief success panel then route to login.
 *
 * RTL: direction from `useLocale`; all technical fields forceLtr.
 * Mirrors `login.tsx` scaffold (split panel on tablet+).
 */
import { useResetPassword } from '@learnexia/api-client';
import { PASSWORD_REGEX, type Direction } from '@learnexia/shared';
import { Button, PasswordStrengthMeter, PASSWORD_STRENGTH, TextField, type PasswordStrength } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect } from 'react';
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
/* Password-strength helpers (mirrored from RegisterForm)              */
/* ------------------------------------------------------------------ */

function scorePassword(password: string): PasswordStrength {
  if (!password) return PASSWORD_STRENGTH.Empty;
  let score = 1;
  if (password.length >= 6) score = 2;
  if (/[a-z]/.test(password) && /[A-Z]/.test(password) && /\d/.test(password)) score = 3;
  if (score >= 3 && /[^a-zA-Z\d]/.test(password)) score = 4;
  return score as PasswordStrength;
}

const STRENGTH_LABEL_KEY: Record<Exclude<PasswordStrength, 0>, string> = {
  1: 'auth.register.strength.weak',
  2: 'auth.register.strength.fair',
  3: 'auth.register.strength.good',
  4: 'auth.register.strength.strong',
};

/* ------------------------------------------------------------------ */
/* Schema                                                               */
/* ------------------------------------------------------------------ */

const resetPasswordSchema = z
  .object({
    newPassword: z
      .string()
      .regex(PASSWORD_REGEX, 'auth.resetPassword.errors.weakPassword'),
    confirmPassword: z.string().min(1, 'auth.resetPassword.errors.mismatch'),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: 'auth.resetPassword.errors.mismatch',
    path: ['confirmPassword'],
  });

type ResetPasswordFormValues = z.infer<typeof resetPasswordSchema>;

/* ------------------------------------------------------------------ */
/* Screen                                                               */
/* ------------------------------------------------------------------ */

export default function ResetPasswordScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();

  // Read email + token from the deep-link query params.
  // The token MUST NOT be echoed into any label, aria-label, or log.
  const params = useLocalSearchParams<{ email?: string; token?: string }>();
  const email = params.email ?? '';
  const token = params.token ?? '';

  return (
    <FormScaffold
      variant="split"
      brandPanel={<LoginBrandPanel direction={direction} appName={t('common.appName')} />}
    >
      <Stack gap="$6">
        {/* Top controls */}
        <Stack
          flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'}
          justifyContent={direction === 'rtl' ? 'flex-start' : 'flex-end'}
        >
          <LocaleThemeControls direction={direction} />
        </Stack>

        {/* App-icon logo mark — phone only */}
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

        {/* Header */}
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
            {t('auth.resetPassword.eyebrow')}
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
            {t('auth.resetPassword.title')}
          </Text>
          {/* Subtitle: email rendered forceLtr inline so bidi doesn't reorder it. */}
          <Text
            color="$fg3"
            fontSize={14}
            fontFamily="$body"
            textAlign={direction === 'rtl' ? 'right' : 'left'}
            writingDirection={direction}
          >
            {t('auth.resetPassword.subtitle')}
            {email ? (
              <Text fontFamily="$body" fontSize={14} writingDirection="ltr">
                {` ${email}`}
              </Text>
            ) : null}
          </Text>
        </Stack>

        <ResetPasswordForm direction={direction} email={email} token={token} />

        {/* Back to Sign in */}
        <BackToSignIn direction={direction} />
      </Stack>
    </FormScaffold>
  );
}

/* ------------------------------------------------------------------ */
/* Back to Sign in link (shared by form + token-error state)           */
/* ------------------------------------------------------------------ */

function BackToSignIn({ direction }: { direction: Direction }) {
  const { t } = useTranslation();
  const router = useRouter();
  return (
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
  );
}

/* ------------------------------------------------------------------ */
/* Form component                                                        */
/* ------------------------------------------------------------------ */

interface ResetPasswordFormProps {
  direction: Direction;
  email: string;
  /** The reset token from the deep-link. NEVER logged, echoed, or surfaced. */
  token: string;
}

function ResetPasswordForm({ direction, email, token }: ResetPasswordFormProps) {
  const { t } = useTranslation();
  const router = useRouter();
  const resetPassword = useResetPassword();
  const resolveError = useServerError();

  const { control, handleSubmit, formState } = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
    mode: 'onTouched',
  });

  // If token is absent or empty, show the invalid-token block immediately.
  const isTokenMissing = !token;

  // On success, show a success panel and then navigate to login.
  const isSuccess = resetPassword.isSuccess;

  // Route to login shortly after success.
  useEffect(() => {
    if (!isSuccess) return;
    const timer = setTimeout(() => {
      router.replace('/(auth)/login');
    }, 1800);
    return () => clearTimeout(timer);
  }, [isSuccess, router]);

  // Classify token errors by HTTP STATUS ONLY (no dependency on the raw backend message — it is
  // never read or echoed). The backend returns a single combined "invalid or has expired" 400 for
  // bad/expired reset links, so we show one combined message rather than guessing expired-vs-invalid.
  const isTokenError =
    resetPassword.isError &&
    (() => {
      const err = resetPassword.error as { status?: number } | null;
      const status = err?.status ?? 0;
      return status === 400 || status === 410 || status === 422;
    })();

  const tokenErrorKey = isTokenError ? 'auth.resetPassword.errors.tokenInvalid' : null;

  const serverMessage =
    resetPassword.isError && !isTokenError
      ? resolveError(resetPassword.error, {
          byStatus: { 500: 'auth.resetPassword.errors.generic' },
          hints: [
            { contains: ['network', 'offline'], key: 'auth.resetPassword.errors.generic' },
          ],
        })
      : null;

  const disabled = resetPassword.isPending;

  const onSubmit = handleSubmit(async (values) => {
    try {
      // Token travels only in the mutation body. Never logged or surfaced.
      await resetPassword.mutateAsync({
        email: email.trim(),
        token,
        newPassword: values.newPassword,
      });
    } catch {
      // Error surfaced inline via resetPassword.error → tokenErrorKey / serverMessage.
    }
  });

  // Show token-invalid block when token is absent or the server rejects it.
  if (isTokenMissing || (resetPassword.isError && isTokenError)) {
    return (
      <TokenErrorBlock
        direction={direction}
        messageKey={tokenErrorKey ?? 'auth.resetPassword.errors.tokenInvalid'}
      />
    );
  }

  if (isSuccess) {
    return (
      <Stack
        testID="reset-password-success"
        backgroundColor="$successSoft"
        borderRadius="$card"
        padding="$5"
        gap="$3"
        accessibilityLiveRegion="polite"
        accessible
        accessibilityLabel={t('auth.resetPassword.successBody')}
      >
        <Text fontSize={28} textAlign="center" accessibilityElementsHidden>
          ✓
        </Text>
        <Text
          color="$success"
          fontSize={16}
          fontWeight="800"
          fontFamily="$heading"
          textAlign="center"
          writingDirection={direction}
        >
          {t('auth.resetPassword.successBody')}
        </Text>
      </Stack>
    );
  }

  return (
    <Stack gap="$4">
      {/* Email — prefilled from param, read-only, display-only, forceLtr */}
      <TextField
        label={t('auth.resetPassword.labelEmail')}
        value={email}
        onChangeText={() => undefined}
        keyboardType="email-address"
        autoCapitalize="none"
        forceLtr
        disabled
        direction={direction}
        testID="reset-password-email"
      />

      {/* New password + strength meter */}
      <Controller
        control={control}
        name="newPassword"
        render={({ field, fieldState }) => {
          const score = scorePassword(field.value ?? '');
          const strengthLabel =
            score > 0 ? t(STRENGTH_LABEL_KEY[score as Exclude<PasswordStrength, 0>]) : '';
          return (
            <Stack gap="$2">
              <TextField
                label={t('auth.resetPassword.labelNewPassword')}
                value={field.value}
                onChangeText={field.onChange}
                secureTextEntry
                showLabel={t('auth.login.showPassword')}
                hideLabel={t('auth.login.hidePassword')}
                error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                direction={direction}
                disabled={disabled}
                testID="reset-password-new"
              />
              {field.value ? (
                <PasswordStrengthMeter
                  score={score}
                  label={strengthLabel}
                  direction={direction}
                  accessibilityLabel={t('auth.register.strength.a11y', { label: strengthLabel })}
                />
              ) : !fieldState.error ? (
                <Text
                  color="$fg3"
                  fontSize={12}
                  fontFamily="$body"
                  textAlign={direction === 'rtl' ? 'right' : 'left'}
                  writingDirection={direction}
                >
                  {t('auth.resetPassword.passwordHelper')}
                </Text>
              ) : null}
            </Stack>
          );
        }}
      />

      {/* Confirm password */}
      <Controller
        control={control}
        name="confirmPassword"
        render={({ field, fieldState }) => (
          <TextField
            label={t('auth.resetPassword.labelConfirmPassword')}
            value={field.value}
            onChangeText={field.onChange}
            secureTextEntry
            showLabel={t('auth.login.showPassword')}
            hideLabel={t('auth.login.hidePassword')}
            error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
            direction={direction}
            disabled={disabled}
            testID="reset-password-confirm"
          />
        )}
      />

      <ServerErrorBanner message={serverMessage} direction={direction} testID="reset-password-error" />

      <Button
        variant="primary"
        size="full"
        accessibilityLabel={t('auth.resetPassword.submitButton')}
        loading={resetPassword.isPending}
        disabled={disabled || formState.isSubmitting}
        onPress={onSubmit}
        testID="reset-password-submit"
      >
        {t('auth.resetPassword.submitButton')}
      </Button>
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* Token-error block                                                    */
/* ------------------------------------------------------------------ */

/**
 * Token-invalid / token-expired dedicated error block.
 * The token is never echoed in this block — only the mapped i18n key.
 */
function TokenErrorBlock({
  direction,
  messageKey,
}: {
  direction: Direction;
  messageKey: string;
}) {
  const { t } = useTranslation();
  const router = useRouter();

  return (
    <Stack
      testID="reset-password-token-error"
      backgroundColor="$warningSoft"
      borderRadius="$card"
      padding="$5"
      gap="$3"
      accessibilityLiveRegion="assertive"
      accessible
      accessibilityLabel={t(messageKey)}
    >
      <Text fontSize={28} textAlign="center" accessibilityElementsHidden>
        ⚠️
      </Text>
      <Text
        color="$danger"
        fontSize={14}
        fontFamily="$body"
        textAlign="center"
        lineHeight={20}
        writingDirection={direction}
        accessibilityRole="text"
      >
        {t(messageKey)}
      </Text>
      <Stack flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'} justifyContent="center">
        <Text
          color="$primaryLight"
          fontSize={14}
          fontWeight="600"
          fontFamily="$body"
          cursor="pointer"
          onPress={() => router.replace('/(auth)/forgot-password')}
          accessibilityRole="link"
          accessibilityLabel={t('auth.resetPassword.requestNewLink')}
          aria-label={t('auth.resetPassword.requestNewLink')}
          writingDirection={direction}
        >
          {t('auth.resetPassword.requestNewLink')}
        </Text>
      </Stack>
    </Stack>
  );
}
