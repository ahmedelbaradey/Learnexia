/**
 * League standings screen — child TAB root (P4-07-FE, carryover batch 3b
 * lane B6, design spec `design-system/ui_kits/student-app/P4-08.md` §6).
 *
 * Route: (child)/league — one of the 4 TabBar tabs (B0-nav); the route was
 * stubbed by batch 2b and this file replaces the stub body only (route name
 * `league` is final, `(child)/_layout.tsx` untouched).
 *
 * Data: `useMyLeague()` ONLY (`GET /api/Gamification/Leagues/Me` — plural).
 * `MyLeagueResponse { tier, periodEndUtc, myRank, myWeeklyXp, standings[],
 * promotionCutoffRank, demotionCutoffRank }`. `standings[].displayName` is
 * server-anonymized ("Student #N") — never render real names.
 *
 * Composition (spec §6): tier banner (gradient by tier, 🏆 disc, countdown +
 * promote-count sub) → week-countdown chip → standings rows (rank, avatar
 * disc, name, weekly XP) with the you-row highlighted (`isMe`/`myRank`) and
 * promotion/demotion cutlines after/before `promotionCutoffRank` /
 * `demotionCutoffRank`. Auto-scrolls so the you-row is visible on mount.
 *
 * RTL: layout is `dir`-driven; XP counters keep Latin digits + LTR in AR
 * (SKILL.md rule — intended deviation from `ar-league-rows.html`, spec G-8);
 * rank / day / hour counts are prose numerals (Eastern-Arabic in AR); the
 * zone arrows ↑/↓ are vertical semantics and never mirror.
 *
 * Countdown ticks every minute (information, not decoration — not gated by
 * reduce-motion, spec §8.2 precedent). No primary action on this screen
 * (informational — spec §9).
 */
import { LeagueTierDto, useMyLeague, type LeagueStandingDto } from '@learnexia/api-client';
import { nativeShadow } from '@learnexia/design-system';
import { Button, GradientBox } from '@learnexia/ui';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ScrollView, type LayoutChangeEvent } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '../../src/hooks/useLocale';
import { CHILD_TAB_BAR_CLEARANCE } from './_components/ChildTabBar';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

/* ------------------------------------------------------------------ */
/* Constants (spec literals + technical identifiers)                   */
/* ------------------------------------------------------------------ */

/** Decorative glyphs (semantic emoji set — not user-facing copy). */
const GLYPHS = {
  trophy: '🏆',
  warning: '⚠️',
} as const;

/**
 * Tier banner gradients — spec §6.1 literals (Bronze/Silver/Gold from
 * `mobile-league-header.html`; Diamond derived `$gem`→`$primary`, gap G-7).
 * Not in `gradientStops` (screen-specific capture literals).
 */
const TIER_BANNER_GRADIENTS: Record<LeagueTierDto, readonly [string, string]> = {
  [LeagueTierDto._1]: ['#FBBF24', '#B45309'], // Bronze
  [LeagueTierDto._2]: ['#F1F5F9', '#94A3B8'], // Silver
  [LeagueTierDto._3]: ['#FDE68A', '#F59E0B'], // Gold
  [LeagueTierDto._4]: ['#38BDF8', '#4F46E5'], // Diamond (G-7 derived)
};

/** Tier display-name i18n keys — reuses the dashboard map (spec §6.1). */
const TIER_I18N_KEYS: Record<LeagueTierDto, string> = {
  [LeagueTierDto._1]: 'child.home.leagueTier.bronze',
  [LeagueTierDto._2]: 'child.home.leagueTier.silver',
  [LeagueTierDto._3]: 'child.home.leagueTier.gold',
  [LeagueTierDto._4]: 'child.home.leagueTier.diamond',
};

/** Avatar disc tint rotation by rank — spec §6.2 literal token rotation. */
const AVATAR_TINTS = ['$streak', '$purple', '$danger', '$gem'] as const;

/** Banner-on-gradient text colors — spec §6.1 literals (white / white .9). */
const BANNER_FG = '#FFFFFF';
const BANNER_FG_SOFT = 'rgba(255,255,255,0.9)';
/** 🏆 disc fill on the banner — spec §6.1 literal. */
const BANNER_DISC_BG = 'rgba(255,255,255,0.2)';
/** Standings row border — spec §6.2 literal. */
const ROW_BORDER = 'rgba(255,255,255,0.04)';
/** Cutline line colors — spec §6.4 literals. */
const PROMOTION_LINE = 'rgba(34,197,94,0.3)';
const DEMOTION_LINE = 'rgba(239,68,68,0.3)';
/** You-row highlight border/bg per spec §6.3 ($primary / $primarySoft). */
const YOU_ROW_BORDER_WIDTH = 2;

/** Sub-line separator — punctuation, not copy (spec banner literal "·"). */
const DOT_SEPARATOR = ' · ';

/** Countdown tick — every minute, not every second (battery, spec §8.2). */
const COUNTDOWN_TICK_MS = 60_000;
const MS_PER_HOUR = 3_600_000;
const MS_PER_DAY = 86_400_000;

/** Keep ~2 rows of context above the you-row when auto-scrolling. */
const AUTO_SCROLL_VIEWPORT_OFFSET = 240;

/* ------------------------------------------------------------------ */
/* Locale-aware formatting (local pure helpers — xp.tsx pattern)       */
/* ------------------------------------------------------------------ */

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

/** Prose count (ranks, days, hours): Eastern-Arabic digits in AR. */
function formatCount(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

/** XP counter: ALWAYS Latin digits (SKILL.md rule — even in AR; spec G-8). */
function formatXp(value: number): string {
  return new Intl.NumberFormat('en-US').format(value);
}

/* ------------------------------------------------------------------ */
/* Screen                                                              */
/* ------------------------------------------------------------------ */

export default function LeagueScreen() {
  const { t } = useTranslation();
  const { locale, direction, isRtl } = useLocale();
  const insets = useSafeAreaInsets();

  const leagueQuery = useMyLeague();

  // --- Week countdown (minute tick — information, not decoration) --------
  const [nowMs, setNowMs] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNowMs(Date.now()), COUNTDOWN_TICK_MS);
    return () => clearInterval(id);
  }, []);

  // --- Auto-scroll so the you-row is visible on mount (spec §6.3) --------
  const scrollRef = useRef<ScrollView>(null);
  const didAutoScrollRef = useRef(false);
  const [meRowY, setMeRowY] = useState<number | null>(null);
  const [listY, setListY] = useState<number | null>(null);
  useEffect(() => {
    if (didAutoScrollRef.current || meRowY === null || listY === null) return;
    didAutoScrollRef.current = true;
    scrollRef.current?.scrollTo({
      y: Math.max(0, listY + meRowY - AUTO_SCROLL_VIEWPORT_OFFSET),
      animated: false,
    });
  }, [meRowY, listY]);

  const rowDir = isRtl ? 'row-reverse' : 'row';

  const data = leagueQuery.data;
  const tier = data?.tier ?? LeagueTierDto._1;
  const myRank = data?.myRank ?? 0;
  const myWeeklyXp = data?.myWeeklyXp ?? 0;
  const promotionCutoff = data?.promotionCutoffRank ?? 0;
  const demotionCutoff = data?.demotionCutoffRank ?? 0;
  const standings: LeagueStandingDto[] = [...(data?.standings ?? [])].sort(
    (a, b) => (a.rank ?? 0) - (b.rank ?? 0),
  );
  const maxRank = standings.length > 0 ? (standings[standings.length - 1]?.rank ?? 0) : 0;

  const tierLabel = t(TIER_I18N_KEYS[tier]);

  // Time left until the weekly reset (clamped at 0 — never negative UI).
  const endMs = data?.periodEndUtc ? new Date(data.periodEndUtc).getTime() : null;
  const msLeft = endMs !== null ? Math.max(0, endMs - nowMs) : null;
  const daysLeft = msLeft !== null ? Math.floor(msLeft / MS_PER_DAY) : 0;
  const hoursLeft = msLeft !== null ? Math.floor((msLeft % MS_PER_DAY) / MS_PER_HOUR) : 0;

  // Banner sub: "{time left} · Top {n} promote" (spec §6.1).
  const timeLeftText =
    msLeft === null || msLeft <= 0
      ? t('league.banner.endingSoon')
      : daysLeft >= 1
        ? t('league.banner.daysLeft', {
            count: daysLeft,
            value: formatCount(daysLeft, locale),
          })
        : hoursLeft >= 1
          ? t('league.banner.hoursLeft', {
              count: hoursLeft,
              value: formatCount(hoursLeft, locale),
            })
          : t('league.banner.endingSoon');
  const promoteText =
    promotionCutoff > 0
      ? t('league.banner.promote', {
          count: promotionCutoff,
          value: formatCount(promotionCutoff, locale),
        })
      : null;
  const bannerSub = promoteText ? `${timeLeftText}${DOT_SEPARATOR}${promoteText}` : timeLeftText;

  // Countdown chip: "Resets in 3d 4h" / "تتجدد بعد ٣ أيام و٤ ساعات" (spec §6.5).
  const countdownText =
    msLeft === null || msLeft <= 0
      ? t('league.countdown.soon')
      : daysLeft >= 1
        ? t('league.countdown.full', {
            days: t('league.countdown.days', {
              count: daysLeft,
              value: formatCount(daysLeft, locale),
            }),
            hours: t('league.countdown.hours', {
              count: hoursLeft,
              value: formatCount(hoursLeft, locale),
            }),
          })
        : hoursLeft >= 1
          ? t('league.countdown.hoursOnly', {
              hours: t('league.countdown.hours', {
                count: hoursLeft,
                value: formatCount(hoursLeft, locale),
              }),
            })
          : t('league.countdown.soon');

  const isLoading = leagueQuery.isLoading;
  const isError = leagueQuery.isError;
  const isEmpty = !isLoading && !isError && standings.length === 0;
  const isAlone = standings.length === 1;

  /* ---------------------------------------------------------------- */
  /* Sub-renderers (screen-local — LeaguePreviewRow precedent)          */
  /* ---------------------------------------------------------------- */

  const renderZoneDivider = (kind: 'promotion' | 'demotion') => (
    <XStack
      key={`zone-${kind}`}
      flexDirection={rowDir}
      alignItems="center"
      gap="$2"
      paddingVertical={2}
      testID={`league-zone-${kind}`}
      accessible
      accessibilityRole="text"
      accessibilityLabel={t(kind === 'promotion' ? 'league.zones.promotion' : 'league.zones.demotion')}
      aria-label={t(kind === 'promotion' ? 'league.zones.promotion' : 'league.zones.demotion')}
    >
      <TamStack
        flex={1}
        height={1}
        backgroundColor={kind === 'promotion' ? PROMOTION_LINE : DEMOTION_LINE}
      />
      {/* ↑/↓ arrows are vertical semantics — never RTL-mirrored (spec §6.4). */}
      <Text
        color={kind === 'promotion' ? '$success' : '$danger'}
        fontSize={11}
        fontWeight="700"
        fontFamily="$heading"
        textTransform="uppercase"
        letterSpacing={0.88}
        accessibilityElementsHidden
      >
        {t(kind === 'promotion' ? 'league.zones.promotion' : 'league.zones.demotion')}
      </Text>
      <TamStack
        flex={1}
        height={1}
        backgroundColor={kind === 'promotion' ? PROMOTION_LINE : DEMOTION_LINE}
      />
    </XStack>
  );

  const renderRow = (row: LeagueStandingDto) => {
    const rank = row.rank ?? 0;
    const isMe = row.isMe === true || (myRank > 0 && rank === myRank);
    const weeklyXp = isMe ? (row.weeklyXp ?? myWeeklyXp) : (row.weeklyXp ?? 0);
    const name = row.displayName ?? '';
    const avatarTint = AVATAR_TINTS[rank % AVATAR_TINTS.length] ?? AVATAR_TINTS[0];
    const xpText = t('league.list.xp', { xp: formatXp(weeklyXp) });
    const a11yLabel = isMe
      ? t('league.list.youRowA11y', {
          rank: formatCount(rank, locale),
          xp: formatXp(weeklyXp),
        })
      : t('league.list.rowA11y', {
          rank: formatCount(rank, locale),
          name,
          xp: formatXp(weeklyXp),
        });

    return (
      <XStack
        key={`rank-${rank}`}
        flexDirection={rowDir}
        alignItems="center"
        gap="$3"
        backgroundColor={isMe ? '$primarySoft' : '$card'}
        borderRadius="$button"
        borderWidth={isMe ? YOU_ROW_BORDER_WIDTH : 1}
        borderColor={isMe ? '$primary' : ROW_BORDER}
        paddingVertical={12}
        paddingHorizontal={14}
        shadowColor={isMe ? nativeShadow.primaryGlow.color : undefined}
        shadowOffset={isMe ? { width: 0, height: nativeShadow.primaryGlow.offsetY } : undefined}
        shadowOpacity={isMe ? 1 : undefined}
        shadowRadius={isMe ? nativeShadow.primaryGlow.radius : undefined}
        testID={isMe ? 'league-you-row' : `league-row-${rank}`}
        accessible
        accessibilityRole="text"
        accessibilityLabel={a11yLabel}
        aria-label={a11yLabel}
        onLayout={isMe ? (e: LayoutChangeEvent) => setMeRowY(e.nativeEvent.layout.y) : undefined}
      >
        {/* Rank — 28px col, top-3 in $xp (spec §6.2). Prose numerals. */}
        <Text
          width={28}
          color={rank <= 3 ? '$xp' : '$fg3'}
          fontSize={14}
          fontWeight="800"
          fontFamily="$heading"
          textAlign="center"
          style={{ fontVariant: ['tabular-nums'] }}
          accessibilityElementsHidden
        >
          {formatCount(rank, locale)}
        </Text>

        {/* Avatar disc — 38px, tint rotation by rank, initial (spec §6.2).
            Never mirrored (emoji/avatar rule, spec §9). */}
        <TamStack
          width={38}
          height={38}
          borderRadius={9999}
          backgroundColor={avatarTint}
          alignItems="center"
          justifyContent="center"
          accessibilityElementsHidden
          importantForAccessibility="no-hide-descendants"
        >
          <Text color={BANNER_FG} fontSize={14} fontWeight="800" fontFamily="$heading">
            {name.charAt(0).toUpperCase()}
          </Text>
        </TamStack>

        {/* Anonymized display name (+ YOU chip on the me-row). */}
        <XStack flex={1} flexDirection={rowDir} alignItems="center" gap="$1">
          <Text
            color="$fg2"
            fontSize={14}
            fontWeight="700"
            fontFamily="$body"
            writingDirection={direction}
            numberOfLines={1}
            accessibilityElementsHidden
          >
            {name}
          </Text>
          {isMe ? (
            <Text
              color="$primaryLight"
              fontSize={11}
              fontWeight="600"
              fontFamily="$heading"
              textTransform="uppercase"
              marginStart={4}
              accessibilityElementsHidden
            >
              {t('league.list.youChip')}
            </Text>
          ) : null}
        </XStack>

        {/* Weekly XP — Latin digits + LTR in BOTH locales (SKILL.md, G-8). */}
        <Text
          color="$xp"
          fontSize={14}
          fontWeight="800"
          fontFamily="$heading"
          writingDirection="ltr"
          style={{ fontVariant: ['tabular-nums'] }}
          accessibilityElementsHidden
        >
          {xpText}
        </Text>
      </XStack>
    );
  };

  /* ---------------------------------------------------------------- */
  /* Render                                                            */
  /* ---------------------------------------------------------------- */

  return (
    <TamStack flex={1} backgroundColor="$bg" testID="league-screen">
      {/* Top safe area */}
      <TamStack height={insets.top} />

      <ScrollView
        ref={scrollRef}
        contentContainerStyle={{
          paddingHorizontal: 24,
          paddingTop: 16,
          paddingBottom: CHILD_TAB_BAR_CLEARANCE + insets.bottom,
        }}
        showsVerticalScrollIndicator={false}
      >
        {/* ≥768 centers at maxWidth 720 (W13 precedent, spec §0.1 layout). */}
        <YStack width="100%" maxWidth={720} alignSelf="center" gap="$4">
          {isLoading ? (
            /* Loading — banner + 6 row skeletons at final geometry (spec §6). */
            <YStack testID="league-loading" gap="$4">
              <TamStack
                width="100%"
                height={112}
                borderRadius="$modal"
                backgroundColor="$cardSoft"
                opacity={0.6}
              />
              <TamStack
                width={140}
                height={16}
                borderRadius={9999}
                backgroundColor="$cardSoft"
                opacity={0.6}
                alignSelf="center"
              />
              <YStack gap="$2">
                {Array.from({ length: 6 }, (_, i) => (
                  <TamStack
                    key={`league-skeleton-${i}`}
                    width="100%"
                    height={62}
                    borderRadius="$button"
                    backgroundColor="$cardSoft"
                    opacity={0.6}
                  />
                ))}
              </YStack>
            </YStack>
          ) : isError ? (
            /* Error strip — W13 dashboard error pattern (strip + retry). */
            <YStack
              testID="league-error"
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
                {`${GLYPHS.warning} ${t('league.error')}`}
              </Text>
              <Button
                variant="ghost"
                size="sm"
                accessibilityLabel={t('common.retry')}
                onPress={() => void leagueQuery.refetch()}
                testID="league-retry"
              >
                {t('common.retry')}
              </Button>
            </YStack>
          ) : isEmpty ? (
            /* Empty / unplaced — no league this week yet (spec §6 states). */
            <YStack
              testID="league-empty"
              alignItems="center"
              gap="$4"
              paddingVertical={48}
              accessible
              accessibilityRole="text"
              accessibilityLabel={t('league.empty.title')}
              aria-label={t('league.empty.title')}
            >
              {/* 64px trophy, dimmed (grayscale approximation — RN text). */}
              <Text fontSize={64} lineHeight={72} opacity={0.4} accessibilityElementsHidden>
                {GLYPHS.trophy}
              </Text>
              <Text
                color="$fg2"
                fontSize={16}
                fontWeight="700"
                fontFamily="$heading"
                textAlign="center"
                writingDirection={direction}
                accessibilityElementsHidden
              >
                {t('league.empty.title')}
              </Text>
            </YStack>
          ) : (
            <>
              {/* ------------------------------------------------------ */}
              {/* 1. Tier banner (mobile-league-header.html, spec §6.1)    */}
              {/* ------------------------------------------------------ */}
              <GradientBox
                stops={TIER_BANNER_GRADIENTS[tier]}
                angle={135}
                borderRadius="$modal"
                overflow="hidden"
                testID="league-banner"
                accessible
                accessibilityRole="text"
                accessibilityLabel={t('league.banner.a11y', {
                  tier: tierLabel,
                  timeLeft: timeLeftText,
                  promote: promoteText ?? '',
                })}
                aria-label={t('league.banner.a11y', {
                  tier: tierLabel,
                  timeLeft: timeLeftText,
                  promote: promoteText ?? '',
                })}
              >
                <XStack
                  flexDirection={rowDir}
                  alignItems="center"
                  gap="$4"
                  paddingVertical={20}
                  paddingHorizontal={18}
                >
                  {/* 🏆 disc 56×56 — never mirrored (spec §9). */}
                  <TamStack
                    width={56}
                    height={56}
                    borderRadius={9999}
                    backgroundColor={BANNER_DISC_BG}
                    alignItems="center"
                    justifyContent="center"
                    accessibilityElementsHidden
                    importantForAccessibility="no-hide-descendants"
                  >
                    <Text fontSize={28} lineHeight={34}>
                      {GLYPHS.trophy}
                    </Text>
                  </TamStack>
                  <YStack flex={1} gap="$1" accessibilityElementsHidden>
                    <Text
                      color={BANNER_FG}
                      fontSize={20}
                      fontWeight="900"
                      fontFamily="$heading"
                      writingDirection={direction}
                    >
                      {t('league.banner.title', { tier: tierLabel })}
                    </Text>
                    <Text
                      color={BANNER_FG_SOFT}
                      fontSize={12}
                      fontWeight="500"
                      fontFamily="$body"
                      writingDirection={direction}
                    >
                      {bannerSub}
                    </Text>
                  </YStack>
                </XStack>
              </GradientBox>

              {/* ------------------------------------------------------ */}
              {/* 2. Week countdown chip — text only (spec §6.5)           */}
              {/* ------------------------------------------------------ */}
              <Text
                testID="league-countdown"
                color="$fg3"
                fontSize={12}
                fontWeight="700"
                fontFamily="$body"
                textAlign="center"
                writingDirection={direction}
                accessibilityRole="text"
              >
                {countdownText}
              </Text>

              {/* ------------------------------------------------------ */}
              {/* 3. Standings + promotion/demotion cutlines (spec §6.2-4) */}
              {/* ------------------------------------------------------ */}
              <YStack
                gap="$2"
                testID="league-standings"
                onLayout={(e: LayoutChangeEvent) => setListY(e.nativeEvent.layout.y)}
              >
                {standings.map((row) => {
                  const rank = row.rank ?? 0;
                  return (
                    <React.Fragment key={`standing-${rank}`}>
                      {/* Demotion cutline BEFORE the first demoted rank. */}
                      {demotionCutoff > 0 && rank === demotionCutoff && rank > 1
                        ? renderZoneDivider('demotion')
                        : null}
                      {renderRow(row)}
                      {/* Promotion cutline AFTER the last promoted rank. */}
                      {promotionCutoff > 0 && rank === promotionCutoff && rank < maxRank
                        ? renderZoneDivider('promotion')
                        : null}
                    </React.Fragment>
                  );
                })}
              </YStack>

              {/* Single-member week — honest hint, no fabricated rivals. */}
              {isAlone ? (
                <Text
                  testID="league-alone-hint"
                  color="$fg3"
                  fontSize={13}
                  fontWeight="500"
                  fontFamily="$body"
                  textAlign="center"
                  writingDirection={direction}
                >
                  {t('league.list.aloneHint')}
                </Text>
              ) : null}
            </>
          )}
        </YStack>
      </ScrollView>
    </TamStack>
  );
}
