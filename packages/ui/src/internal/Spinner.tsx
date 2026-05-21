/**
 * Spinner — loading indicator used by Button's `loading` state.
 *
 * Visible loading feedback per the a11y baseline. Renders a pulsing dot via
 * Moti when available (peer dep); otherwise a static disc (still a visible
 * non-silent state). Color is a token color name (e.g. '$fg1').
 */
import { Stack, type StackProps } from '@tamagui/core';
import React from 'react';

import { tryLoadMoti } from './moti';

export interface SpinnerProps {
  color?: string;
  size?: number;
}

export function Spinner({ color = '$fg1', size = 18 }: SpinnerProps) {
  const moti = tryLoadMoti();
  const dot = (
    <Stack
      width={size}
      height={size}
      borderRadius={9999}
      borderWidth={2}
      borderColor={color as StackProps['borderColor']}
      borderTopColor="transparent"
    />
  );

  if (moti?.MotiView) {
    const MotiView = moti.MotiView as React.ComponentType<Record<string, unknown>>;
    return (
      <MotiView
        from={{ opacity: 0.3 }}
        animate={{ opacity: 1 }}
        transition={{ type: 'timing', duration: 300, loop: true, repeatReverse: true }}
        accessibilityElementsHidden
      >
        {dot}
      </MotiView>
    );
  }
  return <Stack opacity={0.7}>{dot}</Stack>;
}
