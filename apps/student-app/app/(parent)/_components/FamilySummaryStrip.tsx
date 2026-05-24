/**
 * FamilySummaryStrip — the purple "this week · combined" hero strip on the
 * parent dashboard (capture `web/04-my-children.png`). Eyebrow + headline +
 * subline on the start side, four inline KPI stats, and the family mascot art on
 * the end side.
 *
 * Background: the `gradLevelup` (purple → indigo) gradient from the design
 * system. Stats are Phase-5 stubs (`FamilyTotalsStub`, TODO(P5)).
 *
 * RTL: row reverses, gradient angle flips, text uses logical direction.
 * i18n: all copy via translation keys.
 */
import { gradientStops } from '@learnexia/design-system';
import { AvatarStack, GradientBox, KPIStatCard } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import type { FamilyTotalsStub } from './parentDashboardStubs';

export interface FamilySummaryStripProps {
  totals: FamilyTotalsStub;
  /** Linked-child display names — drive the overlapping avatar cluster. */
  childNames: string[];
}

function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(value);
}

export function FamilySummaryStrip({ totals, childNames }: FamilySummaryStripProps) {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const rowDir = isRtl ? 'row-reverse' : 'row';

  return (
    <Stack
      borderRadius="$card"
      overflow="hidden"
      padding="$6"
      flexDirection={rowDir}
      alignItems="center"
      gap="$4"
      flexWrap="wrap"
    >
      <GradientBox
        stops={gradientStops.gradLevelup.colors}
        angle={isRtl ? 225 : 135}
        position="absolute"
        top={0}
        left={0}
        right={0}
        bottom={0}
      />

      {/* Headline block */}
      <Stack flexDirection="column" gap="$2" flex={1} minWidth={220}>
        <Text
          color="$fg1"
          fontSize={11}
          fontWeight="800"
          fontFamily="$heading"
          textTransform="uppercase"
          letterSpacing={1}
          opacity={0.85}
          writingDirection={direction}
        >
          {t('parent.familySummary.eyebrow')}
        </Text>
        <Text color="$fg1" fontSize={28} fontWeight="800" fontFamily="$heading" writingDirection={direction}>
          {t('parent.familySummary.headline')}
        </Text>
        <Text color="$fg1" fontSize={13} fontFamily="$body" opacity={0.85} writingDirection={direction}>
          {t('parent.familySummary.subline', {
            learners: totals.activeLearners,
            lessons: totals.lessonsCompleted,
          })}
        </Text>
      </Stack>

      {/* Stats */}
      <Stack flexDirection={rowDir} alignItems="center" gap="$6" flexWrap="wrap">
        <KPIStatCard
          variant="inline"
          icon="⭐"
          value={formatNumber(totals.totalXp, locale)}
          label={t('parent.familySummary.totalXp')}
          direction={direction}
          accessibilityLabel={`${t('parent.familySummary.totalXp')} ${formatNumber(totals.totalXp, locale)}`}
        />
        <KPIStatCard
          variant="inline"
          icon="📚"
          value={formatNumber(totals.lessonsCompleted, locale)}
          label={t('parent.familySummary.lessons')}
          direction={direction}
          accessibilityLabel={`${t('parent.familySummary.lessons')} ${formatNumber(totals.lessonsCompleted, locale)}`}
        />
        <KPIStatCard
          variant="inline"
          icon="🔥"
          value={`${formatNumber(totals.bestStreakDays, locale)}d`}
          label={t('parent.familySummary.bestStreak')}
          direction={direction}
          accessibilityLabel={`${t('parent.familySummary.bestStreak')} ${formatNumber(totals.bestStreakDays, locale)}`}
        />
        <KPIStatCard
          variant="inline"
          icon="🏅"
          value={formatNumber(totals.badgesEarned, locale)}
          label={t('parent.familySummary.badgesEarned')}
          direction={direction}
          accessibilityLabel={`${t('parent.familySummary.badgesEarned')} ${formatNumber(totals.badgesEarned, locale)}`}
        />
      </Stack>

      {/* Linked-child avatar cluster (capture shows stacked child avatars). */}
      {childNames.length > 0 ? (
        <AvatarStack
          items={childNames.map((name) => ({ name }))}
          size="lg"
          direction={direction}
          accessibilityLabel={t('parent.familySummary.childrenCluster', { count: childNames.length })}
        />
      ) : null}
    </Stack>
  );
}

FamilySummaryStrip.displayName = 'FamilySummaryStrip';
