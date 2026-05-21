/**
 * Card — default + soft surface variants.
 *
 * Design Spec §4.2. Token-driven. Default variant lifts shadow soft → float on
 * web hover (Tamagui `hoverStyle` + `animation`). No scale on hover (only
 * Buttons scale). If `onPress` is supplied the card becomes pressable with a
 * softer 0.98 press scale and a button role + 48px min height.
 */
import { Stack, styled, type GetProps } from '@tamagui/core';
import React from 'react';

const CardFrame = styled(Stack, {
  name: 'LxCard',
  flexDirection: 'column',
  borderRadius: '$card',
  borderWidth: 1,
  borderColor: '$border',
  padding: '$4',

  variants: {
    variant: {
      default: {
        backgroundColor: '$card',
        // soft → float elevation on hover (web)
        shadowColor: '#000',
        shadowOpacity: 0.15,
        shadowRadius: 12,
        shadowOffset: { width: 0, height: 4 },
        hoverStyle: {
          shadowOpacity: 0.25,
          shadowRadius: 24,
          shadowOffset: { width: 0, height: 8 },
        },
      },
      soft: {
        backgroundColor: '$cardSoft',
      },
    },
    size: {
      md: { padding: '$4' },
      lg: { padding: '$6' },
    },
    pressable: {
      true: {
        cursor: 'pointer',
        minHeight: 48,
        pressStyle: { scale: 0.98 },
      },
    },
  } as const,

  defaultVariants: {
    variant: 'default',
    size: 'md',
  },
});

export type CardVariant = 'default' | 'soft';

export interface CardProps extends Omit<GetProps<typeof CardFrame>, 'variant'> {
  variant?: CardVariant;
  children?: React.ReactNode;
  accessibilityLabel?: string;
}

export function Card({ variant = 'default', onPress, accessibilityLabel, ...rest }: CardProps) {
  const isPressable = Boolean(onPress);
  return (
    <CardFrame
      variant={variant}
      pressable={isPressable}
      onPress={onPress}
      accessibilityRole={isPressable ? 'button' : undefined}
      accessible={isPressable || Boolean(accessibilityLabel)}
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
      {...rest}
    />
  );
}

Card.displayName = 'Card';
