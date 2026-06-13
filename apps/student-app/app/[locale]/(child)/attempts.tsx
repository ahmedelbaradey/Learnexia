/**
 * "My activity" — child attempt-history push screen (A5 child surface,
 * carryover batch 2d / CO-FE-7, design spec
 * `design-system/ui_kits/student-app/A5-attempt-history.md` §2).
 *
 * Route: (child)/attempts — registered `href: null` in `(child)/_layout.tsx`
 * (NOT a tab). It is not in the ChildTabBar allowlist, so the bar auto-hides
 * here per the shell mechanism — no `CHILD_TAB_BAR_CLEARANCE` padding needed.
 * Entry point: the Home-dashboard "My activity" link-row is wired by batch 3c
 * (B-int owns `(child)/index.tsx`); until then the route is reachable by URL.
 *
 * Security-UX (spec §4): the student id is ALWAYS the signed-in child's own id
 * from `useMe()` (JWT-resolved) — never a route param, never user-supplied.
 * Any 403/404 from the server surfaces as the one generic error state (the
 * hook never retry-loops a denial); nothing leaks whether other ids exist.
 *
 * Rows = shared `@learnexia/ui` `AttemptRow` — child variant: the hints chip
 * is OMITTED (parent surface only, spec §1). Score colors are child-safe
 * inside AttemptRow (never red). Static screen — no animation, reduced-motion
 * safe by construction.
 *
 * Lesson-name gap (spec G-1): the DTO carries `lessonId` only — rows render
 * the localized "Lesson {n}" fallback.
 *
 * Locale formatting helpers are local pure functions (Eastern-Arabic numerals
 * via Intl, fixed unit glyphs — the sanctioned `DURATION_UNITS` pattern). They
 * mirror `(parent)/_components/reportsFormat.ts`, which this surface must not
 * import (parent-owned file set); lifting both into `packages/shared` is a
 * noted carry-forward.
 */
import { useLocalizedRouter } from '@/hooks/useLocalizedRouter';
import {
  ATTEMPT_STATUS,
  useMe,
  useStudentAttempts,
  type AttemptListItemDto,
} from '@learnexia/api-client';
import { AttemptRow, Button } from '@learnexia/ui';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import React, { useMemo, useState } from 'react';
import { Pressable, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '@/hooks/useLocale';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

/** Client-side soft pagination — endpoint is unpaged today (spec G-3). */
const VISIBLE_LIMIT = 50;
/** Loading state — 4 skeleton rows (spec §1 row-list states). */
const SKELETON_ROW_COUNT = 4;
const DAY_MS = 86_400_000;
/** Rolling "this week" window for the optional group headers (spec §2.4). */
const WEEK_MS = 7 * DAY_MS;

/* ------------------------------------------------------------------ */
/* Locale-aware formatting (local pure helpers — see header note)      */
/* ------------------------------------------------------------------ */

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

/** Fixed percent glyphs per locale (unit symbols, not copy). */
const PERCENT_SIGN = { en: '%', ar: '٪' } as const;

/** "84%" / "٨٤٪". */
function formatPercent(value: number, locale: string): string {
  const sign = locale === 'ar' ? PERCENT_SIGN.ar : PERCENT_SIGN.en;
  return `${formatNumber(Math.round(value), locale)}${sign}`;
}

/** Fixed duration-unit glyphs per locale (sanctioned formatter pattern). */
const DURATION_UNITS = {
  en: { minute: 'm', second: 's' },
  ar: { minute: 'د', second: 'ث' },
} as const;

/** Attempt-row duration: "3m 20s" / "٣د ٢٠ث" (seconds only when < 1m). */
function formatMinutesSeconds(totalSeconds: number, locale: string): string {
  const units = locale === 'ar' ? DURATION_UNITS.ar : DURATION_UNITS.en;
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = Math.round(totalSeconds % 60);
  const fmt = (n: number) => formatNumber(n, locale);
  if (minutes <= 0) return `${fmt(seconds)}${units.second}`;
  return `${fmt(minutes)}${units.minute} ${fmt(seconds)}${units.second}`;
}

/** "14:05" with locale digits. */
function formatTimeOfDay(timestamp: number, locale: string): string {
  return new Intl.DateTimeFormat(intlLocale(locale), {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(new Date(timestamp));
}

/** Short date "12 Jun" / "١٢ يونيو". */
function formatShortDate(timestamp: number, locale: string): string {
  return new Intl.DateTimeFormat(intlLocale(locale), {
    day: 'numeric',
    month: 'short',
  }).format(new Date(timestamp));
}

/**
 * `startedAt` epoch ms. NSwag types it as `Date` but the runtime value is the
 * raw ISO string (no jsonParseReviver) — normalize defensively.
 */
function attemptTimestamp(attempt: AttemptListItemDto): number {
  const raw = attempt.startedAt as unknown;
  if (raw == null) return Number.NaN;
  const date = raw instanceof Date ? raw : new Date(raw as string);
  return date.getTime();
}

/** Calendar-day equality (local time). */
function isSameDay(a: number, b: number): boolean {
  const da = new Date(a);
  const db = new Date(b);
  return (
    da.getFullYear() === db.getFullYear() &&
    da.getMonth() === db.getMonth() &&
    da.getDate() === db.getDate()
  );
}

/* ------------------------------------------------------------------ */
/* Small presentational pieces                                         */
/* ------------------------------------------------------------------ */

/** Summary-strip accent tokens per cell (spec §2.3). */
type SummaryCellColor = '$primaryLight' | '$success' | '$xp';

/** Summary-strip cell: value 20/900 tabular + 10/700 uppercase label (§2.3). */
function SummaryCell({
  value,
  label,
  color,
}: {
  value: string;
  label: string;
  color: SummaryCellColor;
}) {
  return (
    <YStack flex={1} alignItems="center" gap={2}>
      <Text
        color={color}
        fontSize={20}
        fontWeight="900"
        fontFamily="$heading"
        style={{ fontVariant: ['tabular-nums'] }}
      >
        {value}
      </Text>
      <Text
        color="$fg3"
        fontSize={10}
        fontWeight="700"
        fontFamily="$heading"
        textTransform="uppercase"
        numberOfLines={1}
      >
        {label}
      </Text>
    </YStack>
  );
}

/** Group header: 12/700 uppercase tracking 0.04em `$fg3` (§2.4). */
function GroupHeader({
  label,
  direction,
}: {
  label: string;
  direction: 'ltr' | 'rtl';
}) {
  return (
    <Text
      color="$fg3"
      fontSize={12}
      fontWeight="700"
      fontFamily="$heading"
      textTransform="uppercase"
      letterSpacing={0.04 * 12}
      writingDirection={direction}
      accessibilityRole="header"
    >
      {label}
    </Text>
  );
}

/* ------------------------------------------------------------------ */
/* Screen                                                              */
/* ------------------------------------------------------------------ */

export default function MyActivityScreen() {
  const { t } = useTranslation();
  const { locale, direction, isRtl } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useLocalizedRouter();
  const [showAll, setShowAll] = useState(false);

  // The child's OWN id — resolved from the JWT-backed /Users/Me projection.
  // Never a route param / user input (spec §4 security-UX; IDOR audit support).
  const meQuery = useMe();
  const studentId = meQuery.data?.id;

  const attemptsQuery = useStudentAttempts(studentId);

  const sorted = useMemo(
    () =>
      [...(attemptsQuery.data ?? [])].sort(
        (a, b) => attemptTimestamp(b) - attemptTimestamp(a),
      ),
    [attemptsQuery.data],
  );

  // Summary strip values (derived client-side from the fetched list, §2.3).
  const completedCount = sorted.filter(
    (a) => a.status === ATTEMPT_STATUS.Completed,
  ).length;
  const bestScore = sorted.reduce(
    (best, a) => Math.max(best, a.accuracyPercentage ?? 0),
    0,
  );

  const visible = showAll ? sorted : sorted.slice(0, VISIBLE_LIMIT);
  const hasMore = !showAll && sorted.length > VISIBLE_LIMIT;

  const now = Date.now();
  const thisWeek = visible.filter((a) => attemptTimestamp(a) >= now - WEEK_MS);
  const earlier = visible.filter(
    (a) => !(attemptTimestamp(a) >= now - WEEK_MS),
  );
  // Group headers only when both groups exist (they're optional per §2.4).
  const showGroups = thisWeek.length > 0 && earlier.length > 0;

  const isLoading = meQuery.isPending || attemptsQuery.isPending;
  const isError = meQuery.isError || attemptsQuery.isError;

  /** Generic retry — identical for any failure incl. 403/404 (no IDOR leak). */
  const handleRetry = () => {
    if (meQuery.isError) void meQuery.refetch();
    else void attemptsQuery.refetch();
  };

  const handleBack = () => {
    if (router.canGoBack()) router.back();
    else router.replace('/(child)/' as `/${string}`);
  };

  const backChevron = isRtl ? '→' : '←';

  /** "Today · 14:05" / "Yesterday" / "12 Jun" sub-line (spec §1). */
  const sublineFor = (timestamp: number): string => {
    if (Number.isNaN(timestamp)) return '';
    if (isSameDay(timestamp, now)) {
      return `${t('child.attempts.row.today')} · ${formatTimeOfDay(timestamp, locale)}`;
    }
    if (isSameDay(timestamp, now - DAY_MS)) {
      return t('child.attempts.row.yesterday');
    }
    return formatShortDate(timestamp, locale);
  };

  const renderRow = (attempt: AttemptListItemDto) => {
    const ts = attemptTimestamp(attempt);
    const completed = attempt.status === ATTEMPT_STATUS.Completed;
    const accuracy = attempt.accuracyPercentage ?? 0;
    const title = t('child.attempts.row.lessonFallback', {
      n: formatNumber(attempt.lessonId ?? 0, locale),
    });
    const subline = sublineFor(ts);
    const scoreText = formatPercent(accuracy, locale);
    const timeText = formatMinutesSeconds(attempt.durationSeconds ?? 0, locale);
    const statusLabel = completed
      ? t('child.attempts.row.completed')
      : t('child.attempts.row.inProgress');

    return (
      <AttemptRow
        key={attempt.id ?? `${attempt.lessonId}-${ts}`}
        completed={completed}
        title={title}
        subline={subline}
        accuracyPercent={accuracy}
        scoreText={scoreText}
        timeText={timeText}
        // Child surface: NO hintsText — the chip is parent-only (spec §1).
        direction={direction}
        accessibilityLabel={`${title}, ${statusLabel}, ${subline}, ${scoreText}, ${timeText}`}
        testID="child-attempt-row"
      />
    );
  };

  return (
    <TamStack flex={1} backgroundColor="$bg" testID="child-attempts-screen">
      {/* Top safe area */}
      <TamStack height={insets.top} />

      {/* ScreenHeader — back affordance + H1 (W12 pattern, spec §2.1). */}
      <XStack
        flexDirection={isRtl ? 'row-reverse' : 'row'}
        alignItems="center"
        height={56}
        paddingHorizontal="$4"
        gap="$3"
      >
        <Pressable
          onPress={handleBack}
          accessibilityRole="button"
          accessibilityLabel={t('child.attempts.back')}
          testID="attempts-back"
          style={{
            minWidth: 48,
            minHeight: 48,
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Text color="$fg2" fontSize={22} fontFamily="$body">
            {backChevron}
          </Text>
        </Pressable>
        <TamStack flex={1} />
        <TamStack width={48} />
      </XStack>

      <ScrollView
        contentContainerStyle={{
          paddingHorizontal: 24,
          paddingTop: 8,
          paddingBottom: insets.bottom + 24,
        }}
        showsVerticalScrollIndicator={false}
      >
        {/* ≥768 centers at maxWidth 720 (W13 precedent, spec §2). */}
        <YStack width="100%" maxWidth={720} alignSelf="center" gap="$4">
          {/* H1 + encouragement sub-line (spec §2.1–2.2). */}
          <YStack gap={6}>
            <Text
              color="$fg1"
              fontSize={24}
              fontWeight="800"
              fontFamily="$heading"
              writingDirection={direction}
              accessibilityRole="header"
            >
              {t('child.attempts.title')}
            </Text>
            <Text
              color="$fg2"
              fontSize={14}
              fontFamily="$body"
              writingDirection={direction}
            >
              {t('child.attempts.sub')}
            </Text>
          </YStack>

          {isLoading ? (
            /* Loading — 4 skeleton rows (spec §1 states). */
            <YStack testID="attempts-loading" gap={10}>
              {Array.from({ length: SKELETON_ROW_COUNT }, (_, i) => (
                <TamStack
                  key={i}
                  height={64}
                  borderRadius="$card"
                  backgroundColor="$cardSoft"
                  opacity={0.6}
                />
              ))}
            </YStack>
          ) : isError ? (
            /* Generic error strip — identical for network/403/404 (spec §4). */
            <YStack
              testID="attempts-error"
              backgroundColor="$dangerSoft"
              borderRadius="$card"
              padding="$4"
              gap="$3"
              alignItems="center"
            >
              <Text
                color="$fg1"
                fontSize={14}
                fontWeight="700"
                fontFamily="$body"
                textAlign="center"
                writingDirection={direction}
              >
                {`⚠️ ${t('child.attempts.error')}`}
              </Text>
              <Button
                variant="ghost"
                size="sm"
                accessibilityLabel={t('common.retry')}
                onPress={handleRetry}
                testID="attempts-retry"
              >
                {t('common.retry')}
              </Button>
            </YStack>
          ) : sorted.length === 0 ? (
            /* Empty — one primary action (spec §2 empty state). */
            <YStack
              testID="attempts-empty"
              alignItems="center"
              gap="$4"
              paddingVertical="$8"
              paddingHorizontal="$4"
            >
              <Text fontSize={48} opacity={0.4} accessibilityElementsHidden>
                ⭐
              </Text>
              <Text
                color="$fg1"
                fontSize={16}
                fontWeight="700"
                fontFamily="$heading"
                textAlign="center"
                writingDirection={direction}
              >
                {t('child.attempts.empty.title')}
              </Text>
              <Button
                variant="primary"
                size="md"
                accessibilityLabel={t('child.attempts.empty.cta')}
                onPress={() => router.replace('/(child)/' as `/${string}`)}
                testID="attempts-empty-cta"
              >
                {t('child.attempts.empty.cta')}
              </Button>
            </YStack>
          ) : (
            <>
              {/* Summary strip — Attempts · Completed · Best score (§2.3). */}
              <XStack
                testID="attempts-summary"
                backgroundColor="$card"
                borderRadius={16}
                borderWidth={1}
                borderColor="$borderSubtle"
                padding={12}
                flexDirection={isRtl ? 'row-reverse' : 'row'}
                accessible
                accessibilityLabel={`${t('child.attempts.summary.attempts')} ${formatNumber(sorted.length, locale)}, ${t('child.attempts.summary.completed')} ${formatNumber(completedCount, locale)}, ${t('child.attempts.summary.best')} ${formatPercent(bestScore, locale)}`}
              >
                <SummaryCell
                  value={formatNumber(sorted.length, locale)}
                  label={t('child.attempts.summary.attempts')}
                  color="$primaryLight"
                />
                <SummaryCell
                  value={formatNumber(completedCount, locale)}
                  label={t('child.attempts.summary.completed')}
                  color="$success"
                />
                <SummaryCell
                  value={formatPercent(bestScore, locale)}
                  label={t('child.attempts.summary.best')}
                  color="$xp"
                />
              </XStack>

              {/* Attempt list, newest first; optional week grouping (§2.4). */}
              <YStack
                gap={10}
                accessibilityRole="list"
                accessibilityLabel={t('child.attempts.listA11y', {
                  count: sorted.length,
                })}
              >
                {showGroups ? (
                  <>
                    <GroupHeader
                      label={t('child.attempts.group.thisWeek')}
                      direction={direction}
                    />
                    {thisWeek.map(renderRow)}
                    <GroupHeader
                      label={t('child.attempts.group.earlier')}
                      direction={direction}
                    />
                    {earlier.map(renderRow)}
                  </>
                ) : (
                  visible.map(renderRow)
                )}
              </YStack>

              {/* Soft client-side pagination (>50, spec §2.5 / G-3). */}
              {hasMore ? (
                <TamStack alignItems="center">
                  <Button
                    variant="ghost"
                    size="sm"
                    accessibilityLabel={t('child.attempts.showMore')}
                    onPress={() => setShowAll(true)}
                    testID="attempts-show-more"
                  >
                    {t('child.attempts.showMore')}
                  </Button>
                </TamStack>
              ) : null}
            </>
          )}
        </YStack>
      </ScrollView>
    </TamStack>
  );
}
