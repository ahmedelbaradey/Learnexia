/**
 * LanguagePanel — Settings → Language & region tab (P8-99-FE-3).
 *
 * Promotes the UI-language switch from an on-change persist into a backend-saved
 * choice. Language selection updates LOCAL form state only; persistence fires on
 * explicit **Save** (per parent-dashboard-uiux spec D.3).
 *
 * On Save:
 *   - Persists `User.PreferredLanguage` to the backend via `useUpdateUserLanguage`.
 *   - Syncs the locale store (Zustand) + react-i18next so the app reflects the
 *     change immediately (web) or after restart (native).
 *   - WEB: direction flips instantly (no restart needed).
 *   - NATIVE: if the new locale has a different direction, shows the existing
 *     `RestartPrompt` via `restartPromptStore.show()`.
 *
 * Region select is UI-only (no backend endpoint). Save persists language only;
 * region stays local. A helper note flags this to the user (spec D.3).
 *
 * Layout / tokens: mirrors the PlanPanel / ProfilePanel grammar (PanelSurface +
 * PanelHeader + flex row of Select widgets). RTL-aware via `direction` + `rowDir`
 * props passed by `SettingsWeb`. No raw hex; token-only.
 */
import { useUpdateUserLanguage } from '@learnexia/api-client';
import {
  COUNTRIES,
  LOCALES,
  directionForLocale,
  useRestartPromptStore,
  type Locale,
} from '@learnexia/shared';
import { Button, Select } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React, { useState } from 'react';
import { Platform } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../../src/hooks/useLocale';
import { useLocaleStore } from '../../../../src/providers/localeStore';
import { ServerErrorBanner } from '../../../../src/components/ServerErrorBanner';
import { useServerError } from '../../../../src/hooks/useServerError';

interface LanguagePanelProps {
  direction: 'ltr' | 'rtl';
  rowDir: 'row' | 'row-reverse';
  locale: Locale;
}

/** Fixed region set (enum-style; region select is UI-only until a region endpoint ships). */
const REGION = {
  Sa: 'SA',
  Eg: 'EG',
  Ae: 'AE',
} as const;

const LOCALE_LABEL_KEY: Record<Locale, string> = {
  en: 'common.prefs.switchToEnglish',
  ar: 'common.prefs.switchToArabic',
};

function PanelSurface({ children }: { children: React.ReactNode }) {
  return (
    <Stack
      flexDirection="column"
      gap={18}
      borderRadius="$modal"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="rgba(255,255,255,0.06)"
      padding={22}
    >
      {children}
    </Stack>
  );
}

function PanelHeader({
  title,
  subtitle,
  direction,
}: {
  title: string;
  subtitle: string;
  direction: 'ltr' | 'rtl';
}) {
  return (
    <Stack flexDirection="column" gap="$1">
      <Text
        color="$fg1"
        fontSize={16}
        fontWeight="800"
        fontFamily="$heading"
        writingDirection={direction}
      >
        {title}
      </Text>
      <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
        {subtitle}
      </Text>
    </Stack>
  );
}

export function LanguagePanel({ direction, rowDir, locale }: LanguagePanelProps) {
  const { t } = useTranslation();
  const { isRtl } = useLocale();
  const setLocale = useLocaleStore((s) => s.setLocale);
  const showRestartPrompt = useRestartPromptStore((s) => s.show);
  const updateUserLanguage = useUpdateUserLanguage();
  const resolveError = useServerError();

  // Local form state — selection does NOT persist until Save is tapped.
  const [pendingLocale, setPendingLocale] = useState<Locale>(locale);
  const [region, setRegion] = useState<string>(REGION.Sa);

  const languageOptions = LOCALES.map((loc) => ({
    value: loc,
    label: t(LOCALE_LABEL_KEY[loc]),
  }));

  const regionOptions = (Object.values(REGION) as string[]).map((code) => {
    const c = COUNTRIES.find((country) => country.code === code);
    return { value: code, label: locale === 'ar' ? (c?.ar ?? code) : (c?.en ?? code) };
  });

  const serverMessage = updateUserLanguage.isError
    ? resolveError(updateUserLanguage.error, {})
    : null;

  /** Apply the language: persist to backend + locale store. */
  function applyLocale(nextLocale: Locale) {
    const nextDirection = directionForLocale(nextLocale);
    const currentDirection = isRtl ? 'rtl' : 'ltr';

    if (Platform.OS !== 'web' && nextDirection !== currentDirection) {
      // NATIVE: direction change requires a restart for RTL to fully apply.
      showRestartPrompt(nextLocale);
    } else {
      // WEB (or same-direction): apply immediately.
      setLocale(nextLocale);
    }
  }

  function handleSave() {
    if (pendingLocale === locale && !updateUserLanguage.isError) {
      // No change — no-op (or the locale is already the active one).
      // Still apply in case the user toggled away and back.
    }
    updateUserLanguage.mutate(
      { userPreferredLanguage: pendingLocale },
      {
        onSuccess: () => {
          applyLocale(pendingLocale);
        },
      },
    );
  }

  function handleCancel() {
    setPendingLocale(locale);
    updateUserLanguage.reset();
  }

  return (
    <PanelSurface>
      <PanelHeader
        title={t('parent.settings.language.title')}
        subtitle={t('parent.settings.language.subtitle')}
        direction={direction}
      />

      <ServerErrorBanner message={serverMessage} direction={direction} />

      {updateUserLanguage.isSuccess ? (
        <Stack
          backgroundColor="$successSoft"
          borderRadius="$sm"
          padding="$3"
          accessibilityLiveRegion="polite"
        >
          <Text
            color="$success"
            fontSize={14}
            fontFamily="$body"
            textAlign={direction === 'rtl' ? 'right' : 'left'}
            writingDirection={direction}
          >
            {t('parent.settings.language.saveSuccess')}
          </Text>
        </Stack>
      ) : null}

      <Stack flexDirection={rowDir} gap={14} flexWrap="wrap">
        <Stack flex={1} minWidth={240}>
          <Select
            label={t('parent.settings.language.languageLabel')}
            value={pendingLocale}
            onChange={(v) => {
              setPendingLocale(v as Locale);
              updateUserLanguage.reset();
            }}
            options={languageOptions}
            direction={direction}
            accessibilityLabel={t('parent.settings.language.languageLabel')}
            testID="settings-language-switch"
          />
        </Stack>
        <Stack flex={1} minWidth={240}>
          {/* Region select is UI-only (no backend persistence yet). */}
          <Select
            label={t('parent.settings.language.regionLabel')}
            value={region}
            onChange={(v) => setRegion(String(v))}
            options={regionOptions}
            placeholder={t('parent.settings.language.regionPlaceholder')}
            direction={direction}
            accessibilityLabel={t('parent.settings.language.regionLabel')}
          />
        </Stack>
      </Stack>

      {/* Region UI-only note */}
      <Text
        color="$fg3"
        fontSize={11}
        fontFamily="$body"
        writingDirection={direction}
        textAlign={direction === 'rtl' ? 'right' : 'left'}
      >
        {t('parent.settings.language.regionUiOnlyNote')}
      </Text>

      {/* Save / Cancel action row */}
      <Stack flexDirection={rowDir} justifyContent="flex-end" gap={10} paddingTop={6}>
        <Button
          variant="ghost"
          size="md"
          disabled={updateUserLanguage.isPending}
          accessibilityLabel={t('parent.settings.language.cancel')}
          onPress={handleCancel}
          testID="language-cancel"
        >
          {t('parent.settings.language.cancel')}
        </Button>
        <Button
          variant="primary"
          size="md"
          loading={updateUserLanguage.isPending}
          disabled={updateUserLanguage.isPending}
          accessibilityLabel={t('parent.settings.language.save')}
          onPress={handleSave}
          testID="language-save"
        >
          {t('parent.settings.language.save')}
        </Button>
      </Stack>
    </PanelSurface>
  );
}

LanguagePanel.displayName = 'LanguagePanel';
