/**
 * Splash / boot screen (Design Spec Screen 1) + the routing guard mount point.
 *
 * Always-visible brand splash while `authStore.status === 'unknown'` (hydrating)
 * and while a signed-in user's `Me` is still loading — no content flash. Once
 * the guard (`useAuthRoute`) resolves the target, it `router.replace`s away.
 *
 * If a session-expired flash message is pending, it is shown at the bottom (and
 * the mascot is hidden to keep the hierarchy clean, per the spec).
 */
import { useFlashMessageStore } from '@learnexia/shared';
import { Card } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { Image } from 'react-native';
import { useTranslation } from 'react-i18next';

import { assets } from '../src/assets';
import { DotPulse } from '../src/components/DotPulse';
import { RestartPrompt } from '../src/components/RestartPrompt';
import { useAuthRoute } from '../src/hooks/useAuthRoute';
import { useLocale } from '../src/hooks/useLocale';

export default function SplashScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  useAuthRoute();
  const flashKey = useFlashMessageStore((s) => s.messageKey);
  const hasFlash = Boolean(flashKey);

  return (
    <Stack flex={1} backgroundColor="$bg" alignItems="center" justifyContent="center" gap="$8">
      <Image
        source={assets.logo}
        style={{ width: 160, height: 80, resizeMode: 'contain' }}
        accessibilityLabel={t('common.appName')}
      />
      {!hasFlash ? (
        <Image
          source={assets.mascotOwl}
          style={{ width: 120, height: 120, resizeMode: 'contain' }}
          accessibilityElementsHidden
        />
      ) : null}
      <DotPulse />

      {hasFlash && flashKey ? (
        <Stack position="absolute" bottom={48} width="80%" alignItems="center">
          <Card variant="soft">
            <Text color="$fg2" fontSize={14} textAlign="center" writingDirection={direction}>
              {t(flashKey)}
            </Text>
          </Card>
        </Stack>
      ) : null}

      <RestartPrompt />
    </Stack>
  );
}
