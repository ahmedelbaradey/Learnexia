/**
 * Parent dashboard left sidebar (web ≥768) — Design Spec / capture
 * `web/04-my-children.png`. Composes the brand mark, a child-selector card
 * (active child avatar + name + "Grade N · Level L"), and the parent nav with an
 * active-state pill. Built from existing primitives (Stack/Text/Avatar) — no new
 * design pattern.
 *
 * Routing: "My Children" and "Overview" are implemented (P1-11); the other
 * destinations are Phase-2+/Phase-5 surfaces and render as non-active items that
 * route to the children screen for now (TODO: wire to their routes as they ship).
 * The caller passes `activeKey` so each screen lights up its own nav item.
 *
 * RTL: the whole column mirrors via logical flips; nav icon+label row uses
 * `flexDirection: rowDir` so the icon sits on the leading side (right in AR,
 * left in EN) and the label's `textAlign` follows the locale. The brand wordmark
 * keeps `writingDirection="ltr"` per SKILL.md. i18n: every label is a translation
 * key.
 *
 * Child-selector: opens an inline dropdown that lets the parent switch the active
 * child; wired to `useActiveChildStore` (same store as the header ChildSwitcher).
 *
 * Logout: bottom of sidebar; calls `useSignOutAction` which clears the local
 * session and redirects to /(auth)/login. Styled as a nav-item-like row.
 */
import { useMyChildren } from '@learnexia/api-client';
import { LOCALES, directionForLocale, type Direction, type Locale, useRestartPromptStore } from '@learnexia/shared';
import { Avatar } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import React, { useState } from 'react';
import { Image, Platform } from 'react-native';
import { useTranslation } from 'react-i18next';

import { assets } from '../../../src/assets';
import { useLocale } from '../../../src/hooks/useLocale';
import { useLocaleStore } from '../../../src/providers/localeStore';
import { useSignOutAction } from '../../../src/hooks/useSignOutAction';
import { useActiveChildStore } from '../../../src/providers/activeChildStore';
import { useThemeStore } from '../../../src/providers/themeStore';
import { formatNumber } from './ChildSwitcher';
import { getChildStatsStub } from './parentDashboardStubs';

/** Fixed parent nav destinations (enum-style const, never raw literals). */
export const NAV_ITEM = {
  MyChildren: 'myChildren',
  Overview: 'overview',
  Reports: 'reports',
  Activity: 'activity',
  Subjects: 'subjects',
  Settings: 'settings',
} as const;

export type NavItemKey = (typeof NAV_ITEM)[keyof typeof NAV_ITEM];

interface NavDef {
  key: NavItemKey;
  icon: string;
  /** Route this item navigates to (Phase-2+ items fall back to children). */
  route:
    | '/(parent)/children'
    | '/(parent)/overview'
    | '/(parent)/reports'
    | '/(parent)/settings';
}

const NAV: readonly NavDef[] = [
  { key: NAV_ITEM.Overview, icon: '📊', route: '/(parent)/overview' },
  { key: NAV_ITEM.MyChildren, icon: '👨‍👩‍👦', route: '/(parent)/children' },
  { key: NAV_ITEM.Reports, icon: '📝', route: '/(parent)/reports' },
  // TODO(P2+): wire activity/subjects to their own routes.
  { key: NAV_ITEM.Activity, icon: '🎯', route: '/(parent)/children' },
  { key: NAV_ITEM.Subjects, icon: '📚', route: '/(parent)/children' },
  { key: NAV_ITEM.Settings, icon: '⚙️', route: '/(parent)/settings' },
];

export interface SidebarChild {
  id: string;
  fullName: string;
  grade: number;
  level: number;
}

export interface SidebarProps {
  /** The child shown in the selector card (the active/first linked child). */
  activeChild?: SidebarChild;
  /** Which nav item is highlighted (defaults to My Children). */
  activeKey?: NavItemKey;
}

export function Sidebar({ activeChild, activeKey = NAV_ITEM.MyChildren }: SidebarProps) {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const router = useRouter();
  const rowDir = isRtl ? 'row-reverse' : 'row';

  // Child-selector dropdown state (mirrors ChildSwitcher pattern — same store shape).
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const query = useMyChildren();
  const activeChildId = useActiveChildStore((s) => s.activeChildId);
  const setActiveChildId = useActiveChildStore((s) => s.setActiveChildId);
  const openAddChild = useActiveChildStore((s) => s.openAddChild);
  const children = query.data ?? [];

  function handleSelectChild(childId: string) {
    setActiveChildId(childId);
    setDropdownOpen(false);
  }

  // Logout (calls sign-out + redirects to /(auth)/login).
  const { signOut, isPending: isSigningOut } = useSignOutAction();

  // Locale + theme controls (migrated from shell header into sidebar).
  const setLocale = useLocaleStore((s) => s.setLocale);
  const showRestartPrompt = useRestartPromptStore((s) => s.show);
  const theme = useThemeStore((s) => s.theme);
  const setTheme = useThemeStore((s) => s.setTheme);

  function handleLocaleChange(nextLocale: Locale) {
    if (nextLocale === locale) return;
    const nextDirection = directionForLocale(nextLocale);
    const currentDirection = directionForLocale(locale);
    if (Platform.OS !== 'web' && nextDirection !== currentDirection) {
      showRestartPrompt(nextLocale);
    } else {
      setLocale(nextLocale);
    }
  }

  return (
    <Stack
      flexDirection="column"
      width={240}
      height="100%"
      backgroundColor="$bg"
      // Divider faces the body content: border-right in EN (sidebar on the
      // left), border-left in Arabic (sidebar on the right). Set the physical
      // side explicitly — react-native-web does not reliably flip the logical
      // `borderEnd*` by `dir` on web.
      borderRightWidth={isRtl ? 0 : 1}
      borderLeftWidth={isRtl ? 1 : 0}
      borderRightColor="$borderSubtle"
      borderLeftColor="$borderSubtle"
      paddingHorizontal="$4"
      paddingVertical="$6"
      gap="$6"
    >
      {/* Brand — "Learnexia" stays Latin + LTR in every locale (SKILL.md). */}
      <Stack flexDirection={rowDir} alignItems="center" gap="$2" paddingHorizontal="$2">
        <Image source={assets.logoMark} style={{ width: 36, height: 36, resizeMode: 'contain' }} accessibilityElementsHidden />
        <Text color="$fg1" fontSize={18} fontWeight="800" fontFamily="$heading" writingDirection="ltr">
          {t('common.appName')}
        </Text>
      </Stack>

      {/* Child-selector card — opens inline dropdown to switch active child */}
      {activeChild ? (
        <Stack position="relative">
          <Stack
            testID="sidebar-child-selector"
            borderRadius={16}
            backgroundColor="$card"
            borderWidth={1}
            borderColor={dropdownOpen ? 'rgba(99,102,241,0.6)' : '$border'}
            padding={12}
            cursor="pointer"
            pressStyle={{ scale: 0.95 }}
            onPress={() => setDropdownOpen((v) => !v)}
            accessibilityRole="button"
            accessible
            accessibilityLabel={t('parent.childSelector.label')}
            aria-label={t('parent.childSelector.label')}
            aria-expanded={dropdownOpen}
          >
            <Stack flexDirection={rowDir} alignItems="center" gap="$3">
              <Avatar name={activeChild.fullName} size="sm" />
              <Stack flexDirection="column" flex={1}>
                <Text
                  color="$fg1"
                  fontSize={13}
                  fontWeight="700"
                  fontFamily="$heading"
                  writingDirection={direction}
                  textAlign={isRtl ? 'right' : 'left'}
                >
                  {activeChild.fullName}
                </Text>
                <Text
                  color="$fg3"
                  fontSize={11}
                  fontFamily="$body"
                  writingDirection={direction}
                  textAlign={isRtl ? 'right' : 'left'}
                >
                  {t('parent.childSelector.meta', {
                    grade: formatNumber(activeChild.grade, locale),
                    level: formatNumber(activeChild.level, locale),
                  })}
                </Text>
              </Stack>
              {/* Chevron — rotates 90° when open to point downward */}
              <Text
                color="$fg3"
                fontSize={13}
                accessibilityElementsHidden
                style={{ transform: [{ rotate: dropdownOpen ? (isRtl ? '-90deg' : '90deg') : '0deg' }] } as object}
              >
                {isRtl ? '‹' : '›'}
              </Text>
            </Stack>
          </Stack>

          {/* Inline dropdown — open state */}
          {dropdownOpen ? (
            <>
              {/* Backdrop — closes dropdown on outside click */}
              <Stack
                position="fixed"
                top={0}
                left={0}
                right={0}
                bottom={0}
                zIndex={99}
                onPress={() => setDropdownOpen(false)}
                style={{ cursor: 'default' }}
              />
              <Stack
                testID="sidebar-child-dropdown"
                position="absolute"
                top={74}
                left={0}
                right={0}
                zIndex={100}
                backgroundColor="$card"
                borderRadius={16}
                borderWidth={1}
                borderColor="rgba(255,255,255,0.1)"
                style={{ boxShadow: '0 20px 50px rgba(0,0,0,0.55)' }}
                overflow="hidden"
                flexDirection="column"
                padding={8}
                gap={2}
              >
                {/* "SWITCH CHILD" section header */}
                <Stack paddingHorizontal={14} paddingTop={4} paddingBottom={6}>
                  <Text
                    color="$fg3"
                    fontSize={10}
                    fontWeight="800"
                    fontFamily="$heading"
                    textTransform="uppercase"
                    letterSpacing={1.2}
                    writingDirection={direction}
                    textAlign={isRtl ? 'right' : 'left'}
                  >
                    {t('parent.childSelector.switchChild')}
                  </Text>
                </Stack>

                {children.length === 0 ? (
                  <Stack padding={14}>
                    <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
                      {t('parent.childSwitcher.noChildren')}
                    </Text>
                  </Stack>
                ) : (
                  children.map((child) => {
                    const childId = String(child.id);
                    const isActive = childId === (activeChildId ?? String(children[0]?.id));
                    const stats = getChildStatsStub(childId);
                    return (
                      <Stack
                        key={childId}
                        flexDirection={rowDir}
                        alignItems="center"
                        gap={10}
                        paddingVertical={8}
                        paddingHorizontal={10}
                        borderRadius={12}
                        backgroundColor={isActive ? '$primarySoft' : 'transparent'}
                        hoverStyle={{ backgroundColor: isActive ? '$primarySoft' : '$cardSoft' }}
                        cursor="pointer"
                        pressStyle={{ scale: 0.97 }}
                        onPress={() => handleSelectChild(childId)}
                        accessibilityRole="menuitem"
                        accessible
                        accessibilityState={{ selected: isActive }}
                        accessibilityLabel={child.fullName ?? ''}
                      >
                        <Avatar name={child.fullName ?? ''} size="sm" />
                        <Stack flexDirection="column" flex={1}>
                          <Text
                            color={isActive ? '$primaryLight' : '$fg1'}
                            fontSize={13}
                            fontWeight="700"
                            fontFamily="$heading"
                            writingDirection={direction}
                            textAlign={isRtl ? 'right' : 'left'}
                          >
                            {child.fullName ?? ''}
                          </Text>
                          <Text
                            color="$fg3"
                            fontSize={11}
                            fontFamily="$body"
                            writingDirection={direction}
                            textAlign={isRtl ? 'right' : 'left'}
                          >
                            {t('parent.childSelector.meta', {
                              grade: formatNumber(stats.grade, locale),
                              level: formatNumber(stats.level, locale),
                            })}
                          </Text>
                        </Stack>
                        {isActive ? (
                          <Text color="$primaryLight" fontSize={14} accessibilityElementsHidden>
                            {'✓'}
                          </Text>
                        ) : null}
                      </Stack>
                    );
                  })
                )}

                {/* Divider */}
                <Stack height={1} backgroundColor="$borderSubtle" marginHorizontal={10} marginVertical={4} />

                {/* Add a child footer — dashed circle "+" + label */}
                <Stack
                  flexDirection={rowDir}
                  alignItems="center"
                  gap={10}
                  paddingVertical={8}
                  paddingHorizontal={10}
                  borderRadius={12}
                  cursor="pointer"
                  hoverStyle={{ backgroundColor: '$cardSoft' }}
                  pressStyle={{ scale: 0.97 }}
                  onPress={() => {
                    setDropdownOpen(false);
                    openAddChild();
                  }}
                  accessibilityRole="button"
                  accessible
                  accessibilityLabel={t('parent.childSelector.addChild')}
                >
                  {/* Dashed-circle "+" icon — 32px matching spec */}
                  <Stack
                    width={32}
                    height={32}
                    borderRadius={16}
                    borderWidth={1.5}
                    alignItems="center"
                    justifyContent="center"
                    style={{ borderStyle: 'dashed', borderColor: 'rgba(165,180,252,0.5)' } as object}
                  >
                    <Text color="$primaryLight" fontSize={18} fontWeight="700" accessibilityElementsHidden>
                      {'+'}
                    </Text>
                  </Stack>
                  <Text
                    color="$primaryLight"
                    fontSize={13}
                    fontWeight="700"
                    fontFamily="$heading"
                    writingDirection={direction}
                    textAlign={isRtl ? 'right' : 'left'}
                    flex={1}
                  >
                    {t('parent.childSelector.addChild')}
                  </Text>
                </Stack>
              </Stack>
            </>
          ) : null}
        </Stack>
      ) : null}

      {/* Nav */}
      <Stack flexDirection="column" gap={2} accessibilityRole="menu" accessibilityLabel={t('parent.nav.sectionLabel')}>
        {NAV.map((item) => {
          const isActive = item.key === activeKey;
          const label = t(`parent.nav.${item.key}`);
          return (
            <Stack
              key={item.key}
              // Icon+label row: flexDirection follows locale so icon is always
              // on the leading side (right in AR, left in EN). Icons must NOT
              // be mirrored — only the row position flips.
              flexDirection={rowDir}
              alignItems="center"
              gap="$3"
              minHeight={40}
              paddingVertical={10}
              paddingHorizontal={12}
              hitSlop={{ top: 4, bottom: 4 }}
              borderRadius="$nav"
              backgroundColor={isActive ? '$primarySoft' : 'transparent'}
              hoverStyle={{ backgroundColor: isActive ? '$primarySoft' : '$cardSoft' }}
              cursor="pointer"
              pressStyle={{ scale: 0.95 }}
              onPress={() => router.push(item.route)}
              accessibilityRole="menuitem"
              accessible
              accessibilityState={{ selected: isActive }}
              accessibilityLabel={label}
              aria-label={label}
            >
              <Text fontSize={16} accessibilityElementsHidden>
                {item.icon}
              </Text>
              <Text
                flex={1}
                color={isActive ? '$primaryLight' : '$fg3'}
                fontSize={14}
                fontWeight={isActive ? '700' : '600'}
                fontFamily="$heading"
                writingDirection={direction}
                textAlign={isRtl ? 'right' : 'left'}
              >
                {label}
              </Text>
            </Stack>
          );
        })}
      </Stack>

      {/* Bottom controls — language pill + theme pill + XP widget + logout */}
      <Stack marginTop="auto" flexDirection="column" gap={8}>
        {/* Language segmented pill */}
        <Stack
          flexDirection={rowDir}
          borderRadius={12}
          padding={4}
          gap={4}
          style={{ backgroundColor: '#0B1220', border: '1px solid rgba(255,255,255,0.06)' } as object}
        >
          {(LOCALES as readonly Locale[]).map((loc) => {
            const isActive = loc === locale;
            const flag = loc === 'ar' ? '🇪🇬' : '🇺🇸';
            const label = loc === 'ar' ? 'AR' : 'EN';
            return (
              <Stack
                key={loc}
                flex={1}
                flexDirection="row"
                alignItems="center"
                justifyContent="center"
                gap={6}
                height={34}
                borderRadius={9}
                backgroundColor={isActive ? '#334155' : 'transparent'}
                style={isActive ? { boxShadow: '0 2px 6px rgba(0,0,0,0.3)' } as object : undefined}
                cursor="pointer"
                pressStyle={{ scale: 0.97 }}
                onPress={() => handleLocaleChange(loc)}
                accessibilityRole="button"
                accessible
                accessibilityState={{ selected: isActive }}
                accessibilityLabel={t(loc === 'ar' ? 'common.prefs.switchToArabic' : 'common.prefs.switchToEnglish')}
              >
                <Text fontSize={14} accessibilityElementsHidden>{flag}</Text>
                <Text
                  color={isActive ? '$fg1' : '$fg3'}
                  fontSize={13}
                  fontWeight={isActive ? '700' : '600'}
                  fontFamily="$heading"
                >
                  {label}
                </Text>
              </Stack>
            );
          })}
        </Stack>

        {/* Theme segmented pill */}
        <Stack
          flexDirection={rowDir}
          borderRadius={12}
          padding={4}
          gap={4}
          style={{ backgroundColor: '#0B1220', border: '1px solid rgba(255,255,255,0.06)' } as object}
        >
          {(['dark', 'light'] as const).map((th) => {
            const isActive = th === theme;
            const icon = th === 'dark' ? '🌙' : '☀️';
            const label = th === 'dark' ? t('common.prefs.themeDark') : t('common.prefs.themeLight');
            return (
              <Stack
                key={th}
                flex={1}
                flexDirection="row"
                alignItems="center"
                justifyContent="center"
                gap={6}
                height={34}
                borderRadius={9}
                backgroundColor={isActive ? '#334155' : 'transparent'}
                style={isActive ? { boxShadow: '0 2px 6px rgba(0,0,0,0.3)' } as object : undefined}
                cursor="pointer"
                pressStyle={{ scale: 0.97 }}
                onPress={() => setTheme(th)}
                accessibilityRole="button"
                accessible
                accessibilityState={{ selected: isActive }}
                accessibilityLabel={label}
              >
                <Text fontSize={14} accessibilityElementsHidden>{icon}</Text>
                <Text
                  color={isActive ? '$fg1' : '$fg3'}
                  fontSize={13}
                  fontWeight={isActive ? '700' : '600'}
                  fontFamily="$heading"
                >
                  {label}
                </Text>
              </Stack>
            );
          })}
        </Stack>

        {/* Weekly-XP summary widget */}
        <SidebarXpWidget direction={direction} isRtl={isRtl} />

        {/* Logout — danger-outlined button */}
        <Stack
          flexDirection={rowDir}
          alignItems="center"
          gap="$3"
          paddingVertical={10}
          paddingHorizontal={12}
          borderRadius={12}
          borderWidth={1}
          borderColor="rgba(239,68,68,0.25)"
          backgroundColor="transparent"
          hoverStyle={{ backgroundColor: 'rgba(239,68,68,0.06)' }}
          cursor={isSigningOut ? 'default' : 'pointer'}
          pressStyle={{ scale: 0.95 }}
          onPress={isSigningOut ? undefined : signOut}
          accessibilityRole="button"
          accessible
          accessibilityLabel={t('parent.nav.logout')}
          aria-label={t('parent.nav.logout')}
          opacity={isSigningOut ? 0.6 : 1}
        >
          <Text fontSize={16} color="#F87171" accessibilityElementsHidden>{'↩'}</Text>
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
    </Stack>
  );
}

Sidebar.displayName = 'ParentSidebar';

/**
 * SidebarXpWidget — the bottom "THIS WEEK +XP" gamification card (capture
 * `web/04-my-children.png`). TODO(P5): the real weekly-XP delta is server
 * analytics; rendered with a static stub until the Phase-5 reports endpoint
 * lands. Token-only; the eyebrow uses the `$xp` gamification color.
 */
function SidebarXpWidget({ direction, isRtl }: { direction: Direction; isRtl: boolean }) {
  const { t } = useTranslation();

  // TODO(P5): replace these static placeholders with the real weekly delta.
  const STUB_XP = 340;
  const STUB_DELTA_PERCENT = 28;

  return (
    <Stack
      borderRadius={14}
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$borderSubtle"
      padding={14}
      gap="$1"
      accessible
      accessibilityLabel={`${t('parent.xpWidget.eyebrow')} ${t('parent.xpWidget.value', { xp: STUB_XP })} ${t('parent.xpWidget.delta', { percent: STUB_DELTA_PERCENT })}`}
    >
      <Text
        color="$xp"
        fontSize={11}
        fontWeight="800"
        fontFamily="$heading"
        textTransform="uppercase"
        letterSpacing={0.8}
        writingDirection={direction}
        textAlign={isRtl ? 'right' : 'left'}
      >
        {t('parent.xpWidget.eyebrow')}
      </Text>
      <Text
        color="$fg1"
        fontSize={20}
        fontWeight="800"
        fontFamily="$heading"
        writingDirection={direction}
        textAlign={isRtl ? 'right' : 'left'}
      >
        {t('parent.xpWidget.value', { xp: STUB_XP })}
      </Text>
      <Text
        color="$fg3"
        fontSize={11}
        fontFamily="$body"
        writingDirection={direction}
        textAlign={isRtl ? 'right' : 'left'}
      >
        {t('parent.xpWidget.delta', { percent: STUB_DELTA_PERCENT })}
      </Text>
    </Stack>
  );
}

SidebarXpWidget.displayName = 'SidebarXpWidget';
