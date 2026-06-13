/**
 * OverviewWeb — the parent "Dashboard / Overview" main content (capture
 * `web/05-dashboard.png`): page header ("<Child>'s progress" + date range +
 * "This week" select + "Send Report"), four KPI stat cards, a "Daily activity"
 * card (header + "Export CSV" — the bar chart itself is DEFERRED to Phase 5 and
 * rendered as a placeholder), the "Subject mastery" card (4 product subjects),
 * and the "Areas to focus on" list + "Recommendations from Lexi" panel side-by-side.
 *
 * The active child comes from `useActiveChildStore` (set by the ChildSwitcher),
 * falling back to `children[0]`. Every weekly stat (KPIs / mastery / focus areas /
 * date range) is a Phase-5 stub (TODO(P5)). "Send Report" and "Export CSV" are
 * no-op stubs until analytics ship.
 *
 * RTL + ar/en throughout; tokens only (no raw hex); reuses `@learnexia/ui`
 * primitives (KPIStatCard, MasteryBar) — no new design pattern.
 */
import { useMyChildren } from '@learnexia/api-client';
import { Button, Select } from '@learnexia/ui';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { useActiveChildStore } from '../../../src/providers/activeChildStore';
import { DailyActivityCard } from './DailyActivityCard';
import { FocusAreasCard } from './FocusAreasCard';
import { RecommendationsCard } from './RecommendationsCard';
import { SubjectMasteryCard } from './SubjectMasteryCard';
import { getOverviewKpiStub, type OverviewKpiStub } from './parentDashboardStubs';

/** Fixed reporting-period set (enum-style; only "this week" wired in P1-11). */
const REPORTING_PERIOD = {
  ThisWeek: 'thisWeek',
} as const;

/**
 * Format minutes as "Xh Ym" (EN) / "Xس Yد" (AR). Digits are localized via Intl
 * (`ar-EG` → Eastern-Arabic numerals); the unit glyphs are fixed locale symbols
 * (h/m in EN, س/د in AR) per the align spec (M-25). These are unit symbols, not
 * free-text copy, so they live with the deferred duration formatter rather than
 * the i18n resource bundle (which only carries the surrounding delta phrase).
 */
const DURATION_UNITS = {
  en: { hour: 'h', minute: 'm' },
  ar: { hour: 'س', minute: 'د' },
} as const;

function formatDuration(totalMinutes: number, locale: string): string {
  const isAr = locale === 'ar';
  const fmt = (n: number) => new Intl.NumberFormat(isAr ? 'ar-EG' : 'en-US').format(n);
  const units = isAr ? DURATION_UNITS.ar : DURATION_UNITS.en;
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${fmt(hours)}${units.hour} ${fmt(minutes)}${units.minute}`;
}

function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(value);
}

interface KpiTileDef {
  icon: string;
  value: string;
  label: string;
  delta: string;
  /** 32×32 icon-chip tint, per accent (align spec accent-to-soft map B-03/N-06). */
  chipBg: StackProps['backgroundColor'];
}

function buildKpis(
  kpi: OverviewKpiStub,
  t: ReturnType<typeof useTranslation>['t'],
  locale: string,
): KpiTileDef[] {
  return [
    {
      icon: '⏱️',
      chipBg: '$primarySoft',
      value: formatDuration(kpi.timeLearningMinutes, locale),
      label: t('parent.overview.kpi.timeLearning'),
      delta: t('parent.overview.kpi.timeDelta', {
        value: formatDuration(kpi.timeLearningDeltaMinutes, locale),
      }),
    },
    {
      icon: '⭐',
      chipBg: '$xpSoft',
      value: formatNumber(kpi.xpEarned, locale),
      label: t('parent.overview.kpi.xpEarned'),
      delta: t('parent.overview.kpi.xpDelta', {
        value: formatNumber(kpi.xpDeltaPercent, locale),
      }),
    },
    {
      icon: '✅',
      chipBg: '$successSoft',
      value: formatNumber(kpi.lessonsDone, locale),
      label: t('parent.overview.kpi.lessonsDone'),
      delta: t('parent.overview.kpi.lessonsDelta', {
        value: formatNumber(kpi.lessonsDelta, locale),
      }),
    },
    {
      icon: '🔥',
      chipBg: '$streakSoft',
      value: formatNumber(kpi.streakDays, locale),
      label: t('parent.overview.kpi.streak'),
      delta: t('parent.overview.kpi.streakDelta', {
        value: formatNumber(kpi.streakDelta, locale),
      }),
    },
  ];
}

export interface OverviewWebProps {
  /** Called when the "Add child" CTA is tapped (empty state). Opens the AddChildModal. */
  onAddChild?: () => void;
}

export function OverviewWeb({ onAddChild }: OverviewWebProps) {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const query = useMyChildren();
  const [period, setPeriod] = useState<string>(REPORTING_PERIOD.ThisWeek);

  const rowDir = isRtl ? 'row-reverse' : 'row';
  const children = query.data ?? [];

  // Active child: from the child switcher store (fallback to first child).
  const activeChildId = useActiveChildStore((s) => s.activeChildId);
  const activeChild =
    (activeChildId ? children.find((c) => String(c.id) === activeChildId) : null) ??
    children[0];

  const childId = activeChild ? String(activeChild.id) : undefined;
  const childName = activeChild?.fullName ?? '';

  // Static "this week" range — the real range comes with Phase-5 analytics.
  const dateRange = t('parent.overview.dateRange');

  return (
    <Stack testID="overview-root" flexDirection="column" gap="$6" padding="$6" width="100%">
      {/* Header — direction-aware row (rowDir): title block on logical-start side,
           controls on logical-end side. In RTL rowDir=row-reverse so title appears
           on the right (leading) and controls on the left (trailing). */}
      <Stack testID="overview-header" flexDirection={rowDir} alignItems="flex-start" justifyContent="space-between" gap="$4" flexWrap="wrap">
        <Stack flexDirection="column" gap="$1">
          <Text
            color="$fg1"
            fontSize={22}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.overview.title', { name: childName })}
          </Text>
          <Text
            color="$fg3"
            fontSize={12}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {dateRange}
          </Text>
        </Stack>

        <Stack flexDirection={rowDir} alignItems="center" gap="$3">
          <Stack width={150}>
            <Select
              label={t('parent.overview.periodLabel')}
              hideLabel
              value={period}
              onChange={(v) => setPeriod(String(v))}
              options={[{ value: REPORTING_PERIOD.ThisWeek, label: t('parent.overview.periodThisWeek') }]}
              direction={direction}
              accessibilityLabel={t('parent.overview.periodLabel')}
            />
          </Stack>
          {/* Send Report — Phase-5 stub (no-op until analytics ship). */}
          <Button
            variant="primary"
            size="sm"
            accessibilityLabel={t('parent.overview.sendReport')}
            onPress={() => {
              /* TODO(P5-05): wire to the reports endpoint. */
            }}
          >
            {t('parent.overview.sendReport')}
          </Button>
        </Stack>
      </Stack>

      {/* Error state */}
      {query.isError ? (
        <Stack alignItems="center" gap="$4" paddingVertical="$8">
          <Text color="$fg3" fontSize={15} fontFamily="$body" textAlign="center" writingDirection={direction}>
            {t('parent.overview.loadError')}
          </Text>
          <Button variant="ghost" accessibilityLabel={t('common.retry')} onPress={() => query.refetch()}>
            {t('common.retry')}
          </Button>
        </Stack>
      ) : !query.isLoading && !activeChild ? (
        <Stack alignItems="center" gap="$4" paddingVertical="$8">
          <Text color="$fg3" fontSize={15} fontFamily="$body" textAlign="center" writingDirection={direction}>
            {t('parent.overview.empty')}
          </Text>
          <Button
            variant="primary"
            accessibilityLabel={t('parent.myChildren.addChild')}
            onPress={onAddChild ?? (() => {})}
          >
            {t('parent.myChildren.addChild')}
          </Button>
        </Stack>
      ) : (
        <OverviewBody childId={childId} childName={childName} rowDir={rowDir} direction={direction} locale={locale} />
      )}
    </Stack>
  );
}

interface OverviewBodyProps {
  childId?: string;
  childName: string;
  rowDir: 'row' | 'row-reverse';
  direction: 'ltr' | 'rtl';
  locale: string;
}

function OverviewBody({ childId, childName, rowDir, direction, locale }: OverviewBodyProps) {
  const { t } = useTranslation();
  // Deterministic per-child stubs (TODO(P5-05)). Loading skeleton: render with
  // a placeholder id so the layout stays stable until children resolve.
  const seed = childId ?? '0';
  const kpi = getOverviewKpiStub(seed);
  const kpis = buildKpis(kpi, t, locale);

  return (
    <>
      {/* 4 KPI cards (inline by decision GAP-04 — no new KPIStatCard variant). */}
      <Stack testID="overview-kpi-region" flexDirection={rowDir} flexWrap="wrap" gap={14} alignItems="stretch">
        {kpis.map((k) => (
          <Stack
            key={k.label}
            flex={1}
            minWidth={200}
            borderRadius="$card"
            backgroundColor="$card"
            borderWidth={1}
            borderColor="$border"
            padding="$5"
            gap="$2"
            hoverStyle={{ backgroundColor: '$cardSoft', scale: 1.02 }}
          >
            {/* Icon chip + label row — direction-aware: label on logical-start, chip on
                logical-end. flexDirection={rowDir} places chip on the trailing visual
                side in LTR (right) and the trailing visual side in RTL (left), matching
                the AR/EN pixel references (ar-web-kpi.html, web-kpi-row.html). */}
            <Stack flexDirection={rowDir} alignItems="center" justifyContent="space-between" gap="$2">
              <Text
                color="$fg3"
                fontSize={11}
                fontWeight="700"
                fontFamily="$heading"
                textTransform="uppercase"
                letterSpacing={0.88}
                writingDirection={direction}
                textAlign={direction === 'rtl' ? 'right' : 'left'}
                flex={1}
              >
                {k.label}
              </Text>
              {/* 32×32 radius-8 tinted icon chip, per accent (B-03/N-06). */}
              <Stack
                width={32}
                height={32}
                borderRadius="$sm"
                backgroundColor={k.chipBg}
                alignItems="center"
                justifyContent="center"
                accessibilityElementsHidden
              >
                <Text fontSize={18}>{k.icon}</Text>
              </Stack>
            </Stack>
            {/* Value — tabular numerals, locale-formatted; right-aligned in RTL. */}
            <Text
              color="$fg1"
              fontSize={28}
              fontWeight="800"
              fontFamily="$heading"
              style={{ fontVariant: ['tabular-nums'] }}
              writingDirection={direction}
              textAlign={direction === 'rtl' ? 'right' : 'left'}
            >
              {k.value}
            </Text>
            {/* Delta — right-aligned in RTL. */}
            <Text
              color="$success"
              fontSize={12}
              fontWeight="700"
              fontFamily="$heading"
              writingDirection={direction}
              textAlign={direction === 'rtl' ? 'right' : 'left'}
            >
              {k.delta}
            </Text>
            {/* a11y: one composed label per tile (label + value + delta) */}
            <Stack
              position="absolute"
              top={0}
              left={0}
              right={0}
              bottom={0}
              accessible
              accessibilityLabel={`${k.label} ${k.value} ${k.delta}`}
              aria-label={`${k.label} ${k.value} ${k.delta}`}
              pointerEvents="none"
            />
          </Stack>
        ))}
      </Stack>

      {/* Daily activity (chart deferred) + Subject mastery, side by side on wide */}
      <Stack flexDirection={rowDir} gap="$4" flexWrap="wrap" alignItems="stretch">
        <Stack flex={2} minWidth={320}>
          <DailyActivityCard direction={direction} rowDir={rowDir} />
        </Stack>
        <Stack testID="overview-mastery-region" flex={1} minWidth={280}>
          <SubjectMasteryCard childId={seed} direction={direction} />
        </Stack>
      </Stack>

      {/* 2-col row: "Areas to focus on" (1fr) + "Recommendations from Lexi" (1fr).
          Uses flexWrap so it stacks to 1 column on narrow widths (<~680px).
          Per spec C.2: flexDirection={rowDir}, gap=$5 (20), alignItems=flex-start,
          each child flex=1 minWidth=320. */}
      <Stack
        testID="overview-focus-recommendations-row"
        flexDirection={rowDir}
        flexWrap="wrap"
        gap="$5"
        alignItems="flex-start"
      >
        <Stack flex={1} minWidth={320}>
          <FocusAreasCard
            childId={seed}
            childName={childName}
            direction={direction}
            rowDir={rowDir}
            locale={locale}
          />
        </Stack>
        <Stack flex={1} minWidth={320}>
          <RecommendationsCard
            childName={childName}
            direction={direction}
            rowDir={rowDir}
          />
        </Stack>
      </Stack>
    </>
  );
}

OverviewWeb.displayName = 'OverviewWeb';
