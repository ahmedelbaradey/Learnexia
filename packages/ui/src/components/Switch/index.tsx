/**
 * Switch — a 44×26 pill-track toggle (web PWA) that mirrors `CheckboxField`'s
 * prop shape. Pulled directly from the W10-P2-12 Design Spec §0.
 *
 * Visual: `$primary` track when ON with primary glow, `$cardSoft` when OFF.
 * Thumb: 20×20 px white circle (`$fg1`), 3 px inset from the active end.
 * Motion: 160 ms `cubic-bezier(0.16,1,0.3,1)` on thumb + track colour.
 * Focus: 2 px `$primary` outline + outer 4 px `$primaryGlow`.
 * Disabled: full wrapper `opacity: 0.4`, `pointer-events: none`.
 * Touch target: min 44×44 px (`hitSlop` + centred track).
 *
 * NO design-pattern abstraction beyond a plain token-driven wrapper.
 * If you need a ToggleGroup, stop and ask the lead (CLAUDE.md rule #8).
 *
 * RTL: track row reversal is owned by the parent container's `flexDirection`.
 * Thumb position uses `insetInlineStart` via the web `style` prop (logical CSS);
 * on native the `left` value is flipped from the `direction` prop.
 */
import { directionForLocale, type Direction } from '@learnexia/shared/i18n';
import { Platform } from 'react-native';
import React from 'react';

import { Stack, XStack, Text } from '../../internal/primitives';

const TRACK_W = 44;
const TRACK_H = 26;
const THUMB_SIZE = 20;
const THUMB_INSET = 3;
const THUMB_ON_NATIVE = TRACK_W - THUMB_SIZE - THUMB_INSET; // 21 px from start

export interface SwitchProps {
  value: boolean;
  onValueChange: (next: boolean) => void;
  /** Optional inline label rendered to the logical START of the track. */
  label?: string;
  /** If true, label is visually hidden but kept in DOM for a11y. */
  hideLabel?: boolean;
  disabled?: boolean;
  direction?: Direction;
  /** Resolved → direction if `direction` absent. */
  locale?: string;
  /** REQUIRED for a11y — composed by callers as "{categoryLabel} — {channelLabel}". */
  accessibilityLabel: string;
  testID?: string;
}

function resolveDirection(direction?: Direction, locale?: string): Direction {
  if (direction) return direction;
  if (locale) return directionForLocale(locale);
  return 'ltr';
}

const isWeb = Platform.OS === 'web';

export function Switch({
  value,
  onValueChange,
  label,
  hideLabel = false,
  disabled = false,
  direction,
  locale,
  accessibilityLabel,
  testID,
}: SwitchProps) {
  const dir = resolveDirection(direction, locale);

  // Native thumb position (px from track start, logical-start in LTR).
  // In RTL on native, "start" is the right side, so we flip.
  const nativeThumbLeft =
    dir === 'rtl'
      ? value
        ? THUMB_INSET
        : THUMB_ON_NATIVE
      : value
        ? THUMB_ON_NATIVE
        : THUMB_INSET;

  // Web-only inline style for track (transition + glow).
  const trackWebStyle = isWeb
    ? {
        transition: `background-color 160ms cubic-bezier(0.16,1,0.3,1)`,
        ...(value ? { boxShadow: '0 0 12px rgba(99,102,241,0.40)' } : {}),
      }
    : undefined;

  // Web-only inline style for thumb (logical-CSS insetInlineStart).
  const thumbWebStyle = isWeb
    ? {
        insetInlineStart: value ? THUMB_ON_NATIVE : THUMB_INSET,
        transition: `inset-inline-start 160ms cubic-bezier(0.16,1,0.3,1)`,
        boxShadow: '0 2px 6px rgba(0,0,0,0.30)',
      }
    : undefined;

  return (
    <Stack
      testID={testID}
      opacity={disabled ? 0.4 : 1}
      pointerEvents={disabled ? 'none' : 'auto'}
      minWidth={44}
      minHeight={44}
      alignItems="center"
      justifyContent="center"
    >
      <XStack alignItems="center" gap="$2">
        {/* Label — rendered at logical-start of track */}
        {label ? (
          <Text
            color="$fg2"
            fontSize={13}
            fontFamily="$body"
            writingDirection={dir}
            textAlign={dir === 'rtl' ? 'right' : 'left'}
            opacity={hideLabel ? 0 : 1}
          >
            {label}
          </Text>
        ) : null}

        {/* Track + thumb */}
        <Stack
          width={TRACK_W}
          height={TRACK_H}
          borderRadius={9999}
          backgroundColor={value ? '$primary' : '$cardSoft'}
          cursor={disabled ? 'not-allowed' : 'pointer'}
          pressStyle={disabled ? undefined : { scale: 0.95 }}
          onPress={disabled ? undefined : () => onValueChange(!value)}
          accessibilityRole="switch"
          accessible
          accessibilityState={{ checked: value, disabled }}
          accessibilityLabel={accessibilityLabel}
          aria-label={accessibilityLabel}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
          style={trackWebStyle}
          // Native ON-state glow (web handled via style.boxShadow above)
          {...(!isWeb && value
            ? {
                shadowColor: '$primaryGlow',
                shadowRadius: 8,
                shadowOpacity: 1,
                shadowOffset: { width: 0, height: 0 },
              }
            : {})}
        >
          {/* Thumb */}
          {isWeb ? (
            <Stack
              width={THUMB_SIZE}
              height={THUMB_SIZE}
              borderRadius={9999}
              backgroundColor="$fg1"
              position="absolute"
              top={THUMB_INSET}
              style={thumbWebStyle}
            />
          ) : (
            // Native: absolute left derived from direction + value
            <Stack
              width={THUMB_SIZE}
              height={THUMB_SIZE}
              borderRadius={9999}
              backgroundColor="$fg1"
              position="absolute"
              top={THUMB_INSET}
              left={nativeThumbLeft}
              shadowColor="$cardSoft"
              shadowRadius={6}
              shadowOpacity={0.3}
              shadowOffset={{ width: 0, height: 2 }}
            />
          )}
        </Stack>
      </XStack>
    </Stack>
  );
}

Switch.displayName = 'Switch';
