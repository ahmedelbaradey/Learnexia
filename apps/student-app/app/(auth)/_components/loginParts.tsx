/**
 * loginParts — small presentational pieces for the Login screen (P1-11):
 * a Checkbox (Remember me), an "OR" divider, the social-auth button row, and
 * the web brand panel. All token-driven, RTL-aware, no raw hex / free-text.
 * Social buttons are UI-only (no-op TODO handlers — no faked auth).
 */
import { type Direction } from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';
import type { ReactNode } from 'react';

/* ------------------------------------------------------------------ */
/* Checkbox (Remember me)                                              */
/* ------------------------------------------------------------------ */

export interface CheckboxProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
  direction?: Direction;
  disabled?: boolean;
}

export function Checkbox({ checked, onChange, label, direction = 'ltr', disabled = false }: CheckboxProps) {
  return (
    <Stack
      flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'}
      alignItems="center"
      gap="$2"
      minHeight={48}
      cursor="pointer"
      onPress={() => onChange(!checked)}
      accessibilityRole="checkbox"
      accessible
      accessibilityState={{ checked, disabled }}
      accessibilityLabel={label}
      aria-label={label}
      opacity={disabled ? 0.5 : 1}
      pointerEvents={disabled ? 'none' : 'auto'}
    >
      <Stack
        width={20}
        height={20}
        borderRadius="$sm"
        borderWidth={2}
        borderColor={checked ? '$primary' : '$borderStrong'}
        backgroundColor={checked ? '$primary' : 'transparent'}
        alignItems="center"
        justifyContent="center"
      >
        {checked ? (
          <Text color="$fg1" fontSize={12} fontWeight="700" accessibilityElementsHidden>
            ✓
          </Text>
        ) : null}
      </Stack>
      <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
        {label}
      </Text>
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* OR divider                                                          */
/* ------------------------------------------------------------------ */

export interface OrDividerProps {
  label: string;
  direction?: Direction;
}

export function OrDivider({ label, direction = 'ltr' }: OrDividerProps) {
  return (
    <Stack
      flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'}
      alignItems="center"
      gap="$3"
      accessibilityElementsHidden
    >
      <Stack flex={1} height={1} backgroundColor="$border" />
      <Text
        color="$fg3"
        fontSize={12}
        fontWeight="600"
        fontFamily="$heading"
        textTransform="uppercase"
        letterSpacing={1}
        writingDirection={direction}
      >
        {label}
      </Text>
      <Stack flex={1} height={1} backgroundColor="$border" />
    </Stack>
  );
}

/* ------------------------------------------------------------------ */
/* Social auth buttons (UI-only — no-op handlers / TODO)               */
/* ------------------------------------------------------------------ */

export interface SocialButtonProps {
  label: string;
  glyph: string;
  onPress: () => void;
  direction?: Direction;
}

export function SocialButton({ label, glyph, onPress, direction = 'ltr' }: SocialButtonProps) {
  return (
    <Stack
      flex={1}
      height={48}
      flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'}
      alignItems="center"
      justifyContent="center"
      gap="$2"
      backgroundColor="$card"
      borderRadius="$button"
      borderWidth={1}
      borderColor="$border"
      cursor="pointer"
      hoverStyle={{ backgroundColor: '$cardSoft' }}
      pressStyle={{ scale: 0.97 }}
      onPress={onPress}
      accessibilityRole="button"
      accessible
      accessibilityLabel={label}
      aria-label={label}
    >
      <Text fontSize={16} accessibilityElementsHidden>
        {glyph}
      </Text>
      <Text color="$fg1" fontSize={14} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
        {label}
      </Text>
    </Stack>
  );
}

export interface SocialRowProps {
  direction?: Direction;
  children: ReactNode;
}

export function SocialRow({ direction = 'ltr', children }: SocialRowProps) {
  return (
    <Stack flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'} gap="$3">
      {children}
    </Stack>
  );
}
