/**
 * AITutorBubble — chat bubble for AI / student / hint messages.
 *
 * Design Spec §4.7. Variants differ in background, sender label color, avatar
 * presence, and tail side. Typing indicator: three Moti-staggered dots.
 *
 * GAP 5 RESOLUTION (backdrop blur): the AI bubble's frosted-glass blur uses Skia
 * on native; when Skia is unavailable it falls back to a semi-transparent
 * overlay-dark background WITHOUT blur (still legible). For P1-08 (no Metro/Skia
 * host) the fallback is the default render path. Documented in accessibility.md.
 *
 * GAP 7 RESOLUTION (tail): the tail is the flattened-corner technique from the
 * preview (one bottom corner → 4px) using LOGICAL border radii, so it mirrors
 * automatically in RTL. No SVG/Skia triangle.
 *
 * RTL: tail corner uses borderBottomStart/EndRadius; the row direction (avatar
 * vs bubble) flips with `flexDirection`.
 */
import { directionForLocale } from '@learnexia/shared/i18n';
import { Stack } from '@tamagui/core';
import React from 'react';

import { GradientFill } from '../../internal/GradientFill';
import { tryLoadMoti } from '../../internal/moti';
import { XStack, YStack, Text } from '../../internal/primitives';

export type AITutorVariant = 'ai' | 'student' | 'hint';

export interface AITutorChip {
  label: string;
  onPress?: () => void;
  accessibilityLabel?: string;
}

export interface AITutorBubbleProps {
  variant: AITutorVariant;
  /** Message body. When null + variant 'ai', shows the typing indicator. */
  message: string | null;
  /** Sender label (e.g. "Lexi · AI Tutor", student name, "Hint"). */
  sender?: string;
  chips?: AITutorChip[];
  locale?: string;
  /** Required for screen readers (sender + message summary). */
  accessibilityLabel: string;
}

const BUBBLE_BG = {
  ai: 'rgba(15, 23, 42, 0.7)',
  student: '$cardSoft',
  hint: '$warningSoft',
} as const;

const SENDER_COLOR = {
  ai: '$primary',
  student: '$fg3',
  hint: '$warning',
} as const;

function TypingIndicator() {
  const moti = tryLoadMoti();
  const MotiView = moti?.MotiView as React.ComponentType<Record<string, unknown>> | undefined;
  const dot = (delay: number) => {
    const inner = <Stack width={8} height={8} borderRadius={9999} backgroundColor="$fg3" />;
    if (!MotiView) return inner;
    return (
      <MotiView
        from={{ scale: 0.5 }}
        animate={{ scale: 1 }}
        transition={{ type: 'timing', duration: 240, loop: true, repeatReverse: true, delay }}
      >
        {inner}
      </MotiView>
    );
  };
  return (
    <XStack
      gap="$1"
      alignItems="center"
      accessible
      accessibilityLabel="AI tutor is typing"
      aria-label="AI tutor is typing"
      accessibilityLiveRegion="polite"
    >
      {dot(0)}
      {dot(80)}
      {dot(160)}
    </XStack>
  );
}

function Chip({ chip }: { chip: AITutorChip }) {
  return (
    <Stack
      backgroundColor="$primarySoft"
      borderColor="rgba(99, 102, 241, 0.3)"
      borderWidth={1}
      borderRadius={9999}
      paddingVertical={5}
      paddingHorizontal={10}
      minHeight={30}
      // visual chip is 30px tall → extend touch target to 48px
      hitSlop={{ top: 9, bottom: 9, left: 0, right: 0 }}
      cursor="pointer"
      pressStyle={{ scale: 0.95 }}
      onPress={chip.onPress}
      accessibilityRole="button"
      accessible
      accessibilityLabel={chip.accessibilityLabel ?? chip.label}
      aria-label={chip.accessibilityLabel ?? chip.label}
    >
      <Text color="$primaryLight" fontSize={12} fontWeight="600" fontFamily="$body">
        {chip.label}
      </Text>
    </Stack>
  );
}

export function AITutorBubble({
  variant,
  message,
  sender,
  chips,
  locale = 'en',
  accessibilityLabel,
}: AITutorBubbleProps) {
  const isRtl = directionForLocale(locale) === 'rtl';
  const isAi = variant === 'ai';
  const showTyping = isAi && message === null;

  // Tail: flatten one bottom corner. AI tail = start side; student tail = end.
  // Logical radii mirror automatically in RTL.
  const tailRadii =
    variant === 'student'
      ? { borderBottomStartRadius: 20, borderBottomEndRadius: 4 }
      : { borderBottomStartRadius: 4, borderBottomEndRadius: 20 };

  const bubble = (
    <Stack
      backgroundColor={BUBBLE_BG[variant]}
      borderWidth={1}
      borderColor={
        variant === 'hint'
          ? 'rgba(245,158,11,0.3)'
          : variant === 'student'
            ? '$borderStrong'
            : '$borderInput'
      }
      borderTopStartRadius={20}
      borderTopEndRadius={20}
      {...tailRadii}
      padding="$4"
      maxWidth={320}
      gap="$2"
      accessibilityRole="text"
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {sender ? (
        <Text
          color={SENDER_COLOR[variant]}
          fontSize={11}
          fontWeight="800"
          fontFamily="$heading"
          textTransform="uppercase"
          letterSpacing={0.5}
        >
          {sender}
        </Text>
      ) : null}

      {showTyping ? (
        <TypingIndicator />
      ) : (
        <Text color="$fg1" fontSize={15} lineHeight={22} fontFamily="$body">
          {message}
        </Text>
      )}

      {chips && chips.length > 0 ? (
        <XStack gap="$1" flexWrap="wrap" marginTop="$2">
          {chips.map((c, i) => (
            <Chip key={i} chip={c} />
          ))}
        </XStack>
      ) : null}
    </Stack>
  );

  if (!isAi) {
    return bubble;
  }

  // AI variant: avatar + bubble row (avatar leads on the start side; row flips in RTL).
  return (
    <XStack
      gap="$2"
      alignItems="flex-end"
      flexDirection={isRtl ? 'row-reverse' : 'row'}
    >
      <Stack
        width={64}
        height={64}
        borderRadius={9999}
        overflow="hidden"
        alignItems="center"
        justifyContent="center"
        accessibilityElementsHidden
      >
        <GradientFill
          stops={['#A78BFA', '#6366F1']}
          angle={135}
          position="absolute"
          top={0}
          left={0}
          right={0}
          bottom={0}
        />
        <Text fontSize={26}>{'🦉'}</Text>
      </Stack>
      <YStack flexShrink={1}>{bubble}</YStack>
    </XStack>
  );
}

AITutorBubble.displayName = 'AITutorBubble';
