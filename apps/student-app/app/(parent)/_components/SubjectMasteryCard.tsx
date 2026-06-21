/**
 * SubjectMasteryCard — "Subject mastery" card on the parent Overview (P5-05).
 *
 * Data from `useSubjectMastery` (real endpoint replacing the stub).
 * The 4 product subjects are rendered in fixed display order:
 *   Math → Science → Arabic → English.
 *
 * Each row now shows a lesson count on the right (per design spec §3.3):
 *   "{lessonsCount} lessons" (EN) / " دروس" (AR)
 * If `lessonsCount` is absent from the response (GAP-5), the count is omitted.
 *
 * States: Loading skeleton | Error + retry | Empty (no mastery data) | Populated.
 * RTL: label row reverses direction. Mastery bar fill stays LTR (Brand Law).
 */
import { useSubjectMastery, PARENT_SUBJECT_CODE_MAP } from '@learnexia/api-client';
import { Button } from '@learnexia/ui';
import { type Direction } from '@learnexia/shared/i18n';
import { MasteryBar, type MasteryBarProps } from '@learnexia/ui';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { OVERVIEW_SUBJECT, type OverviewSubjectKey } from './parentDashboardStubs';

export interface SubjectMasteryCardProps {
  childId: string;
  direction: Direction;
  locale?: string;
}

/** Subject → i18n label key (never a raw subject string). */
const SUBJECT_LABEL_KEY: Record<OverviewSubjectKey, string> = {
  [OVERVIEW_SUBJECT.Math]: 'parent.overview.subjects.math',
  [OVERVIEW_SUBJECT.Science]: 'parent.overview.subjects.science',
  [OVERVIEW_SUBJECT.Arabic]: 'parent.overview.subjects.arabic',
  [OVERVIEW_SUBJECT.English]: 'parent.overview.subjects.english',
};

/** Per-subject solid mastery-bar accent. */
const SUBJECT_ACCENT: Record<OverviewSubjectKey, MasteryBarProps['accent']> = {
  [OVERVIEW_SUBJECT.Math]: '$primary',
  [OVERVIEW_SUBJECT.Science]: '$success',
  [OVERVIEW_SUBJECT.Arabic]: '$purple',
  [OVERVIEW_SUBJECT.English]: '$accent',
};

/** Fixed display order for the 4 product subjects. */
const DISPLAY_ORDER: OverviewSubjectKey[] = [
  OVERVIEW_SUBJECT.Math,
  OVERVIEW_SUBJECT.Science,
  OVERVIEW_SUBJECT.Arabic,
  OVERVIEW_SUBJECT.English,
];

export function SubjectMasteryCard({
  childId,
  direction,
  locale = 'en',
}: SubjectMasteryCardProps) {
  const { t } = useTranslation();
  const isAr = locale === 'ar';
  const rowDir: 'row' | 'row-reverse' = direction === 'rtl' ? 'row-reverse' : 'row';

  const query = useSubjectMastery(childId || undefined);

  // Build a map from subject key → row data from the real endpoint.
  // Field names: `bySubject` (not `subjects`), `subjectCode` (not `subjectId`),
  // `percent` (not `masteryPercent`). subjectCode is 0-indexed (0=Math, 1=Science,
  // 2=Arabic, 3=English). There is no `lessonsCount` on SubjectMasteryItemDto.
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

  const hasData = Object.keys(masteryMap).length > 0;
  const allZero = hasData && DISPLAY_ORDER.every((s) => (masteryMap[s]?.masteryPercent ?? 0) === 0);

  return (
    <Stack
      borderRadius={20}
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$borderSubtle"
      padding={22}
      gap="$5"
      height="100%"
    >
      {/* Header */}
      <Stack flexDirection="column" gap="$1">
        <Text
          color="$fg1"
          fontSize={16}
          fontWeight="800"
          fontFamily="$heading"
          accessibilityRole="header"
          writingDirection={direction}
          textAlign={direction === 'rtl' ? 'right' : 'left'}
        >
          {t('parent.overview.subjectMastery.title')}
        </Text>
        <Text
          color="$fg3"
          fontSize={12}
          fontFamily="$body"
          writingDirection={direction}
          textAlign={direction === 'rtl' ? 'right' : 'left'}
        >
          {t('parent.overview.subjectMastery.subtitle')}
        </Text>
      </Stack>

      {/* States */}
      {query.isLoading ? (
        /* Loading skeleton — 4 pill stubs */
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
        <Stack flexDirection="column" gap={14}>
          {DISPLAY_ORDER.map((subject) => {
            const row = masteryMap[subject];
            const pct = row?.masteryPercent ?? 0;
            const label = t(SUBJECT_LABEL_KEY[subject]);

            // Right-side label: "{pct}%" (no lessonsCount on SubjectMasteryItemDto)
            const fmt = (n: number) =>
              new Intl.NumberFormat(isAr ? 'ar-EG' : 'en-US').format(n);
            const pctText = isAr ? `${fmt(pct)}٪` : `${pct}%`;

            return (
              <Stack key={subject} flexDirection="column" gap={6}>
                {/* Label row */}
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

                {/* Mastery bar */}
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

          {/* Empty state caption when no lessons done yet */}
          {(!hasData || allZero) ? (
            <Text
              color="$fg3"
              fontSize={13}
              fontFamily="$body"
              textAlign="center"
              writingDirection={direction}
            >
              {t('parent.overview.subjectMastery.empty')}
            </Text>
          ) : null}
        </Stack>
      )}
    </Stack>
  );
}

SubjectMasteryCard.displayName = 'SubjectMasteryCard';
