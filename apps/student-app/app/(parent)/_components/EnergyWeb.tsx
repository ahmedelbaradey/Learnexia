/**
 * EnergyWeb — the parent "Helper Energy" screen body (Batch D, design
 * `PMScreensExtra.jsx` → `PMEnergyScreen` / preview `pm-energy-battery.html`).
 *
 * Composition:
 *   - Teal battery balance card (big "180 / 300" + a battery fill visual).
 *   - "Two separate meters" reassurance — ⚡ Energy (teal, AI-helper fuel) is
 *     NOT ❤️ Hearts (rose, lives). Kept visually distinct (color + icon + copy)
 *     per the handoff "Hearts vs Energy — never merge these two meters".
 *   - Weekly usage tiles (4 helper kinds, stub counts).
 *   - Top-up pack row (+500 ⚡ · $2.99) + a GATED "Buy credits" CTA + the
 *     "Children never see prices" note.
 *
 * ⚠️ IAP IS GATED. The "Buy credits" CTA is a deliberate stub: it does NOT
 * start a real purchase. It surfaces a local "coming soon" notice (no network,
 * no store config). See the IAP TODO on `handleBuy`.
 *
 * DATA: a Phase-10 billing/energy-ledger backend exists on `main` but has NO
 * api-client hooks yet, so balance + usage come from DISPLAY-ONLY deterministic
 * stubs (`getEnergyBalanceStub` / `getEnergyUsageStub`). When the energy
 * api-client lands, swap those for the real query — the layout is data-shaped.
 *
 * BUILD CONTRACT: numeric border radii; tokens for colors where they exist;
 * teal energy as literals (#2DD4BF / #14B8A6 — no teal token, must stay distinct
 * from hearts/rose). RTL via explicit `dir` (NO row-reverse). Eastern-Arabic
 * numerals in AR prose via Intl; Latin for technical strings ($2.99, 180/300).
 * Reuses ParentHeader + the existing card/tile shapes — no new design pattern.
 */
import { Button } from '@learnexia/ui';
import { Stack, Text, type StackProps } from '@tamagui/core';
import React, { useState } from 'react';
import { useWindowDimensions } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { ParentHeader } from './ParentHeader';
import {
  getEnergyBalanceStub,
  getEnergyUsageStub,
  getEnergyTopUpPacksStub,
  type EnergyUsageStub,
} from './parentDashboardStubs';

/** Teal energy literals — NO teal token exists; kept distinct from hearts/rose. */
const ENERGY_TEAL = '#2DD4BF';
const ENERGY_TEAL_DEEP = '#14B8A6';

/** Per-helper accent + glyph (decorative; label comes from i18n). */
const USAGE_META: Record<EnergyUsageStub['kind'], { icon: string; color: string }> = {
  hints: { icon: '💡', color: ENERGY_TEAL },
  explain: { icon: '🔍', color: '#C4B5FD' },
  deep: { icon: '📖', color: '#A5B4FC' },
  practice: { icon: '🎯', color: '#FDBA74' },
};

const USAGE_LABEL_KEY: Record<EnergyUsageStub['kind'], string> = {
  hints: 'parent.energy.usage.hints',
  explain: 'parent.energy.usage.explain',
  deep: 'parent.energy.usage.deep',
  practice: 'parent.energy.usage.practice',
};

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

/** Localized digits for AR prose; Latin for en. */
function fmtNum(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

export function EnergyWeb() {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const { width } = useWindowDimensions();

  // DISPLAY-ONLY stubs (no energy api-client yet — see file header).
  const balance = getEnergyBalanceStub();
  const usage = getEnergyUsageStub();
  const [pack] = getEnergyTopUpPacksStub();

  // Gated IAP — local notice only, never a purchase. Cleared after a tick.
  const [buyNotice, setBuyNotice] = useState(false);
  const handleBuy = () => {
    // TODO(Batch D / P10 IAP): wire to payments backend + store config; needs
    // security-auditor. Until then this is a NO-OP stub — no purchase, no
    // network, no store product. It only surfaces a "coming soon" notice.
    setBuyNotice(true);
  };

  const align = isRtl ? 'right' : 'left';
  // The balance/price strings stay Latin (technical), even in AR.
  const balanceTechnical = `${balance.balance} / ${balance.cap}`;
  const fillPercent = Math.max(0, Math.min(100, Math.round((balance.balance / balance.cap) * 100)));

  const isDesktop = width >= 1025;

  return (
    <Stack testID="energy-root" flexDirection="column" width="100%">
      <ParentHeader title={t('parent.energy.title')} subtitle={t('parent.energy.subtitle')} />

      <Stack flexDirection="column" gap="$5" padding="$6">
        {/* ---- Teal battery balance card ---- */}
        <Stack
          testID="energy-balance-card"
          dir={isRtl ? 'rtl' : 'ltr'}
          flexDirection="column"
          gap={14}
          borderRadius={20}
          padding={18}
          borderWidth={1}
          borderColor="rgba(45,212,191,0.35)"
          backgroundColor="rgba(20,184,166,0.12)"
          accessible
          accessibilityLabel={t('parent.energy.balance.a11y', {
            balance: fmtNum(balance.balance, locale),
            total: fmtNum(balance.cap, locale),
          })}
        >
          <Stack flexDirection="row" alignItems="center" justifyContent="space-between" gap="$2">
            <Stack flexDirection="row" alignItems="center" gap="$2" flex={1} minWidth={0}>
              <Text fontSize={20} accessibilityElementsHidden>{'⚡'}</Text>
              <Text
                color="$fg1"
                fontSize={15}
                fontWeight="800"
                fontFamily="$heading"
                writingDirection={direction}
                numberOfLines={2}
              >
                {t('parent.energy.balance.heading')}
              </Text>
            </Stack>
            {/* Balance is a Latin technical string ("180 / 300") even in AR. */}
            <Text
              color={ENERGY_TEAL}
              fontSize={24}
              fontWeight="900"
              fontFamily="$heading"
              style={{ fontVariant: ['tabular-nums'] }}
              accessibilityElementsHidden
            >
              {balanceTechnical}
            </Text>
          </Stack>

          {/* Battery fill visual */}
          <Stack flexDirection="row" alignItems="center" gap={6} accessibilityElementsHidden>
            <Stack
              flex={1}
              height={24}
              borderRadius={8}
              borderWidth={2}
              borderColor={ENERGY_TEAL_DEEP}
              backgroundColor="$bg"
              padding={3}
              overflow="hidden"
            >
              <Stack
                height="100%"
                width={`${fillPercent}%`}
                borderRadius={4}
                backgroundColor={ENERGY_TEAL}
              />
            </Stack>
            {/* Battery nub */}
            <Stack width={5} height={12} borderTopRightRadius={3} borderBottomRightRadius={3} backgroundColor={ENERGY_TEAL_DEEP} />
          </Stack>

          <Text
            color="$fg3"
            fontSize={12}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={align}
          >
            {'📅 '}
            {t('parent.energy.balance.reset', {
              days: t('parent.energy.balance.resetDays', {
                count: balance.resetsInDays,
                value: fmtNum(balance.resetsInDays, locale),
              }),
            })}
            {' · '}
            {t('parent.energy.balance.dailyCap', {
              count: balance.dailyCap,
              value: fmtNum(balance.dailyCap, locale),
            })}
          </Text>
        </Stack>

        {/* ---- Hearts vs Energy reassurance — two distinct meters ---- */}
        <Stack
          testID="energy-meters-card"
          dir={isRtl ? 'rtl' : 'ltr'}
          flexDirection="column"
          gap={10}
          borderRadius={20}
          padding={16}
          backgroundColor="$card"
          borderWidth={1}
          borderColor="$borderSubtle"
        >
          <Text
            color="$fg3"
            fontSize={11}
            fontWeight="800"
            fontFamily="$heading"
            textTransform="uppercase"
            letterSpacing={0.6}
            writingDirection={direction}
            textAlign={align}
          >
            {t('parent.energy.meters.heading')}
          </Text>
          {/* ⚡ Energy — teal */}
          <MeterRow
            icon="⚡"
            name={t('parent.energy.meters.energyName')}
            nameColor={ENERGY_TEAL}
            desc={t('parent.energy.meters.energyDesc')}
            direction={direction}
            align={align}
          />
          {/* ❤️ Hearts — rose (heart token) */}
          <MeterRow
            icon="❤️"
            name={t('parent.energy.meters.heartsName')}
            nameColor="$heart"
            desc={t('parent.energy.meters.heartsDesc')}
            direction={direction}
            align={align}
          />
        </Stack>

        {/* ---- Weekly usage tiles ---- */}
        <Stack
          testID="energy-usage-card"
          dir={isRtl ? 'rtl' : 'ltr'}
          flexDirection="column"
          gap={12}
          borderRadius={20}
          padding={16}
          backgroundColor="$card"
          borderWidth={1}
          borderColor="$borderSubtle"
        >
          <Text
            color="$fg1"
            fontSize={14}
            fontWeight="800"
            fontFamily="$heading"
            writingDirection={direction}
            textAlign={align}
          >
            {t('parent.energy.usage.heading')}
          </Text>
          <Stack flexDirection="row" gap={8} flexWrap={isDesktop ? 'nowrap' : 'wrap'}>
            {usage.map((u) => {
              const meta = USAGE_META[u.kind];
              const label = t(USAGE_LABEL_KEY[u.kind]);
              return (
                <Stack
                  key={u.kind}
                  testID={`energy-usage-${u.kind}`}
                  flex={1}
                  minWidth={64}
                  padding={12}
                  borderRadius={13}
                  backgroundColor="$bg"
                  alignItems="center"
                  gap={6}
                  accessible
                  accessibilityLabel={`${label}: ${fmtNum(u.count, locale)}`}
                >
                  <Stack
                    width={34}
                    height={34}
                    borderRadius={10}
                    alignItems="center"
                    justifyContent="center"
                    // Dynamic per-helper tint (literal hex + alpha) — cast like
                    // FocusAreasCard's chipBg since it isn't a theme token.
                    backgroundColor={`${meta.color}22` as StackProps['backgroundColor']}
                    accessibilityElementsHidden
                  >
                    <Text fontSize={16}>{meta.icon}</Text>
                  </Stack>
                  <Text
                    color="$fg1"
                    fontSize={18}
                    fontWeight="900"
                    fontFamily="$heading"
                    style={{ fontVariant: ['tabular-nums'] }}
                    accessibilityElementsHidden
                  >
                    {fmtNum(u.count, locale)}
                  </Text>
                  <Text
                    color="$fg3"
                    fontSize={10}
                    fontWeight="700"
                    fontFamily="$heading"
                    writingDirection={direction}
                    accessibilityElementsHidden
                  >
                    {label}
                  </Text>
                </Stack>
              );
            })}
          </Stack>
        </Stack>

        {/* ---- Top-up pack row + GATED buy CTA ---- */}
        {pack ? (
          <Stack flexDirection="column" gap={10}>
            <Stack
              testID="energy-topup-pack"
              dir={isRtl ? 'rtl' : 'ltr'}
              flexDirection="row"
              alignItems="center"
              gap={12}
              padding={14}
              borderRadius={16}
              borderWidth={1}
              borderColor="rgba(45,212,191,0.3)"
              backgroundColor="rgba(45,212,191,0.10)"
            >
              <Text fontSize={28} accessibilityElementsHidden>{'🔋'}</Text>
              <Stack flex={1} minWidth={0}>
                <Text
                  color={ENERGY_TEAL}
                  fontSize={18}
                  fontWeight="900"
                  fontFamily="$heading"
                  writingDirection={direction}
                  textAlign={align}
                >
                  {t('parent.energy.topUp.packTitle', { credits: fmtNum(pack.credits, locale) })}
                </Text>
                <Text
                  color="$fg3"
                  fontSize={11}
                  fontFamily="$body"
                  writingDirection={direction}
                  textAlign={align}
                >
                  {t('parent.energy.topUp.packSubtitle')}
                </Text>
              </Stack>
              {/* Price is a Latin technical string ($2.99) even in AR. */}
              <Text
                color="$fg1"
                fontSize={18}
                fontWeight="900"
                fontFamily="$heading"
                style={{ fontVariant: ['tabular-nums'] }}
              >
                {pack.priceLabel}
              </Text>
            </Stack>

            {/* GATED stub: surfaces a "coming soon" notice — NOT a purchase. */}
            <Button
              variant="primary"
              size="md"
              onPress={handleBuy}
              accessibilityLabel={t('parent.energy.topUp.buy', { credits: fmtNum(pack.credits, locale) })}
              testID="energy-buy-button"
            >
              {`🔋 ${t('parent.energy.topUp.buy', { credits: fmtNum(pack.credits, locale) })}`}
            </Button>

            {buyNotice ? (
              <Stack
                testID="energy-buy-notice"
                backgroundColor="$card"
                borderRadius={12}
                borderWidth={1}
                borderColor="$borderSubtle"
                padding="$3"
                accessibilityLiveRegion="polite"
              >
                <Text
                  color="$fg2"
                  fontSize={13}
                  fontFamily="$body"
                  writingDirection={direction}
                  textAlign="center"
                >
                  {t('parent.energy.topUp.comingSoon')}
                </Text>
              </Stack>
            ) : null}

            <Text
              color="$fg4"
              fontSize={11}
              fontFamily="$body"
              writingDirection={direction}
              textAlign="center"
            >
              {t('parent.energy.topUp.priceNote')}
            </Text>
          </Stack>
        ) : null}
      </Stack>
    </Stack>
  );
}

EnergyWeb.displayName = 'EnergyWeb';

interface MeterRowProps {
  icon: string;
  name: string;
  nameColor: React.ComponentProps<typeof Text>['color'];
  desc: string;
  direction: 'ltr' | 'rtl';
  align: 'left' | 'right';
}

function MeterRow({ icon, name, nameColor, desc, direction, align }: MeterRowProps) {
  return (
    <Stack flexDirection="row" alignItems="center" gap={10}>
      <Text fontSize={22} accessibilityElementsHidden>{icon}</Text>
      <Text fontSize={13} fontFamily="$body" writingDirection={direction} textAlign={align} flex={1}>
        <Text color={nameColor} fontSize={13} fontWeight="800" fontFamily="$heading">
          {name}
        </Text>
        <Text color="$fg3" fontSize={13} fontFamily="$body">
          {` ${desc}`}
        </Text>
      </Text>
    </Stack>
  );
}
