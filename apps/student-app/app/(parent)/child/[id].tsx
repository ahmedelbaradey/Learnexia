/**
 * Child Overview — per-child drill-down route (`(parent)/child/[id]`).
 *
 * A PUSHED sub-screen (like `link-child.tsx`), NOT a tab: reached by tapping a
 * child card on My Children. Mirrors the parent-mobile design `PMChildOverview`:
 * a back/avatar/name/grade header, 3 KPI tiles (Time / XP earned / Lessons), a
 * deferred daily-activity slot (NO fabricated bars — Batch D / Phase-5), the
 * 4-subject mastery card, and two actions ("Full report" → Reports tab,
 * "Energy" → Energy tab).
 *
 * Data: the child IDENTITY is resolved from `useMyChildren` (P1-04) against the
 * `id` route param. All weekly KPIs / mastery are Phase-5 stubs (TODO(P5-05),
 * deterministic per child) — never a faked API. Opening this screen sets the
 * active child so Reports/Energy follow the same child.
 *
 * BUILD CONTRACT: native RTL via `dir` on each row root (NO `row-reverse`
 * double-flip) — children stay in natural DOM order; the back chevron flips by
 * direction (scaleX). Numeric radii (tokens render square on native). Color via
 * tokens; spec literals only where the design mandates. 4 product subjects only.
 */
import { useMyChildren } from '@learnexia/api-client';
import { Avatar, Button, KPIStatCard } from '@learnexia/ui';
import { Stack, Text, type StackProps } from '@tamagui/core';
import { useLocalSearchParams, useRouter } from 'expo-router';
import React from 'react';
import { Platform, ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { useActiveChildStore } from '../../../src/providers/activeChildStore';
import { DailyActivityCard } from '../_components/DailyActivityCard';
import { SubjectMasteryCard } from '../_components/SubjectMasteryCard';
import {
  getChildStatsStub,
  getOverviewKpiStub,
  type OverviewKpiStub,
} from '../_components/parentDashboardStubs';

const IS_WEB = Platform.OS === 'web';
/** Sidebar appears at the tablet breakpoint and up (shell owns the scroll there). */
const WIDE_BREAKPOINT = 768;

/* ------------------------------------------------------------------ */
/* Duration / number formatters — mirror OverviewWeb (M-25): localized   */
/* digits via Intl, fixed unit glyphs (h/m · س/د).                      */
/* ------------------------------------------------------------------ */
const DURATION_UNITS = {
  en: { hour: 'h', minute: 'm' },
  ar: { hour: 'س', minute: 'د' },
} as const;

function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(value);
}

function formatDuration(totalMinutes: number, locale: string): string {
  const isAr = locale === 'ar';
  const units = isAr ? DURATION_UNITS.ar : DURATION_UNITS.en;
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${formatNumber(hours, locale)}${units.hour} ${formatNumber(minutes, locale)}${units.minute}`;
}

/* ------------------------------------------------------------------ */
/* KPI tiles — 3 tiles per the design (Time / XP earned / Lessons).      */
/* ------------------------------------------------------------------ */
interface ChildKpiDef {
  key: string;
  icon: string;
  value: string;
  label: string;
  accent: StackProps['backgroundColor'];
}

function buildKpis(
  kpi: OverviewKpiStub,
  t: ReturnType<typeof useTranslation>['t'],
  locale: string,
): ChildKpiDef[] {
  return [
    {
      key: 'time',
      icon: '⏱️',
      value: formatDuration(kpi.timeLearningMinutes, locale),
      label: t('parent.childOverview.kpi.time'),
      accent: '$gem',
    },
    {
      key: 'xp',
      icon: '⭐',
      value: formatNumber(kpi.xpEarned, locale),
      label: t('parent.childOverview.kpi.xp'),
      accent: '$xp',
    },
    {
      key: 'lessons',
      icon: '✅',
      value: formatNumber(kpi.lessonsDone, locale),
      label: t('parent.childOverview.kpi.lessons'),
      accent: '$success',
    },
  ];
}

export default function ChildOverviewScreen() {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const params = useLocalSearchParams<{ id: string }>();
  const routeId = Array.isArray(params.id) ? params.id[0] : params.id;

  const query = useMyChildren();
  const children = query.data ?? [];
  const child = children.find((c) => String(c.id) === routeId);

  const setActiveChildId = useActiveChildStore((s) => s.setActiveChildId);

  // Make this the active child so Reports / Energy follow the same child.
  React.useEffect(() => {
    if (child && routeId) setActiveChildId(routeId);
  }, [child, routeId, setActiveChildId]);

  const isWide = width >= WIDE_BREAKPOINT;

  // Back affordance — chevron flips by direction (scaleX), label localized.
  const header = (
    <Stack
      dir={isRtl ? 'rtl' : 'ltr'}
      flexDirection="row"
      alignItems="center"
      gap={12}
    >
      <Stack
        width={40}
        height={40}
        borderRadius={12}
        backgroundColor="$card"
        borderWidth={1}
        borderColor="$borderSubtle"
        alignItems="center"
        justifyContent="center"
        cursor="pointer"
        pressStyle={{ scale: 0.92 }}
        onPress={() => router.back()}
        accessibilityRole="button"
        accessible
        accessibilityLabel={t('parent.childOverview.back')}
        aria-label={t('parent.childOverview.back')}
      >
        <Text fontSize={20} color="$fg2" scaleX={isRtl ? -1 : 1} accessibilityElementsHidden>
          {'‹'}
        </Text>
      </Stack>
      {child ? (
        <>
          <Avatar name={child.fullName ?? ''} size="md" />
          <Stack flexDirection="column" flex={1} gap={2}>
            <Text
              color="$fg1"
              fontSize={22}
              fontWeight="900"
              fontFamily="$heading"
              accessibilityRole="header"
              writingDirection={direction}
              textAlign={isRtl ? 'right' : 'left'}
            >
              {t('parent.childOverview.title', { name: child.fullName ?? '' })}
            </Text>
            <Text
              color="$fg3"
              fontSize={12}
              fontFamily="$body"
              writingDirection={direction}
              textAlign={isRtl ? 'right' : 'left'}
            >
              {t(`onboarding.grade.${child.grade ?? getChildStatsStub(routeId ?? '0').grade}`)}
            </Text>
          </Stack>
        </>
      ) : null}
    </Stack>
  );

  // Not-found / empty state — graceful back affordance.
  if (!query.isLoading && !child) {
    const body = (
      <Stack flexDirection="column" gap={20} padding={28}>
        {/* Back-only header (no avatar/name when the child is unknown). */}
        <Stack dir={isRtl ? 'rtl' : 'ltr'} flexDirection="row" alignItems="center" gap={12}>
          <Stack
            width={40}
            height={40}
            borderRadius={12}
            backgroundColor="$card"
            borderWidth={1}
            borderColor="$borderSubtle"
            alignItems="center"
            justifyContent="center"
            cursor="pointer"
            pressStyle={{ scale: 0.92 }}
            onPress={() => router.back()}
            accessibilityRole="button"
            accessible
            accessibilityLabel={t('parent.childOverview.back')}
            aria-label={t('parent.childOverview.back')}
          >
            <Text fontSize={20} color="$fg2" scaleX={isRtl ? -1 : 1} accessibilityElementsHidden>
              {'‹'}
            </Text>
          </Stack>
        </Stack>
        <Stack alignItems="center" gap={16} paddingVertical={48}>
          <Text color="$fg3" fontSize={15} fontFamily="$body" textAlign="center" writingDirection={direction}>
            {t('parent.childOverview.notFound')}
          </Text>
          <Button
            variant="secondary"
            accessibilityLabel={t('parent.childOverview.backToChildren')}
            onPress={() => router.replace('/(parent)/children')}
          >
            {t('parent.childOverview.backToChildren')}
          </Button>
        </Stack>
      </Stack>
    );
    if (isWide) return body;
    return (
      <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
        <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>{body}</ScrollView>
      </Stack>
    );
  }

  // Deterministic per-child stub (TODO(P5-05)). Placeholder id keeps the layout
  // stable while children resolve.
  const seed = routeId ?? '0';
  const kpi = getOverviewKpiStub(seed);
  const kpis = buildKpis(kpi, t, locale);

  // KPI tiles: CSS Grid (3-col) on web for pixel control; flex on native.
  const kpiGridStyle: object | undefined = IS_WEB
    ? ({
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: 10,
        alignItems: 'stretch',
        direction: isRtl ? 'rtl' : 'ltr',
      } as object)
    : undefined;

  const body = (
    <Stack flexDirection="column" gap={20} padding={28}>
      {header}

      {/* 3 KPI tiles — Time / XP earned / Lessons. */}
      <Stack
        testID="child-overview-kpi-region"
        {...(IS_WEB
          ? { style: kpiGridStyle }
          : { dir: isRtl ? 'rtl' : 'ltr', flexDirection: 'row', flexWrap: 'wrap', gap: 10, alignItems: 'stretch' })}
      >
        {kpis.map((k) => (
          <Stack key={k.key} {...(!IS_WEB ? { flex: 1, minWidth: 96 } : {})}>
            <KPIStatCard
              icon={k.icon}
              value={k.value}
              label={k.label}
              accent={k.accent}
              direction={direction}
              accessibilityLabel={`${k.label} ${k.value}`}
            />
          </Stack>
        ))}
      </Stack>

      {/* Daily-activity DEFERRED slot (Batch D / Phase-5 — NO fabricated bars). */}
      <DailyActivityCard direction={direction} rowDir={isRtl ? 'row-reverse' : 'row'} />

      {/* Subject mastery — 4 product subjects (Math/Science/Arabic/English). */}
      <SubjectMasteryCard childId={seed} direction={direction} />

      {/* Quick links — natural order [Full report][Energy]; under dir=rtl the
          first sits at the inline-start (RIGHT). */}
      <Stack dir={isRtl ? 'rtl' : 'ltr'} flexDirection="row" gap={10} flexWrap="wrap">
        <Stack flex={1} minWidth={140}>
          <Button
            variant="ghost"
            width="100%"
            accessibilityLabel={t('parent.childOverview.fullReport')}
            onPress={() => router.push('/(parent)/reports')}
          >
            {t('parent.childOverview.fullReport')}
          </Button>
        </Stack>
        <Stack flex={1} minWidth={140}>
          <Button
            variant="secondary"
            width="100%"
            accessibilityLabel={t('parent.childOverview.energy')}
            onPress={() => router.push('/(parent)/energy')}
          >
            {t('parent.childOverview.energy')}
          </Button>
        </Stack>
      </Stack>
    </Stack>
  );

  // Wide (≥768): the shell `_layout.tsx` owns the Sidebar + content ScrollView —
  // just render the body. Narrow: local scroll region (pushed-screen pattern).
  if (isWide) return body;
  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      <ScrollView contentContainerStyle={{ paddingBottom: 48 }}>{body}</ScrollView>
    </Stack>
  );
}

ChildOverviewScreen.displayName = 'ChildOverviewScreen';
