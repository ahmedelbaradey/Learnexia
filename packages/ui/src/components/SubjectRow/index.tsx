/**
 * SubjectRow — student-facing subject list card.
 *
 * Design Spec §3.1. Composed from `preview/mobile-subject-rows.html`.
 * 52×52 icon tile + localized subject name + mastery caption + thin mastery bar
 * + trailing chevron. Dark-default, token-only, logical RTL flex.
 *
 * Subject tint map (design spec §3.1):
 *   math    → $primary / $primarySoft  + 🧮
 *   science → $success / $successSoft  + 🧪
 *   arabic  → $accent  / $warningSoft  + 📖
 *   english → $purple  / $purpleSoft   + 🔤
 *
 * Motion:
 *   hover (web): brighten 8% (bg → $cardSoft) + scale 1.02, 160ms $ease-out.
 *   press: scale 0.95, 80ms.
 *   focus: $focus-ring 2px $primary + 4px $primaryGlow.
 *
 * A11y: accessibilityRole="button", required accessibilityLabel (caller provides).
 */
import React from 'react';
import { Stack, type StackProps } from '@tamagui/core';

import { YStack, XStack, Text } from '../../internal/primitives';

export type SubjectKey = 'math' | 'science' | 'arabic' | 'english';

export interface SubjectRowProps {
  /** Stable id from the API (used as React key by caller; not rendered). */
  subjectId: number;
  /** Localized display name. */
  name: string;
  /** 0..100; omit to hide the bar+caption block. */
  masteryPercent?: number;
  /** Required — determines the icon tile color tint and glyph. */
  subjectKey: SubjectKey;
  /** Tap handler (always pressable). */
  onPress: () => void;
  /** Already-localized a11y label, e.g. "Math, 45 percent mastered". */
  accessibilityLabel: string;
  /** RTL direction from `useLocale().direction`. */
  direction?: 'ltr' | 'rtl';
  /** For the loading skeleton — render the chrome only, no content. */
  loading?: boolean;
  testID?: string;
}

const SUBJECT_TINT: Record<
  SubjectKey,
  { fg: StackProps['backgroundColor']; soft: StackProps['backgroundColor']; glyph: string }
> = {
  math:    { fg: '$primary', soft: '$primarySoft',  glyph: '🧮' },
  science: { fg: '$success', soft: '$successSoft',  glyph: '🧪' },
  arabic:  { fg: '$accent',  soft: '$warningSoft',  glyph: '📖' },
  english: { fg: '$purple',  soft: '$purpleSoft',   glyph: '🔤' },
};

export function SubjectRow({
  name,
  masteryPercent,
  subjectKey,
  onPress,
  accessibilityLabel,
  direction = 'ltr',
  loading = false,
  testID,
}: SubjectRowProps) {
  const isRtl = direction === 'rtl';
  const rowDir = isRtl ? 'row-reverse' : 'row';
  const tint = SUBJECT_TINT[subjectKey];
  const hasMastery = masteryPercent !== undefined;
  const pct = Math.max(0, Math.min(100, masteryPercent ?? 0));
  const chevron = isRtl ? '‹' : '›';

  if (loading) {
    return (
      <Stack
        testID={testID}
        width="100%"
        height={84}
        borderRadius="$card"
        backgroundColor="$cardSoft"
        borderWidth={1}
        borderColor="$border"
      />
    );
  }

  return (
    <XStack
      testID={testID}
      flexDirection={rowDir}
      alignItems="center"
      gap="$3"
      padding="$4"
      borderRadius="$card"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$border"
      shadowColor="rgba(0,0,0,0.15)"
      shadowRadius={12}
      shadowOffset={{ width: 0, height: 4 }}
      shadowOpacity={1}
      hoverStyle={{ backgroundColor: '$cardSoft', scale: 1.02 }}
      pressStyle={{ scale: 0.95 }}
      cursor="pointer"
      onPress={onPress}
      accessibilityRole="button"
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {/* Icon tile */}
      <Stack
        width={52}
        height={52}
        borderRadius="$button"
        backgroundColor={tint.soft}
        alignItems="center"
        justifyContent="center"
        flexShrink={0}
      >
        <Text fontSize={26} accessibilityElementsHidden>
          {tint.glyph}
        </Text>
      </Stack>

      {/* Body */}
      <YStack flex={1} gap={hasMastery ? 4 : 0}>
        <Text
          color="$fg1"
          fontSize={16}
          fontWeight="800"
          fontFamily="$heading"
          lineHeight={18}
          writingDirection={direction}
        >
          {name}
        </Text>

        {hasMastery ? (
          <>
            <Text
              color="$fg3"
              fontSize={11}
              fontWeight="500"
              fontFamily="$body"
              marginTop={4}
              writingDirection={direction}
            >
              {`${pct}%`}
            </Text>
            {/* Progress bar — always LTR (brand rule: progress grows L→R) */}
            <Stack
              marginTop={6}
              height={6}
              borderRadius={9999}
              backgroundColor="$bg"
              overflow="hidden"
              flexDirection="row"
            >
              <Stack
                width={`${pct}%` as StackProps['width']}
                height="100%"
                borderRadius={9999}
                backgroundColor={tint.fg}
              />
            </Stack>
          </>
        ) : null}
      </YStack>

      {/* Trailing chevron */}
      <Text
        color="$fg3"
        fontSize={18}
        fontFamily="$body"
        flexShrink={0}
        accessibilityElementsHidden
      >
        {chevron}
      </Text>
    </XStack>
  );
}

SubjectRow.displayName = 'SubjectRow';
