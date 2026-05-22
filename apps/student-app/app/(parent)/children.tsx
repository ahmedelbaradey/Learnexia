/**
 * My Children screen (Design Spec Screen 8) — header + the MyChildren list/
 * switcher. Scoped server-side to the authenticated parent (the JWT) — no
 * parent id is ever sent by the client.
 */
import { Stack } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { ScrollView } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { ScreenHeader } from '../../src/components/ScreenHeader';
import { MyChildren } from './_components/MyChildren';

export default function MyChildrenScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const insets = useSafeAreaInsets();

  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScreenHeader title={t('parent.myChildren.title')} onBack={() => router.back()} />
      <ScrollView contentContainerStyle={{ paddingHorizontal: 24, paddingTop: 16, paddingBottom: 48 }}>
        <MyChildren />
      </ScrollView>
    </Stack>
  );
}
