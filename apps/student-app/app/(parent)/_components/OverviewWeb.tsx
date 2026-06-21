/**
 * OverviewWeb — parent "Dashboard / Overview" content (P5-05 real data wiring).
 *
 * Replaces `getOverviewKpiStub` with `useWeeklyKpis` (real endpoint).
 * GAP-8 fixed: xpDelta is ABSOLUTE (not a percent) — copy reads "+120 XP this week".
 *
 * Active child from `useActiveChildStore`. Every query includes childId so
 * child-switching triggers independent per-child fetches.
 *
 * RTL + ar/en throughout; tokens only (no raw hex); reuses `@learnexia/ui`
 * primitives. No new design patterns.
 */
import { useMyChildren, useWeeklyKpis } from '@learnexia/api-client';
import { Button } from '@learnexia/ui';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React from 'react';
import { Platform, useWindowDimensions } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { useActiveChildStore } from '../../../src/providers/activeChildStore';
import { DailyActivityCard } from './DailyActivityCard';
import { ParentHeader } from './ParentHeader';
import { FocusAreasCard } from './FocusAreasCard';
import { RecommendationsCard } from './RecommendationsCard';
import { SubjectMasteryCard } from './SubjectMasteryCard';

/**
 * Format minutes as "Xh Ym" (EN) / "Xس Yد" (AR). Digits are localized via Intl.
 */
const DURATION_UNITS = {
  en: { hour: 'h', minute: 'm' },
  ar: { hour: 'س', minute: 'د' },
} as const;

function formatDuration(totalMinutes: number, locale: string): string {
  const isAr = locale === 'ar';
  const fmt = (n: number) => new Intl.NumberFormat(isAr ? 'ar-EG' : 'en-US').format(n);
  const units = isAr ? DURATION_UNITS.ar : DURATION_UNITS.en;
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${fmt(hours)}${units.hour} ${fmt(minutes)}${units.minute}`;
}

function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(value);
}

interface KpiTileDef {
  icon: string;
  value: string;
  label: string;
  delta: string;
  /** Whether the delta is positive/negative/zero — drives color. */
  deltaSign: 'positive' | 'negative' | 'zero';
  chipBg: StackProps['backgroundColor'];
}

/**
 * Build KPI tile definitions from the real `WeeklyKpisDto`.
 * GAP-8: xpDelta is ABSOLUTE — not a percent. Copy uses "+{n} XP this week".
 */
function buildKpis(
  kpi: {
    timeLearningMinutes: number;
    timeLearningDeltaMinutes: number;
    xpEarned: number;
    xpDelta: number;
    lessonsDone: number;
    lessonsDelta: number;
    streakDays: number;
    streakDelta: number;
  },
  t: ReturnType<typeof useTranslation>['t'],
  locale: string,
): KpiTileDef[] {
  const sign = (n: number): 'positive' | 'negative' | 'zero' =>
    n > 0 ? 'positive' : n < 0 ? 'negative' : 'zero';

  return [
    {
      icon: '⏱️',
      chipBg: '$primarySoft',
      value: formatDuration(kpi.timeLearningMinutes, locale),
      label: t('parent.overview.kpi.timeLearning'),
      delta: t('parent.overview.kpi.timeDelta', {
        value: formatDuration(Math.abs(kpi.timeLearningDeltaMinutes), locale),
      }),
      deltaSign: sign(kpi.timeLearningDeltaMinutes),
    },
    {
      icon: '⭐',
      chipBg: '$xpSoft',
      value: formatNumber(kpi.xpEarned, locale),
      label: t('parent.overview.kpi.xpEarned'),
      // GAP-8: xpDelta is ABSOLUTE — key now reads "+{n} XP this week" (EN) / "+{n} نقطة هذا الأسبوع" (AR).
      delta: t('parent.overview.kpi.xpDelta', {
        value: formatNumber(Math.abs(kpi.xpDelta), locale),
      }),
      deltaSign: sign(kpi.xpDelta),
    },
    {
      icon: '✅',
      chipBg: '$successSoft',
      value: formatNumber(kpi.lessonsDone, locale),
      label: t('parent.overview.kpi.lessonsDone'),
      delta: t('parent.overview.kpi.lessonsDelta', {
        value: formatNumber(Math.abs(kpi.lessonsDelta), locale),
      }),
      deltaSign: sign(kpi.lessonsDelta),
    },
    {
      icon: '🔥',
      chipBg: '$streakSoft',
      value: formatNumber(kpi.streakDays, locale),
      label: t('parent.overview.kpi.streak'),
      delta: t('parent.overview.kpi.streakDelta', {
        value: formatNumber(Math.abs(kpi.streakDelta), locale),
      }),
      deltaSign: sign(kpi.streakDelta),
    },
  ];
}

export interface OverviewWebProps {
  onAddChild?: () => void;
}

const DESKTOP_VIEWPORT_BREAKPOINT = 1025;

export function OverviewWeb({ onAddChild }: OverviewWebProps) {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const query = useMyChildren();
  const { width } = useWindowDimensions();

  const rowDir = isRtl ? 'row-reverse' : 'row';
  const children = query.data ?? [];

  const activeChildId = useActiveChildStore((s) => s.activeChildId);
  const activeChild =
    (activeChildId ? children.find((c) => String(c.id) === activeChildId) : null) ??
    children[0];

  const childId = activeChild ? String(activeChild.id) : undefined;
  const childName = activeChild?.fullName ?? '';

  const dateRange = t('parent.overview.dateRange');
  const isDesktop = width >= DESKTOP_VIEWPORT_BREAKPOINT;

  return (
    <Stack testID="overview-root" flexDirection="column" width="100%">
      <ParentHeader
        title={t('parent.overview.title', { name: childName })}
        subtitle={dateRange}
      />

      <Stack flexDirection="column" gap="$6" padding="$6">
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
              onPress={onAddChild ?? (() => {})}
            >
              {t('parent.myChildren.addChild')}
            </Button>
          </Stack>
        ) : (
          <OverviewBody
            childId={childId}
            childName={childName}
            rowDir={rowDir}
            direction={direction}
            locale={locale}
            isDesktop={isDesktop}
            isRtl={isRtl}
          />
        )}
      </Stack>
    </Stack>
  );
}

const IS_WEB = Platform.OS === 'web';

interface OverviewBodyProps {
  childId?: string;
  childName: string;
  rowDir: 'row' | 'row-reverse';
  direction: 'ltr' | 'rtl';
  locale: string;
  isDesktop: boolean;
  isRtl: boolean;
}

function OverviewBody({
  childId,
  childName,
  rowDir,
  direction,
  locale,
  isDesktop,
  isRtl,
}: OverviewBodyProps) {
  const { t } = useTranslation();
  const kpiQuery = useWeeklyKpis(childId);

  const kpiGridStyle: object | undefined = IS_WEB
    ? ({
        display: 'grid',
        gridTemplateColumns: isDesktop ? 'repeat(4, 1fr)' : 'repeat(2, 1fr)',
        gap: 14,
        alignItems: 'stretch',
        direction: isRtl ? 'rtl' : 'ltr',
      } as object)
    : undefined;

  // Build KPI tiles (loading / error / populated)
  const kpis: KpiTileDef[] = kpiQuery.data
    ? buildKpis(kpiQuery.data, t, locale)
    : [];

  return (
    <>
      {/* 4 KPI tiles */}
      <Stack
        testID="overview-kpi-region"
        {...(IS_WEB
          ? { style: kpiGridStyle }
          : { flexDirection: rowDir, flexWrap: 'wrap' as const, gap: 14, alignItems: 'stretch' })}
      >
        {kpiQuery.isLoading
          ? [0, 1, 2, 3].map((i) => (
              <Stack
                key={i}
                {...(!IS_WEB ? { flex: 1, minWidth: 200 } : {})}
                height={110}
                borderRadius={20}
                backgroundColor="$cardSoft"
                opacity={0.6}
              />
            ))
          : kpiQuery.isError
          ? (
            <Stack
              padding={16}
              backgroundColor="$dangerSoft"
              borderRadius={20}
              flexDirection={rowDir}
              alignItems="center"
              gap="$3"
            >
              <Text color="$fg1" fontSize={13} fontFamily="$body" writingDirection={direction}>
                {t('parent.overview.loadError')}
              </Text>
              <Button
                variant="ghost"
                size="sm"
                accessibilityLabel={t('common.retry')}
                onPress={() => kpiQuery.refetch()}
              >
                {t('common.retry')}
              </Button>
            </Stack>
          )
          : kpis.map((k) => (
              <KpiTile key={k.label} tile={k} rowDir={rowDir} direction={direction} />
            ))}
      </Stack>

      {/* Daily activity (chart wired P5-05) + Subject mastery, side by side on wide */}
      <Stack flexDirection={rowDir} gap="$4" flexWrap="wrap" alignItems="stretch">
        <Stack flex={2} minWidth={320}>
          <DailyActivityCard
            direction={direction}
            rowDir={rowDir}
            childId={childId}
            locale={locale}
          />
        </Stack>
        <Stack testID="overview-mastery-region" flex={1} minWidth={280}>
          <SubjectMasteryCard
            childId={childId ?? ''}
            direction={direction}
            locale={locale}
          />
        </Stack>
      </Stack>

      {/* 2-col row: "Areas to focus on" + "Recommendations from Lexi" */}
      <Stack
        testID="overview-focus-recommendations-row"
        flexDirection={rowDir}
        flexWrap="wrap"
        gap="$5"
        alignItems="flex-start"
      >
        <Stack flex={1} minWidth={320}>
          <FocusAreasCard
            childId={childId ?? '0'}
            childName={childName}
            direction={direction}
            rowDir={rowDir}
            locale={locale}
          />
        </Stack>
        <Stack flex={1} minWidth={320}>
          <RecommendationsCard
            childId={childId}
            childName={childName}
            direction={direction}
            rowDir={rowDir}
          />
        </Stack>
      </Stack>
    </>
  );
}

/**
 * Single KPI tile — exact anatomy from `web-kpi-row.html`.
 * Delta color: $success (positive) | $danger (negative) | $fg3 (zero).
 */
function KpiTile({
  tile,
  rowDir,
  direction,
}: {
  tile: KpiTileDef;
  rowDir: 'row' | 'row-reverse';
  direction: 'ltr' | 'rtl';
}) {
  const deltaColor =
    tile.deltaSign === 'positive'
      ? '$success'
      : tile.deltaSign === 'negative'
      ? '$danger'
      : '$fg3';

  return (
    <Stack
      {...(!IS_WEB ? { flex: 1, minWidth: 200 } : {})}
      borderRadius={20}
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$borderSubtle"
      padding={18}
      gap="$2"
      hoverStyle={{ backgroundColor: '$cardSoft', scale: 1.02 }}
    >
      <Stack flexDirection={rowDir} alignItems="center" justifyContent="space-between" gap="$2">
        <Text
          color="$fg3"
          fontSize={11}
          fontWeight="700"
          fontFamily="$heading"
          textTransform="uppercase"
          letterSpacing={0.88}
          writingDirection={direction}
          textAlign={direction === 'rtl' ? 'right' : 'left'}
          flex={1}
        >
          {tile.label}
        </Text>
        <Stack
          width={32}
          height={32}
          borderRadius={10}
          backgroundColor={tile.chipBg}
          alignItems="center"
          justifyContent="center"
          accessibilityElementsHidden
        >
          <Text fontSize={18}>{tile.icon}</Text>
        </Stack>
      </Stack>

      <Text
        color="$fg1"
        fontSize={28}
        fontWeight="800"
        fontFamily="$heading"
        style={{ fontVariant: ['tabular-nums'] }}
        writingDirection={direction}
        textAlign={direction === 'rtl' ? 'right' : 'left'}
      >
        {tile.value}
      </Text>

      <Text
        color={deltaColor}
        fontSize={12}
        fontWeight="700"
        fontFamily="$heading"
        writingDirection={direction}
        textAlign={direction === 'rtl' ? 'right' : 'left'}
      >
        {tile.delta}
      </Text>

      {/* a11y: one composed label per tile */}
      <Stack
        position="absolute"
        top={0}
        left={0}
        right={0}
        bottom={0}
        accessible
        accessibilityLabel={`${tile.label} ${tile.value} ${tile.delta}`}
        aria-label={`${tile.label} ${tile.value} ${tile.delta}`}
        pointerEvents="none"
      />
    </Stack>
  );
}

OverviewWeb.displayName = 'OverviewWeb';
