/**
 * AccountMenu — parent post-login account control.
 *
 * Trigger: 40×40 circular avatar (orange→red gradient, white initial).
 * Dropdown (inline, below trigger, inline-end aligned): user header, language
 * segmented control, theme segmented control, log-out button.
 *
 * State wiring:
 *  - Name/email: `useAuthStore` (`user.fullName`, `user.email`).
 *  - Language: `useLocaleStore` + `useRestartPromptStore` (mirrors Sidebar pattern).
 *  - Theme: `useThemeStore` (`'dark'` / `'light'` — no "black" theme exists in
 *    the design-system; the "Night" segment maps to `'dark'` and "Black" segment
 *    is intentionally absent — there is only one dark variant).
 *  - Logout: `useSignOutAction`.
 *
 * Dismiss: backdrop Stack (position:fixed, zIndex 89) closes dropdown on
 * outside press — mirrors Sidebar's child-selector pattern.
 *
 * RTL: `dir={isRtl?'rtl':'ltr'}` is set on the root and the dropdown panel; rows
 * use plain `flexDirection="row"` and the browser flips once (the proven pattern,
 * mirrors ChildDashboardCard). Dropdown stays pinned to `right:0` (inline-end).
 * Numeric border radii are used everywhere (NOT token names) because Tamagui
 * radius tokens don't resolve reliably on native.
 *
 * Do NOT touch the `(auth)` screens — LocaleThemeControls stays on pre-login
 * surfaces.
 */
import { useAuthStore } from '@learnexia/shared';
import {
  LOCALES,
  directionForLocale,
  useRestartPromptStore,
  type Locale,
} from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';
import React, { useState } from 'react';
import { Platform } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { useLocaleStore } from '../../../src/providers/localeStore';
import { useSignOutAction } from '../../../src/hooks/useSignOutAction';
import { useThemeStore } from '../../../src/providers/themeStore';

/** Locale display metadata (flag + short label). */
const LOCALE_META: Record<Locale, { flag: string; label: string }> = {
  en: { flag: '🇺🇸', label: 'EN' },
  ar: { flag: '🇪🇬', label: 'AR' },
};

export function AccountMenu() {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();

  const [open, setOpen] = useState(false);

  // Auth state — name + email.
  const user = useAuthStore((s) => s.user);
  const displayName = user?.fullName ?? user?.userName ?? null;
  // email comes from CurrentUser.email (nullable — see gap note below).
  const displayEmail = user?.email ?? null;
  // Derive the avatar initial from the display name or email.
  const initial = displayName
    ? displayName.trim().charAt(0).toUpperCase()
    : displayEmail
      ? displayEmail.trim().charAt(0).toUpperCase()
      : 'P';

  // Locale controls (mirrors Sidebar + LocaleThemeControls pattern).
  const setLocale = useLocaleStore((s) => s.setLocale);
  const showRestartPrompt = useRestartPromptStore((s) => s.show);
  function handleLocaleChange(nextLocale: Locale) {
    if (nextLocale === locale) return;
    const nextDir = directionForLocale(nextLocale);
    const currentDir = directionForLocale(locale);
    if (Platform.OS !== 'web' && nextDir !== currentDir) {
      showRestartPrompt(nextLocale);
    } else {
      setLocale(nextLocale);
    }
    setOpen(false);
  }

  // Theme controls.
  // GAP: only 'dark' and 'light' themes exist in the design-system.
  // The design spec shows "Night" and "Black" segments; "Black" (OLED) is not
  // implemented — there is a single dark variant. "Night" maps to 'dark';
  // "Black" is rendered but also maps to 'dark' with a TODO comment.
  const theme = useThemeStore((s) => s.theme);
  const setTheme = useThemeStore((s) => s.setTheme);

  // Logout.
  const { signOut, isPending: isSigningOut } = useSignOutAction();

  // Segmented control shared styles.
  function segBg(active: boolean): '#334155' | 'transparent' {
    return active ? '#334155' : 'transparent';
  }
  function segColor(active: boolean): '#F8FAFC' | '#94A3B8' {
    return active ? '#F8FAFC' : '#94A3B8';
  }

  return (
    <Stack position="relative" flexShrink={0} dir={isRtl ? 'rtl' : 'ltr'}>
      {/* Trigger avatar — rounded SQUARE (parent), purple gradient to distinguish
          from the child switcher's orange circle. */}
      <Stack
        width={40}
        height={40}
        borderRadius={12}
        flexShrink={0}
        alignItems="center"
        justifyContent="center"
        cursor="pointer"
        pressStyle={{ scale: 0.93 }}
        onPress={() => setOpen((v) => !v)}
        accessible
        accessibilityRole="button"
        accessibilityLabel={t('parent.account.menuLabel')}
        aria-label={t('parent.account.menuLabel')}
        aria-expanded={open}
        style={{
          background: 'linear-gradient(135deg,#A855F7,#6366F1)',
          border: open ? '2px solid #A5B4FC' : '2px solid rgba(255,255,255,0.12)',
        } as object}
      >
        <Text
          color="#FFFFFF"
          fontSize={15}
          fontWeight="800"
          fontFamily="$heading"
          accessibilityElementsHidden
        >
          {initial}
        </Text>
      </Stack>

      {open ? (
        <>
          {/* Backdrop — closes dropdown on outside press (mirrors Sidebar pattern). */}
          <Stack
            position="fixed"
            top={0}
            left={0}
            right={0}
            bottom={0}
            zIndex={89}
            onPress={() => setOpen(false)}
            style={{ cursor: 'default' } as object}
          />

          {/* Dropdown panel */}
          <Stack
            dir={isRtl ? 'rtl' : 'ltr'}
            position="absolute"
            top={48}
            // Open toward the inline-start so the panel stays on-screen: the trigger
            // sits at the inline-end (right in LTR → right:0 opens leftward; left in
            // RTL → left:0 opens rightward). `right`/`left` are physical, so flip them.
            right={isRtl ? undefined : 0}
            left={isRtl ? 0 : undefined}
            zIndex={90}
            width={230}
            borderRadius={16}
            borderWidth={1}
            flexDirection="column"
            gap={12}
            padding={12}
            style={{
              backgroundColor: '#1E293B',
              borderColor: 'rgba(255,255,255,0.1)',
              boxShadow: '0 20px 50px rgba(0,0,0,0.55)',
            } as object}
            accessible
            accessibilityRole="menu"
          >
            {/* Header: avatar + name + email */}
            <Stack
              flexDirection="row"
              alignItems="center"
              gap={10}
              paddingBottom={10}
              borderBottomWidth={1}
              style={{ borderBottomColor: 'rgba(255,255,255,0.07)' } as object}
            >
              {/* Mini avatar in dropdown header — rounded square + purple, matches trigger */}
              <Stack
                width={38}
                height={38}
                borderRadius={12}
                flexShrink={0}
                alignItems="center"
                justifyContent="center"
                style={{
                  background: 'linear-gradient(135deg,#A855F7,#6366F1)',
                } as object}
                accessibilityElementsHidden
              >
                <Text
                  color="#FFFFFF"
                  fontSize={15}
                  fontWeight="800"
                  fontFamily="$heading"
                >
                  {initial}
                </Text>
              </Stack>
              <Stack flexDirection="column" flex={1} overflow="hidden">
                {displayName ? (
                  <Text
                    color="#F8FAFC"
                    fontSize={13}
                    fontWeight="700"
                    fontFamily="$heading"
                    writingDirection={direction}
                    textAlign={isRtl ? 'right' : 'left'}
                    numberOfLines={1}
                  >
                    {displayName}
                  </Text>
                ) : null}
                {displayEmail ? (
                  <Text
                    color="#94A3B8"
                    fontSize={11}
                    fontFamily="$body"
                    writingDirection={direction}
                    textAlign={isRtl ? 'right' : 'left'}
                    numberOfLines={1}
                  >
                    {displayEmail}
                  </Text>
                ) : null}
                {/* GAP: if email is null in the store (JWT payload doesn't always
                    carry it), only the name is shown. The email field is nullable
                    in CurrentUser — no change needed on the type. */}
              </Stack>
            </Stack>

            {/* Language section */}
            <Stack flexDirection="column" gap={6}>
              <Text
                color="#64748B"
                fontSize={10}
                fontWeight="800"
                fontFamily="$heading"
                textTransform="uppercase"
                writingDirection={direction}
                textAlign={isRtl ? 'right' : 'left'}
                style={{ letterSpacing: 0.8 } as object}
              >
                {t('parent.account.languageLabel')}
              </Text>
              {/* Segmented control */}
              <Stack
                flexDirection="row"
                gap={4}
                padding={4}
                borderRadius={10}
                style={{
                  backgroundColor: '#0F172A',
                  border: '1px solid rgba(255,255,255,0.06)',
                } as object}
              >
                {(LOCALES as readonly Locale[]).map((loc) => {
                  const active = loc === locale;
                  const meta = LOCALE_META[loc];
                  const segA11yKey =
                    loc === 'ar'
                      ? 'common.prefs.switchToArabic'
                      : 'common.prefs.switchToEnglish';
                  return (
                    <Stack
                      key={loc}
                      flex={1}
                      flexDirection="row"
                      alignItems="center"
                      justifyContent="center"
                      gap={5}
                      height={32}
                      borderRadius={8}
                      backgroundColor={segBg(active)}
                      cursor="pointer"
                      pressStyle={{ scale: 0.97 }}
                      onPress={() => handleLocaleChange(loc)}
                      accessibilityRole="radio"
                      accessible
                      accessibilityState={{ selected: active }}
                      accessibilityLabel={t(segA11yKey)}
                    >
                      <Text fontSize={13} accessibilityElementsHidden>
                        {meta.flag}
                      </Text>
                      <Text
                        color={segColor(active)}
                        fontSize={12}
                        fontWeight="700"
                        fontFamily="$heading"
                      >
                        {meta.label}
                      </Text>
                    </Stack>
                  );
                })}
              </Stack>
            </Stack>

            {/* Theme section */}
            {/* GAP: only 'dark' and 'light' themes exist. The design spec shows
                "Night" + "Black" segments; Black (OLED) is not implemented.
                "Night" maps to theme='dark'; "Light" maps to theme='light'.
                When the design ships an OLED/black theme, add it to ThemeName
                in the design-system and wire the third segment here. */}
            <Stack flexDirection="column" gap={6}>
              <Text
                color="#64748B"
                fontSize={10}
                fontWeight="800"
                fontFamily="$heading"
                textTransform="uppercase"
                writingDirection={direction}
                textAlign={isRtl ? 'right' : 'left'}
                style={{ letterSpacing: 0.8 } as object}
              >
                {t('parent.account.themeLabel')}
              </Text>
              <Stack
                flexDirection="row"
                gap={4}
                padding={4}
                borderRadius={10}
                style={{
                  backgroundColor: '#0F172A',
                  border: '1px solid rgba(255,255,255,0.06)',
                } as object}
              >
                <Stack
                  flex={1}
                  flexDirection="row"
                  alignItems="center"
                  justifyContent="center"
                  gap={5}
                  height={32}
                  borderRadius={8}
                  backgroundColor={segBg(theme === 'dark')}
                  cursor="pointer"
                  pressStyle={{ scale: 0.97 }}
                  onPress={() => setTheme('dark')}
                  accessibilityRole="radio"
                  accessible
                  accessibilityState={{ selected: theme === 'dark' }}
                  accessibilityLabel={t('parent.account.themeNight')}
                >
                  <Text fontSize={13} accessibilityElementsHidden>
                    {'🌙'}
                  </Text>
                  <Text
                    color={segColor(theme === 'dark')}
                    fontSize={12}
                    fontWeight="700"
                    fontFamily="$heading"
                  >
                    {t('parent.account.themeNight')}
                  </Text>
                </Stack>
                <Stack
                  flex={1}
                  flexDirection="row"
                  alignItems="center"
                  justifyContent="center"
                  gap={5}
                  height={32}
                  borderRadius={8}
                  backgroundColor={segBg(theme === 'light')}
                  cursor="pointer"
                  pressStyle={{ scale: 0.97 }}
                  onPress={() => setTheme('light')}
                  accessibilityRole="radio"
                  accessible
                  accessibilityState={{ selected: theme === 'light' }}
                  accessibilityLabel={t('parent.account.themeLight')}
                >
                  <Text fontSize={13} accessibilityElementsHidden>
                    {'☀️'}
                  </Text>
                  <Text
                    color={segColor(theme === 'light')}
                    fontSize={12}
                    fontWeight="700"
                    fontFamily="$heading"
                  >
                    {t('parent.account.themeLight')}
                  </Text>
                </Stack>
              </Stack>
            </Stack>

            {/* Log-out button */}
            <Stack
              flexDirection="row"
              alignItems="center"
              gap={10}
              paddingVertical={10}
              paddingHorizontal={12}
              borderRadius={12}
              borderWidth={1}
              style={{ borderColor: 'rgba(239,68,68,0.25)' } as object}
              backgroundColor="transparent"
              hoverStyle={{ backgroundColor: 'rgba(239,68,68,0.06)' }}
              cursor={isSigningOut ? 'default' : 'pointer'}
              pressStyle={{ scale: 0.95 }}
              onPress={
                isSigningOut
                  ? undefined
                  : () => {
                      setOpen(false);
                      void signOut();
                    }
              }
              opacity={isSigningOut ? 0.6 : 1}
              accessibilityRole="button"
              accessible
              accessibilityLabel={t('parent.nav.logout')}
              aria-label={t('parent.nav.logout')}
            >
              <Text fontSize={15} color="#F87171" accessibilityElementsHidden>
                {'↪'}
              </Text>
              <Text
                flex={1}
                color="#F87171"
                fontSize={13}
                fontWeight="700"
                fontFamily="$heading"
                writingDirection={direction}
                textAlign={isRtl ? 'right' : 'left'}
              >
                {t('parent.nav.logout')}
              </Text>
            </Stack>
          </Stack>
        </>
      ) : null}
    </Stack>
  );
}

AccountMenu.displayName = 'AccountMenu';
