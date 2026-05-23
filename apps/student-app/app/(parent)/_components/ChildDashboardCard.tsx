/**
 * ChildDashboardCard — the rich per-child card in the parent dashboard grid
 * (capture `web/04-my-children.png`). Header row (avatar, name, grade pill,
 * language, active/inactive status dot), a 3-tile stat row (Level / XP / Streak),
 * a mastery bar, the weakest-topic line, and a "View dashboard →" action that
 * routes to that child's overview.
 *
 * Real child identity comes from `useMyChildren` (P1-04); the stats are Phase-5
 * stubs (`ChildStatsStub`, TODO(P5)). Built from existing primitives + the new
 * Avatar / KPIStatCard / MasteryBar — no new design pattern.
 *
 * RTL: rows reverse, text uses logical direction, the action arrow mirrors.
 * i18n: every label is a translation key.
 */
import { Avatar, KPIStatCard, MasteryBar } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import type { ChildStatsStub } from './parentDashboardStubs';

export interface ChildDashboardCardProps {
  fullName: string;
  stats: ChildStatsStub;
  onViewDashboard: () => void;
}

function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(value);
}

export function ChildDashboardCard({
  fullName,
  stats,
  onViewDashboard,
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

  return (
    <Stack
      flexDirection="column"
      gap="$4"
      flex={1}
      minWidth={300}
      minHeight={300}
      borderRadius="$card"
      borderWidth={1}
      borderColor="$border"
      backgroundColor="$card"
      padding="$5"
    >
      {/* Header */}
      <Stack flexDirection={rowDir} alignItems="flex-start" gap="$3">
        <Avatar name={fullName} size="lg" />
        <Stack flexDirection="column" flex={1} gap="$2">
          <Stack flexDirection={rowDir} alignItems="center" gap="$3" flexWrap="wrap">
            <Text color="$fg1" fontSize={20} fontWeight="800" fontFamily="$heading" writingDirection={direction}>
              {fullName}
            </Text>
            {/* Active/inactive status */}
            <Stack flexDirection={rowDir} alignItems="center" gap="$1">
              <Stack
                width={8}
                height={8}
                borderRadius={9999}
                backgroundColor={stats.activeToday ? '$success' : '$fg4'}
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
            <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
              {langLabel}
            </Text>
          </Stack>
        </Stack>
      </Stack>

      {/* Stat tiles */}
      <Stack flexDirection={rowDir} gap="$3">
        <KPIStatCard
          icon="🧠"
          value={`${t('parent.myChildren.statLevelShort')} ${stats.level}`}
          label={t('parent.myChildren.statLevel')}
          accent="$primaryLight"
          direction={direction}
          accessibilityLabel={`${t('parent.myChildren.statLevel')} ${stats.level}`}
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
          value={`${formatNumber(stats.streakDays, locale)}d`}
          label={t('parent.myChildren.statStreak')}
          accent="$streak"
          direction={direction}
          accessibilityLabel={`${t('parent.myChildren.statStreak')} ${stats.streakDays}`}
        />
      </Stack>

      {/* Mastery */}
      <MasteryBar
        value={stats.masteryPercent}
        asPercent
        label={t('parent.myChildren.mastery')}
        direction={direction}
        accessibilityLabel={`${t('parent.myChildren.mastery')} ${stats.masteryPercent}%`}
      />

      {/* Footer: weakest topic + view dashboard */}
      <Stack flexDirection={rowDir} alignItems="center" justifyContent="space-between" gap="$2" flexWrap="wrap">
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
          pressStyle={{ scale: 0.97 }}
          onPress={() => onViewDashboard()}
          accessibilityRole="button"
          accessible
          accessibilityLabel={`${t('parent.myChildren.viewDashboard')} ${fullName}`}
          aria-label={`${t('parent.myChildren.viewDashboard')} ${fullName}`}
        >
          <Text color="$primaryLight" fontSize={14} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
            {t('parent.myChildren.viewDashboard')}
          </Text>
        </Stack>
      </Stack>
    </Stack>
  );
}

ChildDashboardCard.displayName = 'ChildDashboardCard';
