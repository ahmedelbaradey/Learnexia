/**
 * ProgressDots — W12 quiz stage progress indicator.
 *
 * A row of N dots: current dot is 8×8 filled `$primary`; others are 6×6 outline
 * `$borderStrong`. RTL: row reverses so the leading dot reads from the
 * reading-leading side. Max 12 dots; beyond 12 truncates to `··· ● ···`
 * (3 either side of current — defensive; BE typically returns ≤10 questions).
 *
 * A11y: `accessibilityRole="progressbar"` + `accessibilityValue`.
 * Design Spec §3.7.
 */
import React from 'react';
import { Stack } from '@tamagui/core';

import { XStack } from '../../internal/primitives';

export interface ProgressDotsProps {
  /** 1-based current index. */
  current: number;
  /** Total number of dots. */
  total: number;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** Already-localized a11y label, e.g. "Question 3 of 5". */
  accessibilityLabel?: string;
  testID?: string;
}

const MAX_DOTS = 12;

export function ProgressDots({
  current,
  total,
  direction = 'ltr',
  accessibilityLabel,
  testID,
}: ProgressDotsProps) {
  const isRtl = direction === 'rtl';

  // Determine which dot indices to render (truncation at MAX_DOTS).
  let dots: Array<{ index: number; isPlaceholder?: boolean }>;
  if (total <= MAX_DOTS) {
    dots = Array.from({ length: total }, (_, i) => ({ index: i + 1 }));
  } else {
    // Show 3 dots before current, current, 3 dots after + placeholder ellipses.
    const start = Math.max(1, current - 3);
    const end = Math.min(total, current + 3);
    dots = [];
    if (start > 1) dots.push({ index: -1, isPlaceholder: true });
    for (let i = start; i <= end; i++) dots.push({ index: i });
    if (end < total) dots.push({ index: -2, isPlaceholder: true });
  }

  return (
    <XStack
      flexDirection={isRtl ? 'row-reverse' : 'row'}
      gap={6}
      alignItems="center"
      justifyContent="center"
      accessible
      accessibilityRole="progressbar"
      accessibilityValue={{ now: current, min: 1, max: total }}
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
      aria-valuenow={current}
      aria-valuemin={1}
      aria-valuemax={total}
      testID={testID}
      // Ensure the container has a minimum 48px tap-area height (a11y floor).
      minHeight={24}
    >
      {dots.map((dot, idx) => {
        if (dot.isPlaceholder) {
          return (
            <Stack key={`ph-${idx}`} width={6} height={6} borderRadius={9999} backgroundColor="$borderStrong" opacity={0.4} />
          );
        }
        const isCurrent = dot.index === current;
        return (
          <Stack
            key={dot.index}
            width={isCurrent ? 8 : 6}
            height={isCurrent ? 8 : 6}
            borderRadius={9999}
            backgroundColor={isCurrent ? '$primary' : 'transparent'}
            borderWidth={isCurrent ? 0 : 1.5}
            borderColor="$borderStrong"
            // Optional glow on current dot — inline since no $shadow-progress-dot token exists.
            shadowColor={isCurrent ? 'rgba(99,102,241,0.40)' : 'transparent'}
            shadowOpacity={isCurrent ? 1 : 0}
            shadowRadius={isCurrent ? 4 : 0}
            shadowOffset={{ width: 0, height: 0 }}
            accessibilityElementsHidden
          />
        );
      })}
    </XStack>
  );
}

ProgressDots.displayName = 'ProgressDots';
