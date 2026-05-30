/**
 * TrueFalseChoice — pair of large True/False buttons.
 *
 * State chrome mirrors MCQOption: default/selected/correct/incorrect via tokens.
 * Each side is 50% width, height 88. RTL: row reverses (True still on reading-
 * leading edge). `accessibilityRole="radiogroup"` on the container.
 *
 * Answer payload: `boolean | null`. At submit the caller converts `true`→`"true"`
 * / `false`→`"false"` (per Design Spec §11 item 5).
 *
 * Design Spec §3.3 + §2.2.
 */
import React from 'react';
import { Pressable } from 'react-native';
import { Stack } from '@tamagui/core';

import { XStack, Text } from '../../internal/primitives';

export interface TrueFalseChoiceProps {
  /** Selected value or null if untouched. */
  value: boolean | null;
  /** Change handler — fires on tap during `answering` phase. */
  onChange: (next: boolean) => void;
  /** Overall phase drives whether options are interactive. */
  phase: 'answering' | 'feedback';
  /** When phase==='feedback', the correct answer to highlight green. */
  correctAnswer?: boolean | null;
  /** When phase==='feedback', the user's actual pick (used to mark the wrong choice red). */
  selectedAnswer?: boolean | null;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** Already-localized labels — defaults to "True" / "False". */
  trueLabel?: string;
  falseLabel?: string;
  testID?: string;
}

type SideState = 'default' | 'selected' | 'correct' | 'incorrect' | 'locked-default';

function resolveSideState(
  sideValue: boolean,
  phase: 'answering' | 'feedback',
  currentValue: boolean | null,
  correctAnswer?: boolean | null,
  selectedAnswer?: boolean | null,
): SideState {
  if (phase === 'answering') {
    return currentValue === sideValue ? 'selected' : 'default';
  }
  // feedback phase:
  const isCorrect = correctAnswer === sideValue;
  const wasSelected = selectedAnswer === sideValue;
  if (isCorrect) return 'correct';
  if (wasSelected) return 'incorrect';
  return 'locked-default';
}

const BG: Record<SideState, string> = {
  default: '$card',
  selected: '$primarySoft',
  correct: '$successSoft',
  incorrect: '$dangerSoft',
  'locked-default': '$card',
};

const BORDER_COLOR: Record<SideState, string> = {
  default: '$border',
  selected: '$primary',
  correct: '$success',
  incorrect: '$danger',
  'locked-default': '$border',
};

const BORDER_WIDTH: Record<SideState, number> = {
  default: 1,
  selected: 2,
  correct: 2,
  incorrect: 2,
  'locked-default': 1,
};

const GLYPH_COLOR: Record<SideState, string> = {
  default: '$fg3',
  selected: '$primary',
  correct: '$success',
  incorrect: '$danger',
  'locked-default': '$fg4',
};

const LABEL_COLOR: Record<SideState, string> = {
  default: '$fg1',
  selected: '$fg1',
  correct: '$fg1',
  incorrect: '$fg1',
  'locked-default': '$fg3',
};

interface SideProps {
  sideValue: boolean;
  sideState: SideState;
  label: string;
  glyph: string;
  onPress: () => void;
  locked: boolean;
  direction: 'ltr' | 'rtl';
  accessibilityLabel: string;
}

function Side({ sideValue, sideState, label, glyph, onPress, locked, direction, accessibilityLabel }: SideProps) {
  const checked = sideState === 'selected' || sideState === 'correct' || sideState === 'incorrect';
  const inner = (
    <Stack
      flex={1}
      height={88}
      backgroundColor={BG[sideState] as any}
      borderWidth={BORDER_WIDTH[sideState]}
      borderColor={BORDER_COLOR[sideState] as any}
      borderRadius="$button"
      alignItems="center"
      justifyContent="center"
      gap={8}
      padding={16}
      pointerEvents={locked ? 'none' : 'auto'}
    >
      <Text fontSize={28} color={GLYPH_COLOR[sideState] as any} accessibilityElementsHidden>
        {glyph}
      </Text>
      <Text
        fontSize={16}
        fontWeight="800"
        fontFamily="$heading"
        color={LABEL_COLOR[sideState] as any}
        textAlign="center"
        writingDirection={direction}
      >
        {label}
      </Text>
    </Stack>
  );

  if (locked) {
    return (
      <Stack
        flex={1}
        accessible
        accessibilityRole="radio"
        accessibilityState={{ checked, disabled: true }}
        accessibilityLabel={accessibilityLabel}
        aria-label={accessibilityLabel}
        aria-checked={checked}
        aria-disabled
      >
        {inner}
      </Stack>
    );
  }

  return (
    <Pressable
      style={{ flex: 1 }}
      onPress={onPress}
      accessible
      accessibilityRole="radio"
      accessibilityState={{ checked, disabled: false }}
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
      aria-checked={checked}
    >
      {inner}
    </Pressable>
  );
}

export function TrueFalseChoice({
  value,
  onChange,
  phase,
  correctAnswer,
  selectedAnswer,
  direction = 'ltr',
  trueLabel = 'True',
  falseLabel = 'False',
  testID,
}: TrueFalseChoiceProps) {
  const isRtl = direction === 'rtl';
  const locked = phase === 'feedback';

  const trueState = resolveSideState(true, phase, value, correctAnswer, selectedAnswer);
  const falseState = resolveSideState(false, phase, value, correctAnswer, selectedAnswer);

  return (
    <XStack
      flexDirection={isRtl ? 'row-reverse' : 'row'}
      gap={12}
      accessible
      accessibilityRole="radiogroup"
      testID={testID}
    >
      <Side
        sideValue={true}
        sideState={trueState}
        label={trueLabel}
        glyph="✓"
        onPress={() => onChange(true)}
        locked={locked}
        direction={direction}
        accessibilityLabel={`${trueLabel}, ${trueState === 'selected' ? 'selected' : trueState === 'correct' ? 'correct answer' : trueState === 'incorrect' ? 'incorrect' : ''}`}
      />
      <Side
        sideValue={false}
        sideState={falseState}
        label={falseLabel}
        glyph="✕"
        onPress={() => onChange(false)}
        locked={locked}
        direction={direction}
        accessibilityLabel={`${falseLabel}, ${falseState === 'selected' ? 'selected' : falseState === 'correct' ? 'correct answer' : falseState === 'incorrect' ? 'incorrect' : ''}`}
      />
    </XStack>
  );
}

TrueFalseChoice.displayName = 'TrueFalseChoice';
