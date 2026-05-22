/**
 * Child Home placeholder (Design Spec Screen 12) — kid-friendly, mascot-forward,
 * rendered in the child's language (set from `Me.preferredLanguage` at login).
 * Greets the child by name from `authStore.user.fullName`. Includes sign-out.
 */
import { useAuthStore } from '@learnexia/shared';
import { AITutorBubble } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { Image } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { assets } from '../../src/assets';
import { useLocale } from '../../src/hooks/useLocale';
import { useSignOutAction } from '../../src/hooks/useSignOutAction';

export default function ChildHomeScreen() {
  const { t } = useTranslation();
  const { isRtl, direction, locale } = useLocale();
  const insets = useSafeAreaInsets();
  const fullName = useAuthStore((s) => s.user?.fullName);
  const { signOut, isPending } = useSignOutAction();

  const childName = (fullName ?? '').split(/\s+/)[0] || '';

  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <Stack
        height={56}
        paddingHorizontal="$6"
        alignItems="center"
        flexDirection={isRtl ? 'row-reverse' : 'row'}
        justifyContent="space-between"
      >
        <Image source={assets.logoMark} style={{ width: 32, height: 32, resizeMode: 'contain' }} accessibilityElementsHidden />
        <Stack
          minHeight={48}
          justifyContent="center"
          cursor="pointer"
          onPress={isPending ? undefined : signOut}
          accessibilityRole="button"
          accessible
          accessibilityLabel={t('auth.signOut')}
          aria-label={t('auth.signOut')}
        >
          <Text color="$fg3" fontSize={13} fontFamily="$body">
            {t('auth.signOut')}
          </Text>
        </Stack>
      </Stack>

      <Stack flex={1} alignItems="center" justifyContent="center" gap="$6" paddingHorizontal="$6">
        <Image source={assets.mascotOwl} style={{ width: 140, height: 140, resizeMode: 'contain' }} accessibilityElementsHidden />
        <Text color="$fg1" fontSize={26} fontWeight="800" fontFamily="$heading" textAlign="center" accessibilityRole="header" writingDirection={direction}>
          {t('child.home.greeting', { childName })}
        </Text>
        <Text color="$fg2" fontSize={16} fontFamily="$body" textAlign="center" writingDirection={direction}>
          {t('child.home.subtitle')}
        </Text>
        <AITutorBubble
          variant="ai"
          message={t('child.home.mascotMessage')}
          sender={t('child.home.mascotSender')}
          locale={locale}
          accessibilityLabel={t('child.home.mascotMessage')}
        />
      </Stack>
    </Stack>
  );
}
