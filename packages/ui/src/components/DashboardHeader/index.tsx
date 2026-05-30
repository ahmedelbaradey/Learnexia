/**
 * DashboardHeader — W13 P2-09-FE
 *
 * Top-of-screen greeting block + stats strip.
 *
 * Design Spec §3.1 (W13-home-dashboard.md):
 *   - Mascot tile (48×48) on the logical leading edge.
 *   - Greeting H1 + grade caption column.
 *   - Stats strip: Hearts | StreakFlame | XPBar (flex 1).
 *   - Skeleton (`loading={true}`) renders shimmer bars in place of live content.
 *   - Mount-only 240ms fade-in; honours `prefers-reduced-motion` (instant).
 *   - Not interactive — no tap targets on this primitive.
 *
 * Token-only: no raw hex, no physical left/right margins.
 * RTL: all row stacks flip via `flexDirection={isRtl ? 'row-reverse' : 'row'}`.
 * A11y: greeting H1 carries `accessibilityRole="header"`; stats strip wrapper
 * carries `accessibilityRole="group"` + caller-composed `statsAccessibilityLabel`.
 */

import React from 'react';
import { Image, type ImageSourcePropType } from 'react-native';
import { Stack } from '@tamagui/core';

import { Hearts } from '../Hearts';
import { StreakFlame } from '../StreakFlame';
import { XPBar } from '../XPBar';
import { XStack, YStack, Text } from '../../internal/primitives';

export interface DashboardHeaderProps {
  /** Optional first-name token (informational only; greetingText is the rendered string). */
  childName?: string;
  /** Greeting i18n string already resolved by caller (e.g. "Hi, Sami!" / "مرحبا سامي!"). */
  greetingText: string;
  /** Grade caption already resolved (e.g. "Grade 3" / "الصف ٣"), or null/undefined to omit. */
  gradeCaption?: string | null;
  /** Hearts widget current count. Phase 2 = always 3. */
  hearts: number;
  /** Hearts max — default 3 (matches W12 lesson intro precedent). */
  heartsMax?: number;
  /** Streak days. Phase 2 = always 0. TODO P4-03 */
  streakDays: number;
  /** Weekly XP earned. Phase 2 = always 0. TODO P4-02 */
  weeklyXp: number;
  /** Weekly XP target. Phase 2 = hard-coded 100. TODO P4-02 */
  weeklyXpTarget: number;
  /** XPBar level — Phase 2 = hard-coded 1. TODO P4-02 */
  weeklyLevel?: number;
  /** Optional mascot/logo asset on the logical leading edge. Caller passes assets.logoMark. */
  mascotSrc?: ImageSourcePropType;
  /** Pre-composed stats a11y label, e.g. "3 hearts, 0 day streak, 0 XP". */
  statsAccessibilityLabel: string;
  /** Hearts a11y label. */
  heartsAccessibilityLabel: string;
  /** Streak a11y label. */
  streakAccessibilityLabel: string;
  /** XP bar a11y label. */
  xpAccessibilityLabel: string;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** When true, renders shimmer skeleton instead of live content. */
  loading?: boolean;
  testID?: string;
}

/** Shimmer-bar placeholder for skeleton rendering. */
function ShimmerBar({ width, height }: { width: number | string; height: number }) {
  return (
    <Stack
      width={width as never}
      height={height}
      borderRadius={9999}
      backgroundColor="$cardSoft"
      opacity={0.7}
    />
  );
}

export function DashboardHeader({
  greetingText,
  gradeCaption,
  hearts,
  heartsMax = 3,
  streakDays,
  weeklyXp,
  weeklyXpTarget,
  weeklyLevel = 1,
  mascotSrc,
  statsAccessibilityLabel,
  heartsAccessibilityLabel,
  streakAccessibilityLabel,
  xpAccessibilityLabel,
  direction = 'ltr',
  locale = 'en',
  loading = false,
  testID,
}: DashboardHeaderProps) {
  const isRtl = direction === 'rtl';
  const rowDir = isRtl ? 'row-reverse' : 'row';

  if (loading) {
    return (
      <YStack gap={16} testID={testID}>
        {/* Greeting row skeleton */}
        <XStack
          flexDirection={rowDir}
          alignItems="center"
          gap={12}
        >
          {/* Mascot tile skeleton */}
          <Stack
            width={48}
            height={48}
            borderRadius="$button"
            backgroundColor="$cardSoft"
            opacity={0.7}
          />
          {/* Greeting text skeleton */}
          <YStack gap={6} flex={1}>
            <ShimmerBar width="60%" height={24} />
          </YStack>
        </XStack>

        {/* Stats strip skeleton — 3 pill chips */}
        <XStack flexDirection={rowDir} gap={10} alignItems="center">
          <ShimmerBar width={72} height={32} />
          <ShimmerBar width={72} height={32} />
          <ShimmerBar width={72} height={32} />
        </XStack>
      </YStack>
    );
  }

  return (
    <YStack gap={16} testID={testID}>
      {/* Greeting row */}
      <XStack
        flexDirection={rowDir}
        alignItems="center"
        gap={12}
      >
        {/* Mascot tile — decorative; hidden from screen readers */}
        <Stack
          width={48}
          height={48}
          borderRadius="$button"
          backgroundColor="$primarySoft"
          alignItems="center"
          justifyContent="center"
          accessibilityElementsHidden
          importantForAccessibility="no-hide-descendants"
        >
          {mascotSrc ? (
            <Image
              source={mascotSrc}
              style={{ width: 32, height: 32, resizeMode: 'contain' }}
              accessibilityElementsHidden
            />
          ) : (
            /* Fallback: owl emoji (semantic placeholder per design spec §3.1) */
            <Text fontSize={28} accessibilityElementsHidden>
              {'🦉'}
            </Text>
          )}
        </Stack>

        {/* Greeting column */}
        <YStack flex={1} gap={2}>
          <Text
            color="$fg1"
            fontSize={24}
            fontWeight="800"
            fontFamily="$heading"
            lineHeight={29}
            letterSpacing={-0.24}
            writingDirection={direction}
            numberOfLines={1}
            accessibilityRole="header"
          >
            {greetingText}
          </Text>
          {gradeCaption ? (
            <Text
              color="$fg3"
              fontSize={13}
              fontWeight="500"
              fontFamily="$body"
              writingDirection={direction}
              marginTop={2}
            >
              {gradeCaption}
            </Text>
          ) : null}
        </YStack>
      </XStack>

      {/* Stats strip */}
      <XStack
        flexDirection={rowDir}
        alignItems="center"
        gap={10}
        justifyContent="flex-start"
        accessibilityLabel={statsAccessibilityLabel}
        accessibilityRole="summary"
        accessible
        aria-label={statsAccessibilityLabel}
      >
        <Hearts
          current={hearts}
          maxHearts={heartsMax}
          locale={locale}
          accessibilityLabel={heartsAccessibilityLabel}
        />
        <StreakFlame
          days={streakDays}
          size="sm"
          locale={locale}
          accessibilityLabel={streakAccessibilityLabel}
        />
        <XStack flex={1}>
          <XPBar
            currentXp={weeklyXp}
            totalXp={weeklyXpTarget}
            level={weeklyLevel}
            locale={locale}
            animated={false}
            accessibilityLabel={xpAccessibilityLabel}
          />
        </XStack>
      </XStack>
    </YStack>
  );
}

DashboardHeader.displayName = 'DashboardHeader';
