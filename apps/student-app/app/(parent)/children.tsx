/**
 * My Children screen — responsive (capture `web/04-my-children.png` + mobile
 * `07-my-children.png`).
 *
 * Wide (≥768): the parent dashboard — left `Sidebar` (brand + child-selector +
 * nav) and the `MyChildrenWeb` main content (header, family summary, child-card
 * grid). Narrow (<768): the existing mobile `ScreenHeader` + `MyChildren` list.
 *
 * Scoped server-side to the authenticated parent (the JWT) — no parent id is
 * ever sent by the client. Children come from `useMyChildren` (P1-04); per-child
 * + family stats are Phase-5 stubs (TODO(P5)).
 */
import { useMyChildren } from '@learnexia/api-client';
import { Stack } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { ScreenHeader } from '../../src/components/ScreenHeader';
import { MyChildren } from './_components/MyChildren';
import { MyChildrenWeb } from './_components/MyChildrenWeb';
import { Sidebar } from './_components/Sidebar';
import { getChildStatsStub } from './_components/parentDashboardStubs';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

export default function MyChildrenScreen() {
  const { t } = useTranslation();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const query = useMyChildren();

  const isWide = width >= WIDE_BREAKPOINT;

  if (isWide) {
    const firstChild = (query.data ?? [])[0];
    const activeChild = firstChild
      ? (() => {
          const id = String(firstChild.id);
          const stats = getChildStatsStub(id);
          return {
            id,
            fullName: firstChild.fullName ?? '',
            grade: stats.grade,
            level: stats.level,
          };
        })()
      : undefined;

    return (
      <Stack flex={1} flexDirection="row" backgroundColor="$bg">
        <Sidebar activeChild={activeChild} />
        <ScrollView style={{ flex: 1 }} contentContainerStyle={{ flexGrow: 1, paddingBottom: 48 }}>
          <MyChildrenWeb />
        </ScrollView>
      </Stack>
    );
  }

  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScreenHeader title={t('parent.myChildren.title')} onBack={() => router.back()} />
      <ScrollView contentContainerStyle={{ paddingHorizontal: 24, paddingTop: 16, paddingBottom: 48 }}>
        <MyChildren />
      </ScrollView>
    </Stack>
  );
}
