/**
 * Parent dashboard left sidebar (web ≥768) — Design Spec / capture
 * `web/04-my-children.png`. Composes the brand mark, a child-selector card
 * (active child avatar + name + "Grade N · Level L"), and the parent nav with an
 * active-state pill. Built from existing primitives (Stack/Text/Avatar) — no new
 * design pattern.
 *
 * Routing: only "My Children" is implemented (P1-11); the other destinations are
 * Phase-2+/Phase-5 surfaces and render as non-active items that route to the
 * children screen for now (TODO: wire to their routes as they ship).
 *
 * RTL: the whole column mirrors via logical flips; the active pill uses the
 * logical START border. i18n: every label is a translation key.
 */
import { Avatar } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import React from 'react';
import { Image } from 'react-native';
import { useTranslation } from 'react-i18next';

import { assets } from '../../../src/assets';
import { useLocale } from '../../../src/hooks/useLocale';

/** Fixed parent nav destinations (enum-style const, never raw literals). */
const NAV_ITEM = {
  MyChildren: 'myChildren',
  Overview: 'overview',
  Reports: 'reports',
  Activity: 'activity',
  Subjects: 'subjects',
  Settings: 'settings',
} as const;

type NavItemKey = (typeof NAV_ITEM)[keyof typeof NAV_ITEM];

interface NavDef {
  key: NavItemKey;
  icon: string;
}

const NAV: readonly NavDef[] = [
  { key: NAV_ITEM.MyChildren, icon: '👧' },
  { key: NAV_ITEM.Overview, icon: '📊' },
  { key: NAV_ITEM.Reports, icon: '📝' },
  { key: NAV_ITEM.Activity, icon: '🎯' },
  { key: NAV_ITEM.Subjects, icon: '📚' },
  { key: NAV_ITEM.Settings, icon: '⚙️' },
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
}

export function Sidebar({ activeChild }: SidebarProps) {
  const { t } = useTranslation();
  const { direction, isRtl } = useLocale();
  const router = useRouter();
  const rowDir = isRtl ? 'row-reverse' : 'row';

  // Only My Children is routed in P1-11; the other destinations are Phase-2+/
  // Phase-5 surfaces, so My Children is the active item for now. When the sibling
  // routes (overview/reports/…) ship, derive this from the current route.
  const activeKey: NavItemKey = NAV_ITEM.MyChildren;

  return (
    <Stack
      flexDirection="column"
      width={240}
      height="100%"
      backgroundColor="$bgElevated"
      borderEndWidth={1}
      borderEndColor="$border"
      paddingHorizontal="$4"
      paddingVertical="$6"
      gap="$6"
    >
      {/* Brand */}
      <Stack flexDirection={rowDir} alignItems="center" gap="$2" paddingHorizontal="$2">
        <Image source={assets.logoMark} style={{ width: 32, height: 32, resizeMode: 'contain' }} accessibilityElementsHidden />
        <Text color="$fg1" fontSize={20} fontWeight="800" fontFamily="$heading">
          {t('common.appName')}
        </Text>
      </Stack>

      {/* Child-selector card */}
      {activeChild ? (
        <Stack
          borderRadius="$card"
          backgroundColor="$card"
          borderWidth={1}
          borderColor="$borderStrong"
          padding="$3"
          cursor="pointer"
          pressStyle={{ scale: 0.98 }}
          onPress={() => router.push('/(parent)/children')}
          accessibilityRole="button"
          accessible
          accessibilityLabel={t('parent.childSelector.label')}
          aria-label={t('parent.childSelector.label')}
        >
          <Stack flexDirection={rowDir} alignItems="center" gap="$3">
            <Avatar name={activeChild.fullName} size="sm" />
            <Stack flexDirection="column" flex={1}>
              <Text color="$fg1" fontSize={15} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
                {activeChild.fullName}
              </Text>
              <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
                {t('parent.childSelector.meta', { grade: activeChild.grade, level: activeChild.level })}
              </Text>
            </Stack>
            <Text color="$fg3" fontSize={16} accessibilityElementsHidden scaleX={isRtl ? -1 : 1}>
              {'›'}
            </Text>
          </Stack>
        </Stack>
      ) : null}

      {/* Nav */}
      <Stack flexDirection="column" gap="$1" accessibilityRole="menu" accessibilityLabel={t('parent.nav.sectionLabel')}>
        {NAV.map((item) => {
          const isActive = item.key === activeKey;
          const label = t(`parent.nav.${item.key}`);
          return (
            <Stack
              key={item.key}
              flexDirection={rowDir}
              alignItems="center"
              gap="$3"
              minHeight={48}
              paddingHorizontal="$3"
              borderRadius="$button"
              backgroundColor={isActive ? '$primarySoft' : 'transparent'}
              borderStartWidth={isActive ? 3 : 0}
              borderStartColor={isActive ? '$primary' : 'transparent'}
              hoverStyle={{ backgroundColor: isActive ? '$primarySoft' : '$card' }}
              cursor="pointer"
              pressStyle={{ scale: 0.98 }}
              onPress={() => router.push('/(parent)/children')}
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
                color={isActive ? '$fg1' : '$fg2'}
                fontSize={15}
                fontWeight={isActive ? '700' : '500'}
                fontFamily="$heading"
                writingDirection={direction}
              >
                {label}
              </Text>
            </Stack>
          );
        })}
      </Stack>
    </Stack>
  );
}

Sidebar.displayName = 'ParentSidebar';
