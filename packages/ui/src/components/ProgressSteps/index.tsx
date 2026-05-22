/**
 * ProgressSteps — wizard "Step N of M" dot indicator. Design Spec §2.3.
 *
 * Pill dots: completed/current are `$primary`; future are `$card` + border. The
 * current dot is wider (32 vs 24) for emphasis. The dot order is ALWAYS LTR
 * (progress is directionally neutral) — no RTL flip. Width animates via Moti
 * when present; otherwise it renders at the correct width statically.
 *
 * A11y: container is a `progressbar` with `accessibilityValue`; dots are
 * decorative (`accessible={false}`).
 */
import { Stack } from '@tamagui/core';
import React from 'react';

import { tryLoadMoti } from '../../internal/moti';
import { XStack } from '../../internal/primitives';

export interface ProgressStepsProps {
  /** 1-based current step. */
  currentStep: number;
  totalSteps: number;
  accessibilityLabel: string;
}

function Dot({ state }: { state: 'completed' | 'current' | 'future' }) {
  const width = state === 'current' ? 32 : 24;
  const backgroundColor = state === 'future' ? '$card' : '$primary';
  const borderWidth = state === 'future' ? 1 : 0;

  const inner = (
    <Stack
      height={8}
      width={width}
      borderRadius={9999}
      backgroundColor={backgroundColor}
      borderWidth={borderWidth}
      borderColor="$border"
      shadowColor={state === 'current' ? 'rgba(99,102,241,0.55)' : 'transparent'}
      shadowOpacity={state === 'current' ? 1 : 0}
      shadowRadius={8}
      accessibilityElementsHidden
    />
  );

  const moti = tryLoadMoti();
  const MotiView = moti?.MotiView as React.ComponentType<Record<string, unknown>> | undefined;
  if (MotiView) {
    return (
      <MotiView animate={{ width }} transition={{ type: 'timing', duration: 240 }}>
        {inner}
      </MotiView>
    );
  }
  return inner;
}

export function ProgressSteps({ currentStep, totalSteps, accessibilityLabel }: ProgressStepsProps) {
  const steps = Array.from({ length: totalSteps }, (_, i) => i + 1);
  return (
    <XStack
      gap="$2"
      alignItems="center"
      justifyContent="center"
      accessibilityRole="progressbar"
      accessible
      accessibilityValue={{ min: 1, max: totalSteps, now: currentStep }}
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {steps.map((step) => (
        <Dot
          key={step}
          state={step < currentStep ? 'completed' : step === currentStep ? 'current' : 'future'}
        />
      ))}
    </XStack>
  );
}

ProgressSteps.displayName = 'ProgressSteps';
