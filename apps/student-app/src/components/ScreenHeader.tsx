/**
 * ScreenHeader — back button (RTL-flipped chevron) + optional title.
 * Min 48px touch target. Design Spec screen-header pattern.
 */
import { Stack, Text } from '@tamagui/core';

import { useLocale } from '../hooks/useLocale';

export interface ScreenHeaderProps {
  title?: string;
  onBack?: () => void;
  backLabel?: string;
}

export function ScreenHeader({ title, onBack, backLabel = 'Back' }: ScreenHeaderProps) {
  const { isRtl } = useLocale();
  return (
    <Stack
      height={56}
      paddingHorizontal="$6"
      alignItems="center"
      flexDirection={isRtl ? 'row-reverse' : 'row'}
      gap="$3"
    >
      {onBack ? (
        <Stack
          minWidth={48}
          minHeight={48}
          alignItems="center"
          justifyContent="center"
          cursor="pointer"
          pressStyle={{ scale: 0.92 }}
          onPress={onBack}
          accessibilityRole="button"
          accessible
          accessibilityLabel={backLabel}
          aria-label={backLabel}
        >
          <Text fontSize={22} color="$fg3" scaleX={isRtl ? -1 : 1} accessibilityElementsHidden>
            {'‹'}
          </Text>
        </Stack>
      ) : null}
      {title ? (
        <Text color="$fg1" fontSize={18} fontWeight="700" fontFamily="$heading" flex={1} textAlign="left">
          {title}
        </Text>
      ) : null}
    </Stack>
  );
}
