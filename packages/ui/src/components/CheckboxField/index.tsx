/**
 * CheckboxField — the FE-14 Checkbox primitive: a tappable box + inline label
 * for consent / opt-in rows (e.g. the parent Terms gate on Register).
 *
 * Universal (Expo + RN Web), token-only styling, logical RTL props. The box and
 * label form a single 44px+ touch target. When `error` is set the box border
 * turns `$danger` and the message is announced via `accessibilityLiveRegion`.
 *
 * Copy is owned by callers (i18n `t()` results) — the component never hardcodes
 * user-facing text. `label` may be a string or rich nodes (e.g. linked Terms).
 *
 * `boxRadius = 6` is a LOCAL constant (single-use, between `$sm` and the larger
 * input radius) — no new global token.
 */
import { directionForLocale, type Direction } from '@learnexia/shared/i18n';
import { Stack } from '@tamagui/core';
import React, { type ReactNode } from 'react';

import { XStack, YStack, Text } from '../../internal/primitives';

const boxRadius = 6;

export interface CheckboxFieldProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  /** Label content — a string or rich nodes (links). */
  label: ReactNode;
  error?: string;
  disabled?: boolean;
  direction?: Direction;
  locale?: string;
  /** Required for a11y when `label` is rich/non-text. */
  accessibilityLabel?: string;
  testID?: string;
}

function resolveDirection(direction?: Direction, locale?: string): Direction {
  if (direction) return direction;
  if (locale) return directionForLocale(locale);
  return 'ltr';
}

export function CheckboxField({
  checked,
  onChange,
  label,
  error,
  disabled = false,
  direction,
  locale,
  accessibilityLabel,
  testID,
}: CheckboxFieldProps) {
  const dir = resolveDirection(direction, locale);
  const hasError = Boolean(error);
  const rowDir = dir === 'rtl' ? 'row-reverse' : 'row';

  const borderColor = hasError ? '$danger' : checked ? '$primary' : '$borderStrong';

  return (
    <YStack gap="$1" opacity={disabled ? 0.5 : 1} pointerEvents={disabled ? 'none' : 'auto'}>
      <XStack
        testID={testID}
        flexDirection={rowDir}
        alignItems="flex-start"
        gap="$3"
        minHeight={44}
        cursor="pointer"
        onPress={() => onChange(!checked)}
        accessibilityRole="checkbox"
        accessible
        accessibilityState={{ checked, disabled }}
        accessibilityLabel={accessibilityLabel}
        aria-label={accessibilityLabel}
        aria-checked={checked}
        hitSlop={{ top: 6, bottom: 6, left: 6, right: 6 }}
      >
        <Stack
          width={22}
          height={22}
          marginTop={2}
          borderRadius={boxRadius}
          borderWidth={2}
          borderColor={borderColor}
          backgroundColor={checked ? '$primary' : 'transparent'}
          alignItems="center"
          justifyContent="center"
          accessibilityElementsHidden
        >
          {checked ? (
            <Text fontSize={13} fontWeight="800" color="$fg1" accessibilityElementsHidden>
              {'✓'}
            </Text>
          ) : null}
        </Stack>

        {typeof label === 'string' ? (
          <Text
            flex={1}
            color="$fg2"
            fontSize={13}
            lineHeight={19}
            fontFamily="$body"
            textAlign={dir === 'rtl' ? 'right' : 'left'}
            writingDirection={dir}
          >
            {label}
          </Text>
        ) : (
          <Stack flex={1}>{label}</Stack>
        )}
      </XStack>

      {hasError ? (
        <Text
          color="$danger"
          fontSize={12}
          fontFamily="$body"
          marginTop={2}
          textAlign={dir === 'rtl' ? 'right' : 'left'}
          writingDirection={dir}
          accessibilityLiveRegion="polite"
        >
          {error}
        </Text>
      ) : null}
    </YStack>
  );
}

CheckboxField.displayName = 'CheckboxField';
