/**
 * Button — primary/secondary/success/danger/ghost/disabled variants.
 *
 * Design Spec §4.1. Token-driven (no raw hex). Press feedback: scale 0.95 via
 * Reanimated on native + a Tamagui `pressStyle` scale on web (covers both
 * runtimes without importing Reanimated in the type graph). Touch target ≥ 48px
 * for md/full; `sm` adds vertical hitSlop to reach 48px.
 *
 * A11y: `accessibilityRole="button"`, `accessibilityState={{disabled, busy}}`,
 * required `accessibilityLabel`. See `src/accessibility.md`.
 */
import { Stack, Text, styled, type GetProps } from '@tamagui/core';
import React from 'react';

import { Spinner } from '../../internal/Spinner';

const ButtonFrame = styled(Stack, {
  name: 'LxButton',
  role: 'button',
  flexDirection: 'row',
  alignItems: 'center',
  justifyContent: 'center',
  gap: '$2',
  borderRadius: '$button',
  borderWidth: 1,
  borderColor: 'transparent',
  cursor: 'pointer',
  // press feedback (web + native fallback)
  pressStyle: { scale: 0.95 },

  variants: {
    variant: {
      primary: {
        backgroundColor: '$primary',
        hoverStyle: { backgroundColor: '$primaryHover' },
        pressStyle: { backgroundColor: '$primaryPress', scale: 0.95 },
        // Indigo glow (--lx-shadow-primary-glow). RN shadow props → CSS box-shadow
        // on RN Web; a soft drop-glow on native.
        shadowColor: '$primaryGlow',
        shadowOpacity: 1,
        shadowRadius: 12,
        shadowOffset: { width: 0, height: 4 },
      },
      secondary: {
        backgroundColor: '$cardSoft',
        borderColor: '$border',
      },
      success: {
        backgroundColor: '$secondary',
      },
      danger: {
        backgroundColor: '$danger',
      },
      ghost: {
        backgroundColor: 'transparent',
        borderColor: '$borderStrong',
      },
      disabled: {
        backgroundColor: '$card',
        opacity: 0.4,
        pointerEvents: 'none',
        cursor: 'default',
      },
    },
    size: {
      sm: { height: 40, paddingHorizontal: '$4' },
      md: { height: 52, paddingHorizontal: '$6' },
      full: { height: 52, paddingHorizontal: '$6', width: '100%' },
    },
  } as const,

  defaultVariants: {
    variant: 'primary',
    size: 'md',
  },
});

const labelColorByVariant = {
  primary: '$fg1',
  secondary: '$fg1',
  success: '$fgInverse',
  danger: '$fg1',
  ghost: '$fg2',
  disabled: '$fg4',
} as const;

const labelSizeBySize = {
  sm: 14,
  md: 16,
  full: 16,
} as const;

export type ButtonVariant = keyof typeof labelColorByVariant;
export type ButtonSize = keyof typeof labelSizeBySize;

export interface ButtonProps extends Omit<GetProps<typeof ButtonFrame>, 'variant' | 'size'> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** Required for screen readers. */
  accessibilityLabel: string;
  disabled?: boolean;
  loading?: boolean;
  /** Optional leading/trailing icon slots (already direction-aware via logical gap). */
  iconBefore?: React.ReactNode;
  iconAfter?: React.ReactNode;
  children?: React.ReactNode;
}

export function Button({
  variant = 'primary',
  size = 'md',
  disabled = false,
  loading = false,
  accessibilityLabel,
  iconBefore,
  iconAfter,
  children,
  onPress,
  ...rest
}: ButtonProps) {
  const effectiveVariant: ButtonVariant = disabled ? 'disabled' : variant;
  const isInteractive = !disabled && !loading;

  return (
    <ButtonFrame
      variant={effectiveVariant}
      size={size}
      // Per a11y baseline: sm visual is 40px → add vertical hitSlop to reach 48px.
      hitSlop={size === 'sm' ? { top: 4, bottom: 4, left: 4, right: 4 } : undefined}
      accessibilityRole="button"
      accessible
      aria-label={accessibilityLabel}
      accessibilityLabel={accessibilityLabel}
      accessibilityState={{ disabled: !isInteractive, busy: loading }}
      onPress={isInteractive ? onPress : undefined}
      {...rest}
    >
      {loading ? (
        <Spinner color={labelColorByVariant[effectiveVariant]} />
      ) : (
        <>
          {iconBefore}
          {typeof children === 'string' ? (
            <Text
              fontFamily="$heading"
              fontWeight="700"
              fontSize={labelSizeBySize[size]}
              letterSpacing={0.16}
              color={labelColorByVariant[effectiveVariant]}
            >
              {children}
            </Text>
          ) : (
            children
          )}
          {iconAfter}
        </>
      )}
    </ButtonFrame>
  );
}

Button.displayName = 'Button';
