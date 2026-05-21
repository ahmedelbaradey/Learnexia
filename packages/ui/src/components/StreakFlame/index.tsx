/**
 * StreakFlame — animated flame + day counter.
 *
 * Design Spec §4.5. Continuous pulse on the flame (scale 1 → 1.08, opacity
 * 1 → 0.85) over durSlow via Moti loop. Streak drop-shadow uses streakGlow.
 *
 * RTL: row reverses so flame sits on the start (right) side, counter on the end.
 * A11y: non-interactive `text` container with a descriptive label.
 */
import { durations, colors } from '@learnexia/design-system';
import { directionForLocale } from '@learnexia/shared/i18n';
import React from 'react';

import { tryLoadMoti } from '../../internal/moti';
import { XStack, YStack, Text } from '../../internal/primitives';

export interface StreakFlameProps {
  days: number;
  label?: string;
  size?: 'sm' | 'md';
  locale?: string;
  /** Required for screen readers, e.g. "7 day streak". */
  accessibilityLabel: string;
}

const SIZES = {
  sm: { flame: 24, counter: 18, showMeta: false },
  md: { flame: 48, counter: 36, showMeta: true },
} as const;

export function StreakFlame({
  days,
  label = 'Keep it alive!',
  size = 'md',
  locale = 'en',
  accessibilityLabel,
}: StreakFlameProps) {
  const isRtl = directionForLocale(locale) === 'rtl';
  const dims = SIZES[size];

  const moti = tryLoadMoti();
  const MotiView = moti?.MotiView as React.ComponentType<Record<string, unknown>> | undefined;

  const flameGlyph = (
    <Text
      fontSize={dims.flame}
      // streak glow — web drop-shadow via shadow props on container
      style={{ textShadowColor: colors.streakGlow, textShadowRadius: 24 }}
      accessibilityElementsHidden
    >
      {'🔥'}
    </Text>
  );

  const flame = MotiView ? (
    <MotiView
      from={{ scale: 1, opacity: 1 }}
      animate={{ scale: 1.08, opacity: 0.85 }}
      transition={{ type: 'timing', duration: durations.slow, loop: true, repeatReverse: true }}
    >
      {flameGlyph}
    </MotiView>
  ) : (
    flameGlyph
  );

  return (
    <XStack
      gap="$4"
      alignItems="center"
      flexDirection={isRtl ? 'row-reverse' : 'row'}
      accessibilityRole="text"
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {flame}
      <YStack alignItems={isRtl ? 'flex-end' : 'flex-start'}>
        <Text
          color="$streak"
          fontSize={dims.counter}
          fontWeight="800"
          fontFamily="$heading"
          style={{ fontVariant: ['tabular-nums'] }}
        >
          {days}
        </Text>
        {dims.showMeta ? (
          <Text color="$fg3" fontSize={12} fontFamily="$body">
            {label}
          </Text>
        ) : null}
      </YStack>
    </XStack>
  );
}

StreakFlame.displayName = 'StreakFlame';
