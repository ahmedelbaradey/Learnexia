/**
 * OverviewWeb — the parent "Dashboard / Overview" main content (capture
 * `web/05-dashboard.png`): page header ("<Child>'s progress" + date range +
 * "This week" select + "Send Report"), four KPI stat cards, a "Daily activity"
 * card (header + "Export CSV" — the bar chart itself is DEFERRED to Phase 5 and
 * rendered as a placeholder), the "Subject mastery" card (4 product subjects),
 * and the "Areas to focus on" list.
 *
 * The active child comes from `useMyChildren` (P1-04); every weekly stat
 * (KPIs / mastery / focus areas / date range) is a Phase-5 stub (TODO(P5)).
 * "Send Report" and "Export CSV" are no-op stubs until analytics ship.
 *
 * RTL + ar/en throughout; tokens only (no raw hex); reuses `@learnexia/ui`
 * primitives (KPIStatCard, MasteryBar) — no new design pattern.
 */
import { useMyChildren } from '@learnexia/api-client';
import { Button, Select } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { DailyActivityCard } from './DailyActivityCard';
import { FocusAreasCard } from './FocusAreasCard';
import { SubjectMasteryCard } from './SubjectMasteryCard';
import { getOverviewKpiStub, type OverviewKpiStub } from './parentDashboardStubs';

/** Fixed reporting-period set (enum-style; only "this week" wired in P1-11). */
const REPORTING_PERIOD = {
  ThisWeek: 'thisWeek',
} as const;

/** Format minutes as "Xh Ym" (digits localized; the h/m glyphs are unit symbols). */
function formatDuration(totalMinutes: number, locale: string): string {
  const fmt = (n: number) => new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(n);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${fmt(hours)}h ${fmt(minutes)}m`;
}

function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(value);
}

interface KpiTileDef {
  icon: string;
  value: string;
  label: string;
  delta: string;
}

function buildKpis(
  kpi: OverviewKpiStub,
  t: ReturnType<typeof useTranslation>['t'],
  locale: string,
): KpiTileDef[] {
  return [
    {
      icon: '⏱️',
      value: formatDuration(kpi.timeLearningMinutes, locale),
      label: t('parent.overview.kpi.timeLearning'),
      delta: t('parent.overview.kpi.timeDelta', {
        value: formatDuration(kpi.timeLearningDeltaMinutes, locale),
      }),
    },
    {
      icon: '⭐',
      value: formatNumber(kpi.xpEarned, locale),
      label: t('parent.overview.kpi.xpEarned'),
      delta: t('parent.overview.kpi.xpDelta', {
        value: formatNumber(kpi.xpDeltaPercent, locale),
      }),
    },
    {
      icon: '✅',
      value: formatNumber(kpi.lessonsDone, locale),
      label: t('parent.overview.kpi.lessonsDone'),
      delta: t('parent.overview.kpi.lessonsDelta', {
        value: formatNumber(kpi.lessonsDelta, locale),
      }),
    },
    {
      icon: '🔥',
      value: formatNumber(kpi.streakDays, locale),
      label: t('parent.overview.kpi.streak'),
      delta: t('parent.overview.kpi.streakDelta', {
        value: formatNumber(kpi.streakDelta, locale),
      }),
    },
  ];
}

export function OverviewWeb() {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const router = useRouter();
  const query = useMyChildren();
  const [period, setPeriod] = useState<string>(REPORTING_PERIOD.ThisWeek);

  const rowDir = isRtl ? 'row-reverse' : 'row';
  const children = query.data ?? [];
  const activeChild = children[0];
  const childId = activeChild ? String(activeChild.id) : undefined;
  const childName = activeChild?.fullName ?? '';

  // Static "this week" range — the real range comes with Phase-5 analytics.
  const dateRange = t('parent.overview.dateRange');

  return (
    <Stack flexDirection="column" gap="$6" padding="$6" maxWidth={1200} width="100%" alignSelf="center">
      {/* Header */}
      <Stack flexDirection={rowDir} alignItems="flex-start" justifyContent="space-between" gap="$4" flexWrap="wrap">
        <Stack flexDirection="column" gap="$1">
          <Text
            color="$fg1"
            fontSize={26}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('parent.overview.title', { name: childName })}
          </Text>
          <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
            {dateRange}
          </Text>
        </Stack>

        <Stack flexDirection={rowDir} alignItems="center" gap="$3">
          <Stack width={150}>
            <Select
              label={t('parent.overview.periodLabel')}
              value={period}
              onChange={(v) => setPeriod(String(v))}
              options={[{ value: REPORTING_PERIOD.ThisWeek, label: t('parent.overview.periodThisWeek') }]}
              direction={direction}
              accessibilityLabel={t('parent.overview.periodLabel')}
            />
          </Stack>
          {/* Send Report — Phase-5 stub (no-op until analytics ship). */}
          <Button
            variant="primary"
            size="sm"
            accessibilityLabel={t('parent.overview.sendReport')}
            onPress={() => {
              /* TODO(P5-05): wire to the reports endpoint. */
            }}
          >
            {t('parent.overview.sendReport')}
          </Button>
        </Stack>
      </Stack>

      {/* Error state */}
      {query.isError ? (
        <Stack alignItems="center" gap="$4" paddingVertical="$8">
          <Text color="$fg3" fontSize={15} fontFamily="$body" textAlign="center" writingDirection={direction}>
            {t('parent.overview.loadError')}
          </Text>
          <Button variant="ghost" accessibilityLabel={t('common.retry')} onPress={() => query.refetch()}>
            {t('common.retry')}
          </Button>
        </Stack>
      ) : !query.isLoading && !activeChild ? (
        <Stack alignItems="center" gap="$4" paddingVertical="$8">
          <Text color="$fg3" fontSize={15} fontFamily="$body" textAlign="center" writingDirection={direction}>
            {t('parent.overview.empty')}
          </Text>
          <Button
            variant="primary"
            accessibilityLabel={t('parent.myChildren.addChild')}
            onPress={() => router.push('/(onboarding)/add-child')}
          >
            {t('parent.myChildren.addChild')}
          </Button>
        </Stack>
      ) : (
        <OverviewBody childId={childId} childName={childName} rowDir={rowDir} direction={direction} locale={locale} />
      )}
    </Stack>
  );
}

interface OverviewBodyProps {
  childId?: string;
  childName: string;
  rowDir: 'row' | 'row-reverse';
  direction: 'ltr' | 'rtl';
  locale: string;
}

function OverviewBody({ childId, childName, rowDir, direction, locale }: OverviewBodyProps) {
  const { t } = useTranslation();
  // Deterministic per-child stubs (TODO(P5-05)). Loading skeleton: render with
  // a placeholder id so the layout stays stable until children resolve.
  const seed = childId ?? '0';
  const kpi = getOverviewKpiStub(seed);
  const kpis = buildKpis(kpi, t, locale);

  return (
    <>
      {/* 4 KPI cards */}
      <Stack flexDirection={rowDir} flexWrap="wrap" gap="$4" alignItems="stretch">
        {kpis.map((k) => (
          <Stack
            key={k.label}
            flex={1}
            minWidth={200}
            borderRadius="$card"
            backgroundColor="$card"
            borderWidth={1}
            borderColor="$border"
            padding="$5"
            gap="$2"
          >
            <Stack flexDirection={rowDir} alignItems="center" justifyContent="space-between" gap="$2">
              <Text
                color="$fg3"
                fontSize={11}
                fontWeight="700"
                fontFamily="$heading"
                textTransform="uppercase"
                letterSpacing={0.6}
                writingDirection={direction}
                flex={1}
              >
                {k.label}
              </Text>
              <Text fontSize={18} accessibilityElementsHidden>
                {k.icon}
              </Text>
            </Stack>
            <Text
              color="$fg1"
              fontSize={32}
              fontWeight="800"
              fontFamily="$heading"
              style={{ fontVariant: ['tabular-nums'] }}
              writingDirection={direction}
            >
              {k.value}
            </Text>
            <Text color="$success" fontSize={12} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
              {k.delta}
            </Text>
            {/* a11y: one composed label per tile (label + value + delta) */}
            <Stack
              position="absolute"
              top={0}
              left={0}
              right={0}
              bottom={0}
              accessible
              accessibilityLabel={`${k.label} ${k.value} ${k.delta}`}
              aria-label={`${k.label} ${k.value} ${k.delta}`}
              pointerEvents="none"
            />
          </Stack>
        ))}
      </Stack>

      {/* Daily activity (chart deferred) + Subject mastery, side by side on wide */}
      <Stack flexDirection={rowDir} gap="$4" flexWrap="wrap" alignItems="stretch">
        <Stack flex={2} minWidth={320}>
          <DailyActivityCard direction={direction} rowDir={rowDir} />
        </Stack>
        <Stack flex={1} minWidth={280}>
          <SubjectMasteryCard childId={seed} direction={direction} />
        </Stack>
      </Stack>

      {/* Areas to focus on */}
      <FocusAreasCard childId={seed} childName={childName} direction={direction} rowDir={rowDir} />
    </>
  );
}

OverviewWeb.displayName = 'OverviewWeb';
