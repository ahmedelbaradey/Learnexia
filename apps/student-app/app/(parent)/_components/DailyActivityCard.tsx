/**
 * DailyActivityCard — "Daily activity" card on the parent Overview (P5-05).
 *
 * Renders BarChart Shape A (7 bars, 180px well, value labels, Level-Up gradient
 * active bar). Data from `useChildReports` — real `GET api/Parent/Children/{id}/Reports`
 * (`dailyXpSeries` field). The `useChildReports` hook is shared with the 20-day
 * and time-of-day panels on the Reports page.
 *
 * Field mapping: `dailyXpSeries[].day` (not `date`) and `.xp` (not `xpEarned`).
 *
 * Export CSV: serializes `{ day, xp }` × 7 to a CSV string and triggers
 * a browser download via a temporary <a> element.
 *
 * RTL: header row reverses (title right, button left). Bar layout always LTR
 * per Brand Law RTL rule 6. Active bar = today's calendar date client-side.
 *
 * States: Loading skeleton | Error + retry | Empty (all xp=0) | Populated.
 */
import { useChildReports } from '@learnexia/api-client';
import { Button } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React from 'react';
import { Platform } from 'react-native';
import { useTranslation } from 'react-i18next';

import { type Direction } from '@learnexia/shared/i18n';

import { BarChart } from './BarChart';

export interface DailyActivityCardProps {
  direction: Direction;
  rowDir: 'row' | 'row-reverse';
  /** Active child id from the parent shell switcher. */
  childId?: string;
  locale: string;
}

const IS_WEB = Platform.OS === 'web';

/**
 * Returns the 3-letter English abbreviation for a date string —
 * always Latin regardless of locale (Brand Law RTL rule 6).
 */
function formatDayLabel(dateStr: string): string {
  try {
    return new Intl.DateTimeFormat('en-US', { weekday: 'short' }).format(
      new Date(dateStr),
    );
  } catch {
    return dateStr.slice(0, 3);
  }
}

/** Returns true when two date strings refer to the same calendar day. */
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

/**
 * Build the a11y summary string for screen readers.
 * Uses `day` and `xp` fields from the real DailyXpEntryDto shape.
 * The chart label prefix uses fixed Arabic/English variants (not i18n keys)
 * because it is an accessibility summary, not user-facing display text.
 */
function buildA11yLabel(
  days: Array<{ day: string; xp: number }>,
  locale: string,
): string {
  const isAr = locale === 'ar';
  const fmt = (n: number) =>
    new Intl.NumberFormat(isAr ? 'ar-EG' : 'en-US').format(n);
  const parts = days.map((d) => {
    const label = formatDayLabel(d.day);
    return isAr
      ? `${label}: ${fmt(d.xp)} نقطة`
      : `${label}: ${fmt(d.xp)} XP`;
  });
  const chartLabel = isAr ? 'مخطط النشاط اليومي' : 'Daily activity chart';
  return `${chartLabel}. ${parts.join(', ')}`;
}

/** Trigger a CSV download in the browser. No-op on native. */
function downloadCsv(
  days: Array<{ day: string; xp: number }>,
  locale: string,
): void {
  if (!IS_WEB) return;
  const isAr = locale === 'ar';
  const header = isAr ? 'التاريخ,النقاط' : 'Date,XP';
  const rows = days.map((d) => `${d.day},${d.xp}`).join('\n');
  const csv = `${header}\n${rows}`;
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'daily-activity.csv';
  a.click();
  URL.revokeObjectURL(url);
}

export function DailyActivityCard({
  direction,
  rowDir,
  childId,
  locale,
}: DailyActivityCardProps) {
  const { t } = useTranslation();
  // useChildReports feeds all three chart panels. dailyXpSeries is the 7-day series.
  const query = useChildReports(childId);
  const today = new Date();

  const days = query.data?.dailyXpSeries ?? [];
  const allEmpty = days.every((d) => d.xp === 0);

  return (
    <Stack
      testID="daily-activity-card"
      borderRadius={20}
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$borderSubtle"
      padding={22}
      gap="$5"
      height="100%"
    >
      {/* Header */}
      <Stack
        flexDirection={rowDir}
        alignItems="flex-start"
        justifyContent="space-between"
        gap="$3"
      >
        <Stack flexDirection="column" gap="$1">
          <Text
            color="$fg1"
            fontSize={18}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.overview.dailyActivity.title')}
          </Text>
          <Text
            color="$fg3"
            fontSize={13}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.overview.dailyActivity.subtitle')}
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
          onPress={() => downloadCsv(days, locale)}
          accessibilityRole="button"
          accessible
          accessibilityLabel={t('parent.overview.dailyActivity.exportCsv')}
          aria-label={t('parent.overview.dailyActivity.exportCsv')}
        >
          <Text
            color="$primaryLight"
            fontSize={12}
            fontWeight="700"
            fontFamily="$heading"
            writingDirection={direction}
          >
            {t('parent.overview.dailyActivity.exportCsv')}
          </Text>
        </Stack>
      </Stack>

      {/* Chart area */}
      {query.isLoading ? (
        <Stack
          height={200}
          borderRadius="$sm"
          backgroundColor="$bg"
          opacity={0.5}
        />
      ) : query.isError ? (
        <Stack gap="$3" alignItems="flex-start">
          <Text
            color="$fg3"
            fontSize={13}
            fontFamily="$body"
            writingDirection={direction}
          >
            {t('parent.overview.loadError')}
          </Text>
          <Button
            variant="ghost"
            size="sm"
            accessibilityLabel={t('common.retry')}
            onPress={() => query.refetch()}
          >
            {t('common.retry')}
          </Button>
        </Stack>
      ) : (
        <>
          <BarChart
            data={days.map((d) => ({
              label: formatDayLabel(d.day),
              value: d.xp,
              isActive: isSameCalendarDay(d.day, today),
            }))}
            chartHeight={180}
            showValueLabels
            direction={direction}
            locale={locale}
            shape="A"
            testID="daily-activity-chart"
            accessibilityLabel={buildA11yLabel(days, locale)}
          />
          {allEmpty && days.length > 0 ? (
            <Text
              color="$fg3"
              fontSize={13}
              fontFamily="$body"
              textAlign="center"
              writingDirection={direction}
            >
              {t('parent.overview.dailyActivity.empty')}
            </Text>
          ) : null}
        </>
      )}
    </Stack>
  );
}

DailyActivityCard.displayName = 'DailyActivityCard';
