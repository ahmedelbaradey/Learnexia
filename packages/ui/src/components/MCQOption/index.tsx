/**
 * MCQOption — one tappable option row in a Multiple Choice Question.
 *
 * 5 states: default / selected / correct / incorrect / locked-default.
 * Chrome (background, border, glyph) driven entirely by the `state` prop and
 * design tokens — no raw hex. Min height 56 (≥48 a11y floor).
 *
 * Locked states (correct/incorrect/locked-default) are non-interactive:
 * `pointerEvents: 'none'` — but remain focusable for screen-readers so they
 * can announce the outcome ("Option B, incorrect").
 *
 * A11y: `accessibilityRole="radio"` + `accessibilityState={{ checked, disabled }}`.
 * Design Spec §3.2.
 */
import React from 'react';
import { Pressable } from 'react-native';
import { Stack } from '@tamagui/core';

import { XStack, Text } from '../../internal/primitives';

export type MCQOptionState = 'default' | 'selected' | 'correct' | 'incorrect' | 'locked-default';

export interface MCQOptionProps {
  /** Already-localized option label. */
  label: string;
  /** Option state — drives chrome. */
  state: MCQOptionState;
  /** Press handler. Locked states are non-interactive. */
  onPress?: () => void;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** Already-localized a11y label, e.g. "Option B, 4, selected". */
  accessibilityLabel: string;
  testID?: string;
}

const STATE_BG: Record<MCQOptionState, string> = {
  default: '$card',
  selected: '$primarySoft',
  correct: '$successSoft',
  incorrect: '$dangerSoft',
  'locked-default': '$card',
};

const STATE_BORDER_COLOR: Record<MCQOptionState, string> = {
  default: '$border',
  selected: '$primary',
  correct: '$success',
  incorrect: '$danger',
  'locked-default': '$border',
};

const STATE_BORDER_WIDTH: Record<MCQOptionState, number> = {
  default: 1,
  selected: 2,
  correct: 2,
  incorrect: 2,
  'locked-default': 1,
};

const STATE_LABEL_COLOR: Record<MCQOptionState, string> = {
  default: '$fg2',
  selected: '$fg1',
  correct: '$fg1',
  incorrect: '$fg1',
  'locked-default': '$fg3',
};

const STATE_LABEL_WEIGHT: Record<MCQOptionState, '600' | '700'> = {
  default: '600',
  selected: '700',
  correct: '700',
  incorrect: '700',
  'locked-default': '600',
};

const isLocked = (state: MCQOptionState) =>
  state === 'correct' || state === 'incorrect' || state === 'locked-default';

const isChecked = (state: MCQOptionState) =>
  state === 'selected' || state === 'correct' || state === 'incorrect';

export function MCQOption({
  label,
  state,
  onPress,
  direction = 'ltr',
  accessibilityLabel,
  testID,
}: MCQOptionProps) {
  const locked = isLocked(state);
  const checked = isChecked(state);
  const isRtl = direction === 'rtl';

  const radioDisc = (
    <Stack
      width={20}
      height={20}
      borderRadius={9999}
      borderWidth={state === 'selected' ? 2 : 2}
      borderColor={state === 'selected' ? '$primary' : state === 'correct' ? '$success' : state === 'incorrect' ? '$danger' : '$borderStrong'}
      backgroundColor={state === 'selected' ? '$primarySoft' : 'transparent'}
      alignItems="center"
      justifyContent="center"
      accessibilityElementsHidden
    >
      {state === 'selected' ? (
        <Stack width={10} height={10} borderRadius={9999} backgroundColor="$primary" />
      ) : null}
    </Stack>
  );

  const trailingGlyph =
    state === 'correct' ? (
      <Text fontSize={18} color="$success" accessibilityElementsHidden>
        {'✓'}
      </Text>
    ) : state === 'incorrect' ? (
      <Text fontSize={18} color="$danger" accessibilityElementsHidden>
        {'✕'}
      </Text>
    ) : null;

  const content = (
    <Stack
      backgroundColor={STATE_BG[state] as any}
      borderWidth={STATE_BORDER_WIDTH[state]}
      borderColor={STATE_BORDER_COLOR[state] as any}
      borderRadius="$button"
      paddingVertical={14}
      paddingHorizontal={16}
      minHeight={56}
      justifyContent="center"
      // Locked states: still reachable by keyboard/SR but not tappable.
      pointerEvents={locked ? 'none' : 'auto'}
      testID={testID}
    >
      <XStack
        flexDirection={isRtl ? 'row-reverse' : 'row'}
        alignItems="center"
        gap={12}
      >
        {radioDisc}
        <Text
          flex={1}
          fontSize={16}
          fontWeight={STATE_LABEL_WEIGHT[state]}
          color={STATE_LABEL_COLOR[state] as any}
          fontFamily="$heading"
          numberOfLines={2}
          textAlign={isRtl ? 'right' : 'left'}
          writingDirection={direction}
        >
          {label}
        </Text>
        {trailingGlyph}
      </XStack>
    </Stack>
  );

  if (locked) {
    // Locked: render as accessible non-interactive container.
    return (
      <Stack
        accessible
        accessibilityRole="radio"
        accessibilityState={{ checked, disabled: true }}
        accessibilityLabel={accessibilityLabel}
        aria-label={accessibilityLabel}
        aria-checked={checked}
        aria-disabled
      >
        {content}
      </Stack>
    );
  }

  return (
    <Pressable
      onPress={onPress}
      accessible
      accessibilityRole="radio"
      accessibilityState={{ checked, disabled: false }}
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
      aria-checked={checked}
    >
      {content}
    </Pressable>
  );
}

MCQOption.displayName = 'MCQOption';
