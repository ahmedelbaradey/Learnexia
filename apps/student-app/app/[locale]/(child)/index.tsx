/**
 * Child Home Dashboard — W13 P2-09-FE
 *
 * Replaces the W11 bare Subjects list with a personalized dashboard:
 *   TopBar (logo + sign-out, preserved from W11)
 *   → DashboardHeader (greeting + Hearts/Streak/XP strip)
 *   → ContinueCard (conditional — only when dashboardQuery.data?.continue is non-null)
 *   → MissionBanner (always null in Phase 2 → never rendered per AC6)
 *   → SubjectsListSection (W11 logic preserved, extracted to _components)
 *
 * P4-04 data flip:
 *   Hearts: dashboardQuery.data?.hearts ?? 5 (real from BE — was hard-coded 3)
 *   InPracticeMode: dashboardQuery.data?.inPracticeMode (pill shown when true)
 *   Level: dashboardQuery.data?.level ?? 1 (real from BE — was hard-coded 1)
 *
 * P4-07 data flip:
 *   LeaguePreview: dashboardQuery.data?.leaguePreview — now populated by BE;
 *   shows tier name + rank row. Hidden when leaguePreview is null (brand-new student
 *   before the BE ships league engine — defensive fallback).
 *
 * Carryover batch 3c (B-int) — gamification integration (Design Spec
 * design-system/ui_kits/student-app/P4-08.md, dashboard/celebration sections):
 *   Entry points: hearts chip → /(child)/hearts · streak chip → /(child)/streak
 *   · ❄️ freeze chip (when earned) + timed-event banner → /(child)/events ·
 *   XP bar → /(child)/xp · league preview → /(child)/league (tab) ·
 *   "My activity" row → /(child)/attempts. The live header is recomposed from
 *   the kit primitives so each chip is a ≥48px tap target (the kit
 *   DashboardHeader is non-interactive by design; loading still renders its
 *   skeleton). Celebrations: useDashboardDiff (status-enum adapter at the call
 *   site) queues ONE RewardPopup per diff occurrence — priority level-up →
 *   badge → streak milestone → mission-completed; cold-start safe (ZERO_DIFF).
 *   Scroll content reserves CHILD_TAB_BAR_CLEARANCE under the floating TabBar.
 *
 * Remaining stubs (carry inline TODO comments pointing to Phase-4 story):
 *   WeeklyXpTarget: 100 (TODO P4-02 — weekly aggregation target)
 *   DailyMission widget + recent-badges row: flagged carry-forward (TODO P4-06 /
 *   P4-05 — outside the B-int task scope, which wires entry points only)
 *
 * Acceptance criteria reference: AC1–AC13 in docs/briefs/W13-P2-09-FE.md.
 * Design Spec: design-system/ui_kits/student-mobile/W13-home-dashboard.md.
 */

import { useLocalizedRouter } from '@/hooks/useLocalizedRouter';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import { ScrollView, Image } from 'react-native';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import {
  BadgeRarityDto,
  MissionStatusDto,
  useMe,
  useDashboard,
  type ActiveTimedEventDto,
  type DashboardDto,
} from '@learnexia/api-client';
import {
  ContinueCard,
  DashboardHeader,
  GradientBox,
  Hearts,
  RewardPopup,
  StreakFlame,
  XPBar,
  useDashboardDiff,
  type BadgeVariant,
  type DashboardLike,
  type RewardVariant,
} from '@learnexia/ui';
import { gradientStops } from '@learnexia/design-system';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { assets } from '@/assets';
import { useLocale } from '@/hooks/useLocale';
import { useSignOutAction } from '@/hooks/useSignOutAction';
import { CHILD_TAB_BAR_CLEARANCE } from './_components/ChildTabBar';
import { resolveSubjectKey } from './_components/subjects';
import { SubjectsListSection } from './_components/SubjectsListSection';

const XStack = styled(TamStack, { flexDirection: 'row' });
const YStack = styled(TamStack, { flexDirection: 'column' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

/* ------------------------------------------------------------------ */
/* Locale-aware formatting (local pure helpers — xp.tsx/attempts.tsx    */
/* pattern; lifting into packages/shared is a noted carry-forward)      */
/* ------------------------------------------------------------------ */

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

/** Prose count (hearts/streak/level/freezes): Eastern-Arabic digits in AR. */
function formatCount(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

/** XP counter: ALWAYS Latin digits (SKILL.md rule — even in AR). */
function formatXp(value: number): string {
  return new Intl.NumberFormat('en-US').format(value);
}

/* ------------------------------------------------------------------ */
/* Fixed value sets + spec literals (3c / B-int)                        */
/* ------------------------------------------------------------------ */

/** Streak milestones celebrated on the dashboard (mirror of streak.tsx markers). */
const STREAK_MILESTONES: readonly number[] = [3, 7, 14, 30];

/** Spec §2.4 literal: `$gem` family soft pill bg (no `gemSoft` token exists — streak.tsx precedent). */
const FREEZE_PILL_BG = 'rgba(56, 189, 248, 0.18)';

/** Spec §8.2 banner literals (mobile-mission-hero.html chrome — events.tsx precedent). */
const BANNER_FG = '#FFFFFF';
const BANNER_FG_SOFT = 'rgba(255, 255, 255, 0.9)';
const BANNER_DISC_BG = 'rgba(255, 255, 255, 0.25)';
const BANNER_SHADOW = 'rgba(245, 158, 11, 0.35)';

/** Decorative glyphs (semantic emoji set — not user-facing copy). */
const GLYPHS = { freeze: '❄️', star: '⭐', warning: '⚠️' } as const;

/** Mission titleKey i18n roots (typed key refs — missions.tsx precedent). */
const MISSION_TITLE_KEY_PREFIX = 'missions.titles.';
const MISSION_UNKNOWN_TITLE_KEY = 'missions.titles.unknown';

/* ------------------------------------------------------------------ */
/* useDashboardDiff input adapter (B1-flagged seam)                     */
/*                                                                      */
/* `DashboardLike` expects STRING mission statuses (the hook's          */
/* 'Completed' contract in diffCompletedMissions), while the generated  */
/* `MissionSummary.status` is the NUMERIC `MissionStatusDto` enum. The  */
/* mapping lives here at the call site — the hook itself is untouched.  */
/* ------------------------------------------------------------------ */

/** String statuses of the `useDashboardDiff` contract (technical identifiers). */
const DiffMissionStatus = {
  Completed: 'Completed',
  NotCompleted: 'NotCompleted',
} as const;

function toDiffMissionStatus(status?: MissionStatusDto): string {
  return status === MissionStatusDto._2
    ? DiffMissionStatus.Completed
    : DiffMissionStatus.NotCompleted;
}

/** Maps the typed `DashboardDto` onto the hook's structural input shape. */
function toDashboardLike(data: DashboardDto | undefined): DashboardLike | undefined {
  if (!data) return undefined;
  return {
    xp: data.xp,
    level: data.level,
    streak: data.streak,
    hearts: data.hearts,
    inPracticeMode: data.inPracticeMode,
    recentBadges: data.recentBadges?.map((b) => ({ code: b.code })),
    dailyMissions: data.dailyMissions?.map((m) => ({
      code: m.code,
      status: toDiffMissionStatus(m.status),
    })),
    weeklyMission: data.weeklyMission
      ? {
          code: data.weeklyMission.code,
          status: toDiffMissionStatus(data.weeklyMission.status),
        }
      : null,
    leaguePreview: data.leaguePreview
      ? { tierName: data.leaguePreview.tierName }
      : null,
  };
}

/** Badge rarity (numeric DTO) → kit Badge variant for the celebration popup. */
const BADGE_RARITY_VARIANT: Record<BadgeRarityDto, BadgeVariant> = {
  [BadgeRarityDto._1]: 'bronze',
  [BadgeRarityDto._2]: 'silver',
  [BadgeRarityDto._3]: 'gold',
  [BadgeRarityDto._4]: 'legendary',
};

/* ------------------------------------------------------------------ */
/* Dashboard celebrations (3c — spec celebration section)               */
/* ------------------------------------------------------------------ */

/** Celebration kinds fired from the dashboard diff (fixed value set). */
const CelebrationKind = {
  LevelUp: 'levelUp',
  Badge: 'badge',
  StreakMilestone: 'streakMilestone',
  MissionCompleted: 'missionCompleted',
} as const;
type CelebrationKindValue = (typeof CelebrationKind)[keyof typeof CelebrationKind];

/**
 * One queued celebration occurrence. Raw values only — copy is resolved at
 * render time so it stays locale-reactive.
 */
interface HomeCelebration {
  kind: CelebrationKindValue;
  /** Total XP gained in the dashboard refresh that produced this occurrence. */
  xpGained: number;
  /** LevelUp: the new level · StreakMilestone: the milestone day count. */
  value: number;
  /** Badge: badge code · MissionCompleted: mission code. */
  code: string;
  /** Badge: rarity-mapped popup badge variant. */
  badgeVariant: BadgeVariant | null;
  /** MissionCompleted: server titleKey for subtitle resolution. */
  titleKey: string | null;
}

// ---------------------------------------------------------------------------
// LeaguePreviewRow — P4-07 dashboard data flip.
// Minimal inline component (screen-local, not promoted to @learnexia/ui —
// full LeagueCard with motion is P4-08). Reuses existing token/layout primitives.
// ---------------------------------------------------------------------------
interface LeaguePreviewRowProps {
  tierName: string | null;
  rank: number | null;
  totalPlayers: number | null;
  xpThisWeek: number | null;
  direction?: 'ltr' | 'rtl';
  /** 3c (B-int): when set, the row is a tap target into the League tab. */
  onPress?: () => void;
}

function LeaguePreviewRow({
  tierName,
  rank,
  totalPlayers,
  xpThisWeek,
  direction = 'ltr',
  onPress,
}: LeaguePreviewRowProps) {
  const { t } = useTranslation();
  const isRtl = direction === 'rtl';
  const rowDir = isRtl ? 'row-reverse' : 'row';

  // Map BE tier name string → i18n key. BE sends LeagueTier.ToString() = "Bronze"|"Silver"|"Gold"|"Diamond".
  const tierLabel = (() => {
    const lower = (tierName ?? '').toLowerCase();
    if (lower === 'bronze')  return t('child.home.leagueTier.bronze');
    if (lower === 'silver')  return t('child.home.leagueTier.silver');
    if (lower === 'gold')    return t('child.home.leagueTier.gold');
    if (lower === 'diamond') return t('child.home.leagueTier.diamond');
    return tierName ?? '';
  })();

  const rankText =
    rank !== null && totalPlayers !== null && totalPlayers > 0
      ? t('child.home.leaguePreview.rankLabel', { rank, total: totalPlayers })
      : t('child.home.leaguePreview.rankUnknown');

  const a11yLabel =
    rank !== null && totalPlayers !== null
      ? t('child.home.leaguePreview.a11y', {
          tier: tierLabel,
          rank,
          total: totalPlayers,
          xp: xpThisWeek ?? 0,
        })
      : tierLabel;

  return (
    <TamStack
      flexDirection={rowDir as 'row' | 'row-reverse'}
      paddingHorizontal={16}
      paddingVertical={12}
      minHeight={48}
      borderRadius="$card"
      backgroundColor="$cardSoft"
      alignItems="center"
      justifyContent="space-between"
      onPress={onPress}
      cursor={onPress ? 'pointer' : undefined}
      pressStyle={onPress ? { scale: 0.98 } : undefined}
      accessibilityRole={onPress ? 'button' : 'text'}
      accessibilityHint={onPress ? t('child.home.leaguePreview.hintA11y') : undefined}
      accessible
      accessibilityLabel={a11yLabel}
      aria-label={a11yLabel}
    >
      {/* Tier label */}
      <Text
        color="$fg1"
        fontSize={15}
        fontWeight="700"
        fontFamily="$heading"
        writingDirection={direction}
      >
        {tierLabel}
      </Text>

      {/* Rank text */}
      <Text
        color="$fg3"
        fontSize={13}
        fontWeight="500"
        fontFamily="$body"
        writingDirection={direction}
      >
        {rankText}
      </Text>
    </TamStack>
  );
}

export default function ChildHomeScreen() {
  const { t, i18n } = useTranslation();
  const { isRtl, direction, locale } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useLocalizedRouter();
  const { signOut, isPending } = useSignOutAction();

  const rowDir = isRtl ? 'row-reverse' : 'row';

  // --- Data queries ---
  const meQuery = useMe();
  const dashboardQuery = useDashboard(); // AC1, AC9, AC10

  // ------------------------------------------------------------------
  // Celebrations (3c — B-int). useDashboardDiff compares consecutive
  // dashboard snapshots; the adapter maps the numeric MissionStatusDto to
  // the hook's string contract. Cold-start safe: the hook returns ZERO_DIFF
  // on the first successful load, so nothing fires on app open.
  // ------------------------------------------------------------------
  const diffSnapshot = useMemo(
    () => toDashboardLike(dashboardQuery.data),
    [dashboardQuery.data],
  );
  const rewardDiff = useDashboardDiff(diffSnapshot);

  // Latest dashboard data via ref — the enqueue effect must depend ONLY on
  // the diff identity (one enqueue per occurrence; depending on data too
  // would re-run the effect against a stale non-zero diff and double-fire).
  const latestDashboardRef = useRef<DashboardDto | undefined>(undefined);
  latestDashboardRef.current = dashboardQuery.data;

  const [celebrations, setCelebrations] = useState<readonly HomeCelebration[]>([]);

  useEffect(() => {
    const data = latestDashboardRef.current;
    if (!data) return;

    // One popup per diff occurrence — when several reward events land in the
    // same refresh, the highest-priority one is celebrated (kid UX: a single
    // celebration moment, never a popup chain). Priority follows the spec's
    // celebration order: level-up → badge → streak milestone → mission.
    const xpGained = Math.max(0, rewardDiff.xpDelta);
    let next: HomeCelebration | null = null;

    if (rewardDiff.levelDelta > 0) {
      next = {
        kind: CelebrationKind.LevelUp,
        xpGained,
        value: data.level ?? 0,
        code: '',
        badgeVariant: null,
        titleKey: null,
      };
    } else if (rewardDiff.newBadges[0] !== undefined) {
      const code = rewardDiff.newBadges[0];
      const rarity = data.recentBadges?.find((b) => b.code === code)?.rarity;
      next = {
        kind: CelebrationKind.Badge,
        xpGained,
        value: 0,
        code,
        badgeVariant: rarity !== undefined ? BADGE_RARITY_VARIANT[rarity] : 'gold',
        titleKey: null,
      };
    } else if (
      rewardDiff.streakDelta > 0 &&
      STREAK_MILESTONES.includes(data.streak ?? 0)
    ) {
      next = {
        kind: CelebrationKind.StreakMilestone,
        xpGained,
        value: data.streak ?? 0,
        code: '',
        badgeVariant: null,
        titleKey: null,
      };
    } else if (rewardDiff.completedMissions[0] !== undefined) {
      const code = rewardDiff.completedMissions[0];
      const mission =
        (data.dailyMissions ?? []).find((m) => m.code === code) ??
        (data.weeklyMission?.code === code ? data.weeklyMission : undefined);
      next = {
        kind: CelebrationKind.MissionCompleted,
        xpGained,
        value: 0,
        code,
        badgeVariant: null,
        titleKey: mission?.titleKey ?? null,
      };
    }

    if (next) {
      const queued = next;
      setCelebrations((queue) => [...queue, queued]);
    }
    // De-dupe: rewardDiff identity changes exactly once per dashboard-data
    // change (TanStack structural sharing keeps unchanged data referentially
    // stable), so each occurrence enqueues exactly once.
  }, [rewardDiff]);

  const activeCelebration = celebrations[0] ?? null;
  const dismissCelebration = () => setCelebrations((queue) => queue.slice(1));

  // First still-running timed event (entry point into /(child)/events).
  // Defensive endUtc filter so an already-ended event never renders stale.
  const activeEvent = useMemo<ActiveTimedEventDto | null>(() => {
    const events = dashboardQuery.data?.activeTimedEvents ?? [];
    const now = Date.now();
    return (
      events.find((e) => {
        if (!e.endUtc) return false;
        const end = new Date(e.endUtc).getTime();
        return Number.isFinite(end) && end > now;
      }) ?? null
    );
  }, [dashboardQuery.data?.activeTimedEvents]);

  // Derive child name (first token of full name — AC8)
  const childName = useMemo(
    () => (meQuery.data?.fullName ?? '').split(/\s+/)[0] || '',
    [meQuery.data?.fullName],
  );
  const grade = meQuery.data?.grade ?? null;
  const gradeKnown = grade !== null && grade !== undefined && grade >= 1 && grade <= 6;

  // Continue target from dashboard (AC2, AC3)
  const continueTarget = dashboardQuery.data?.continue ?? null;

  // --- Derived strings ---
  const greetingText = childName
    ? t('child.home.greeting', { childName })
    : t('child.home.welcomeBack');

  const gradeCaption = gradeKnown
    ? t('child.home.gradeCaption', { grade })
    : null;

  // Live stat values (P4-02/03/04 — real from BE; defaults match W13/P4-04)
  const heartsValue = dashboardQuery.data?.hearts ?? 5;
  const streakValue = dashboardQuery.data?.streak ?? 0;
  const xpValue = dashboardQuery.data?.xp ?? 0;
  const levelValue = dashboardQuery.data?.level ?? 1;
  const freezeBalance = dashboardQuery.data?.freezeBalance ?? 0;

  // Stats a11y label (AC11, design spec §7)
  // P4-04: hearts now real from dashboardQuery; inPracticeMode from dashboard.
  const statsA11y = t('child.home.statsA11y', {
    hearts: heartsValue, // P4-04 — real from BE (default 5 cap for new students)
    streak: streakValue, // P4-03
    xp: xpValue,         // P4-02
  });

  // 3c — per-chip tap-target labels (chips push their detail screens)
  const heartsNavA11y = t('child.home.statsNav.hearts', {
    value: formatCount(heartsValue, locale),
  });
  const streakNavA11y = t('child.home.statsNav.streak', {
    value: formatCount(streakValue, locale),
  });
  const xpNavA11y = t('child.home.statsNav.xp', { value: formatXp(xpValue) });
  const freezeNavA11y = t('child.home.statsNav.freeze', {
    value: formatCount(freezeBalance, locale),
  });

  // 3c — timed-event banner copy (name from DTO per locale, spec §8.2)
  const activeEventName = activeEvent
    ? locale === 'ar'
      ? (activeEvent.nameAr ?? activeEvent.nameEn ?? '')
      : (activeEvent.nameEn ?? activeEvent.nameAr ?? '')
    : '';
  const activeEventMultiplier = activeEvent
    ? t('events.timed.multiplier', { value: activeEvent.multiplier ?? 1 })
    : '';
  const eventBannerA11y = t('child.home.events.bannerA11y', {
    name: activeEventName,
    multiplier: activeEventMultiplier,
  });

  // 3c — celebration popup copy (resolved at render: locale-reactive).
  // Level-up reuses xp.levelUp.*; mission-complete reuses missions.complete.*.
  const celebrationCopy = useMemo(():
    | {
        variant: RewardVariant;
        title: string;
        subtitle: string;
        cta: string;
        a11y: string;
        badgeVariant: BadgeVariant;
      }
    | null => {
    if (!activeCelebration) return null;
    const xpText = formatXp(activeCelebration.xpGained);

    switch (activeCelebration.kind) {
      case CelebrationKind.LevelUp: {
        const levelText = formatCount(activeCelebration.value, locale);
        return {
          variant: 'level-up',
          title: t('xp.levelUp.title'),
          subtitle: t('xp.levelUp.subtitle', { level: levelText }),
          cta: t('xp.levelUp.cta'),
          a11y: t('xp.levelUp.a11y', { level: levelText, xp: xpText }),
          badgeVariant: 'gold',
        };
      }
      case CelebrationKind.Badge: {
        // Badge display name from the server code (badges.tsx precedent:
        // unknown codes fall back to the raw code — technical identifier).
        const name = t(`badges.names.${activeCelebration.code}`, {
          defaultValue: activeCelebration.code,
        });
        return {
          variant: 'badge-unlock',
          title: t('child.home.celebrate.badgeTitle'),
          subtitle: name,
          cta: t('child.home.celebrate.cta'),
          a11y: t('child.home.celebrate.badgeA11y', { name, xp: xpText }),
          badgeVariant: activeCelebration.badgeVariant ?? 'gold',
        };
      }
      case CelebrationKind.StreakMilestone: {
        const days = activeCelebration.value;
        const daysText = formatCount(days, locale);
        return {
          variant: 'xp',
          title: t('child.home.celebrate.streakTitle'),
          subtitle: t('child.home.celebrate.streakSubtitle', {
            count: days,
            value: daysText,
          }),
          cta: t('child.home.celebrate.cta'),
          a11y: t('child.home.celebrate.streakA11y', {
            count: days,
            value: daysText,
            xp: xpText,
          }),
          badgeVariant: 'gold',
        };
      }
      case CelebrationKind.MissionCompleted: {
        // Mission title from the server titleKey (missions.tsx resolver:
        // unknown keys fall back to a generic localized title, never the key).
        const key = activeCelebration.titleKey
          ? `${MISSION_TITLE_KEY_PREFIX}${activeCelebration.titleKey}`
          : MISSION_UNKNOWN_TITLE_KEY;
        const subtitle = i18n.exists(key) ? t(key) : t(MISSION_UNKNOWN_TITLE_KEY);
        return {
          variant: 'xp',
          title: t('missions.complete.title'),
          subtitle,
          cta: t('missions.complete.cta'),
          a11y: t('missions.complete.a11y', { xp: xpText }),
          badgeVariant: 'gold',
        };
      }
      default:
        return null;
    }
  }, [activeCelebration, locale, t, i18n]);

  // ContinueCard — derive state + press handler (AC2, AC3)
  const continueNodeState = (continueTarget?.nodeState ?? 1) as 1 | 2;
  const continueSubjectKey = continueTarget
    ? (resolveSubjectKey(continueTarget.subjectName) ?? 'math')
    : 'math';

  const isAvailableState = continueNodeState !== 2;
  const continueEyebrow = isAvailableState
    ? t('child.home.continue.eyebrow')
    : t('child.home.continue.eyebrowReplay');
  const continueCta = isAvailableState
    ? t('child.home.continue.cta')
    : t('child.home.continue.replayCta');

  const continueA11y = continueTarget
    ? t('child.home.continueA11y', { lesson: continueTarget.lessonName ?? '' })
    : '';
  const continueHintA11y = t('child.home.continueHintA11y');
  const bossLabel = t('child.home.boss');

  const handleContinuePress = () => {
    if (!continueTarget?.lessonId || !continueTarget?.subjectId) return;
    // AC3: navigate to W12 lesson player with subjectId back-stack seam
    router.push(
      `/(child)/lessons/${continueTarget.lessonId}?subjectId=${continueTarget.subjectId}` as `/${string}`,
    );
  };

  // Loading union (AC9) — shimmer while either me or dashboard is loading
  const isHeaderLoading = meQuery.isLoading || dashboardQuery.isLoading;

  return (
    <TamStack
      flex={1}
      backgroundColor="$bg"
      paddingTop={insets.top}
    >
      {/* ------------------------------------------------------------------ */}
      {/* TopBar — logo + sign-out (preserved from W11, AC12)                  */}
      {/* ------------------------------------------------------------------ */}
      <XStack
        flexDirection={rowDir}
        height={56}
        paddingHorizontal="$6"
        alignItems="center"
        justifyContent="space-between"
      >
        <Image
          source={assets.logoMark}
          style={{ width: 32, height: 32, resizeMode: 'contain' }}
          accessibilityElementsHidden
        />
        {/* Sign-out — ghost CTA, not a primary action (AC12) */}
        <TamStack
          testID="sign-out-button"
          minHeight={48}
          justifyContent="center"
          cursor="pointer"
          onPress={isPending ? undefined : signOut}
          accessibilityRole="button"
          accessible
          accessibilityLabel={t('child.subjects.signOut')}
          aria-label={t('child.subjects.signOut')}
        >
          <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
            {t('child.subjects.signOut')}
          </Text>
        </TamStack>
      </XStack>

      {/* ------------------------------------------------------------------ */}
      {/* Scrollable body                                                      */}
      {/* ------------------------------------------------------------------ */}
      <ScrollView
        contentContainerStyle={{
          paddingHorizontal: 24,
          // 3c — TabBar clearance: the floating ChildTabBar overlays content,
          // so scroll content reserves its height (B0-nav spec §3).
          paddingBottom: insets.bottom + CHILD_TAB_BAR_CLEARANCE,
          paddingTop: 16,
          // Web max-width 720 centered (design spec §1 — matches W12 lesson pattern)
          maxWidth: 720,
          width: '100%',
          alignSelf: 'center',
        }}
        showsVerticalScrollIndicator={false}
      >
        {/* AC1, AC9: dashboard header with loading state.
            Loading: the kit DashboardHeader skeleton (unchanged geometry).
            Live (3c — B-int): the same header anatomy recomposed locally from
            the kit primitives (Hearts / StreakFlame / XPBar) so each stat chip
            is a ≥48px tap target pushing its detail screen — the kit
            DashboardHeader is deliberately non-interactive ("no tap targets on
            this primitive") and packages/ui is outside this batch's file set.
            Promoting optional onPress props into the kit component is a noted
            carry-forward. */}
        {isHeaderLoading ? (
          <DashboardHeader
            childName={childName}
            greetingText={greetingText}
            gradeCaption={gradeCaption}
            hearts={heartsValue}
            heartsMax={5}
            streakDays={streakValue}
            weeklyXp={xpValue}
            weeklyXpTarget={100} // TODO P4-02 — weekly aggregation target
            weeklyLevel={levelValue}
            mascotSrc={assets.logoMark}
            statsAccessibilityLabel={statsA11y}
            heartsAccessibilityLabel={t('child.home.stats.hearts')}
            streakAccessibilityLabel={t('child.home.stats.streak')}
            xpAccessibilityLabel={t('child.home.stats.xp')}
            direction={direction}
            locale={locale}
            loading
            testID="dashboard-header"
          />
        ) : (
          <YStack gap={16} testID="dashboard-header">
            {/* Greeting row — mirrors kit DashboardHeader §3.1 (same tokens) */}
            <XStack flexDirection={rowDir} alignItems="center" gap={12}>
              {/* Mascot tile — decorative; hidden from screen readers */}
              <TamStack
                width={48}
                height={48}
                borderRadius="$button"
                backgroundColor="$primarySoft"
                alignItems="center"
                justifyContent="center"
                accessibilityElementsHidden
                importantForAccessibility="no-hide-descendants"
              >
                <Image
                  source={assets.logoMark}
                  style={{ width: 32, height: 32, resizeMode: 'contain' }}
                  accessibilityElementsHidden
                />
              </TamStack>

              {/* Greeting column */}
              <YStack flex={1} gap={2}>
                <Text
                  color="$fg1"
                  fontSize={24}
                  fontWeight="800"
                  fontFamily="$heading"
                  lineHeight={29}
                  letterSpacing={-0.24}
                  writingDirection={direction}
                  numberOfLines={1}
                  accessibilityRole="header"
                >
                  {greetingText}
                </Text>
                {gradeCaption ? (
                  <Text
                    color="$fg3"
                    fontSize={13}
                    fontWeight="500"
                    fontFamily="$body"
                    writingDirection={direction}
                    marginTop={2}
                  >
                    {gradeCaption}
                  </Text>
                ) : null}
              </YStack>
            </XStack>

            {/* Stats strip — each chip pushes its gamification screen (3c) */}
            <XStack flexDirection={rowDir} alignItems="center" gap={10}>
              {/* Hearts chip → /(child)/hearts (B3) */}
              <TamStack
                testID="stats-hearts"
                minHeight={48}
                justifyContent="center"
                cursor="pointer"
                pressStyle={{ scale: 0.97 }}
                onPress={() => router.push('/(child)/hearts')}
                accessibilityRole="button"
                accessible
                accessibilityLabel={heartsNavA11y}
                aria-label={heartsNavA11y}
              >
                <Hearts
                  current={heartsValue}
                  maxHearts={5}
                  locale={locale}
                  accessibilityLabel={t('child.home.stats.hearts')}
                />
              </TamStack>

              {/* Practice Mode pill — P4-04 (informational, not a tap target) */}
              {(dashboardQuery.data?.inPracticeMode ?? false) ? (
                <XStack
                  paddingHorizontal={8}
                  paddingVertical={4}
                  borderRadius={9999}
                  backgroundColor="$warningSoft"
                  alignItems="center"
                  accessibilityRole="text"
                  accessible
                  accessibilityLabel={t('child.home.practiceModeA11y')}
                  aria-label={t('child.home.practiceModeA11y')}
                >
                  <Text
                    color="$warning"
                    fontSize={11}
                    fontWeight="700"
                    fontFamily="$body"
                    writingDirection={direction}
                  >
                    {t('child.home.practiceMode')}
                  </Text>
                </XStack>
              ) : null}

              {/* Streak chip → /(child)/streak (B2) */}
              <TamStack
                testID="stats-streak"
                minHeight={48}
                justifyContent="center"
                cursor="pointer"
                pressStyle={{ scale: 0.97 }}
                onPress={() => router.push('/(child)/streak')}
                accessibilityRole="button"
                accessible
                accessibilityLabel={streakNavA11y}
                aria-label={streakNavA11y}
              >
                <StreakFlame
                  days={streakValue}
                  size="sm"
                  locale={locale}
                  accessibilityLabel={t('child.home.stats.streak')}
                />
              </TamStack>

              {/* Freeze chip → /(child)/events (B8 §8.1 — shown when earned) */}
              {freezeBalance > 0 ? (
                <TamStack
                  testID="stats-freeze"
                  minHeight={48}
                  justifyContent="center"
                  cursor="pointer"
                  pressStyle={{ scale: 0.97 }}
                  onPress={() => router.push('/(child)/events')}
                  accessibilityRole="button"
                  accessible
                  accessibilityLabel={freezeNavA11y}
                  aria-label={freezeNavA11y}
                >
                  <XStack
                    alignItems="center"
                    gap={4}
                    paddingHorizontal={8}
                    paddingVertical={4}
                    borderRadius="$pill"
                    backgroundColor={FREEZE_PILL_BG}
                  >
                    <Text fontSize={12} accessibilityElementsHidden>
                      {GLYPHS.freeze}
                    </Text>
                    <Text
                      color="$gem"
                      fontSize={13}
                      fontWeight="800"
                      style={{ fontVariant: ['tabular-nums'] }}
                      accessibilityElementsHidden
                    >
                      {formatCount(freezeBalance, locale)}
                    </Text>
                  </XStack>
                </TamStack>
              ) : null}

              {/* XP bar → /(child)/xp (B1) */}
              <TamStack
                testID="stats-xp"
                flex={1}
                minHeight={48}
                justifyContent="center"
                cursor="pointer"
                pressStyle={{ scale: 0.97 }}
                onPress={() => router.push('/(child)/xp')}
                accessibilityRole="button"
                accessible
                accessibilityLabel={xpNavA11y}
                aria-label={xpNavA11y}
              >
                <XPBar
                  currentXp={xpValue}
                  totalXp={100} // TODO P4-02 — weekly aggregation target
                  level={levelValue}
                  locale={locale}
                  animated={false}
                  accessibilityLabel={t('child.home.stats.xp')}
                />
              </TamStack>
            </XStack>
          </YStack>
        )}

        {/* ---------------------------------------------------------------- */}
        {/* Dashboard error strip (AC10)                                      */}
        {/* Renders between header and ContinueCard when dashboardQuery fails  */}
        {/* SubjectsListSection beneath still renders normally (AC10).         */}
        {/* ---------------------------------------------------------------- */}
        {dashboardQuery.isError ? (
          <XStack
            testID="dashboard-error"
            flexDirection={rowDir}
            marginTop={24}
            padding={12}
            paddingHorizontal={16}
            gap={12}
            borderRadius="$card"
            backgroundColor="$dangerSoft"
            borderStartWidth={3}
            borderStartColor="$danger"
            alignItems="center"
            accessibilityRole="alert"
            accessibilityLiveRegion="polite"
          >
            {/* Warning glyph disc */}
            <TamStack
              width={32}
              height={32}
              borderRadius={9999}
              backgroundColor="$dangerSoft"
              alignItems="center"
              justifyContent="center"
              flexShrink={0}
              accessibilityElementsHidden
              importantForAccessibility="no-hide-descendants"
            >
              <Text fontSize={18} accessibilityElementsHidden>{'⚠️'}</Text>
            </TamStack>

            {/* Error message */}
            <Text
              flex={1}
              color="$fg1"
              fontSize={14}
              fontWeight="700"
              fontFamily="$heading"
              writingDirection={direction}
            >
              {t('child.home.errorRetry')}
            </Text>

            {/* Retry button (AC10) */}
            <TamStack
              testID="dashboard-error-retry"
              minHeight={36}
              paddingHorizontal={12}
              paddingVertical={6}
              borderRadius="$button"
              cursor="pointer"
              onPress={() => dashboardQuery.refetch()}
              accessibilityRole="button"
              accessible
              accessibilityLabel={t('child.home.errorRetryCta')}
              aria-label={t('child.home.errorRetryCta')}
              justifyContent="center"
            >
              <Text color="$fg2" fontSize={13} fontWeight="500" fontFamily="$body" writingDirection={direction}>
                {t('child.home.errorRetryCta')}
              </Text>
            </TamStack>
          </XStack>
        ) : null}

        {/* ---------------------------------------------------------------- */}
        {/* ContinueCard (AC2, AC3, AC5, AC9)                                 */}
        {/* Shown only when dashboardQuery has data AND continue is non-null   */}
        {/* Loading: skeleton placeholder rendered by conditional below.        */}
        {/* ---------------------------------------------------------------- */}
        {isHeaderLoading ? (
          /* Loading placeholder for the ContinueCard area (AC9) */
          <TamStack
            marginTop={24}
            height={96}
            borderRadius="$modal"
            backgroundColor="$cardSoft"
            opacity={0.7}
          />
        ) : continueTarget ? (
          /* AC2: rendered only when continue is non-null */
          <TamStack marginTop={24}>
            <ContinueCard
              subjectName={continueTarget.subjectName ?? ''}
              subjectKey={continueSubjectKey}
              lessonName={continueTarget.lessonName ?? ''}
              unitName={continueTarget.unitName ?? undefined}
              skillName={continueTarget.skillName ?? undefined}
              isBoss={continueTarget.isBoss ?? false}
              nodeState={continueNodeState}
              onPress={handleContinuePress}
              eyebrowText={continueEyebrow}
              ctaLabel={continueCta}
              bossLabel={bossLabel}
              accessibilityLabel={continueA11y}
              accessibilityHint={continueHintA11y}
              direction={direction}
              locale={locale}
              testID="continue-card"
            />
          </TamStack>
        ) : null}

        {/* ---------------------------------------------------------------- */}
        {/* Timed-event banner (3c — B8 §8.2 entry point)                     */}
        {/* Compact gradReward banner for the first still-running event;       */}
        {/* taps push /(child)/events (countdown + full list live there).      */}
        {/* ---------------------------------------------------------------- */}
        {!isHeaderLoading && activeEvent ? (
          <TamStack
            testID="event-entry"
            marginTop={24}
            cursor="pointer"
            pressStyle={{ scale: 0.98 }}
            onPress={() => router.push('/(child)/events')}
            accessibilityRole="button"
            accessible
            accessibilityLabel={eventBannerA11y}
            aria-label={eventBannerA11y}
          >
            <GradientBox
              stops={gradientStops.gradReward.colors}
              angle={135}
              borderRadius="$modal"
              overflow="hidden"
              shadowColor={BANNER_SHADOW}
              shadowOffset={{ width: 0, height: 8 }}
              shadowRadius={24}
              shadowOpacity={1}
            >
              <XStack
                flexDirection={rowDir}
                alignItems="center"
                gap="$4"
                paddingVertical={14}
                paddingHorizontal={16}
              >
                {/* ⭐ disc — XP semantics (events are XP multipliers) */}
                <TamStack
                  width={44}
                  height={44}
                  borderRadius="$pill"
                  backgroundColor={BANNER_DISC_BG}
                  alignItems="center"
                  justifyContent="center"
                  flexShrink={0}
                  accessibilityElementsHidden
                >
                  <Text fontSize={22} accessibilityElementsHidden>
                    {GLYPHS.star}
                  </Text>
                </TamStack>

                <YStack
                  flex={1}
                  alignItems={isRtl ? 'flex-end' : 'flex-start'}
                  gap={2}
                >
                  <Text
                    color={BANNER_FG}
                    fontSize={15}
                    fontWeight="900"
                    fontFamily="$heading"
                    writingDirection={direction}
                    numberOfLines={1}
                    accessibilityElementsHidden
                  >
                    {activeEventName}
                  </Text>
                  {/* ×N XP — Latin digits, LTR-locked in both locales */}
                  <Text
                    color={BANNER_FG}
                    fontSize={18}
                    fontWeight="900"
                    fontFamily="$heading"
                    writingDirection="ltr"
                    style={{ fontVariant: ['tabular-nums'] }}
                    accessibilityElementsHidden
                  >
                    {activeEventMultiplier}
                  </Text>
                </YStack>

                <Text
                  color={BANNER_FG_SOFT}
                  fontSize={13}
                  fontWeight="700"
                  fontFamily="$body"
                  writingDirection={direction}
                  accessibilityElementsHidden
                >
                  {t('child.home.events.seeAll')}
                </Text>
              </XStack>
            </GradientBox>
          </TamStack>
        ) : null}

        {/* ---------------------------------------------------------------- */}
        {/* MissionBanner — NEVER rendered in Phase 2 (AC6)                   */}
        {/* dashboardQuery.data?.dailyMission is always null in Phase 2.       */}
        {/* P4-06 deletes this comment and passes real DailyMissionDto data.   */}
        {/* TODO P4-06 — wire dailyMission when Phase 4 ships the mission engine. */}
        {/* (3c note: the dashboard daily-mission widget + recent-badges row    */}
        {/* remain a flagged carry-forward — this batch wires entry points,     */}
        {/* celebrations and clearance only, per the B-int task scope.)         */}
        {/* ---------------------------------------------------------------- */}

        {/* ---------------------------------------------------------------- */}
        {/* LeaguePreview — P4-07: shows real tier + rank from dashboard.     */}
        {/* Hidden when leaguePreview is null (brand-new student / BE < P4-07).*/}
        {/* Full league screen + animations are P4-08.                         */}
        {/* ---------------------------------------------------------------- */}
        {!isHeaderLoading && dashboardQuery.data?.leaguePreview ? (
          <TamStack testID="league-preview" marginTop={24}>
            <LeaguePreviewRow
              tierName={dashboardQuery.data.leaguePreview.tierName ?? null}
              rank={dashboardQuery.data.leaguePreview.rank ?? null}
              totalPlayers={dashboardQuery.data.leaguePreview.totalPlayers ?? null}
              xpThisWeek={dashboardQuery.data.leaguePreview.xpThisWeek ?? null}
              direction={direction}
              // 3c — entry point into the League tab (spec §6)
              onPress={() => router.push('/(child)/league')}
            />
          </TamStack>
        ) : null}

        {/* ---------------------------------------------------------------- */}
        {/* SubjectsListSection (AC4, AC13)                                   */}
        {/* W11 grade/empty/error/loading paths preserved.                     */}
        {/* Defensive 4-subject filter still wins — Social Studies never shows. */}
        {/* ---------------------------------------------------------------- */}
        {/* SubjectsListSection — AC4, AC13 (W11 behaviour preserved)
            Always rendered. SubjectsListSection owns its own loading/error/empty
            states. Social Studies defensive filter lives in subjects.ts → still wins. */}
        <TamStack marginTop={24}>
          <SubjectsListSection
            grade={grade}
            direction={direction}
            loading={meQuery.isLoading || dashboardQuery.isLoading}
            testID="subjects-list-section"
          />
        </TamStack>

        {/* ---------------------------------------------------------------- */}
        {/* "My activity" entry (3c — A5 link-row deferred by batch 2d)       */}
        {/* Pushes /(child)/attempts (the child's own attempt history).        */}
        {/* ---------------------------------------------------------------- */}
        <TamStack
          testID="activity-entry"
          flexDirection={rowDir as 'row' | 'row-reverse'}
          marginTop={24}
          minHeight={48}
          paddingHorizontal={16}
          paddingVertical={12}
          borderRadius="$card"
          backgroundColor="$cardSoft"
          alignItems="center"
          justifyContent="space-between"
          cursor="pointer"
          pressStyle={{ scale: 0.98 }}
          onPress={() => router.push('/(child)/attempts')}
          accessibilityRole="button"
          accessible
          accessibilityLabel={t('child.home.activity.a11y')}
          aria-label={t('child.home.activity.a11y')}
        >
          <Text
            color="$fg1"
            fontSize={15}
            fontWeight="700"
            fontFamily="$heading"
            writingDirection={direction}
          >
            {t('child.attempts.title')}
          </Text>
          <Text
            color="$primaryLight"
            fontSize={13}
            fontWeight="700"
            fontFamily="$body"
            writingDirection={direction}
          >
            {t('child.home.activity.seeAll')}
          </Text>
        </TamStack>
      </ScrollView>

      {/* ------------------------------------------------------------------ */}
      {/* Dashboard celebration popup (3c — useDashboardDiff plumbing)        */}
      {/* Level-up / badge-earned / streak-milestone / mission-completed.     */}
      {/* RewardPopup's internal ConfettiLayer self-gates under reduced       */}
      {/* motion; cold start fires nothing (ZERO_DIFF invariant).             */}
      {/* ------------------------------------------------------------------ */}
      {activeCelebration && celebrationCopy ? (
        <RewardPopup
          variant={celebrationCopy.variant}
          xpAmount={activeCelebration.xpGained}
          title={celebrationCopy.title}
          subtitle={celebrationCopy.subtitle}
          badgeVariant={celebrationCopy.badgeVariant}
          visible
          ctaLabel={celebrationCopy.cta}
          onDismiss={dismissCelebration}
          locale={locale}
          accessibilityLabel={celebrationCopy.a11y}
        />
      ) : null}
    </TamStack>
  );
}
