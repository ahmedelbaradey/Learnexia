/**
 * RecentAttemptsPanel — A5 parent surface: the child's attempt history as the
 * LAST section of the Reports page (design spec
 * `design-system/ui_kits/student-app/A5-attempt-history.md` §3). NOT a new
 * sidebar item — no `(parent)/_layout.tsx` nav edit (plan §3).
 *
 * Rows = shared `@learnexia/ui` `AttemptRow` (same component the child
 * "My activity" screen renders); the hints chip is ENABLED here (parent
 * surface only). Data = the page's already-range-filtered REAL attempts from
 * `useStudentAttempts` — this panel does no fetching of its own and never
 * receives a user-supplied student id (active child only, via the page).
 *
 * Shows max 8 rows + a ghost "Show all" expander (endpoint is unpaged, spec
 * gap G-3). Empty-in-range state per §3.4; the page hides the panel entirely
 * in the first-week (zero attempts ever) state.
 *
 * Lesson-name gap (A5 G-1): the DTO carries `lessonId` only — rows render the
 * localized "Lesson {n}" fallback. Backend carry-forward: add `lessonName`.
 */
import { ATTEMPT_STATUS, type AttemptListItemDto } from '@learnexia/api-client';
import { type Direction } from '@learnexia/shared/i18n';
import { AttemptRow, Button } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  attemptTimestamp,
  formatMinutesSeconds,
  formatNumber,
  formatPercent,
  formatShortDate,
  formatTimeOfDay,
  isSameDay,
} from './reportsFormat';

/** Rows shown before the "Show all" expander (A5 §3.3). */
const COLLAPSED_ROW_COUNT = 8;

const DAY_MS = 86_400_000;

export interface RecentAttemptsPanelProps {
  /** Attempts already filtered to the page's selected range, any order. */
  attempts: AttemptListItemDto[];
  childName: string;
  direction: Direction;
  locale: string;
}

export function RecentAttemptsPanel({
  attempts,
  childName,
  direction,
  locale,
}: RecentAttemptsPanelProps) {
  const { t } = useTranslation();
  const [expanded, setExpanded] = useState(false);

  const sorted = [...attempts].sort(
    (a, b) => attemptTimestamp(b) - attemptTimestamp(a),
  );
  const visible = expanded ? sorted : sorted.slice(0, COLLAPSED_ROW_COUNT);
  const hasMore = sorted.length > COLLAPSED_ROW_COUNT;

  const now = Date.now();

  /** "Today · 14:05" / "Yesterday" / "12 Jun" (A5 §1 sub-line). */
  const sublineFor = (timestamp: number): string => {
    if (Number.isNaN(timestamp)) return '';
    if (isSameDay(timestamp, now)) {
      return `${t('parent.reports.attempts.row.today')} · ${formatTimeOfDay(timestamp, locale)}`;
    }
    if (isSameDay(timestamp, now - DAY_MS)) {
      return t('parent.reports.attempts.row.yesterday');
    }
    return formatShortDate(timestamp, locale);
  };

  return (
    <Stack
      testID="reports-attempts-panel"
      backgroundColor="$card"
      borderRadius="$modal"
      borderWidth={1}
      borderColor="$borderSubtle"
      padding={22}
      gap="$4"
      width="100%"
    >
      {/* Panel header */}
      <Stack flexDirection="column" gap={6}>
        <Text
          color="$fg1"
          fontSize={16}
          fontWeight="800"
          fontFamily="$heading"
          accessibilityRole="header"
          writingDirection={direction}
        >
          {t('parent.reports.attempts.title')}
        </Text>
        <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
          {t('parent.reports.attempts.sub', { name: childName })}
        </Text>
      </Stack>

      {sorted.length === 0 ? (
        /* Empty in range (A5 §3.4) — the first-week page state hides the panel. */
        <Text
          color="$fg3"
          fontSize={13}
          fontFamily="$body"
          textAlign="center"
          paddingVertical="$4"
          writingDirection={direction}
        >
          {t('parent.reports.attempts.empty')}
        </Text>
      ) : (
        <>
          <Stack flexDirection="column" gap={10} accessibilityRole="list">
            {visible.map((attempt) => {
              const ts = attemptTimestamp(attempt);
              const completed = attempt.status === ATTEMPT_STATUS.Completed;
              const accuracy = attempt.accuracyPercentage ?? 0;
              const title = t('parent.reports.attempts.row.lessonFallback', {
                n: formatNumber(attempt.lessonId ?? 0, locale),
              });
              const subline = sublineFor(ts);
              const scoreText = formatPercent(accuracy, locale);
              const timeText = formatMinutesSeconds(
                attempt.durationSeconds ?? 0,
                locale,
              );
              const hintsCount = attempt.hintsUsedCount ?? 0;
              const hintsText =
                hintsCount > 0
                  ? t('parent.reports.attempts.row.hints', { count: hintsCount })
                  : undefined;
              const statusLabel = completed
                ? t('parent.reports.attempts.row.completed')
                : t('parent.reports.attempts.row.inProgress');

              return (
                <AttemptRow
                  key={attempt.id ?? `${attempt.lessonId}-${ts}`}
                  completed={completed}
                  title={title}
                  subline={subline}
                  accuracyPercent={accuracy}
                  scoreText={scoreText}
                  timeText={timeText}
                  hintsText={hintsText}
                  direction={direction}
                  accessibilityLabel={`${title}, ${statusLabel}, ${subline}, ${scoreText}, ${timeText}`}
                  testID="reports-attempt-row"
                />
              );
            })}
          </Stack>

          {hasMore ? (
            <Stack alignItems="center">
              <Button
                variant="ghost"
                size="sm"
                accessibilityLabel={
                  expanded
                    ? t('parent.reports.attempts.showLess')
                    : t('parent.reports.attempts.showAll')
                }
                onPress={() => setExpanded((e) => !e)}
              >
                {expanded
                  ? t('parent.reports.attempts.showLess')
                  : t('parent.reports.attempts.showAll')}
              </Button>
            </Stack>
          ) : null}
        </>
      )}
    </Stack>
  );
}

RecentAttemptsPanel.displayName = 'RecentAttemptsPanel';
