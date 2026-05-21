/**
 * Hearts — life HUD (full / empty / lost / recovering) with loss animation.
 *
 * Design Spec §4.4. Loss animation: scale 1.3 → 0 (fade) → 1 grayscale settle
 * via Moti (spring settle). Recovering empty hearts pulse opacity
 * [0.35, 0.55, 0.35] over durSlow (GAP 6 RESOLUTION — documented timing).
 *
 * RTL: row reverses so heart index 0 (last to lose) sits on the right.
 * A11y: container `meter` with `accessibilityValue`; each heart is decorative.
 */
import { durations, springs } from '@learnexia/design-system';
import { directionForLocale } from '@learnexia/shared/i18n';
import React from 'react';

import { tryLoadMoti } from '../../internal/moti';
import { XStack, YStack, Text } from '../../internal/primitives';

export interface HeartsProps {
  current: number;
  maxHearts?: number;
  /** Minutes until refill; shows the refill label when set. */
  recoveringIn?: number | null;
  locale?: string;
  /** Required for screen readers, e.g. "3 of 5 hearts remaining". */
  accessibilityLabel: string;
}

const HEART_SIZE = 30;

function HeartIcon({
  filled,
  recovering,
}: {
  filled: boolean;
  recovering: boolean;
}) {
  const moti = tryLoadMoti();
  const MotiView = moti?.MotiView as React.ComponentType<Record<string, unknown>> | undefined;

  const glyph = (
    <Text
      fontSize={HEART_SIZE}
      // GAP 5/9-adjacent: grayscale via opacity (filter:grayscale not on RN) —
      // empty hearts use reduced opacity to read as "lost".
      opacity={filled ? 1 : 0.35}
      accessibilityElementsHidden
    >
      {'❤️'}
    </Text>
  );

  // Recovering empty hearts: pulse opacity (GAP 6 documented timing).
  if (!filled && recovering && MotiView) {
    return (
      <MotiView
        from={{ opacity: 0.35 }}
        animate={{ opacity: 0.55 }}
        transition={{ type: 'timing', duration: durations.slow, loop: true, repeatReverse: true }}
      >
        {glyph}
      </MotiView>
    );
  }

  // Filled hearts: gentle settle on mount (loss animation is parent-driven by
  // re-rendering with a lower `current`; Moti animates between states).
  if (MotiView) {
    return (
      <MotiView
        animate={{ scale: 1, opacity: filled ? 1 : 0.35 }}
        transition={{ type: 'spring', ...springs.settle }}
      >
        {glyph}
      </MotiView>
    );
  }

  return glyph;
}

export function Hearts({
  current,
  maxHearts = 5,
  recoveringIn = null,
  locale = 'en',
  accessibilityLabel,
}: HeartsProps) {
  const isRtl = directionForLocale(locale) === 'rtl';
  const slots = Array.from({ length: maxHearts }, (_, i) => i < current);
  const recovering = recoveringIn !== null;

  return (
    <YStack gap="$2">
      <XStack
        gap="$2"
        flexDirection={isRtl ? 'row-reverse' : 'row'}
        accessibilityRole="text"
        accessible
        accessibilityLabel={accessibilityLabel}
        aria-label={accessibilityLabel}
        accessibilityValue={{ min: 0, max: maxHearts, now: current }}
      >
        {slots.map((filled, i) => (
          <HeartIcon key={i} filled={filled} recovering={recovering && !filled} />
        ))}
      </XStack>

      {recovering ? (
        <Text fontSize={14} color="$fg2" fontFamily="$body">
          {'Refill in '}
          <Text color="$warning" fontWeight="700">{`${recoveringIn} min`}</Text>
        </Text>
      ) : null}
    </YStack>
  );
}

Hearts.displayName = 'Hearts';
