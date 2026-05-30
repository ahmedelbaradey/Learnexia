/**
 * AttemptSummaryCard — lesson completion summary card.
 *
 * Renders score row (correct/total), accuracy %, duration. Trophy disc with
 * mount animation (opacity fade). "+10 XP" placeholder badge with TODO P4-02.
 * Primary CTA "Back to lessons" + secondary CTA "Try again".
 *
 * Motion: card opacity 0→1 240ms. Trophy disc translateY(8→0) + scale(0.9→1)
 * 200ms spring (RN Animated — no Reanimated). No confetti (W14 polish).
 *
 * A11y: `accessibilityRole="region"`. CTAs are standard Button a11y.
 * Design Spec §3.8.
 */
import React, { useEffect, useRef } from 'react';
import { Animated } from 'react-native';
import { Stack } from '@tamagui/core';

import { XStack, YStack, Text } from '../../internal/primitives';
import { Button } from '../Button';

export interface AttemptSummaryCardProps {
  /** Number of correct answers. */
  correct: number;
  /** Total number of questions. */
  total: number;
  /** 0..100 — already rounded by caller. */
  accuracyPercent: number;
  /** Duration in seconds. */
  durationSeconds: number;
  /** Primary CTA — "Back to lessons". */
  onBack: () => void;
  /** Secondary CTA — "Try again" (re-creates the attempt). */
  onRetry: () => void;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** Already-localized labels. */
  titleText?: string;
  backLabel?: string;
  retryLabel?: string;
  correctLabel?: string;
  accuracyLabel?: string;
  durationLabel?: string;
  xpStubText?: string;
  testID?: string;
}

export function AttemptSummaryCard({
  correct,
  total,
  accuracyPercent,
  durationSeconds,
  onBack,
  onRetry,
  direction = 'ltr',
  locale = 'ar',
  titleText,
  backLabel,
  retryLabel,
  correctLabel,
  accuracyLabel,
  durationLabel,
  xpStubText,
  testID,
}: AttemptSummaryCardProps) {
  const isRtl = direction === 'rtl';
  const isAr = (locale ?? 'ar') === 'ar';

  // Card entrance animation.
  const cardOpacity = useRef(new Animated.Value(0)).current;
  // Trophy disc animation.
  const trophyTranslateY = useRef(new Animated.Value(8)).current;
  const trophyScale = useRef(new Animated.Value(0.9)).current;

  useEffect(() => {
    Animated.parallel([
      Animated.timing(cardOpacity, {
        toValue: 1,
        duration: 240,
        useNativeDriver: true,
      }),
      Animated.timing(trophyTranslateY, {
        toValue: 0,
        duration: 200,
        useNativeDriver: true,
      }),
      Animated.timing(trophyScale, {
        toValue: 1,
        duration: 200,
        useNativeDriver: true,
      }),
    ]).start();
  }, [cardOpacity, trophyTranslateY, trophyScale]);

  // Localized defaults
  const displayTitle = titleText ?? (isAr ? 'اكتمل الدرس!' : 'Lesson complete!');
  const displayBack = backLabel ?? (isAr ? 'الرجوع إلى الدروس' : 'Back to lessons');
  const displayRetry = retryLabel ?? (isAr ? 'حاول مجددًا' : 'Try again');
  const displayCorrectLabel = correctLabel ?? (isAr ? 'صحيحة' : 'Correct');
  const displayAccuracyLabel = accuracyLabel ?? (isAr ? 'الدقة' : 'Accuracy');
  const displayDurationLabel = durationLabel ?? (isAr ? 'الوقت' : 'Time');
  // TODO P4-02 — wire real XP reward
  const displayXpStub = xpStubText ?? (isAr ? '+١٠ نقطة خبرة (قريبًا)' : '+10 XP (coming soon)');

  // Format numbers. Arabic numerals for AR locale.
  const ARABIC_DIGITS = ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'] as const;
  const toArabicNumerals = (n: string) =>
    n.replace(/[0-9]/g, (d) => ARABIC_DIGITS[parseInt(d)] ?? d);

  const formatNumber = (n: number) =>
    isAr ? toArabicNumerals(String(n)) : String(n);

  const a11yRegionLabel = `${displayTitle}. ${correct} ${isAr ? 'من' : 'of'} ${total} ${displayCorrectLabel}. ${accuracyPercent}${isAr ? '٪' : '%'} ${displayAccuracyLabel}. ${durationSeconds}${isAr ? ' ث' : 's'}.`;

  return (
    <Animated.View
      style={{ opacity: cardOpacity }}
      accessible
      accessibilityLabel={a11yRegionLabel}
    >
      <YStack
        backgroundColor="$card"
        borderWidth={1}
        borderColor="$borderStrong"
        borderRadius={24}
        padding={24}
        gap={20}
        shadowColor="rgba(0,0,0,0.15)"
        shadowOpacity={1}
        shadowRadius={12}
        shadowOffset={{ width: 0, height: 4 }}
        alignItems="center"
        testID={testID}
      >
        {/* Trophy disc */}
        <Animated.View
          style={{
            transform: [
              { translateY: trophyTranslateY },
              { scale: trophyScale },
            ],
          }}
          accessibilityElementsHidden
        >
          <Stack
            width={96}
            height={96}
            borderRadius={9999}
            alignItems="center"
            justifyContent="center"
            // $gradXp gradient — using background shorthand (web) + fallback color (native).
            backgroundColor="$primary"
            shadowColor="rgba(99,102,241,0.45)"
            shadowOpacity={1}
            shadowRadius={24}
            shadowOffset={{ width: 0, height: 8 }}
          >
            <Text fontSize={56} accessibilityElementsHidden>
              {'🏆'}
            </Text>
          </Stack>
        </Animated.View>

        {/* Title */}
        <Text
          fontSize={24}
          fontWeight="800"
          fontFamily="$heading"
          color="$fg1"
          textAlign="center"
          writingDirection={direction}
        >
          {displayTitle}
        </Text>

        {/* Score row */}
        <XStack
          flexDirection={isRtl ? 'row-reverse' : 'row'}
          justifyContent="space-around"
          alignItems="flex-start"
          width="100%"
        >
          {/* Correct */}
          <YStack alignItems="center" gap={4}>
            <Text
              fontSize={28}
              fontWeight="800"
              color="$success"
              fontFamily="$heading"
              style={{ fontVariant: ['tabular-nums'] }}
            >
              {`${formatNumber(correct)} / ${formatNumber(total)}`}
            </Text>
            <Text
              fontSize={12}
              fontWeight="700"
              color="$fg3"
              fontFamily="$heading"
              textTransform="uppercase"
              letterSpacing={0.04}
              textAlign="center"
            >
              {displayCorrectLabel}
            </Text>
          </YStack>

          {/* Accuracy */}
          <YStack alignItems="center" gap={4}>
            <Text
              fontSize={28}
              fontWeight="800"
              color="$primary"
              fontFamily="$heading"
              style={{ fontVariant: ['tabular-nums'] }}
            >
              {`${formatNumber(accuracyPercent)}${isAr ? '٪' : '%'}`}
            </Text>
            <Text
              fontSize={12}
              fontWeight="700"
              color="$fg3"
              fontFamily="$heading"
              textTransform="uppercase"
              letterSpacing={0.04}
              textAlign="center"
            >
              {displayAccuracyLabel}
            </Text>
          </YStack>

          {/* Duration */}
          <YStack alignItems="center" gap={4}>
            {/* LTR wrapper for duration so the suffix 's' / 'ث' stays correct. */}
            <Text
              fontSize={28}
              fontWeight="800"
              color="$fg1"
              fontFamily="$heading"
              style={{ fontVariant: ['tabular-nums'] }}
              dir="ltr"
            >
              {`${formatNumber(durationSeconds)}${isAr ? 'ث' : 's'}`}
            </Text>
            <Text
              fontSize={12}
              fontWeight="700"
              color="$fg3"
              fontFamily="$heading"
              textTransform="uppercase"
              letterSpacing={0.04}
              textAlign="center"
            >
              {displayDurationLabel}
            </Text>
          </YStack>
        </XStack>

        {/* XP placeholder badge — TODO P4-02 — wire real XP reward */}
        <Stack
          paddingHorizontal={12}
          paddingVertical={6}
          borderRadius={9999}
          backgroundColor="rgba(250,204,21,0.18)"
          borderWidth={1}
          borderColor="rgba(250,204,21,0.35)"
        >
          <Text
            fontSize={12}
            fontWeight="800"
            fontFamily="$heading"
            color="$xp"
            textTransform="uppercase"
            letterSpacing={0.04}
          >
            {/* TODO P4-02 — wire real XP reward */}
            {displayXpStub}
          </Text>
        </Stack>

        {/* CTA stack */}
        <YStack gap={12} width="100%">
          <Button
            variant="primary"
            size="full"
            accessibilityLabel={displayBack}
            onPress={onBack}
          >
            {displayBack}
          </Button>
          <Button
            variant="ghost"
            size="full"
            accessibilityLabel={displayRetry}
            onPress={onRetry}
          >
            {displayRetry}
          </Button>
        </YStack>
      </YStack>
    </Animated.View>
  );
}

AttemptSummaryCard.displayName = 'AttemptSummaryCard';
