/**
 * Dashboard / Overview screen — the parent weekly-progress dashboard
 * (capture `web/05-dashboard.png`), P1-11-FE-8.
 *
 * Wide (≥768): left `Sidebar` (Overview nav item active) + the `OverviewWeb`
 * main content (header, 4 KPI cards, Daily-activity card [chart deferred to
 * Phase 5], Subject-mastery card, Areas-to-focus list). Narrow (<768): the
 * mobile `ScreenHeader` + the same `OverviewWeb` content stacked.
 *
 * Scoped server-side to the authenticated parent (the JWT). The active child is
 * the first linked child from `useMyChildren` (P1-04) — switching child happens
 * via the sidebar child-selector. Every weekly stat is a Phase-5 stub (TODO(P5));
 * the daily-activity bar chart is deferred to P5-05-FE.
 */
import { useMyChildren } from '@learnexia/api-client';
import { Stack } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { ScreenHeader } from '../../src/components/ScreenHeader';
import { NAV_ITEM, Sidebar } from './_components/Sidebar';
import { OverviewWeb } from './_components/OverviewWeb';
import { getChildStatsStub } from './_components/parentDashboardStubs';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

export default function OverviewScreen() {
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

    // Plain `row` — the document `dir="rtl"` already flips it so the sidebar
    // sits on the right in Arabic. An explicit `row-reverse` would double-flip it.
    return (
      <Stack flex={1} flexDirection="row" backgroundColor="$bg">
        <Sidebar activeChild={activeChild} activeKey={NAV_ITEM.Overview} />
        <ScrollView style={{ flex: 1 }} contentContainerStyle={{ flexGrow: 1, paddingBottom: 48 }}>
          <OverviewWeb />
        </ScrollView>
      </Stack>
    );
  }

  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScreenHeader title={t('parent.nav.overview')} onBack={() => router.back()} />
      <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>
        <OverviewWeb />
      </ScrollView>
    </Stack>
  );
}
