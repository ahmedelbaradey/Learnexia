/**
 * Tabs — controlled vertical tab list (left-rail) for the parent Settings screen
 * (capture `web/07-settings.png`). P1-11-FE-14.
 *
 * Mirrors the existing component shapes (Sidebar nav rows, Select option rows):
 * a token-driven list of pressable rows with an active-state pill (`$primarySoft`
 * background + bold `$fg1` label) and a logical START accent border, like the
 * Sidebar's active nav item. Each item carries an icon glyph + an already-i18n-
 * resolved label (labels are NOT hardcoded here — the caller injects them).
 *
 * Controlled: the caller owns `value` + `onChange` and decides which panel to
 * render beside the rail. RTL via `direction` (logical row + START accent).
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
            flexDirection={rowDir}
            alignItems="center"
            gap="$3"
            minHeight={48}
            paddingHorizontal="$3"
            borderRadius="$button"
            backgroundColor={isActive ? '$primarySoft' : 'transparent'}
            borderStartWidth={isActive ? 3 : 0}
            borderStartColor={isActive ? '$primary' : 'transparent'}
            hoverStyle={{ backgroundColor: isActive ? '$primarySoft' : '$card' }}
            pressStyle={{ scale: 0.98 }}
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
              color={isActive ? '$fg1' : '$fg2'}
              fontSize={15}
              fontWeight={isActive ? '700' : '500'}
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
