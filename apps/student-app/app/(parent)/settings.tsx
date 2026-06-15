/**
 * Settings screen — the parent account & preferences screen
 * (capture `web/07-settings.png`), P1-11.
 *
 * Wide (≥768): The shell `_layout.tsx` owns the Sidebar + content ScrollView.
 * This page just renders `<SettingsWeb>` — no duplicate row/sidebar/scroll here.
 *
 * Narrow (<768): Mobile `ScreenHeader` + scrollable body.
 *
 * `onAddChild` is driven by `useActiveChildStore().openAddChild` so the
 * AddChildModal (mounted in _layout) can be triggered from the
 * LinkedChildrenPanel CTA without prop-drilling through `<Slot>`.
 */
import { Stack } from '@tamagui/core';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useActiveChildStore } from '../../src/providers/activeChildStore';
import { SettingsWeb } from './_components/SettingsWeb';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

export default function SettingsScreen() {
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const openAddChild = useActiveChildStore((s) => s.openAddChild);

  const isWide = width >= WIDE_BREAKPOINT;

  if (isWide) {
    // Wide: shell owns sidebar + scroll; just render the body content.
    return <SettingsWeb onAddChild={openAddChild} />;
  }

  // Narrow: SettingsWeb now renders the unified ParentHeader (title + controls),
  // so no ScreenHeader is needed — just a local scroll region.
  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>
        <SettingsWeb onAddChild={openAddChild} />
      </ScrollView>
    </Stack>
  );
}
