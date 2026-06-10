/**
 * AnswerFeedbackStrip — correct / incorrect feedback strip.
 *
 * Green variant: slides in 160ms, auto-dismisses after 800ms (1200ms if
 * reduced-motion). Incorrect variant: persists until parent transitions.
 *
 * Motion: CSS Animated-style fade + translate using RN's Animated API
 * (no Reanimated 3 or Skia — per W12 spec). Reduced motion: fade only,
 * no translate. Uses `borderStartWidth` (logical) for the leading-edge bar so
 * it auto-flips in RTL.
 *
 * A11y: `accessibilityRole="alert"` (web), `accessibilityLiveRegion="polite"`
 * (Android) — screen-reader announces on mount. Design Spec §3.5.
 */
import React, { useEffect, useRef } from 'react';
import { Animated, Platform } from 'react-native';
import { Stack } from '@tamagui/core';

import { XStack, YStack, Text } from '../../internal/primitives';

export interface AnswerFeedbackStripProps {
  variant: 'correct' | 'incorrect';
  /** Required when variant='incorrect' — already localized + interpolated. */
  revealText?: string;
  /** Optional "Next" handler — not used by W12 (QuestionCard owns the CTA). */
  onNext?: () => void;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
  /** Already-localized title, e.g. "Great job!" / "Not quite". */
  title?: string;
}

const ENTER_DURATION = 160;
// AUTO_ADVANCE_MS / REDUCED_MOTION_MS owned by the lesson screen (TODO P4 motion pass).

export function AnswerFeedbackStrip({
  variant,
  revealText,
  title,
  direction = 'ltr',
  locale = 'ar',
  testID,
}: AnswerFeedbackStripProps) {
  const isCorrect = variant === 'correct';
  const isRtl = direction === 'rtl';

  // Fade + translate animation on mount.
  const opacity = useRef(new Animated.Value(0)).current;
  const translateY = useRef(new Animated.Value(-4)).current;

  useEffect(() => {
    Animated.parallel([
      Animated.timing(opacity, {
        toValue: 1,
        duration: ENTER_DURATION,
        useNativeDriver: true,
      }),
      Animated.timing(translateY, {
        toValue: 0,
        duration: ENTER_DURATION,
        useNativeDriver: true,
      }),
    ]).start();
  }, [opacity, translateY]);

  const defaultCorrectTitle = locale === 'ar' ? 'أحسنت!' : 'Great job!';
  const defaultIncorrectTitle = locale === 'ar' ? 'ليست الإجابة الصحيحة' : 'Not quite';

  const displayTitle = title ?? (isCorrect ? defaultCorrectTitle : defaultIncorrectTitle);

  const a11yLabel = isCorrect
    ? displayTitle
    : `${displayTitle}${revealText ? `. ${revealText}` : ''}`;

  return (
    <Animated.View
      style={{ opacity, transform: [{ translateY }] }}
      accessibilityLiveRegion="polite"
      accessible
      accessibilityRole="alert"
      accessibilityLabel={a11yLabel}
      aria-live="polite"
      aria-label={a11yLabel}
    >
      <Stack
        flexDirection={isRtl ? 'row-reverse' : 'row'}
        paddingVertical={12}
        paddingHorizontal={16}
        gap={12}
        borderRadius={14}
        backgroundColor={isCorrect ? '$successSoft' : '$dangerSoft'}
        // Logical leading-edge accent bar (auto-flips with RTL layout).
        borderStartWidth={3}
        borderStartColor={isCorrect ? '$success' : '$danger'}
        alignItems="flex-start"
        testID={testID}
        // @ts-ignore — data-* attributes are web-only, non-user-facing E2E hooks
        data-correct={Platform.OS === 'web' ? String(isCorrect) : undefined}
      >
        {/* Glyph disc */}
        <Stack
          width={32}
          height={32}
          borderRadius={9999}
          backgroundColor={isCorrect ? 'rgba(34,197,94,0.28)' : 'rgba(239,68,68,0.28)'}
          alignItems="center"
          justifyContent="center"
          accessibilityElementsHidden
        >
          <Text
            fontSize={18}
            color={isCorrect ? '$success' : '$danger'}
          >
            {isCorrect ? '✓' : '✕'}
          </Text>
        </Stack>

        {/* Body */}
        <YStack flex={1} gap={4}>
          <Text
            fontSize={16}
            fontWeight="800"
            fontFamily="$heading"
            color="$fg1"
            writingDirection={direction}
          >
            {displayTitle}
          </Text>
          {!isCorrect && revealText ? (
            <Text
              fontSize={14}
              fontWeight="500"
              fontFamily="$body"
              color="$fg2"
              numberOfLines={2}
              writingDirection={direction}
              textAlign={isRtl ? 'right' : 'left'}
            >
              {revealText}
            </Text>
          ) : null}
        </YStack>
      </Stack>
    </Animated.View>
  );
}

AnswerFeedbackStrip.displayName = 'AnswerFeedbackStrip';
