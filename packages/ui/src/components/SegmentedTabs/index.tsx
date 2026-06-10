/**
 * SegmentedTabs — horizontal segmented-control variant of the Tabs primitive.
 *
 * Design Spec §3.6. Mirrors the existing `Tabs` component grammar (active-pill
 * $primarySoftStrong bg + $primaryLight label + $nav radius) but laid out as a
 * horizontal row instead of a vertical list. Used for the Lessons | Skill Tree
 * switch inside `subjects/[subjectId]/_layout.tsx`.
 *
 * This is NOT a design pattern — it is an orientation variant that mirrors the
 * existing Tabs shape. Per CLAUDE.md rule 8, mirroring existing shapes does not
 * require approval.
 *
 * Dims (design spec §3.6):
 *   Container: full-width minus 32px margins, height 44, padding 4,
 *              radius $button (16), bg $card, border 1px $borderSubtle.
 *   Each segment: flex 1, padding 8px 14px, radius $nav (12), center-aligned.
 *   Active: bg $primarySoftStrong, label $primaryLight, font 14/700.
 *   Inactive: bg transparent, label $fg3, font 14/500.
 *   Hover (web): label $fg2, bg rgba(255,255,255,0.04).
 *   Press: scale 0.97, 80ms.
 *
 * A11y: tablist / tab roles, accessibilityState.selected.
 */
import type { Direction } from '@learnexia/shared/i18n';

import { Stack, XStack, Text } from '../../internal/primitives';

export type SegmentValue = string;

export interface SegmentItem {
  /** Stable identity (enum/const value, never a raw label). */
  value: SegmentValue;
  /** Already-localized label. */
  label: string;
  /** Optional decorative icon glyph. */
  icon?: string;
}

export interface SegmentedTabsProps {
  items: SegmentItem[];
  value: SegmentValue;
  onChange: (value: SegmentValue) => void;
  direction?: Direction;
  /** a11y label for the tablist (already localized). */
  accessibilityLabel: string;
  testID?: string;
  /**
   * When provided, each tab segment receives `testID={itemTestIDPrefix}-{item.value}`.
   * E.g. prefix="segmented-tab" → "segmented-tab-lessons", "segmented-tab-tree".
   * Used by E2E harness to target individual segments without copy selectors.
   */
  itemTestIDPrefix?: string;
}

export function SegmentedTabs({
  items,
  value,
  onChange,
  direction = 'ltr',
  accessibilityLabel,
  testID,
  itemTestIDPrefix,
}: SegmentedTabsProps) {
  const isRtl = direction === 'rtl';
  const rowDir = isRtl ? 'row-reverse' : 'row';

  return (
    <XStack
      testID={testID}
      flexDirection={rowDir}
      height={44}
      paddingHorizontal={4}
      paddingVertical={4}
      borderRadius="$button"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$borderSubtle"
      alignItems="center"
      accessibilityRole="tablist"
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {items.map((item) => {
        const isActive = item.value === value;
        const itemTestID = itemTestIDPrefix ? `${itemTestIDPrefix}-${item.value}` : undefined;
        return (
          <Stack
            key={item.value}
            testID={itemTestID}
            flex={1}
            height="100%"
            borderRadius="$nav"
            backgroundColor={isActive ? '$primarySoftStrong' : 'transparent'}
            alignItems="center"
            justifyContent="center"
            paddingHorizontal={14}
            paddingVertical={8}
            hoverStyle={{
              backgroundColor: isActive
                ? '$primarySoftStrong'
                : 'rgba(255,255,255,0.04)',
            }}
            pressStyle={{ scale: 0.97 }}
            cursor="pointer"
            onPress={() => onChange(item.value)}
            accessibilityRole="tab"
            accessible
            accessibilityState={{ selected: isActive }}
            accessibilityLabel={item.label}
            aria-label={item.label}
          >
            <XStack
              flexDirection={rowDir}
              alignItems="center"
              gap={6}
            >
              {item.icon ? (
                <Text fontSize={14} accessibilityElementsHidden>
                  {item.icon}
                </Text>
              ) : null}
              <Text
                color={isActive ? '$primaryLight' : '$fg3'}
                fontSize={14}
                fontWeight={isActive ? '700' : '500'}
                fontFamily="$heading"
                writingDirection={direction}
                hoverStyle={{ color: '$fg2' }}
              >
                {item.label}
              </Text>
            </XStack>
          </Stack>
        );
      })}
    </XStack>
  );
}

SegmentedTabs.displayName = 'SegmentedTabs';
