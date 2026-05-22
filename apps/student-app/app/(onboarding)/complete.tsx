/**
 * Onboarding Complete (Design Spec Screen 7) — calm "you're all set" affirmation
 * after all children were added. Routes the parent to their dashboard.
 */
import { useChildContextStore } from '@learnexia/shared';
import { Button } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { MotiView } from 'moti';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../src/hooks/useLocale';

export default function OnboardingCompleteScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const clearActiveChild = useChildContextStore((s) => s.clear);

  const goToDashboard = () => {
    clearActiveChild();
    router.replace('/(parent)');
  };

  return (
    <Stack flex={1} backgroundColor="$bg" alignItems="center" justifyContent="center" paddingHorizontal="$6" gap="$6">
      <MotiView
        from={{ scale: 0.5, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        transition={{ type: 'spring', damping: 10, stiffness: 200 }}
      >
        <Stack width={88} height={88} borderRadius="$pill" backgroundColor="$successSoft" alignItems="center" justifyContent="center">
          <Text fontSize={40} color="$success">
            {'✓'}
          </Text>
        </Stack>
      </MotiView>
      <Text color="$fg1" fontSize={28} fontWeight="800" fontFamily="$heading" textAlign="center" accessibilityRole="header" writingDirection={direction}>
        {t('onboarding.complete.title')}
      </Text>
      <Text color="$fg2" fontSize={16} fontFamily="$body" textAlign="center" maxWidth={280} writingDirection={direction}>
        {t('onboarding.complete.body')}
      </Text>
      <Stack width="100%" maxWidth={320}>
        <Button variant="primary" size="full" accessibilityLabel={t('onboarding.complete.cta')} onPress={goToDashboard}>
          {t('onboarding.complete.cta')}
        </Button>
      </Stack>
    </Stack>
  );
}
