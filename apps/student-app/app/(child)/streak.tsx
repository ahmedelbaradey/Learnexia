/**
 * Streak screen — B2 / P4-03-FE (carryover batch 3a, lane B2).
 * Design Spec: design-system/ui_kits/student-app/P4-08.md §2.
 *
 * Route: (child)/streak — `href: null` push screen (NOT a tab). Batch 3c
 * registers the route in `(child)/_layout.tsx` and wires the entry point
 * (tap on the 🔥 streak chip in `DashboardHeader` on Home); until then the
 * route is reachable by URL only. The ChildTabBar allowlist auto-hides the
 * bar here (attempts.tsx precedent) — no tab-bar clearance padding needed.
 *
 * Data: typed `DashboardDto` via `useDashboard()` ONLY —
 *   `streak`        → current daily streak (hero counter)
 *   `freezeBalance` → earned streak-freeze count (❄️ pill; earned-only,
 *                     NO buy/spend UI per plan B8 AC)
 * Honest-data rules (per the plan's backend carry-forward):
 *   - No per-day streak history endpoint (spec gap G-2) → no fabricated
 *     week-row/calendar; instead milestone markers (day 3/7/14/30 — pure
 *     derivations of `streak`) + an explicit "calendar coming soon" card.
 *   - No `longestStreak` field (spec gap G-3) → tile omitted, TODO slot below.
 *
 * Motion: flame-loop (scale 1→1.04→1, 1.5s infinite — spec §0.1) via Moti,
 * gated by `useReduceMotion`; reduced-motion fallback = static flame with the
 * glow kept (spec table). Zero-state flame renders static at 0.4 opacity.
 *
 * Streak-advanced badge-pop (`useDashboardDiff.streakAdvanced`, spec §2) is
 * wired by batch 3c together with the rest of the diff plumbing.
 *
 * Numerals (SKILL.md rule 5): day/freeze counts are prose counts →
 * Eastern-Arabic numerals in ar via Intl (`ar-EG`), Latin in en. The local
 * `formatNumber` helper mirrors `(child)/attempts.tsx` (sanctioned pattern;
 * lifting into `packages/shared` is a noted carry-forward).
 */
import { useDashboard } from '@learnexia/api-client';
import { colors } from '@learnexia/design-system';
import { Button, useReduceMotion } from '@learnexia/ui';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { MotiView } from 'moti';
import React from 'react';
import { Pressable, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '../../src/hooks/useLocale';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

/** Visual streak targets (task spec: day 3/7/14/30 markers). Fixed value set. */
const STREAK_MILESTONES = [3, 7, 14, 30] as const;

/** Spec §0.1 flame-loop: 1.5s full cycle = two 750ms legs (Moti repeatReverse). */
const FLAME_LOOP_LEG_MS = 750;
/** Spec §2.1 literal: drop-shadow(0 0 14px …) on the hero flame. */
const FLAME_GLOW_RADIUS = 14;
/** Spec §2.4 literal: `$gem` family soft pill bg (no `gemSoft` token exists). */
const FREEZE_PILL_BG = 'rgba(56, 189, 248, 0.18)';

/* ------------------------------------------------------------------ */
/* Locale-aware number formatting (mirrors (child)/attempts.tsx)       */
/* ------------------------------------------------------------------ */

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

/** Eastern-Arabic numerals in ar, Latin in en (SKILL.md rule 5 prose counts). */
function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

/* ------------------------------------------------------------------ */
/* Section eyebrow — lx-small uppercase (attempts GroupHeader pattern)  */
/* ------------------------------------------------------------------ */

function SectionEyebrow({
  label,
  direction,
}: {
  label: string;
  direction: 'ltr' | 'rtl';
}) {
  return (
    <Text
      color="$fg3"
      fontSize={12}
      fontWeight="700"
      fontFamily="$heading"
      textTransform="uppercase"
      letterSpacing={0.06 * 12}
      writingDirection={direction}
      accessibilityRole="header"
    >
      {label}
    </Text>
  );
}

/* ------------------------------------------------------------------ */
/* Screen                                                              */
/* ------------------------------------------------------------------ */

export default function StreakScreen() {
  const { t } = useTranslation();
  const { locale, direction, isRtl } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const reduceMotion = useReduceMotion();

  const dashboardQuery = useDashboard();

  const streak = dashboardQuery.data?.streak ?? 0;
  const freezeBalance = dashboardQuery.data?.freezeBalance ?? 0;
  const hasStreak = streak > 0;

  const isLoading = dashboardQuery.isPending;
  const isError = dashboardQuery.isError;

  const rowDir = (isRtl ? 'row-reverse' : 'row') as 'row' | 'row-reverse';
  const startAlign = (isRtl ? 'flex-end' : 'flex-start') as 'flex-start' | 'flex-end';
  const backChevron = isRtl ? '→' : '←';

  const handleBack = () => {
    if (router.canGoBack()) router.back();
    else router.replace('/(child)/' as `/${string}`);
  };

  // --- Derived copy -------------------------------------------------
  const daysText = t('streak.days', {
    count: streak,
    value: formatNumber(streak, locale),
  });
  // Never-shaming: zero-state swaps the meta line, never scolds.
  const metaText = hasStreak ? t('streak.keepAlive') : t('streak.zeroTitle');
  const heroA11y = `${t('streak.eyebrow')}: ${daysText}. ${metaText}`;

  const freezeA11y = t('streak.freeze.countA11y', {
    count: freezeBalance,
    value: formatNumber(freezeBalance, locale),
  });

  const nextMilestone =
    STREAK_MILESTONES.find((m) => streak < m) ?? null;

  // --- Flame hero (flame-loop, reduced-motion safe) ------------------
  const flameGlyph = (
    <Text
      fontSize={64}
      // Zero-state: flame at 0.4 opacity (spec §2 zero state; cross-platform
      // stand-in for the grayscale treatment — RN text has no filter).
      opacity={hasStreak ? 1 : 0.4}
      style={{ textShadowColor: colors.streakGlow, textShadowRadius: FLAME_GLOW_RADIUS }}
      accessibilityElementsHidden
    >
      {'🔥'}
    </Text>
  );

  // Reduced motion → static flame WITH glow (spec §0.1 fallback table).
  const flame =
    !reduceMotion && hasStreak ? (
      <MotiView
        from={{ scale: 1 }}
        animate={{ scale: 1.04 }}
        transition={{
          type: 'timing',
          duration: FLAME_LOOP_LEG_MS,
          loop: true,
          repeatReverse: true,
        }}
      >
        {flameGlyph}
      </MotiView>
    ) : (
      flameGlyph
    );

  return (
    <TamStack flex={1} backgroundColor="$bg" testID="streak-screen">
      {/* Top safe area */}
      <TamStack height={insets.top} />

      {/* ScreenHeader — back affordance (W12 pattern, attempts.tsx precedent) */}
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
          accessibilityLabel={t('streak.back')}
          testID="streak-back"
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
        {/* ≥768 centers at maxWidth 720 (W13 precedent, spec §0.1 layout). */}
        <YStack width="100%" maxWidth={720} alignSelf="center" gap="$5">
          {isLoading ? (
            /* Loading — skeleton blocks at final geometry (spec states). */
            <YStack testID="streak-loading" gap="$5">
              <TamStack height={180} borderRadius="$modal" backgroundColor="$cardSoft" opacity={0.6} />
              <TamStack height={96} borderRadius="$card" backgroundColor="$cardSoft" opacity={0.6} />
              <TamStack height={112} borderRadius="$card" backgroundColor="$cardSoft" opacity={0.6} />
            </YStack>
          ) : isError ? (
            /* Error strip + retry (W13 dashboard error pattern). */
            <YStack
              testID="streak-error"
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
                {`⚠️ ${t('streak.error')}`}
              </Text>
              <Button
                variant="ghost"
                size="sm"
                accessibilityLabel={t('common.retry')}
                onPress={() => dashboardQuery.refetch()}
                testID="streak-retry"
              >
                {t('common.retry')}
              </Button>
            </YStack>
          ) : (
            <>
              {/* -------------------------------------------------------- */}
              {/* Flame hero + freeze pill (spec §2.1 + §2.4)               */}
              {/* -------------------------------------------------------- */}
              <YStack
                testID="streak-hero"
                backgroundColor="$card"
                borderRadius="$modal"
                padding="$5"
                gap="$4"
                accessible
                accessibilityLabel={heroA11y}
                aria-label={heroA11y}
              >
                <XStack
                  flexDirection={rowDir}
                  alignItems="center"
                  justifyContent="space-between"
                >
                  <SectionEyebrow label={t('streak.eyebrow')} direction={direction} />

                  {/* Freeze balance pill — earned-only, NO spend UI (B8 AC). */}
                  <XStack
                    testID="streak-freeze"
                    flexDirection={rowDir}
                    alignItems="center"
                    gap="$1"
                    paddingHorizontal={12}
                    paddingVertical={6}
                    borderRadius="$pill"
                    backgroundColor={FREEZE_PILL_BG}
                    accessible
                    accessibilityLabel={freezeA11y}
                    aria-label={freezeA11y}
                  >
                    <Text fontSize={14} accessibilityElementsHidden>
                      {'❄️'}
                    </Text>
                    <Text
                      color="$gem"
                      fontSize={14}
                      fontWeight="800"
                      fontFamily="$heading"
                      style={{ fontVariant: ['tabular-nums'] }}
                      accessibilityElementsHidden
                    >
                      {formatNumber(freezeBalance, locale)}
                    </Text>
                  </XStack>
                </XStack>

                <XStack flexDirection={rowDir} alignItems="center" gap="$4">
                  {flame}
                  <YStack alignItems={startAlign} gap={2}>
                    <Text
                      color="$streak"
                      fontSize={36}
                      fontWeight="800"
                      fontFamily="$heading"
                      writingDirection={direction}
                      style={{ fontVariant: ['tabular-nums'] }}
                    >
                      {daysText}
                    </Text>
                    <Text
                      color="$fg3"
                      fontSize={12}
                      fontFamily="$body"
                      writingDirection={direction}
                    >
                      {metaText}
                    </Text>
                  </YStack>
                </XStack>

                {/* Freeze explainer (spec §2.4 copy) */}
                <Text
                  color="$fg3"
                  fontSize={12}
                  fontFamily="$body"
                  writingDirection={direction}
                >
                  {t('streak.freeze.explainer')}
                </Text>
              </YStack>

              {/* -------------------------------------------------------- */}
              {/* Milestone markers — day 3/7/14/30 (derived from `streak`) */}
              {/* -------------------------------------------------------- */}
              <YStack testID="streak-milestones" gap="$3">
                <SectionEyebrow
                  label={t('streak.milestones.eyebrow')}
                  direction={direction}
                />

                {/* List of days → follows reading direction (RTL flips). */}
                <XStack flexDirection={rowDir} justifyContent="space-between">
                  {STREAK_MILESTONES.map((milestone) => {
                    const reached = streak >= milestone;
                    const milestoneValue = formatNumber(milestone, locale);
                    const markerA11y = reached
                      ? t('streak.milestones.reachedA11y', { value: milestoneValue })
                      : t('streak.milestones.upcomingA11y', { value: milestoneValue });
                    return (
                      <YStack
                        key={milestone}
                        alignItems="center"
                        gap={6}
                        accessible
                        accessibilityLabel={markerA11y}
                        aria-label={markerA11y}
                      >
                        <TamStack
                          width={40}
                          height={40}
                          borderRadius="$pill"
                          alignItems="center"
                          justifyContent="center"
                          backgroundColor={reached ? '$streak' : '$card'}
                          borderWidth={reached ? 0 : 1}
                          borderColor="$borderStrong"
                          accessibilityElementsHidden
                        >
                          <Text
                            color={reached ? '$fg1' : '$fg4'}
                            fontSize={reached ? 16 : 13}
                            fontWeight="800"
                            fontFamily="$heading"
                            style={{ fontVariant: ['tabular-nums'] }}
                          >
                            {reached ? '✓' : milestoneValue}
                          </Text>
                        </TamStack>
                        <Text
                          color="$fg3"
                          fontSize={11}
                          fontWeight="700"
                          fontFamily="$heading"
                          textTransform="uppercase"
                          writingDirection={direction}
                          accessibilityElementsHidden
                        >
                          {t('streak.milestones.dayLabel', { value: milestoneValue })}
                        </Text>
                      </YStack>
                    );
                  })}
                </XStack>

                {/* Encouraging next-target line — no remaining-count grammar. */}
                <Text
                  color="$fg3"
                  fontSize={12}
                  fontFamily="$body"
                  writingDirection={direction}
                >
                  {nextMilestone
                    ? t('streak.milestones.next', {
                        target: formatNumber(nextMilestone, locale),
                      })
                    : t('streak.milestones.allDone')}
                </Text>
              </YStack>

              {/*
                Longest-streak KPIStatCard slot — OMITTED (spec gap G-3):
                `longestStreak` is not on DashboardDto. Do NOT stub a fake
                number. TODO(backend): render
                  <KPIStatCard icon="🔥" label={t('…')} accent="$streak" …/>
                here once the field ships.
              */}

              {/* -------------------------------------------------------- */}
              {/* Calendar/history — honest placeholder (spec gap G-2):     */}
              {/* no per-day streak-history endpoint exists; we do not      */}
              {/* fabricate per-day qualifying data.                        */}
              {/* -------------------------------------------------------- */}
              <YStack
                testID="streak-history"
                backgroundColor="$card"
                borderRadius="$card"
                borderWidth={1}
                borderColor="$borderSubtle"
                padding="$5"
                gap="$2"
                alignItems="center"
              >
                <Text fontSize={32} opacity={0.5} accessibilityElementsHidden>
                  {'📅'}
                </Text>
                <Text
                  color="$fg1"
                  fontSize={14}
                  fontWeight="700"
                  fontFamily="$heading"
                  textAlign="center"
                  writingDirection={direction}
                >
                  {t('streak.history.comingSoon')}
                </Text>
                <Text
                  color="$fg3"
                  fontSize={12}
                  fontFamily="$body"
                  textAlign="center"
                  writingDirection={direction}
                >
                  {t('streak.history.body')}
                </Text>
              </YStack>
            </>
          )}
        </YStack>
      </ScrollView>
    </TamStack>
  );
}
