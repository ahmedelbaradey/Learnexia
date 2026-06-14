/**
 * MissionBanner — W13 P2-09-FE (built, NOT rendered in W13)
 *
 * "Today's mission" tile. Phase 2 always returns `dailyMission: null` from the
 * BE, so this primitive is NEVER mounted in W13. P4-06 turns it on by removing
 * the screen-level null-guard and passing real `DailyMissionDto` data.
 *
 * Design Spec §3.3 (W13-home-dashboard.md):
 *   - 🎯 glyph disc on the logical leading edge.
 *   - Body column: eyebrow + title + optional progress bar.
 *   - Trailing: XP reward badge (when rewardXp set > 0) OR "Start Mission" CTA
 *     (when onPress set).
 *   - Hover lift 160ms / press scale 80ms (parity with ContinueCard).
 *   - RTL: row flip; progress bar wrapped in dir=ltr (brand law).
 *
 * Token-only: no raw hex. No physical left/right.
 */

import React from 'react';
import { Pressable } from 'react-native';
import { Stack } from '@tamagui/core';

import { Badge } from '../Badge';
import { XStack, YStack, Text } from '../../internal/primitives';

export interface MissionBannerProps {
  /** Localized mission title, e.g. "Solve 10 math problems". */
  title: string;
  /** Reward XP from DailyMissionDto.rewardXp. Badge hidden when 0 or undefined. */
  rewardXp?: number;
  /** Progress from DailyMissionDto (current already Eastern-numeral formatted by caller for AR). */
  progress?: { current: number; total: number };
  /** Tap handler — caller routes per Phase-4 contract. Omit to render non-interactive. */
  onPress?: () => void;
  /** Pre-localized a11y label. */
  accessibilityLabel: string;
  /** Mission section eyebrow copy ("Today's mission" / "مهمة اليوم"). */
  eyebrowText: string;
  /** "Start Mission" CTA label — shown when onPress set AND rewardXp not shown. */
  startCtaLabel?: string;
  /** XP reward badge label prefix ("+{n} XP" / "+{n} نقطة"). */
  xpBadgeLabel?: string;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}

export function MissionBanner({
  title,
  rewardXp,
  progress,
  onPress,
  accessibilityLabel,
  eyebrowText,
  startCtaLabel,
  xpBadgeLabel,
  direction = 'ltr',
  locale = 'en',
  testID,
}: MissionBannerProps) {
  const isRtl = direction === 'rtl';
  const rowDir = isRtl ? 'row-reverse' : 'row';
  const showXpBadge = !!rewardXp && rewardXp > 0;
  const showStartCta = !!onPress && !showXpBadge;

  const inner = (
    <Stack
      padding={16}
      paddingHorizontal={18}
      borderRadius="$card"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$borderSubtle"
      // Shadow resolved to raw rgba — $shadow-soft (design spec §4.4)
      shadowColor="rgba(0,0,0,0.15)"
      shadowOffset={{ width: 0, height: 4 }}
      shadowOpacity={1}
      shadowRadius={12}
      minHeight={80}
      testID={testID}
    >
      <XStack flexDirection={rowDir} alignItems="center" gap={12}>
        {/* 🎯 glyph disc — logical leading edge */}
        <Stack
          width={44}
          height={44}
          borderRadius="$button"
          backgroundColor="$warningSoft"
          alignItems="center"
          justifyContent="center"
          flexShrink={0}
          accessibilityElementsHidden
          importantForAccessibility="no-hide-descendants"
        >
          <Text fontSize={24} accessibilityElementsHidden>{'🎯'}</Text>
        </Stack>

        {/* Body column */}
        <YStack flex={1} gap={4}>
          {/* Eyebrow */}
          <Text
            fontSize={11}
            fontWeight="700"
            textTransform="uppercase"
            letterSpacing={0.44}
            color="$accent"
            fontFamily="$heading"
            writingDirection={direction}
            numberOfLines={1}
          >
            {eyebrowText}
          </Text>

          {/* Mission title */}
          <Text
            fontSize={16}
            fontWeight="800"
            fontFamily="$heading"
            color="$fg1"
            lineHeight={19}
            writingDirection={direction}
            numberOfLines={2}
          >
            {title}
          </Text>

          {/* Progress bar — wrapped in dir=ltr per brand law (progress reads L→R) */}
          {progress ? (
            <XStack
              flexDirection={rowDir}
              alignItems="center"
              gap={8}
              marginTop={4}
            >
              {/* Fill bar */}
              <Stack
                flex={1}
                height={6}
                borderRadius={9999}
                backgroundColor="$bg"
                overflow="hidden"
              >
                <Stack
                  // Always LTR for brand law
                  style={{ direction: 'ltr' } as never}
                  width={`${Math.min(100, (progress.current / Math.max(1, progress.total)) * 100)}%` as never}
                  height="100%"
                  borderRadius={9999}
                  backgroundColor="$accent"
                />
              </Stack>
              {/* Progress caption */}
              <Text
                fontSize={12}
                fontWeight="700"
                fontFamily="$heading"
                color="$fg2"
                style={{ fontVariant: ['tabular-nums'] }}
              >
                {`${progress.current} / ${progress.total}`}
              </Text>
            </XStack>
          ) : null}
        </YStack>

        {/* Trailing: XP badge OR Start CTA */}
        {showXpBadge ? (
          <Badge
            variant="gold"
            label={xpBadgeLabel ?? `+${rewardXp} XP`}
            accessibilityLabel={xpBadgeLabel ?? `+${rewardXp} XP`}
          />
        ) : showStartCta ? (
          <Stack
            paddingHorizontal={14}
            paddingVertical={8}
            borderRadius="$pill"
            backgroundColor="$primary"
            justifyContent="center"
            alignItems="center"
            minHeight={36}
          >
            <Text
              fontSize={13}
              fontWeight="700"
              fontFamily="$heading"
              color="$fg1"
              writingDirection={direction}
            >
              {startCtaLabel ?? (locale === 'ar' ? 'ابدأ المهمة' : 'Start Mission')}
            </Text>
          </Stack>
        ) : null}
      </XStack>
    </Stack>
  );

  if (onPress) {
    return (
      <Pressable
        onPress={onPress}
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel}
        aria-label={accessibilityLabel}
      >
        {({ pressed }: { pressed: boolean }) => (
          <Stack style={{ transform: [{ scale: pressed ? 0.95 : 1 }] }}>
            {inner}
          </Stack>
        )}
      </Pressable>
    );
  }

  return (
    <Stack
      accessibilityRole="text"
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {inner}
    </Stack>
  );
}

MissionBanner.displayName = 'MissionBanner';
