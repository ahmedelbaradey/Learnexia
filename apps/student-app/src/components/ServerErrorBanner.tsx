/**
 * ServerErrorBanner — the localized server-error banner used on form screens
 * (Design Spec: `$dangerSoft` bg, `$danger` text, announced assertively).
 */
import { Stack, Text } from '@tamagui/core';
import type { Direction } from '@learnexia/shared';

export interface ServerErrorBannerProps {
  message: string | null;
  direction?: Direction;
  testID?: string;
}

export function ServerErrorBanner({ message, direction = 'ltr', testID }: ServerErrorBannerProps) {
  if (!message) return null;
  return (
    <Stack
      testID={testID}
      backgroundColor="$dangerSoft"
      borderRadius="$sm"
      padding="$3"
      accessibilityLiveRegion="assertive"
      accessible
      accessibilityLabel={message}
      aria-label={message}
    >
      <Text
        color="$danger"
        fontSize={14}
        fontFamily="$body"
        textAlign={direction === 'rtl' ? 'right' : 'left'}
        writingDirection={direction}
      >
        {message}
      </Text>
    </Stack>
  );
}
