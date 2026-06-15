/**
 * Reports screen — A4 parent Reports page (CO-FE-1 / P1-11-FE-9, design spec
 * `design-system/ui_kits/parent-dashboard/A4-reports.md`). Charts are deferred
 * to P5-05 (slots reserved); "Send Report" is the L6 toast stub.
 *
 * Wide (≥768): the shell `_layout.tsx` owns the Sidebar + content ScrollView —
 * this page just renders `<ReportsWeb>` (no duplicate row/sidebar/scroll).
 *
 * Narrow (<768): mobile `ScreenHeader` + local scroll (Overview pattern).
 */
import { Stack } from '@tamagui/core';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ReportsWeb } from './_components/ReportsWeb';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

export default function ReportsScreen() {
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();

  const isWide = width >= WIDE_BREAKPOINT;

  if (isWide) {
    // Wide: shell owns sidebar + scroll; just render the body content.
    return <ReportsWeb />;
  }

  // Narrow: ReportsWeb now renders the unified ParentHeader (title + controls),
  // so no ScreenHeader is needed — just a local scroll region.
  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>
        <ReportsWeb />
      </ScrollView>
    </Stack>
  );
}
