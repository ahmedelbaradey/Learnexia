/**
 * RegisterForm — parent registration form (P1-11 capture, P1-12-FE-7 wired).
 *
 * react-hook-form + zod (`registerParentSchema`). Field set: full name + country
 * (two-column row at tablet+), email, password (with "At least 6 characters"
 * helper), and a required Terms consent checkbox that gates submit. Posts via
 * `useRegisterParent`; on success persists tokens (`authStore.setTokens`) and
 * routes to onboarding. Maps `BaseResponse.errors` to localized banner copy.
 *
 * P1-12-FE-7: `country` and `acceptedTerms` are posted.
 *
 * P1-11-FE-16 / CO-FE-3 (CAPTCHA): when the Turnstile requirement is
 * advertised (`EXPO_PUBLIC_TURNSTILE_SITE_KEY` set — the FE half of the
 * backend's `Captcha:Enabled` config gate, same env-gated pattern as the
 * Google client ID), the web form renders the Turnstile challenge and posts
 * the resulting `captchaToken`; submit stays disabled until the challenge
 * passes, and a failed register resets the challenge (tokens are single-use).
 * When the site key is absent the widget is not rendered and no token is sent
 * (pre-CAPTCHA behavior preserved). The backend's `CaptchaVerificationFailed`
 * rejection maps to a localized banner message.
 *
 * RTL-aware via `useLocale`.
 */
import { useRegisterParent } from '@learnexia/api-client';
import {
  COUNTRIES,
  registerParentSchema,
  useAuthStore,
  type Locale,
  type RegisterParentFormValues,
} from '@learnexia/shared';
import {
  Button,
  CheckboxField,
  PasswordStrengthMeter,
  PASSWORD_STRENGTH,
  Select,
  TextField,
  type PasswordStrength,
  type SelectOption,
} from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { useMemo, useState } from 'react';
import { Platform } from 'react-native';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';
import { useThemeStore } from '../../../src/providers/themeStore';
import { TurnstileWidget } from './TurnstileWidget';

/**
 * Backend-message hint substrings for the CAPTCHA rejection
 * (`SharedResourcesKey.CaptchaVerificationFailed`) — both the en-US and ar-EG
 * resource values contain the Latin token "CAPTCHA", so one case-insensitive
 * hint covers both locales. Technical matcher, not user-facing copy.
 */
const CAPTCHA_FAILED_HINTS = ['captcha'];

/**
 * Cumulative password-strength score (0..4) for the PasswordStrengthMeter
 * (align-register B-1). Length ≥6 unlocks segment 2; lower/upper/digit mix
 * unlocks 3; a special char unlocks 4. Empty password scores 0.
 */
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

export interface RegisterFormProps {
  /**
   * Called after the parent account has been successfully created and tokens
   * persisted. The wizard host (register.tsx) advances to step 2 instead of
   * navigating to a separate route.
   */
  onSuccess: () => void;
}

export function RegisterForm({ onSuccess }: RegisterFormProps) {
  const { t, i18n } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const setTokens = useAuthStore((s) => s.setTokens);
  const register = useRegisterParent();
  const resolveError = useServerError();

  const locale = (i18n.language?.startsWith('ar') ? 'ar' : 'en') as Locale;
  const countryOptions: SelectOption[] = useMemo(
    () => COUNTRIES.map((c) => ({ value: c.code, label: c[locale] })),
    [locale],
  );

  // ── CAPTCHA (P1-11-FE-16) ─────────────────────────────────────────────────
  // The requirement is advertised via the public Turnstile site key — env
  // config mirroring the backend `Captcha:Enabled` gate (no advertisement
  // endpoint exists; ops keeps the two in sync, like GoogleAuth__ClientId ↔
  // EXPO_PUBLIC_GOOGLE_CLIENT_ID). Site key absent → no widget, no token.
  // The challenge is web-only this wave; native sends no token.
  const turnstileSiteKey = process.env.EXPO_PUBLIC_TURNSTILE_SITE_KEY ?? '';
  const captchaRequired = Platform.OS === 'web' && Boolean(turnstileSiteKey);
  // UI-only client state (NOT server data → local state, not Zustand).
  const [captchaToken, setCaptchaToken] = useState<string | null>(null);
  const [captchaResetSignal, setCaptchaResetSignal] = useState(0);
  const theme = useThemeStore((s) => s.theme);

  const { control, handleSubmit, formState } = useForm<RegisterParentFormValues>({
    resolver: zodResolver(registerParentSchema),
    defaultValues: { fullName: '', country: '', email: '', password: '', acceptedTerms: false },
    mode: 'onTouched',
  });

  const serverMessage = register.isError
    ? resolveError(register.error, {
        hints: [
          // CAPTCHA hint first — its backend message must not fall through to
          // the broader 'email'/'password' substring hints below.
          { contains: CAPTCHA_FAILED_HINTS, key: 'auth.register.errors.captchaFailed' },
          { contains: ['exists', 'duplicate', 'taken', 'email'], key: 'auth.register.errors.duplicateEmail' },
          { contains: ['password', 'weak'], key: 'auth.register.errors.weakPassword' },
        ],
        byStatus: { 409: 'auth.register.errors.duplicateEmail', 422: 'auth.register.errors.weakPassword' },
      })
    : null;

  const onSubmit = handleSubmit(async (values) => {
    try {
      // P1-12-FE-7: `country` and `acceptedTerms` are now sent to the backend.
      // `acceptedTerms` MUST remain false by default and only become true via
      // the user checking the CheckboxField (security requirement — never auto-set).
      // P1-11-FE-16: `captchaToken` is sent only when the requirement is
      // advertised (site key set) — otherwise the field is omitted entirely.
      const res = await register.mutateAsync({
        email: values.email.trim(),
        password: values.password,
        fullName: values.fullName?.trim() || undefined,
        country: values.country || undefined,
        acceptedTerms: values.acceptedTerms,
        captchaToken: captchaRequired && captchaToken ? captchaToken : undefined,
      });
      if (res.accessToken && res.refreshToken?.tokenString) {
        await setTokens({ accessToken: res.accessToken, refreshToken: res.refreshToken.tokenString });
        onSuccess();
      }
    } catch {
      // Failure is surfaced inline via register.error → serverMessage; swallow
      // the rejection so it doesn't bubble as an uncaught promise error.
      // Turnstile tokens are single-use — force a fresh challenge for retry.
      if (captchaRequired) {
        setCaptchaToken(null);
        setCaptchaResetSignal((n) => n + 1);
      }
    }
  });

  const disabled = register.isPending;
  // With CAPTCHA advertised, submit stays gated until the challenge passes.
  const submitDisabled = disabled || formState.isSubmitting || (captchaRequired && !captchaToken);

  return (
    <Stack testID="register-form" gap="$4">
      {/* Full name + Country — two-column row at tablet+, stacked on phone. */}
      <Stack
        gap="$4"
        $tablet={{ flexDirection: direction === 'rtl' ? 'row-reverse' : 'row' }}
      >
        <Stack flex={1} $tablet={{ flex: 1 }}>
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
                testID="register-fullname"
              />
            )}
          />
        </Stack>
        <Stack flex={1} $tablet={{ flex: 1 }}>
          <Controller
            control={control}
            name="country"
            render={({ field, fieldState }) => (
              <Select
                label={t('auth.register.labelCountry')}
                placeholder={t('auth.register.countryPlaceholder')}
                value={field.value || null}
                onChange={(v) => field.onChange(String(v))}
                options={countryOptions}
                error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                direction={direction}
                disabled={disabled}
                testID="register-country"
              />
            )}
          />
        </Stack>
      </Stack>

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
            forceValueLtr
            error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
            direction={direction}
            disabled={disabled}
            testID="register-email"
          />
        )}
      />

      <Controller
        control={control}
        name="password"
        render={({ field, fieldState }) => {
          const score = scorePassword(field.value ?? '');
          const strengthLabel = score > 0 ? t(STRENGTH_LABEL_KEY[score as Exclude<PasswordStrength, 0>]) : '';
          return (
            <Stack gap="$2">
              <TextField
                label={t('auth.register.labelPassword')}
                value={field.value}
                onChangeText={field.onChange}
                secureTextEntry
                showLabel={t('auth.login.showPassword')}
                hideLabel={t('auth.login.hidePassword')}
                autoComplete="new-password"
                error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                direction={direction}
                disabled={disabled}
                testID="register-password"
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
                  {t('auth.register.passwordHelper')}
                </Text>
              ) : null}
            </Stack>
          );
        }}
      />

      <Controller
        control={control}
        name="acceptedTerms"
        render={({ field, fieldState }) => {
          const checked = field.value === true;
          return (
            // Card-frame wrapper around the consent checkbox (align-register m-3):
            // `$card` resting; subtle green success tint + border when accepted.
            // borderRadius="$cardInner" (14px) per design-system radius token.
            // checked border/bg use $successSoft (0.18 alpha) and a lighter $successSoft variant.
            <Stack
              paddingVertical="$3"
              paddingHorizontal="$4"
              borderRadius="$cardInner"
              borderWidth={1}
              borderColor={checked ? '$successSoftStrong' : '$border'}
              backgroundColor={checked ? '$successSoft' : '$card'}
            >
              <CheckboxField
                checked={checked}
                onChange={field.onChange}
                label={<TermsLabel direction={direction} />}
                accessibilityLabel={t('auth.register.termsA11y')}
                error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                direction={direction}
                disabled={disabled}
                testID="register-terms"
              />
            </Stack>
          );
        }}
      />

      {/* Turnstile challenge — rendered ONLY when the server advertises the
          requirement (site key configured); sits between consent and submit. */}
      {captchaRequired ? (
        <TurnstileWidget
          siteKey={turnstileSiteKey}
          onToken={setCaptchaToken}
          resetSignal={captchaResetSignal}
          theme={theme}
          direction={direction}
          testID="register-captcha"
        />
      ) : null}

      <ServerErrorBanner message={serverMessage} direction={direction} testID="register-error" />

      <Button
        variant="primary"
        size="full"
        accessibilityLabel={t('auth.register.submitButton')}
        loading={register.isPending}
        disabled={submitDisabled}
        onPress={onSubmit}
        testID="register-submit"
      >
        {t('auth.register.submitButton')}
      </Button>

      <Stack flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'} justifyContent="center" gap="$1" marginTop="$5">
        <Text color="$fg3" fontSize={14} fontFamily="$body">
          {t('auth.register.haveAccount')}
        </Text>
        <Text
          testID="register-sign-in-link"
          color="$primaryLight"
          fontSize={14}
          fontWeight="800"
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

/**
 * TermsLabel — the consent sentence with inline "Terms" / "Privacy Policy"
 * emphasis. Built by splitting the translated template around its `{{terms}}`
 * and `{{privacy}}` placeholders so word order stays correct in ar + en.
 */
const TERMS_TOKEN = '@@TERMS@@';
const PRIVACY_TOKEN = '@@PRIVACY@@';

function TermsLabel({ direction }: { direction: 'ltr' | 'rtl' }) {
  const { t } = useTranslation();
  const terms = t('auth.register.termsLink');
  const privacy = t('auth.register.privacyLink');
  const template = t('auth.register.termsLabel', {
    terms: TERMS_TOKEN,
    privacy: PRIVACY_TOKEN,
  });

  // Split on the sentinel tokens; render plain text between, links for tokens.
  const parts = template.split(new RegExp('(' + TERMS_TOKEN + '|' + PRIVACY_TOKEN + ')'));

  return (
    <Text
      color="$fg2"
      fontSize={13}
      lineHeight={19}
      fontFamily="$body"
      textAlign={direction === 'rtl' ? 'right' : 'left'}
      writingDirection={direction}
    >
      {parts.map((part, idx) => {
        if (part === TERMS_TOKEN) {
          return (
            <Text key={idx} color="$primaryLight" fontWeight="700" fontFamily="$body">
              {terms}
            </Text>
          );
        }
        if (part === PRIVACY_TOKEN) {
          return (
            <Text key={idx} color="$primaryLight" fontWeight="700" fontFamily="$body">
              {privacy}
            </Text>
          );
        }
        return part;
      })}
    </Text>
  );
}
