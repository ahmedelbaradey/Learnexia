/**
 * Parent Dashboard placeholder (Design Spec Screen 11). Branded "coming soon"
 * with a sign-out affordance and a link to My-Children.
 */
import { Button } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { Image } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { assets } from '../../src/assets';
import { useLocale } from '../../src/hooks/useLocale';
import { useSignOutAction } from '../../src/hooks/useSignOutAction';

export default function ParentDashboardScreen() {
  const { t } = useTranslation();
  const { isRtl, direction } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { signOut, isPending } = useSignOutAction();

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
          <Text color="$fg3" fontSize={14} fontWeight="500" fontFamily="$body">
            {t('auth.signOut')}
          </Text>
        </Stack>
      </Stack>

      <Stack flex={1} alignItems="center" justifyContent="center" gap="$6" paddingHorizontal="$6">
        <Image source={assets.mascotOwl} style={{ width: 100, height: 100, resizeMode: 'contain', opacity: 0.8 }} accessibilityElementsHidden />
        <Text color="$fg1" fontSize={22} fontWeight="700" fontFamily="$heading" textAlign="center" accessibilityRole="header" writingDirection={direction}>
          {t('parent.dashboard.title')}
        </Text>
        <Text color="$fg3" fontSize={15} fontFamily="$body" textAlign="center" maxWidth={260} writingDirection={direction}>
          {t('parent.dashboard.body')}
        </Text>
        <Button variant="secondary" accessibilityLabel={t('parent.dashboard.viewChildren')} onPress={() => router.push('/(parent)/children')}>
          {t('parent.dashboard.viewChildren')}
        </Button>
      </Stack>
    </Stack>
  );
}
