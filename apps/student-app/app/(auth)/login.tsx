/**
 * Shared Login screen (Design Spec Screen 2, P1-11 pixel pass) — parent + child.
 *
 * Web (≥768): split-panel — left `$primary` brand panel ("Welcome back to the
 * adventure." + social proof), right `$bg` form column. Phone (≤768): the brand
 * panel collapses; an app-icon logo mark sits above the header.
 *
 * Header: "LOG IN" eyebrow + "Welcome back" + "Log in to keep your streak alive
 * 🔥". A language (en/ar) + dark/light switch sits in the top corner. Footer
 * link routes to parent registration (no student self-register). Reads + clears
 * any pending session-expired flash. RTL-aware; all copy via i18n.
 */
import { useFlashMessageStore } from '@learnexia/shared';
import { Card } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { FormScaffold } from '../../src/components/FormScaffold';
import { useLocale } from '../../src/hooks/useLocale';
import { LoginBrandPanel } from './_components/LoginBrandPanel';
import { LocaleThemeControls } from './_components/LocaleThemeControls';
import { LoginForm } from './_components/LoginForm';

export default function LoginScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const consume = useFlashMessageStore((s) => s.consume);
  const [flashKey, setFlashKey] = useState<string | null>(null);
  const align = direction === 'rtl' ? 'right' : 'left';

  // Read-and-clear the one-shot flash (e.g. session expired) once on mount.
  useEffect(() => {
    setFlashKey(consume());
  }, [consume]);

  return (
    <FormScaffold
      variant="split"
      brandPanel={<LoginBrandPanel direction={direction} appName={t('common.appName')} />}
    >
      <Stack gap="$6">
        {/* Top controls — language + theme switch, aligned to the end. */}
        <Stack
          flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'}
          justifyContent={direction === 'rtl' ? 'flex-start' : 'flex-end'}
        >
          <LocaleThemeControls direction={direction} />
        </Stack>

        {/* App-icon logo mark — phone only (web shows it in the brand panel). */}
        <Stack alignItems="center" $tablet={{ display: 'none' }}>
          <Stack
            width={72}
            height={72}
            borderRadius="$card"
            backgroundColor="$primary"
            alignItems="center"
            justifyContent="center"
          >
            <Text fontSize={36} accessibilityElementsHidden>
              ✦
            </Text>
          </Stack>
        </Stack>

        {/* Header — eyebrow + heading + subtitle */}
        <Stack gap="$2" alignItems="center" $tablet={{ alignItems: 'flex-start' }}>
          <Text
            color="$primary"
            fontSize={12}
            fontWeight="700"
            fontFamily="$heading"
            textTransform="uppercase"
            letterSpacing={1.5}
            writingDirection={direction}
          >
            {t('auth.login.eyebrow')}
          </Text>
          <Text
            color="$fg1"
            fontSize={32}
            fontWeight="800"
            fontFamily="$heading"
            textAlign={align}
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('auth.login.title')}
          </Text>
          <Text
            color="$fg3"
            fontSize={14}
            fontFamily="$body"
            textAlign={align}
            writingDirection={direction}
          >
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

        <Stack
          flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'}
          justifyContent="center"
          gap="$1"
        >
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
