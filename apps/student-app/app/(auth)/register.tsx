/**
 * Parent Register screen (Design Spec Screen 2). The ONLY registration screen —
 * no student self-register. Responsive via FormScaffold (phone full-width /
 * tablet+ centered card). Parent-trust tone, no mascot.
 */
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { Image } from 'react-native';
import { useTranslation } from 'react-i18next';

import { assets } from '../../src/assets';
import { FormScaffold } from '../../src/components/FormScaffold';
import { ScreenHeader } from '../../src/components/ScreenHeader';
import { useLocale } from '../../src/hooks/useLocale';
import { RegisterForm } from './_components/RegisterForm';

export default function RegisterScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();

  return (
    <FormScaffold
      header={<ScreenHeader onBack={() => router.replace('/(auth)/login')} backLabel={t('auth.register.backToSignIn')} />}
    >
      <Stack gap="$6">
        <Stack alignItems="center" gap="$2">
          <Image source={assets.logoMark} style={{ width: 48, height: 48, resizeMode: 'contain' }} accessibilityElementsHidden />
          <Text color="$fg1" fontSize={28} fontWeight="800" fontFamily="$heading" textAlign="center" accessibilityRole="header" writingDirection={direction}>
            {t('auth.register.title')}
          </Text>
          <Text color="$fg3" fontSize={14} fontFamily="$body" textAlign="center" writingDirection={direction}>
            {t('auth.register.subtitle')}
          </Text>
        </Stack>
        <RegisterForm />
      </Stack>
    </FormScaffold>
  );
}
