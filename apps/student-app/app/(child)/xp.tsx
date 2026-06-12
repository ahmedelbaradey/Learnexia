/**
 * XP & Level screen + level-up moment — child push screen (P4-02-FE, carryover
 * batch 3a lane B1, design spec `design-system/ui_kits/student-app/P4-08.md` §1).
 *
 * Route: (child)/xp — secondary `href: null` push screen (NOT a tab),
 * registered in `(child)/_layout.tsx` by batch 3c (B-int owns the shell).
 * Entry point (3c wires): tap the XP value / XP bar in the Home
 * `DashboardHeader`; until then the route is reachable by URL / deep link.
 *
 * Data: `useDashboard()` ONLY — typed `DashboardDto.xp` (total XP) +
 * `DashboardDto.level` (computed by the BE LevelCurve). The DTO carries NO
 * xp-into-level / xp-to-next-level fields and `GET /api/Gamification/Profile`
 * is generated untyped (spec gap G-1), so the level window is DERIVED here
 * from the published backend curve (`Gamification.Domain.Services.LevelCurve`
 * thresholds, mirrored as a const below) anchored on the BE-sent `level`.
 * Honesty guard: if the BE total XP ever falls outside the derived window
 * (curve drift), the card drops the per-level numbers (shows total XP only,
 * empty track, no footer) rather than fabricating progress. Carry-forward:
 * replace the mirror with the typed Profile response when G-1 is fixed.
 *
 * Recent XP history is OUT OF SCOPE (no endpoint — spec §1.4); not mocked.
 *
 * Level-up moment (spec §1): `useDashboardDiff` reports `levelDelta > 0`
 * across dashboard refreshes → mount `RewardPopup variant="level-up"`
 * (popup-in + its internal level-up ConfettiLayer burst) → on dismiss the
 * hero level number runs the count-up motion (600ms spec literal) and the
 * XP bar re-runs xp-fill from the old value to the new one.
 *
 * Reduced motion (`useReduceMotion`): no bar fill animation (renders at
 * target), no count-up (jump to final), and ConfettiLayer self-gates to none.
 *
 * RTL: layout is `dir`-driven; the XP bar is LTR-LOCKED (progress reads L→R
 * universally, spec §1.2); "n / m XP" counters keep Latin digits + LTR in AR
 * (SKILL.md numerals rule); level numbers use Eastern-Arabic digits in AR.
 */
import { useDashboard } from '@learnexia/api-client';
import { durations, gradientStops } from '@learnexia/design-system';
import {
  Button,
  GradientBox,
  KPIStatCard,
  RewardPopup,
  useDashboardDiff,
  useReduceMotion,
} from '@learnexia/ui';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { MotiView } from 'moti';
import React, { useEffect, useMemo, useState } from 'react';
import { Pressable, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '../../src/hooks/useLocale';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

/* ------------------------------------------------------------------ */
/* Backend level-curve mirror (spec gap G-1 — see file header)         */
/* ------------------------------------------------------------------ */

/**
 * Cumulative XP floors for levels 1–10 — mirrors
 * `backend .. Gamification.Domain.Services.LevelCurve.Thresholds` exactly.
 * `LEVEL_XP_FLOORS[i]` = total XP at which level (i+1) starts.
 */
const LEVEL_XP_FLOORS = [0, 100, 250, 500, 1000, 2000, 4000, 7000, 11000, 16000] as const;
/** L11+ linear extension — +5000 XP per level (LevelCurve mirror). */
const XP_PER_LEVEL_BEYOND_TABLE = 5000;

/** Total XP at which `level` (1-based) starts. */
function xpFloorForLevel(level: number): number {
  const clamped = Math.max(1, Math.floor(level));
  if (clamped <= LEVEL_XP_FLOORS.length) {
    return LEVEL_XP_FLOORS[clamped - 1] ?? 0;
  }
  return (
    LEVEL_XP_FLOORS[LEVEL_XP_FLOORS.length - 1]! +
    (clamped - LEVEL_XP_FLOORS.length) * XP_PER_LEVEL_BEYOND_TABLE
  );
}

/* ------------------------------------------------------------------ */
/* Locale-aware formatting (local pure helpers — attempts.tsx pattern) */
/* ------------------------------------------------------------------ */

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

/** Prose count (levels): Eastern-Arabic digits in AR. */
function formatCount(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

/** XP counter: ALWAYS Latin digits (SKILL.md rule — even in AR). */
function formatXp(value: number): string {
  return new Intl.NumberFormat('en-US').format(value);
}

/** Fixed percent glyphs per locale (unit symbols, not copy). */
const PERCENT_SIGN = { en: '%', ar: '٪' } as const;

/** "85%" / "٨٥٪". */
function formatPercent(value: number, locale: string): string {
  const sign = locale === 'ar' ? PERCENT_SIGN.ar : PERCENT_SIGN.en;
  return `${formatCount(Math.round(value), locale)}${sign}`;
}

/* ------------------------------------------------------------------ */
/* Constants                                                           */
/* ------------------------------------------------------------------ */

/** Decorative glyphs (semantic emoji set — not user-facing copy). */
const GLYPHS = {
  star: '⭐',
  warning: '⚠️',
} as const;

/** Hero level number — spec §1.1 `lx-display` scale literal (48/800). */
const HERO_FONT_SIZE = 48;
/** Hero glow disc diameter (derived — soft `$primarySoft` disc, spec §1.1). */
const HERO_DISC_SIZE = 160;
/** Progress bar track height — spec §1.2 literal. */
const BAR_HEIGHT = 12;
/** Count-up duration — spec §0.1 motion table literal (600ms). */
const COUNT_UP_MS = 600;

/** Level-up celebration snapshot (set when `useDashboardDiff` fires). */
interface Celebration {
  fromLevel: number;
  toLevel: number;
  fromXp: number;
  xpGained: number;
}

/* ------------------------------------------------------------------ */
/* Screen                                                              */
/* ------------------------------------------------------------------ */

export default function XpScreen() {
  const { t } = useTranslation();
  const { locale, direction, isRtl } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const reduceMotion = useReduceMotion();

  const dashboardQuery = useDashboard();

  // Narrowed, MEMOIZED `DashboardLike` subset — this screen only consumes
  // xp/level deltas. (The generated `MissionSummary.status` is the numeric
  // `MissionStatusDto` enum, incompatible with the salvaged hook's
  // string-status shape — full diff plumbing is batch 3c's seam; flagged.)
  const diffInput = useMemo(
    () =>
      dashboardQuery.data
        ? { xp: dashboardQuery.data.xp, level: dashboardQuery.data.level }
        : undefined,
    [dashboardQuery.data],
  );
  const diff = useDashboardDiff(diffInput);

  const xp = dashboardQuery.data?.xp ?? 0;
  const level = dashboardQuery.data?.level ?? 1;

  // --- Level-up celebration state ---------------------------------------
  const [celebration, setCelebration] = useState<Celebration | null>(null);
  // Hero count-up override (post-dismiss); null = show the live level.
  const [heroOverride, setHeroOverride] = useState<number | null>(null);
  const [countUp, setCountUp] = useState<{ from: number; to: number } | null>(null);

  // Trigger: the dashboard diff reports a level increase across refreshes
  // (cold-start safe — first load always emits ZERO_DIFF).
  useEffect(() => {
    if (diff.levelDelta > 0) {
      const xpGained = Math.max(diff.xpDelta, 0);
      setCelebration({
        fromLevel: level - diff.levelDelta,
        toLevel: level,
        fromXp: Math.max(0, xp - xpGained),
        xpGained,
      });
    }
    // `level`/`xp` change exactly when `diff` re-emits (same data source).
  }, [diff, level, xp]);

  // Count-up motion: tick the hero number old → new over 600ms (spec §0.1).
  useEffect(() => {
    if (!countUp) return;
    const start = Date.now();
    let raf: number;
    const tick = () => {
      const progress = Math.min(1, (Date.now() - start) / COUNT_UP_MS);
      setHeroOverride(Math.round(countUp.from + (countUp.to - countUp.from) * progress));
      if (progress < 1) {
        raf = requestAnimationFrame(tick);
      } else {
        setCountUp(null);
        setHeroOverride(null);
      }
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [countUp]);

  // Popup dismiss → count-up (motion-gated), then the bar re-fills because
  // the displayed window flips from the old snapshot to the live values.
  const handleDismissCelebration = () => {
    const current = celebration;
    setCelebration(null);
    if (!current) return;
    if (reduceMotion || current.toLevel <= current.fromLevel) return;
    setHeroOverride(current.fromLevel);
    setCountUp({ from: current.fromLevel, to: current.toLevel });
  };

  // --- Displayed window (old snapshot while the popup is up) -------------
  const displayLevel = celebration?.fromLevel ?? level;
  const displayXp = celebration?.fromXp ?? xp;
  const heroLevel = heroOverride ?? displayLevel;

  const windowFloor = xpFloorForLevel(displayLevel);
  const windowNext = xpFloorForLevel(displayLevel + 1);
  const windowSpan = windowNext - windowFloor;
  const xpIntoLevel = displayXp - windowFloor;
  const xpLeft = windowNext - displayXp;
  // Honesty guard (G-1): the BE-sent total must sit inside the derived window.
  const curveAligned = windowSpan > 0 && xpIntoLevel >= 0 && xpLeft > 0;
  const ratio = curveAligned ? Math.min(1, xpIntoLevel / windowSpan) : 0;
  const percent = Math.round(ratio * 100);
  const fillPct: `${number}%` = `${ratio * 100}%`;

  const isLoading = dashboardQuery.isLoading;
  const rowDir = isRtl ? 'row-reverse' : 'row';
  const backChevron = isRtl ? '→' : '←';

  const handleBack = () => {
    if (router.canGoBack()) router.back();
    else router.replace('/(child)/' as `/${string}`);
  };

  const heroLevelText = t('xp.hero.level', { value: formatCount(heroLevel, locale) });
  const counterText = curveAligned
    ? t('xp.progress.counter', {
        current: formatXp(xpIntoLevel),
        total: formatXp(windowSpan),
      })
    : t('xp.progress.counterTotalOnly', { xp: formatXp(displayXp) });
  const barA11y = curveAligned
    ? t('xp.progress.barA11y', {
        current: formatXp(xpIntoLevel),
        total: formatXp(windowSpan),
        next: formatCount(displayLevel + 1, locale),
      })
    : counterText;

  return (
    <TamStack flex={1} backgroundColor="$bg" testID="xp-screen">
      {/* Top safe area */}
      <TamStack height={insets.top} />

      {/* ScreenHeader — back affordance (W12 pattern, push-screen shell). */}
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
          accessibilityLabel={t('xp.back')}
          testID="xp-back"
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
        <YStack width="100%" maxWidth={720} alignSelf="center" gap="$6">
          {isLoading ? (
            /* Loading — hero + card skeletons at final geometry (spec §1). */
            <YStack testID="xp-loading" gap="$6" alignItems="center">
              <TamStack
                width={HERO_DISC_SIZE}
                height={HERO_DISC_SIZE}
                borderRadius={9999}
                backgroundColor="$cardSoft"
                opacity={0.6}
              />
              <TamStack
                width="100%"
                height={108}
                borderRadius="$card"
                backgroundColor="$cardSoft"
                opacity={0.6}
              />
              <TamStack
                width="100%"
                height={72}
                borderRadius="$card"
                backgroundColor="$cardSoft"
                opacity={0.6}
              />
            </YStack>
          ) : dashboardQuery.isError ? (
            /* Error strip — W13 dashboard error pattern (strip + retry). */
            <YStack
              testID="xp-error"
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
                accessibilityRole="alert"
              >
                {`${GLYPHS.warning} ${t('xp.error')}`}
              </Text>
              <Button
                variant="ghost"
                size="sm"
                accessibilityLabel={t('common.retry')}
                onPress={() => void dashboardQuery.refetch()}
                testID="xp-retry"
              >
                {t('common.retry')}
              </Button>
            </YStack>
          ) : (
            <>
              {/* ---------------------------------------------------------- */}
              {/* 1. Level hero — eyebrow + display number on a soft disc.    */}
              {/* ---------------------------------------------------------- */}
              <YStack
                testID="xp-hero"
                alignItems="center"
                gap="$3"
                accessible
                accessibilityLabel={t('xp.hero.a11y', {
                  level: formatCount(heroLevel, locale),
                })}
                aria-label={t('xp.hero.a11y', {
                  level: formatCount(heroLevel, locale),
                })}
              >
                <Text
                  color="$fg3"
                  fontSize={12}
                  fontWeight="700"
                  fontFamily="$heading"
                  textTransform="uppercase"
                  letterSpacing={1.2}
                  writingDirection={direction}
                  accessibilityElementsHidden
                >
                  {t('xp.hero.eyebrow')}
                </Text>
                {/* Soft $primarySoft glow disc (flat-soft approximation of the
                    radial glow — token-only, spec §1.1). */}
                <TamStack
                  width={HERO_DISC_SIZE}
                  height={HERO_DISC_SIZE}
                  borderRadius={9999}
                  backgroundColor="$primarySoft"
                  alignItems="center"
                  justifyContent="center"
                  accessibilityElementsHidden
                  importantForAccessibility="no-hide-descendants"
                >
                  <Text
                    color="$fg1"
                    fontSize={HERO_FONT_SIZE}
                    lineHeight={HERO_FONT_SIZE + 8}
                    fontWeight="800"
                    fontFamily="$heading"
                    textAlign="center"
                    writingDirection={direction}
                    style={{ fontVariant: ['tabular-nums'] }}
                  >
                    {heroLevelText}
                  </Text>
                </TamStack>
              </YStack>

              {/* ---------------------------------------------------------- */}
              {/* 2. Level progress card (preview/mobile-level-progress.html) */}
              {/* ---------------------------------------------------------- */}
              <YStack
                testID="xp-progress-card"
                backgroundColor="$card"
                borderRadius="$card"
                borderWidth={1}
                borderColor="$borderSubtle"
                padding={16}
                gap="$3"
              >
                {/* Header row: "📈 Level 5 → 6" ↔ "850 / 1000 XP" (17/700). */}
                <XStack
                  flexDirection={rowDir}
                  justifyContent="space-between"
                  alignItems="center"
                  gap="$3"
                >
                  <Text
                    color="$fg2"
                    fontSize={17}
                    fontWeight="700"
                    fontFamily="$heading"
                    writingDirection={direction}
                  >
                    {t('xp.progress.header', {
                      from: formatCount(displayLevel, locale),
                      to: formatCount(displayLevel + 1, locale),
                    })}
                  </Text>
                  {/* XP counter — Latin digits + LTR in BOTH locales (spec §1.2). */}
                  <Text
                    color="$fg2"
                    fontSize={17}
                    fontWeight="700"
                    fontFamily="$heading"
                    writingDirection="ltr"
                    style={{ fontVariant: ['tabular-nums'] }}
                  >
                    {counterText}
                  </Text>
                </XStack>

                {/* Bar — 12px track, $bgElevated, pill radius, gradXp fill.
                    LTR-LOCKED: progress reads L→R universally (spec §1.2). */}
                <TamStack
                  height={BAR_HEIGHT}
                  borderRadius={9999}
                  backgroundColor="$bgElevated"
                  overflow="hidden"
                  flexDirection="row"
                  accessibilityRole="progressbar"
                  accessible
                  accessibilityLabel={barA11y}
                  aria-label={barA11y}
                  accessibilityValue={{
                    min: 0,
                    max: Math.max(windowSpan, 1),
                    now: Math.max(0, Math.min(xpIntoLevel, windowSpan)),
                  }}
                >
                  {curveAligned ? (
                    reduceMotion ? (
                      /* Reduced motion — render at target instantly (§0.1). */
                      <TamStack
                        width={fillPct}
                        height="100%"
                        borderRadius={9999}
                        overflow="hidden"
                      >
                        <GradientBox
                          stops={gradientStops.gradXp.colors}
                          angle={90}
                          height="100%"
                          width="100%"
                          borderRadius={9999}
                        />
                      </TamStack>
                    ) : (
                      /* xp-fill — width animates to target over 700ms (§0.1). */
                      <MotiView
                        from={{ width: '0%' }}
                        animate={{ width: fillPct }}
                        transition={{ type: 'timing', duration: durations.celebrate }}
                        style={{ height: '100%', borderRadius: 9999, overflow: 'hidden' }}
                      >
                        <GradientBox
                          stops={gradientStops.gradXp.colors}
                          angle={90}
                          height="100%"
                          width="100%"
                          borderRadius={9999}
                        />
                      </MotiView>
                    )
                  ) : null}
                </TamStack>

                {/* Footer row: "85% to next level" ↔ "150 XP left" (13/$fg3).
                    Hidden when the derived window is not trustworthy (G-1). */}
                {curveAligned ? (
                  <XStack
                    flexDirection={rowDir}
                    justifyContent="space-between"
                    alignItems="center"
                    gap="$3"
                  >
                    <Text
                      color="$fg3"
                      fontSize={13}
                      fontFamily="$body"
                      writingDirection={direction}
                    >
                      {t('xp.progress.footerPercent', {
                        percent: formatPercent(percent, locale),
                        next: formatCount(displayLevel + 1, locale),
                      })}
                    </Text>
                    <Text
                      color="$fg3"
                      fontSize={13}
                      fontFamily="$body"
                      writingDirection={direction}
                      style={{ fontVariant: ['tabular-nums'] }}
                    >
                      {t('xp.progress.footerLeft', { xp: formatXp(xpLeft) })}
                    </Text>
                  </XStack>
                ) : null}
              </YStack>

              {/* ---------------------------------------------------------- */}
              {/* 3. Total XP stat tile — KPIStatCard on card chrome (§1.3).  */}
              {/* ---------------------------------------------------------- */}
              <XStack
                testID="xp-total-tile"
                backgroundColor="$card"
                borderRadius="$card"
                borderWidth={1}
                borderColor="$borderSubtle"
                padding={12}
              >
                <KPIStatCard
                  icon={GLYPHS.star}
                  value={formatXp(displayXp)}
                  label={t('xp.totalXp.label')}
                  accent="$xp"
                  variant="tile"
                  direction={direction}
                  accessibilityLabel={t('xp.totalXp.a11y', { xp: formatXp(displayXp) })}
                />
              </XStack>

              {/* §1.4 — recent XP history: OUT OF SCOPE (no endpoint), not mocked. */}
            </>
          )}
        </YStack>
      </ScrollView>

      {/* ------------------------------------------------------------------ */}
      {/* Level-up moment — RewardPopup (level-up variant). Confetti is the   */}
      {/* popup's internal level-up ConfettiLayer (self-gated by reduce-motion).*/}
      {/* ------------------------------------------------------------------ */}
      {celebration ? (
        <RewardPopup
          variant="level-up"
          xpAmount={celebration.xpGained}
          title={t('xp.levelUp.title')}
          subtitle={t('xp.levelUp.subtitle', {
            level: formatCount(celebration.toLevel, locale),
          })}
          visible
          ctaLabel={t('xp.levelUp.cta')}
          onDismiss={handleDismissCelebration}
          locale={locale}
          accessibilityLabel={t('xp.levelUp.a11y', {
            level: formatCount(celebration.toLevel, locale),
            xp: formatXp(celebration.xpGained),
          })}
        />
      ) : null}
    </TamStack>
  );
}
