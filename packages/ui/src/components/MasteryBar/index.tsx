/**
 * MasteryBar — a labeled progress bar with a gradient fill and a % readout.
 *
 * Lighter sibling of `XPBar` for the parent dashboard child cards: a caption row
 * ("Mastery" + "72%") above a thin `gradXp` (green → indigo) track. Distinct
 * from XPBar (which shows level/XP counters); this one is a plain mastery
 * percentage.
 *
 * RTL: the fill grows from the logical end; the caption row reverses.
 * A11y: non-interactive `progressbar` with `accessibilityValue`.
 */
import { gradientStops } from '@learnexia/design-system';
import { type Direction } from '@learnexia/shared/i18n';
import { Stack, type StackProps } from '@tamagui/core';
import React from 'react';

import { GradientFill } from '../../internal/GradientFill';
import { XStack, YStack, Text } from '../../internal/primitives';

export interface MasteryBarProps {
  /** Mastery ratio 0..1 (or 0..100 if `asPercent`). */
  value: number;
  /** Treat `value` as a 0..100 percentage. Default false (0..1). */
  asPercent?: boolean;
  /** Caption shown on the start side (already localized). */
  label: string;
  direction?: Direction;
  /**
   * Optional solid fill color token (e.g. `'$primary'`). When set, the bar
   * fills with this single color instead of the default `gradXp` gradient —
   * used by the subject-mastery card (per-subject accent). Design Gap GAP-03.
   */
  accent?: StackProps['backgroundColor'];
  /** Required for screen readers, e.g. "Mastery 72 percent". */
  accessibilityLabel: string;
}

const TRACK_HEIGHT = 10;

export function MasteryBar({
  value,
  asPercent = false,
  label,
  direction = 'ltr',
  accent,
  accessibilityLabel,
}: MasteryBarProps) {
  const ratio = Math.max(0, Math.min(1, asPercent ? value / 100 : value));
  const pct = Math.round(ratio * 100);
  const isRtl = direction === 'rtl';
  const rowDir = isRtl ? 'row-reverse' : 'row';

  return (
    <YStack gap="$2" width="100%">
      <XStack justifyContent="space-between" flexDirection={rowDir}>
        <Text
          color="$fg1"
          fontSize={13}
          fontWeight="700"
          fontFamily="$heading"
          writingDirection={direction}
        >
          {label}
        </Text>
        <Text
          color={accent ?? '$fg1'}
          fontSize={13}
          fontWeight="800"
          fontFamily="$heading"
          style={{ fontVariant: ['tabular-nums'] }}
        >
          {`${pct}%`}
        </Text>
      </XStack>

      {/*
       * Progress bars stay LTR regardless of locale (SKILL.md rule 6): force the
       * track + fill row to `flexDirection="row"` so the fill always grows from
       * the visual left, even in RTL.
       */}
      <Stack
        height={TRACK_HEIGHT}
        borderRadius={9999}
        backgroundColor="$bg"
        overflow="hidden"
        flexDirection="row"
        accessibilityRole="progressbar"
        accessible
        accessibilityLabel={accessibilityLabel}
        aria-label={accessibilityLabel}
        accessibilityValue={{ min: 0, max: 100, now: pct }}
      >
        <Stack
          width={`${pct}%` as StackProps['width']}
          height="100%"
          borderRadius={9999}
          overflow="hidden"
          backgroundColor={accent}
        >
          {accent ? null : (
            <GradientFill stops={gradientStops.gradXp.colors} angle={90} height="100%" width="100%" />
          )}
        </Stack>
      </Stack>
    </YStack>
  );
}

MasteryBar.displayName = 'MasteryBar';
