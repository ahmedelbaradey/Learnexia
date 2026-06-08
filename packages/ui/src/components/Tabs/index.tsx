/**
 * Tabs — controlled vertical tab list (left-rail) for the parent Settings screen
 * (capture `web/07-settings.png`). P1-11-FE-14.
 *
 * Mirrors the existing component shapes (Sidebar nav rows, Select option rows):
 * a token-driven list of pressable rows with an active-state pill
 * (`$primarySoftStrong` background + `$primaryLight` label, `$nav` 12px radius,
 * no border stripe). Each item carries an icon glyph + an already-i18n-resolved
 * label (labels are NOT hardcoded here — the caller injects them).
 *
 * Controlled: the caller owns `value` + `onChange` and decides which panel to
 * render beside the rail. RTL via `direction` (logical row).
 *
 * A11y: `tablist` / `tab` roles, `accessibilityState.selected`, required label.
 */
import { type Direction } from '@learnexia/shared/i18n';
import React from 'react';

import { YStack, XStack, Text } from '../../internal/primitives';

export type TabValue = string;

export interface TabItem {
  /** Stable identity (an enum/const value, never a raw label). */
  value: TabValue;
  /** Already-localized label (caller resolves `t(...)`). */
  label: string;
  /** Optional leading glyph (decorative). */
  icon?: string;
  /** Optional testID for the tab row (non-user-facing technical identifier). */
  testID?: string;
}

export interface TabsProps {
  items: TabItem[];
  value: TabValue;
  onChange: (value: TabValue) => void;
  direction?: Direction;
  /** a11y label for the tablist (already localized). */
  accessibilityLabel: string;
  testID?: string;
}

export function Tabs({
  items,
  value,
  onChange,
  direction = 'ltr',
  accessibilityLabel,
  testID,
}: TabsProps) {
  const isRtl = direction === 'rtl';
  const rowDir = isRtl ? 'row-reverse' : 'row';

  return (
    <YStack
      testID={testID}
      gap="$1"
      accessibilityRole="tablist"
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {items.map((item) => {
        const isActive = item.value === value;
        return (
          <XStack
            key={item.value}
            testID={item.testID}
            flexDirection={rowDir}
            alignItems="center"
            gap="$3"
            minHeight={40}
            paddingVertical={10}
            paddingHorizontal={14}
            // Keep the 48px touch target via hitSlop while the visual height is ~40px.
            hitSlop={{ top: 4, bottom: 4 }}
            borderRadius="$nav"
            backgroundColor={isActive ? '$primarySoftStrong' : 'transparent'}
            hoverStyle={{ backgroundColor: isActive ? '$primarySoftStrong' : '$cardSoft' }}
            pressStyle={{ scale: 0.95 }}
            cursor="pointer"
            onPress={() => onChange(item.value)}
            accessibilityRole="tab"
            accessible
            accessibilityState={{ selected: isActive }}
            accessibilityLabel={item.label}
            aria-label={item.label}
          >
            {item.icon ? (
              <Text fontSize={16} accessibilityElementsHidden>
                {item.icon}
              </Text>
            ) : null}
            <Text
              flex={1}
              color={isActive ? '$primaryLight' : '$fg3'}
              fontSize={14}
              fontWeight={isActive ? '800' : '600'}
              fontFamily="$heading"
              writingDirection={direction}
            >
              {item.label}
            </Text>
          </XStack>
        );
      })}
    </YStack>
  );
}

Tabs.displayName = 'Tabs';
