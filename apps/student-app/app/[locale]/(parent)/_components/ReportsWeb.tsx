/**
 * ReportsWeb — the A4 parent Reports page body (CO-FE-1 / P1-11-FE-9,
 * design spec `design-system/ui_kits/parent-dashboard/A4-reports.md`).
 *
 * Composition (§1): page header (title + range select + Send Report stub) →
 * KPI row (4-up) → "Last 20 days · XP earned" chart SLOT → 2-col
 * [skills mastery | "Time of day" chart SLOT] → Recent attempts panel (A5).
 * Charts are DEFERRED to P5-05 — slots reserve the space, nothing is faked.
 *
 * REAL data only: KPIs (time / lessons / accuracy) derive from the active
 * child's attempts via `useStudentAttempts` (range-filtered client-side,
 * §2.1). Honest gaps per spec:
 *   - G-2: no parent-readable child XP → the XP tile shows "—" + sub copy.
 *   - G-1: no per-subject mastery endpoint → the mastery panel renders its
 *     empty state (bars at 0). Never fabricated.
 *   - L6: "Send Report" fires a local toast (Settings notice pattern) + a 2s
 *     button cooldown. No network call until P5-04 / Phase-9.
 *
 * Active child comes from the shell ChildSwitcher (`activeChildStore`) — the
 * page has no child picker and never takes a student id from the route/URL.
 * Attempt-fetch failures (incl. 403/404) render ONE generic error strip — no
 * IDOR leakage. RTL + ar/en; tokens only (spec pixel literals allowed).
 */
import {
  ATTEMPT_STATUS,
  useMyChildren,
  useStudentAttempts,
  type AttemptListItemDto,
} from '@learnexia/api-client';
import { type Direction } from '@learnexia/shared/i18n';
import { Button, MasteryBar, Select, type MasteryBarProps } from '@learnexia/ui';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '@/hooks/useLocale';
import { useActiveChildStore } from '@/providers/activeChildStore';
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

/** Send-Report stub timings (L6): 2s button cooldown, toast visible 4s. */
const SEND_COOLDOWN_MS = 2000;
const SEND_TOAST_MS = 4000;

/** Empty-value placeholder glyph mandated by the spec ("—", §2.2/§2.5). */
const EMPTY_VALUE = '—';

/** Subject label keys — reuse the existing Overview subject copy (4 subjects). */
const SUBJECT_LABEL_KEY: Record<OverviewSubjectKey, string> = {
  [OVERVIEW_SUBJECT.Math]: 'parent.overview.subjects.math',
  [OVERVIEW_SUBJECT.Science]: 'parent.overview.subjects.science',
  [OVERVIEW_SUBJECT.Arabic]: 'parent.overview.subjects.arabic',
  [OVERVIEW_SUBJECT.English]: 'parent.overview.subjects.english',
};

/** Per-subject mastery-bar accents (A4 §2.3 / Design Gap GAP-03 precedent). */
const SUBJECT_ACCENT: Record<OverviewSubjectKey, MasteryBarProps['accent']> = {
  [OVERVIEW_SUBJECT.Math]: '$subjectMathFg',
  [OVERVIEW_SUBJECT.Science]: '$subjectScienceFg',
  [OVERVIEW_SUBJECT.Arabic]: '$subjectArabicFg',
  [OVERVIEW_SUBJECT.English]: '$subjectEnglishFg',
};

const SUBJECT_KEYS = Object.values(OVERVIEW_SUBJECT);

export function ReportsWeb() {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const rowDir: 'row' | 'row-reverse' = isRtl ? 'row-reverse' : 'row';

  const childrenQuery = useMyChildren();
  const children = childrenQuery.data ?? [];
  const activeChildId = useActiveChildStore((s) => s.activeChildId);
  const openAddChild = useActiveChildStore((s) => s.openAddChild);

  // Active child from the shell switcher (never a route/user-supplied id).
  const activeChild =
    (activeChildId ? children.find((c) => String(c.id) === activeChildId) : null) ??
    children[0];
  const childName = activeChild?.fullName ?? '';

  const attemptsQuery = useStudentAttempts(activeChild?.id);

  const [range, setRange] = useState<ReportRangeValue>(REPORT_RANGE.Week);

  // Send Report stub (L6): toast + 2s cooldown, no network call.
  const [toastVisible, setToastVisible] = useState(false);
  const [sendCooldown, setSendCooldown] = useState(false);
  const timersRef = useRef<ReturnType<typeof setTimeout>[]>([]);
  useEffect(
    () => () => {
      timersRef.current.forEach(clearTimeout);
    },
    [],
  );
  const handleSendReport = () => {
    if (sendCooldown) return;
    setToastVisible(true);
    setSendCooldown(true);
    timersRef.current.push(setTimeout(() => setSendCooldown(false), SEND_COOLDOWN_MS));
    timersRef.current.push(setTimeout(() => setToastVisible(false), SEND_TOAST_MS));
  };

  const isLoading =
    childrenQuery.isLoading || (activeChild != null && attemptsQuery.isLoading);
  const hasChildren = !childrenQuery.isLoading && children.length > 0;
  const attempts = attemptsQuery.data ?? [];
  /** Zero attempts EVER → first-week page state (§2.5); hides the A5 panel. */
  const isFirstWeek =
    activeChild != null && attemptsQuery.isSuccess && attempts.length === 0;

  const windows = splitByRange(attempts, range);

  return (
    <Stack testID="reports-root" flexDirection="column" gap="$6" padding="$6" width="100%">
      {/* ---- Page header (§2.1) ---- */}
      <Stack
        testID="reports-header"
        flexDirection={rowDir}
        alignItems="flex-start"
        justifyContent="space-between"
        gap="$4"
        flexWrap="wrap"
        paddingBottom={20}
        borderBottomWidth={1}
        borderBottomColor="$borderSubtle"
      >
        <Stack flexDirection="column" gap={2}>
          <Text
            color="$fg1"
            fontSize={20}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
          >
            {activeChild
              ? t('parent.reports.title', { name: childName })
              : t('parent.nav.reports')}
          </Text>
          <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
            {t('parent.reports.subtitle')}
          </Text>
        </Stack>

        <Stack flexDirection={rowDir} alignItems="center" gap={10} flexWrap="wrap">
          <Stack width={150}>
            <Select
              label={t('parent.reports.range.label')}
              hideLabel
              size="sm"
              value={range}
              onChange={(v) => setRange(v as ReportRangeValue)}
              options={[
                { value: REPORT_RANGE.Week, label: t('parent.reports.range.week') },
                { value: REPORT_RANGE.Month, label: t('parent.reports.range.month') },
                { value: REPORT_RANGE.All, label: t('parent.reports.range.all') },
              ]}
              direction={direction}
              accessibilityLabel={t('parent.reports.range.label')}
              testID="reports-range-select"
            />
          </Stack>
          <Button
            variant="primary"
            size="sm"
            disabled={sendCooldown}
            accessibilityLabel={t('parent.reports.send')}
            onPress={handleSendReport}
            testID="reports-send-button"
          >
            {t('parent.reports.send')}
          </Button>
        </Stack>
      </Stack>

      {/* ---- Send-Report toast (L6 stub — Settings notice pattern) ---- */}
      {toastVisible ? (
        <Stack
          testID="reports-send-toast"
          backgroundColor="$successSoft"
          borderRadius="$sm"
          padding={12}
          accessibilityLiveRegion="polite"
        >
          <Text
            color="$success"
            fontSize={14}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={isRtl ? 'right' : 'left'}
          >
            {t('parent.reports.sendToast')}
          </Text>
        </Stack>
      ) : null}

      {/* ---- Page states ---- */}
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
        /* No children — defer to the shell's add-child path (§2.5). */
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
        /* Generic error strip for ALL failures incl. 403/404 — no IDOR leak. */
        <ErrorStrip
          message={t('parent.reports.loadError')}
          retryLabel={t('common.retry')}
          onRetry={() => attemptsQuery.refetch()}
          rowDir={rowDir}
          direction={direction}
        />
      ) : (
        <>
          {/* First-week friendly band (§2.5) — no CTA (parents can't start lessons). */}
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

          {/* ---- KPI row (§2.2) ---- */}
          <KpiRow
            windows={windows}
            range={range}
            rowDir={rowDir}
            direction={direction}
            locale={locale}
          />

          {/* ---- Chart slot: "Last 20 days · XP earned" (§2.4, P5-05) ---- */}
          <ChartPlaceholderPanel
            title={t('parent.reports.charts.xpTitle')}
            body={t('parent.reports.charts.comingSoon')}
            direction={direction}
            borderRadius="$modal"
            testID="reports-chart-slot-xp"
          />

          {/* ---- 2-col: skills mastery + "Time of day" slot (§1) ---- */}
          <Stack flexDirection={rowDir} gap="$6" flexWrap="wrap" alignItems="flex-start">
            <Stack flex={1} minWidth={320}>
              <SkillsMasteryPanel direction={direction} />
            </Stack>
            <Stack flex={1} minWidth={320}>
              <ChartPlaceholderPanel
                title={t('parent.reports.charts.todTitle')}
                body={t('parent.reports.charts.comingSoon')}
                direction={direction}
                borderRadius="$card"
                testID="reports-chart-slot-tod"
              />
            </Stack>
          </Stack>

          {/* ---- Recent attempts (A5 parent surface) — hidden first-week ---- */}
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
  );
}

/* ------------------------------------------------------------------ */
/* KPI row (§2.2) — real attempt-derived values; honest gaps           */
/* ------------------------------------------------------------------ */

interface KpiTileDef {
  key: string;
  icon: string;
  chipBg: StackProps['backgroundColor'];
  label: string;
  value: string;
  /** Delta line text (12/700) or muted sub line; tone picks the color. */
  subText?: string;
  subTone: 'success' | 'muted';
}

interface KpiRowProps {
  windows: ReturnType<typeof splitByRange>;
  range: ReportRangeValue;
  rowDir: 'row' | 'row-reverse';
  direction: Direction;
  locale: string;
}

function KpiRow({ windows, range, rowDir, direction, locale }: KpiRowProps) {
  const { t } = useTranslation();
  const { current, previous } = windows;

  const completed = current.filter((a) => a.status === ATTEMPT_STATUS.Completed);
  const prevCompleted = previous.filter(
    (a) => a.status === ATTEMPT_STATUS.Completed,
  );

  const sumSeconds = (list: AttemptListItemDto[]) =>
    list.reduce((acc, a) => acc + (a.durationSeconds ?? 0), 0);
  const meanAccuracy = (list: AttemptListItemDto[]): number | null =>
    list.length === 0
      ? null
      : list.reduce((acc, a) => acc + (a.accuracyPercentage ?? 0), 0) / list.length;

  const totalSeconds = sumSeconds(current);
  const lessonsDone = completed.length;
  const avgAccuracy = meanAccuracy(completed);

  const hasActivity = current.length > 0;
  // Deltas only for the week range vs the previous week, and only when the
  // previous window has data (first week omits the line, §2.2).
  const withDeltas = range === REPORT_RANGE.Week && previous.length > 0;

  const deltaText = (value: number | null): string | undefined =>
    value == null
      ? undefined
      : t('parent.reports.delta.vsLastWeek', {
          value: formatSignedNumber(value, locale),
        });

  const prevSeconds = sumSeconds(previous);
  const timeDelta =
    withDeltas && prevSeconds > 0
      ? ((totalSeconds - prevSeconds) / prevSeconds) * 100
      : null;
  const lessonsDelta =
    withDeltas && prevCompleted.length > 0
      ? ((lessonsDone - prevCompleted.length) / prevCompleted.length) * 100
      : null;
  const prevAccuracy = meanAccuracy(prevCompleted);
  const accuracyDelta =
    withDeltas && prevAccuracy != null && avgAccuracy != null
      ? avgAccuracy - prevAccuracy // percentage-point diff
      : null;

  const noActivitySub = hasActivity ? undefined : t('parent.reports.kpi.noActivity');

  const tiles: KpiTileDef[] = [
    {
      key: 'time',
      icon: '⏱️',
      chipBg: '$primarySoft',
      label: t('parent.reports.kpi.time'),
      value: hasActivity ? formatHoursMinutes(totalSeconds, locale) : EMPTY_VALUE,
      subText: deltaText(timeDelta) ?? noActivitySub,
      subTone: timeDelta != null && timeDelta >= 0 ? 'success' : 'muted',
    },
    {
      // G-2: no parent-readable child XP endpoint — honest placeholder.
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
      value: hasActivity ? formatNumber(lessonsDone, locale) : EMPTY_VALUE,
      subText: deltaText(lessonsDelta) ?? noActivitySub,
      subTone: lessonsDelta != null && lessonsDelta >= 0 ? 'success' : 'muted',
    },
    {
      key: 'accuracy',
      icon: '🎯',
      chipBg: '$streakSoft',
      label: t('parent.reports.kpi.accuracy'),
      value: avgAccuracy != null ? formatPercent(avgAccuracy, locale) : EMPTY_VALUE,
      subText: deltaText(accuracyDelta) ?? noActivitySub,
      subTone: accuracyDelta != null && accuracyDelta >= 0 ? 'success' : 'muted',
    },
  ];

  return (
    <Stack
      testID="reports-kpi-region"
      flexDirection={rowDir}
      flexWrap="wrap"
      gap={14}
      alignItems="stretch"
    >
      {tiles.map((tile) => (
        <Stack
          key={tile.key}
          flex={1}
          minWidth={200}
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
            {/* 32×32 tinted icon chip, radius 10 (spec literal). */}
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
              color={tile.subTone === 'success' ? '$success' : '$fg3'}
              fontSize={12}
              fontWeight="700"
              fontFamily="$heading"
              writingDirection={direction}
            >
              {tile.subText}
            </Text>
          ) : null}
          {/* a11y: one composed label per tile (Overview precedent). */}
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
/* Skills mastery panel (§2.3) — EMPTY state until P5-05 (gap G-1)     */
/* ------------------------------------------------------------------ */

function SkillsMasteryPanel({ direction }: { direction: Direction }) {
  const { t } = useTranslation();
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
        {/* G-1: no per-subject mastery aggregate yet — honest empty copy. */}
        <Text color="$fg4" fontSize={12} fontFamily="$body" writingDirection={direction}>
          {t('parent.reports.mastery.empty')}
        </Text>
      </Stack>

      <Stack flexDirection="column" gap={14}>
        {SUBJECT_KEYS.map((subject) => {
          const label = t(SUBJECT_LABEL_KEY[subject]);
          return (
            <MasteryBar
              key={subject}
              value={0}
              asPercent
              label={label}
              direction={direction}
              accent={SUBJECT_ACCENT[subject]}
              accessibilityLabel={`${label} 0%`}
            />
          );
        })}
      </Stack>
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* Chart placeholder panels (§2.4) — P5-05 slots, build nothing         */
/* ------------------------------------------------------------------ */

// TODO(P5-05): replace the placeholder bodies with the real 20-day XP and
// time-of-day charts once the parent analytics aggregates land.
function ChartPlaceholderPanel({
  title,
  body,
  direction,
  borderRadius,
  testID,
}: {
  title: string;
  body: string;
  direction: Direction;
  borderRadius: StackProps['borderRadius'];
  testID?: string;
}) {
  return (
    <Stack
      testID={testID}
      backgroundColor="$card"
      borderRadius={borderRadius}
      borderWidth={1}
      borderColor="$borderSubtle"
      padding={22}
      minHeight={200}
      gap="$4"
      width="100%"
    >
      <Text
        color="$fg1"
        fontSize={16}
        fontWeight="800"
        fontFamily="$heading"
        accessibilityRole="header"
        writingDirection={direction}
      >
        {title}
      </Text>
      <Stack flex={1} alignItems="center" justifyContent="center" gap="$3">
        <Text
          color="$fg4"
          fontSize={13}
          fontWeight="600"
          fontFamily="$body"
          writingDirection={direction}
        >
          {body}
        </Text>
        {/* Skeleton hint bands (8px ×3, $cardSoft) — visual slot marker. */}
        <Stack width="70%" gap="$2" accessibilityElementsHidden>
          <Stack height={8} borderRadius="$pill" backgroundColor="$cardSoft" width="100%" />
          <Stack height={8} borderRadius="$pill" backgroundColor="$cardSoft" width="80%" />
          <Stack height={8} borderRadius="$pill" backgroundColor="$cardSoft" width="55%" />
        </Stack>
      </Stack>
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* Loading + error states (§2.5)                                       */
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
