/**
 * loginParts — small presentational pieces for the Login screen (P1-11):
 * a Checkbox (Remember me), an "OR" divider, the social-auth button row, and
 * the web brand panel. All token-driven, RTL-aware, no raw hex / free-text.
 * Social buttons are UI-only (no-op TODO handlers — no faked auth).
 */
import { type Direction } from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';
import type { ReactNode } from 'react';

/**
 * Social-button + checkbox-box radii are LOCAL single-use constants (they fall
 * between `$sm` 8px and `$button` 16px — no exact global token; align-login M-10/m-10).
 */
const socialButtonRadius = 14;
const checkboxBoxRadius = 6;

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
        width={22}
        height={22}
        borderRadius={checkboxBoxRadius}
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
      <Text color="$fg2" fontSize={13} fontFamily="$body" writingDirection={direction}>
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
        color="$fg4"
        fontSize={12}
        fontWeight="600"
        fontFamily="$heading"
        textTransform="uppercase"
        letterSpacing={0.48}
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
  /** Brand icon node (e.g. `<GoogleIcon />`) — decorative, hidden from a11y. */
  icon: ReactNode;
  onPress: () => void;
  direction?: Direction;
}

export function SocialButton({ label, icon, onPress, direction = 'ltr' }: SocialButtonProps) {
  return (
    <Stack
      flex={1}
      height={48}
      flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'}
      alignItems="center"
      justifyContent="center"
      gap="$2"
      backgroundColor="$card"
      borderRadius={socialButtonRadius}
      borderWidth={1}
      borderColor="$border"
      cursor="pointer"
      hoverStyle={{ backgroundColor: '$cardSoft' }}
      pressStyle={{ scale: 0.95 }}
      onPress={onPress}
      accessibilityRole="button"
      accessible
      accessibilityLabel={label}
      aria-label={label}
    >
      <Stack accessibilityElementsHidden aria-hidden>
        {icon}
      </Stack>
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
