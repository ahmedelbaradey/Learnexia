/**
 * SettingsWeb — the parent "Settings" main content (capture `web/07-settings.png`):
 * page header ("Settings" + "Manage your account and preferences" + "This week"
 * select + "Send Report"), a left-rail six-tab list (reusable `Tabs` primitive),
 * and the active tab's panel.
 *
 * Functional tabs:
 *  - Profile: avatar (image from `avatarUrl`, else initials) + Upload photo /
 *    Remove, Full name, Phone, Country select, Cancel / Save changes. Initial
 *    values load from `useMyProfile` (P1-12 backend); Save persists fullName /
 *    phone / country via `useUpdateProfile` with success + error feedback; Cancel
 *    resets to the loaded values. Email is display-only (not in the profile
 *    contract). Avatar upload/remove stay UI-only stubs — the avatar-upload
 *    backend (P1-12 BE-4) isn't built yet (TODO(P1-12 avatar upload)).
 *  - Language & region: switches the app language (en↔ar) app-wide + persists via
 *    the locale store (reused from the Login locale switch); region is UI-only.
 * Placeholder tabs (P2-12): Notifications / Linked children / Security /
 *  Plan & billing render a "coming soon" panel (TODO(P2-12)).
 *
 * RTL + ar/en throughout; tokens only (no raw hex); reuses `@learnexia/ui`
 * primitives (Tabs, Avatar, Button, TextField, Select) — no new design pattern.
 */
import {
  useMyProfile,
  useUpdateProfile,
  useUploadAvatar,
  useRemoveAvatar,
  type AccountProfileResponse,
  type FileParameter,
} from '@learnexia/api-client';
import { LanguagePanel } from './settings/LanguagePanel';
import { LinkedChildrenPanel } from './settings/LinkedChildrenPanel';
import { NotificationsPanel } from './settings/NotificationsPanel';
import { PlanPanel } from './settings/PlanPanel';
import { SecurityPanel } from './settings/SecurityPanel';
import { COUNTRIES, type CountryCode } from '@learnexia/shared';
import { Avatar, Button, Select, Tabs, TextField, type TabItem } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React, { useEffect, useRef, useState } from 'react';
import { Platform } from 'react-native';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';

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
  [SETTINGS_TAB.LinkedChildren]: '👨‍👩‍👦',
  [SETTINGS_TAB.Security]: '🛡️',
  [SETTINGS_TAB.Billing]: '💎',
  [SETTINGS_TAB.Language]: '🌍',
};

const TAB_LABEL_KEY: Record<SettingsTabKey, string> = {
  [SETTINGS_TAB.Profile]: 'parent.settings.tabs.profile',
  [SETTINGS_TAB.Notifications]: 'parent.settings.tabs.notifications',
  [SETTINGS_TAB.LinkedChildren]: 'parent.settings.tabs.linkedChildren',
  [SETTINGS_TAB.Security]: 'parent.settings.tabs.security',
  [SETTINGS_TAB.Billing]: 'parent.settings.tabs.billing',
  [SETTINGS_TAB.Language]: 'parent.settings.tabs.language',
};

export function SettingsWeb() {
  const { t } = useTranslation();
  const { direction, locale } = useLocale();
  const profile = useMyProfile();
  const [period, setPeriod] = useState<string>(REPORTING_PERIOD.ThisWeek);
  const [activeTab, setActiveTab] = useState<SettingsTabKey>(SETTINGS_TAB.Profile);

  // Always `row` — the document `dir="rtl"` flips the layout once (rail on the
  // right in Arabic). An explicit `row-reverse` for RTL would double-flip it.
  const rowDir = 'row' as const;

  const tabItems: TabItem[] = (Object.values(SETTINGS_TAB) as SettingsTabKey[]).map((key) => ({
    value: key,
    label: t(TAB_LABEL_KEY[key]),
    icon: TAB_ICON[key],
  }));

  return (
    <Stack testID="settings-root" flexDirection="column" width="100%">
      {/* Header — own padding + a 1px bottom rule (web-page-header card). */}
      <Stack
        flexDirection={rowDir}
        alignItems="flex-start"
        justifyContent="space-between"
        gap="$4"
        flexWrap="wrap"
        paddingVertical={20}
        paddingHorizontal={28}
        borderBottomWidth={1}
        borderBottomColor="rgba(255,255,255,0.06)"
      >
        <Stack flexDirection="column" gap="$1">
          <Text
            color="$fg1"
            fontSize={24}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('parent.settings.title')}
          </Text>
          <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
            {t('parent.settings.subtitle')}
          </Text>
        </Stack>

        <Stack flexDirection={rowDir} alignItems="center" gap="$3">
          <Stack width={130}>
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
          {/* Send Report — Phase-5 stub (no-op until analytics ship). md height +
              primary glow are foundation (Button primary variant). */}
          <Button
            variant="primary"
            size="md"
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
      <Stack flexDirection={rowDir} gap="$5" flexWrap="wrap" alignItems="flex-start" padding="$6">
        <Stack width={220} minWidth={180}>
          <Tabs
            items={tabItems}
            value={activeTab}
            onChange={(v) => setActiveTab(v as SettingsTabKey)}
            direction={direction}
            accessibilityLabel={t('parent.settings.tabs.navLabel')}
            testID="settings-tabs-nav"
          />
        </Stack>

        <Stack flex={1} minWidth={320}>
          {(() => {
            switch (activeTab) {
              case SETTINGS_TAB.Profile:
                return (
                  <ProfilePanel
                    direction={direction}
                    rowDir={rowDir}
                    profile={profile.data}
                    isLoading={profile.isPending}
                  />
                );
              case SETTINGS_TAB.Language:
                return <LanguagePanel direction={direction} rowDir={rowDir} locale={locale} />;
              case SETTINGS_TAB.Notifications:
                return <NotificationsPanel direction={direction} rowDir={rowDir} />;
              case SETTINGS_TAB.LinkedChildren:
                return <LinkedChildrenPanel direction={direction} rowDir={rowDir} />;
              case SETTINGS_TAB.Security:
                return <SecurityPanel direction={direction} rowDir={rowDir} />;
              case SETTINGS_TAB.Billing:
                return <PlanPanel direction={direction} rowDir={rowDir} />;
              default:
                return <ComingSoonPanel direction={direction} />;
            }
          })()}
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

function PanelHeader({ title, subtitle, direction }: { title: string; subtitle: string; direction: 'ltr' | 'rtl' }) {
  return (
    <Stack flexDirection="column" gap="$1">
      <Text color="$fg1" fontSize={16} fontWeight="800" fontFamily="$heading" writingDirection={direction}>
        {title}
      </Text>
      <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
        {subtitle}
      </Text>
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* Profile panel — loads via useMyProfile, saves via useUpdateProfile   */
/* ------------------------------------------------------------------ */

interface ProfilePanelProps extends PanelProps {
  rowDir: 'row' | 'row-reverse';
  /** Loaded profile (fullName / phone / country / avatarUrl) — undefined while fetching. */
  profile: AccountProfileResponse | undefined;
  isLoading: boolean;
}

/** Resolve a profile `country` string to a known `CountryCode`, else null. */
function toCountryCode(country: string | undefined): CountryCode | null {
  if (!country) return null;
  const match = COUNTRIES.find((c) => c.code === country);
  return match ? (match.code as CountryCode) : null;
}

/** Maximum avatar file size allowed (5 MB). */
const AVATAR_MAX_BYTES = 5 * 1024 * 1024;
/** Accepted avatar MIME types (web <input accept> + client-side guard). */
// Kept in sync with the avatar helper/wrongType copy ("PNG or JPG"). The MIME guard
// in handleFileChange derives its allowlist from this string, and the <input accept> uses it.
const AVATAR_ACCEPT = 'image/png,image/jpeg';

function ProfilePanel({ direction, rowDir, profile, isLoading }: ProfilePanelProps) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  const updateProfile = useUpdateProfile();
  const uploadAvatar = useUploadAvatar();
  const removeAvatar = useRemoveAvatar();
  const resolveError = useServerError();

  // Web-only hidden file input ref for programmatic <input type="file"> pick.
  // Platform-guarded: only wired on web; native avatar upload is deferred (B-3).
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  // Avatar feedback state (inline, below the avatar row).
  const [avatarError, setAvatarError] = useState<string | null>(null);
  const [avatarSuccess, setAvatarSuccess] = useState<string | null>(null);

  const avatarPending = uploadAvatar.isPending || removeAvatar.isPending;
  const hasAvatar = Boolean(profile?.avatarUrl);

  // Form state seeded from the loaded profile. `email` is display-only (not part
  // of the profile contract). Re-sync whenever the loaded profile changes.
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [country, setCountry] = useState<CountryCode | null>(null);

  const resetToLoaded = React.useCallback(() => {
    setName(profile?.fullName ?? '');
    setPhone(profile?.phone ?? '');
    setCountry(toCountryCode(profile?.country));
    updateProfile.reset();
  }, [profile, updateProfile]);

  useEffect(() => {
    setName(profile?.fullName ?? '');
    setPhone(profile?.phone ?? '');
    setCountry(toCountryCode(profile?.country));
  }, [profile]);

  // Email is display-only. The current `AccountProfileResponse` contract does
  // NOT include `email` yet (backend dependency — see report). Read it
  // defensively so the field populates automatically once BE adds it, without
  // a type error in the meantime.
  const profileEmail = (profile as { email?: string } | undefined)?.email ?? '';

  const countryOptions = COUNTRIES.map((c) => ({ value: c.code, label: locale === 'ar' ? c.ar : c.en }));

  const serverMessage = updateProfile.isError
    ? resolveError(updateProfile.error, {
        byStatus: { 400: 'parent.settings.profile.saveError', 422: 'parent.settings.profile.saveError' },
      })
    : null;

  const onSave = () => {
    updateProfile.mutate({
      fullName: name.trim(),
      phone: phone.trim() || undefined,
      country: country ?? undefined,
    });
  };

  /** Trigger the hidden web file input. Native deferred per plan B-3. */
  const handleUploadPress = () => {
    if (Platform.OS === 'web' && fileInputRef.current) {
      setAvatarError(null);
      setAvatarSuccess(null);
      fileInputRef.current.value = '';
      fileInputRef.current.click();
    }
  };

  /** Validate and upload the chosen file (web only). */
  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Client-side type guard — match AVATAR_ACCEPT exactly (the <input accept> attr is advisory
    // only; this allowlist is the real client check, though the server remains authoritative).
    if (!AVATAR_ACCEPT.split(',').includes(file.type)) {
      setAvatarError(t('parent.settings.profile.avatar.wrongType'));
      return;
    }
    // Client-side size cap (5 MB).
    if (file.size > AVATAR_MAX_BYTES) {
      setAvatarError(t('parent.settings.profile.avatar.tooLarge'));
      return;
    }

    setAvatarError(null);
    setAvatarSuccess(null);

    const param: FileParameter = { data: file, fileName: file.name };
    try {
      await uploadAvatar.mutateAsync(param);
      setAvatarSuccess(t('parent.settings.profile.avatar.uploadSuccess'));
    } catch {
      setAvatarError(t('parent.settings.profile.avatar.uploadError'));
    }
  };

  /** Direct remove — no confirm dialog (reversible; danger button is the UX safeguard). */
  const handleRemove = async () => {
    setAvatarError(null);
    setAvatarSuccess(null);
    try {
      await removeAvatar.mutateAsync();
      setAvatarSuccess(t('parent.settings.profile.avatar.removeSuccess'));
    } catch {
      setAvatarError(t('parent.settings.profile.avatar.removeError'));
    }
  };

  if (isLoading) {
    return (
      <PanelSurface>
        <PanelHeader
          title={t('parent.settings.profile.title')}
          subtitle={t('parent.settings.profile.subtitle')}
          direction={direction}
        />
        <Stack alignItems="center" paddingVertical="$8">
          <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
            {t('parent.settings.profile.loading')}
          </Text>
        </Stack>
      </PanelSurface>
    );
  }

  return (
    <PanelSurface>
      <PanelHeader
        title={t('parent.settings.profile.title')}
        subtitle={t('parent.settings.profile.subtitle')}
        direction={direction}
      />

      {/* Avatar upload/remove — wired (P1-12-FE FE-2).
          Web: hidden <input type="file"> triggered programmatically.
          Native: deferred (Platform guard; see plan B-3). */}
      {Platform.OS === 'web' && (
        <input
          ref={fileInputRef}
          data-testid="avatar-file-input"
          type="file"
          accept={AVATAR_ACCEPT}
          style={{ display: 'none' }}
          onChange={handleFileChange}
          aria-hidden
        />
      )}
      <Stack flexDirection={rowDir} alignItems="center" gap={18}>
        {/* Avatar circle with pending overlay while mutation is in flight. */}
        <Stack position="relative">
          <Avatar
            name={name || profile?.fullName || 'A'}
            uri={profile?.avatarUrl || undefined}
            size="xl"
            accessibilityLabel={t('parent.settings.profile.title')}
          />
          {avatarPending && (
            <Stack
              position="absolute"
              top={0}
              left={0}
              right={0}
              bottom={0}
              borderRadius={9999}
              backgroundColor="$overlay"
              alignItems="center"
              justifyContent="center"
              aria-busy
            >
              <Text fontSize={20} accessibilityElementsHidden>
                {'⏳'}
              </Text>
            </Stack>
          )}
        </Stack>

        <Stack flexDirection="column" gap="$2">
          <Stack flexDirection={rowDir} gap="$3">
            <Button
              variant="primary"
              size="sm"
              loading={uploadAvatar.isPending}
              disabled={avatarPending}
              accessibilityLabel={t('parent.settings.profile.uploadPhoto')}
              onPress={handleUploadPress}
              testID="avatar-upload-button"
            >
              {t('parent.settings.profile.uploadPhoto')}
            </Button>
            {/* Remove shown only when a photo is set (no remove on initials-only avatar). */}
            {hasAvatar && (
              <Button
                variant="danger"
                size="sm"
                loading={removeAvatar.isPending}
                disabled={avatarPending}
                accessibilityLabel={t('parent.settings.profile.removePhoto')}
                onPress={handleRemove}
                testID="avatar-remove-button"
              >
                {t('parent.settings.profile.removePhoto')}
              </Button>
            )}
          </Stack>

          {/* Helper / feedback text below the button group. */}
          {avatarError ? (
            <Text
              color="$danger"
              fontSize={12}
              fontFamily="$body"
              textAlign={direction === 'rtl' ? 'right' : 'left'}
              writingDirection={direction}
              accessibilityLiveRegion="assertive"
            >
              {avatarError}
            </Text>
          ) : avatarSuccess ? (
            <Text
              color="$success"
              fontSize={12}
              fontFamily="$body"
              textAlign={direction === 'rtl' ? 'right' : 'left'}
              writingDirection={direction}
              accessibilityLiveRegion="polite"
            >
              {avatarSuccess}
            </Text>
          ) : avatarPending ? (
            <Text
              color="$fg3"
              fontSize={12}
              fontFamily="$body"
              textAlign={direction === 'rtl' ? 'right' : 'left'}
              writingDirection={direction}
            >
              {t('parent.settings.profile.avatar.uploading')}
            </Text>
          ) : (
            <Text
              color="$fg3"
              fontSize={12}
              fontFamily="$body"
              textAlign={direction === 'rtl' ? 'right' : 'left'}
              writingDirection={direction}
            >
              {t('parent.settings.profile.avatar.helper')}
            </Text>
          )}
        </Stack>
      </Stack>

      {/* Two-column field grid (full name / email, phone / country) */}
      <Stack flexDirection={rowDir} gap={14} flexWrap="wrap">
        <Stack flex={1} minWidth={240}>
          <TextField
            label={t('parent.settings.profile.fullName')}
            value={name}
            onChangeText={setName}
            autoCapitalize="words"
            direction={direction}
            disabled={updateProfile.isPending}
          />
        </Stack>
        <Stack flex={1} minWidth={240}>
          {/* Email is display-only — not part of the profile-update contract.
              Value comes from the loaded profile; forced LTR (Latin technical
              string) even in an RTL form per SKILL.md. */}
          <TextField
            label={t('parent.settings.profile.email')}
            value={profileEmail}
            onChangeText={() => undefined}
            keyboardType="email-address"
            autoComplete="email"
            direction={direction}
            forceLtr
            disabled
          />
        </Stack>
      </Stack>

      <Stack flexDirection={rowDir} gap={14} flexWrap="wrap">
        <Stack flex={1} minWidth={240}>
          {/* Phone numbers stay Latin + LTR even in an RTL form (SKILL.md). */}
          <TextField
            label={t('parent.settings.profile.phone')}
            value={phone}
            onChangeText={setPhone}
            keyboardType="phone-pad"
            autoComplete="tel"
            direction={direction}
            forceLtr
            disabled={updateProfile.isPending}
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

      {/* Save feedback — error banner + success confirmation. */}
      <ServerErrorBanner message={serverMessage} direction={direction} />
      {updateProfile.isSuccess ? (
        <Stack backgroundColor="$successSoft" borderRadius="$sm" padding="$3" accessibilityLiveRegion="polite">
          <Text
            color="$success"
            fontSize={14}
            fontFamily="$body"
            textAlign={direction === 'rtl' ? 'right' : 'left'}
            writingDirection={direction}
          >
            {t('parent.settings.profile.saveSuccess')}
          </Text>
        </Stack>
      ) : null}

      {/* Cancel resets to loaded values; Save persists via useUpdateProfile.
          md height (~52px) + primary glow are foundation (Button variant). */}
      <Stack flexDirection={rowDir} justifyContent="flex-end" gap={10} paddingTop={6}>
        <Button
          variant="ghost"
          size="md"
          disabled={updateProfile.isPending}
          accessibilityLabel={t('parent.settings.profile.cancel')}
          onPress={resetToLoaded}
          testID="profile-cancel"
        >
          {t('parent.settings.profile.cancel')}
        </Button>
        <Button
          variant="primary"
          size="md"
          loading={updateProfile.isPending}
          disabled={updateProfile.isPending}
          accessibilityLabel={t('parent.settings.profile.save')}
          onPress={onSave}
          testID="profile-save"
        >
          {t('parent.settings.profile.save')}
        </Button>
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
