/**
 * Shared Login screen (Design Spec Screen 3) — parent + child. Welcoming tone
 * with the mascot. Reads + clears any pending session-expired flash message.
 * Footer link to parent registration (no student self-register).
 */
import { useFlashMessageStore } from '@learnexia/shared';
import { Card } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { Image } from 'react-native';
import { useTranslation } from 'react-i18next';

import { assets } from '../../src/assets';
import { FormScaffold } from '../../src/components/FormScaffold';
import { useLocale } from '../../src/hooks/useLocale';
import { LoginForm } from './_components/LoginForm';

export default function LoginScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const consume = useFlashMessageStore((s) => s.consume);
  const [flashKey, setFlashKey] = useState<string | null>(null);

  // Read-and-clear the one-shot flash (e.g. session expired) once on mount.
  useEffect(() => {
    setFlashKey(consume());
  }, [consume]);

  return (
    <FormScaffold>
      <Stack gap="$6">
        <Stack alignItems="center" gap="$3">
          <Image source={assets.mascotOwl} style={{ width: 80, height: 80, resizeMode: 'contain' }} accessibilityElementsHidden />
          <Image source={assets.logo} style={{ width: 140, height: 64, resizeMode: 'contain' }} accessibilityLabel={t('common.appName')} />
          <Text color="$fg1" fontSize={24} fontWeight="700" fontFamily="$heading" textAlign="center" accessibilityRole="header" writingDirection={direction}>
            {t('auth.login.title')}
          </Text>
          <Text color="$fg3" fontSize={14} fontFamily="$body" textAlign="center" writingDirection={direction}>
            {t('auth.login.subtitle')}
          </Text>
        </Stack>

        {flashKey ? (
          <Card variant="soft">
            <Text color="$fg2" fontSize={13} textAlign="center" writingDirection={direction}>
              {t(flashKey)}
            </Text>
          </Card>
        ) : null}

        <LoginForm />

        <Stack flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'} justifyContent="center" gap="$1">
          <Text color="$fg3" fontSize={14} fontFamily="$body">
            {t('auth.login.newParent')}
          </Text>
          <Text
            color="$primaryLight"
            fontSize={14}
            fontWeight="600"
            fontFamily="$body"
            cursor="pointer"
            onPress={() => router.push('/(auth)/register')}
            accessibilityRole="link"
            accessibilityLabel={t('auth.login.createAccount')}
            aria-label={t('auth.login.createAccount')}
          >
            {t('auth.login.createAccount')}
          </Text>
        </Stack>
      </Stack>
    </FormScaffold>
  );
}
