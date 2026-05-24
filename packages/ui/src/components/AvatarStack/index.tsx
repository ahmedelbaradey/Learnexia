/**
 * AvatarStack — a cluster of overlapping `Avatar`s (capture
 * `web/04-my-children.png`, the FamilySummaryStrip child cluster). Each
 * subsequent avatar overlaps the previous one by a negative logical margin, with
 * a thin ring (border) so the overlap reads cleanly on a colored surface.
 *
 * Composes the existing `Avatar` primitive — no new design pattern. RTL-aware:
 * the overlap direction follows the logical start so the cluster reads the same
 * in ar/en. A11y: the whole cluster is exposed with a single caller-provided
 * label (the individual avatars are decorative).
 */
import { type Direction } from '@learnexia/shared/i18n';
import { Stack } from '@tamagui/core';
import React from 'react';

import { Avatar, type AvatarSize } from '../Avatar';

export interface AvatarStackEntry {
  /** Display name — drives the initial + deterministic color. */
  name: string;
  /** Optional image source. */
  uri?: string;
}

export interface AvatarStackProps {
  items: AvatarStackEntry[];
  size?: AvatarSize;
  /** Overlap in px (how much each avatar covers the previous). Default 16. */
  overlap?: number;
  /** Cap how many avatars render before stopping. Default 4. */
  max?: number;
  direction?: Direction;
  /** Required cluster label for screen readers (already localized). */
  accessibilityLabel: string;
}

export function AvatarStack({
  items,
  size = 'md',
  overlap = 16,
  max = 4,
  direction = 'ltr',
  accessibilityLabel,
}: AvatarStackProps) {
  const isRtl = direction === 'rtl';
  const shown = items.slice(0, max);

  return (
    <Stack
      flexDirection={isRtl ? 'row-reverse' : 'row'}
      alignItems="center"
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {shown.map((item, i) => (
        <Stack
          key={`${item.name}-${i}`}
          marginStart={i === 0 ? 0 : -overlap}
          borderRadius={9999}
          borderWidth={2}
          borderColor="$bgElevated"
          accessibilityElementsHidden
          aria-hidden
        >
          <Avatar name={item.name} uri={item.uri} size={size} />
        </Stack>
      ))}
    </Stack>
  );
}

AvatarStack.displayName = 'AvatarStack';
