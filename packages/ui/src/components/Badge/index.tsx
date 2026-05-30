/**
 * Badge — bronze / silver / gold / legendary / locked achievement disc.
 *
 * Design Spec §4.6. Disc backgrounds are radial gradients (rendered via
 * GradientFill / expo-linear-gradient approximation; web can use the
 * radialGradients string). Earned pop-in via Moti spring (pop: damping 10,
 * stiffness 200) fires only when `isNewlyEarned`.
 *
 * GAP 9 RESOLUTION: the legendary hue-rotate animation requires Skia ColorMatrix
 * on native; for P1-08 the legendary badge renders STATIC on native (no hue
 * cycle) and relies on its gradient + glow. Web hosts can add the CSS keyframe
 * separately. Documented here and in accessibility.md.
 *
 * GAP 2/4 RESOLUTION: locked label uses the gap-fill `$fg4` (#64748B); legendary
 * label uses gap-fill `$purpleLight` (#E9D5FF).
 *
 * A11y: `accessibilityRole="image"`, required `accessibilityLabel`.
 */
import { gradientStops, springs } from '@learnexia/design-system';
import { Stack } from '@tamagui/core';
import React from 'react';

import { GradientFill } from '../../internal/GradientFill';
import { tryLoadMoti } from '../../internal/moti';
import { YStack, Text } from '../../internal/primitives';

export type BadgeVariant = 'bronze' | 'silver' | 'gold' | 'legendary' | 'locked' | 'boss';

export interface BadgeProps {
  variant: BadgeVariant;
  label: string;
  sublabel?: string;
  /** Fires the pop-in entrance once (on unlock). */
  isNewlyEarned?: boolean;
  /** Required for screen readers, e.g. "Gold badge, earned". */
  accessibilityLabel: string;
}

const DISC = 74;

const ICONS: Record<BadgeVariant, string> = {
  bronze: '🥉',
  silver: '🥈',
  gold: '🥇',
  legendary: '👑',
  locked: '🔒',
  boss: '👑',
};

const DISC_STOPS = {
  bronze: gradientStops.badgeBronze.colors,
  silver: gradientStops.badgeSilver.colors,
  gold: gradientStops.badgeGold.colors,
  legendary: gradientStops.badgeLegendary.colors,
} as const;

const GLOW = {
  bronze: 'rgba(180,83,9,0.45)',
  silver: 'rgba(148,163,184,0.35)',
  gold: 'rgba(245,158,11,0.45)',
  legendary: 'rgba(168,85,247,0.65)',
} as const;

export function Badge({
  variant,
  label,
  sublabel,
  isNewlyEarned = false,
  accessibilityLabel,
}: BadgeProps) {
  const locked = variant === 'locked';

  // Boss variant renders as a pill (design spec §3.4), not a disc.
  if (variant === 'boss') {
    return (
      <Stack
        flexDirection="row"
        alignItems="center"
        gap={4}
        paddingHorizontal={10}
        paddingVertical={4}
        borderRadius={9999}
        borderWidth={1}
        borderColor="$streak"
        backgroundColor="$streakSoft"
        accessibilityRole="image"
        accessible
        accessibilityLabel={accessibilityLabel}
        aria-label={accessibilityLabel}
      >
        <Text
          fontSize={10}
          fontWeight="800"
          textTransform="uppercase"
          letterSpacing={0.4}
          color="$streak"
          fontFamily="$heading"
          style={{ fontVariant: ['tabular-nums'] }}
        >
          {`👑 ${label}`}
        </Text>
      </Stack>
    );
  }

  const labelColor = locked ? '$fg4' : variant === 'legendary' ? '$purpleLight' : '$fg2';

  const moti = tryLoadMoti();
  const MotiView = moti?.MotiView as React.ComponentType<Record<string, unknown>> | undefined;

  const discInner = (
    <Stack
      width={DISC}
      height={DISC}
      borderRadius={9999}
      alignItems="center"
      justifyContent="center"
      overflow="hidden"
      backgroundColor={locked ? '$cardSoft' : undefined}
      shadowColor={locked ? 'transparent' : GLOW[variant]}
      shadowOpacity={locked ? 0 : 1}
      shadowRadius={variant === 'legendary' ? 28 : 20}
      shadowOffset={{ width: 0, height: variant === 'legendary' ? 0 : 8 }}
    >
      {!locked ? (
        <GradientFill
          stops={DISC_STOPS[variant]}
          angle={135}
          position="absolute"
          top={0}
          left={0}
          right={0}
          bottom={0}
        />
      ) : null}
      <Text fontSize={30} accessibilityElementsHidden>
        {ICONS[variant]}
      </Text>
    </Stack>
  );

  const disc =
    isNewlyEarned && !locked && MotiView ? (
      <MotiView
        from={{ scale: 0, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        transition={{ type: 'spring', ...springs.pop }}
      >
        {discInner}
      </MotiView>
    ) : (
      discInner
    );

  return (
    <YStack
      alignItems="center"
      gap="$2"
      accessibilityRole="image"
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {disc}
      <Text color={labelColor} fontWeight="700" fontSize={12} fontFamily="$body">
        {label}
      </Text>
      {sublabel ? (
        <Text
          color="$fg3"
          fontWeight="600"
          fontSize={10}
          fontFamily="$body"
          textTransform="uppercase"
          letterSpacing={0.4}
        >
          {sublabel}
        </Text>
      ) : null}
    </YStack>
  );
}

Badge.displayName = 'Badge';
