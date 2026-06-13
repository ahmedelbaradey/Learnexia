/**
 * Link Existing Child screen (Design Spec Screen 9) — pushed screen with the
 * LinkChildForm. Pushed (not a modal) for deep-linking compatibility.
 */
import { useLocalizedRouter } from '@/hooks/useLocalizedRouter';
import { Stack } from '@tamagui/core';
import { ScrollView } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { ScreenHeader } from '@/components/ScreenHeader';
import { LinkChildForm } from './_components/LinkChildForm';

export default function LinkChildScreen() {
  const { t } = useTranslation();
  const router = useLocalizedRouter();
  const insets = useSafeAreaInsets();

  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScreenHeader title={t('parent.linkChild.title')} onBack={() => router.back()} />
      <ScrollView contentContainerStyle={{ paddingHorizontal: 24, paddingTop: 32, paddingBottom: 48 }} keyboardShouldPersistTaps="handled">
        <LinkChildForm />
      </ScrollView>
    </Stack>
  );
}
