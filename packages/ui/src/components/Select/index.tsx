/**
 * Select / GradePicker / LanguageSelect — controlled universal picker.
 * Design Spec §2.2.
 *
 * Cross-platform strategy: a styled Pressable trigger that toggles an
 * absolutely-positioned options panel rendered below it. This avoids depending
 * on a native-only Sheet/Modal so the component type-checks and renders in a
 * Node smoke env and on RN Web identically. On native the panel overlays inline
 * (sufficient for the short grade/language lists in P1-09).
 *
 * `GradePicker` and `LanguageSelect` are thin wrappers that the app feeds with
 * i18n-resolved option labels (labels are NOT hardcoded in the component).
 *
 * `inputRadius = 14` local constant (Design Spec Gap 9), matching TextField.
 */
import { directionForLocale, type Direction } from '@learnexia/shared/i18n';
import { Stack } from '@tamagui/core';
import React, { useState } from 'react';

import { XStack, YStack, Text } from '../../internal/primitives';

const inputRadius = 14;

export type SelectValue = string | number;

export interface SelectOption {
  value: SelectValue;
  label: string;
}

export interface SelectProps {
  label: string;
  value: SelectValue | null;
  onChange: (value: SelectValue) => void;
  options: SelectOption[];
  placeholder?: string;
  error?: string;
  disabled?: boolean;
  direction?: Direction;
  locale?: string;
  accessibilityLabel?: string;
  /**
   * Suppress the visible stacked label for compact inline usage (e.g. the
   * page-header period picker) — the label still feeds the a11y name. GAP-05.
   */
  hideLabel?: boolean;
  /**
   * Trigger height: `md` (48px, default form size) or `sm` (40px, compact
   * page-header period picker — align-settings DG-03). `sm` keeps a 48px touch
   * target via hitSlop.
   */
  size?: 'sm' | 'md';
  testID?: string;
}

const TRIGGER_HEIGHT = { sm: 40, md: 48 } as const;

function resolveDirection(direction?: Direction, locale?: string): Direction {
  if (direction) return direction;
  if (locale) return directionForLocale(locale);
  return 'ltr';
}

export function Select({
  label,
  value,
  onChange,
  options,
  placeholder,
  error,
  disabled = false,
  direction,
  locale,
  accessibilityLabel,
  hideLabel = false,
  size = 'md',
  testID,
}: SelectProps) {
  const dir = resolveDirection(direction, locale);
  const [open, setOpen] = useState(false);
  const hasError = Boolean(error);
  const selected = options.find((o) => o.value === value) ?? null;
  const isRtl = dir === 'rtl';
  const textAlign = isRtl ? 'right' : 'left';
  const triggerHeight = TRIGGER_HEIGHT[size];

  const borderColor = hasError
    ? '$danger'
    : open
      ? '$borderFocus'
      : selected
        ? '$borderStrong'
        : '$border';

  return (
    <YStack gap="$1" opacity={disabled ? 0.5 : 1} pointerEvents={disabled ? 'none' : 'auto'}>
      {hideLabel ? null : (
        <Text
          color="$fg3"
          fontSize={12}
          fontWeight="600"
          fontFamily="$heading"
          textTransform="uppercase"
          letterSpacing={0.6}
          textAlign={textAlign}
          writingDirection={dir}
        >
          {label}
        </Text>
      )}

      <Stack position="relative">
        <XStack
          testID={testID}
          height={triggerHeight}
          hitSlop={size === 'sm' ? { top: 4, bottom: 4 } : undefined}
          alignItems="center"
          justifyContent="space-between"
          flexDirection={dir === 'rtl' ? 'row-reverse' : 'row'}
          paddingHorizontal={14}
          borderRadius={inputRadius}
          borderWidth={1}
          borderColor={borderColor}
          backgroundColor="$card"
          shadowColor={open ? 'rgba(99,102,241,0.25)' : 'transparent'}
          shadowOpacity={open ? 1 : 0}
          shadowRadius={4}
          cursor="pointer"
          onPress={() => setOpen((o) => !o)}
          accessibilityRole="combobox"
          accessible
          accessibilityState={{ expanded: open }}
          accessibilityLabel={accessibilityLabel ?? label}
          aria-label={accessibilityLabel ?? label}
        >
          <Text
            flex={1}
            color={selected ? '$fg1' : '$fg3'}
            fontSize={15}
            fontFamily="$body"
            textAlign={textAlign}
            writingDirection={dir}
          >
            {selected ? selected.label : (placeholder ?? '')}
          </Text>
          <Text fontSize={14} color="$fg3" accessibilityElementsHidden scaleY={open ? -1 : 1}>
            {'▾'}
          </Text>
        </XStack>

        {open ? (
          <YStack
            position="absolute"
            top={triggerHeight + 4}
            start={0}
            end={0}
            zIndex={1000}
            backgroundColor="$card"
            borderRadius="$sm"
            borderWidth={1}
            borderColor="$borderStrong"
            shadowColor="#000"
            shadowOpacity={0.3}
            shadowRadius={16}
            shadowOffset={{ width: 0, height: 8 }}
            overflow="hidden"
          >
            {options.map((opt) => {
              const isSelected = opt.value === value;
              return (
                <XStack
                  key={String(opt.value)}
                  minHeight={52}
                  alignItems="center"
                  justifyContent="space-between"
                  flexDirection={dir === 'rtl' ? 'row-reverse' : 'row'}
                  paddingHorizontal="$4"
                  backgroundColor={isSelected ? '$primarySoft' : '$card'}
                  hoverStyle={{ backgroundColor: '$cardSoft' }}
                  pressStyle={{ backgroundColor: '$cardSoft' }}
                  cursor="pointer"
                  onPress={() => {
                    onChange(opt.value);
                    setOpen(false);
                  }}
                  accessibilityRole="radio"
                  accessible
                  accessibilityState={{ selected: isSelected }}
                  accessibilityLabel={opt.label}
                  aria-label={opt.label}
                >
                  <Text
                    color={isSelected ? '$primaryLight' : '$fg1'}
                    fontSize={15}
                    fontWeight="500"
                    fontFamily="$body"
                    textAlign={textAlign}
                    writingDirection={dir}
                  >
                    {opt.label}
                  </Text>
                  {isSelected ? (
                    <Text color="$primaryLight" fontSize={16} accessibilityElementsHidden>
                      {'✓'}
                    </Text>
                  ) : null}
                </XStack>
              );
            })}
          </YStack>
        ) : null}
      </Stack>

      {hasError ? (
        <Text
          color="$danger"
          fontSize={12}
          fontFamily="$body"
          marginTop={2}
          textAlign={textAlign}
          writingDirection={dir}
          accessibilityLiveRegion="polite"
        >
          {error}
        </Text>
      ) : null}
    </YStack>
  );
}

Select.displayName = 'Select';

/* ------------------------------------------------------------------ */
/* GradePicker — Select pre-shaped for grades. App injects labels.     */
/* ------------------------------------------------------------------ */

export interface GradePickerProps extends Omit<SelectProps, 'options'> {
  /** Grade options 1–6 with localized labels (from `onboarding.grade.*`). */
  options: SelectOption[];
}

export function GradePicker(props: GradePickerProps) {
  return <Select {...props} />;
}

GradePicker.displayName = 'GradePicker';

/* ------------------------------------------------------------------ */
/* LanguageSelect — Select pre-shaped for ar/en. App injects labels.   */
/* ------------------------------------------------------------------ */

export interface LanguageSelectProps extends Omit<SelectProps, 'options'> {
  options: SelectOption[];
}

export function LanguageSelect(props: LanguageSelectProps) {
  return <Select {...props} />;
}

LanguageSelect.displayName = 'LanguageSelect';
