/**
 * TextField / FormField — the labelled text input for all auth & onboarding
 * forms. Design Spec §2.1.
 *
 * Universal (Expo + RN Web). Built on RN's `TextInput` (RN Web maps it to a DOM
 * input) wrapped in token-driven Tamagui stacks. Token-only styling, logical
 * RTL props, password visibility toggle, inline error slot. Input height 48px,
 * resting border `$borderInput` (0.10). `forceValueLtr` / `forceLtr` keeps
 * email/phone values LTR. Also auto-forces LTR when `keyboardType` is
 * `'email-address'` or `autoComplete` is `'email'` / `'tel'` (SKILL.md rule).
 *
 * `inputRadius = 14` is a LOCAL constant per Design Spec Gap 9 (it falls between
 * `$sm` 8px and `$card` 20px and is single-use — no new global token).
 *
 * A11y: label drives `accessibilityLabel`; error string is announced via
 * `accessibilityLiveRegion="polite"`; the password toggle is a 48px button.
 * The reveal toggle renders localised "Show" / "Hide" text (not emoji) per
 * align-login M-06/M-07, positioned inside the label row above the input.
 */
import { colors } from '@learnexia/design-system';
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
  autoCorrect?: boolean;
  disabled?: boolean;
  testID?: string;
  accessibilityLabel?: string;
  /** Layout direction; pass `useDirection(locale)` result, or a locale string. */
  direction?: Direction;
  locale?: string;
  /**
   * Force the input VALUE to render left-to-right regardless of locale — for
   * technical strings (email, phone, URLs) that must stay Latin + LTR per
   * SKILL.md, even inside an RTL form. The label still follows `direction`.
   * Also auto-forced when `keyboardType='email-address'` or
   * `autoComplete='email'` / `'tel'`.
   * `forceValueLtr` is the canonical name; `forceLtr` is kept as an alias.
   */
  forceValueLtr?: boolean;
  /** @deprecated Use `forceValueLtr`. */
  forceLtr?: boolean;
  /**
   * Localized label shown on the reveal toggle when the password is hidden.
   * Defaults to 'Show'. Callers should pass `t('auth.login.showPassword')`.
   */
  showLabel?: string;
  /**
   * Localized label shown on the reveal toggle when the password is visible.
   * Defaults to 'Hide'. Callers should pass `t('auth.login.hidePassword')`.
   */
  hideLabel?: string;
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

/** Returns true if the field type implies the value must stay LTR (email/tel). */
function isLtrField(
  keyboardType?: KeyboardTypeOptions,
  autoComplete?: TextInputProps['autoComplete'],
): boolean {
  if (keyboardType === 'email-address' || keyboardType === 'phone-pad') return true;
  if (autoComplete === 'email' || autoComplete === 'tel') return true;
  return false;
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
  autoCorrect,
  disabled = false,
  testID,
  accessibilityLabel,
  direction,
  locale,
  forceValueLtr = false,
  forceLtr = false,
  showLabel = 'Show',
  hideLabel = 'Hide',
}: TextFieldProps) {
  const dir = resolveDirection(direction, locale);
  const [focused, setFocused] = useState(false);
  const [revealed, setRevealed] = useState(false);

  const hasError = Boolean(error);
  const filled = value.length > 0;

  // Email/phone/url values stay Latin + LTR even in an RTL form (SKILL.md).
  // Auto-force when keyboardType or autoComplete signals an email/phone field.
  const shouldForceLtr = forceValueLtr || forceLtr || isLtrField(keyboardType, autoComplete);
  // Email/phone keep an LTR *writing direction* so "user@host.com" renders in the
  // correct order — but the text still aligns to the form's start edge (RIGHT in
  // RTL), so an Arabic email field reads from the right like every other field.
  const valueDir: Direction = shouldForceLtr ? 'ltr' : dir;
  const valueAlign = dir === 'rtl' ? 'right' : 'left';

  // Border / shadow per state (Design Spec §2.1 States table).
  const borderColor = hasError
    ? '$danger'
    : focused
      ? '$borderFocus'
      : filled
        ? '$borderStrong'
        : '$borderInput';

  // Glow colors use design-system tokens: $dangerSoft (0.18) for error,
  // $primarySoft (0.18) for focus — close to the visual 0.20/0.25 intent.
  const glowColor = hasError
    ? colors.dangerSoft
    : focused
      ? colors.primarySoft
      : 'transparent';

  return (
    <YStack gap="$1" opacity={disabled ? 0.5 : 1} pointerEvents={disabled ? 'none' : 'auto'}>
      {/*
       * Label row: label text on the leading side, reveal toggle on the trailing
       * side (only when secureTextEntry). Direction-aware (RTL flips via rowDir).
       * align-login M-06/M-07: "Show"/"Hide" text (localised), positioned in the
       * label row, NOT inside the input overlay.
       */}
      <XStack
        flexDirection={dir === 'rtl' ? 'row-reverse' : 'row'}
        justifyContent="space-between"
        alignItems="center"
      >
        <Text
          color="$fg3"
          fontSize={12}
          fontWeight="600"
          fontFamily="$heading"
          textTransform="uppercase"
          letterSpacing={0.72}
          textAlign={dir === 'rtl' ? 'right' : 'left'}
          writingDirection={dir}
        >
          {label}
        </Text>

        {secureTextEntry ? (
          <Stack
            cursor="pointer"
            hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
            onPress={() => setRevealed((r) => !r)}
            accessibilityRole="button"
            accessible
            accessibilityLabel={revealed ? hideLabel : showLabel}
            aria-label={revealed ? hideLabel : showLabel}
          >
            <Text
              color="$primaryLight"
              fontSize={12}
              fontWeight="600"
              fontFamily="$heading"
            >
              {revealed ? hideLabel : showLabel}
            </Text>
          </Stack>
        ) : null}
      </XStack>

      <Stack
        position="relative"
        height={48}
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
          placeholderTextColor={colors.fg3}
          secureTextEntry={secureTextEntry && !revealed}
          keyboardType={keyboardType}
          autoCapitalize={autoCapitalize}
          autoComplete={autoComplete}
          autoCorrect={autoCorrect}
          editable={!disabled}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          accessibilityLabel={accessibilityLabel ?? label}
          style={{
            height: 48,
            paddingStart: 14,
            paddingEnd: 14,
            color: colors.fg1,
            fontSize: 15,
            textAlign: valueAlign,
            writingDirection: valueDir,
          }}
        />
      </Stack>

      {hasError ? (
        <XStack
          gap="$1"
          marginTop={2}
          accessibilityLiveRegion="polite"
          flexDirection={dir === 'rtl' ? 'row-reverse' : 'row'}
        >
          <Text color="$danger" fontSize={12} fontFamily="$body" textAlign={dir === 'rtl' ? 'right' : 'left'} writingDirection={dir}>
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

/**
 * Convenience re-export of the prop type so callers can reference the canonical
 * `forceValueLtr` name in their code without having to import `TextFieldProps`.
 */
export type { TextFieldProps as FormFieldProps };
