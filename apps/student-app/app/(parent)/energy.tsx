/**
 * Energy screen — the parent "Helper Energy" surface (Batch D).
 *
 * Wide (≥768): the shell `_layout.tsx` owns the Sidebar + content ScrollView,
 * so this page just renders `<EnergyWeb>` (same pattern as settings.tsx).
 *
 * Narrow (<768): the shell provides the tab bar; this page wraps `<EnergyWeb>`
 * in a local ScrollView. `EnergyWeb` renders the unified ParentHeader itself.
 *
 * IAP NOTE: the "Buy credits" CTA inside EnergyWeb is a GATED stub — it does NOT
 * start a real purchase (no payments backend / store config wired). See
 * EnergyWeb's `handleBuy` TODO(Batch D / P10 IAP).
 */
import { Stack } from '@tamagui/core';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { EnergyWeb } from './_components/EnergyWeb';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

export default function EnergyScreen() {
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();

  const isWide = width >= WIDE_BREAKPOINT;

  if (isWide) {
    // Wide: shell owns sidebar + scroll; just render the body content.
    return <EnergyWeb />;
  }

  // Narrow: EnergyWeb renders the unified ParentHeader — just a local scroll region.
  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>
        <EnergyWeb />
      </ScrollView>
    </Stack>
  );
}
