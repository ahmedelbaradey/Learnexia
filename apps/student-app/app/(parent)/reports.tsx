/**
 * Reports screen — BLANK placeholder (P1-11). The full pixel-perfect Reports
 * build (KPIs + 20-day / time-of-day charts + tables) is deferred.
 *
 * TODO(P1-11-FE-9 / P5-05-FE): full Reports + charts.
 *
 * Reuses the parent responsive shell (Sidebar with "Reports" active ≥768; mobile
 * `ScreenHeader` below). Body is a clean empty-state: a title + a muted
 * "coming soon" message. RTL + ar/en; tokens only (no raw hex).
 */
import { useMyChildren } from '@learnexia/api-client';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { ScreenHeader } from '../../src/components/ScreenHeader';
import { useLocale } from '../../src/hooks/useLocale';
import { NAV_ITEM, Sidebar } from './_components/Sidebar';
import { getChildStatsStub } from './_components/parentDashboardStubs';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

function ReportsBody() {
  const { t } = useTranslation();
  const { direction } = useLocale();

  return (
    <Stack
      flexDirection="column"
      gap="$3"
      padding="$6"
      width="100%"
    >
      <Text
        color="$fg1"
        fontSize={26}
        fontWeight="800"
        fontFamily="$heading"
        accessibilityRole="header"
        writingDirection={direction}
      >
        {t('parent.reports.title')}
      </Text>
      <Text color="$fg3" fontSize={15} fontFamily="$body" writingDirection={direction}>
        {t('parent.reports.comingSoon')}
      </Text>
    </Stack>
  );
}

export default function ReportsScreen() {
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
        <Sidebar activeChild={activeChild} activeKey={NAV_ITEM.Reports} />
        <ScrollView style={{ flex: 1 }} contentContainerStyle={{ flexGrow: 1, paddingBottom: 48 }}>
          <ReportsBody />
        </ScrollView>
      </Stack>
    );
  }

  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScreenHeader title={t('parent.reports.title')} onBack={() => router.back()} />
      <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>
        <ReportsBody />
      </ScrollView>
    </Stack>
  );
}
