/**
 * Hearts screen + Practice Mode — child push screen (P4-04-FE, carryover
 * batch 3a lane B3, design spec `design-system/ui_kits/student-app/P4-08.md`
 * §3.1; pixel target `screenshots/mobile/18-hearts.png`).
 *
 * Route: (child)/hearts — secondary `href: null` push screen (NOT a tab),
 * registered in `(child)/_layout.tsx` by batch 3c (B-int owns the shell).
 * Entry point (3c wires): the Hearts chip in the Home `DashboardHeader`;
 * until then the route is reachable by URL / deep link. The quiz wrong-answer
 * path may also land here with `?lost=1` to show the heart-lost card.
 *
 * Data: `useDashboard()` ONLY — `hearts` (current, cap 5) + `inPracticeMode`
 * + `continue` (for the Practice CTA target). There is NO heart-refill
 * countdown field on `DashboardDto` (spec gap G-5) → the refill card renders
 * the honest static line ("Hearts come back over time…"), never a fake timer.
 *
 * Practice Mode is a SERVER state (BE flips `inPracticeMode` at 0 hearts and
 * stops charging hearts) — the CTA simply continues the next lesson; it is a
 * soft "keep learning to refill" flow, never a paywall/block (spec §3.2).
 *
 * Motion: heart-break (scale 1 → 1.2 → settle, ❤️→💔 swap at 80ms, then
 * lost-state opacity) plays on the most-recently-lost heart when arriving
 * with `?lost=1`. Gated by `useReduceMotion` → instant gray-out, no swap.
 *
 * CTA chrome: the kit-sanctioned purple Practice button (spec §3.1.5 / G-6).
 * `@learnexia/ui` Button has no `purple` variant yet and this lane must not
 * touch packages/ui — token overrides are passed inline on `variant="primary"`
 * (same shape SKILL.md sanctions); promoting it to `variant="purple"` in the
 * kit is flagged for the 3b/packages-ui owner.
 */
import { useLocalizedRouter } from '@/hooks/useLocalizedRouter';
import { useDashboard } from '@learnexia/api-client';
import { durations, springs } from '@learnexia/design-system';
import { Button, useReduceMotion } from '@learnexia/ui';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useLocalSearchParams } from 'expo-router';
import { MotiView } from 'moti';
import React, { useEffect, useState } from 'react';
import { Pressable, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '@/hooks/useLocale';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

/** Hearts cap (FR — 5 hearts; matches the Home dashboard default). */
const MAX_HEARTS = 5;
/** Hearts row glyph size — spec §3.1.2 "5 hearts at 44px". */
const HEART_SIZE = 44;
/** Lost-heart settle opacity — spec `grayscale(1) opacity(0.3)` (RN-safe opacity-only, kit Hearts precedent). */
const LOST_OPACITY = 0.3;
/** ❤️ → 💔 swap point in the heart-break recipe (spec §0.1, 80ms). */
const HEART_BREAK_SWAP_MS = 80;
/** Truthy value of the `?lost=` arrival param (technical identifier). */
const LOST_PARAM_TRUE = '1';

/** Decorative glyphs (semantic emoji set — not user-facing copy). */
const GLYPHS = {
  heart: '❤️',
  broken: '💔',
  clock: '⏰',
  warning: '⚠️',
} as const;

/* ------------------------------------------------------------------ */
/* Hearts row                                                          */
/* ------------------------------------------------------------------ */

/**
 * One heart in the row. `breaking` plays the heart-break motion (only ever
 * set on the most-recently-lost slot when arriving with `?lost=1`).
 */
function BigHeart({
  filled,
  breaking,
  reduceMotion,
}: {
  filled: boolean;
  breaking: boolean;
  reduceMotion: boolean;
}) {
  // ❤️→💔 swap at 80ms, then settle back to the standard lost glyph so the
  // row stays consistent with the other lost hearts (spec §0.1 heart-break).
  const [glyph, setGlyph] = useState<string>(GLYPHS.heart);
  useEffect(() => {
    if (!breaking || reduceMotion) return;
    const swap = setTimeout(() => setGlyph(GLYPHS.broken), HEART_BREAK_SWAP_MS);
    const settle = setTimeout(
      () => setGlyph(GLYPHS.heart),
      HEART_BREAK_SWAP_MS + durations.slow,
    );
    return () => {
      clearTimeout(swap);
      clearTimeout(settle);
    };
  }, [breaking, reduceMotion]);

  const node = (
    <Text
      fontSize={HEART_SIZE}
      lineHeight={HEART_SIZE + 8}
      opacity={breaking && !reduceMotion ? 1 : filled ? 1 : LOST_OPACITY}
      // Full hearts glow — spec literal drop-shadow(0 0 12px rgba(251,113,133,0.6)).
      style={
        filled
          ? {
              textShadowColor: 'rgba(251, 113, 133, 0.6)',
              textShadowRadius: 12,
              textShadowOffset: { width: 0, height: 0 },
            }
          : undefined
      }
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
    >
      {breaking && !reduceMotion ? glyph : GLYPHS.heart}
    </Text>
  );

  // Reduced motion: instant gray-out, no scale, no glyph swap (spec §0.1).
  if (!breaking || reduceMotion) return node;

  return (
    <MotiView
      from={{ scale: 1, opacity: 1 }}
      animate={{ scale: [1.2, 1], opacity: [1, LOST_OPACITY] }}
      transition={{
        scale: { type: 'spring', ...springs.settle },
        opacity: { type: 'timing', duration: durations.slow },
      }}
    >
      {node}
    </MotiView>
  );
}

/* ------------------------------------------------------------------ */
/* Screen                                                              */
/* ------------------------------------------------------------------ */

export default function HeartsScreen() {
  const { t } = useTranslation();
  const { direction, isRtl } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useLocalizedRouter();
  const reduceMotion = useReduceMotion();

  // `?lost=1` — set by the quiz wrong-answer path (lane B3 §3.2 wiring);
  // shows the heart-lost info card + plays the break on the freshest loss.
  const params = useLocalSearchParams<{ lost?: string }>();
  const arrivedFromLoss = params.lost === LOST_PARAM_TRUE;

  const dashboardQuery = useDashboard();

  const hearts = Math.min(
    Math.max(dashboardQuery.data?.hearts ?? MAX_HEARTS, 0),
    MAX_HEARTS,
  );
  const inPracticeMode = dashboardQuery.data?.inPracticeMode ?? false;
  const heartsFull = hearts >= MAX_HEARTS;
  const continueTarget = dashboardQuery.data?.continue ?? null;

  const isLoading = dashboardQuery.isPending;
  const isError = dashboardQuery.isError;

  const handleBack = () => {
    if (router.canGoBack()) router.back();
    else router.replace('/(child)/' as `/${string}`);
  };

  /**
   * Practice CTA — soft regain flow: continue the next lesson (the BE keeps
   * `inPracticeMode` on at 0 hearts, so answers cost nothing and earn hearts
   * back). Falls back to Home when no continue target exists (net-new child).
   */
  const handlePracticePress = () => {
    if (continueTarget?.lessonId && continueTarget?.subjectId) {
      router.push(
        `/(child)/lessons/${continueTarget.lessonId}?subjectId=${continueTarget.subjectId}` as `/${string}`,
      );
    } else {
      router.replace('/(child)/' as `/${string}`);
    }
  };

  const backChevron = isRtl ? '→' : '←';
  const rowDir = isRtl ? 'row-reverse' : 'row';

  // Sub-line: positive ready-state when full, never-shaming rule line otherwise.
  const subText = heartsFull && !isLoading && !isError
    ? t('hearts.subFull')
    : t('hearts.sub');

  // Most-recently-lost slot = first empty index (the break target).
  const breakingIndex = arrivedFromLoss && hearts < MAX_HEARTS ? hearts : -1;
  const slots = Array.from({ length: MAX_HEARTS }, (_, i) => i < hearts);

  return (
    <TamStack flex={1} backgroundColor="$bg" testID="child-hearts-screen">
      {/* Top safe area */}
      <TamStack height={insets.top} />

      {/* ScreenHeader — back affordance (W12 pattern, spec §0.1 push screens). */}
      <XStack
        flexDirection={rowDir}
        alignItems="center"
        height={56}
        paddingHorizontal="$4"
        gap="$3"
      >
        <Pressable
          onPress={handleBack}
          accessibilityRole="button"
          accessibilityLabel={t('hearts.back')}
          testID="hearts-back"
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
        {/* ≥768 centers at maxWidth 720 (W13 precedent, spec §0.1). */}
        <YStack width="100%" maxWidth={720} alignSelf="center" gap="$5">
          {/* H1 + sub — centered (spec §3.1.1). */}
          <YStack gap={6}>
            <Text
              color="$fg1"
              fontSize={28}
              fontWeight="800"
              fontFamily="$heading"
              textAlign="center"
              writingDirection={direction}
              accessibilityRole="header"
            >
              {t('hearts.title')}
            </Text>
            <Text
              color="$fg2"
              fontSize={14}
              fontFamily="$body"
              textAlign="center"
              writingDirection={direction}
              testID="hearts-sub"
            >
              {subText}
            </Text>
          </YStack>

          {isLoading ? (
            /* Loading — hearts row as 5 gray discs (spec §3.1 states). */
            <XStack
              testID="hearts-loading"
              justifyContent="center"
              gap={12}
              paddingVertical={32}
              paddingHorizontal={16}
              borderRadius={24}
              backgroundColor="$cardSoft"
              opacity={0.6}
            >
              {Array.from({ length: MAX_HEARTS }, (_, i) => (
                <TamStack
                  key={i}
                  width={HEART_SIZE}
                  height={HEART_SIZE}
                  borderRadius={9999}
                  backgroundColor="$card"
                />
              ))}
            </XStack>
          ) : isError ? (
            /* Error strip — W13 dashboard pattern (spec §0.1). */
            <YStack
              testID="hearts-error"
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
                {`${GLYPHS.warning} ${t('hearts.error')}`}
              </Text>
              <Button
                variant="ghost"
                size="sm"
                accessibilityLabel={t('common.retry')}
                onPress={() => void dashboardQuery.refetch()}
                testID="hearts-retry"
              >
                {t('common.retry')}
              </Button>
            </YStack>
          ) : (
            <>
              {/* Hearts row — spec §3.1.2 (`mobile-hearts-row.html`):
                  rgba(251,113,133,0.18) backdrop ($heartGlow family literal),
                  radius 24, padding 32/16, hearts 44px. */}
              <XStack
                testID="hearts-row"
                flexDirection={rowDir}
                justifyContent="center"
                gap={12}
                paddingVertical={32}
                paddingHorizontal={16}
                borderRadius={24}
                backgroundColor="rgba(251, 113, 133, 0.18)"
                accessibilityRole="text"
                accessible
                accessibilityLabel={t('hearts.rowA11y', {
                  current: hearts,
                  max: MAX_HEARTS,
                })}
                aria-label={t('hearts.rowA11y', {
                  current: hearts,
                  max: MAX_HEARTS,
                })}
                accessibilityValue={{ min: 0, max: MAX_HEARTS, now: hearts }}
              >
                {slots.map((filled, i) => (
                  <BigHeart
                    key={i}
                    filled={filled}
                    breaking={i === breakingIndex}
                    reduceMotion={reduceMotion}
                  />
                ))}
              </XStack>

              {/* Heart-lost info card — only on `?lost=1` arrival (spec §3.1.4).
                  Never-shaming voice; spec literals for the red-soft chrome. */}
              {arrivedFromLoss && !heartsFull ? (
                <XStack
                  testID="hearts-lost-card"
                  flexDirection={rowDir}
                  alignItems="center"
                  gap={12}
                  padding={14}
                  borderRadius={16}
                  backgroundColor="rgba(239, 68, 68, 0.12)"
                  borderWidth={1}
                  borderColor="rgba(239, 68, 68, 0.3)"
                  accessibilityLiveRegion="polite"
                >
                  <Text fontSize={22} accessibilityElementsHidden>
                    {GLYPHS.broken}
                  </Text>
                  <YStack flex={1} gap={2}>
                    <Text
                      color="$danger"
                      fontSize={14}
                      fontWeight="800"
                      fontFamily="$heading"
                      writingDirection={direction}
                    >
                      {t('hearts.lost.title')}
                    </Text>
                    <Text
                      color="$heart"
                      fontSize={11}
                      fontFamily="$body"
                      writingDirection={direction}
                    >
                      {t('hearts.lost.sub')}
                    </Text>
                  </YStack>
                </XStack>
              ) : null}

              {/* Refill card — shown only when hearts < 5 (spec §3.1.3).
                  Gap G-5: no countdown field on DashboardDto → honest static
                  copy only; no fake timer. */}
              {!heartsFull ? (
                <XStack
                  testID="hearts-refill-card"
                  flexDirection={rowDir}
                  alignItems="center"
                  gap={12}
                  padding={14}
                  borderRadius={16}
                  backgroundColor="$warningSoft"
                  borderWidth={1}
                  borderColor="rgba(245, 158, 11, 0.3)"
                >
                  <Text fontSize={22} accessibilityElementsHidden>
                    {GLYPHS.clock}
                  </Text>
                  <Text
                    flex={1}
                    color="$warning"
                    fontSize={14}
                    fontWeight="800"
                    fontFamily="$heading"
                    writingDirection={direction}
                  >
                    {t('hearts.refillNote')}
                  </Text>
                </XStack>
              ) : null}

              {/* Practice Mode explanation — server `inPracticeMode` only
                  (spec §3.2 voice: encouraging, soft regain, never a block). */}
              {inPracticeMode ? (
                <YStack
                  testID="hearts-practice-card"
                  gap={4}
                  padding={14}
                  borderRadius={16}
                  backgroundColor="$purpleSoft"
                  borderWidth={1}
                  borderColor="rgba(168, 85, 247, 0.3)"
                >
                  <Text
                    color="$purple"
                    fontSize={14}
                    fontWeight="800"
                    fontFamily="$heading"
                    writingDirection={direction}
                  >
                    {t('hearts.practiceMode.title')}
                  </Text>
                  <Text
                    color="$fg2"
                    fontSize={12}
                    fontFamily="$body"
                    writingDirection={direction}
                  >
                    {t('hearts.practiceMode.body')}
                  </Text>
                </YStack>
              ) : null}

              {/* Primary CTA — Practice Mode (the screen's ONE primary action,
                  spec §9). Purple chrome per §3.1.5 — token overrides on the
                  kit Button until `variant="purple"` lands in packages/ui (G-6). */}
              <Button
                variant="primary"
                size="full"
                accessibilityLabel={t('hearts.practiceMode.cta')}
                onPress={handlePracticePress}
                testID="hearts-practice-cta"
                backgroundColor="$purple"
                hoverStyle={{ backgroundColor: '$purple', opacity: 0.92 }}
                pressStyle={{ backgroundColor: '$purple', scale: 0.95 }}
                shadowColor="rgba(168, 85, 247, 0.4)"
                shadowOpacity={1}
                shadowRadius={12}
                shadowOffset={{ width: 0, height: 4 }}
              >
                {t('hearts.practiceMode.cta')}
              </Button>
            </>
          )}
        </YStack>
      </ScrollView>
    </TamStack>
  );
}
