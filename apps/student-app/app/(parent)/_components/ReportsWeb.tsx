/**
 * ReportsWeb — parent Reports page body (P5-05 real charts + data wiring).
 *
 * Charts replacing ChartPlaceholderPanel slots:
 *  - 20-day XP trend (BarChart Shape B, testID "reports-chart-20day")
 *  - Time-of-day distribution (BarChart Shape C, testID "reports-chart-tod")
 *
 * Data sources:
 *  - KPI row: `useWeeklyKpis` endpoint (time / lessons / accuracy from weekly-kpis;
 *    XP tile stays "—" — G-2 gap unchanged per spec)
 *  - All three charts: `useChildReports` (single GET /api/Parent/Children/{id}/Reports)
 *    - `dailyXpSeries`    → daily-activity (not used here, consumed by DailyActivityCard)
 *    - `xpTrend20Day`     → 20-day trend (Shape B); field `day`+`xp` (not `date`+`xpEarned`)
 *    - `timeOfDayBuckets` → 4 named buckets "Morning|Afternoon|Evening|Night" (Shape C)
 *      NOTE: NOT 8 hourly bars — the real DTO has named buckets, not an `hour` integer.
 *  - Mastery panel: `useSubjectMastery` — fields `bySubject[].subjectCode`+`percent`
 *    (not `subjects[].subjectId`+`masteryPercent`)
 *
 * RTL + ar/en; tokens only; no new design patterns.
 */
import {
  ATTEMPT_STATUS,
  useMyChildren,
  useStudentAttempts,
  useChildReports,
  useSubjectMastery,
  useWeeklyKpis,
  PARENT_SUBJECT_CODE_MAP,
  type AttemptListItemDto,
} from '@learnexia/api-client';
import { type Direction } from '@learnexia/shared/i18n';
import { Button, MasteryBar, type MasteryBarProps } from '@learnexia/ui';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React, { useState } from 'react';
import { Platform, useWindowDimensions } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { useActiveChildStore } from '../../../src/providers/activeChildStore';
import { FocusAreasCard } from './FocusAreasCard';
import { ParentHeader } from './ParentHeader';
import { RecommendationsCard } from './RecommendationsCard';
import { RecentAttemptsPanel } from './RecentAttemptsPanel';
import { OVERVIEW_SUBJECT, type OverviewSubjectKey } from './parentDashboardStubs';
import {
  REPORT_RANGE,
  formatHoursMinutes,
  formatNumber,
  formatPercent,
  formatSignedNumber,
  splitByRange,
  type ReportRangeValue,
} from './reportsFormat';
import { BarChart } from './BarChart';

const IS_WEB = Platform.OS === 'web';
const DESKTOP_VIEWPORT_BREAKPOINT = 1025;
const EMPTY_VALUE = '—';

/** Subject label keys */
const SUBJECT_LABEL_KEY: Record<OverviewSubjectKey, string> = {
  [OVERVIEW_SUBJECT.Math]: 'parent.overview.subjects.math',
  [OVERVIEW_SUBJECT.Science]: 'parent.overview.subjects.science',
  [OVERVIEW_SUBJECT.Arabic]: 'parent.overview.subjects.arabic',
  [OVERVIEW_SUBJECT.English]: 'parent.overview.subjects.english',
};

const SUBJECT_ACCENT: Record<OverviewSubjectKey, MasteryBarProps['accent']> = {
  [OVERVIEW_SUBJECT.Math]: '$subjectMathFg',
  [OVERVIEW_SUBJECT.Science]: '$subjectScienceFg',
  [OVERVIEW_SUBJECT.Arabic]: '$subjectArabicFg',
  [OVERVIEW_SUBJECT.English]: '$subjectEnglishFg',
};

const SUBJECT_KEYS = Object.values(OVERVIEW_SUBJECT);

/**
 * Convert a named time-of-day bucket to a short display label.
 * The backend returns "Morning" | "Afternoon" | "Evening" | "Night" (4 buckets).
 * These are NOT hourly bars — there is no `hour` integer in the real DTO.
 * Labels are i18n keys resolved here for display.
 */
const TOD_BUCKET_LABEL_KEY: Record<string, string> = {
  Morning: 'parent.reports.charts.tod.morning',
  Afternoon: 'parent.reports.charts.tod.afternoon',
  Evening: 'parent.reports.charts.tod.evening',
  Night: 'parent.reports.charts.tod.night',
} as const;

export function ReportsWeb() {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const { width } = useWindowDimensions();
  const rowDir: 'row' | 'row-reverse' = isRtl ? 'row-reverse' : 'row';
  const isDesktop = width >= DESKTOP_VIEWPORT_BREAKPOINT;

  const childrenQuery = useMyChildren();
  const children = childrenQuery.data ?? [];
  const activeChildId = useActiveChildStore((s) => s.activeChildId);
  const openAddChild = useActiveChildStore((s) => s.openAddChild);

  const activeChild =
    (activeChildId ? children.find((c) => String(c.id) === activeChildId) : null) ??
    children[0];
  const childName = activeChild?.fullName ?? '';
  const childId = activeChild ? String(activeChild.id) : undefined;

  const attemptsQuery = useStudentAttempts(activeChild?.id);
  const [range] = useState<ReportRangeValue>(REPORT_RANGE.Week);

  const isLoading =
    childrenQuery.isLoading || (activeChild != null && attemptsQuery.isLoading);
  const hasChildren = !childrenQuery.isLoading && children.length > 0;
  const attempts = attemptsQuery.data ?? [];
  const isFirstWeek =
    activeChild != null && attemptsQuery.isSuccess && attempts.length === 0;

  const windows = splitByRange(attempts, range);

  return (
    <Stack testID="reports-root" flexDirection="column" width="100%">
      <ParentHeader
        title={
          activeChild
            ? t('parent.reports.title', { name: childName })
            : t('parent.nav.reports')
        }
        subtitle={t('parent.reports.subtitle')}
      />

      <Stack flexDirection="column" gap="$6" padding="$6">
        {childrenQuery.isError ? (
          <ErrorStrip
            message={t('parent.reports.loadError')}
            retryLabel={t('common.retry')}
            onRetry={() => childrenQuery.refetch()}
            rowDir={rowDir}
            direction={direction}
          />
        ) : isLoading ? (
          <LoadingSkeleton rowDir={rowDir} />
        ) : !hasChildren ? (
          <Stack
            testID="reports-add-child-band"
            backgroundColor="$primarySoft"
            borderRadius="$card"
            padding={16}
            gap="$3"
            alignItems="flex-start"
          >
            <Text
              color="$fg1"
              fontSize={13}
              fontWeight="700"
              fontFamily="$heading"
              writingDirection={direction}
            >
              {t('parent.reports.empty.addChild')}
            </Text>
            <Button
              variant="ghost"
              size="sm"
              accessibilityLabel={t('parent.myChildren.addChild')}
              onPress={() => openAddChild()}
            >
              {t('parent.myChildren.addChild')}
            </Button>
          </Stack>
        ) : attemptsQuery.isError ? (
          <ErrorStrip
            message={t('parent.reports.loadError')}
            retryLabel={t('common.retry')}
            onRetry={() => attemptsQuery.refetch()}
            rowDir={rowDir}
            direction={direction}
          />
        ) : (
          <>
            {isFirstWeek ? (
              <Stack
                testID="reports-first-week-band"
                backgroundColor="$primarySoft"
                borderRadius="$card"
                padding={16}
              >
                <Text
                  color="$fg1"
                  fontSize={13}
                  fontWeight="700"
                  fontFamily="$heading"
                  writingDirection={direction}
                >
                  {t('parent.reports.empty.firstWeek', { name: childName })}
                </Text>
              </Stack>
            ) : null}

            {/* KPI row — now uses useWeeklyKpis for real values */}
            <KpiRow
              childId={childId}
              windows={windows}
              range={range}
              rowDir={rowDir}
              direction={direction}
              locale={locale}
              isDesktop={isDesktop}
              isRtl={isRtl}
            />

            {/* 20-day XP trend chart (Shape B) */}
            <TwentyDayChartPanel
              childId={childId}
              direction={direction}
              rowDir={rowDir}
              locale={locale}
            />

            {/* 2-col: skills mastery + time-of-day chart */}
            <Stack flexDirection={rowDir} gap="$6" flexWrap="wrap" alignItems="flex-start">
              <Stack flex={1} minWidth={320}>
                <SkillsMasteryPanel childId={childId} direction={direction} locale={locale} />
              </Stack>
              <Stack flex={1} minWidth={320}>
                <TimeOfDayChartPanel
                  childId={childId}
                  childName={childName}
                  direction={direction}
                  rowDir={rowDir}
                  locale={locale}
                />
              </Stack>
            </Stack>

            {/* Areas to focus + Recommendations */}
            <Stack flexDirection={rowDir} gap="$6" flexWrap="wrap" alignItems="flex-start">
              <Stack flex={1} minWidth={320}>
                <FocusAreasCard
                  childId={childId ?? ''}
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

            {!isFirstWeek ? (
              <RecentAttemptsPanel
                attempts={windows.current}
                childName={childName}
                direction={direction}
                locale={locale}
              />
            ) : null}
          </>
        )}
      </Stack>
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* KPI row — real endpoint (useWeeklyKpis) for time / lessons;        */
/* XP tile stays "—" (G-2 gap); accuracy from attempts client-side.   */
/* ------------------------------------------------------------------ */

interface KpiTileDef {
  key: string;
  icon: string;
  chipBg: StackProps['backgroundColor'];
  label: string;
  value: string;
  subText?: string;
  subTone: 'success' | 'danger' | 'muted';
}

interface KpiRowProps {
  childId: string | undefined;
  windows: ReturnType<typeof splitByRange>;
  range: ReportRangeValue;
  rowDir: 'row' | 'row-reverse';
  direction: Direction;
  locale: string;
  isDesktop: boolean;
  isRtl: boolean;
}

function KpiRow({
  childId,
  windows,
  range,
  rowDir,
  direction,
  locale,
  isDesktop,
  isRtl,
}: KpiRowProps) {
  const { t } = useTranslation();
  const kpiQuery = useWeeklyKpis(childId);

  const { current, previous } = windows;
  const completed = current.filter((a) => a.status === ATTEMPT_STATUS.Completed);
  const prevCompleted = previous.filter(
    (a) => a.status === ATTEMPT_STATUS.Completed,
  );

  const meanAccuracy = (list: AttemptListItemDto[]): number | null =>
    list.length === 0
      ? null
      : list.reduce((acc, a) => acc + (a.accuracyPercentage ?? 0), 0) / list.length;

  const avgAccuracy = meanAccuracy(completed);

  // Accuracy delta from attempts (kept client-side per spec §3.4)
  const withDeltas = range === REPORT_RANGE.Week && previous.length > 0;
  const prevAccuracy = meanAccuracy(prevCompleted);
  const accuracyDelta =
    withDeltas && prevAccuracy != null && avgAccuracy != null
      ? avgAccuracy - prevAccuracy
      : null;

  const signTone = (n: number | null): 'success' | 'danger' | 'muted' =>
    n == null ? 'muted' : n >= 0 ? 'success' : 'danger';

  const kpiData = kpiQuery.data;

  // Time tile — from endpoint or attempts fallback
  const timeLearningMinutes = kpiData?.timeLearningMinutes ?? null;
  const timeLearningDeltaMinutes = kpiData?.timeLearningDeltaMinutes ?? null;

  // Lessons tile — from endpoint or attempts fallback
  const lessonsDone = kpiData?.lessonsDone ?? completed.length;
  const lessonsDelta = kpiData?.lessonsDelta ?? null;

  const tiles: KpiTileDef[] = [
    {
      key: 'time',
      icon: '⏱️',
      chipBg: '$primarySoft',
      label: t('parent.reports.kpi.time'),
      value:
        timeLearningMinutes != null
          ? formatHoursMinutes(timeLearningMinutes * 60, locale)
          : EMPTY_VALUE,
      subText:
        timeLearningDeltaMinutes != null && withDeltas
          ? t('parent.overview.kpi.timeDelta', {
              value: formatNumber(Math.abs(timeLearningDeltaMinutes), locale),
            })
          : undefined,
      subTone: signTone(timeLearningDeltaMinutes),
    },
    {
      // G-2: no parent-readable child XP — honest "—".
      key: 'xp',
      icon: '⭐',
      chipBg: '$xpSoft',
      label: t('parent.reports.kpi.xp'),
      value: EMPTY_VALUE,
      subText: t('parent.reports.kpi.xpComingSoon'),
      subTone: 'muted',
    },
    {
      key: 'lessons',
      icon: '✓',
      chipBg: '$successSoft',
      label: t('parent.reports.kpi.lessons'),
      value: formatNumber(lessonsDone, locale),
      subText:
        lessonsDelta != null && withDeltas
          ? t('parent.overview.kpi.lessonsDelta', {
              value: formatNumber(Math.abs(lessonsDelta), locale),
            })
          : undefined,
      subTone: signTone(lessonsDelta),
    },
    {
      key: 'accuracy',
      icon: '🎯',
      chipBg: '$streakSoft',
      label: t('parent.reports.kpi.accuracy'),
      value: avgAccuracy != null ? formatPercent(avgAccuracy, locale) : EMPTY_VALUE,
      subText:
        accuracyDelta != null
          ? t('parent.reports.delta.vsLastWeek', {
              value: formatSignedNumber(accuracyDelta, locale),
            })
          : undefined,
      subTone: signTone(accuracyDelta),
    },
  ];

  const kpiGridStyle: object | undefined = IS_WEB
    ? ({
        display: 'grid',
        gridTemplateColumns: isDesktop ? 'repeat(4, 1fr)' : 'repeat(2, 1fr)',
        gap: 14,
        alignItems: 'stretch',
        direction: isRtl ? 'rtl' : 'ltr',
      } as object)
    : undefined;

  if (kpiQuery.isLoading) {
    return (
      <Stack
        testID="reports-kpi-region"
        {...(IS_WEB
          ? { style: kpiGridStyle }
          : { flexDirection: rowDir, flexWrap: 'wrap' as const, gap: 14 })}
      >
        {[0, 1, 2, 3].map((i) => (
          <Stack
            key={i}
            flex={1}
            minWidth={200}
            height={110}
            borderRadius="$card"
            backgroundColor="$cardSoft"
            opacity={0.6}
          />
        ))}
      </Stack>
    );
  }

  return (
    <Stack
      testID="reports-kpi-region"
      {...(IS_WEB
        ? { style: kpiGridStyle }
        : { flexDirection: rowDir, flexWrap: 'wrap' as const, gap: 14, alignItems: 'stretch' })}
    >
      {tiles.map((tile) => (
        <Stack
          key={tile.key}
          {...(!IS_WEB ? { flex: 1, minWidth: 200 } : {})}
          borderRadius="$card"
          backgroundColor="$card"
          borderWidth={1}
          borderColor="$borderSubtle"
          padding={18}
          gap="$2"
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
              <Text fontSize={16}>{tile.icon}</Text>
            </Stack>
          </Stack>
          <Text
            color="$fg1"
            fontSize={28}
            fontWeight="800"
            fontFamily="$heading"
            lineHeight={30}
            style={{ fontVariant: ['tabular-nums'] }}
            writingDirection={direction}
          >
            {tile.value}
          </Text>
          {tile.subText ? (
            <Text
              color={
                tile.subTone === 'success'
                  ? '$success'
                  : tile.subTone === 'danger'
                  ? '$danger'
                  : '$fg3'
              }
              fontSize={12}
              fontWeight="700"
              fontFamily="$heading"
              writingDirection={direction}
            >
              {tile.subText}
            </Text>
          ) : null}
          <Stack
            position="absolute"
            top={0}
            left={0}
            right={0}
            bottom={0}
            accessible
            accessibilityLabel={`${tile.label} ${tile.value}${tile.subText ? `, ${tile.subText}` : ''}`}
            aria-label={`${tile.label} ${tile.value}${tile.subText ? `, ${tile.subText}` : ''}`}
            pointerEvents="none"
          />
        </Stack>
      ))}
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* Skills mastery panel — real values from useSubjectMastery (P5-05)   */
/* ------------------------------------------------------------------ */

function SkillsMasteryPanel({
  childId,
  direction,
  locale = 'en',
}: {
  childId: string | undefined;
  direction: Direction;
  locale?: string;
}) {
  const { t } = useTranslation();
  const isAr = locale === 'ar';
  const rowDir: 'row' | 'row-reverse' = direction === 'rtl' ? 'row-reverse' : 'row';

  const query = useSubjectMastery(childId);

  // Field names: `bySubject` (not `subjects`), `subjectCode` (not `subjectId`),
  // `percent` (not `masteryPercent`). subjectCode is 0-indexed (0=Math…3=English).
  const masteryMap: Record<string, { masteryPercent: number }> = {};
  if (query.data?.bySubject) {
    for (const row of query.data.bySubject) {
      const subjectKey = PARENT_SUBJECT_CODE_MAP[row.subjectCode];
      if (subjectKey) {
        masteryMap[subjectKey] = {
          masteryPercent: row.percent,
        };
      }
    }
  }

  return (
    <Stack
      testID="reports-mastery-panel"
      backgroundColor="$card"
      borderRadius="$modal"
      borderWidth={1}
      borderColor="$borderSubtle"
      padding={22}
      gap="$4"
      width="100%"
    >
      <Stack flexDirection="column" gap={6}>
        <Text
          color="$fg1"
          fontSize={16}
          fontWeight="800"
          fontFamily="$heading"
          accessibilityRole="header"
          writingDirection={direction}
        >
          {t('parent.reports.mastery.title')}
        </Text>
        <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
          {t('parent.reports.mastery.sub')}
        </Text>
      </Stack>

      {query.isLoading ? (
        <Stack flexDirection="column" gap={14}>
          {[0, 1, 2, 3].map((i) => (
            <Stack
              key={i}
              height={10}
              borderRadius={9999}
              backgroundColor="$cardSoft"
              opacity={0.5}
            />
          ))}
        </Stack>
      ) : query.isError ? (
        <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
          {t('parent.reports.mastery.empty')}
        </Text>
      ) : (
        <Stack flexDirection="column" gap={14}>
          {SUBJECT_KEYS.map((subject) => {
            const row = masteryMap[subject];
            const pct = row?.masteryPercent ?? 0;
            const label = t(SUBJECT_LABEL_KEY[subject]);
            const fmt = (n: number) =>
              new Intl.NumberFormat(isAr ? 'ar-EG' : 'en-US').format(n);
            // SubjectMasteryItemDto has no lessonsCount — show percent only.
            const pctText = isAr ? `${fmt(pct)}٪` : `${pct}%`;

            return (
              <Stack key={subject} flexDirection="column" gap={6}>
                <Stack
                  flexDirection={rowDir}
                  justifyContent="space-between"
                  alignItems="center"
                >
                  <Text
                    color="$fg1"
                    fontSize={13}
                    fontWeight="700"
                    fontFamily="$heading"
                    writingDirection={direction}
                  >
                    {label}
                  </Text>
                  <Text
                    color={SUBJECT_ACCENT[subject] as StackProps['backgroundColor']}
                    fontSize={12}
                    fontWeight="800"
                    fontFamily="$heading"
                  >
                    {pctText}
                  </Text>
                </Stack>
                <MasteryBar
                  value={pct}
                  asPercent
                  label=""
                  direction={direction}
                  accent={SUBJECT_ACCENT[subject]}
                  accessibilityLabel={`${label} ${pctText}`}
                />
              </Stack>
            );
          })}
        </Stack>
      )}
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* 20-day XP trend chart panel (Shape B, P5-05-FE-3)                   */
/* ------------------------------------------------------------------ */

function TwentyDayChartPanel({
  childId,
  direction,
  rowDir,
  locale,
}: {
  childId: string | undefined;
  direction: Direction;
  rowDir: 'row' | 'row-reverse';
  locale: string;
}) {
  const { t } = useTranslation();
  // useChildReports feeds ALL three chart panels from one GET /api/Parent/Children/{id}/Reports.
  // Use xpTrend20Day (exactly 20 entries; fields: `day` + `xp` — not `date`/`xpEarned`).
  const query = useChildReports(childId);

  const days = query.data?.xpTrend20Day ?? [];
  const today = new Date();
  const allEmpty = days.every((d) => d.xp === 0);

  /** Download 20-day CSV */
  function handleExport() {
    if (!IS_WEB) return;
    const isAr = locale === 'ar';
    const header = isAr ? 'التاريخ,النقاط' : 'Date,XP';
    const rows = days.map((d) => `${d.day},${d.xp}`).join('\n');
    const blob = new Blob([`${header}\n${rows}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'xp-trend-20day.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  return (
    <Stack
      testID="reports-chart-20day"
      backgroundColor="$card"
      borderRadius="$modal"
      borderWidth={1}
      borderColor="$borderSubtle"
      padding={22}
      minHeight={200}
      gap="$4"
      width="100%"
    >
      {/* Header row */}
      <Stack flexDirection={rowDir} alignItems="flex-start" justifyContent="space-between" gap="$3">
        <Stack flexDirection="column" gap="$1">
          <Text
            color="$fg1"
            fontSize={16}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('parent.reports.charts.xpTitle')}
          </Text>
          <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
            {t('parent.reports.charts.xpSubtitle')}
          </Text>
        </Stack>
        {/* Export CSV ghost pill */}
        <Stack
          minHeight={36}
          minWidth={44}
          paddingHorizontal="$4"
          alignItems="center"
          justifyContent="center"
          borderRadius={9999}
          borderWidth={1}
          borderColor="$borderStrong"
          backgroundColor="transparent"
          cursor="pointer"
          hoverStyle={{ backgroundColor: '$cardSoft' }}
          pressStyle={{ scale: 0.95 }}
          onPress={handleExport}
          accessibilityRole="button"
          accessible
          accessibilityLabel={t('parent.overview.dailyActivity.exportCsv')}
          aria-label={t('parent.overview.dailyActivity.exportCsv')}
        >
          <Text color="$primaryLight" fontSize={12} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
            {t('parent.overview.dailyActivity.exportCsv')}
          </Text>
        </Stack>
      </Stack>

      {/* Chart area */}
      {query.isLoading ? (
        <Stack
          height={200}
          borderRadius="$modal"
          backgroundColor="$cardSoft"
          opacity={0.5}
        />
      ) : query.isError ? (
        <ErrorStrip
          message={t('parent.reports.loadError')}
          retryLabel={t('common.retry')}
          onRetry={() => query.refetch()}
          rowDir={rowDir}
          direction={direction}
        />
      ) : (
        <>
          <BarChart
            data={days.map((d, index) => ({
              label: String(index + 1),
              value: d.xp,
              isActive: isSameCalendarDay(d.day, today),
            }))}
            chartHeight={200}
            showValueLabels={false}
            barGap={6}
            direction={direction}
            locale={locale}
            shape="B"
            testID="reports-chart-20day-bars"
            accessibilityLabel={
              locale === 'ar'
                ? 'مخطط نقاط XP لآخر ٢٠ يوماً'
                : 'XP trend chart for the last 20 days'
            }
          />
          {allEmpty && days.length > 0 ? (
            <Text
              color="$fg3"
              fontSize={13}
              fontFamily="$body"
              textAlign="center"
              writingDirection={direction}
            >
              {t('parent.reports.charts.noData')}
            </Text>
          ) : null}
        </>
      )}
    </Stack>
  );
}

/** Checks two date strings refer to the same calendar day. */
function isSameCalendarDay(dateStr: string, today: Date): boolean {
  try {
    const d = new Date(dateStr);
    return (
      d.getFullYear() === today.getFullYear() &&
      d.getMonth() === today.getMonth() &&
      d.getDate() === today.getDate()
    );
  } catch {
    return false;
  }
}

/* ------------------------------------------------------------------ */
/* Time-of-day chart panel (Shape C, P5-05-FE-3)                        */
/* ------------------------------------------------------------------ */

function TimeOfDayChartPanel({
  childId,
  childName,
  direction,
  rowDir,
  locale,
}: {
  childId: string | undefined;
  childName: string;
  direction: Direction;
  rowDir: 'row' | 'row-reverse';
  locale: string;
}) {
  const { t } = useTranslation();
  // useChildReports feeds all three chart panels. timeOfDayBuckets is the 4-bucket data.
  // Buckets: { bucket: "Morning"|"Afternoon"|"Evening"|"Night", totalXp: number }
  // NOT 8 hourly bars with an integer `hour` field.
  const query = useChildReports(childId);

  const buckets = query.data?.timeOfDayBuckets ?? [];
  const allEmpty = buckets.every((b) => b.totalXp === 0);

  // Find peak XP bucket
  const peakXp = Math.max(0, ...buckets.map((b) => b.totalXp));

  // Build bar colors
  const barColors: Array<'peak' | 'secondary' | 'muted'> = buckets.map((b) => {
    if (b.totalXp === peakXp && peakXp > 0) return 'peak';
    if (b.totalXp > 0 && b.totalXp >= peakXp * 0.6) return 'secondary';
    return 'muted';
  });

  // Find peak bucket for insight tip — use the i18n label for the bucket name
  const peakBucket = buckets.find((b) => b.totalXp === peakXp && peakXp > 0);
  const peakBucketLabel = peakBucket
    ? t(TOD_BUCKET_LABEL_KEY[peakBucket.bucket] ?? 'parent.reports.charts.tod.morning')
    : null;

  // Value label suffix: "XP" for time-of-day (totalXp, not minutes)
  const valueSuffix = 'XP';

  return (
    <Stack
      testID="reports-chart-tod"
      backgroundColor="$card"
      borderRadius={20}
      borderWidth={1}
      borderColor="$borderSubtle"
      padding={20}
      width="100%"
    >
      {/* Header */}
      <Stack flexDirection="column" gap="$1" marginBottom="$4">
        <Text
          color="$fg1"
          fontSize={16}
          fontWeight="800"
          fontFamily="$heading"
          accessibilityRole="header"
          writingDirection={direction}
        >
          {t('parent.reports.charts.todTitle')}
        </Text>
        <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
          {t('parent.reports.charts.todSubtitle', { name: childName })}
        </Text>
      </Stack>

      {/* Chart area */}
      {query.isLoading ? (
        <Stack
          height={160}
          borderRadius="$card"
          backgroundColor="$cardSoft"
          opacity={0.5}
        />
      ) : query.isError ? (
        <ErrorStrip
          message={t('parent.reports.loadError')}
          retryLabel={t('common.retry')}
          onRetry={() => query.refetch()}
          rowDir={rowDir}
          direction={direction}
        />
      ) : (
        <>
          <BarChart
            data={buckets.map((b) => ({
              // Use the translated bucket name as the bar label
              label: t(TOD_BUCKET_LABEL_KEY[b.bucket] ?? 'parent.reports.charts.tod.morning'),
              value: b.totalXp,
            }))}
            chartHeight={160}
            showValueLabels
            barGap={6}
            barColors={barColors}
            valueSuffix={valueSuffix}
            direction={direction}
            locale={locale}
            shape="C"
            testID="reports-chart-tod-bars"
            accessibilityLabel={
              locale === 'ar'
                ? 'مخطط نقاط XP حسب وقت اليوم'
                : 'XP by time of day chart'
            }
          />

          {allEmpty ? (
            <Text
              color="$fg3"
              fontSize={13}
              fontFamily="$body"
              textAlign="center"
              writingDirection={direction}
              marginTop="$2"
            >
              {t('parent.reports.charts.todEmpty')}
            </Text>
          ) : null}

          {/* Peak focus insight tip (only when there is data) */}
          {!allEmpty && peakBucketLabel ? (
            <Stack
              testID="tod-peak-insight"
              backgroundColor="rgba(251,146,60,0.08)"
              borderWidth={1}
              borderColor="rgba(251,146,60,0.2)"
              borderRadius={12}
              padding={10}
              paddingHorizontal={12}
              marginTop="$2"
              flexDirection={rowDir}
              alignItems="center"
              gap="$2"
            >
              <Text fontSize={16} accessibilityElementsHidden>💡</Text>
              <Text
                color="#FB923C"
                fontSize={13}
                fontWeight="700"
                fontFamily="$heading"
                writingDirection={direction}
                flex={1}
              >
                {t('parent.reports.charts.peakBucketInsight', {
                  bucket: peakBucketLabel,
                })}
              </Text>
            </Stack>
          ) : null}
        </>
      )}
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* Loading + error states                                               */
/* ------------------------------------------------------------------ */

function LoadingSkeleton({ rowDir }: { rowDir: 'row' | 'row-reverse' }) {
  return (
    <Stack testID="reports-loading" flexDirection="column" gap="$6">
      <Stack flexDirection={rowDir} flexWrap="wrap" gap={14}>
        {[0, 1, 2, 3].map((i) => (
          <Stack
            key={i}
            flex={1}
            minWidth={200}
            height={110}
            borderRadius="$card"
            backgroundColor="$cardSoft"
            opacity={0.6}
          />
        ))}
      </Stack>
      {[0, 1].map((i) => (
        <Stack
          key={i}
          height={200}
          borderRadius="$modal"
          backgroundColor="$cardSoft"
          opacity={0.5}
        />
      ))}
    </Stack>
  );
}

function ErrorStrip({
  message,
  retryLabel,
  onRetry,
  rowDir,
  direction,
}: {
  message: string;
  retryLabel: string;
  onRetry: () => void;
  rowDir: 'row' | 'row-reverse';
  direction: Direction;
}) {
  return (
    <Stack
      testID="reports-error-strip"
      flexDirection={rowDir}
      alignItems="center"
      gap="$4"
      backgroundColor="$dangerSoft"
      borderRadius="$card"
      padding={16}
    >
      <Stack
        width={40}
        height={40}
        borderRadius="$pill"
        backgroundColor="$card"
        alignItems="center"
        justifyContent="center"
        flexShrink={0}
        accessibilityElementsHidden
      >
        <Text fontSize={18}>⚠️</Text>
      </Stack>
      <Text
        flex={1}
        color="$fg1"
        fontSize={13}
        fontWeight="600"
        fontFamily="$body"
        writingDirection={direction}
      >
        {message}
      </Text>
      <Button variant="ghost" size="sm" accessibilityLabel={retryLabel} onPress={onRetry}>
        {retryLabel}
      </Button>
    </Stack>
  );
}

ReportsWeb.displayName = 'ReportsWeb';
