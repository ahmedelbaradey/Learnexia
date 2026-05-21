/**
 * GradientFill — renders a linear gradient using `expo-linear-gradient` when it
 * is installed (peer dep), else falls back to the first stop color as a flat
 * fill. Used by XPBar, RewardPopup, AITutorBubble avatar, etc.
 *
 * Keeping this guarded means the package type-checks and Node-smoke renders
 * without requiring the native gradient module.
 */
import { Stack, type StackProps } from '@tamagui/core';
import React from 'react';

export interface GradientFillProps extends StackProps {
  /** Two or more color stops. */
  stops: readonly string[];
  /** Angle in degrees (90 = left→right, 270 = right→left, 135 = diagonal). */
  angle?: number;
}

let cachedGrad: React.ComponentType<Record<string, unknown>> | null | undefined;
function tryLoadGradient(): React.ComponentType<Record<string, unknown>> | null {
  if (cachedGrad !== undefined) return cachedGrad;
  try {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const mod = require('expo-linear-gradient') as {
      LinearGradient?: React.ComponentType<Record<string, unknown>>;
    };
    cachedGrad = mod.LinearGradient ?? null;
  } catch {
    cachedGrad = null;
  }
  return cachedGrad;
}

function angleToVector(angle: number): { start: { x: number; y: number }; end: { x: number; y: number } } {
  const rad = (angle * Math.PI) / 180;
  // 90deg → horizontal L→R. Map CSS-ish angle to expo start/end unit vectors.
  const x = Math.cos(rad);
  const y = Math.sin(rad);
  return {
    start: { x: 0.5 - x / 2, y: 0.5 - y / 2 },
    end: { x: 0.5 + x / 2, y: 0.5 + y / 2 },
  };
}

export function GradientFill({ stops, angle = 90, ...rest }: GradientFillProps) {
  const Gradient = tryLoadGradient();
  if (Gradient) {
    const { start, end } = angleToVector(angle);
    return (
      <Stack {...rest}>
        <Gradient
          colors={stops as string[]}
          start={start}
          end={end}
          style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0 }}
        />
      </Stack>
    );
  }
  // Fallback: flat first-stop color (still visible, never empty).
  const flat = stops[0] ?? '#000';
  return <Stack backgroundColor={flat as StackProps['backgroundColor']} {...rest} />;
}
