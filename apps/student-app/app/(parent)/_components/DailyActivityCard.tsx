/**
 * DailyActivityCard — the "Daily activity" card on the parent Overview
 * (capture `web/05-dashboard.png`). Renders the card shell + header
 * ("Daily activity" / "XP earned per day") and the "Export CSV" button.
 *
 * The bar chart itself is DEFERRED to Phase 5 — in the chart area we render a
 * muted empty-state panel sized like the chart, so the card stays pixel-faithful
 * for everything except the bars.
 *
 * "Export CSV" is a no-op stub (Phase 5 — analytics). RTL + ar/en; tokens only.
 */
import { Button } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { type Direction } from '@learnexia/shared/i18n';

export interface DailyActivityCardProps {
  direction: Direction;
  rowDir: 'row' | 'row-reverse';
}

/** Matches the chart-area height in the capture so the card keeps its size. */
const CHART_AREA_HEIGHT = 260;

export function DailyActivityCard({ direction, rowDir }: DailyActivityCardProps) {
  const { t } = useTranslation();

  return (
    <Stack
      borderRadius="$card"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$border"
      padding="$6"
      gap="$5"
      height="100%"
    >
      {/* Header */}
      <Stack flexDirection={rowDir} alignItems="flex-start" justifyContent="space-between" gap="$3">
        <Stack flexDirection="column" gap="$1">
          <Text
            color="$fg1"
            fontSize={18}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('parent.overview.dailyActivity.title')}
          </Text>
          <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
            {t('parent.overview.dailyActivity.subtitle')}
          </Text>
        </Stack>
        {/* Export CSV — Phase-5 stub (no-op until analytics ship). */}
        <Button
          variant="secondary"
          size="sm"
          accessibilityLabel={t('parent.overview.dailyActivity.exportCsv')}
          onPress={() => {
            /* TODO(P5-05): export the daily-activity series to CSV. */
          }}
        >
          {t('parent.overview.dailyActivity.exportCsv')}
        </Button>
      </Stack>

      {/* TODO(P5-05-FE): daily-activity bar chart — deferred to Phase 5.
          Until then, a muted empty-state panel sized like the chart. */}
      <Stack
        height={CHART_AREA_HEIGHT}
        borderRadius="$sm"
        backgroundColor="$bg"
        borderWidth={1}
        borderColor="$border"
        borderStyle="dashed"
        alignItems="center"
        justifyContent="center"
        accessible
        accessibilityLabel={t('parent.overview.dailyActivity.placeholder')}
        aria-label={t('parent.overview.dailyActivity.placeholder')}
      >
        <Text color="$fg3" fontSize={14} fontFamily="$body" textAlign="center" writingDirection={direction}>
          {t('parent.overview.dailyActivity.placeholder')}
        </Text>
      </Stack>
    </Stack>
  );
}

DailyActivityCard.displayName = 'DailyActivityCard';
