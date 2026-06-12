/**
 * Missions tab — daily + weekly missions (P4-06-FE, carryover batch 3b lane
 * B5, design spec `design-system/ui_kits/student-app/P4-08.md` §5).
 *
 * Route: (child)/missions — TAB root (registered by the B0-nav shell; this
 * file only owns the screen body). Scroll content reserves the TabBar
 * clearance per `ChildTabBar` (spec §0.1 layout).
 *
 * Data: `useMyMissions()` ONLY — `MyMissionsResponse.daily/weekly:
 * MissionStateDto[]` (`code`, `iconKey`, `titleKey`, `targetType`, `target`,
 * `rewardXp`, `progress`, `status`). Percent toward target is computed
 * client-side as `progress / target` (clamped 0..1; completed rows pin to
 * 100%, `target <= 0` renders honestly at 0%). Weekly rows with the
 * `CHALLENGE_` code prefix are EXCLUDED — they belong to the B8 challenge
 * cards (spec §8.3).
 *
 * Mission titles come from the server-sent `titleKey` (an i18n key path,
 * e.g. "mission.daily_3_lessons.title") resolved inside the
 * `missions.titles.*` namespace; unknown keys fall back to a generic
 * translated title (never the raw key — i18n rule).
 *
 * Status treatments (spec §5.4): in-progress = gradXp fill + counts sub;
 * completed = celebratory (✓ tile on $successSoft, solid $success fill at
 * 100%, green reward pill, "Completed" sub); expired = muted (row opacity
 * 0.4, "Time's up" sub — never red, non-shaming).
 *
 * Mission-complete feedback (spec §5.6): when a row's status flips to
 * Completed across refetches the row flashes $successSoft once (240ms,
 * `durations.base`); when the flip completes ALL dailies, a
 * `RewardPopup variant="xp"` mounts with the total daily XP. The reward-pill
 * count-up micro-motion is NOT implemented (kept to pulse + popup — flagged
 * to reviewer).
 *
 * Reduced motion (`useReduceMotion`): bars render at target instantly, no
 * pulse; RewardPopup's confetti self-gates.
 *
 * RTL: layout is `dir`-driven; progress bars are LTR-locked (read L→R
 * universally — xp.tsx precedent); XP amounts keep Latin digits + LTR in AR
 * (SKILL.md numerals rule); prose counts ("١ من ٤") use locale digits.
 */
import { MissionStatusDto, MissionTargetTypeDto, useMyMissions } from '@learnexia/api-client';
import type { MissionStateDto } from '@learnexia/api-client';
import { durations, gradientStops } from '@learnexia/design-system';
import { Button, GradientBox, RewardPopup, useReduceMotion } from '@learnexia/ui';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { MotiView } from 'moti';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import { ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { CHILD_TAB_BAR_CLEARANCE } from './_components/ChildTabBar';
import { useLocale } from '../../src/hooks/useLocale';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

/* ------------------------------------------------------------------ */
/* Enum aliases (generated enums are numeric `_N` members — alias for   */
/* readability; values always reference the enum, never raw numbers).   */
/* ------------------------------------------------------------------ */

const MissionStatus = {
  NotStarted: MissionStatusDto._0,
  InProgress: MissionStatusDto._1,
  Completed: MissionStatusDto._2,
  Expired: MissionStatusDto._3,
} as const;

const MissionTargetType = {
  CompleteLessons: MissionTargetTypeDto._1,
  CorrectAnswers: MissionTargetTypeDto._2,
  MaintainStreak: MissionTargetTypeDto._3,
} as const;

/** B8 weekly-challenge discriminator (technical code prefix, spec §5.5/§8.3). */
const CHALLENGE_CODE_PREFIX = 'CHALLENGE_';

/* ------------------------------------------------------------------ */
/* Locale-aware formatting (local pure helpers — xp.tsx pattern)        */
/* ------------------------------------------------------------------ */

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

/** Prose count (mission counts): Eastern-Arabic digits in AR. */
function formatCount(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

/** XP counter: ALWAYS Latin digits (SKILL.md rule — even in AR). */
function formatXp(value: number): string {
  return new Intl.NumberFormat('en-US').format(value);
}

/* ------------------------------------------------------------------ */
/* Constants (spec literals)                                            */
/* ------------------------------------------------------------------ */

/** Decorative glyphs (semantic emoji set — not user-facing copy). */
const GLYPHS = {
  target: '🎯',
  gift: '🎁',
  warning: '⚠️',
  check: '✓',
  lessons: '📘',
  correct: '✅',
  streak: '🔥',
} as const;

/** Icon-tile glyph by mission target family (no asset bundle yet — emoji). */
const TARGET_GLYPHS: Record<number, string> = {
  [MissionTargetType.CompleteLessons]: GLYPHS.lessons,
  [MissionTargetType.CorrectAnswers]: GLYPHS.correct,
  [MissionTargetType.MaintainStreak]: GLYPHS.streak,
};

/** Icon-tile soft tint by mission target family (spec §5.4). */
const TARGET_TINTS = {
  [MissionTargetType.CompleteLessons]: '$primarySoft',
  [MissionTargetType.CorrectAnswers]: '$successSoft',
  [MissionTargetType.MaintainStreak]: '$streakSoft',
} as const;

/** i18n key roots for server-sent `titleKey` resolution (typed key refs). */
const TITLE_KEY_PREFIX = 'missions.titles.';
const UNKNOWN_TITLE_KEY = 'missions.titles.unknown';

/** Row progress-bar track height — spec §5.4 literal. */
const BAR_HEIGHT = 6;
/** Icon tile size / radius — spec §5.4 literals. */
const ICON_TILE_SIZE = 48;
const ICON_TILE_RADIUS = 14;
/** Reward-hero gift disc — spec §5.3 literal (60×60 on white/.25). */
const HERO_DISC_SIZE = 60;
/** Expired row opacity — spec §5.4 literal (muted, non-shaming). */
const EXPIRED_OPACITY = 0.4;

/* ------------------------------------------------------------------ */
/* Derived row model                                                    */
/* ------------------------------------------------------------------ */

interface RowModel {
  code: string;
  title: string;
  glyph: string;
  tint: (typeof TARGET_TINTS)[keyof typeof TARGET_TINTS];
  target: number;
  progress: number;
  rewardXp: number;
  ratio: number;
  isCompleted: boolean;
  isExpired: boolean;
}

function toRowModel(
  mission: MissionStateDto,
  title: string,
): RowModel {
  const status = mission.status ?? MissionStatus.NotStarted;
  const isCompleted = status === MissionStatus.Completed;
  const isExpired = status === MissionStatus.Expired;
  const target = Math.max(0, mission.target ?? 0);
  const rawProgress = Math.max(0, mission.progress ?? 0);
  const progress = target > 0 ? Math.min(rawProgress, target) : rawProgress;
  // Percent toward target — completed pins to 100%; target<=0 renders 0%
  // (honest: no fabricated progress when the server sends no target).
  const ratio = isCompleted ? 1 : target > 0 ? Math.min(1, rawProgress / target) : 0;
  const targetType = mission.targetType ?? MissionTargetType.CompleteLessons;
  return {
    code: mission.code ?? '',
    title,
    glyph: TARGET_GLYPHS[targetType] ?? GLYPHS.target,
    tint: TARGET_TINTS[targetType as keyof typeof TARGET_TINTS] ?? TARGET_TINTS[MissionTargetType.CompleteLessons],
    target,
    progress,
    rewardXp: Math.max(0, mission.rewardXp ?? 0),
    ratio,
    isCompleted,
    isExpired,
  };
}

/* ------------------------------------------------------------------ */
/* Mission row (local — spec "MissionCard" chrome, §5.4)                */
/* ------------------------------------------------------------------ */

interface MissionRowProps {
  row: RowModel;
  weekly: boolean;
  pulsing: boolean;
  reduceMotion: boolean;
  testID: string;
}

function MissionRow({ row, weekly, pulsing, reduceMotion, testID }: MissionRowProps) {
  const { t } = useTranslation();
  const { locale, direction, isRtl } = useLocale();
  const rowDir = isRtl ? 'row-reverse' : 'row';

  const sub = row.isCompleted
    ? t('missions.row.completed')
    : row.isExpired
      ? t('missions.row.expired')
      : t('missions.row.countSub', {
          progress: formatCount(row.progress, locale),
          target: formatCount(row.target, locale),
        });

  const a11y = row.isCompleted
    ? t('missions.row.completedA11y', { title: row.title, xp: formatXp(row.rewardXp) })
    : row.isExpired
      ? t('missions.row.expiredA11y', { title: row.title })
      : t('missions.row.a11y', {
          title: row.title,
          progress: formatCount(row.progress, locale),
          target: formatCount(row.target, locale),
          xp: formatXp(row.rewardXp),
        });

  const fillPct: `${number}%` = `${row.ratio * 100}%`;

  return (
    <XStack
      testID={testID}
      flexDirection={rowDir}
      alignItems="center"
      gap={14}
      backgroundColor="$card"
      borderRadius="$card"
      borderWidth={1}
      borderColor="$borderSubtle"
      paddingVertical={14}
      paddingHorizontal={16}
      opacity={row.isExpired ? EXPIRED_OPACITY : 1}
      overflow="hidden"
      accessible
      accessibilityLabel={a11y}
      aria-label={a11y}
    >
      {/* Completed-flip green pulse — $successSoft flash, 240ms (spec §5.6). */}
      {!reduceMotion ? (
        <MotiView
          pointerEvents="none"
          from={{ opacity: 0 }}
          animate={{ opacity: pulsing ? 1 : 0 }}
          transition={{ type: 'timing', duration: durations.base }}
          style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0 }}
        >
          <TamStack flex={1} backgroundColor="$successSoft" borderRadius="$card" />
        </MotiView>
      ) : null}

      {/* Icon tile — 48×48 radius 14, soft tint; ✓ on $successSoft when done. */}
      <TamStack
        width={ICON_TILE_SIZE}
        height={ICON_TILE_SIZE}
        borderRadius={ICON_TILE_RADIUS}
        backgroundColor={row.isCompleted ? '$successSoft' : row.tint}
        alignItems="center"
        justifyContent="center"
        accessibilityElementsHidden
        importantForAccessibility="no-hide-descendants"
      >
        <Text
          fontSize={row.isCompleted ? 24 : 22}
          fontWeight="800"
          color={row.isCompleted ? '$success' : '$fg1'}
        >
          {row.isCompleted ? GLYPHS.check : row.glyph}
        </Text>
      </TamStack>

      {/* Title + sub + progress */}
      <YStack flex={1} gap={6}>
        <XStack flexDirection={rowDir} alignItems="center" gap={6}>
          <Text
            color="$fg1"
            fontSize={15}
            fontWeight="700"
            fontFamily="$heading"
            writingDirection={direction}
            numberOfLines={2}
            flexShrink={1}
          >
            {row.title}
          </Text>
          {weekly ? (
            /* Weekly variant chip — $primarySoft/$primaryLight 10/700 (§5.5). */
            <TamStack
              backgroundColor="$primarySoft"
              borderRadius="$pill"
              paddingHorizontal={8}
              paddingVertical={2}
            >
              <Text
                color="$primaryLight"
                fontSize={10}
                fontWeight="700"
                textTransform="uppercase"
                letterSpacing={0.5}
                writingDirection={direction}
              >
                {t('missions.weekly.chip')}
              </Text>
            </TamStack>
          ) : null}
        </XStack>

        <Text color="$fg3" fontSize={12} writingDirection={direction}>
          {sub}
        </Text>

        {/* Progress bar — 6px track $bg, gradXp fill; LTR-LOCKED (reads L→R
            universally, xp.tsx precedent). Completed = solid $success 100%. */}
        <TamStack
          height={BAR_HEIGHT}
          borderRadius="$pill"
          backgroundColor="$bg"
          overflow="hidden"
          flexDirection="row"
          accessibilityRole="progressbar"
          accessibilityValue={{
            min: 0,
            max: Math.max(row.target, 1),
            now: row.isCompleted ? Math.max(row.target, 1) : row.progress,
          }}
        >
          {row.isCompleted ? (
            <TamStack width="100%" height="100%" borderRadius="$pill" backgroundColor="$success" />
          ) : row.ratio > 0 ? (
            reduceMotion ? (
              <TamStack width={fillPct} height="100%" borderRadius="$pill" overflow="hidden">
                <GradientBox
                  stops={gradientStops.gradXp.colors}
                  angle={gradientStops.gradXp.angle}
                  height="100%"
                  width="100%"
                  borderRadius="$pill"
                />
              </TamStack>
            ) : (
              <MotiView
                from={{ width: '0%' }}
                animate={{ width: fillPct }}
                transition={{ type: 'timing', duration: durations.celebrate }}
                style={{ height: '100%', borderRadius: 9999, overflow: 'hidden' }}
              >
                <GradientBox
                  stops={gradientStops.gradXp.colors}
                  angle={gradientStops.gradXp.angle}
                  height="100%"
                  width="100%"
                  borderRadius="$pill"
                />
              </MotiView>
            )
          ) : null}
        </TamStack>
      </YStack>

      {/* Reward pill — "⭐ +50" $xp on $xpSoft; green when completed (§5.4). */}
      <TamStack
        backgroundColor={row.isCompleted ? '$successSoft' : '$xpSoft'}
        borderRadius="$pill"
        paddingHorizontal={10}
        paddingVertical={6}
        accessibilityElementsHidden
        importantForAccessibility="no-hide-descendants"
      >
        <Text
          color={row.isCompleted ? '$success' : '$xp'}
          fontSize={14}
          fontWeight="800"
          writingDirection="ltr"
          style={{ fontVariant: ['tabular-nums'] }}
        >
          {t('missions.row.reward', { xp: formatXp(row.rewardXp) })}
        </Text>
      </TamStack>
    </XStack>
  );
}

/* ------------------------------------------------------------------ */
/* Skeleton row (loading — final geometry, W13 precedent)               */
/* ------------------------------------------------------------------ */

function SkeletonRow() {
  return (
    <TamStack
      width="100%"
      height={ICON_TILE_SIZE + 28}
      borderRadius="$card"
      backgroundColor="$cardSoft"
      opacity={0.6}
    />
  );
}

/* ------------------------------------------------------------------ */
/* Screen                                                               */
/* ------------------------------------------------------------------ */

/** All-dailies-complete celebration snapshot. */
interface Celebration {
  totalXp: number;
}

export default function MissionsScreen() {
  const { t, i18n } = useTranslation();
  const { locale, direction, isRtl } = useLocale();
  const insets = useSafeAreaInsets();
  const reduceMotion = useReduceMotion();

  const missionsQuery = useMyMissions();
  const data = missionsQuery.data;

  // Resolve a mission title from the server-sent i18n key path; unknown keys
  // fall back to a generic translated title (never the raw key).
  const missionTitle = (titleKey?: string): string => {
    const key = titleKey ? `${TITLE_KEY_PREFIX}${titleKey}` : UNKNOWN_TITLE_KEY;
    return i18n.exists(key) ? t(key) : t(UNKNOWN_TITLE_KEY);
  };

  const daily = useMemo(() => data?.daily ?? [], [data]);
  // Weekly rows EXCLUDING the B8 challenge cards (spec §5.5 / §8.3).
  const weekly = useMemo(
    () => (data?.weekly ?? []).filter((m) => !(m.code ?? '').startsWith(CHALLENGE_CODE_PREFIX)),
    [data],
  );

  const dailyRows = daily.map((m) => toRowModel(m, missionTitle(m.titleKey)));
  const weeklyRows = weekly.map((m) => toRowModel(m, missionTitle(m.titleKey)));

  const dailyDone = dailyRows.filter((r) => r.isCompleted).length;
  // Reward hero value: sum of rewardXp over INCOMPLETE dailies (spec §5.3).
  const heroXp = dailyRows
    .filter((r) => !r.isCompleted && !r.isExpired)
    .reduce((sum, r) => sum + r.rewardXp, 0);

  // --- Completed-flip feedback (spec §5.6) -------------------------------
  const prevStatusesRef = useRef<Map<string, MissionStatusDto> | null>(null);
  const [pulseCodes, setPulseCodes] = useState<readonly string[]>([]);
  const [celebration, setCelebration] = useState<Celebration | null>(null);

  useEffect(() => {
    if (!data) return;
    const all = [...(data.daily ?? []), ...(data.weekly ?? [])];
    const next = new Map<string, MissionStatusDto>(
      all.map((m) => [m.code ?? '', m.status ?? MissionStatus.NotStarted]),
    );
    const prev = prevStatusesRef.current;
    prevStatusesRef.current = next;
    if (!prev) return; // cold start — no flip on first load
    const flipped: string[] = [];
    next.forEach((status, code) => {
      const before = prev.get(code);
      if (
        before !== undefined &&
        before !== MissionStatus.Completed &&
        status === MissionStatus.Completed
      ) {
        flipped.push(code);
      }
    });
    if (flipped.length === 0) return;
    setPulseCodes(flipped);
    // ALL dailies complete + a daily flipped → "Mission Complete!" popup.
    const dailies = data.daily ?? [];
    const allDailyDone =
      dailies.length > 0 && dailies.every((m) => m.status === MissionStatus.Completed);
    const dailyFlipped = flipped.some((code) => dailies.some((m) => m.code === code));
    if (allDailyDone && dailyFlipped) {
      setCelebration({
        totalXp: dailies.reduce((sum, m) => sum + Math.max(0, m.rewardXp ?? 0), 0),
      });
    }
  }, [data]);

  // Clear the pulse after the flash window (fade-in + fade-out at 240ms).
  useEffect(() => {
    if (pulseCodes.length === 0) return;
    const timer = setTimeout(() => setPulseCodes([]), durations.base * 2);
    return () => clearTimeout(timer);
  }, [pulseCodes]);

  const isLoading = missionsQuery.isLoading;
  const isEmpty = !isLoading && !missionsQuery.isError && dailyRows.length === 0 && weeklyRows.length === 0;
  const rowDir = isRtl ? 'row-reverse' : 'row';

  const headerProgress = t('missions.header.progress', {
    done: formatCount(dailyDone, locale),
    total: formatCount(dailyRows.length, locale),
  });

  return (
    <TamStack flex={1} backgroundColor="$bg" testID="missions-screen">
      {/* Top safe area */}
      <TamStack height={insets.top} />

      <ScrollView
        contentContainerStyle={{
          paddingHorizontal: 24,
          paddingTop: 16,
          paddingBottom: insets.bottom + CHILD_TAB_BAR_CLEARANCE,
        }}
        showsVerticalScrollIndicator={false}
      >
        {/* ≥768 centers at maxWidth 720 (W13 precedent, spec §0.1 layout). */}
        <YStack width="100%" maxWidth={720} alignSelf="center" gap="$5">
          {isLoading ? (
            /* Loading — hero + 3 row skeletons at final geometry (spec §5). */
            <YStack testID="missions-loading" gap="$5">
              <TamStack
                width="100%"
                height={96}
                borderRadius="$modal"
                backgroundColor="$cardSoft"
                opacity={0.6}
              />
              <SkeletonRow />
              <SkeletonRow />
              <SkeletonRow />
            </YStack>
          ) : missionsQuery.isError ? (
            /* Error strip — W13 dashboard error pattern (strip + retry). */
            <YStack
              testID="missions-error"
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
                {`${GLYPHS.warning} ${t('missions.error')}`}
              </Text>
              <Button
                variant="ghost"
                size="sm"
                accessibilityLabel={t('common.retry')}
                onPress={() => void missionsQuery.refetch()}
                testID="missions-retry"
              >
                {t('common.retry')}
              </Button>
            </YStack>
          ) : isEmpty ? (
            /* Empty — 🎯 64px + "New missions arrive at midnight" (spec §5). */
            <YStack testID="missions-empty" alignItems="center" gap="$4" paddingTop="$8">
              <Text fontSize={64} accessibilityElementsHidden>
                {GLYPHS.target}
              </Text>
              <Text
                color="$fg3"
                fontSize={14}
                textAlign="center"
                writingDirection={direction}
              >
                {t('missions.empty')}
              </Text>
            </YStack>
          ) : (
            <>
              {/* ---------------------------------------------------------- */}
              {/* 1+2. Eyebrow + progress headline (spec §5.1–5.2)            */}
              {/* ---------------------------------------------------------- */}
              <YStack gap="$2">
                <Text
                  color="$primaryLight"
                  fontSize={12}
                  fontWeight="800"
                  fontFamily="$heading"
                  textTransform="uppercase"
                  letterSpacing={1.44}
                  writingDirection={direction}
                  textAlign={isRtl ? 'right' : 'left'}
                >
                  {`${GLYPHS.target} ${t('missions.header.eyebrow')}`}
                </Text>
                <Text
                  color="$fg1"
                  fontSize={30}
                  lineHeight={38}
                  fontWeight="800"
                  fontFamily="$heading"
                  writingDirection={direction}
                  textAlign={isRtl ? 'right' : 'left'}
                  accessibilityRole="header"
                >
                  {headerProgress}
                </Text>
                <Text
                  color="$fg3"
                  fontSize={14}
                  writingDirection={direction}
                  textAlign={isRtl ? 'right' : 'left'}
                >
                  {t('missions.header.resets')}
                </Text>
              </YStack>

              {/* ---------------------------------------------------------- */}
              {/* 3. Reward hero — gradReward banner, only while dailies      */}
              {/*    remain incomplete (no fabricated reward when all done).  */}
              {/* ---------------------------------------------------------- */}
              {heroXp > 0 ? (
                <TamStack
                  testID="missions-hero"
                  borderRadius="$modal"
                  overflow="hidden"
                  /* Spec §5.3 shadow literal: 0 8px 24px rgba(245,158,11,0.35). */
                  shadowColor="rgba(245,158,11,0.35)"
                  shadowOffset={{ width: 0, height: 8 }}
                  shadowRadius={24}
                  accessible
                  accessibilityLabel={t('missions.hero.a11y', { xp: formatXp(heroXp) })}
                  aria-label={t('missions.hero.a11y', { xp: formatXp(heroXp) })}
                >
                  <GradientBox
                    stops={gradientStops.gradReward.colors}
                    angle={135}
                    width="100%"
                    borderRadius="$modal"
                  >
                    <XStack
                      flexDirection={rowDir}
                      alignItems="center"
                      gap={14}
                      paddingVertical={18}
                      paddingHorizontal={20}
                    >
                      {/* 🎁 disc 60×60 on white/.25 (spec literal). */}
                      <TamStack
                        width={HERO_DISC_SIZE}
                        height={HERO_DISC_SIZE}
                        borderRadius="$pill"
                        backgroundColor="rgba(255,255,255,0.25)"
                        alignItems="center"
                        justifyContent="center"
                        accessibilityElementsHidden
                        importantForAccessibility="no-hide-descendants"
                      >
                        <Text fontSize={28}>{GLYPHS.gift}</Text>
                      </TamStack>
                      <YStack flex={1} gap={2} accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
                        {/* "+125 XP" — white 22/900, Latin digits LTR even in AR. */}
                        <Text
                          color="#FFFFFF"
                          fontSize={22}
                          fontWeight="900"
                          fontFamily="$heading"
                          writingDirection="ltr"
                          textAlign={isRtl ? 'right' : 'left'}
                          style={{ fontVariant: ['tabular-nums'] }}
                        >
                          {t('missions.hero.xp', { xp: formatXp(heroXp) })}
                        </Text>
                        {/* No badge implication in the server data → finish-all
                            sub (spec §5.3 conditional copy). */}
                        <Text
                          color="rgba(255,255,255,0.9)"
                          fontSize={12}
                          writingDirection={direction}
                          textAlign={isRtl ? 'right' : 'left'}
                        >
                          {t('missions.hero.sub')}
                        </Text>
                      </YStack>
                    </XStack>
                  </GradientBox>
                </TamStack>
              ) : null}

              {/* ---------------------------------------------------------- */}
              {/* 4. Daily list (spec §5.4)                                   */}
              {/* ---------------------------------------------------------- */}
              <YStack gap="$3" testID="missions-daily">
                {dailyRows.map((row) => (
                  <MissionRow
                    key={row.code}
                    row={row}
                    weekly={false}
                    pulsing={pulseCodes.includes(row.code)}
                    reduceMotion={reduceMotion}
                    testID={`mission-row-${row.code}`}
                  />
                ))}
              </YStack>

              {/* ---------------------------------------------------------- */}
              {/* 5. Weekly section (spec §5.5; CHALLENGE_* excluded → B8)    */}
              {/* ---------------------------------------------------------- */}
              {weeklyRows.length > 0 ? (
                <YStack gap="$3" testID="missions-weekly">
                  <Text
                    color="$primaryLight"
                    fontSize={12}
                    fontWeight="800"
                    fontFamily="$heading"
                    textTransform="uppercase"
                    letterSpacing={1.44}
                    writingDirection={direction}
                    textAlign={isRtl ? 'right' : 'left'}
                  >
                    {t('missions.weekly.eyebrow')}
                  </Text>
                  {weeklyRows.map((row) => (
                    <MissionRow
                      key={row.code}
                      row={row}
                      weekly
                      pulsing={pulseCodes.includes(row.code)}
                      reduceMotion={reduceMotion}
                      testID={`mission-row-${row.code}`}
                    />
                  ))}
                </YStack>
              ) : null}
            </>
          )}
        </YStack>
      </ScrollView>

      {/* ------------------------------------------------------------------ */}
      {/* All-dailies-complete moment — RewardPopup (xp variant, spec §5.6).  */}
      {/* ------------------------------------------------------------------ */}
      {celebration ? (
        <RewardPopup
          variant="xp"
          xpAmount={celebration.totalXp}
          title={t('missions.complete.title')}
          subtitle={t('missions.complete.subtitle')}
          visible
          ctaLabel={t('missions.complete.cta')}
          onDismiss={() => setCelebration(null)}
          locale={locale}
          accessibilityLabel={t('missions.complete.a11y', {
            xp: formatXp(celebration.totalXp),
          })}
        />
      ) : null}
    </TamStack>
  );
}
