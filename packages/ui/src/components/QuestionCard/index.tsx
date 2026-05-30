/**
 * QuestionCard — wrapper card for all question-type renderers.
 *
 * Layout:
 *   - Question stem (top).
 *   - Children slot (type-specific renderer: MCQ / TrueFalse / FillInBlank / Matching).
 *   - AnswerFeedbackStrip slot (inline — not below the card).
 *   - Optional network-error strip (above footer).
 *   - Footer row: hint (leading) + submit (trailing).
 *
 * Pure layout — no state. Caller manages all state and passes in pre-configured
 * slot nodes. RTL via `direction` prop.
 *
 * A11y: `accessibilityRole="group"`. Children carry their own roles.
 * Design Spec §3.1.
 */
import React from 'react';
import { Stack } from '@tamagui/core';

import { XStack, YStack, Text } from '../../internal/primitives';

export interface QuestionCardProps {
  /** The question stem text, already localized. */
  questionText: string;
  /** Slot for the type-specific renderer. */
  children: React.ReactNode;
  /** Slot for the AnswerFeedbackStrip — rendered between content and footer. */
  feedback?: React.ReactNode;
  /** The Submit / Next CTA (already configured by the parent screen). */
  submitButton?: React.ReactNode;
  /** Hint affordance (disabled this wave). */
  hintButton?: React.ReactNode;
  /** Inline network-error message — null/undefined hides it. */
  errorMessage?: string | null;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  testID?: string;
}

export function QuestionCard({
  questionText,
  children,
  feedback,
  submitButton,
  hintButton,
  errorMessage,
  direction = 'ltr',
  testID,
}: QuestionCardProps) {
  const isRtl = direction === 'rtl';

  return (
    <YStack
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$border"
      borderRadius={24}
      padding={20}
      gap={16}
      shadowColor="rgba(0,0,0,0.15)"
      shadowOpacity={1}
      shadowRadius={12}
      shadowOffset={{ width: 0, height: 4 }}
      accessible={false}
      testID={testID}
    >
      {/* Question stem */}
      <Text
        fontSize={18}
        fontWeight="700"
        fontFamily="$heading"
        color="$fg1"
        numberOfLines={0}
        writingDirection={direction}
        textAlign={isRtl ? 'right' : 'left'}
        lineHeight={25}
      >
        {questionText}
      </Text>

      {/* Type-specific renderer */}
      {children}

      {/* AnswerFeedbackStrip slot */}
      {feedback ? feedback : null}

      {/* Network-error strip */}
      {errorMessage ? (
        <Stack
          paddingVertical={10}
          paddingHorizontal={12}
          borderRadius={14}
          borderWidth={1}
          borderColor="$danger"
          backgroundColor="$dangerSoft"
        >
          <Text
            fontSize={12}
            fontWeight="600"
            color="$danger"
            fontFamily="$body"
            writingDirection={direction}
            textAlign={isRtl ? 'right' : 'left'}
          >
            {errorMessage}
          </Text>
        </Stack>
      ) : null}

      {/* Footer row: hint (leading) + submit (trailing) */}
      <XStack
        flexDirection={isRtl ? 'row-reverse' : 'row'}
        justifyContent="space-between"
        alignItems="center"
        gap={12}
      >
        {hintButton ? hintButton : <Stack />}
        {submitButton ? submitButton : null}
      </XStack>
    </YStack>
  );
}

QuestionCard.displayName = 'QuestionCard';
