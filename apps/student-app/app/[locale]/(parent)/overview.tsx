/**
 * Dashboard / Overview screen — the parent weekly-progress dashboard
 * (capture `web/05-dashboard.png`), P1-11-FE-8.
 *
 * Wide (≥768): The shell `_layout.tsx` owns the Sidebar + content ScrollView.
 * This page just renders `<OverviewWeb>` — no duplicate row/sidebar/scroll here.
 *
 * Narrow (<768): Mobile `ScreenHeader` + scrollable body (scroll is local here
 * since the shell's narrow path only provides a plain full-height Stack).
 *
 * `onAddChild` is driven by `useActiveChildStore().openAddChild` so the
 * AddChildModal (mounted in _layout) can be triggered from the empty-state CTA
 * without prop-drilling through `<Slot>`.
 */
import { useLocalizedRouter } from '@/hooks/useLocalizedRouter';
import { Stack } from '@tamagui/core';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { ScreenHeader } from '@/components/ScreenHeader';
import { useActiveChildStore } from '@/providers/activeChildStore';
import { OverviewWeb } from './_components/OverviewWeb';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

export default function OverviewScreen() {
  const { t } = useTranslation();
  const router = useLocalizedRouter();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const openAddChild = useActiveChildStore((s) => s.openAddChild);

  const isWide = width >= WIDE_BREAKPOINT;

  if (isWide) {
    // Wide: shell owns sidebar + scroll; just render the body content.
    return <OverviewWeb onAddChild={openAddChild} />;
  }

  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScreenHeader title={t('parent.nav.overview')} onBack={() => router.back()} />
      <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>
        <OverviewWeb onAddChild={openAddChild} />
      </ScrollView>
    </Stack>
  );
}
