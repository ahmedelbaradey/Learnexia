/**
 * FamilySummaryStrip — the purple "this week · combined" hero strip on the
 * parent dashboard (capture `web/04-my-children.png` + `web-ar/04`). Eyebrow +
 * headline + subline on the start side, four inline KPI stats, and a large
 * decorative family watermark on the trailing corner.
 *
 * Background: the `gradLevelup` (purple → indigo) gradient from the design
 * system. Stats are Phase-5 stubs (`FamilyTotalsStub`, TODO(P5)).
 *
 * Layout (align-my-children F-5/F-23): web uses a CSS Grid `1.4fr repeat(4,1fr)`
 * so the label column is ~1.4x the stat columns; in AR the grid container is
 * `direction:rtl` so the label sits on the right. Native falls back to a
 * wrapping flex row.
 *
 * RTL: row reverses, gradient angle flips (NOT the colors), text uses logical
 * direction, the watermark hugs the opposite corner. i18n: all copy via keys.
 */
import { gradientStops, shadows } from '@learnexia/design-system';
import { GradientBox, KPIStatCard } from '@learnexia/ui';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React from 'react';
import { Platform } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocale } from '@/hooks/useLocale';
import type { FamilyTotalsStub } from './parentDashboardStubs';

export interface FamilySummaryStripProps {
  totals: FamilyTotalsStub;
}

const IS_WEB = Platform.OS === 'web';

function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(value);
}

export function FamilySummaryStrip({ totals }: FamilySummaryStripProps) {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const rowDir = isRtl ? 'row-reverse' : 'row';

  // Streak value differs by locale: EN "9d", AR "٩ أيام" (F-19). Digits are
  // localized via Intl (Eastern-Arabic in AR); the unit suffix is the i18n key.
  const bestStreakValue = t('parent.myChildren.statStreakValue', {
    n: formatNumber(totals.bestStreakDays, locale),
  });

  // CSS Grid on web (F-5): label column 1.4fr, four stat columns. In AR the
  // grid wrapper is direction:rtl so the label block sits on the right (F-23).
  // The hero drop-shadow + inset highlight (F-3) is a CSS box-shadow on web;
  // native degrades to the soft RN elevation props below.
  const webStyle = IS_WEB
    ? ({
        display: 'grid',
        gridTemplateColumns: '1.4fr repeat(4, 1fr)',
        alignItems: 'center',
        columnGap: 24,
        rowGap: 16,
        direction: isRtl ? 'rtl' : 'ltr',
        boxShadow: `${shadows.primaryGlow}, inset 0 1px 0 rgba(255,255,255,0.18)`,
      } as object)
    : undefined;

  return (
    <Stack
      borderRadius="$modal"
      overflow="hidden"
      padding={28}
      position="relative"
      // Native shadow fallback (web uses the box-shadow in `webStyle`).
      shadowColor="#6366F1"
      shadowOpacity={0.4}
      shadowRadius={24}
      shadowOffset={{ width: 0, height: 8 }}
      {...(IS_WEB
        ? { style: webStyle }
        : { flexDirection: rowDir, alignItems: 'center', gap: '$4', flexWrap: 'wrap' as const })}
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

      {/* Decorative family watermark (F-21): 160px emoji hugging the trailing
          top corner — EN right, AR left — at 0.18 opacity. Hidden from AT. */}
      <Text
        position="absolute"
        top={-20}
        {...(isRtl ? { left: -20 } : { right: -20 })}
        fontSize={160}
        opacity={0.18}
        accessibilityElementsHidden
      >
        {'👨‍👩‍👦'}
      </Text>

      {/* Headline block (label column) */}
      <Stack flexDirection="column" gap="$2" {...(IS_WEB ? {} : { flex: 1, minWidth: 220 })}>
        <Text
          color="$fg1"
          fontSize={11}
          fontWeight="800"
          fontFamily="$heading"
          textTransform="uppercase"
          letterSpacing={1.3}
          opacity={0.85}
          writingDirection={direction}
        >
          {t('parent.familySummary.eyebrow')}
        </Text>
        <Text color="$fg1" fontSize={28} fontWeight="900" fontFamily="$heading" writingDirection={direction}>
          {t('parent.familySummary.headline')}
        </Text>
        <Text color="$fg1" fontSize={13} fontFamily="$body" opacity={0.85} writingDirection={direction}>
          {t('parent.familySummary.subline', {
            learners: totals.activeLearners,
            lessons: totals.lessonsCompleted,
          })}
        </Text>
      </Stack>

      {/* Stats — each occupies one grid column on web. */}
      <HeroStat
        web={IS_WEB}
        icon="⭐"
        value={formatNumber(totals.totalXp, locale)}
        label={t('parent.familySummary.totalXp')}
        direction={direction}
        accessibilityLabel={`${t('parent.familySummary.totalXp')} ${formatNumber(totals.totalXp, locale)}`}
      />
      <HeroStat
        web={IS_WEB}
        icon="📚"
        value={formatNumber(totals.lessonsCompleted, locale)}
        label={t('parent.familySummary.lessons')}
        direction={direction}
        accessibilityLabel={`${t('parent.familySummary.lessons')} ${formatNumber(totals.lessonsCompleted, locale)}`}
      />
      <HeroStat
        web={IS_WEB}
        icon="🔥"
        value={bestStreakValue}
        label={t('parent.familySummary.bestStreak')}
        direction={direction}
        accessibilityLabel={`${t('parent.familySummary.bestStreak')} ${bestStreakValue}`}
      />
      <HeroStat
        web={IS_WEB}
        icon="🏆"
        value={formatNumber(totals.badgesEarned, locale)}
        label={t('parent.familySummary.badgesEarned')}
        direction={direction}
        accessibilityLabel={`${t('parent.familySummary.badgesEarned')} ${formatNumber(totals.badgesEarned, locale)}`}
      />
    </Stack>
  );
}

interface HeroStatProps {
  web: boolean;
  icon: string;
  value: string;
  label: string;
  direction: 'ltr' | 'rtl';
  accessibilityLabel: string;
}

/**
 * One hero stat. On web it is a bare grid cell (no flex sizing) so the parent
 * grid controls its column; on native it keeps the inline KPI sizing.
 */
function HeroStat({ web, ...rest }: HeroStatProps) {
  const cellProps: StackProps = web ? {} : { flex: 1, minWidth: 120 };
  return (
    <Stack {...cellProps}>
      <KPIStatCard variant="inline" {...rest} />
    </Stack>
  );
}

FamilySummaryStrip.displayName = 'FamilySummaryStrip';
