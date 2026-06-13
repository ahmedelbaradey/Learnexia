/**
 * AttemptRow — one attempt-history list row (A5, design spec
 * `design-system/ui_kits/student-app/A5-attempt-history.md` §1).
 *
 * Shared by BOTH surfaces: the child "My activity" screen and the parent
 * "Recent attempts" panel inside Reports. Composes existing primitives only
 * (house list-row chrome from `components-missions.html` + the
 * AttemptSummaryCard stat treatment) — not a new pattern.
 *
 * Anatomy (logical order; mirrors in RTL via `direction`):
 *   [status disc 40×40] [title / subline] … [hints chip?] [score] [time]
 *
 * Presentational: all text arrives already localized via props (no i18n in
 * `packages/ui`, consistent with AttemptSummaryCard). The score color band is
 * derived from `accuracyPercent`: ≥80 `$success` · 50–79 `$xp` · <50 `$fg3`
 * (never `$danger` — child-safe, identical for the parent render).
 *
 * Non-interactive this wave (no attempt-detail screen) — no chevron, hover
 * only brightens (`$cardSoft`), no scale.
 *
 * A11y: one composed `accessibilityLabel` per row (caller composes it from
 * i18n, e.g. "Lesson 12, completed, today 14:05, 84 percent, 3m 20s").
 */
import { type Direction } from '@learnexia/shared/i18n';
import React from 'react';

import { XStack, YStack, Text } from '../../internal/primitives';

/** Score color bands (spec §1) — child-safe, no red. */
const SCORE_BAND = {
  high: 80,
  mid: 50,
} as const;

export interface AttemptRowProps {
  /** Completed → ✓ disc; otherwise the neutral "…" disc (non-shaming). */
  completed: boolean;
  /** Lesson name or localized "Lesson {n}" fallback. */
  title: string;
  /** Relative date + time line, already localized ("Today · 14:05"). */
  subline: string;
  /** 0..100 — drives the score color band. */
  accuracyPercent: number;
  /** Formatted score, localized digits ("84%" / "٨٤٪"). */
  scoreText: string;
  /** Formatted duration, localized digits ("3m 20s" / "٣د ٢٠ث"). Rendered LTR. */
  timeText: string;
  /** Optional hints chip text (parent surface only; hidden for kids). */
  hintsText?: string;
  direction?: Direction;
  /** Composed row label for screen readers. */
  accessibilityLabel: string;
  testID?: string;
}

export function AttemptRow({
  completed,
  title,
  subline,
  accuracyPercent,
  scoreText,
  timeText,
  hintsText,
  direction = 'ltr',
  accessibilityLabel,
  testID,
}: AttemptRowProps) {
  const isRtl = direction === 'rtl';
  const rowDir = isRtl ? 'row-reverse' : 'row';
  const textAlign = isRtl ? 'right' : 'left';

  const scoreColor =
    accuracyPercent >= SCORE_BAND.high
      ? '$success'
      : accuracyPercent >= SCORE_BAND.mid
        ? '$xp'
        : '$fg3';

  return (
    <XStack
      testID={testID}
      flexDirection={rowDir}
      alignItems="center"
      gap={14}
      minHeight={64}
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$borderSubtle"
      borderRadius="$card"
      paddingVertical={14}
      paddingHorizontal={16}
      hoverStyle={{ backgroundColor: '$cardSoft' }}
      accessible
      accessibilityRole="text"
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {/* Status disc — Completed ✓, otherwise neutral "…" (never red). */}
      <XStack
        width={40}
        height={40}
        borderRadius="$pill"
        backgroundColor={completed ? '$successSoft' : '$cardSoft'}
        alignItems="center"
        justifyContent="center"
        flexShrink={0}
        accessibilityElementsHidden
      >
        <Text
          color={completed ? '$success' : '$fg4'}
          fontSize={16}
          fontWeight="900"
          fontFamily="$heading"
        >
          {completed ? '✓' : '…'}
        </Text>
      </XStack>

      {/* Title + subline */}
      <YStack flex={1} gap={2} accessibilityElementsHidden>
        <Text
          color="$fg1"
          fontSize={15}
          fontWeight="700"
          fontFamily="$heading"
          textAlign={textAlign}
          writingDirection={direction}
          numberOfLines={1}
        >
          {title}
        </Text>
        <Text
          color="$fg3"
          fontSize={12}
          fontFamily="$body"
          textAlign={textAlign}
          writingDirection={direction}
          numberOfLines={1}
        >
          {subline}
        </Text>
      </YStack>

      {/* Trailing stats: [hints chip?] [score] [time] */}
      <XStack
        flexDirection={rowDir}
        alignItems="center"
        gap={12}
        flexShrink={0}
        accessibilityElementsHidden
      >
        {hintsText ? (
          <XStack
            borderRadius="$pill"
            backgroundColor="$warningSoft"
            paddingVertical={4}
            paddingHorizontal={10}
            alignItems="center"
          >
            <Text
              color="$warning"
              fontSize={11}
              fontWeight="700"
              fontFamily="$heading"
              writingDirection={direction}
            >
              {hintsText}
            </Text>
          </XStack>
        ) : null}

        <Text
          color={scoreColor}
          fontSize={16}
          fontWeight="800"
          fontFamily="$heading"
          style={{ fontVariant: ['tabular-nums'] }}
        >
          {scoreText}
        </Text>

        {/* Duration stays LTR regardless of locale (spec §1). */}
        <Text
          color="$fg3"
          fontSize={13}
          fontWeight="700"
          fontFamily="$heading"
          style={{ fontVariant: ['tabular-nums'] }}
          writingDirection="ltr"
        >
          {timeText}
        </Text>
      </XStack>
    </XStack>
  );
}

AttemptRow.displayName = 'AttemptRow';
