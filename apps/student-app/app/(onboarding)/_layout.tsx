/**
 * Onboarding wizard chrome (Design Spec Screen 4): persistent header (step
 * label + ProgressSteps) over each step screen. 2 steps: add-child → complete.
 *
 * The current step is derived from the active route segment so each step screen
 * stays simple. Back is hidden on step 1. RTL: back chevron flips; step dots do
 * not (progress order is universal).
 *
 * Auth/role guard: signed-out users and students (ROLES.Student) are redirected
 * away before any onboarding content is rendered (useGroupGuard).
 */
import { ProgressSteps } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { Slot, useRouter, useSegments } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useGroupGuard } from '../../src/hooks/useGroupGuard';
import { useLocale } from '../../src/hooks/useLocale';

const TOTAL_STEPS = 2;

function stepForRoute(segment: string | undefined): number {
  if (segment === 'complete') return 2;
  return 1; // add-child (default)
}

export default function OnboardingLayout() {
  const { t } = useTranslation();
  const { isRtl } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const segments = useSegments();
  const { isResolving } = useGroupGuard('(onboarding)');

  // segments e.g. ['(onboarding)', 'add-child'] — the leaf is the step.
  const leaf = segments[segments.length - 1];
  const currentStep = stepForRoute(leaf);
  const showBack = currentStep > 1;

  // Render nothing while the guard is resolving auth/role (prevents content flash).
  if (isResolving) return null;

  return (
    <Stack flex={1} backgroundColor="$bg">
      <Stack paddingHorizontal="$6" paddingTop={insets.top + 16} paddingBottom="$4" gap="$3">
        <Stack
          flexDirection={isRtl ? 'row-reverse' : 'row'}
          justifyContent="space-between"
          alignItems="center"
        >
          {showBack ? (
            <Stack
              minWidth={48}
              minHeight={48}
              alignItems="center"
              justifyContent="center"
              cursor="pointer"
              pressStyle={{ scale: 0.92 }}
              onPress={() => router.back()}
              accessibilityRole="button"
              accessible
              accessibilityLabel={t('onboarding.back')}
              aria-label={t('onboarding.back')}
            >
              <Text fontSize={22} color="$fg3" scaleX={isRtl ? -1 : 1} accessibilityElementsHidden>
                {'‹'}
              </Text>
            </Stack>
          ) : (
            <Stack minWidth={48} minHeight={48} />
          )}

          <Text color="$fg3" fontSize={12} fontWeight="600" fontFamily="$heading" textTransform="uppercase" letterSpacing={0.6}>
            {t('onboarding.stepLabel', { current: currentStep, total: TOTAL_STEPS })}
          </Text>

          <Stack minWidth={48} minHeight={48} />
        </Stack>

        <ProgressSteps
          currentStep={currentStep}
          totalSteps={TOTAL_STEPS}
          accessibilityLabel={t('onboarding.stepLabel', { current: currentStep, total: TOTAL_STEPS })}
        />
      </Stack>

      <Slot />
    </Stack>
  );
}
