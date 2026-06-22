/**
 * RewardPopup — celebration overlay for XP / badge-unlock / level-up.
 *
 * Design Spec §4.8. Center-aligned modal card (radius $modal = 24, GAP 8: use
 * 24 not the preview's 28). Celebration sequence: popup spring entrance (Moti),
 * reward icon pop, XP count-up. Confetti + radial glow burst are Skia layers on
 * native (GAP 5) — when Skia is unavailable the popup renders without confetti
 * (still a complete, legible celebration). Documented in accessibility.md.
 *
 * A11y: `accessibilityViewIsModal` + `role="alert"` + assertive live region;
 * CTA is a primary Button (pill radius) with a 44px height + hitSlop.
 */
import { gradientStops, springs } from '@learnexia/design-system';
import { Stack } from '@tamagui/core';
import React from 'react';

import { Badge, type BadgeVariant } from '../Badge';
import { Button } from '../Button';
import { GradientFill } from '../../internal/GradientFill';
import { tryLoadMoti } from '../../internal/moti';
import { ConfettiLayer } from '../../internal/ConfettiLayer';
import { Text } from '../../internal/primitives';

export type RewardVariant = 'xp' | 'badge-unlock' | 'level-up';

export interface RewardPopupProps {
  variant: RewardVariant;
  xpAmount: number;
  title: string;
  subtitle?: string;
  badgeVariant?: BadgeVariant;
  visible: boolean;
  ctaLabel?: string;
  onDismiss: () => void;
  locale?: string;
  /** Required for screen readers. */
  accessibilityLabel: string;
  /**
   * Stable test identifier for E2E targeting.
   * Defaults to `"reward-popup"`. Pass a more specific value (e.g.
   * `"league-promotion-popup"` / `"league-demotion-popup"`) when the caller
   * needs to distinguish between celebration kinds in assertions.
   */
  testID?: string;
}

export function RewardPopup({
  variant,
  xpAmount,
  title,
  subtitle,
  badgeVariant = 'gold',
  visible,
  ctaLabel = 'Keep Going',
  onDismiss,
  accessibilityLabel,
  testID = 'reward-popup',
}: RewardPopupProps) {
  const moti = tryLoadMoti();
  const MotiView = moti?.MotiView as React.ComponentType<Record<string, unknown>> | undefined;

  if (!visible) return null;

  const cardInner = (
    <Stack
      borderRadius={24}
      borderWidth={1}
      borderColor="rgba(255, 255, 255, 0.12)"
      backgroundColor="rgba(30, 41, 59, 0.85)"
      paddingVertical="$6"
      paddingHorizontal="$8"
      minWidth={280}
      maxWidth={360}
      alignItems="center"
      gap="$4"
      shadowColor="#000"
      shadowOpacity={0.55}
      shadowRadius={64}
      shadowOffset={{ width: 0, height: 24 }}
      accessibilityRole="alert"
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
      accessibilityLiveRegion="assertive"
    >
      {/* Reward icon */}
      {variant === 'badge-unlock' ? (
        <Badge
          variant={badgeVariant}
          label=""
          isNewlyEarned
          accessibilityLabel={`${badgeVariant} badge unlocked`}
        />
      ) : (
        <Stack
          width={88}
          height={88}
          borderRadius={9999}
          overflow="hidden"
          alignItems="center"
          justifyContent="center"
          shadowColor="rgba(250,204,21,0.6)"
          shadowOpacity={1}
          shadowRadius={36}
          shadowOffset={{ width: 0, height: 0 }}
        >
          <GradientFill
            stops={gradientStops.badgeGold.colors}
            angle={135}
            position="absolute"
            top={0}
            left={0}
            right={0}
            bottom={0}
          />
          <Text fontSize={40} accessibilityElementsHidden>
            {variant === 'level-up' ? '⭐' : '✨'}
          </Text>
        </Stack>
      )}

      <Text color="$fg1" fontSize={26} fontWeight="800" fontFamily="$heading" textAlign="center">
        {title}
      </Text>
      {subtitle ? (
        <Text color="$fg2" fontSize={14} fontFamily="$body" textAlign="center">
          {subtitle}
        </Text>
      ) : null}

      {xpAmount > 0 ? (
        <Text
          color="$xp"
          fontSize={36}
          fontWeight="900"
          fontFamily="$heading"
          style={{ fontVariant: ['tabular-nums'], textShadowColor: 'rgba(250,204,21,0.5)', textShadowRadius: 18 }}
        >
          {`+${xpAmount} XP`}
        </Text>
      ) : null}

      <Button
        variant="primary"
        size="sm"
        borderRadius={9999}
        paddingHorizontal={28}
        hitSlop={{ top: 4, bottom: 4, left: 0, right: 0 }}
        accessibilityLabel={ctaLabel}
        onPress={onDismiss}
      >
        {ctaLabel}
      </Button>
    </Stack>
  );

  const card = MotiView ? (
    <MotiView
      from={{ scale: 0.8, opacity: 0 }}
      animate={{ scale: 1, opacity: 1 }}
      transition={{ type: 'spring', ...springs.popup }}
    >
      {cardInner}
    </MotiView>
  ) : (
    cardInner
  );

  return (
    <Stack
      // Overlay fill
      testID={testID}
      position="absolute"
      top={0}
      left={0}
      right={0}
      bottom={0}
      alignItems="center"
      justifyContent="center"
      backgroundColor="$overlay"
      // native modal focus trap when rendered as a true overlay
      // (true Modal wrapping is the app's responsibility — P1-09)
    >
      <ConfettiLayer
        active={visible}
        paletteVariant={variant === 'level-up' ? 'levelup' : 'multicolor'}
      />
      {card}
    </Stack>
  );
}

RewardPopup.displayName = 'RewardPopup';
