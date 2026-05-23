/**
 * Settings screen — the parent account & preferences screen
 * (capture `web/07-settings.png`), P1-11.
 *
 * Wide (≥768): left `Sidebar` (Settings nav item active) + the `SettingsWeb`
 * main content (header, six-tab left rail, active panel). Narrow (<768): the
 * mobile `ScreenHeader` + the same `SettingsWeb` content stacked.
 *
 * Functional tabs: Profile (UI-first; Save + avatar upload are P1-12 stubs) and
 * Language & region (switches the app language en↔ar app-wide + persists via the
 * locale store; region UI-only). The other four tabs (Notifications / Linked
 * children / Security / Plan & billing) render a "coming soon" panel — TODO(P2-12).
 *
 * Scoped server-side to the authenticated parent (the JWT); full name comes from
 * `useMe`. RTL + ar/en; tokens only.
 */
import { useMyChildren } from '@learnexia/api-client';
import { Stack } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { ScreenHeader } from '../../src/components/ScreenHeader';
import { NAV_ITEM, Sidebar } from './_components/Sidebar';
import { SettingsWeb } from './_components/SettingsWeb';
import { getChildStatsStub } from './_components/parentDashboardStubs';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

export default function SettingsScreen() {
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
        <Sidebar activeChild={activeChild} activeKey={NAV_ITEM.Settings} />
        <ScrollView style={{ flex: 1 }} contentContainerStyle={{ flexGrow: 1, paddingBottom: 48 }}>
          <SettingsWeb />
        </ScrollView>
      </Stack>
    );
  }

  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScreenHeader title={t('parent.settings.title')} onBack={() => router.back()} />
      <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>
        <SettingsWeb />
      </ScrollView>
    </Stack>
  );
}
