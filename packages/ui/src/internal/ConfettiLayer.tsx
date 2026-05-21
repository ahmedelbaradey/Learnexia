/**
 * ConfettiLayer — celebratory particle burst for RewardPopup.
 *
 * GAP 5 RESOLUTION: confetti + the radial glow burst are Skia canvas effects on
 * native. Skia is loaded lazily (`tryLoadSkia`); when it is unavailable (web SSR,
 * Node smoke, or a host without the peer dep) this renders nothing — the popup
 * is still a complete celebration without particles. The full Skia particle
 * system (40–60 particles, gravity, rotation, fade) is wired by the app shell in
 * P1-09 once a Skia-capable host exists; here we expose the seam and the
 * token-driven color set so the implementation is drop-in.
 *
 * Confetti is decorative — `accessible={false}` / hidden from screen readers.
 */
import { colors } from '@learnexia/design-system';
import { Stack } from '@tamagui/core';
import React from 'react';

import { tryLoadSkia } from './skia';

/** Token-derived confetti palette (Design Spec §4.8 motion step 3). */
export const CONFETTI_COLORS = [
  colors.primary,
  colors.secondary,
  colors.accent,
  colors.xp,
  colors.heart,
  colors.purple,
] as const;

export function ConfettiLayer() {
  const skia = tryLoadSkia();
  if (!skia) {
    // No Skia → no particle layer. The popup celebration still renders fully.
    return null;
  }
  // Skia-capable host: a real particle canvas is attached in P1-09. We render an
  // accessible-hidden absolute container as the mount point.
  return (
    <Stack
      position="absolute"
      top={0}
      left={0}
      right={0}
      bottom={0}
      pointerEvents="none"
      accessibilityElementsHidden
    />
  );
}
