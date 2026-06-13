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
 * RTL: the whole column mirrors via the document `dir="rtl"` single flip; all
 * icon+label rows use `flexDirection="row"` — the document dir reverses item order
 * once, placing the icon on the leading side (right in AR, left in EN) without
 * a second flip. The label's `textAlign` follows the locale. The brand wordmark
 * keeps `writingDirection="ltr"` per SKILL.md. i18n: every label is a translation
 * key.
 *
 * Child-selector: opens an inline dropdown that lets the parent switch the active
 * child; wired to `useActiveChildStore` (same store as the header ChildSwitcher).
 *
 * Logout: bottom of sidebar; calls `useSignOutAction` which clears the local
 * session and redirects to /(auth)/login. Styled as a nav-item-like row.
 */
import { useLocalizedRouter } from '@/hooks/useLocalizedRouter';
import { useMyChildren } from '@learnexia/api-client';
import { type Direction } from '@learnexia/shared';
import { Avatar } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React, { useState } from 'react';
import { Image } from 'react-native';
import { useTranslation } from 'react-i18next';

import { assets } from '@/assets';
import { useLocale } from '@/hooks/useLocale';
import { useSignOutAction } from '@/hooks/useSignOutAction';
import { useActiveChildStore } from '@/providers/activeChildStore';
import { formatNumber } from './ChildSwitcher';

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
  const router = useLocalizedRouter();
  // No rowDir — the document `dir="rtl"` already flips `flexDirection="row"` once.
  // Using row-reverse here would double-flip (items land back in LTR order in AR).

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
      <Stack flexDirection="row" alignItems="center" gap="$2" paddingHorizontal="$2">
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
            borderRadius="$cardInner"
            backgroundColor="$card"
            borderWidth={1}
            borderColor="$border"
            padding={10}
            cursor="pointer"
            pressStyle={{ scale: 0.95 }}
            onPress={() => setDropdownOpen((v) => !v)}
            accessibilityRole="button"
            accessible
            accessibilityLabel={t('parent.childSelector.label')}
            aria-label={t('parent.childSelector.label')}
            aria-expanded={dropdownOpen}
          >
            <Stack flexDirection="row" alignItems="center" gap="$3">
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
              <Text color="$fg3" fontSize={16} accessibilityElementsHidden>
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
                top={50}
                // Align dropdown to sidebar start: left in LTR, right in RTL
                {...(isRtl ? { right: 0 } : { left: 0 })}
                zIndex={100}
                minWidth={220}
                backgroundColor="$card"
                borderRadius="$card"
                borderWidth={1}
                borderColor="$border"
                style={{ boxShadow: '0 8px 24px rgba(0,0,0,0.25)' }}
                overflow="hidden"
                flexDirection="column"
              >
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
                    return (
                      <Stack
                        key={childId}
                        flexDirection="row"
                        alignItems="center"
                        gap="$3"
                        paddingVertical={10}
                        paddingHorizontal={12}
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
                        </Stack>
                        {isActive ? (
                          <Text color="$primary" fontSize={14} accessibilityElementsHidden>
                            {'✓'}
                          </Text>
                        ) : null}
                      </Stack>
                    );
                  })
                )}

                {/* Divider */}
                <Stack height={1} backgroundColor="$borderSubtle" marginHorizontal={8} />

                {/* + Add child footer */}
                <Stack
                  flexDirection="row"
                  alignItems="center"
                  gap="$2"
                  padding={12}
                  cursor="pointer"
                  hoverStyle={{ backgroundColor: '$cardSoft' }}
                  pressStyle={{ scale: 0.97 }}
                  onPress={() => {
                    setDropdownOpen(false);
                    openAddChild();
                  }}
                  accessibilityRole="button"
                  accessible
                  accessibilityLabel={t('parent.childSwitcher.addChild')}
                >
                  <Text
                    color="$primaryLight"
                    fontSize={13}
                    fontWeight="700"
                    fontFamily="$heading"
                    writingDirection={direction}
                    textAlign={isRtl ? 'right' : 'left'}
                  >
                    {t('parent.childSwitcher.addChild')}
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
              // Icon+label row: flexDirection="row" + document dir="rtl" flips once
              // so icon sits on the leading side (right in AR, left in EN). Icons
              // must NOT be mirrored — only the row position flips via dir.
              flexDirection="row"
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

      {/* Weekly-XP summary widget — pinned to the bottom (capture). */}
      <SidebarXpWidget direction={direction} isRtl={isRtl} />

      {/* Logout row — below XP widget; calls sign-out + redirects to login. */}
      <Stack
        flexDirection="row"
        alignItems="center"
        gap="$3"
        minHeight={40}
        paddingVertical={10}
        paddingHorizontal={12}
        hitSlop={{ top: 4, bottom: 4 }}
        borderRadius="$nav"
        backgroundColor="transparent"
        hoverStyle={{ backgroundColor: '$cardSoft' }}
        cursor={isSigningOut ? 'default' : 'pointer'}
        pressStyle={{ scale: 0.95 }}
        onPress={isSigningOut ? undefined : signOut}
        accessibilityRole="button"
        accessible
        accessibilityLabel={t('parent.nav.logout')}
        aria-label={t('parent.nav.logout')}
        opacity={isSigningOut ? 0.6 : 1}
      >
        <Text fontSize={16} accessibilityElementsHidden>
          {'🚪'}
        </Text>
        <Text
          flex={1}
          color="$fg3"
          fontSize={14}
          fontWeight="600"
          fontFamily="$heading"
          writingDirection={direction}
          textAlign={isRtl ? 'right' : 'left'}
        >
          {t('parent.nav.logout')}
        </Text>
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
      marginTop="auto"
      borderRadius="$button"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$border"
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
