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
  const isRtl = direction === 'rtl';
  const isTile = variant === 'tile';

  return (
    <YStack
      flex={isTile ? 1 : undefined}
      // Explicit direction so the icon+value row flips natively under RTL — the
      // icon ends up at the inline-end (RIGHT in RTL) with NO manual reversal.
      dir={isRtl ? 'rtl' : 'ltr'}
      paddingVertical={isTile ? 8 : 0}
      paddingHorizontal={isTile ? 10 : 0}
      borderRadius={isTile ? 12 : 0}
      borderWidth={isTile ? 1 : 0}
      borderColor={isTile ? 'rgba(255,255,255,0.04)' : 'transparent'}
      backgroundColor={isTile ? '$bg' : 'transparent'}
      // Tile content spans the full width so the value-row + label sit on the
      // correct side (RTL → right, LTR → left) instead of always hugging left.
      alignItems={isTile ? 'stretch' : 'center'}
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      <XStack
        width={isTile ? '100%' : undefined}
        alignItems="center"
        // flex-start = inline-start: RIGHT under dir=rtl, LEFT under dir=ltr.
        justifyContent={isTile ? 'flex-start' : 'center'}
        gap={isTile ? 5 : '$2'}
        flexDirection="row"
        accessibilityElementsHidden
      >
        {icon ? <Text fontSize={isTile ? 13 : 22}>{icon}</Text> : null}
        <Text
          color={accent}
          fontSize={isTile ? 14 : 26}
          fontWeight="900"
          fontFamily="$heading"
          style={{ fontVariant: ['tabular-nums'] }}
        >
          {value}
        </Text>
      </XStack>
      <Text
        color="$fg3"
        width={isTile ? '100%' : undefined}
        textAlign={isTile ? (isRtl ? 'right' : 'left') : 'center'}
        fontSize={isTile ? 9 : 11}
        fontWeight="700"
        fontFamily="$heading"
        textTransform="uppercase"
        letterSpacing={0.4}
        marginTop={isTile ? 2 : '$1'}
        accessibilityElementsHidden
      >
        {label}
      </Text>
    </YStack>
  );
}

KPIStatCard.displayName = 'KPIStatCard';
