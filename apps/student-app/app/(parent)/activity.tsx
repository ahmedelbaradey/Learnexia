/**
 * Activity screen — stub placeholder for the notification / activity timeline.
 *
 * The full timeline UI (lesson completions, badge awards, streak alerts) is a
 * later batch. This stub satisfies the parent tab bar navigation so the Activity
 * tab routes correctly.
 *
 * Wide (≥768): the shell owns the Sidebar + content ScrollView — this page just
 * renders its body content directly (same pattern as reports.tsx / settings.tsx).
 *
 * Narrow (<768): the shell provides the tab bar; this page renders the body
 * only (no duplicate header — the tab bar serves as the nav).
 */
import { Stack, Text } from '@tamagui/core';
import { useWindowDimensions } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../src/hooks/useLocale';
import { ParentHeader } from './_components/ParentHeader';

const WIDE_BREAKPOINT = 768;

export default function ActivityScreen() {
  const { t } = useTranslation();
  const { width } = useWindowDimensions();
  const { direction } = useLocale();

  const isWide = width >= WIDE_BREAKPOINT;

  return (
    <Stack flex={1} backgroundColor="$bg">
      {/* Unified header keeps ChildSwitcher + AccountMenu reachable on this stub. */}
      <ParentHeader title={t('parent.activity.title')} />
      <Stack
        flex={1}
        paddingHorizontal="$6"
        paddingTop={isWide ? '$8' : '$6'}
        paddingBottom="$6"
        alignItems="center"
        justifyContent="center"
        gap="$4"
      >
        {/* Page eyebrow */}
        <Text
          fontSize={40}
          accessibilityElementsHidden
          importantForAccessibility="no-hide-descendants"
        >
          {'🔔'}
        </Text>
        <Text
          color="$fg3"
          fontSize={15}
          fontFamily="$body"
          writingDirection={direction}
          textAlign="center"
          maxWidth={300}
        >
          {t('parent.activity.comingSoon')}
        </Text>
      </Stack>
    </Stack>
  );
}
