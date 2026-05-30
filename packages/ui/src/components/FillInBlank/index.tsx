/**
 * FillInBlank — text input wrapper for FillInBlank quiz questions.
 *
 * Wraps the existing TextField primitive with appropriate quiz-state chrome:
 * - default: standard input styling.
 * - correct: $successSoft bg, $success border (non-editable).
 * - incorrect: $dangerSoft bg, $danger border (non-editable).
 *
 * The Submit CTA lives in the QuestionCard footer, NOT in this component.
 * `onSubmitEditing` lets the keyboard "Done" key trigger Submit when available.
 *
 * A11y: `accessibilityRole="text"`, `accessibilityState={{ disabled: locked }}`.
 * Design Spec §3.4.
 */
import React from 'react';
import { TextInput } from 'react-native';
import { colors } from '@learnexia/design-system';
import { View } from 'react-native';

import { YStack } from '../../internal/primitives';

export type FillInBlankState = 'default' | 'focused' | 'correct' | 'incorrect';

export interface FillInBlankProps {
  value: string;
  onChange: (next: string) => void;
  /** Drives chrome — see design spec §2.3 state table. */
  state?: FillInBlankState;
  /** Locked once feedback shows — non-editable + non-focusable. */
  locked?: boolean;
  /** Optional placeholder override; defaults to 'Type your answer' / 'اكتب إجابتك'. */
  placeholder?: string;
  /** Optional submit trigger from keyboard "Done" key. */
  onSubmitEditing?: () => void;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}

const BG_COLOR: Record<FillInBlankState, string> = {
  default: colors.bg,
  focused: colors.bg,
  correct: colors.successSoft,
  incorrect: colors.dangerSoft,
};

const BORDER_COLOR: Record<FillInBlankState, string> = {
  default: colors.border,
  focused: colors.primary,
  correct: colors.success,
  incorrect: colors.danger,
};

const BORDER_WIDTH: Record<FillInBlankState, number> = {
  default: 1,
  focused: 1.5,
  correct: 1.5,
  incorrect: 1.5,
};

export function FillInBlank({
  value,
  onChange,
  state = 'default',
  locked = false,
  placeholder,
  onSubmitEditing,
  direction = 'ltr',
  locale = 'ar',
  testID,
}: FillInBlankProps) {
  const isRtl = direction === 'rtl';
  const textAlign = isRtl ? 'right' : 'left';
  const writingDir = direction;

  return (
    <YStack gap={0}>
      <View
        style={{
          minHeight: 56,
          borderRadius: 14,
          borderWidth: BORDER_WIDTH[state],
          borderColor: BORDER_COLOR[state],
          backgroundColor: BG_COLOR[state],
          justifyContent: 'center',
          overflow: 'hidden',
        }}
      >
        <TextInput
          testID={testID}
          value={value}
          onChangeText={onChange}
          placeholder={placeholder}
          placeholderTextColor={colors.fg4}
          editable={!locked}
          autoCorrect={false}
          autoCapitalize="none"
          returnKeyType="done"
          onSubmitEditing={locked ? undefined : onSubmitEditing}
          accessibilityLabel={locale === 'ar' ? 'حقل الإجابة' : 'Answer field'}
          accessibilityRole="text"
          accessible
          accessibilityState={{ disabled: locked }}
          aria-label={locale === 'ar' ? 'حقل الإجابة' : 'Answer field'}
          aria-disabled={locked}
          style={{
            minHeight: 56,
            paddingHorizontal: 14,
            paddingVertical: 12,
            color: colors.fg1,
            fontSize: 16,
            fontFamily: isRtl ? 'Tajawal' : 'Poppins',
            textAlign,
            writingDirection: writingDir,
          }}
        />
      </View>
    </YStack>
  );
}

FillInBlank.displayName = 'FillInBlank';
