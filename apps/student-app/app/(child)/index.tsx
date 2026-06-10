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
 * Remaining stubs (carry inline TODO comments pointing to Phase-4 story):
 *   WeeklyXpTarget: 100 (TODO P4-02 — weekly aggregation target)
 *   DailyMission: always null → MissionBanner never mounts (TODO P4-06)
 *
 * Acceptance criteria reference: AC1–AC13 in docs/briefs/W13-P2-09-FE.md.
 * Design Spec: design-system/ui_kits/student-mobile/W13-home-dashboard.md.
 */

import React, { useMemo } from 'react';
import { ScrollView, Image } from 'react-native';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useMe, useDashboard } from '@learnexia/api-client';
import { DashboardHeader, ContinueCard } from '@learnexia/ui';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';
import { useRouter } from 'expo-router';

import { assets } from '../../src/assets';
import { useLocale } from '../../src/hooks/useLocale';
import { useSignOutAction } from '../../src/hooks/useSignOutAction';
import { resolveSubjectKey } from './_components/subjects';
import { SubjectsListSection } from './_components/SubjectsListSection';

const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

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
}

function LeaguePreviewRow({
  tierName,
  rank,
  totalPlayers,
  xpThisWeek,
  direction = 'ltr',
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
      borderRadius="$card"
      backgroundColor="$cardSoft"
      alignItems="center"
      justifyContent="space-between"
      accessibilityRole="text"
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
  const { t } = useTranslation();
  const { isRtl, direction, locale } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { signOut, isPending } = useSignOutAction();

  const rowDir = isRtl ? 'row-reverse' : 'row';

  // --- Data queries ---
  const meQuery = useMe();
  const dashboardQuery = useDashboard(); // AC1, AC9, AC10

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

  // Stats a11y label (AC11, design spec §7)
  // P4-04: hearts now real from dashboardQuery; inPracticeMode from dashboard.
  const statsA11y = t('child.home.statsA11y', {
    hearts: dashboardQuery.data?.hearts ?? 5, // P4-04 — real from BE (default 5 cap for new students)
    streak: dashboardQuery.data?.streak ?? 0, // P4-03
    xp: dashboardQuery.data?.xp ?? 0,         // P4-02
  });

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
          paddingBottom: insets.bottom + 24,
          paddingTop: 16,
          // Web max-width 720 centered (design spec §1 — matches W12 lesson pattern)
          maxWidth: 720,
          width: '100%',
          alignSelf: 'center',
        }}
        showsVerticalScrollIndicator={false}
      >
        {/* AC1, AC9: DashboardHeader with loading state */}
        <DashboardHeader
          childName={childName}
          greetingText={greetingText}
          gradeCaption={gradeCaption}
          hearts={dashboardQuery.data?.hearts ?? 5}         // P4-04 — real from BE (default 5 for new students)
          heartsMax={5}
          streakDays={dashboardQuery.data?.streak ?? 0}    // P4-03 — real from BE
          weeklyXp={dashboardQuery.data?.xp ?? 0}          // P4-02 — real from BE
          weeklyXpTarget={100}                              // TODO P4-02 — weekly aggregation target
          weeklyLevel={dashboardQuery.data?.level ?? 1}    // P4-02 — real level from BE
          mascotSrc={assets.logoMark}
          statsAccessibilityLabel={statsA11y}
          heartsAccessibilityLabel={t('child.home.stats.hearts')}
          streakAccessibilityLabel={t('child.home.stats.streak')}
          xpAccessibilityLabel={t('child.home.stats.xp')}
          inPracticeMode={dashboardQuery.data?.inPracticeMode ?? false}  // P4-04
          practiceModeLabel={t('child.home.practiceMode')}               // P4-04
          practiceModeAccessibilityLabel={t('child.home.practiceModeA11y')} // P4-04
          direction={direction}
          locale={locale}
          loading={isHeaderLoading}
          testID="dashboard-header"
        />

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
        {/* MissionBanner — NEVER rendered in Phase 2 (AC6)                   */}
        {/* dashboardQuery.data?.dailyMission is always null in Phase 2.       */}
        {/* P4-06 deletes this comment and passes real DailyMissionDto data.   */}
        {/* TODO P4-06 — wire dailyMission when Phase 4 ships the mission engine. */}
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
      </ScrollView>
    </TamStack>
  );
}
