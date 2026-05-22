/**
 * DotPulse — the splash loading indicator (Design Spec Screen 1).
 * Three `$primary` dots with a staggered pulse (Moti when present, else static).
 * LTR order regardless of locale (brand animation, not content).
 */
import { Stack } from '@tamagui/core';
import { MotiView } from 'moti';

const DELAYS = [0, 150, 300];

export function DotPulse() {
  return (
    <Stack
      flexDirection="row"
      gap="$2"
      alignItems="center"
      accessibilityRole="progressbar"
      accessible
      accessibilityLabel="Loading"
      aria-label="Loading"
    >
      {DELAYS.map((delay) => (
        <MotiView
          key={delay}
          from={{ opacity: 0.3, scale: 0.8 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ type: 'timing', duration: 600, loop: true, repeatReverse: true, delay }}
        >
          <Stack width={10} height={10} borderRadius={9999} backgroundColor="$primary" />
        </MotiView>
      ))}
    </Stack>
  );
}
