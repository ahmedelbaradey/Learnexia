/**
 * RegisterForm — parent registration form (P1-11 capture).
 *
 * react-hook-form + zod (`registerParentSchema`). Field set: full name + country
 * (two-column row at tablet+), email, password (with "At least 6 characters"
 * helper), and a required Terms consent checkbox that gates submit. Posts via
 * `useRegisterParent`; on success persists tokens (`authStore.setTokens`) and
 * routes to onboarding. Maps `BaseResponse.errors` to localized banner copy.
 *
 * Client-only fields NOT posted:
 *  - `country` — collected for UX; backend `RegisterParentCommand` has no country
 *    field yet. TODO(P1-12): country-on-register needs a Batch-2 BE field.
 *  - `acceptedTerms` — consent gate; no backend flag exists yet (UI-only).
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
import { Button, CheckboxField, Select, TextField, type SelectOption } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { useMemo } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';

export function RegisterForm() {
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

  const { control, handleSubmit, formState } = useForm<RegisterParentFormValues>({
    resolver: zodResolver(registerParentSchema),
    defaultValues: { fullName: '', country: '', email: '', password: '', acceptedTerms: false },
    mode: 'onTouched',
  });

  const serverMessage = register.isError
    ? resolveError(register.error, {
        hints: [
          { contains: ['exists', 'duplicate', 'taken', 'email'], key: 'auth.register.errors.duplicateEmail' },
          { contains: ['password', 'weak'], key: 'auth.register.errors.weakPassword' },
        ],
        byStatus: { 409: 'auth.register.errors.duplicateEmail', 422: 'auth.register.errors.weakPassword' },
      })
    : null;

  const onSubmit = handleSubmit(async (values) => {
    try {
      // NOTE: `country` + `acceptedTerms` are intentionally NOT sent — the BE
      // RegisterParentCommand only accepts { email, password, fullName }.
      // TODO(P1-12): country-on-register needs a Batch-2 BE field.
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
    <Stack gap="$4">
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
          <Stack gap="$1">
            <TextField
              label={t('auth.register.labelPassword')}
              value={field.value}
              onChangeText={field.onChange}
              secureTextEntry
              error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
              direction={direction}
              disabled={disabled}
            />
            {!fieldState.error ? (
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
        )}
      />

      <Controller
        control={control}
        name="acceptedTerms"
        render={({ field, fieldState }) => (
          <CheckboxField
            checked={field.value === true}
            onChange={field.onChange}
            label={<TermsLabel direction={direction} />}
            accessibilityLabel={t('auth.register.termsA11y')}
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
