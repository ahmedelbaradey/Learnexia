/**
 * Activity screen — the parent "Activity" notification/timeline surface (Batch D).
 *
 * Wide (≥768): the shell `_layout.tsx` owns the Sidebar + content ScrollView,
 * so this page just renders `<ActivityWeb>` (same pattern as settings.tsx).
 *
 * Narrow (<768): the shell provides the tab bar; this page wraps `<ActivityWeb>`
 * in a local ScrollView. `ActivityWeb` renders the unified ParentHeader itself.
 */
import { Stack } from '@tamagui/core';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ActivityWeb } from './_components/ActivityWeb';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

export default function ActivityScreen() {
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();

  const isWide = width >= WIDE_BREAKPOINT;

  if (isWide) {
    // Wide: shell owns sidebar + scroll; just render the body content.
    return <ActivityWeb />;
  }

  // Narrow: ActivityWeb renders the unified ParentHeader — just a local scroll region.
  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>
        <ActivityWeb />
      </ScrollView>
    </Stack>
  );
}
