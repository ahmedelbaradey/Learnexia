/**
 * Events screen — B8 / P4-11-FE (carryover batch 3b, lane B8).
 * Design Spec: design-system/ui_kits/student-app/P4-08.md §8 (B8 — freeze
 * balance · timed events · weekly challenges; derived design, spec gap G-9).
 *
 * Route: (child)/events — `href: null` push screen (NOT a tab). Batch 3c
 * registers the route in `(child)/_layout.tsx` and wires the entry point
 * (the Home timed-event banner + the ❄️ freeze count next to the streak chip
 * per spec §8.1/§8.2); until then the route is reachable by URL only.
 *
 * Data:
 * - `useDashboard()` — typed `DashboardDto` (never the AdminOnly
 *   `GET /api/admin/timed-events` — spec §8.2):
 *     `freezeBalance`     → streak-freeze count. EARNED-only — NO purchase or
 *                           spend UI (no economy exists; plan B8 AC).
 *     `activeTimedEvents` → ActiveTimedEventDto[] {code, nameEn, nameAr,
 *                           multiplier, endUtc} — XP-multiplier event banners
 *                           with a live minute-tick countdown to `endUtc`.
 * - `useMyMissions()` — typed `MyMissionsResponse.weekly: MissionStateDto[]`,
 *   filtered to the `CHALLENGE_` code prefix → the §8.3 weekly-challenge
 *   cards. THIS SCREEN OWNS the challenge cards: the Missions tab
 *   (`(child)/missions.tsx`) EXCLUDES `CHALLENGE_*` weekly rows and renders
 *   only regular weekly missions. `MissionStateDto` carries
 *   `rewardXp`/`progress`/`target`/`status`, so each card renders a progress
 *   bar + reward pill + status like the mission rows.
 *   (`DashboardDto.weeklyMission` is NOT used for challenges — the backend
 *   always fills it with the first `WEEKLY_*` row by sort order, never a
 *   `CHALLENGE_*` row.) Honest empty state when no challenges are active;
 *   the challenge section carries its own loading/error states so a
 *   missions-endpoint failure never hides the freeze/timed sections.
 *
 * Countdown: a single `setInterval` ticks once per MINUTE (not per second —
 * battery, spec §8.2); events whose `endUtc` passed are filtered out on the
 * next tick so no stale banner survives. Countdowns keep ticking under
 * reduced motion (information, not decoration — spec §8 note).
 *
 * Numerals (SKILL.md rule 5): countdown + freeze/progress counts are prose →
 * Eastern-Arabic numerals in ar via Intl (`ar-EG`), Latin in en. The XP
 * multiplier ("×2 XP") keeps Latin digits + LTR in BOTH locales (XP-counter
 * exception). The local `formatNumber` helper mirrors `(child)/streak.tsx`.
 */
import { useLocalizedRouter } from '@/hooks/useLocalizedRouter';
import {
  MissionStatusDto,
  useDashboard,
  useMyMissions,
  type ActiveTimedEventDto,
  type MissionStateDto,
} from '@learnexia/api-client';
import { gradientStops } from '@learnexia/design-system';
import { Button, GradientBox } from '@learnexia/ui';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import React from 'react';
import { Pressable, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '@/hooks/useLocale';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

/* ------------------------------------------------------------------ */
/* Fixed value sets + spec literals                                     */
/* ------------------------------------------------------------------ */

/** Spec §8.3: weekly challenges are weekly missions with this code prefix. */
const CHALLENGE_CODE_PREFIX = 'CHALLENGE_';

/** Spec §8.2: max event banners visible; overflow collapses to a ghost line. */
const MAX_VISIBLE_EVENTS = 2;

/** Countdown tick — once per minute, not per second (spec §8.2, battery). */
const COUNTDOWN_TICK_MS = 60_000;

/** Spec §2.4 literal: `$gem` family soft pill bg (no `gemSoft` token exists). */
const FREEZE_PILL_BG = 'rgba(56, 189, 248, 0.18)';

/** Spec §8.2 banner literals (mobile-mission-hero.html chrome). */
const BANNER_FG = '#FFFFFF';
const BANNER_FG_SOFT = 'rgba(255, 255, 255, 0.9)';
const BANNER_DISC_BG = 'rgba(255, 255, 255, 0.25)';
const BANNER_SHADOW = 'rgba(245, 158, 11, 0.35)';

/** Spec §8.3 literal: legendary-tile purple border on the challenge card. */
const CHALLENGE_BORDER = 'rgba(168, 85, 247, 0.3)';

/** Challenge progress-bar geometry (MissionCard chrome, spec §5.4). */
const CHALLENGE_BAR_HEIGHT = 6;
/** Spec §5.4 expired-row treatment: disabled opacity, never red. */
const EXPIRED_OPACITY = 0.4;

/**
 * Seeded challenge `titleKey` → typed i18n key (server sends keys, not copy —
 * MissionSeeder.cs P4-11 Batch 1-C). Unknown keys fall back to a generic
 * localized title (never the raw key).
 */
const CHALLENGE_TITLE_I18N = {
  'mission.challenge_25_lessons.title': 'events.challenges.titles.lessons25',
  'mission.challenge_100_correct.title': 'events.challenges.titles.correct100',
  'mission.challenge_5_day_streak.title': 'events.challenges.titles.streak5',
} as const;

/**
 * Seeded challenge `iconKey` → display glyph (non-user-facing visual glyph,
 * sibling-screen precedent: 🔥/❄️/📅 in streak.tsx).
 */
const CHALLENGE_ICON_GLYPHS = {
  'icon.mission.challenge_lessons': '📚',
  'icon.mission.challenge_correct': '✅',
  'icon.mission.challenge_streak': '🔥',
} as const;
const CHALLENGE_ICON_FALLBACK = '🏆';

/* ------------------------------------------------------------------ */
/* Locale-aware number formatting (mirrors (child)/streak.tsx)          */
/* ------------------------------------------------------------------ */

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

/** Eastern-Arabic numerals in ar, Latin in en (SKILL.md rule 5 prose counts). */
function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

/** XP counter: ALWAYS Latin digits (SKILL.md rule — even in AR). */
function formatXp(value: number): string {
  return new Intl.NumberFormat('en-US').format(value);
}

/** `endUtc` defensively to epoch ms (generated client sends Date; guard str). */
function toMillis(value: Date | undefined): number {
  return value ? new Date(value).getTime() : 0;
}

/* ------------------------------------------------------------------ */
/* Section eyebrow — lx-small uppercase (streak.tsx sibling pattern)    */
/* ------------------------------------------------------------------ */

function SectionEyebrow({
  label,
  direction,
  color = '$fg3',
}: {
  label: string;
  direction: 'ltr' | 'rtl';
  color?: '$fg3' | '$purple';
}) {
  return (
    <Text
      color={color}
      fontSize={12}
      fontWeight="800"
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
/* Empty-state card (shared by timed events + weekly challenge)         */
/* ------------------------------------------------------------------ */

function EmptyCard({
  glyph,
  title,
  body,
  direction,
  testID,
}: {
  glyph: string;
  title: string;
  body: string;
  direction: 'ltr' | 'rtl';
  testID: string;
}) {
  return (
    <YStack
      testID={testID}
      backgroundColor="$card"
      borderRadius="$card"
      borderWidth={1}
      borderColor="$borderSubtle"
      padding="$5"
      gap="$2"
      alignItems="center"
      accessible
      accessibilityLabel={`${title}. ${body}`}
      aria-label={`${title}. ${body}`}
    >
      <Text fontSize={32} opacity={0.5} accessibilityElementsHidden>
        {glyph}
      </Text>
      <Text
        color="$fg1"
        fontSize={14}
        fontWeight="700"
        fontFamily="$heading"
        textAlign="center"
        writingDirection={direction}
      >
        {title}
      </Text>
      <Text
        color="$fg3"
        fontSize={12}
        fontFamily="$body"
        textAlign="center"
        writingDirection={direction}
      >
        {body}
      </Text>
    </YStack>
  );
}

/* ------------------------------------------------------------------ */
/* Timed-event banner (spec §8.2 — mobile-mission-hero chrome)          */
/* ------------------------------------------------------------------ */

function TimedEventBanner({
  event,
  nowMs,
  locale,
  direction,
}: {
  event: ActiveTimedEventDto;
  nowMs: number;
  locale: string;
  direction: 'ltr' | 'rtl';
}) {
  const { t } = useTranslation();
  const isRtl = direction === 'rtl';
  const rowDir = (isRtl ? 'row-reverse' : 'row') as 'row' | 'row-reverse';
  const startAlign = (isRtl ? 'flex-end' : 'flex-start') as 'flex-start' | 'flex-end';

  // Localized event name from the DTO (spec §8.2: nameAr when ar, else nameEn).
  const name =
    locale === 'ar'
      ? (event.nameAr ?? event.nameEn ?? '')
      : (event.nameEn ?? event.nameAr ?? '');

  // ×N XP — Latin digits + LTR in BOTH locales (XP-counter exception).
  const multiplierText = t('events.timed.multiplier', {
    value: event.multiplier ?? 1,
  });

  // Countdown — d/h when ≥1 day, h/m otherwise, m under an hour. Prose
  // numbers → Eastern-Arabic in ar (spec §8.2 "ينتهي بعد ٥ س ١٢ د").
  const remainingMs = Math.max(0, toMillis(event.endUtc) - nowMs);
  const totalMinutes = Math.max(1, Math.floor(remainingMs / 60_000));
  const d = Math.floor(totalMinutes / 1_440);
  const h = Math.floor((totalMinutes % 1_440) / 60);
  const m = totalMinutes % 60;
  const endsText =
    d >= 1
      ? t('events.timed.endsInDayHour', {
          d: formatNumber(d, locale),
          h: formatNumber(h, locale),
        })
      : h >= 1
        ? t('events.timed.endsInHourMinute', {
            h: formatNumber(h, locale),
            m: formatNumber(m, locale),
          })
        : t('events.timed.endsInMinute', { m: formatNumber(m, locale) });

  const a11y = t('events.timed.cardA11y', {
    name,
    multiplier: multiplierText,
    ends: endsText,
  });

  return (
    <GradientBox
      stops={gradientStops.gradReward.colors}
      angle={135}
      borderRadius="$modal"
      overflow="hidden"
      shadowColor={BANNER_SHADOW}
      shadowOffset={{ width: 0, height: 8 }}
      shadowRadius={24}
      shadowOpacity={1}
      accessible
      accessibilityLabel={a11y}
      aria-label={a11y}
      testID="event-banner"
    >
      <XStack
        flexDirection={rowDir}
        alignItems="center"
        gap="$4"
        paddingVertical={18}
        paddingHorizontal={20}
      >
        {/* ⭐ disc — XP semantics (events are XP multipliers, spec §8.2). */}
        <TamStack
          width={60}
          height={60}
          borderRadius="$pill"
          backgroundColor={BANNER_DISC_BG}
          alignItems="center"
          justifyContent="center"
          flexShrink={0}
          accessibilityElementsHidden
        >
          <Text fontSize={30} accessibilityElementsHidden>
            {'⭐'}
          </Text>
        </TamStack>

        <YStack flex={1} alignItems={startAlign} gap={2}>
          <Text
            color={BANNER_FG}
            fontSize={18}
            fontWeight="900"
            fontFamily="$heading"
            writingDirection={direction}
            accessibilityElementsHidden
          >
            {name}
          </Text>
          {/* Multiplier — Latin digits, LTR-locked in both locales. */}
          <Text
            color={BANNER_FG}
            fontSize={22}
            fontWeight="900"
            fontFamily="$heading"
            writingDirection="ltr"
            style={{ fontVariant: ['tabular-nums'] }}
            accessibilityElementsHidden
          >
            {multiplierText}
          </Text>
          <Text
            color={BANNER_FG_SOFT}
            fontSize={12}
            fontFamily="$body"
            writingDirection={direction}
            accessibilityElementsHidden
          >
            {endsText}
          </Text>
        </YStack>
      </XStack>
    </GradientBox>
  );
}

/* ------------------------------------------------------------------ */
/* Weekly-challenge card (spec §8.3 — MissionCard chrome, purple).      */
/* Sourced from useMyMissions().weekly CHALLENGE_* rows (MissionStateDto */
/* carries rewardXp/progress/target/status — full mission-row anatomy). */
/* ------------------------------------------------------------------ */

function WeeklyChallengeCard({
  mission,
  locale,
  direction,
}: {
  mission: MissionStateDto;
  locale: string;
  direction: 'ltr' | 'rtl';
}) {
  const { t } = useTranslation();
  const isRtl = direction === 'rtl';
  const rowDir = (isRtl ? 'row-reverse' : 'row') as 'row' | 'row-reverse';
  const startAlign = (isRtl ? 'flex-end' : 'flex-start') as 'flex-start' | 'flex-end';

  const isCompleted = mission.status === MissionStatusDto._2;
  const isExpired = mission.status === MissionStatusDto._3;

  const titleKey =
    CHALLENGE_TITLE_I18N[
      (mission.titleKey ?? '') as keyof typeof CHALLENGE_TITLE_I18N
    ] ?? 'events.challenges.titles.fallback';
  const title = t(titleKey);

  const glyph =
    CHALLENGE_ICON_GLYPHS[
      (mission.iconKey ?? '') as keyof typeof CHALLENGE_ICON_GLYPHS
    ] ?? CHALLENGE_ICON_FALLBACK;

  const progress = mission.progress ?? 0;
  const target = Math.max(1, mission.target ?? 1);
  const rewardXp = Math.max(0, mission.rewardXp ?? 0);
  const pct = isCompleted
    ? 100
    : Math.max(0, Math.min(100, Math.round((progress * 100) / target)));

  // Prose count "6 of 10" → Eastern-Arabic numerals in ar (spec §5.4).
  const statusText = isCompleted
    ? t('events.challenges.completed')
    : isExpired
      ? t('events.challenges.expired')
      : t('events.challenges.progressLabel', {
          progress: formatNumber(progress, locale),
          target: formatNumber(target, locale),
        });

  const a11y = t('events.challenges.cardA11y', {
    title,
    status: statusText,
    xp: formatXp(rewardXp),
  });

  return (
    <YStack
      testID="weekly-challenge-card"
      backgroundColor="$purpleSoft"
      borderRadius="$card"
      borderWidth={1}
      borderColor={CHALLENGE_BORDER}
      padding={14}
      gap="$3"
      opacity={isExpired ? EXPIRED_OPACITY : 1}
      accessible
      accessibilityLabel={a11y}
      aria-label={a11y}
    >
      <XStack flexDirection={rowDir} alignItems="center" gap={14}>
        {/* Icon tile 48×48 radius 14 (MissionCard chrome, spec §5.4). */}
        <TamStack
          width={48}
          height={48}
          borderRadius={14}
          backgroundColor={isCompleted ? '$successSoft' : '$card'}
          alignItems="center"
          justifyContent="center"
          flexShrink={0}
          accessibilityElementsHidden
        >
          <Text fontSize={22} accessibilityElementsHidden>
            {isCompleted ? '✓' : glyph}
          </Text>
        </TamStack>

        <YStack flex={1} alignItems={startAlign} gap={2}>
          <Text
            color="$fg1"
            fontSize={15}
            fontWeight="700"
            fontFamily="$heading"
            writingDirection={direction}
            accessibilityElementsHidden
          >
            {title}
          </Text>
          <Text
            color="$fg3"
            fontSize={12}
            fontFamily="$body"
            writingDirection={direction}
            style={{ fontVariant: ['tabular-nums'] }}
            accessibilityElementsHidden
          >
            {statusText}
          </Text>
        </YStack>

        {/* Reward pill — "⭐ +50" $xp on $xpSoft; green when completed
            (mission-row chrome, spec §5.4). XP keeps Latin digits + LTR
            in BOTH locales (XP-counter exception). */}
        <TamStack
          backgroundColor={isCompleted ? '$successSoft' : '$xpSoft'}
          borderRadius="$pill"
          paddingHorizontal={10}
          paddingVertical={6}
          flexShrink={0}
          accessibilityElementsHidden
          importantForAccessibility="no-hide-descendants"
        >
          <Text
            color={isCompleted ? '$success' : '$xp'}
            fontSize={14}
            fontWeight="800"
            writingDirection="ltr"
            style={{ fontVariant: ['tabular-nums'] }}
          >
            {t('events.challenges.reward', { xp: formatXp(rewardXp) })}
          </Text>
        </TamStack>
      </XStack>

      {/*
       * Progress bar — LTR-locked regardless of locale (SKILL.md rule 6,
       * MasteryBar precedent): explicit flexDirection="row" so the fill
       * grows from the visual left even in RTL. Fill = gradLevelup
       * (achievement-class, spec §8.3); completed → solid $success (§5.4).
       */}
      <TamStack
        height={CHALLENGE_BAR_HEIGHT}
        borderRadius="$pill"
        backgroundColor="$bg"
        overflow="hidden"
        flexDirection="row"
        accessibilityElementsHidden
      >
        {isCompleted ? (
          <TamStack width="100%" height="100%" borderRadius="$pill" backgroundColor="$success" />
        ) : pct > 0 ? (
          <GradientBox
            stops={gradientStops.gradLevelup.colors}
            angle={135}
            width={`${pct}%`}
            height="100%"
            borderRadius="$pill"
            overflow="hidden"
          />
        ) : null}
      </TamStack>
    </YStack>
  );
}

/* ------------------------------------------------------------------ */
/* Screen                                                              */
/* ------------------------------------------------------------------ */

export default function EventsScreen() {
  const { t } = useTranslation();
  const { locale, direction, isRtl } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useLocalizedRouter();

  const dashboardQuery = useDashboard();
  // §8.3 challenge cards source — useMyMissions().weekly CHALLENGE_* rows.
  // Section-local states: a missions failure never hides freeze/timed.
  const missionsQuery = useMyMissions();

  const isLoading = dashboardQuery.isPending;
  const isError = dashboardQuery.isError;

  const freezeBalance = dashboardQuery.data?.freezeBalance ?? 0;

  // --- Live minute tick (spec §8.2 — never per-second) ---------------
  const [nowMs, setNowMs] = React.useState(() => Date.now());
  React.useEffect(() => {
    const id = setInterval(() => setNowMs(Date.now()), COUNTDOWN_TICK_MS);
    return () => clearInterval(id);
  }, []);

  // Ended events drop out on the next tick — no stale banners (spec §8.2).
  const activeEvents = React.useMemo(() => {
    const events = dashboardQuery.data?.activeTimedEvents ?? [];
    return events
      .filter((e) => toMillis(e.endUtc) > nowMs)
      .sort((a, b) => toMillis(a.endUtc) - toMillis(b.endUtc));
  }, [dashboardQuery.data?.activeTimedEvents, nowMs]);

  const visibleEvents = activeEvents.slice(0, MAX_VISIBLE_EVENTS);
  const overflowCount = activeEvents.length - visibleEvents.length;

  // Weekly challenges — ONLY CHALLENGE_*-prefixed weekly missions qualify
  // (§8.3). This screen owns these cards; missions.tsx excludes them.
  const weeklyChallenges = React.useMemo(
    () =>
      (missionsQuery.data?.weekly ?? []).filter((m) =>
        (m.code ?? '').startsWith(CHALLENGE_CODE_PREFIX),
      ),
    [missionsQuery.data?.weekly],
  );

  const rowDir = (isRtl ? 'row-reverse' : 'row') as 'row' | 'row-reverse';
  const backChevron = isRtl ? '→' : '←';

  const handleBack = () => {
    if (router.canGoBack()) router.back();
    else router.replace('/(child)/' as `/${string}`);
  };

  const freezeA11y = t('events.freeze.countA11y', {
    count: freezeBalance,
    value: formatNumber(freezeBalance, locale),
  });

  return (
    <TamStack flex={1} backgroundColor="$bg" testID="events-screen">
      {/* Top safe area */}
      <TamStack height={insets.top} />

      {/* ScreenHeader — back affordance (W12 pattern, streak.tsx precedent) */}
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
          accessibilityLabel={t('events.back')}
          testID="events-back"
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
        <TamStack flex={1} alignItems="center">
          <Text
            color="$fg1"
            fontSize={16}
            fontWeight="800"
            fontFamily="$heading"
            writingDirection={direction}
            accessibilityRole="header"
          >
            {t('events.title')}
          </Text>
        </TamStack>
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
            <YStack testID="events-loading" gap="$5">
              <TamStack height={120} borderRadius="$card" backgroundColor="$cardSoft" opacity={0.6} />
              <TamStack height={96} borderRadius="$modal" backgroundColor="$cardSoft" opacity={0.6} />
              <TamStack height={112} borderRadius="$card" backgroundColor="$cardSoft" opacity={0.6} />
            </YStack>
          ) : isError ? (
            /* Error strip + retry (W13 dashboard error pattern). */
            <YStack
              testID="events-error"
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
                {`⚠️ ${t('events.error')}`}
              </Text>
              <Button
                variant="ghost"
                size="sm"
                accessibilityLabel={t('common.retry')}
                onPress={() => dashboardQuery.refetch()}
                testID="events-retry"
              >
                {t('common.retry')}
              </Button>
            </YStack>
          ) : (
            <>
              {/* -------------------------------------------------------- */}
              {/* 1. Streak freeze — earned-only, NO buy/spend UI (B8 AC)  */}
              {/* -------------------------------------------------------- */}
              <YStack gap="$3">
                <SectionEyebrow label={t('events.freeze.eyebrow')} direction={direction} />
                <YStack
                  testID="events-freeze"
                  backgroundColor="$card"
                  borderRadius="$card"
                  borderWidth={1}
                  borderColor="$borderSubtle"
                  padding="$4"
                  gap="$3"
                  accessible
                  accessibilityLabel={`${freezeA11y}. ${t('events.freeze.explainer')}`}
                  aria-label={`${freezeA11y}. ${t('events.freeze.explainer')}`}
                >
                  <XStack flexDirection={rowDir} alignItems="center" gap="$3">
                    {/* ❄️ + count pill (spec §2.4 chrome) */}
                    <XStack
                      flexDirection={rowDir}
                      alignItems="center"
                      gap="$1"
                      paddingHorizontal={12}
                      paddingVertical={6}
                      borderRadius="$pill"
                      backgroundColor={FREEZE_PILL_BG}
                      accessibilityElementsHidden
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

                    <Text
                      flex={1}
                      color="$fg1"
                      fontSize={14}
                      fontWeight="700"
                      fontFamily="$heading"
                      writingDirection={direction}
                      accessibilityElementsHidden
                    >
                      {t('events.freeze.explainer')}
                    </Text>
                  </XStack>

                  {/* Earned-only note / zero-state encouragement. */}
                  <Text
                    color="$fg3"
                    fontSize={12}
                    fontFamily="$body"
                    writingDirection={direction}
                    accessibilityElementsHidden
                  >
                    {freezeBalance > 0
                      ? t('events.freeze.earnedNote')
                      : t('events.freeze.zeroNote')}
                  </Text>
                </YStack>
              </YStack>

              {/* -------------------------------------------------------- */}
              {/* 2. Timed events — gradReward banners + countdown (§8.2)  */}
              {/* -------------------------------------------------------- */}
              <YStack gap="$3" testID="events-timed">
                <SectionEyebrow label={t('events.timed.eyebrow')} direction={direction} />
                {visibleEvents.length === 0 ? (
                  <EmptyCard
                    glyph={'⭐'}
                    title={t('events.timed.empty.title')}
                    body={t('events.timed.empty.body')}
                    direction={direction}
                    testID="events-timed-empty"
                  />
                ) : (
                  <>
                    {visibleEvents.map((event) => (
                      <TimedEventBanner
                        key={event.code ?? String(toMillis(event.endUtc))}
                        event={event}
                        nowMs={nowMs}
                        locale={locale}
                        direction={direction}
                      />
                    ))}
                    {overflowCount > 0 ? (
                      /* "+N more" ghost line (spec §8.2 overflow rule). */
                      <Text
                        testID="events-timed-more"
                        color="$fg3"
                        fontSize={12}
                        fontWeight="700"
                        fontFamily="$body"
                        textAlign="center"
                        writingDirection={direction}
                      >
                        {t('events.timed.more', {
                          count: overflowCount,
                          value: formatNumber(overflowCount, locale),
                        })}
                      </Text>
                    ) : null}
                  </>
                )}
              </YStack>

              {/* -------------------------------------------------------- */}
              {/* 3. Weekly challenges — purple chrome (§8.3). Sourced from */}
              {/* useMyMissions().weekly filtered to CHALLENGE_* — THIS     */}
              {/* screen owns the challenge cards (missions.tsx excludes    */}
              {/* them). Section-local loading/error; honest empty state    */}
              {/* when no challenges are active.                            */}
              {/* -------------------------------------------------------- */}
              <YStack gap="$3" testID="events-challenges">
                <SectionEyebrow
                  label={t('events.challenges.eyebrow')}
                  direction={direction}
                  color="$purple"
                />
                {missionsQuery.isPending ? (
                  /* Challenge skeleton at final card geometry. */
                  <TamStack
                    testID="events-challenges-loading"
                    height={112}
                    borderRadius="$card"
                    backgroundColor="$cardSoft"
                    opacity={0.6}
                  />
                ) : missionsQuery.isError ? (
                  /* Section-local error strip + retry (W13 pattern). */
                  <YStack
                    testID="events-challenges-error"
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
                      {`⚠️ ${t('events.challenges.error')}`}
                    </Text>
                    <Button
                      variant="ghost"
                      size="sm"
                      accessibilityLabel={t('common.retry')}
                      onPress={() => missionsQuery.refetch()}
                      testID="events-challenges-retry"
                    >
                      {t('common.retry')}
                    </Button>
                  </YStack>
                ) : weeklyChallenges.length > 0 ? (
                  weeklyChallenges.map((mission) => (
                    <WeeklyChallengeCard
                      key={mission.code ?? mission.titleKey ?? ''}
                      mission={mission}
                      locale={locale}
                      direction={direction}
                    />
                  ))
                ) : (
                  <EmptyCard
                    glyph={'🏆'}
                    title={t('events.challenges.empty.title')}
                    body={t('events.challenges.empty.body')}
                    direction={direction}
                    testID="events-challenges-empty"
                  />
                )}
              </YStack>
            </>
          )}
        </YStack>
      </ScrollView>
    </TamStack>
  );
}
