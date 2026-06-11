/**
 * RecommendationsCard — "Recommendations from Lexi" panel (parent Overview C.3).
 *
 * Mirrors the `FocusAreasCard` / `PDPanel` grammar: `$card` surface, `$card`
 * radius (20), `$border` hairline, 22px padding. Three stub rows (TODO(P5):
 * wired to a real endpoint when the recommendations API ships).
 *
 * Rows: `$bg` background, `$button` radius (16), icon chip (40×40, `$nav` radius),
 * text column (title + body + CTA ghost link). CTA arrow flips in RTL (←).
 *
 * RTL: `rowDir` on rows; Eastern-Arabic numerals; `٪` in the body copy is already
 * in the i18n keys. No `scaleX` on emoji. Cairo headings / Tajawal body.
 *
 * This is stub data only (Phase-5, brief Q-C1) — no endpoint, no TanStack Query
 * hook. The component is purely presentational.
 */
import { type Direction } from '@learnexia/shared/i18n';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

export interface RecommendationsCardProps {
  childName: string;
  direction: Direction;
  rowDir: 'row' | 'row-reverse';
}

interface RecommendationRow {
  /** Emoji icon */
  icon: string;
  /** Accent color token for the CTA link (uses backgroundColor token space — matches FocusAreasCard). */
  accent: StackProps['backgroundColor'];
  /** Background token for the icon chip */
  chipBg: StackProps['backgroundColor'];
  titleKey: string;
  bodyKey: string;
  ctaKey: string;
}

const ROWS: readonly RecommendationRow[] = [
  {
    icon: '🎯',
    accent: '$primary',
    chipBg: '$primarySoft',
    titleKey: 'parent.overview.recommendations.rows.subtraction.title',
    bodyKey: 'parent.overview.recommendations.rows.subtraction.body',
    ctaKey: 'parent.overview.recommendations.rows.subtraction.cta',
  },
  {
    icon: '📖',
    accent: '$purple',
    chipBg: '$purpleSoft',
    titleKey: 'parent.overview.recommendations.rows.reading.title',
    bodyKey: 'parent.overview.recommendations.rows.reading.body',
    ctaKey: 'parent.overview.recommendations.rows.reading.cta',
  },
  {
    icon: '🎉',
    accent: '$xp',
    chipBg: '$xpSoft',
    titleKey: 'parent.overview.recommendations.rows.streak.title',
    bodyKey: 'parent.overview.recommendations.rows.streak.body',
    ctaKey: 'parent.overview.recommendations.rows.streak.cta',
  },
];

export function RecommendationsCard({ childName, direction, rowDir }: RecommendationsCardProps) {
  const { t } = useTranslation();

  return (
    <Stack
      testID="recommendations-card"
      borderRadius="$card"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$border"
      padding={22}
      gap="$5"
    >
      {/* Panel header */}
      <Stack flexDirection="column" gap="$1">
        <Text
          color="$fg1"
          fontSize={16}
          fontWeight="800"
          fontFamily="$heading"
          accessibilityRole="header"
          writingDirection={direction}
        >
          {t('parent.overview.recommendations.title')}
        </Text>
        <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
          {t('parent.overview.recommendations.subtitle')}
        </Text>
      </Stack>

      {/* Recommendation rows */}
      <Stack flexDirection="column" gap={10}>
        {ROWS.map((row) => {
          const title = t(row.titleKey);
          const body = t(row.bodyKey, { name: childName });
          const cta = t(row.ctaKey);
          return (
            <Stack
              key={row.titleKey}
              flexDirection={rowDir}
              gap={12}
              padding={14}
              backgroundColor="$bg"
              borderWidth={1}
              borderColor="rgba(255,255,255,0.04)"
              borderRadius="$button"
              accessible
              accessibilityLabel={`${title} ${body}`}
              aria-label={`${title} ${body}`}
            >
              {/* Icon chip */}
              <Stack
                width={40}
                height={40}
                borderRadius="$nav"
                backgroundColor={row.chipBg}
                alignItems="center"
                justifyContent="center"
                flexShrink={0}
                accessibilityElementsHidden
              >
                <Text fontSize={18}>{row.icon}</Text>
              </Stack>

              {/* Text column */}
              <Stack flexDirection="column" flex={1} gap={0}>
                <Text
                  color="$fg1"
                  fontSize={13}
                  fontWeight="700"
                  fontFamily="$heading"
                  writingDirection={direction}
                >
                  {title}
                </Text>
                <Text
                  color="$fg3"
                  fontSize={12}
                  fontFamily="$body"
                  writingDirection={direction}
                  marginTop={3}
                  style={{ lineHeight: 1.45 }}
                >
                  {body}
                </Text>
                {/* Ghost CTA — no border, transparent bg, accent color */}
                <Stack
                  marginTop={10}
                  alignSelf="flex-start"
                  cursor="pointer"
                  pressStyle={{ opacity: 0.7 }}
                  onPress={() => {
                    /* TODO(P5): wire to action. */
                  }}
                  accessibilityRole="button"
                  accessible
                  accessibilityLabel={cta}
                  aria-label={cta}
                  minHeight={32}
                  justifyContent="center"
                >
                  <Text
                    color={row.accent}
                    fontSize={12}
                    fontWeight="700"
                    fontFamily="$heading"
                    writingDirection={direction}
                  >
                    {cta}
                  </Text>
                </Stack>
              </Stack>
            </Stack>
          );
        })}
      </Stack>
    </Stack>
  );
}

RecommendationsCard.displayName = 'RecommendationsCard';
