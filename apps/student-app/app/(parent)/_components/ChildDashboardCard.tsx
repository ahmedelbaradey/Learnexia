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
}: ChildDashboardCardProps) {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const rowDir = isRtl ? 'row-reverse' : 'row';

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
      flexDirection="column"
      gap={18}
      flex={1}
      minWidth={300}
      borderRadius="$modal"
      borderWidth={1}
      borderColor="rgba(255,255,255,0.06)"
      backgroundColor="$card"
      padding={22}
      // Soft drop-shadow (C-5) + hover lift (C-6).
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
      {/* Header — avatar + name/meta + optional edit affordance at inline-end */}
      <Stack flexDirection={rowDir} alignItems="flex-start" gap="$3">
        <Avatar name={fullName} size="lg" />
        <Stack flexDirection="column" flex={1} gap="$2">
          <Stack flexDirection={rowDir} alignItems="center" gap="$3" flexWrap="wrap">
            <Text color="$fg1" fontSize={22} fontWeight="900" fontFamily="$heading" writingDirection={direction}>
              {fullName}
            </Text>
            {/* Active/inactive status */}
            <Stack flexDirection={rowDir} alignItems="center" gap="$1">
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
              <Text color={stats.activeToday ? '$success' : '$fg3'} fontSize={12} fontWeight="600" fontFamily="$body">
                {t(statusKey)}
              </Text>
            </Stack>
          </Stack>
          <Stack flexDirection={rowDir} alignItems="center" gap="$2" flexWrap="wrap">
            <Stack
              backgroundColor="$primarySoft"
              borderRadius="$pill"
              paddingHorizontal="$3"
              paddingVertical="$1"
              accessible
              accessibilityLabel={t(`onboarding.grade.${stats.grade}`)}
            >
              <Text color="$primaryLight" fontSize={12} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
                {t(`onboarding.grade.${stats.grade}`)}
              </Text>
            </Stack>
            {/* Flag + language label kept LTR so the flag glyph stays leading. */}
            <Text
              color="$fg3"
              fontSize={13}
              fontFamily="$body"
              writingDirection="ltr"
            >
              {langLabel}
            </Text>
          </Stack>
        </Stack>
        {/* Edit affordance — pencil icon button, rendered when onEdit is provided.
            Sits at the inline-end of the header row (reverses with RTL).
            44×44 touch target, labelled for a11y. */}
        {onEdit ? (
          <Stack
            width={44}
            height={44}
            alignItems="center"
            justifyContent="center"
            cursor="pointer"
            borderRadius="$sm"
            hoverStyle={{ backgroundColor: '$cardSoft' }}
            pressStyle={{ scale: 0.95 }}
            onPress={onEdit}
            accessibilityRole="button"
            accessible
            accessibilityLabel={t('parent.myChildren.editChild', { name: fullName })}
            aria-label={t('parent.myChildren.editChild', { name: fullName })}
          >
            {/* Pencil glyph — consistent with the codebase's existing emoji/glyph icon approach */}
            <Text fontSize={16} color="$fg3" accessibilityElementsHidden>
              ✎
            </Text>
          </Stack>
        ) : null}
      </Stack>

      {/* Stat tiles */}
      <Stack flexDirection={rowDir} gap="$3">
        <KPIStatCard
          icon="🧠"
          value={levelValue}
          label={t('parent.myChildren.statLevel')}
          accent="$primaryLight"
          direction={direction}
          accessibilityLabel={`${t('parent.myChildren.statLevel')} ${formatNumber(stats.level, locale)}`}
        />
        <KPIStatCard
          icon="⭐"
          value={xpValue}
          label={t('parent.myChildren.statXp')}
          accent="$xp"
          direction={direction}
          accessibilityLabel={`${t('parent.myChildren.statXp')} ${xpValue}`}
        />
        <KPIStatCard
          icon="🔥"
          value={streakValue}
          label={t('parent.myChildren.statStreak')}
          accent="$streak"
          direction={direction}
          accessibilityLabel={`${t('parent.myChildren.statStreak')} ${streakValue}`}
        />
      </Stack>

      {/*
       * Mastery (C-31..C-36, R-2). The bar fill always grows from the visual
       * left (SKILL rule 6). The label + percent caption reverses with the
       * locale, but the percent itself is a dir=ltr span (Eastern-Arabic + ٪).
       */}
      <Stack
        gap="$2"
        width="100%"
        accessibilityRole="progressbar"
        accessible
        accessibilityLabel={`${t('parent.myChildren.mastery')} ${masteryReadout}`}
        aria-label={`${t('parent.myChildren.mastery')} ${masteryReadout}`}
        accessibilityValue={{ min: 0, max: 100, now: masteryPct }}
      >
        <Stack flexDirection={rowDir} justifyContent="space-between" alignItems="center">
          <Text
            color="$fg1"
            fontSize={11}
            fontWeight="700"
            fontFamily="$heading"
            textTransform="uppercase"
            letterSpacing={0.66}
            writingDirection={direction}
          >
            {t('parent.myChildren.mastery')}
          </Text>
          <Text
            color="$fg1"
            fontSize={11}
            fontWeight="800"
            fontFamily="$heading"
            writingDirection="ltr"
            style={{ fontVariant: ['tabular-nums'] }}
          >
            {masteryReadout}
          </Text>
        </Stack>
        <Stack height={8} borderRadius={9999} backgroundColor="$bg" overflow="hidden" flexDirection="row">
          <Stack width={`${masteryPct}%` as StackProps['width']} height="100%" borderRadius={9999} overflow="hidden">
            <GradientBox stops={gradientStops.gradXp.colors} angle={90} height="100%" width="100%" />
          </Stack>
        </Stack>
      </Stack>

      {/* Footer: weakest topic + view dashboard */}
      <Stack
        flexDirection={rowDir}
        alignItems="center"
        justifyContent="space-between"
        gap="$2"
        flexWrap="wrap"
        paddingTop={14}
        borderTopWidth={1}
        borderTopColor="rgba(255,255,255,0.05)"
      >
        <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
          {`${t('parent.myChildren.weakest')} `}
          <Text color="$fg2" fontSize={13} fontWeight="700" fontFamily="$body">
            {weakestLabel}
          </Text>
        </Text>
        <Stack
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
        </Stack>
      </Stack>
    </Stack>
  );
}

ChildDashboardCard.displayName = 'ChildDashboardCard';
