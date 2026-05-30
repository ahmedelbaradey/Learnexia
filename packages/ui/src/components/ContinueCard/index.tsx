/**
 * ContinueCard — W13 P2-09-FE
 *
 * Tappable "resume" tile for the home dashboard Continue section.
 *
 * Design Spec §3.2 (W13-home-dashboard.md):
 *   - Single full-width card (NOT the 2-up rail from the EN screenshot).
 *   - Subject icon tile on the logical leading edge (tinted by subjectKey).
 *   - Body column: eyebrow + lesson title + optional unit caption.
 *   - CTA chip on the logical trailing edge.
 *   - Optional Boss badge overlay at top-trailing corner.
 *   - Available state: `$primary` border + glow. Completed state: quiet border.
 *   - Progress bar omitted in W13 (`progressPercent` prop reserved for P3-08).
 *   - Hover lift 160ms (web); press scale 80ms; focus ring.
 *   - RTL: `flexDirection` flip; CTA chevron swaps `→` / `←`; Boss badge uses
 *     logical `end` positioning.
 *
 * Touch target: full card (≥80px tall × full-width) — far above the 48px floor.
 * Token-only: no raw hex. No physical left/right props.
 */

import React from 'react';
import { Pressable } from 'react-native';
import { Stack } from '@tamagui/core';

import { Badge } from '../Badge';
import { XStack, YStack, Text } from '../../internal/primitives';

export type SubjectKey = 'math' | 'science' | 'arabic' | 'english';

/** ContinueCard available vs completed chrome state. */
type ContinueState = 'available' | 'completed';

/** Subject tint map — token refs per brand design. */
const SUBJECT_TINTS: Record<SubjectKey, { fg: string; soft: string; glyph: string }> = {
  math: { fg: '$primary', soft: '$primarySoft', glyph: '🧮' },
  science: { fg: '$success', soft: '$successSoft', glyph: '🧪' },
  arabic: { fg: '$accent', soft: '$warningSoft', glyph: '📖' },
  english: { fg: '$purple', soft: '$purpleSoft', glyph: '🔤' },
};

export interface ContinueCardProps {
  /** Localized subject name from ContinueTargetDto.subjectName. */
  subjectName: string;
  /** Resolved key for tint — caller maps from subjectName via SUBJECT_NAME_MAP. */
  subjectKey: SubjectKey;
  /** Localized lesson name from ContinueTargetDto.lessonName. */
  lessonName: string;
  /** Optional unit name — rendered as 2nd-line caption when set. */
  unitName?: string;
  /** Optional skill name — prepended to the eyebrow when set. */
  skillName?: string;
  /** When true, renders Boss badge + 🔥 prefix on the CTA. */
  isBoss?: boolean;
  /** 1=Available (primary chrome), 2=Completed (quiet chrome). Default: 1. */
  nodeState?: 1 | 2;
  /** Reserved for Phase 3 P3-08 — progress bar (0..100). Omitted in W13. */
  progressPercent?: number;
  /** Tap handler — caller passes router.push to /(child)/lessons/{id}?subjectId={sid}. */
  onPress: () => void;
  /** Pre-localized a11y label, e.g. "Continue lesson Compare Bigger and Smaller". */
  accessibilityLabel: string;
  /** Pre-localized a11y hint, e.g. "Opens the lesson player". */
  accessibilityHint?: string;
  /** Eyebrow copy ("Pick up where you left off" or "Replay last lesson"). */
  eyebrowText: string;
  /** CTA label ("Continue" / "Replay" / "متابعة" / "إعادة"). */
  ctaLabel: string;
  /** Boss badge label ("Boss" / "بوس"). */
  bossLabel?: string;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}

export function ContinueCard({
  subjectName,
  subjectKey,
  lessonName,
  unitName,
  skillName,
  isBoss = false,
  nodeState = 1,
  onPress,
  accessibilityLabel,
  accessibilityHint,
  eyebrowText,
  ctaLabel,
  bossLabel = 'Boss',
  direction = 'ltr',
  locale = 'en',
  testID,
}: ContinueCardProps) {
  const isRtl = direction === 'rtl';
  const rowDir = isRtl ? 'row-reverse' : 'row';
  const isAvailable = nodeState !== 2;
  const tint = SUBJECT_TINTS[subjectKey] ?? SUBJECT_TINTS.math;

  /** Chevron glyph flips with direction (design spec §6). */
  const chevron = isRtl ? '←' : '→';
  /** Boss state prefixes the CTA with 🔥 (design spec §3.2). */
  const ctaDisplay = isBoss && isAvailable ? `🔥 ${ctaLabel}` : ctaLabel;

  const eyebrowDisplay =
    skillName
      ? `${skillName} · ${eyebrowText}`
      : eyebrowText;

  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      accessibilityHint={accessibilityHint}
      testID={testID}
    >
      {({ pressed }: { pressed: boolean }) => (
        <Stack
          padding={18}
          borderRadius="$modal"
          backgroundColor="$card"
          borderWidth={isAvailable ? 2 : 1}
          borderColor={isAvailable ? '$primary' : '$borderStrong'}
          // Shadow resolved to raw rgba — $primaryGlow / soft shadow (design spec §4.4)
          shadowColor={isAvailable ? 'rgba(99,102,241,0.45)' : 'rgba(0,0,0,0.15)'}
          shadowOffset={{ width: 0, height: isAvailable ? 8 : 4 }}
          shadowOpacity={1}
          shadowRadius={isAvailable ? 24 : 12}
          position="relative"
          style={{ transform: [{ scale: pressed ? 0.95 : 1 }] }}
          minHeight={80}
        >
          {/* Boss badge — top-trailing corner, logical positioning */}
          {isBoss ? (
            <Stack
              position="absolute"
              top={14}
              end={14}
              zIndex={1}
            >
              <Badge
                variant="boss"
                label={bossLabel}
                accessibilityLabel={bossLabel}
              />
            </Stack>
          ) : null}

          {/* Main row */}
          <XStack
            flexDirection={rowDir}
            alignItems="center"
            gap={14}
          >
            {/* Subject icon tile — logical leading edge */}
            <Stack
              width={52}
              height={52}
              borderRadius="$button"
              backgroundColor={tint.soft as never}
              alignItems="center"
              justifyContent="center"
              flexShrink={0}
            >
              <Text
                fontSize={26}
                accessibilityElementsHidden
                importantForAccessibility="no-hide-descendants"
              >
                {tint.glyph}
              </Text>
            </Stack>

            {/* Body column */}
            <YStack flex={1} gap={4}>
              {/* Eyebrow */}
              <Text
                fontSize={11}
                fontWeight="700"
                textTransform="uppercase"
                letterSpacing={0.44}
                color={isAvailable ? '$primaryLight' : '$fg3'}
                fontFamily="$heading"
                writingDirection={direction}
                numberOfLines={1}
              >
                {eyebrowDisplay}
              </Text>

              {/* Lesson title */}
              <Text
                fontSize={16}
                fontWeight="800"
                fontFamily="$heading"
                color="$fg1"
                lineHeight={19}
                writingDirection={direction}
                numberOfLines={2}
                marginTop={4}
              >
                {lessonName}
              </Text>

              {/* Unit caption */}
              {unitName ? (
                <Text
                  fontSize={12}
                  fontWeight="500"
                  fontFamily="$body"
                  color="$fg3"
                  writingDirection={direction}
                  numberOfLines={1}
                  marginTop={2}
                >
                  {unitName}
                </Text>
              ) : null}
            </YStack>

            {/* CTA chip — logical trailing edge */}
            <Stack
              paddingHorizontal={14}
              paddingVertical={8}
              borderRadius="$pill"
              minHeight={36}
              justifyContent="center"
              alignItems="center"
              backgroundColor={isAvailable ? '$primary' : 'transparent'}
              borderWidth={isAvailable ? 0 : 1.5}
              borderColor={isAvailable ? 'transparent' : '$borderStrong'}
              flexShrink={0}
            >
              <Text
                fontSize={13}
                fontWeight="700"
                fontFamily="$heading"
                color={isAvailable ? '$fg1' : '$fg2'}
                writingDirection={direction}
              >
                {`${ctaDisplay} ${chevron}`}
              </Text>
            </Stack>
          </XStack>

          {/* Subject name context below main row (not in spec but aids SR) — hidden */}
          <Stack
            accessibilityElementsHidden
            importantForAccessibility="no-hide-descendants"
            height={0}
            overflow="hidden"
          >
            <Text>{subjectName}</Text>
          </Stack>
        </Stack>
      )}
    </Pressable>
  );
}

ContinueCard.displayName = 'ContinueCard';
