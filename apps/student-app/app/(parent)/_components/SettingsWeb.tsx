/**
 * SettingsWeb — the parent "Settings" main content (capture `web/07-settings.png`):
 * page header ("Settings" + "Manage your account and preferences" + "This week"
 * select + "Send Report"), a left-rail six-tab list (reusable `Tabs` primitive),
 * and the active tab's panel.
 *
 * Functional tabs:
 *  - Profile: avatar (initials) + Upload photo / Remove, Full name, Email, Phone,
 *    Country select, Cancel / Save changes. Save + avatar upload have NO backend
 *    yet (P1-12) → wired UI-first: Save is a disabled no-op stub (TODO(P1-12));
 *    no fake API call. Full name is prefilled from `useMe`; email/phone/country
 *    are local UI state (those columns land in P1-12).
 *  - Language & region: switches the app language (en↔ar) app-wide + persists via
 *    the locale store (reused from the Login locale switch); region is UI-only.
 * Placeholder tabs (P2-12): Notifications / Linked children / Security /
 *  Plan & billing render a "coming soon" panel (TODO(P2-12)).
 *
 * RTL + ar/en throughout; tokens only (no raw hex); reuses `@learnexia/ui`
 * primitives (Tabs, Avatar, Button, TextField, Select) — no new design pattern.
 */
import { useMe } from '@learnexia/api-client';
import { COUNTRIES, LOCALES, type CountryCode, type Locale } from '@learnexia/shared';
import { Avatar, Button, Select, Tabs, TextField, type TabItem } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { useLocaleStore } from '../../../src/providers/localeStore';

/** Fixed set of settings sections (enum-style const, never raw literals). */
const SETTINGS_TAB = {
  Profile: 'profile',
  Notifications: 'notifications',
  LinkedChildren: 'linkedChildren',
  Security: 'security',
  Billing: 'billing',
  Language: 'language',
} as const;

type SettingsTabKey = (typeof SETTINGS_TAB)[keyof typeof SETTINGS_TAB];

/** Fixed reporting-period set (enum-style; only "this week" wired in P1-11). */
const REPORTING_PERIOD = {
  ThisWeek: 'thisWeek',
} as const;

/** Icon glyph per tab (decorative; matches the capture's left-rail icons). */
const TAB_ICON: Record<SettingsTabKey, string> = {
  [SETTINGS_TAB.Profile]: '👤',
  [SETTINGS_TAB.Notifications]: '🔔',
  [SETTINGS_TAB.LinkedChildren]: '👨‍👧',
  [SETTINGS_TAB.Security]: '🛡️',
  [SETTINGS_TAB.Billing]: '💎',
  [SETTINGS_TAB.Language]: '🌐',
};

const TAB_LABEL_KEY: Record<SettingsTabKey, string> = {
  [SETTINGS_TAB.Profile]: 'parent.settings.tabs.profile',
  [SETTINGS_TAB.Notifications]: 'parent.settings.tabs.notifications',
  [SETTINGS_TAB.LinkedChildren]: 'parent.settings.tabs.linkedChildren',
  [SETTINGS_TAB.Security]: 'parent.settings.tabs.security',
  [SETTINGS_TAB.Billing]: 'parent.settings.tabs.billing',
  [SETTINGS_TAB.Language]: 'parent.settings.tabs.language',
};

/** Fixed region set (enum-style; region select is UI-only in P1-11). */
const REGION = {
  Sa: 'SA',
  Eg: 'EG',
  Ae: 'AE',
} as const;

export function SettingsWeb() {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const me = useMe();
  const [period, setPeriod] = useState<string>(REPORTING_PERIOD.ThisWeek);
  const [activeTab, setActiveTab] = useState<SettingsTabKey>(SETTINGS_TAB.Profile);

  const rowDir = isRtl ? 'row-reverse' : 'row';

  const tabItems: TabItem[] = (Object.values(SETTINGS_TAB) as SettingsTabKey[]).map((key) => ({
    value: key,
    label: t(TAB_LABEL_KEY[key]),
    icon: TAB_ICON[key],
  }));

  return (
    <Stack flexDirection="column" gap="$6" padding="$6" maxWidth={1200} width="100%" alignSelf="center">
      {/* Header */}
      <Stack flexDirection={rowDir} alignItems="flex-start" justifyContent="space-between" gap="$4" flexWrap="wrap">
        <Stack flexDirection="column" gap="$1">
          <Text
            color="$fg1"
            fontSize={26}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('parent.settings.title')}
          </Text>
          <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
            {t('parent.settings.subtitle')}
          </Text>
        </Stack>

        <Stack flexDirection={rowDir} alignItems="center" gap="$3">
          <Stack width={150}>
            <Select
              label={t('parent.overview.periodLabel')}
              hideLabel
              value={period}
              onChange={(v) => setPeriod(String(v))}
              options={[{ value: REPORTING_PERIOD.ThisWeek, label: t('parent.overview.periodThisWeek') }]}
              direction={direction}
              accessibilityLabel={t('parent.overview.periodLabel')}
            />
          </Stack>
          {/* Send Report — Phase-5 stub (no-op until analytics ship). */}
          <Button
            variant="primary"
            size="sm"
            accessibilityLabel={t('parent.overview.sendReport')}
            onPress={() => {
              /* TODO(P5-05): wire to the reports endpoint. */
            }}
          >
            {t('parent.overview.sendReport')}
          </Button>
        </Stack>
      </Stack>

      {/* Tab rail + active panel */}
      <Stack flexDirection={rowDir} gap="$5" flexWrap="wrap" alignItems="flex-start">
        <Stack width={210} minWidth={180}>
          <Tabs
            items={tabItems}
            value={activeTab}
            onChange={(v) => setActiveTab(v as SettingsTabKey)}
            direction={direction}
            accessibilityLabel={t('parent.settings.tabs.navLabel')}
          />
        </Stack>

        <Stack flex={1} minWidth={320}>
          {activeTab === SETTINGS_TAB.Profile ? (
            <ProfilePanel
              direction={direction}
              rowDir={rowDir}
              fullName={me.data?.fullName ?? ''}
            />
          ) : activeTab === SETTINGS_TAB.Language ? (
            <LanguagePanel direction={direction} rowDir={rowDir} locale={locale} />
          ) : (
            <ComingSoonPanel direction={direction} />
          )}
        </Stack>
      </Stack>
    </Stack>
  );
}

SettingsWeb.displayName = 'SettingsWeb';

/* ------------------------------------------------------------------ */
/* Shared panel surface (matches the capture's bordered card)          */
/* ------------------------------------------------------------------ */

interface PanelProps {
  direction: 'ltr' | 'rtl';
  rowDir?: 'row' | 'row-reverse';
}

function PanelSurface({ children }: { children: React.ReactNode }) {
  return (
    <Stack
      flexDirection="column"
      gap="$5"
      borderRadius="$card"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$border"
      padding="$6"
    >
      {children}
    </Stack>
  );
}

function PanelHeader({ title, subtitle, direction }: { title: string; subtitle: string; direction: 'ltr' | 'rtl' }) {
  return (
    <Stack flexDirection="column" gap="$1">
      <Text color="$fg1" fontSize={18} fontWeight="800" fontFamily="$heading" writingDirection={direction}>
        {title}
      </Text>
      <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
        {subtitle}
      </Text>
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* Profile panel — functional UI; Save is a disabled stub (P1-12)      */
/* ------------------------------------------------------------------ */

interface ProfilePanelProps extends PanelProps {
  rowDir: 'row' | 'row-reverse';
  fullName: string;
}

function ProfilePanel({ direction, rowDir, fullName }: ProfilePanelProps) {
  const { t } = useTranslation();
  const { locale } = useLocale();

  // UI-first local form state. `fullName` is prefilled from `useMe`; email /
  // phone / country are placeholder-only until the P1-12 profile API lands.
  const [name, setName] = useState(fullName);
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [country, setCountry] = useState<CountryCode | null>(null);

  const countryOptions = COUNTRIES.map((c) => ({ value: c.code, label: locale === 'ar' ? c.ar : c.en }));

  return (
    <PanelSurface>
      <PanelHeader
        title={t('parent.settings.profile.title')}
        subtitle={t('parent.settings.profile.subtitle')}
        direction={direction}
      />

      {/* Avatar + upload/remove (P1-12 stubs — no backend yet) */}
      <Stack flexDirection={rowDir} alignItems="center" gap="$4">
        <Avatar name={name || fullName || 'A'} size="xl" accessibilityLabel={t('parent.settings.profile.title')} />
        <Stack flexDirection={rowDir} alignItems="center" gap="$3">
          {/* TODO(P1-12): avatar upload — no storage/AvatarUrl backend yet. */}
          <Button
            variant="primary"
            size="sm"
            disabled
            accessibilityLabel={t('parent.settings.profile.uploadPhoto')}
            onPress={() => {
              /* TODO(P1-12): wire avatar upload. */
            }}
          >
            {t('parent.settings.profile.uploadPhoto')}
          </Button>
          <Button
            variant="ghost"
            size="sm"
            disabled
            accessibilityLabel={t('parent.settings.profile.removePhoto')}
            onPress={() => {
              /* TODO(P1-12): wire avatar removal. */
            }}
          >
            {t('parent.settings.profile.removePhoto')}
          </Button>
        </Stack>
      </Stack>

      {/* Two-column field grid (full name / email, phone / country) */}
      <Stack flexDirection={rowDir} gap="$4" flexWrap="wrap">
        <Stack flex={1} minWidth={240}>
          <TextField
            label={t('parent.settings.profile.fullName')}
            value={name}
            onChangeText={setName}
            autoCapitalize="words"
            direction={direction}
          />
        </Stack>
        <Stack flex={1} minWidth={240}>
          <TextField
            label={t('parent.settings.profile.email')}
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoComplete="email"
            direction={direction}
          />
        </Stack>
      </Stack>

      <Stack flexDirection={rowDir} gap="$4" flexWrap="wrap">
        <Stack flex={1} minWidth={240}>
          <TextField
            label={t('parent.settings.profile.phone')}
            value={phone}
            onChangeText={setPhone}
            keyboardType="phone-pad"
            autoComplete="tel"
            direction={direction}
          />
        </Stack>
        <Stack flex={1} minWidth={240}>
          <Select
            label={t('parent.settings.profile.country')}
            value={country}
            onChange={(v) => setCountry(v as CountryCode)}
            options={countryOptions}
            placeholder={t('parent.settings.profile.countryPlaceholder')}
            direction={direction}
            accessibilityLabel={t('parent.settings.profile.country')}
          />
        </Stack>
      </Stack>

      {/* Cancel / Save — Save disabled until the P1-12 profile-update API lands */}
      <Stack flexDirection={rowDir} justifyContent="flex-end" gap="$3">
        <Button
          variant="ghost"
          size="sm"
          accessibilityLabel={t('parent.settings.profile.cancel')}
          onPress={() => {
            setName(fullName);
            setEmail('');
            setPhone('');
            setCountry(null);
          }}
        >
          {t('parent.settings.profile.cancel')}
        </Button>
        {/* TODO(P1-12): wire profile save — no profile-update endpoint yet. */}
        <Button
          variant="primary"
          size="sm"
          disabled
          accessibilityLabel={t('parent.settings.profile.save')}
          onPress={() => {
            /* TODO(P1-12): no-op until the profile-update API lands. */
          }}
        >
          {t('parent.settings.profile.save')}
        </Button>
      </Stack>
    </PanelSurface>
  );
}

/* ------------------------------------------------------------------ */
/* Language & region panel — functional language switch + UI region    */
/* ------------------------------------------------------------------ */

interface LanguagePanelProps extends PanelProps {
  rowDir: 'row' | 'row-reverse';
  locale: Locale;
}

const LOCALE_LABEL_KEY: Record<Locale, string> = {
  en: 'common.prefs.switchToEnglish',
  ar: 'common.prefs.switchToArabic',
};

function LanguagePanel({ direction, rowDir, locale }: LanguagePanelProps) {
  const { t } = useTranslation();
  // Reuse the existing locale switch (Login uses the same store). Setting the
  // locale flips i18n + RTL app-wide and persists via the locale store; on
  // native a direction change is gated behind the restart prompt.
  const setLocale = useLocaleStore((s) => s.setLocale);
  const [region, setRegion] = useState<string>(REGION.Sa);

  const languageOptions = LOCALES.map((loc) => ({ value: loc, label: t(LOCALE_LABEL_KEY[loc]) }));
  const regionOptions = (Object.values(REGION) as string[]).map((code) => {
    const c = COUNTRIES.find((country) => country.code === code);
    return { value: code, label: locale === 'ar' ? (c?.ar ?? code) : (c?.en ?? code) };
  });

  return (
    <PanelSurface>
      <PanelHeader
        title={t('parent.settings.language.title')}
        subtitle={t('parent.settings.language.subtitle')}
        direction={direction}
      />

      <Stack flexDirection={rowDir} gap="$4" flexWrap="wrap">
        <Stack flex={1} minWidth={240}>
          <Select
            label={t('parent.settings.language.languageLabel')}
            value={locale}
            onChange={(v) => setLocale(v as Locale)}
            options={languageOptions}
            direction={direction}
            accessibilityLabel={t('parent.settings.language.languageLabel')}
          />
        </Stack>
        <Stack flex={1} minWidth={240}>
          {/* Region select is UI-only in P1-11 (no backend persistence yet). */}
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
    </PanelSurface>
  );
}

/* ------------------------------------------------------------------ */
/* Coming-soon panel — Notifications / Linked / Security / Billing      */
/* TODO(P2-12): build these four tabs.                                  */
/* ------------------------------------------------------------------ */

function ComingSoonPanel({ direction }: PanelProps) {
  const { t } = useTranslation();
  return (
    <PanelSurface>
      <Stack flexDirection="column" gap="$2" alignItems="center" paddingVertical="$8">
        <Text fontSize={32} accessibilityElementsHidden>
          {'🚧'}
        </Text>
        <Text color="$fg1" fontSize={18} fontWeight="800" fontFamily="$heading" textAlign="center" writingDirection={direction}>
          {t('parent.settings.comingSoon.title')}
        </Text>
        <Text color="$fg3" fontSize={14} fontFamily="$body" textAlign="center" writingDirection={direction}>
          {t('parent.settings.comingSoon.body')}
        </Text>
      </Stack>
    </PanelSurface>
  );
}
