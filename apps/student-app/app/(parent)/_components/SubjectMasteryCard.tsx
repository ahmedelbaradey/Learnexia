/**
 * SubjectMasteryCard — the "Subject mastery" card on the parent Overview
 * (capture `web/05-dashboard.png`). Header ("Subject mastery" / "Last 7 days")
 * then one `MasteryBar` per subject.
 *
 * The capture's "Reading / Art" rows are mock — we render the 4 PRODUCT subjects
 * (Math / Science / Arabic / English). Percentages are Phase-5 stubs (TODO(P5)).
 *
 * RTL + ar/en; reuses `@learnexia/ui` MasteryBar; tokens only.
 */
import { type Direction } from '@learnexia/shared/i18n';
import { MasteryBar, type MasteryBarProps } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { getSubjectMasteryStub, OVERVIEW_SUBJECT, type OverviewSubjectKey } from './parentDashboardStubs';

export interface SubjectMasteryCardProps {
  childId: string;
  direction: Direction;
}

/** Subject → i18n label key (never a raw subject string). */
const SUBJECT_LABEL_KEY: Record<OverviewSubjectKey, string> = {
  [OVERVIEW_SUBJECT.Math]: 'parent.overview.subjects.math',
  [OVERVIEW_SUBJECT.Science]: 'parent.overview.subjects.science',
  [OVERVIEW_SUBJECT.Arabic]: 'parent.overview.subjects.arabic',
  [OVERVIEW_SUBJECT.English]: 'parent.overview.subjects.english',
};

/** Per-subject solid mastery-bar accent (capture uses distinct colors). */
const SUBJECT_ACCENT: Record<OverviewSubjectKey, MasteryBarProps['accent']> = {
  [OVERVIEW_SUBJECT.Math]: '$primary',
  [OVERVIEW_SUBJECT.Science]: '$success',
  [OVERVIEW_SUBJECT.Arabic]: '$purple',
  [OVERVIEW_SUBJECT.English]: '$accent',
};

export function SubjectMasteryCard({ childId, direction }: SubjectMasteryCardProps) {
  const { t } = useTranslation();
  const rows = getSubjectMasteryStub(childId);

  return (
    <Stack
      borderRadius="$card"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$border"
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
        >
          {t('parent.overview.subjectMastery.title')}
        </Text>
        <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
          {t('parent.overview.subjectMastery.subtitle')}
        </Text>
      </Stack>

      {/* Rows — MasteryBar renders the subject label + % caption above its bar. */}
      <Stack flexDirection="column" gap={14}>
        {rows.map((row) => {
          const label = t(SUBJECT_LABEL_KEY[row.subject]);
          return (
            <MasteryBar
              key={row.subject}
              value={row.percent}
              asPercent
              label={label}
              direction={direction}
              accent={SUBJECT_ACCENT[row.subject]}
              accessibilityLabel={`${label} ${row.percent}%`}
            />
          );
        })}
      </Stack>
    </Stack>
  );
}

SubjectMasteryCard.displayName = 'SubjectMasteryCard';
