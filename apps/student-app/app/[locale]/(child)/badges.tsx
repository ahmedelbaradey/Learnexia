/**
 * Badges collection — child TAB screen (P4-05-FE, carryover batch 3b lane B4,
 * design spec `design-system/ui_kits/student-app/P4-08.md` §4; pixel target
 * `screenshots/mobile/17-badges.png` + `preview/mobile-badge-tiles.html` +
 * `preview/mobile-badge-stats-strip.html`).
 *
 * Route: (child)/badges — one of the 4 TabBar roots (B0-nav). The route is
 * already registered; this file only replaces the 2b stub body. Scroll content
 * bakes in the TabBar clearance (`CHILD_TAB_BAR_CLEARANCE + insets.bottom`).
 *
 * Data: `useMyBadges()` ONLY → `MyBadgesResponse.badges: BadgeStateDto[]`
 * (full catalog, earned + locked). Display names + how-to-earn hints are i18n
 * keys per server `code` (`badges.names.{code}` / `badges.hints.{code}` —
 * server sends codes, never copy); unknown codes fall back to the raw code
 * (technical identifier) with no hint line. `awardedAtUtc` is typed `Date` by
 * NSwag but arrives as a raw ISO string (no jsonParseReviver) — normalized
 * defensively, mirroring `(child)/attempts.tsx`.
 *
 * Sort (spec §4.4): earned first (newest `awardedAtUtc` first), then locked
 * in server (catalog) order — `Array.sort` stability preserves it.
 *
 * Celebration: the badge-unlock RewardPopup is driven by `useDashboardDiff`
 * on the Home dashboard (batch 3c, spec §4.5) — NOT mounted here. Tiles render
 * with `isNewlyEarned={false}`; 3c may pass fresh codes through navigation
 * params later if the pop-in is wanted on this screen too.
 *
 * Reviewer notes (intended deviations, see lane report):
 *  - Kit `Badge` disc is fixed at 74px (spec tile disc 64px). Spec §4.3 says
 *    "compose Badge, don't fork the disc" and this lane must not touch
 *    packages/ui — composed as-is at 74px.
 *  - Legendary status sublabel renders `$fg3` (kit Badge hardcodes it); spec
 *    wants `$purple`. Same packages/ui boundary.
 *  - Tile footer line (earned date / how-to-earn hint) is a task-directed
 *    addition on top of the preview tile anatomy (disc + name + status).
 */
import {
  BadgeRarity,
  useMyBadges,
  type BadgeStateDto,
} from '@learnexia/api-client';
import { Badge, Button, type BadgeVariant } from '@learnexia/ui';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import React from 'react';
import { ScrollView, useWindowDimensions } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { CHILD_TAB_BAR_CLEARANCE } from './_components/ChildTabBar';
import { useLocale } from '@/hooks/useLocale';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

/* ------------------------------------------------------------------ */
/* Spec literals (P4-08 §4 / preview cards)                            */
/* ------------------------------------------------------------------ */

/** Grid gap + tile chrome — `mobile-badge-tiles.html` literals. */
const GRID_GAP = 12;
const TILE_RADIUS = 20;
const TILE_PADDING = 14;
/** 3 columns; 2 under 360px (spec §4.3). */
const GRID_COLUMNS = 3;
const GRID_COLUMNS_NARROW = 2;
const NARROW_BREAKPOINT = 360;
/** Screen padding 24 + centered maxWidth 720 (spec §0.1 / W13 precedent). */
const SCREEN_PADDING = 24;
const MAX_CONTENT_WIDTH = 720;
/** Loading state — strip + 6 tile skeletons (spec §4 states). */
const SKELETON_TILE_COUNT = 6;
/** Skeleton tile height ≈ final tile geometry (74px disc + 2 text lines). */
const SKELETON_TILE_HEIGHT = 150;
/** Legendary-earned tile chrome literal (spec §4.3 / `mobile-badge-tiles.html`). */
const LEGENDARY_TILE_BORDER = 'rgba(168, 85, 247, 0.3)';
/** Bronze stat-cell value color — `mobile-badge-stats-strip.html` literal. */
const BRONZE_STAT_COLOR = '#B45309';

/** Decorative glyphs (semantic emoji set — not user-facing copy). */
const GLYPHS = {
  trophy: '🏆',
  medal: '🏅',
  warning: '⚠️',
} as const;

/* ------------------------------------------------------------------ */
/* Rarity mapping — enum members only (CLAUDE.md enum rule)            */
/* ------------------------------------------------------------------ */

/** BadgeRarity 1..4 → kit Badge variant (spec §4.4). */
const RARITY_VARIANT: Partial<Record<BadgeRarity, Exclude<BadgeVariant, 'locked' | 'boss'>>> = {
  [BadgeRarity._1]: 'bronze',
  [BadgeRarity._2]: 'silver',
  [BadgeRarity._3]: 'gold',
  [BadgeRarity._4]: 'legendary',
};

function rarityVariant(rarity: BadgeRarity | undefined): Exclude<BadgeVariant, 'locked' | 'boss'> {
  return (rarity != null ? RARITY_VARIANT[rarity] : undefined) ?? 'bronze';
}

/* ------------------------------------------------------------------ */
/* Locale-aware formatting (mirrors (child)/attempts.tsx)              */
/* ------------------------------------------------------------------ */

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

/** Eastern-Arabic numerals in ar, Latin in en (SKILL.md rule 5 prose counts). */
function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

/** Earned date "12 Jun 2026" / "١٢ يونيو ٢٠٢٦". */
function formatEarnedDate(timestamp: number, locale: string): string {
  return new Intl.DateTimeFormat(intlLocale(locale), {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date(timestamp));
}

/**
 * `awardedAtUtc` epoch ms. NSwag types it as `Date` but the runtime value is
 * the raw ISO string (no jsonParseReviver) — normalize defensively.
 */
function awardedTimestamp(badge: BadgeStateDto): number {
  const raw = badge.awardedAtUtc as unknown;
  if (raw == null) return Number.NaN;
  if (raw instanceof Date) return raw.getTime();
  return Date.parse(String(raw));
}

/* ------------------------------------------------------------------ */
/* Badge tile — composes the kit Badge inside the spec tile chrome     */
/* ------------------------------------------------------------------ */

function BadgeTileItem({
  badge,
  width,
  locale,
  direction,
}: {
  badge: BadgeStateDto;
  width: number;
  locale: string;
  direction: 'ltr' | 'rtl';
}) {
  const { t } = useTranslation();

  const earned = badge.isEarned === true;
  const variant: BadgeVariant = earned ? rarityVariant(badge.rarity) : 'locked';
  const legendaryEarned = earned && variant === 'legendary';

  const code = badge.code ?? '';
  // Unknown codes (catalog grew before the FE map) fall back to the raw code —
  // a server technical identifier, never invented copy.
  const name = t(`badges.names.${code}`, { defaultValue: code });
  const hint = t(`badges.hints.${code}`, { defaultValue: '' });
  const status = earned ? t('badges.status.earned') : t('badges.status.locked');

  const earnedMs = awardedTimestamp(badge);
  const earnedDate = earned && Number.isFinite(earnedMs)
    ? formatEarnedDate(earnedMs, locale)
    : null;

  // Footer line: earned → date; locked → how-to-earn hint (task addition).
  const footer = earned
    ? earnedDate
      ? t('badges.earnedOn', { date: earnedDate })
      : null
    : hint || null;

  const rarityLabel = t(`badges.rarity.${rarityVariant(badge.rarity)}`);
  const a11yLabel = earned
    ? earnedDate
      ? t('badges.tileA11y.earnedOn', { name, rarity: rarityLabel, date: earnedDate })
      : t('badges.tileA11y.earned', { name, rarity: rarityLabel })
    : hint
      ? t('badges.tileA11y.locked', { name, hint })
      : t('badges.tileA11y.lockedNoHint', { name });

  return (
    <YStack
      testID={`badge-tile-${code}`}
      width={width}
      alignItems="center"
      gap={6}
      padding={TILE_PADDING}
      borderRadius={TILE_RADIUS}
      backgroundColor={legendaryEarned ? '$purpleSoft' : '$card'}
      borderWidth={1}
      borderColor={legendaryEarned ? LEGENDARY_TILE_BORDER : '$borderSubtle'}
      // Locked tiles: dimmed silhouette treatment on top of the kit's locked
      // disc (no glow, $fg4 label come from Badge itself).
      opacity={earned ? 1 : 0.8}
    >
      <Badge
        variant={variant}
        label={name}
        sublabel={status}
        isNewlyEarned={false}
        accessibilityLabel={a11yLabel}
      />
      {footer ? (
        <Text
          fontSize={10}
          color="$fg4"
          textAlign="center"
          writingDirection={direction}
          numberOfLines={2}
          accessibilityElementsHidden
          importantForAccessibility="no-hide-descendants"
        >
          {footer}
        </Text>
      ) : null}
    </YStack>
  );
}

/* ------------------------------------------------------------------ */
/* Screen                                                              */
/* ------------------------------------------------------------------ */

export default function BadgesScreen() {
  const { t } = useTranslation();
  const { locale, direction, isRtl } = useLocale();
  const insets = useSafeAreaInsets();
  const { width: windowWidth } = useWindowDimensions();

  const badgesQuery = useMyBadges();
  const badges = badgesQuery.data?.badges ?? [];

  const isLoading = badgesQuery.isPending;
  const isError = badgesQuery.isError;

  const rowDir = (isRtl ? 'row-reverse' : 'row') as 'row' | 'row-reverse';

  // --- Grid geometry (3 cols; 2 under 360px — spec §4.3) -------------
  const columns = windowWidth < NARROW_BREAKPOINT ? GRID_COLUMNS_NARROW : GRID_COLUMNS;
  const contentWidth = Math.min(windowWidth - SCREEN_PADDING * 2, MAX_CONTENT_WIDTH);
  const tileWidth = Math.floor((contentWidth - GRID_GAP * (columns - 1)) / columns);

  // --- Derived data ---------------------------------------------------
  const earnedCount = badges.filter((b) => b.isEarned === true).length;
  const totalCount = badges.length;

  // Earned (newest first) then locked in server/catalog order (stable sort).
  const sorted = [...badges].sort((a, b) => {
    const aEarned = a.isEarned === true ? 1 : 0;
    const bEarned = b.isEarned === true ? 1 : 0;
    if (aEarned !== bEarned) return bEarned - aEarned;
    if (aEarned === 1) {
      const aMs = awardedTimestamp(a);
      const bMs = awardedTimestamp(b);
      const aVal = Number.isFinite(aMs) ? aMs : Number.NEGATIVE_INFINITY;
      const bVal = Number.isFinite(bMs) ? bMs : Number.NEGATIVE_INFINITY;
      return bVal - aVal;
    }
    return 0;
  });

  // Stats strip — EARNED counts per rarity (`mobile-badge-stats-strip.html`).
  const countEarned = (rarity: BadgeRarity) =>
    badges.filter((b) => b.isEarned === true && b.rarity === rarity).length;
  const statCells = [
    { key: 'bronze', label: t('badges.stats.bronze'), color: BRONZE_STAT_COLOR, count: countEarned(BadgeRarity._1) },
    { key: 'silver', label: t('badges.stats.silver'), color: '$fg3', count: countEarned(BadgeRarity._2) },
    { key: 'gold', label: t('badges.stats.gold'), color: '$warning', count: countEarned(BadgeRarity._3) },
    { key: 'legend', label: t('badges.stats.legend'), color: '$purple', count: countEarned(BadgeRarity._4) },
  ] as const;

  // Header sub: live counts; 0 earned → the encouraging empty-sub (spec §4 states).
  const subText =
    earnedCount > 0
      ? t('badges.sub', {
          earned: formatNumber(earnedCount, locale),
          total: formatNumber(totalCount, locale),
        })
      : t('badges.subEmpty');

  return (
    <TamStack flex={1} backgroundColor="$bg" testID="child-badges-screen">
      {/* Top safe area */}
      <TamStack height={insets.top} />

      <ScrollView
        contentContainerStyle={{
          paddingHorizontal: SCREEN_PADDING,
          paddingTop: 16,
          // Tab screen — clear the floating TabBar (B0-nav §3).
          paddingBottom: CHILD_TAB_BAR_CLEARANCE + insets.bottom,
        }}
        showsVerticalScrollIndicator={false}
      >
        {/* ≥768 centers at maxWidth 720 (W13 precedent, spec §0.1). */}
        <YStack width="100%" maxWidth={MAX_CONTENT_WIDTH} alignSelf="center" gap="$5">
          {/* Header — 🏆 + H1 + live counts sub (spec §4.1). */}
          <YStack gap={6}>
            <XStack
              flexDirection={rowDir}
              alignItems="center"
              justifyContent="center"
              gap={8}
            >
              <Text fontSize={24} accessibilityElementsHidden>
                {GLYPHS.trophy}
              </Text>
              <Text
                color="$fg1"
                fontSize={28}
                fontWeight="800"
                fontFamily="$heading"
                writingDirection={direction}
                accessibilityRole="header"
              >
                {t('badges.title')}
              </Text>
            </XStack>
            {!isLoading && !isError ? (
              <Text
                color="$fg2"
                fontSize={14}
                fontFamily="$body"
                textAlign="center"
                writingDirection={direction}
                testID="badges-sub"
              >
                {subText}
              </Text>
            ) : null}
          </YStack>

          {isLoading ? (
            /* Loading — stats-strip + 6 tile skeletons at final geometry. */
            <YStack testID="badges-loading" gap="$5">
              <TamStack
                height={56}
                borderRadius={16}
                backgroundColor="$cardSoft"
                opacity={0.6}
              />
              <XStack flexDirection={rowDir} flexWrap="wrap" gap={GRID_GAP}>
                {Array.from({ length: SKELETON_TILE_COUNT }, (_, i) => (
                  <TamStack
                    key={i}
                    width={tileWidth}
                    height={SKELETON_TILE_HEIGHT}
                    borderRadius={TILE_RADIUS}
                    backgroundColor="$cardSoft"
                    opacity={0.6}
                  />
                ))}
              </XStack>
            </YStack>
          ) : isError ? (
            /* Error strip — W13 dashboard pattern (spec §0.1). */
            <YStack
              testID="badges-error"
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
                {`${GLYPHS.warning} ${t('badges.error')}`}
              </Text>
              <Button
                variant="ghost"
                size="sm"
                accessibilityLabel={t('common.retry')}
                onPress={() => void badgesQuery.refetch()}
                testID="badges-retry"
              >
                {t('common.retry')}
              </Button>
            </YStack>
          ) : totalCount === 0 ? (
            /* Empty catalog edge (server sent no badge definitions). */
            <YStack
              testID="badges-empty"
              alignItems="center"
              gap="$3"
              paddingVertical={32}
            >
              <Text fontSize={64} accessibilityElementsHidden>
                {GLYPHS.medal}
              </Text>
              <Text
                color="$fg2"
                fontSize={14}
                fontFamily="$body"
                textAlign="center"
                writingDirection={direction}
              >
                {t('badges.empty')}
              </Text>
            </YStack>
          ) : (
            <>
              {/* Stats strip — earned per rarity (spec §4.2). */}
              <XStack
                testID="badges-stats"
                flexDirection={rowDir}
                gap={8}
                backgroundColor="$card"
                borderRadius={16}
                padding={12}
                borderWidth={1}
                borderColor="$borderSubtle"
              >
                {statCells.map((cell) => (
                  <YStack
                    key={cell.key}
                    flex={1}
                    alignItems="center"
                    accessible
                    accessibilityLabel={t('badges.stats.cellA11y', {
                      label: cell.label,
                      value: formatNumber(cell.count, locale),
                    })}
                    aria-label={t('badges.stats.cellA11y', {
                      label: cell.label,
                      value: formatNumber(cell.count, locale),
                    })}
                  >
                    <Text
                      fontSize={20}
                      fontWeight="900"
                      fontFamily="$heading"
                      color={cell.color}
                      style={{ fontVariant: ['tabular-nums'] }}
                      accessibilityElementsHidden
                      importantForAccessibility="no-hide-descendants"
                    >
                      {formatNumber(cell.count, locale)}
                    </Text>
                    <Text
                      fontSize={10}
                      fontWeight="700"
                      fontFamily="$heading"
                      color="$fg3"
                      textTransform="uppercase"
                      letterSpacing={0.6}
                      marginTop={2}
                      accessibilityElementsHidden
                      importantForAccessibility="no-hide-descendants"
                    >
                      {cell.label}
                    </Text>
                  </YStack>
                ))}
              </XStack>

              {/* Grid — earned (newest first) then locked (spec §4.3/§4.4). */}
              <XStack
                testID="badges-grid"
                flexDirection={rowDir}
                flexWrap="wrap"
                gap={GRID_GAP}
              >
                {sorted.map((badge, i) => (
                  <BadgeTileItem
                    key={badge.code ?? i}
                    badge={badge}
                    width={tileWidth}
                    locale={locale}
                    direction={direction}
                  />
                ))}
              </XStack>
            </>
          )}
        </YStack>
      </ScrollView>
    </TamStack>
  );
}
