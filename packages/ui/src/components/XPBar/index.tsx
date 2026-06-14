/**
 * XPBar — animated XP progress with level + counter.
 *
 * Design Spec §4.3. Authoritative fill gradient is `gradXp` (green → indigo);
 * the yellow `$xp` is the COUNTER label color only (Design Gap 3). At 100% the
 * fill switches to the gold shimmer (`gradXpFull`) and the track gains an XP glow.
 *
 * Motion: the fill width animates via Moti when available (timing, durSlow);
 * otherwise the fill renders statically at the target ratio (no silent broken
 * state). Gradient is rendered with expo-linear-gradient when available, else a
 * flat token color fallback.
 *
 * RTL: the fill grows from the end toward the start; header labels swap sides.
 * A11y: non-interactive `progressbar` with `accessibilityValue`.
 */
import { gradientStops, durations, colors } from '@learnexia/design-system';
import { directionForLocale } from '@learnexia/shared/i18n';
import { Stack } from '@tamagui/core';
import React from 'react';

import type { StackProps } from '@tamagui/core';

import { GradientFill } from '../../internal/GradientFill';
import { tryLoadMoti } from '../../internal/moti';
import { XStack, YStack, Text } from '../../internal/primitives';

export interface XPBarProps {
  currentXp: number;
  totalXp: number;
  level: number;
  /** Locale for RTL + label direction. Defaults to 'en'. */
  locale?: string;
  /** Animate the fill on mount/value change. Default false (render at target). */
  animated?: boolean;
  /** Required for screen readers, e.g. "820 of 1000 XP, Level 7". */
  accessibilityLabel: string;
}

const TRACK_HEIGHT = 14;

export function XPBar({
  currentXp,
  totalXp,
  level,
  locale = 'en',
  animated = false,
  accessibilityLabel,
}: XPBarProps) {
  const ratio = Math.max(0, Math.min(1, totalXp > 0 ? currentXp / totalXp : 0));
  const isFull = ratio >= 1;
  const direction = directionForLocale(locale);
  const isRtl = direction === 'rtl';

  const widthPct = `${ratio * 100}%`;
  const stops = isFull ? gradientStops.gradXpFull : gradientStops.gradXp;

  const moti = tryLoadMoti();
  const MotiView = moti?.MotiView as React.ComponentType<Record<string, unknown>> | undefined;

  const fillInner = (
    <GradientFill
      stops={stops.colors}
      angle={isRtl ? 270 : 90}
      borderRadius={9999}
      height="100%"
      width="100%"
    />
  );

  const fill =
    animated && MotiView ? (
      <MotiView
        from={{ width: '0%' }}
        animate={{ width: widthPct }}
        transition={{ type: 'timing', duration: durations.slow }}
        style={{ height: '100%', borderRadius: 9999, overflow: 'hidden' }}
      >
        {fillInner}
      </MotiView>
    ) : (
      <Stack
        width={widthPct as StackProps['width']}
        height="100%"
        borderRadius={9999}
        overflow="hidden"
      >
        {fillInner}
      </Stack>
    );

  return (
    <YStack gap="$2" width="100%">
      <XStack
        justifyContent="space-between"
        flexDirection={isRtl ? 'row-reverse' : 'row'}
      >
        <Text fontSize={14} color="$fg2" fontFamily="$body">
          {isFull ? 'Level Up!' : `Level ${level} → ${level + 1}`}
        </Text>
        <Text
          fontSize={14}
          color="$xp"
          fontWeight="700"
          fontFamily="$heading"
          // tabular numerals for stable digit width
          style={{ fontVariant: ['tabular-nums'] }}
        >
          {`${currentXp} / ${totalXp} XP`}
        </Text>
      </XStack>

      <Stack
        height={TRACK_HEIGHT}
        borderRadius={9999}
        backgroundColor="$bg"
        borderWidth={1}
        borderColor="$borderSubtle"
        overflow="hidden"
        // glow on level-up (web box-shadow; native uses shadow props)
        shadowColor={colors.xp}
        shadowOpacity={isFull ? 0.45 : 0}
        shadowRadius={isFull ? 24 : 0}
        // fill aligns to end in RTL so it grows from the right
        flexDirection={isRtl ? 'row-reverse' : 'row'}
        accessibilityRole="progressbar"
        accessible
        accessibilityLabel={accessibilityLabel}
        aria-label={accessibilityLabel}
        accessibilityValue={{ min: 0, max: totalXp, now: currentXp }}
      >
        {fill}
      </Stack>
    </YStack>
  );
}

XPBar.displayName = 'XPBar';
