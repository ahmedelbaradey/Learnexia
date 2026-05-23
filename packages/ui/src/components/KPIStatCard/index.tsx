/**
 * KPIStatCard — compact stat tile: an icon glyph, a large value, and an
 * uppercase label below. Used by the parent dashboard child cards (Level / XP /
 * Streak) and reused later by the full Dashboard.
 *
 * Two visual tones:
 *  - `tile`  — boxed dark surface (`$bg` on a card), value tinted by `accent`.
 *  - `inline` — no box; icon + value + label stacked (family-summary strip).
 *
 * Token-only styling. RTL: the icon+value row reverses via the `direction` prop.
 * A11y: the whole tile is one node with an `accessibilityLabel` (caller composes
 * "Level 12" etc. from i18n); the inner glyph/text are hidden from AT.
 */
import { type Direction } from '@learnexia/shared/i18n';
import React from 'react';

import { XStack, YStack, Text } from '../../internal/primitives';

type TextColor = React.ComponentProps<typeof Text>['color'];

export type KPIStatVariant = 'tile' | 'inline';

export interface KPIStatCardProps {
  /** Emoji / glyph shown before the value. Decorative. */
  icon?: string;
  value: string;
  label: string;
  /** Value text color token (e.g. `$xp`, `$streak`, `$primaryLight`). */
  accent?: TextColor;
  variant?: KPIStatVariant;
  direction?: Direction;
  /** Composed label for screen readers, e.g. "XP 1,240". */
  accessibilityLabel: string;
}

export function KPIStatCard({
  icon,
  value,
  label,
  accent = '$fg1',
  variant = 'tile',
  direction = 'ltr',
  accessibilityLabel,
}: KPIStatCardProps) {
  const rowDir = direction === 'rtl' ? 'row-reverse' : 'row';
  const isTile = variant === 'tile';

  return (
    <YStack
      flex={isTile ? 1 : undefined}
      gap={isTile ? '$1' : '$2'}
      padding={isTile ? '$3' : '$0'}
      borderRadius={isTile ? '$sm' : 0}
      backgroundColor={isTile ? '$bg' : 'transparent'}
      alignItems={isTile ? 'flex-start' : 'center'}
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      <XStack alignItems="center" gap="$1" flexDirection={rowDir} accessibilityElementsHidden>
        {icon ? <Text fontSize={isTile ? 15 : 22}>{icon}</Text> : null}
        <Text
          color={accent}
          fontSize={isTile ? 16 : 26}
          fontWeight="800"
          fontFamily="$heading"
          style={{ fontVariant: ['tabular-nums'] }}
        >
          {value}
        </Text>
      </XStack>
      <Text
        color="$fg3"
        fontSize={isTile ? 10 : 11}
        fontWeight="700"
        fontFamily="$heading"
        textTransform="uppercase"
        letterSpacing={0.6}
        accessibilityElementsHidden
      >
        {label}
      </Text>
    </YStack>
  );
}

KPIStatCard.displayName = 'KPIStatCard';
