/**
 * Avatar — colored circle with a name initial, or an image when `uri` is given.
 *
 * Used by the parent dashboard child cards + child-selector, the HUD greeting,
 * etc. The background color is deterministic from the name (stable per child)
 * picked from a fixed token-backed palette, so the same child is always the
 * same color across screens (matches the capture: orange / purple / blue).
 *
 * Token-only styling (no raw hex outside the palette, which is sourced from the
 * design-system color tokens). RTL-agnostic (a circle has no direction).
 * A11y: decorative by default (`accessibilityElementsHidden`) since the name is
 * always rendered as text beside it; pass `accessibilityLabel` to expose it.
 */
import { colors } from '@learnexia/design-system';
import { Stack } from '@tamagui/core';
import React from 'react';
import { Image } from 'react-native';

import { Text } from '../../internal/primitives';

export type AvatarSize = 'sm' | 'md' | 'lg' | 'xl';

export interface AvatarProps {
  /** Display name — first character is used as the initial. */
  name: string;
  /** Optional image source; when set it overrides the colored circle. */
  uri?: string;
  size?: AvatarSize;
  /** Override the deterministic background color with a specific palette key. */
  color?: AvatarColor;
  /** If provided, the avatar is exposed to screen readers with this label. */
  accessibilityLabel?: string;
}

/** Fixed palette (design-system color tokens) for deterministic avatar bg. */
export type AvatarColor = keyof typeof AVATAR_PALETTE;

const AVATAR_PALETTE = {
  orange: colors.streak, // #FB923C
  purple: colors.purple, // #A855F7
  blue: colors.gem, // #38BDF8
  green: colors.secondary, // #22C55E
  pink: colors.heart, // #FB7185
  indigo: colors.primaryHover, // #6366F1
} as const;

const PALETTE_KEYS = Object.keys(AVATAR_PALETTE) as AvatarColor[];

const SIZE_PX: Record<AvatarSize, number> = {
  sm: 40,
  md: 48,
  lg: 64,
  xl: 72,
};

const FONT_PX: Record<AvatarSize, number> = {
  sm: 16,
  md: 20,
  lg: 26,
  xl: 30,
};

/** Stable hash → palette index (same name always maps to the same color). */
function colorForName(name: string): AvatarColor {
  let hash = 0;
  for (let i = 0; i < name.length; i += 1) {
    hash = (hash * 31 + name.charCodeAt(i)) >>> 0;
  }
  return PALETTE_KEYS[hash % PALETTE_KEYS.length] ?? 'indigo';
}

function initial(name: string): string {
  const trimmed = name.trim();
  return trimmed.length > 0 ? trimmed.charAt(0).toUpperCase() : '?';
}

export function Avatar({
  name,
  uri,
  size = 'md',
  color,
  accessibilityLabel,
}: AvatarProps) {
  const px = SIZE_PX[size];
  const bg = AVATAR_PALETTE[color ?? colorForName(name)];
  const exposed = Boolean(accessibilityLabel);

  return (
    <Stack
      width={px}
      height={px}
      borderRadius={9999}
      overflow="hidden"
      alignItems="center"
      justifyContent="center"
      backgroundColor={bg}
      accessible={exposed}
      accessibilityElementsHidden={!exposed}
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {uri ? (
        <Image source={{ uri }} style={{ width: px, height: px }} />
      ) : (
        <Text color="$fg1" fontSize={FONT_PX[size]} fontWeight="700" fontFamily="$heading">
          {initial(name)}
        </Text>
      )}
    </Stack>
  );
}

Avatar.displayName = 'Avatar';
