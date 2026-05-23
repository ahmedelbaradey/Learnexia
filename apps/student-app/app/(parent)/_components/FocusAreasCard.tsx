/**
 * FocusAreasCard — the "Areas to focus on" card on the parent Overview
 * (capture `web/05-dashboard.png`). Header ("Areas to focus on" + subtitle +
 * "See all" link) then one row per weak topic: an icon chip, the topic + its
 * subject, a small confidence bar, and the percentage.
 *
 * Topics / subjects / percentages are Phase-5 stubs (TODO(P5)); "See all" is a
 * no-op stub until analytics ship. The bar/percent tint is driven by a fixed
 * severity enum (high → danger, medium → warning), never a raw string.
 *
 * RTL + ar/en; tokens only (no raw hex).
 */
import { type Direction } from '@learnexia/shared/i18n';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import {
  FOCUS_SEVERITY,
  getFocusAreasStub,
  OVERVIEW_SUBJECT,
  type FocusSeverity,
  type OverviewSubjectKey,
} from './parentDashboardStubs';

export interface FocusAreasCardProps {
  childId: string;
  childName: string;
  direction: Direction;
  rowDir: 'row' | 'row-reverse';
}

/** Subject → i18n label key. */
const SUBJECT_LABEL_KEY: Record<OverviewSubjectKey, string> = {
  [OVERVIEW_SUBJECT.Math]: 'parent.overview.subjects.math',
  [OVERVIEW_SUBJECT.Science]: 'parent.overview.subjects.science',
  [OVERVIEW_SUBJECT.Arabic]: 'parent.overview.subjects.arabic',
  [OVERVIEW_SUBJECT.English]: 'parent.overview.subjects.english',
};

/** Topic key → i18n label key (reuses the existing myChildren topic copy). */
const TOPIC_LABEL_KEY: Record<string, string> = {
  fractions: 'parent.myChildren.topics.fractions',
  letters: 'parent.myChildren.topics.letters',
  geometry: 'parent.myChildren.topics.geometry',
  reading: 'parent.myChildren.topics.reading',
  numbers: 'parent.myChildren.topics.numbers',
};

type TextColor = StackProps['backgroundColor'];

/** Severity → fill color token. */
const SEVERITY_COLOR: Record<FocusSeverity, TextColor> = {
  [FOCUS_SEVERITY.High]: '$danger',
  [FOCUS_SEVERITY.Medium]: '$warning',
};

export function FocusAreasCard({ childId, childName, direction, rowDir }: FocusAreasCardProps) {
  const { t } = useTranslation();
  const rows = getFocusAreasStub(childId);

  return (
    <Stack
      borderRadius="$card"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$border"
      padding="$6"
      gap="$5"
    >
      {/* Header */}
      <Stack flexDirection={rowDir} alignItems="flex-start" justifyContent="space-between" gap="$3">
        <Stack flexDirection="column" gap="$1" flex={1}>
          <Text
            color="$fg1"
            fontSize={18}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('parent.overview.focusAreas.title')}
          </Text>
          <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
            {t('parent.overview.focusAreas.subtitle', { name: childName })}
          </Text>
        </Stack>
        {/* See all — Phase-5 stub (no-op until the full topics list ships). */}
        <Stack
          minHeight={36}
          paddingHorizontal="$3"
          justifyContent="center"
          borderRadius="$button"
          cursor="pointer"
          hoverStyle={{ backgroundColor: '$card' }}
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

      {/* Rows */}
      <Stack flexDirection="column" gap="$3">
        {rows.map((row) => {
          const topic = t(TOPIC_LABEL_KEY[row.topicKey] ?? 'parent.myChildren.topics.numbers');
          const subject = t(SUBJECT_LABEL_KEY[row.subject]);
          const fill = SEVERITY_COLOR[row.severity];
          return (
            <Stack
              key={`${row.topicKey}-${row.subject}`}
              flexDirection={rowDir}
              alignItems="center"
              gap="$3"
              borderRadius="$sm"
              backgroundColor="$bg"
              borderWidth={1}
              borderColor="$border"
              padding="$4"
              accessible
              accessibilityLabel={`${topic} ${subject} ${row.percent}%`}
              aria-label={`${topic} ${subject} ${row.percent}%`}
            >
              {/* Icon chip */}
              <Stack
                width={40}
                height={40}
                borderRadius="$sm"
                backgroundColor="$cardSoft"
                alignItems="center"
                justifyContent="center"
                accessibilityElementsHidden
              >
                <Text fontSize={18}>{'📘'}</Text>
              </Stack>

              {/* Topic + subject */}
              <Stack flexDirection="column" gap="$1" flex={1}>
                <Text color="$fg1" fontSize={15} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
                  {topic}
                </Text>
                <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
                  {subject}
                </Text>
              </Stack>

              {/* Confidence bar */}
              <Stack
                width={120}
                height={8}
                borderRadius={9999}
                backgroundColor="$cardSoft"
                overflow="hidden"
                flexDirection={rowDir}
                accessibilityElementsHidden
              >
                <Stack
                  width={`${Math.max(0, Math.min(100, row.percent))}%` as StackProps['width']}
                  height="100%"
                  borderRadius={9999}
                  backgroundColor={fill}
                />
              </Stack>

              {/* Percent */}
              <Text
                color={fill}
                fontSize={14}
                fontWeight="800"
                fontFamily="$heading"
                width={48}
                textAlign={direction === 'rtl' ? 'left' : 'right'}
                style={{ fontVariant: ['tabular-nums'] }}
              >
                {`${row.percent}%`}
              </Text>
            </Stack>
          );
        })}
      </Stack>
    </Stack>
  );
}

FocusAreasCard.displayName = 'FocusAreasCard';
