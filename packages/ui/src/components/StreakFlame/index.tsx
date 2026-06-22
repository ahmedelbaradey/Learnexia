/**
 * StreakFlame — animated flame + day counter.
 *
 * Design Spec P4-08 §6 / §4.5. Size-conditional pulse:
 *
 *   size="sm" (HUD chip in DashboardHeader, spec §6.4):
 *     scale 1 → 1.02 → 1 loop, 2000ms full cycle (slow/subtle).
 *     Glow: rgba(251,146,60,0.45), radius 8.
 *
 *   size="md" (hero flame slot — streak.tsx has its own MotiView):
 *     scale 1 → 1.08, opacity 0.85, 400ms (durations.slow) loop.
 *     Glow: colors.streakGlow, radius 24.
 *
 * Reduce-motion (useReduceMotion): static glyph with glow, no loop.
 *
 * RTL: row reverses so flame sits on the start (right) side, counter on the end.
 * A11y: non-interactive `text` container with a descriptive label.
 */
import { durations, colors } from '@learnexia/design-system';
import { directionForLocale } from '@learnexia/shared/i18n';
import React from 'react';

import { tryLoadMoti } from '../../internal/moti';
import { useReduceMotion } from '../../hooks/useReduceMotion';
import { XStack, YStack, Text } from '../../internal/primitives';

export interface StreakFlameProps {
  days: number;
  label?: string;
  size?: 'sm' | 'md';
  locale?: string;
  /** Required for screen readers, e.g. "7 day streak". */
  accessibilityLabel: string;
  /**
   * Stable test identifier — present in both animated and reduce-motion paths
   * so E2E can assert static vs animated state.
   * @default 'streak-flame'
   */
  testID?: string;
}

const SIZES = {
  sm: { flame: 24, counter: 18, showMeta: false },
  md: { flame: 48, counter: 36, showMeta: true },
} as const;

/** HUD chip (sm): subtle 2s pulse — scale 1→1.02, total 2000ms cycle. */
const HUD_SCALE_TARGET = 1.02;
const HUD_LOOP_MS = 1000; // half-cycle; repeatReverse → 2s full loop

/** Hero (md): bolder 400ms (durations.slow) pulse — scale 1→1.08. */
const HERO_SCALE_TARGET = 1.08;

/** HUD chip glow (spec §6.4 literal). */
const HUD_GLOW = 'rgba(251,146,60,0.45)';
const HUD_GLOW_RADIUS = 8;

export function StreakFlame({
  days,
  label = 'Keep it alive!',
  size = 'md',
  locale = 'en',
  accessibilityLabel,
  testID = 'streak-flame',
}: StreakFlameProps) {
  const isRtl = directionForLocale(locale) === 'rtl';
  const dims = SIZES[size];
  const reduceMotion = useReduceMotion();

  const moti = tryLoadMoti();
  const MotiView = moti?.MotiView as React.ComponentType<Record<string, unknown>> | undefined;

  const isHud = size === 'sm';
  const glowColor = isHud ? HUD_GLOW : colors.streakGlow;
  const glowRadius = isHud ? HUD_GLOW_RADIUS : 24;

  const flameGlyph = (
    <Text
      fontSize={dims.flame}
      style={{ textShadowColor: glowColor, textShadowRadius: glowRadius }}
      accessibilityElementsHidden
    >
      {'🔥'}
    </Text>
  );

  let flame: React.ReactNode;

  if (!MotiView || reduceMotion) {
    // No Moti (web SSR) or reduce-motion: static glyph with glow only.
    flame = flameGlyph;
  } else if (isHud) {
    // HUD chip: scale 1→1.02, 1000ms half-cycle, 2s full loop via repeatReverse.
    flame = (
      <MotiView
        from={{ scale: 1 }}
        animate={{ scale: HUD_SCALE_TARGET }}
        transition={{ type: 'timing', duration: HUD_LOOP_MS, loop: true, repeatReverse: true }}
      >
        {flameGlyph}
      </MotiView>
    );
  } else {
    // Hero (md): scale 1→1.08, opacity 0.85, 400ms loop.
    flame = (
      <MotiView
        from={{ scale: 1, opacity: 1 }}
        animate={{ scale: HERO_SCALE_TARGET, opacity: 0.85 }}
        transition={{ type: 'timing', duration: durations.slow, loop: true, repeatReverse: true }}
      >
        {flameGlyph}
      </MotiView>
    );
  }

  return (
    <XStack
      testID={testID}
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
