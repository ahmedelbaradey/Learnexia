/**
 * MatchingPanel — STUB for Matching question type (W12).
 *
 * BE has zero Matching questions seeded (Pipeline Brief R4). This renders a
 * muted "coming soon" tile inside the QuestionCard. The QuestionCard's Submit
 * CTA becomes "Next" and the answer payload is `""` (empty string).
 *
 * The real drag-pair UI will be built when content authoring seeds matching
 * questions. This stub is defensive only.
 *
 * Design Spec §3.6 + §2.4.
 */
import React from 'react';

import { YStack, Text } from '../../internal/primitives';

export interface MatchingPanelProps {
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** Title copy — already localized ("Matching questions coming soon"). */
  title?: string;
  /** Sub copy — already localized ("Tap Next to skip"). */
  subTitle?: string;
  testID?: string;
}

export function MatchingPanel({
  direction = 'ltr',
  title = 'Matching questions coming soon',
  subTitle = 'Tap Next to skip',
  testID,
}: MatchingPanelProps) {
  const isRtl = direction === 'rtl'; // eslint-disable-line @typescript-eslint/no-unused-vars

  return (
    <YStack
      padding={24}
      gap={8}
      borderRadius={14}
      borderWidth={1}
      borderStyle="dashed"
      borderColor="$borderStrong"
      backgroundColor="$cardSoft"
      alignItems="center"
      accessible
      accessibilityRole="text"
      accessibilityLabel={`${title}. ${subTitle}`}
      testID={testID}
    >
      <Text
        fontSize={32}
        accessibilityElementsHidden
      >
        {'🧩'}
      </Text>
      <Text
        fontSize={16}
        fontWeight="700"
        color="$fg2"
        fontFamily="$heading"
        textAlign="center"
        writingDirection={direction}
      >
        {title}
      </Text>
      <Text
        fontSize={12}
        fontWeight="500"
        color="$fg3"
        fontFamily="$body"
        textAlign="center"
        writingDirection={direction}
      >
        {subTitle}
      </Text>
    </YStack>
  );
}

MatchingPanel.displayName = 'MatchingPanel';
