/**
 * Button — primary/secondary/success/danger/ghost/disabled variants.
 *
 * Design Spec §4.1. Token-driven (no raw hex). Press feedback: scale 0.95 via
 * Reanimated on native + a Tamagui `pressStyle` scale on web (covers both
 * runtimes without importing Reanimated in the type graph). Touch target ≥ 48px
 * for md/full; `sm` adds vertical hitSlop to reach 48px.
 *
 * Web-only: primary variant adds an inset highlight via CSS box-shadow
 * (`0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)`)
 * per align-login M-09. Native keeps the RN shadow props.
 *
 * Disabled: solid `$cardSoft` surface at full opacity (no translucency
 * artefact) — align-login M-08 / align-register B-3.
 *
 * A11y: `accessibilityRole="button"`, `accessibilityState={{disabled, busy}}`,
 * required `accessibilityLabel`. See `src/accessibility.md`.
 */
import { Stack, Text, styled, type GetProps } from '@tamagui/core';
import React from 'react';
import { Platform } from 'react-native';

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
        // Solid muted surface at full opacity (no translucency artefact).
        // align-login M-08 / align-register B-3: $cardSoft (#334155) is the
        // closest token to the design target; opacity stays at 1.
        backgroundColor: '$cardSoft',
        opacity: 1,
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
  testID?: string;
}

/** Web-only CSS box-shadow for the primary button (outer glow + inset highlight). */
const PRIMARY_WEB_SHADOW =
  '0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)';

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
  testID,
  ...rest
}: ButtonProps) {
  const effectiveVariant: ButtonVariant = disabled ? 'disabled' : variant;
  const isInteractive = !disabled && !loading;

  // Web-only: apply CSS box-shadow with inset highlight for primary CTA.
  // Native keeps the RN shadow props on the styled variant (align-login M-09).
  const webPrimaryShadow =
    Platform.OS === 'web' && effectiveVariant === 'primary'
      ? ({ boxShadow: PRIMARY_WEB_SHADOW } as Record<string, string>)
      : undefined;

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
      aria-busy={loading}
      aria-disabled={!isInteractive}
      testID={testID}
      onPress={isInteractive ? onPress : undefined}
      style={webPrimaryShadow}
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
