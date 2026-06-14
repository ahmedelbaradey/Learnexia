/**
 * loginParts — small presentational pieces for the Login screen (P1-11 +
 * P1-12 Batch 3): Checkbox (Remember me), OR divider, social-auth buttons, and
 * the web brand panel. All token-driven, RTL-aware, no raw hex / free-text.
 *
 * P1-12-FE-3: `SocialButton` extended with `loading` and `disabled` props to
 * support the Google OAuth in-flight and Apple/Microsoft placeholder states.
 */
import { type Direction } from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';
import { MotiView } from 'moti';
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
  /**
   * When true, swaps the icon for a spinner, dims the label, removes press
   * feedback, and sets aria-busy. Enforces one-action-at-a-time by disabling
   * the other social buttons externally (see LoginForm Google flow).
   */
  loading?: boolean;
  /**
   * When true, the button is fully non-interactive (opacity 0.5, no press
   * feedback). Used for Apple + Microsoft placeholder buttons.
   */
  disabled?: boolean;
  /**
   * Force the label to render LTR regardless of the ambient direction.
   * Use this for Latin brand names (Google) that must not flip in RTL.
   */
  labelLtr?: boolean;
  testID?: string;
}

export function SocialButton({
  label,
  icon,
  onPress,
  direction = 'ltr',
  loading = false,
  disabled = false,
  labelLtr = false,
  testID,
}: SocialButtonProps) {
  const isInteractive = !loading && !disabled;
  const labelDirection = labelLtr ? 'ltr' : direction;

  return (
    <Stack
      testID={testID}
      flex={1}
      height={48}
      flexDirection="row"
      alignItems="center"
      justifyContent="center"
      gap="$2"
      backgroundColor="$card"
      borderRadius={socialButtonRadius}
      borderWidth={1}
      borderColor="$border"
      cursor={isInteractive ? 'pointer' : 'default'}
      hoverStyle={isInteractive ? { backgroundColor: '$cardSoft' } : undefined}
      pressStyle={isInteractive ? { scale: 0.95 } : undefined}
      onPress={isInteractive ? onPress : undefined}
      opacity={disabled ? 0.5 : 1}
      accessibilityRole="button"
      accessible
      accessibilityLabel={label}
      aria-label={label}
      accessibilityState={{ disabled, busy: loading }}
      aria-disabled={disabled}
      aria-busy={loading}
    >
      <Stack accessibilityElementsHidden aria-hidden>
        {loading ? (
          /* Spinner replaces icon in-place — no layout shift (same 20px slot).
             Moti opacity pulse (same pattern as packages/ui Spinner). */
          <MotiView
            from={{ opacity: 0.3 }}
            animate={{ opacity: 1 }}
            transition={{ type: 'timing', duration: 300, loop: true, repeatReverse: true }}
          >
            <Stack
              width={20}
              height={20}
              borderRadius={9999}
              borderWidth={2}
              borderColor="$fg1"
              borderTopColor="transparent"
            />
          </MotiView>
        ) : (
          icon
        )}
      </Stack>
      <Text
        color={loading ? '$fg3' : '$fg1'}
        fontSize={14}
        fontWeight="700"
        fontFamily="$heading"
        writingDirection={labelDirection}
      >
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
