/**
 * PasswordStrengthMeter — a 4-segment pill meter + label for password fields.
 * Design Spec: `preview/mobile-password-meter.html` (P1-11-FE-14, align-register B-1).
 *
 * Four equal pill segments (h:4px, gap:4px, radius:9999) light up cumulatively
 * with the password strength score (1..4). The colour ramps danger → warning →
 * xp → secondary, and the trailing label (already localized by the caller —
 * never hardcoded here) takes the matching colour.
 *
 * The score is computed by the caller (the form's zod scorer) and passed in as a
 * `PasswordStrength` value; the component is purely presentational and token-only.
 *
 * RTL: segments fill in reading order; pass `direction` so the row + label align
 * logically. A11y: exposes a `progressbar` with the localized label.
 */
import { type Direction } from '@learnexia/shared/i18n';
import { Stack, type StackProps } from '@tamagui/core';
import React from 'react';

import { XStack, Text } from '../../internal/primitives';

/** Cumulative password-strength score (0 = empty, 1..4 = Weak..Strong). */
export const PASSWORD_STRENGTH = {
  Empty: 0,
  Weak: 1,
  Fair: 2,
  Good: 3,
  Strong: 4,
} as const;

export type PasswordStrength = (typeof PASSWORD_STRENGTH)[keyof typeof PASSWORD_STRENGTH];

const SEGMENT_COUNT = 4;

/**
 * Per-score active colour (token references). Index 0 is unused (Empty renders
 * no active segments). Order matches the card: danger → warning → xp → secondary.
 */
const SCORE_COLOR: Record<Exclude<PasswordStrength, 0>, StackProps['backgroundColor']> = {
  1: '$danger',
  2: '$warning',
  3: '$xp',
  4: '$secondary',
};

export interface PasswordStrengthMeterProps {
  /** Strength score 0..4 (caller computes from the password). */
  score: PasswordStrength;
  /** Already-localized strength label (e.g. t('auth.register.strength.weak')). */
  label: string;
  direction?: Direction;
  /** Required for screen readers (already localized). */
  accessibilityLabel: string;
  testID?: string;
}

export function PasswordStrengthMeter({
  score,
  label,
  direction = 'ltr',
  accessibilityLabel,
  testID,
}: PasswordStrengthMeterProps) {
  const isRtl = direction === 'rtl';
  const rowDir = isRtl ? 'row-reverse' : 'row';
  const activeColor = score > 0 ? SCORE_COLOR[score as Exclude<PasswordStrength, 0>] : undefined;

  return (
    <XStack
      testID={testID}
      flexDirection={rowDir}
      alignItems="center"
      gap="$3"
      accessibilityRole="progressbar"
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
      accessibilityValue={{ min: 0, max: SEGMENT_COUNT, now: score }}
    >
      <XStack flex={1} gap="$1" flexDirection={rowDir}>
        {Array.from({ length: SEGMENT_COUNT }).map((_, i) => {
          const filled = i < score;
          return (
            <Stack
              key={i}
              flex={1}
              height={4}
              borderRadius={9999}
              backgroundColor={filled ? activeColor : '$border'}
            />
          );
        })}
      </XStack>
      <Text
        minWidth={60}
        color={activeColor ?? '$fg3'}
        fontSize={12}
        fontWeight="800"
        fontFamily="$heading"
        textAlign={isRtl ? 'left' : 'right'}
        writingDirection={direction}
      >
        {label}
      </Text>
    </XStack>
  );
}

PasswordStrengthMeter.displayName = 'PasswordStrengthMeter';
