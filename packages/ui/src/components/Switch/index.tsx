/**
 * Switch — the FE-14 toggle primitive: a 44×24 track + 20px thumb that slides
 * between off (logical start) and on (logical end). The whole row (track + the
 * optional label) is one ≥44px touch target; the entire row is pressable so the
 * label drives the toggle too.
 *
 * Universal (Expo + RN Web), token-only styling, logical RTL props. On web the
 * thumb animates via a CSS `transform`/`transition`; on native the thumb just
 * snaps — no animation lib added. Mirrors `CheckboxField`'s shape (no new
 * pattern); copy is owned by callers via `t()`.
 *
 * Use for: theme toggle (light/dark), notification opt-ins (P2-12 Notifications
 * tab), security opt-ins, any boolean preference. Distinct from CheckboxField
 * (which is for tappable consent rows with rich labels) — Switch is for live
 * stateful preferences that toggle a value immediately.
 *
 * Track 44×24, thumb 20×20 with 2px padding (offset 22px) — local constants,
 * not new global tokens. Colors: on=`$primary`, off=`$cardSoft`; thumb `$fg1`.
 */
import { directionForLocale, type Direction } from '@learnexia/shared/i18n';
import { Stack } from '@tamagui/core';
import React, { type ReactNode } from 'react';

import { Text } from '../../internal/primitives';

const TRACK_WIDTH = 44;
const TRACK_HEIGHT = 24;
const THUMB_SIZE = 20;
const THUMB_OFFSET = TRACK_WIDTH - THUMB_SIZE - 2; // 22 — start padding 2px each side

export interface SwitchProps {
  value: boolean;
  onValueChange: (next: boolean) => void;
  /** Optional inline label (string or rich nodes). */
  label?: ReactNode;
  disabled?: boolean;
  direction?: Direction;
  locale?: string;
  /** Required for a11y when `label` is rich/absent. */
  accessibilityLabel?: string;
  testID?: string;
}

function resolveDirection(direction?: Direction, locale?: string): Direction {
  if (direction) return direction;
  if (locale) return directionForLocale(locale);
  return 'ltr';
}

export function Switch({
  value,
  onValueChange,
  label,
  disabled = false,
  direction,
  locale,
  accessibilityLabel,
  testID,
}: SwitchProps) {
  const dir = resolveDirection(direction, locale);
  const rowDir = dir === 'rtl' ? 'row-reverse' : 'row';

  // Logical: thumb sits at start when off, end when on. In RTL "end" is left.
  const onTranslate = dir === 'rtl' ? -THUMB_OFFSET : THUMB_OFFSET;
  const translateX = value ? onTranslate : 0;

  const handlePress = () => {
    if (!disabled) onValueChange(!value);
  };

  return (
    <Stack
      flexDirection={rowDir}
      alignItems="center"
      gap="$3"
      opacity={disabled ? 0.4 : 1}
      cursor={disabled ? 'not-allowed' : 'pointer'}
      onPress={handlePress}
      pressStyle={disabled ? undefined : { scale: 0.95 }}
      hoverStyle={disabled ? undefined : { opacity: 0.92 }}
      focusStyle={{ outlineColor: '$primary', outlineWidth: 2, outlineStyle: 'solid' }}
      accessibilityRole="switch"
      accessibilityState={{ checked: value, disabled }}
      accessibilityLabel={
        accessibilityLabel ?? (typeof label === 'string' ? label : undefined)
      }
      testID={testID}
      // Ensure the row keeps a ≥44px touch target even when no label is shown.
      minHeight={44}
      paddingVertical={6}
    >
      <Stack
        width={TRACK_WIDTH}
        height={TRACK_HEIGHT}
        borderRadius={9999}
        backgroundColor={value ? '$primary' : '$cardSoft'}
        justifyContent="center"
        padding={2}
        // Soft web transition for the track color; native ignores.
        style={{
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          transitionProperty: 'background-color' as any,
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          transitionDuration: '160ms' as any,
        }}
      >
        <Stack
          width={THUMB_SIZE}
          height={THUMB_SIZE}
          borderRadius={9999}
          backgroundColor="$fg1"
          style={{
            transform: [{ translateX }],
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            transitionProperty: 'transform' as any,
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            transitionDuration: '160ms' as any,
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            transitionTimingFunction: 'cubic-bezier(0.16,1,0.3,1)' as any,
          }}
        />
      </Stack>

      {label != null ? (
        typeof label === 'string' ? (
          <Text color="$fg1" fontFamily="$body" fontSize={14} fontWeight="500">
            {label}
          </Text>
        ) : (
          label
        )
      ) : null}
    </Stack>
  );
}

Switch.displayName = 'Switch';
