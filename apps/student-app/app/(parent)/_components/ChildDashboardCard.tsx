/**
 * ChildDashboardCard — the rich per-child card in the parent dashboard grid
 * (captures `web/04-my-children.png` + `web-ar/04`). Header row (avatar, name,
 * grade pill, language, active/inactive status dot), a 3-tile stat row
 * (Level / XP / Streak), a mastery bar, the weakest-topic line, and a
 * "View dashboard →" action that routes to that child's overview.
 *
 * Real child identity comes from `useMyChildren` (P1-04); the stats are Phase-5
 * stubs (`ChildStatsStub`, TODO(P5)). Built from existing primitives — no new
 * design pattern.
 *
 * RTL: rows reverse, text uses logical direction. The mastery bar (SKILL rule 6)
 * stays LTR; its percent uses the Arabic percent sign `٪` + Eastern-Arabic
 * numerals in AR, wrapped in a dir=ltr span so bidi doesn't reorder it (C-36).
 * The "View dashboard" arrow lives in the i18n copy (EN →, AR ←) — never
 * mirrored programmatically. i18n: every label is a translation key.
 */
import { gradientStops } from '@learnexia/design-system';
import { Avatar, GradientBox, KPIStatCard } from '@learnexia/ui';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import type { ChildStatsStub } from './parentDashboardStubs';

export interface ChildDashboardCardProps {
  fullName: string;
  stats: ChildStatsStub;
  onViewDashboard: () => void;
  /** When provided, renders an Edit affordance (pencil icon button) in the card header. */
  onEdit?: () => void;
  testID?: string;
}

function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(value);
}

/**
 * Mastery percent readout (DEFERRED FORMATTER — C-36). EN: "72%" (Latin digit +
 * Latin %). AR: "٧٢٪" — Eastern-Arabic digit + Arabic percent sign U+066A. Both
 * are rendered inside a dir=ltr span so the RTL bidi algorithm doesn't flip the
 * digit/sign order.
 */
function formatPercent(value: number, locale: string): string {
  const digits = formatNumber(value, locale);
  return locale === 'ar' ? `${digits}٪` : `${digits}%`;
}

export function ChildDashboardCard({
  fullName,
  stats,
  onViewDashboard,
  onEdit,
  testID,
}: ChildDashboardCardProps) {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  // RTL is driven by the CSS `direction` set via `dir` on the card root (below):
  // the browser flips every plain `flexDirection: 'row'` for us, exactly like
  // `ar-child-card.html` (which uses `dir="rtl"`). So children stay in natural
  // DOM order — NO manual reversal (reversing would double-flip under `dir=rtl`).
  // `nat` is kept as an identity passthrough so the row JSX reads uniformly.
  const nat = (nodes: React.ReactNode[]) => nodes;

  const statusKey = stats.activeToday
    ? 'parent.myChildren.activeToday'
    : 'parent.myChildren.inactive';
  const langLabel = t(`onboarding.language.${stats.locale}`);
  const weakestLabel = t(`parent.myChildren.topics.${stats.weakestTopicKey}`);
  const xpValue = formatNumber(stats.xp, locale);

  // Level KPI value differs by locale (C-27/C-28): EN "Lv 12", AR "المستوى ١٢".
  const levelValue = `${t('parent.myChildren.statLevelShort')} ${formatNumber(stats.level, locale)}`;
  // Streak KPI value (C-29/C-30): EN "7d", AR "٧ أيام".
  const streakValue = t('parent.myChildren.statStreakValue', {
    n: formatNumber(stats.streakDays, locale),
  });

  const masteryPct = Math.max(0, Math.min(100, stats.masteryPercent));
  const masteryReadout = formatPercent(masteryPct, locale);

  return (
    <Stack
      testID={testID}
      // Explicit direction: the browser flips all inner `row` layouts to RTL,
      // so children below stay in natural (LTR) DOM order.
      dir={isRtl ? 'rtl' : 'ltr'}
      flexDirection="column"
      gap={16}
      flex={1}
      minWidth={300}
      borderRadius="$modal"
      borderWidth={1}
      borderColor="rgba(255,255,255,0.06)"
      backgroundColor="$card"
      padding={22}
      shadowColor="#000"
      shadowOpacity={0.15}
      shadowRadius={12}
      shadowOffset={{ width: 0, height: 4 }}
      hoverStyle={{
        scale: 1.02,
        shadowOpacity: 0.25,
        shadowRadius: 24,
        shadowOffset: { width: 0, height: 8 },
      }}
    >
      {/* Header — natural order [avatar][info][edit+active group]; under `dir=rtl`
          the browser flips it so visually: avatar RIGHT, info, then active LEFT-most
          with the edit button to its right (matches ar-child-card.html). */}
      <Stack flexDirection="row" alignItems="center" gap={14}>
        {nat([
          <Avatar key="avatar" name={fullName} size="card" />,

          // Info column: name (row 1) · grade pill + language (row 2)
          <Stack key="info" flexDirection="column" flex={1} gap={6}>
            <Text
              color="$fg1"
              fontSize={20}
              fontWeight="900"
              lineHeight={20}
              fontFamily="$heading"
              writingDirection={direction}
              textAlign={isRtl ? 'right' : 'left'}
            >
              {fullName}
            </Text>
            <Stack flexDirection="row" alignItems="center" gap={8} flexWrap="wrap">
              {nat([
                <Stack
                  key="grade"
                  backgroundColor="$primarySoft"
                  borderRadius="$pill"
                  paddingHorizontal={8}
                  paddingVertical={2}
                  accessible
                  accessibilityLabel={t(`onboarding.grade.${stats.grade}`)}
                >
                  <Text color="$primaryLight" fontSize={11} fontWeight="800" fontFamily="$heading" writingDirection={direction}>
                    {t(`onboarding.grade.${stats.grade}`)}
                  </Text>
                </Stack>,
                <Text key="lang" color="$fg3" fontSize={12} fontFamily="$body" writingDirection="ltr">
                  {langLabel}
                </Text>,
              ])}
            </Stack>
          </Stack>,

          // Edit button + active status — grouped (ar-child-card.html line 9, gap 8).
          // Natural DOM order [edit][active]: under dir=rtl the edit button sits at
          // the inline-start (RIGHT) and the active label to its LEFT.
          <Stack key="actions" flexDirection="row" alignItems="center" gap={8} flexShrink={0}>
            {onEdit ? (
              <Stack
                width={32}
                height={32}
                flexShrink={0}
                alignItems="center"
                justifyContent="center"
                borderRadius={10}
                backgroundColor="rgba(79,70,229,0.14)"
                borderWidth={1}
                borderColor="rgba(99,102,241,0.3)"
                cursor="pointer"
                hoverStyle={{ backgroundColor: 'rgba(79,70,229,0.24)' }}
                pressStyle={{ scale: 0.95 }}
                onPress={onEdit}
                accessibilityRole="button"
                accessible
                accessibilityLabel={t('parent.myChildren.editChild', { name: fullName })}
                aria-label={t('parent.myChildren.editChild', { name: fullName })}
              >
                <Text fontSize={13} color="$primaryLight" accessibilityElementsHidden>
                  ✏️
                </Text>
              </Stack>
            ) : null}
            <Stack flexDirection="row" alignItems="center" gap={4} flexShrink={0}>
              <Stack
                width={8}
                height={8}
                borderRadius={9999}
                backgroundColor={stats.activeToday ? '$success' : '$fg4'}
                {...(stats.activeToday
                  ? {
                      shadowColor: '#22C55E',
                      shadowOpacity: 0.6,
                      shadowRadius: 6,
                      shadowOffset: { width: 0, height: 0 },
                    }
                  : null)}
                accessibilityElementsHidden
              />
              <Text color={stats.activeToday ? '$success' : '$fg3'} fontSize={11} fontWeight="700" fontFamily="$body">
                {t(statusKey)}
              </Text>
            </Stack>
          </Stack>,
        ])}
      </Stack>

      {/* Stat tiles — natural order Level→XP→Streak; under dir=rtl Level sits RIGHT. */}
      <Stack flexDirection="row" gap={10}>
        {nat([
          <KPIStatCard
            key="level"
            icon="🧠"
            value={levelValue}
            label={t('parent.myChildren.statLevel')}
            accent="$purple"
            direction={direction}
            accessibilityLabel={`${t('parent.myChildren.statLevel')} ${formatNumber(stats.level, locale)}`}
          />,
          <KPIStatCard
            key="xp"
            icon="⭐"
            value={xpValue}
            label={t('parent.myChildren.statXp')}
            accent="$xp"
            direction={direction}
            accessibilityLabel={`${t('parent.myChildren.statXp')} ${xpValue}`}
          />,
          <KPIStatCard
            key="streak"
            icon="🔥"
            value={streakValue}
            label={t('parent.myChildren.statStreak')}
            accent="$streak"
            direction={direction}
            accessibilityLabel={`${t('parent.myChildren.statStreak')} ${streakValue}`}
          />,
        ])}
      </Stack>

      {/* Mastery — gap 5px, label $fg3/10px, no uppercase, letterSpacing 0.4 */}
      <Stack
        gap={5}
        width="100%"
        accessibilityRole="progressbar"
        accessible
        accessibilityLabel={`${t('parent.myChildren.mastery')} ${masteryReadout}`}
        aria-label={`${t('parent.myChildren.mastery')} ${masteryReadout}`}
        accessibilityValue={{ min: 0, max: 100, now: masteryPct }}
      >
        <Stack flexDirection="row" justifyContent="space-between" alignItems="center">
          {nat([
            <Text
              key="label"
              color="$fg3"
              fontSize={10}
              fontWeight="700"
              fontFamily="$heading"
              letterSpacing={0.4}
              writingDirection={direction}
            >
              {t('parent.myChildren.mastery')}
            </Text>,
            <Text
              key="pct"
              color="$fg1"
              fontSize={11}
              fontWeight="800"
              fontFamily="$heading"
              writingDirection="ltr"
              style={{ fontVariant: ['tabular-nums'] }}
            >
              {masteryReadout}
            </Text>,
          ])}
        </Stack>
        <Stack height={7} borderRadius={9999} backgroundColor="$bg" overflow="hidden" flexDirection="row">
          <Stack width={`${masteryPct}%` as StackProps['width']} height="100%" borderRadius={9999} overflow="hidden">
            <GradientBox stops={gradientStops.gradXp.colors} angle={90} height="100%" width="100%" />
          </Stack>
        </Stack>
      </Stack>

      {/* Footer — natural order [weakest][view]; under dir=rtl weakest sits RIGHT, view LEFT. */}
      <Stack
        flexDirection="row"
        alignItems="center"
        justifyContent="space-between"
        gap="$2"
        flexWrap="wrap"
        paddingTop={12}
        borderTopWidth={1}
        borderTopColor="rgba(255,255,255,0.05)"
      >
        {nat([
          <Text key="weak" color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
            {`${t('parent.myChildren.weakest')} `}
            <Text color="$fg2" fontSize={12} fontWeight="700" fontFamily="$body">
              {weakestLabel}
            </Text>
          </Text>,
          <Stack
            key="view"
            minHeight={40}
            justifyContent="center"
            cursor="pointer"
            pressStyle={{ scale: 0.95 }}
            onPress={() => onViewDashboard()}
            accessibilityRole="button"
            accessible
            accessibilityLabel={`${t('parent.myChildren.viewDashboard')} ${fullName}`}
            aria-label={`${t('parent.myChildren.viewDashboard')} ${fullName}`}
          >
            <Text color="$primaryLight" fontSize={12} fontWeight="800" fontFamily="$heading" writingDirection={direction}>
              {t('parent.myChildren.viewDashboard')}
            </Text>
          </Stack>,
        ])}
      </Stack>
    </Stack>
  );
}

ChildDashboardCard.displayName = 'ChildDashboardCard';
