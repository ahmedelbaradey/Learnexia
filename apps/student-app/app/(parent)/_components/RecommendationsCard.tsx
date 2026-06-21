/**
 * RecommendationsCard — "Recommendations from Lexi" panel (parent Overview C.3).
 *
 * Wired to the real GET /api/Parent/Children/{id}/Recommendations endpoint via
 * `useChildRecommendations(childId)`. Mirrors `FocusAreasCard` / `PDPanel` grammar:
 * `$card` surface, `$card` radius (20), `$border` hairline, 22px padding.
 *
 * The backend returns `items[]` where each item carries:
 *   - `titleKey`, `bodyKey`, `ctaKey` — bare backend SharedResourcesKey strings
 *     (e.g. "RecReviewTitle"). Mapped to FE i18n via `REC_ITEM_KEY_MAP`.
 *   - `subjectCode` (0=Math…3=English), `severity` (1=Low,2=Medium,3=High),
 *     `actionType` (1=Practice,2=Review,3=KeepStreak,4=Celebrate).
 *
 * States: loading skeleton → error + retry → empty (empty `items` or cold-start) → rows.
 *
 * Rows: `$bg` background, `$button` radius (16), icon chip (40×40, `$nav` radius),
 * text column (title + body + CTA ghost link). CTA arrow flips in RTL (←).
 *
 * RTL: `rowDir` on rows; Cairo headings / Tajawal body.
 */
import { useChildRecommendations } from '@learnexia/api-client';
import type { RecommendationItemDto } from '@learnexia/api-client';
import { Button } from '@learnexia/ui';
import { type Direction } from '@learnexia/shared/i18n';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

export interface RecommendationsCardProps {
  childId: string | undefined;
  childName: string;
  direction: Direction;
  rowDir: 'row' | 'row-reverse';
}

/**
 * Maps backend SharedResourcesKey strings (TitleKey/BodyKey/CtaKey) to
 * the FE i18n key paths in the `rec` namespace group.
 *
 * The 9 keys correspond to 3 action types × 3 text slots.
 * Unknown backend keys fall through to `undefined` (item is skipped in render).
 */
const REC_ITEM_KEY_MAP: Record<string, string> = {
  RecReviewTitle: 'rec.RecReviewTitle',
  RecReviewBody: 'rec.RecReviewBody',
  RecReviewCta: 'rec.RecReviewCta',
  RecPracticeTitle: 'rec.RecPracticeTitle',
  RecPracticeBody: 'rec.RecPracticeBody',
  RecPracticeCta: 'rec.RecPracticeCta',
  RecColdStartTitle: 'rec.RecColdStartTitle',
  RecColdStartBody: 'rec.RecColdStartBody',
  RecColdStartCta: 'rec.RecColdStartCta',
} as const;

/** Resolve a backend key string to a translated string, or empty string for unknown keys. */
function resolveRecKey(
  backendKey: string,
  t: ReturnType<typeof useTranslation>['t'],
): string {
  const feKey = REC_ITEM_KEY_MAP[backendKey];
  return feKey ? t(feKey) : '';
}

/** Choose icon + accent/chip colors by action type (1=Practice,2=Review,3=KeepStreak,4=Celebrate). */
function rowStyle(actionType: number): {
  icon: string;
  accent: StackProps['backgroundColor'];
  chipBg: StackProps['backgroundColor'];
} {
  switch (actionType) {
    case 2: // Review — high-severity (danger accent)
      return { icon: '📖', accent: '$danger', chipBg: '$dangerSoft' };
    case 3: // KeepStreak
      return { icon: '🔥', accent: '$streak', chipBg: '$streakSoft' };
    case 4: // Celebrate / cold-start
      return { icon: '🎉', accent: '$xp', chipBg: '$xpSoft' };
    case 1: // Practice (default)
    default:
      return { icon: '🎯', accent: '$primary', chipBg: '$primarySoft' };
  }
}

export function RecommendationsCard({
  childId,
  direction,
  rowDir,
}: RecommendationsCardProps) {
  const { t } = useTranslation();
  // childId may be undefined when no active child — hook disables itself when falsy.
  const query = useChildRecommendations(childId || undefined);

  const items: RecommendationItemDto[] = query.data?.items ?? [];

  return (
    <Stack
      testID="recommendations-card"
      borderRadius={20}
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
          textAlign={direction === 'rtl' ? 'right' : 'left'}
        >
          {t('parent.overview.recommendations.title')}
        </Text>
        <Text
          color="$fg3"
          fontSize={12}
          fontFamily="$body"
          writingDirection={direction}
          textAlign={direction === 'rtl' ? 'right' : 'left'}
        >
          {t('parent.overview.recommendations.subtitle')}
        </Text>
      </Stack>

      {/* Loading skeleton — 3 placeholder rows */}
      {query.isLoading ? (
        <Stack flexDirection="column" gap={10}>
          {[0, 1, 2].map((i) => (
            <Stack
              key={i}
              height={72}
              borderRadius="$button"
              backgroundColor="$cardSoft"
              opacity={0.5}
            />
          ))}
        </Stack>
      ) : query.isError ? (
        /* Error + retry */
        <Stack gap="$3" alignItems="flex-start">
          <Text
            color="$fg3"
            fontSize={13}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.overview.loadError')}
          </Text>
          <Button
            variant="ghost"
            size="sm"
            accessibilityLabel={t('common.retry')}
            onPress={() => query.refetch()}
          >
            {t('common.retry')}
          </Button>
        </Stack>
      ) : items.length === 0 ? (
        /* Empty state — cold-start / no weak areas yet (not an error) */
        <Text
          color="$fg3"
          fontSize={13}
          fontFamily="$body"
          textAlign="center"
          writingDirection={direction}
        >
          {t('parent.overview.recommendations.empty')}
        </Text>
      ) : (
        /* Recommendation rows */
        <Stack flexDirection="column" gap={10}>
          {items.map((item, idx) => {
            const { icon, accent, chipBg } = rowStyle(item.actionType);
            const title = resolveRecKey(item.titleKey, t);
            const body = resolveRecKey(item.bodyKey, t);
            const cta = resolveRecKey(item.ctaKey, t);

            return (
              <Stack
                key={`${item.skillId}-${idx}`}
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
                  backgroundColor={chipBg}
                  alignItems="center"
                  justifyContent="center"
                  flexShrink={0}
                  accessibilityElementsHidden
                >
                  <Text fontSize={18}>{icon}</Text>
                </Stack>

                {/* Text column — aligns to writing-direction start (right in RTL). */}
                <Stack flexDirection="column" flex={1} gap={0}>
                  <Text
                    color="$fg1"
                    fontSize={13}
                    fontWeight="700"
                    fontFamily="$heading"
                    writingDirection={direction}
                    textAlign={direction === 'rtl' ? 'right' : 'left'}
                  >
                    {title}
                  </Text>
                  <Text
                    color="$fg3"
                    fontSize={12}
                    fontFamily="$body"
                    writingDirection={direction}
                    textAlign={direction === 'rtl' ? 'right' : 'left'}
                    marginTop={3}
                    style={{ lineHeight: 1.45 }}
                  >
                    {body}
                  </Text>
                  {/* Ghost CTA — accent color, no border, transparent bg.
                      alignSelf adapts to direction: start in RTL = flex-end visually. */}
                  <Stack
                    marginTop={10}
                    alignSelf={direction === 'rtl' ? 'flex-end' : 'flex-start'}
                    cursor="pointer"
                    pressStyle={{ opacity: 0.7 }}
                    onPress={() => {
                      /* TODO(P5): wire to subject/skill navigation action. */
                    }}
                    accessibilityRole="button"
                    accessible
                    accessibilityLabel={cta}
                    aria-label={cta}
                    minHeight={32}
                    justifyContent="center"
                  >
                    <Text
                      color={accent}
                      fontSize={12}
                      fontWeight="700"
                      fontFamily="$heading"
                      writingDirection={direction}
                      textAlign={direction === 'rtl' ? 'right' : 'left'}
                    >
                      {cta}
                    </Text>
                  </Stack>
                </Stack>
              </Stack>
            );
          })}
        </Stack>
      )}
    </Stack>
  );
}

RecommendationsCard.displayName = 'RecommendationsCard';
