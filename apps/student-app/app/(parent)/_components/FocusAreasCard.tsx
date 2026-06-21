/**
 * FocusAreasCard — the "Areas to focus on" card on the parent Overview (P5-05).
 *
 * Replaced stub data (`getFocusAreasStub`) with real `useWeakAreas` hook
 * (GET api/Parent/Children/{id}/WeakAreas). Empty `areas` list → graceful
 * empty-state (not an error). Loading skeleton + error + retry all present.
 *
 * Field mapping from WeakAreaItemDto:
 *   - `skillName` → topic display text (backend string, displayed as-is)
 *   - `subjectCode` (0=Math,1=Science,2=Arabic,3=English) → subject label + icon
 *   - `masteryPercent` → confidence bar fill (0–100)
 *   - `severity` (1=Low,2=Medium,3=High) → bar tint + chip bg
 *
 * RTL + ar/en; tokens only (no raw hex).
 */
import { useWeakAreas, PARENT_SUBJECT_CODE_MAP } from '@learnexia/api-client';
import { Button } from '@learnexia/ui';
import { type Direction } from '@learnexia/shared/i18n';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { OVERVIEW_SUBJECT, type OverviewSubjectKey } from './parentDashboardStubs';

export interface FocusAreasCardProps {
  childId: string;
  childName: string;
  direction: Direction;
  rowDir: 'row' | 'row-reverse';
  /** Active locale — formats the percent in the a11y label. */
  locale: string;
}

/**
 * Localized percent — Eastern-Arabic digits in AR, Latin digits in EN.
 * Uses ARABIC PERCENT SIGN (U+066A, '٪') in AR per align spec M-26.
 */
function formatPercent(value: number, locale: string): string {
  const isAr = locale === 'ar';
  const formatted = new Intl.NumberFormat(isAr ? 'ar-EG' : 'en-US').format(value);
  return `${formatted}${isAr ? '٪' : '%'}`;
}

/** Subject → i18n label key. */
const SUBJECT_LABEL_KEY: Record<OverviewSubjectKey, string> = {
  [OVERVIEW_SUBJECT.Math]: 'parent.overview.subjects.math',
  [OVERVIEW_SUBJECT.Science]: 'parent.overview.subjects.science',
  [OVERVIEW_SUBJECT.Arabic]: 'parent.overview.subjects.arabic',
  [OVERVIEW_SUBJECT.English]: 'parent.overview.subjects.english',
};

/** Subject → focus-row icon glyph (decorative). */
const SUBJECT_ICON: Record<OverviewSubjectKey, string> = {
  [OVERVIEW_SUBJECT.Math]: '🔢',
  [OVERVIEW_SUBJECT.Science]: '🔬',
  [OVERVIEW_SUBJECT.Arabic]: '📖',
  [OVERVIEW_SUBJECT.English]: '🔡',
};

type TextColor = StackProps['backgroundColor'];

/**
 * Map integer severity (1=Low, 2=Medium, 3=High) to fill/chip colors.
 * High (3) → danger, Medium (2) → warning, Low (1) → fg3 / muted.
 */
function severityFillColor(severity: number): TextColor {
  if (severity >= 3) return '$danger';
  if (severity === 2) return '$warning';
  return '$fg3';
}

function severityChipBg(severity: number): TextColor {
  if (severity >= 3) return '$dangerSoft';
  if (severity === 2) return '$warningSoft';
  return '$cardSoft';
}

export function FocusAreasCard({ childId, childName, direction, rowDir, locale }: FocusAreasCardProps) {
  const { t } = useTranslation();
  // childId may be '' when no active child — useWeakAreas disables when falsy.
  const query = useWeakAreas(childId || undefined);

  const areas = query.data?.areas ?? [];

  return (
    <Stack
      borderRadius={20}
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$border"
      padding={22}
      gap="$5"
    >
      {/* Header */}
      <Stack flexDirection={rowDir} alignItems="flex-start" justifyContent="space-between" gap="$3">
        <Stack flexDirection="column" gap="$1" flex={1}>
          <Text
            color="$fg1"
            fontSize={16}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.overview.focusAreas.title')}
          </Text>
          <Text
            color="$fg3"
            fontSize={13}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.overview.focusAreas.subtitle', { name: childName })}
          </Text>
        </Stack>
        {/* See all — ghost pill stub (no full list screen yet). */}
        <Stack
          minHeight={36}
          paddingHorizontal="$4"
          alignItems="center"
          justifyContent="center"
          borderRadius={9999}
          borderWidth={1}
          borderColor="$borderStrong"
          backgroundColor="transparent"
          cursor="pointer"
          hoverStyle={{ backgroundColor: '$cardSoft' }}
          pressStyle={{ scale: 0.98 }}
          onPress={() => {
            /* TODO(P5-05): open the full focus-areas list. */
          }}
          accessibilityRole="button"
          accessible
          accessibilityLabel={t('parent.overview.focusAreas.seeAll')}
          aria-label={t('parent.overview.focusAreas.seeAll')}
        >
          <Text color="$primaryLight" fontSize={13} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
            {t('parent.overview.focusAreas.seeAll')}
          </Text>
        </Stack>
      </Stack>

      {/* Loading skeleton */}
      {query.isLoading ? (
        <Stack flexDirection="column" gap={10}>
          {[0, 1, 2].map((i) => (
            <Stack
              key={i}
              height={64}
              borderRadius="$cardInner"
              backgroundColor="$cardSoft"
              opacity={0.5}
            />
          ))}
        </Stack>
      ) : query.isError ? (
        /* Error + retry */
        <Stack gap="$3" alignItems="flex-start">
          <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
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
      ) : areas.length === 0 ? (
        /* Empty state — no weak areas detected (common for new child) */
        <Text
          color="$fg3"
          fontSize={13}
          fontFamily="$body"
          textAlign="center"
          writingDirection={direction}
        >
          {t('parent.overview.focusAreas.empty')}
        </Text>
      ) : (
        /* Rows */
        <Stack flexDirection="column" gap={10}>
          {areas.map((area) => {
            const subjectKey = PARENT_SUBJECT_CODE_MAP[area.subjectCode] as OverviewSubjectKey | undefined;
            const subject = subjectKey
              ? t(SUBJECT_LABEL_KEY[subjectKey])
              : '';
            const icon = subjectKey ? SUBJECT_ICON[subjectKey] : '📚';
            const fill = severityFillColor(area.severity);
            const chipBg = severityChipBg(area.severity);
            const a11yPercent = formatPercent(area.masteryPercent, locale);

            return (
              <Stack
                key={area.skillId}
                flexDirection={rowDir}
                alignItems="center"
                gap={14}
                borderRadius="$cardInner"
                backgroundColor="$bg"
                borderWidth={1}
                borderColor="$borderSubtle"
                paddingVertical={12}
                paddingHorizontal={14}
                accessible
                accessibilityLabel={`${area.skillName} ${subject} ${a11yPercent}`}
                aria-label={`${area.skillName} ${subject} ${a11yPercent}`}
              >
                {/* Icon chip */}
                <Stack
                  width={40}
                  height={40}
                  borderRadius="$sm"
                  backgroundColor={chipBg}
                  alignItems="center"
                  justifyContent="center"
                  accessibilityElementsHidden
                >
                  <Text fontSize={18} color={fill}>
                    {icon}
                  </Text>
                </Stack>

                {/* Topic + subject */}
                <Stack flexDirection="column" gap="$1" flex={1}>
                  <Text
                    color="$fg1"
                    fontSize={13}
                    fontWeight="700"
                    fontFamily="$heading"
                    writingDirection={direction}
                    textAlign={direction === 'rtl' ? 'right' : 'left'}
                  >
                    {area.skillName}
                  </Text>
                  <Text
                    color="$fg3"
                    fontSize={12}
                    fontFamily="$body"
                    writingDirection={direction}
                    textAlign={direction === 'rtl' ? 'right' : 'left'}
                  >
                    {subject}
                  </Text>
                </Stack>

                {/* Confidence bar — fill anchors to inline-start */}
                <Stack
                  dir={direction === 'rtl' ? 'rtl' : 'ltr'}
                  width={120}
                  height={8}
                  borderRadius={9999}
                  backgroundColor="$card"
                  borderWidth={1}
                  borderColor="$borderSubtle"
                  overflow="hidden"
                  flexDirection="row"
                  accessibilityElementsHidden
                >
                  <Stack
                    width={`${Math.max(0, Math.min(100, area.masteryPercent))}%` as StackProps['width']}
                    height="100%"
                    backgroundColor={fill}
                  />
                </Stack>

                {/* Percent readout */}
                <Text
                  color={fill}
                  fontSize={14}
                  fontWeight="800"
                  fontFamily="$heading"
                  width={44}
                  textAlign={direction === 'rtl' ? 'left' : 'right'}
                  style={{ fontVariant: ['tabular-nums'] }}
                >
                  {formatPercent(area.masteryPercent, locale)}
                </Text>
              </Stack>
            );
          })}
        </Stack>
      )}
    </Stack>
  );
}

FocusAreasCard.displayName = 'FocusAreasCard';
