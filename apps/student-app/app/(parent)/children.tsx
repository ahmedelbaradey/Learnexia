/**
 * My Children screen — responsive (capture `web/04-my-children.png` + mobile
 * `07-my-children.png`).
 *
 * Wide (≥768): The shell `_layout.tsx` owns the Sidebar + content ScrollView.
 * This page just renders `<MyChildrenWeb>` — no duplicate row/sidebar/scroll.
 *
 * Narrow (<768): Mobile `ScreenHeader` + `MyChildren` list with local scroll.
 */
import { Stack } from '@tamagui/core';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { MyChildrenWeb } from './_components/MyChildrenWeb';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

export default function MyChildrenScreen() {
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();

  const isWide = width >= WIDE_BREAKPOINT;

  if (isWide) {
    // Wide: shell owns sidebar + scroll; just render the body content.
    return <MyChildrenWeb />;
  }

  // Narrow: MyChildrenWeb now renders the unified ParentHeader (title +
  // controls), so no ScreenHeader is needed — just a local scroll region.
  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>
        <MyChildrenWeb />
      </ScrollView>
    </Stack>
  );
}
