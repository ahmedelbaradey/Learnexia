/**
 * TextField / FormField — the labelled text input for all auth & onboarding
 * forms. Design Spec §2.1.
 *
 * Universal (Expo + RN Web). Built on RN's `TextInput` (RN Web maps it to a DOM
 * input) wrapped in token-driven Tamagui stacks. Token-only styling, logical
 * RTL props, password visibility toggle, inline error slot.
 *
 * `inputRadius = 14` is a LOCAL constant per Design Spec Gap 9 (it falls between
 * `$sm` 8px and `$card` 20px and is single-use — no new global token).
 *
 * A11y: label drives `accessibilityLabel`; error string is announced via
 * `accessibilityLiveRegion="polite"`; the password toggle is a 48px button.
 */
import { directionForLocale, type Direction } from '@learnexia/shared/i18n';
import { Stack } from '@tamagui/core';
import React, { useState } from 'react';
import {
  TextInput,
  type KeyboardTypeOptions,
  type TextInputProps,
} from 'react-native';

import { XStack, YStack, Text } from '../../internal/primitives';

const inputRadius = 14;

export interface TextFieldProps {
  label: string;
  value: string;
  onChangeText: (text: string) => void;
  placeholder?: string;
  error?: string;
  secureTextEntry?: boolean;
  keyboardType?: KeyboardTypeOptions;
  autoCapitalize?: TextInputProps['autoCapitalize'];
  autoComplete?: TextInputProps['autoComplete'];
  disabled?: boolean;
  testID?: string;
  accessibilityLabel?: string;
  /** Layout direction; pass `useDirection(locale)` result, or a locale string. */
  direction?: Direction;
  locale?: string;
}

/**
 * Resolve the active direction. Prefer an explicit `direction`, then a `locale`,
 * defaulting to LTR. Components remain context-free (callers own the locale).
 */
function resolveDirection(direction?: Direction, locale?: string): Direction {
  if (direction) return direction;
  if (locale) return directionForLocale(locale);
  return 'ltr';
}

export function TextField({
  label,
  value,
  onChangeText,
  placeholder,
  error,
  secureTextEntry = false,
  keyboardType = 'default',
  autoCapitalize = 'none',
  autoComplete,
  disabled = false,
  testID,
  accessibilityLabel,
  direction,
  locale,
}: TextFieldProps) {
  const dir = resolveDirection(direction, locale);
  const [focused, setFocused] = useState(false);
  const [revealed, setRevealed] = useState(false);

  const hasError = Boolean(error);
  const filled = value.length > 0;

  // Border / shadow per state (Design Spec §2.1 States table).
  const borderColor = hasError
    ? '$danger'
    : focused
      ? '$borderFocus'
      : filled
        ? '$borderStrong'
        : '$border';

  const glowColor = hasError
    ? 'rgba(239,68,68,0.20)'
    : focused
      ? 'rgba(99,102,241,0.25)'
      : 'transparent';

  return (
    <YStack gap="$1" opacity={disabled ? 0.5 : 1} pointerEvents={disabled ? 'none' : 'auto'}>
      <Text
        color="$fg3"
        fontSize={12}
        fontWeight="600"
        fontFamily="$heading"
        textTransform="uppercase"
        letterSpacing={0.6}
        textAlign="left"
        writingDirection={dir}
      >
        {label}
      </Text>

      <Stack
        position="relative"
        height={52}
        borderRadius={inputRadius}
        borderWidth={1}
        borderColor={borderColor}
        backgroundColor={disabled ? '$bgElevated' : '$card'}
        shadowColor={glowColor}
        shadowOpacity={focused || hasError ? 1 : 0}
        shadowRadius={4}
        shadowOffset={{ width: 0, height: 0 }}
        justifyContent="center"
      >
        <TextInput
          testID={testID}
          value={value}
          onChangeText={onChangeText}
          placeholder={placeholder}
          placeholderTextColor="#94A3B8"
          secureTextEntry={secureTextEntry && !revealed}
          keyboardType={keyboardType}
          autoCapitalize={autoCapitalize}
          autoComplete={autoComplete}
          editable={!disabled}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          accessibilityLabel={accessibilityLabel ?? label}
          style={{
            height: 52,
            paddingStart: 14,
            paddingEnd: secureTextEntry ? 48 : 14,
            color: '#F8FAFC',
            fontSize: 15,
            textAlign: dir === 'rtl' ? 'right' : 'left',
            writingDirection: dir,
          }}
        />

        {secureTextEntry ? (
          <Stack
            position="absolute"
            end={4}
            top={0}
            bottom={0}
            width={48}
            alignItems="center"
            justifyContent="center"
            cursor="pointer"
            hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
            onPress={() => setRevealed((r) => !r)}
            accessibilityRole="button"
            accessible
            accessibilityLabel={revealed ? 'Hide password' : 'Show password'}
            aria-label={revealed ? 'Hide password' : 'Show password'}
          >
            <Text fontSize={18} color="$fg3" accessibilityElementsHidden>
              {revealed ? '🙈' : '👁'}
            </Text>
          </Stack>
        ) : null}
      </Stack>

      {hasError ? (
        <XStack
          gap="$1"
          marginTop={2}
          accessibilityLiveRegion="polite"
          flexDirection={dir === 'rtl' ? 'row-reverse' : 'row'}
        >
          <Text color="$danger" fontSize={12} fontFamily="$body" textAlign="left" writingDirection={dir}>
            {error}
          </Text>
        </XStack>
      ) : null}
    </YStack>
  );
}

TextField.displayName = 'TextField';

/** Alias — the Design Spec uses both names interchangeably. */
export const FormField = TextField;
